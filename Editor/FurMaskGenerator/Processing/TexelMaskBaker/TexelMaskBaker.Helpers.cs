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
        #region ボーンマスクヘルパー

        /// <summary>
        /// ボーンマスク値を親の影響も含めて解決
        /// </summary>
        private float ResolveBoneMaskWithInheritance(Transform bone)
        {
            float maxVal = 0f;
            Transform current = bone;
            while (current != null)
            {
                if (boneMaskMap.TryGetValue(current, out float v))
                {
                    maxVal = Mathf.Max(maxVal, v);
                }
                current = current.parent;
            }
            return maxVal;
        }

        /// <summary>
        /// ボーンウェイトからマスク値を計算
        /// </summary>
        private float CalculateBoneMaskValue(BoneWeight bw, Transform[] bones, Dictionary<Transform, float> resolved)
        {
            if (bones == null || resolved == null) return 0f;

            float result = 0f;

            if (bw.boneIndex0 >= 0 && bw.boneIndex0 < bones.Length && bones[bw.boneIndex0] != null)
            {
                if (resolved.TryGetValue(bones[bw.boneIndex0], out float v0))
                    result += v0 * bw.weight0;
            }
            if (bw.boneIndex1 >= 0 && bw.boneIndex1 < bones.Length && bones[bw.boneIndex1] != null)
            {
                if (resolved.TryGetValue(bones[bw.boneIndex1], out float v1))
                    result += v1 * bw.weight1;
            }
            if (bw.boneIndex2 >= 0 && bw.boneIndex2 < bones.Length && bones[bw.boneIndex2] != null)
            {
                if (resolved.TryGetValue(bones[bw.boneIndex2], out float v2))
                    result += v2 * bw.weight2;
            }
            if (bw.boneIndex3 >= 0 && bw.boneIndex3 < bones.Length && bones[bw.boneIndex3] != null)
            {
                if (resolved.TryGetValue(bones[bw.boneIndex3], out float v3))
                    result += v3 * bw.weight3;
            }

            return Mathf.Clamp01(result);
        }

        #endregion

        #region ノーマルマップヘルパー

        /// <summary>
        /// ノーマルマップをサンプリングして法線方向を修正
        /// </summary>
        private Vector3 SampleNormalMap(string materialName, Vector2 uv, Vector3 originalNormal, int vertexIndex)
        {
            if (string.IsNullOrEmpty(materialName) || !normalMapCache.TryGetValue(materialName, out var normalMapData))
            {
                return originalNormal;
            }

            if (normalMapData.normalMap == null)
            {
                return originalNormal;
            }

            if (Mathf.Abs(normalMapData.normalStrength) < AppSettings.POSITION_PRECISION)
            {
                return originalNormal;
            }



            if (!cachedIsPackedAG.ContainsKey(materialName))
            {
                TryAutoDetectMaterialFlags(materialName, normalMapData);
            }

            Color normalColor = normalMapData.normalMap.GetPixelBilinear(uv.x, uv.y);

            Vector3 tangentSpaceNormal;

            bool usePackedAG = cachedIsPackedAG.TryGetValue(materialName, out bool cIsAG)
                ? cIsAG : normalMapData.isPackedAG;
            if (usePackedAG)
            {
                float x = (normalColor.a * 2f - 1f) * normalMapData.normalStrength;
                float y = (normalColor.g * 2f - 1f) * normalMapData.normalStrength;
                float z = Mathf.Sqrt(Mathf.Max(0f, 1f - x * x - y * y));
                tangentSpaceNormal = new Vector3(x, y, z);
            }
            else
            {
                float x = (normalColor.r * 2f - 1f) * normalMapData.normalStrength;
                float y = (normalColor.g * 2f - 1f) * normalMapData.normalStrength;
                float z = Mathf.Sqrt(Mathf.Max(0f, 1f - x * x - y * y));
                tangentSpaceNormal = new Vector3(x, y, z);
            }

            Vector3 worldNormal = TransformTangentToWorld(tangentSpaceNormal, originalNormal, vertexIndex);

            float influence = Mathf.Clamp01(Mathf.Abs(normalMapData.normalStrength));
            Vector3 blendedNormal = Vector3.Lerp(originalNormal, worldNormal, influence).normalized;

            return blendedNormal;
        }

        private Vector3 TransformTangentToWorld(Vector3 tangentSpaceNormal, Vector3 originalNormal, int vertexIndex)
        {
            Vector3 normal = originalNormal.normalized;
            if (vertexIndex >= 0 && vertexIndex < tangents.Count)
            {
                Vector4 tangent4 = tangents[vertexIndex];
                Vector3 tangent = new Vector3(tangent4.x, tangent4.y, tangent4.z).normalized;
                if (tangent.sqrMagnitude < AppSettings.POSITION_PRECISION)
                {
                    return Vector3.Lerp(originalNormal, tangentSpaceNormal, 1f).normalized;
                }
                Vector3 bitangent = Vector3.Cross(normal, tangent) * tangent4.w;
                Matrix4x4 tbnMatrix = new Matrix4x4(
                    new Vector4(tangent.x, tangent.y, tangent.z, 0),
                    new Vector4(bitangent.x, bitangent.y, bitangent.z, 0),
                    new Vector4(normal.x, normal.y, normal.z, 0),
                    new Vector4(0, 0, 0, 1)
                );
                Vector3 world = tbnMatrix.MultiplyVector(tangentSpaceNormal).normalized;
                if (float.IsNaN(world.x) || float.IsNaN(world.y) || float.IsNaN(world.z) || world.sqrMagnitude < AppSettings.UV_THRESHOLD_DEFAULT)
                {
                    return originalNormal;
                }
                return world;
            }
            Vector3 tFallback = Vector3.Cross(normal, Vector3.up);
            if (tFallback.sqrMagnitude < AppSettings.POSITION_PRECISION * 100f) tFallback = Vector3.Cross(normal, Vector3.right);
            tFallback.Normalize();
            Vector3 bFallback = Vector3.Cross(normal, tFallback);
            Vector3 rotatedFallback = (tFallback * tangentSpaceNormal.x + bFallback * tangentSpaceNormal.y + normal * tangentSpaceNormal.z).normalized;
            return rotatedFallback;
        }

        /// <summary>
        /// テクセル単位でノーマルマップをサンプリングして法線方向を修正
        /// 補間済みのタンジェントデータを使用してTBN変換を行う
        /// </summary>
        private Vector3 SampleNormalMapForTexel(string materialName, Vector2 texelUV, Vector3 interpolatedNormal, Vector3 interpolatedTangent, float tangentW)
        {
            if (!normalMapCache.TryGetValue(materialName, out var normalMapData))
                return interpolatedNormal;

            if (normalMapData.normalMap == null)
                return interpolatedNormal;

            if (Mathf.Abs(normalMapData.normalStrength) < AppSettings.POSITION_PRECISION)
                return interpolatedNormal;

            if (!cachedIsPackedAG.ContainsKey(materialName))
                TryAutoDetectMaterialFlags(materialName, normalMapData);

            Color normalColor = normalMapData.normalMap.GetPixelBilinear(texelUV.x, texelUV.y);

            Vector3 tangentSpaceNormal;
            bool usePackedAG = cachedIsPackedAG.TryGetValue(materialName, out bool cIsAG) ? cIsAG : normalMapData.isPackedAG;

            if (usePackedAG)
            {
                float nx = (normalColor.a * 2f - 1f) * normalMapData.normalStrength;
                float ny = (normalColor.g * 2f - 1f) * normalMapData.normalStrength;
                float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - ny * ny));
                tangentSpaceNormal = new Vector3(nx, ny, nz);
            }
            else
            {
                float nx = (normalColor.r * 2f - 1f) * normalMapData.normalStrength;
                float ny = (normalColor.g * 2f - 1f) * normalMapData.normalStrength;
                float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - ny * ny));
                tangentSpaceNormal = new Vector3(nx, ny, nz);
            }

            // TBN行列でタンジェント空間→ワールド空間に変換
            Vector3 normal = interpolatedNormal.normalized;
            Vector3 tangent = interpolatedTangent.normalized;

            if (tangent.sqrMagnitude < AppSettings.POSITION_PRECISION)
            {
                tangent = Vector3.Cross(normal, Vector3.up);
                if (tangent.sqrMagnitude < AppSettings.POSITION_PRECISION * 100f)
                    tangent = Vector3.Cross(normal, Vector3.right);
                tangent.Normalize();
            }

            // Gram-Schmidt直交化: 補間後のタンジェントが法線と直交することを保証
            tangent = (tangent - normal * Vector3.Dot(normal, tangent)).normalized;
            if (tangent.sqrMagnitude < AppSettings.POSITION_PRECISION)
            {
                tangent = Vector3.Cross(normal, Vector3.up);
                if (tangent.sqrMagnitude < AppSettings.POSITION_PRECISION * 100f)
                    tangent = Vector3.Cross(normal, Vector3.right);
                tangent.Normalize();
            }

            Vector3 bitangent = Vector3.Cross(normal, tangent) * tangentW;
            Matrix4x4 tbnMatrix = new Matrix4x4(
                new Vector4(tangent.x, tangent.y, tangent.z, 0),
                new Vector4(bitangent.x, bitangent.y, bitangent.z, 0),
                new Vector4(normal.x, normal.y, normal.z, 0),
                new Vector4(0, 0, 0, 1)
            );

            Vector3 world = tbnMatrix.MultiplyVector(tangentSpaceNormal).normalized;
            if (float.IsNaN(world.x) || float.IsNaN(world.y) || float.IsNaN(world.z) || world.sqrMagnitude < AppSettings.UV_THRESHOLD_DEFAULT)
            {
                return interpolatedNormal;
            }

            float influence = Mathf.Clamp01(Mathf.Abs(normalMapData.normalStrength));
            return Vector3.Lerp(interpolatedNormal, world, influence).normalized;
        }

        private void TryAutoDetectMaterialFlags(string materialName, MaterialNormalMapData data)
        {
            if (string.IsNullOrEmpty(materialName) || data == null || data.normalMap == null) return;

            var candidates = new List<int>(64);
            for (int i = 0; i < verts.Count && candidates.Count < 64; i++)
            {
                if (vertexToMaterialName.TryGetValue(i, out string m) && m == materialName)
                {
                    candidates.Add(i);
                }
            }
            if (candidates.Count == 0) return;

            bool[] combos = new bool[] { false, true };
            float bestScore = -1f; bool best = false;

            foreach (var combo in combos)
            {
                float sum = 0f; int ct = 0;
                foreach (int vi in candidates)
                {
                    Vector2 uv = (vi < uvs.Count) ? uvs[vi] : Vector2.zero;
                    Color c = data.normalMap.GetPixelBilinear(uv.x, uv.y);
                    float xr, yr;
                    if (combo)
                    {
                        xr = c.a * 2f - 1f; yr = c.g * 2f - 1f;
                    }
                    else
                    {
                        xr = c.r * 2f - 1f; yr = c.g * 2f - 1f;
                    }
                    float zr = Mathf.Sqrt(Mathf.Max(0f, 1f - xr * xr - yr * yr));
                    Vector3 ts = new Vector3(xr, yr, zr);

                    Vector3 n = norms[vi].normalized;
                    Vector3 world;
                    if (vi < tangents.Count)
                    {
                        Vector4 t4 = tangents[vi];
                        Vector3 t = new Vector3(t4.x, t4.y, t4.z).normalized;
                        Vector3 b = Vector3.Cross(n, t) * t4.w;
                        Matrix4x4 tbn = new Matrix4x4(
                            new Vector4(t.x, t.y, t.z, 0),
                            new Vector4(b.x, b.y, b.z, 0),
                            new Vector4(n.x, n.y, n.z, 0),
                            new Vector4(0, 0, 0, 1)
                        );
                        world = tbn.MultiplyVector(ts).normalized;
                    }
                    else
                    {
                        Vector3 t = Vector3.Cross(n, Vector3.up);
                        if (t.sqrMagnitude < AppSettings.POSITION_PRECISION * 100f) t = Vector3.Cross(n, Vector3.right);
                        t.Normalize(); Vector3 b = Vector3.Cross(n, t);
                        world = (t * ts.x + b * ts.y + n * ts.z).normalized;
                    }

                    sum += Mathf.Clamp01(Vector3.Dot(n, world));
                    ct++;
                }
                float score = (ct > 0) ? sum / ct : -1f;
                if (score > bestScore)
                {
                    bestScore = score; best = combo;
                }
            }

            cachedIsPackedAG[materialName] = best;
            data.isPackedAG = best;
        }

        #endregion
    }
}
#endif
