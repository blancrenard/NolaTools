#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Data;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator
{
    public partial class TexelMaskBaker
    {
        #region テクセルベイク

        private void BakeStep()
        {
            try
            {
                int processedThisFrame = 0;

                while (currentSubIndex < subDatas.Count && processedThisFrame < batchSize)
                {
                    var (tri, mat) = subDatas[currentSubIndex];
                    int triCount = tri.Length / 3;

                    if (!materialBuffers.TryGetValue(mat, out Color[] buffer))
                    {
                        currentSubIndex++;
                        currentTriIndex = 0;
                        continue;
                    }

                    var rasterizedPixels = materialRasterizedPixels[mat];

                    while (currentTriIndex < triCount && processedThisFrame < batchSize)
                    {
                        int i0 = tri[currentTriIndex * 3];
                        int i1 = tri[currentTriIndex * 3 + 1];
                        int i2 = tri[currentTriIndex * 3 + 2];

                        BakeTriangleTexels(buffer, texSize, texSize,
                            uvs[i0], uvs[i1], uvs[i2],
                            i0, i1, i2,
                            rasterizedPixels, mat);

                        currentTriIndex++;
                        processedThisFrame++;
                        processedTexels++;
                    }

                    if (currentTriIndex >= triCount)
                    {
                        currentSubIndex++;
                        currentTriIndex = 0;
                    }
                }

                // 進捗更新
                if (processedTexels % progressUpdateInterval == 0 || currentSubIndex >= subDatas.Count)
                {
                    float progress = totalTexelsToProcess > 0
                        ? (float)processedTexels / totalTexelsToProcess
                        : 1f;

                    float displayProgress = progress * 0.8f; // 0〜0.8 をベイク工程に使用
                    if (ShouldUpdateProgressBar(displayProgress, $"{processedTexels}/{totalTexelsToProcess}"))
                    {
                        Cancel();
                        return;
                    }
                }

                // 全三角形処理完了
                if (currentSubIndex >= subDatas.Count)
                {
                    EditorApplication.update -= BakeStep;

                    if (cancelRequested)
                    {
                        Cancel();
                        return;
                    }

                    Finish();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError(string.Format(ErrorMessages.ERROR_BAKE_EXCEPTION, e.Message));
                Cancel();
            }
        }

        /// <summary>
        /// 1つの三角形のUVバウンディングボックス内の全テクセルについて、
        /// UV→3D逆変換→距離計算を行いバッファに書き込む
        /// </summary>
        private void BakeTriangleTexels(Color[] buffer, int width, int height,
            Vector2 uv0, Vector2 uv1, Vector2 uv2,
            int vertIdx0, int vertIdx1, int vertIdx2,
            HashSet<int> rasterizedPixels, string materialName)
        {
            // UV座標をピクセル座標に変換
            Vector2 p0 = new Vector2(uv0.x * width, uv0.y * height);
            Vector2 p1 = new Vector2(uv1.x * width, uv1.y * height);
            Vector2 p2 = new Vector2(uv2.x * width, uv2.y * height);

            // バウンディングボックス
            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.x, p1.x, p2.x)), 0, width - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.x, p1.x, p2.x)), 0, width - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.y, p1.y, p2.y)), 0, height - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.y, p1.y, p2.y)), 0, height - 1);

            if (maxX < minX || maxY < minY) return;

            // 頂点の3D位置・法線を取得
            Vector3 worldPos0 = verts[vertIdx0];
            Vector3 worldPos1 = verts[vertIdx1];
            Vector3 worldPos2 = verts[vertIdx2];
            Vector3 normal0 = rayDirections[vertIdx0];
            Vector3 normal1 = rayDirections[vertIdx1];
            Vector3 normal2 = rayDirections[vertIdx2];

            // ノーマルマップ用のタンジェントデータ（テクセル単位サンプリング）
            bool hasNormalMap = !string.IsNullOrEmpty(materialName) && normalMapCache.ContainsKey(materialName);
            Vector4 tan0 = default, tan1 = default, tan2 = default;
            if (hasNormalMap)
            {
                tan0 = (vertIdx0 < tangents.Count) ? tangents[vertIdx0] : new Vector4(1, 0, 0, 1);
                tan1 = (vertIdx1 < tangents.Count) ? tangents[vertIdx1] : new Vector4(1, 0, 0, 1);
                tan2 = (vertIdx2 < tangents.Count) ? tangents[vertIdx2] : new Vector4(1, 0, 0, 1);
            }

            // ボーンマスク値を取得
            float bone0 = (boneMaskValues != null && vertIdx0 < boneMaskValues.Count) ? boneMaskValues[vertIdx0] : 0f;
            float bone1 = (boneMaskValues != null && vertIdx1 < boneMaskValues.Count) ? boneMaskValues[vertIdx1] : 0f;
            float bone2 = (boneMaskValues != null && vertIdx2 < boneMaskValues.Count) ? boneMaskValues[vertIdx2] : 0f;

            float threshold = 1.0f - AppSettings.POSITION_PRECISION;

            for (int y = minY; y <= maxY; y++)
            {
                int yOffset = y * width;
                float yPos = y + 0.5f;

                for (int x = minX; x <= maxX; x++)
                {
                    // バリセントリック座標を計算
                    Vector3 bary = EditorMeshUtils.GetBarycentric(new Vector2(x + 0.5f, yPos), p0, p1, p2);
                    if (bary.x < 0 || bary.y < 0 || bary.z < 0) continue;

                    // UV→3D位置の逆変換（バリセントリック補間）
                    Vector3 worldPos = worldPos0 * bary.x + worldPos1 * bary.y + worldPos2 * bary.z;
                    Vector3 normal = (normal0 * bary.x + normal1 * bary.y + normal2 * bary.z).normalized;
                    Vector3 geometricNormal = normal; // ノーマルマップ適用前の幾何法線を保存

                    // テクセル単位でノーマルマップをサンプリング
                    if (hasNormalMap)
                    {
                        Vector2 texelUV = uv0 * bary.x + uv1 * bary.y + uv2 * bary.z;
                        Vector3 interpTangent = new Vector3(
                            tan0.x * bary.x + tan1.x * bary.y + tan2.x * bary.z,
                            tan0.y * bary.x + tan1.y * bary.y + tan2.y * bary.z,
                            tan0.z * bary.x + tan1.z * bary.y + tan2.z * bary.z
                        );
                        normal = SampleNormalMapForTexel(materialName, texelUV, normal, interpTangent, tan0.w);
                    }

                    // ボーンマスク値を補間
                    float boneControl = bone0 * bary.x + bone1 * bary.y + bone2 * bary.z;
                    float boneMaskValue = 1f - Mathf.Clamp01(boneControl);

                    // このテクセルの距離を計算
                    float distValue = CalculateTexelDistance(worldPos, normal, geometricNormal, boneMaskValue);

                    // ガンマ補正
                    distValue = Mathf.Clamp01(distValue);
                    if (!Mathf.Approximately(settings.Gamma, 1.0f))
                    {
                        distValue = Mathf.Pow(distValue, settings.Gamma);
                    }

                    int colorIndex = yOffset + x;

                    if (settings.UseTransparentMode)
                    {
                        if (distValue >= threshold)
                        {
                            // 既に白い場合は既存値を維持（他の三角形がすでに濃い値を書いている可能性）
                            if (buffer[colorIndex].a > 0f || buffer[colorIndex].r < 1f)
                            {
                                // 既に描画済みの場合はより暗い方を採用
                                float existingVal = buffer[colorIndex].a > 0f ? 0f : buffer[colorIndex].r;
                                if (distValue < existingVal)
                                {
                                    float alpha = 1f - distValue;
                                    buffer[colorIndex] = new Color(0f, 0f, 0f, alpha);
                                }
                            }
                            else
                            {
                                buffer[colorIndex] = new Color(0f, 0f, 0f, 0f);
                            }
                        }
                        else
                        {
                            float alpha = 1f - distValue;
                            // より暗い値を採用
                            if (!rasterizedPixels.Contains(colorIndex) || alpha > buffer[colorIndex].a)
                            {
                                buffer[colorIndex] = new Color(0f, 0f, 0f, alpha);
                            }
                        }
                    }
                    else
                    {
            // より暗い値を採用
                            if (!rasterizedPixels.Contains(colorIndex) || distValue < buffer[colorIndex].r)
                            {
                                buffer[colorIndex] = new Color(distValue, distValue, distValue, 1f);
                            }
                        }

                    rasterizedPixels.Add(colorIndex);
                }
            }
        }

        /// <summary>
        /// テクセル位置における距離値を計算
        /// スフィアマスク・ボーンマスク・コライダーレイキャストを統合
        /// ノーマルマップ適用時は幾何法線によるセーフティチェックも行う
        /// </summary>
        private float CalculateTexelDistance(Vector3 worldPos, Vector3 normal, Vector3 geometricNormal, float boneMaskValue)
        {
            // スフィアマスク計算
            float sphereMaskValue = CheckSphereMasks(worldPos);

            // ボーンマスクとスフィアマスクの最小値
            float minMaskValue = Mathf.Min(sphereMaskValue, boneMaskValue);
            if (minMaskValue <= AppSettings.POSITION_PRECISION)
            {
                return 0f;
            }

            // コライダーとのレイキャスト
            if (clothCollider == null)
            {
                return minMaskValue;
            }

            // メインのレイキャスト（ノーマルマップ反映後の法線方向）
            float hitDistance = PerformRaycast(worldPos, normal);

            // セーフティチェック（幾何法線方向）
            // ノーマルマップによって法線が大きく曲げられている場合、
            // 「本来は服の下だが、斜めにレイを飛ばすことで服の隙間をすり抜けてしまう（リークする）」現象を防ぐため、
            // 傾きを少し緩めた方向（ノーマルマップ反映後と幾何法線の中間）でもチェックを行い、遮蔽されている場合はマスクを適用する
            if (Vector3.Dot(normal, geometricNormal) < 0.99f) // 角度差がある程度ある場合のみ
            {
                // 完全な幾何法線ではなく、少し傾きを残した方向（0.5でブレンド）を使用
                Vector3 safetyDirection = Vector3.Lerp(normal, geometricNormal, 0.5f).normalized;
                float safetyHitDistance = PerformRaycast(worldPos, safetyDirection);
                hitDistance = Mathf.Min(hitDistance, safetyHitDistance);
            }

            float distMask = hitDistance / maxM;
            return Mathf.Min(minMaskValue, distMask);
        }

        private float PerformRaycast(Vector3 worldPos, Vector3 direction)
        {
            float hitDistance = maxM;
            const float rayOffsetMultiplier = AppSettings.RAY_OFFSET_MULTIPLIER;
            float rayOffset = AppSettings.POSITION_PRECISION * rayOffsetMultiplier;
            
            // 始点を少し内側にずらすことで、表面ギリギリのコライダーとの接触漏れを防ぐ
            var ray = new Ray(worldPos - direction * rayOffset, direction);

            if (clothCollider.Raycast(ray, out RaycastHit hitInfo, maxM + rayOffset))
            {
                hitDistance = Mathf.Max(0, hitInfo.distance - rayOffset);
            }
            return hitDistance;
        }

        /// <summary>
        /// スフィアマスク値を計算（BakerUtils共通ロジックを使用）
        /// </summary>
        private float CheckSphereMasks(Vector3 vertexPosition)
        {
            float maskValue = 1f;

            if (settings.SphereMasks == null || settings.SphereMasks.Count == 0)
                return maskValue;

            foreach (var sphere in settings.SphereMasks)
            {
                float cr = Mathf.Min(sphere.radius, AppSettings.SHOW_MAX_RADIUS);
                if (cr <= 0f) continue;

                // オリジナルの計算
                float dist = Vector3.Distance(vertexPosition, sphere.position);
                if (dist <= cr)
                {
                    float sphereValue = BakerUtils.CalculateSphereMaskValue(dist, cr, sphere);
                    maskValue = Mathf.Min(maskValue, sphereValue);
                }

                // ミラー機能
                if (sphere.useMirror)
                {
                    Vector3 mirroredPosition = new Vector3(-sphere.position.x, sphere.position.y, sphere.position.z);
                    float mirroredDist = Vector3.Distance(vertexPosition, mirroredPosition);
                    if (mirroredDist <= cr)
                    {
                        float mirroredValue = BakerUtils.CalculateSphereMaskValue(mirroredDist, cr, sphere);
                        maskValue = Mathf.Min(maskValue, mirroredValue);
                    }
                }
            }

            return maskValue;
        }

        private bool ShouldUpdateProgressBar(float progress, string info)
        {
            if (progress - lastProgressBarUpdate < 0.01f && progress < 0.99f)
                return false;

            lastProgressBarUpdate = progress;
            return EditorCoreUtils.ShowCancelableProgressAutoClear(
                UILabels.PROGRESS_BAR_TITLE_TEXEL, info, progress);
        }

        #endregion
    }
}
#endif
