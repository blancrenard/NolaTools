#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Data;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator.UI
{
    public partial class TexturePreviewWindow
    {
        private void HandlePreviewClick(Rect textureRect)
        {
            Event e = Event.current;
            if (!addUVMasksOnPreview) return;
            if (e == null || e.type != EventType.MouseDown || e.button != 0) return;
            if (texture == null) return;

            // より正確なクリック判定：テクスチャ矩形内にマウスがあるかを確認
            // 境界付近の誤差を考慮して少し広めに判定
            Rect expandedRect = new Rect(
                textureRect.x - 2f,
                textureRect.y - 2f,
                textureRect.width + 4f,
                textureRect.height + 4f
            );
            if (!expandedRect.Contains(e.mousePosition)) return;

            // ScaleMode.ScaleToFitを考慮した実際のテクスチャ描画サイズを計算
            float textureAspect = (float)texture.width / texture.height;
            float rectAspect = textureRect.width / textureRect.height;

            float actualDrawWidth, actualDrawHeight;
            if (textureAspect > rectAspect)
            {
                // テクスチャが横長：幅に合わせる
                actualDrawWidth = textureRect.width;
                actualDrawHeight = textureRect.width / textureAspect;
            }
            else
            {
                // テクスチャが縦長または正方形：高さに合わせる
                actualDrawHeight = textureRect.height;
                actualDrawWidth = textureRect.height * textureAspect;
            }

            // 実際の描画領域のオフセットを計算
            float drawOffsetX = textureRect.x + (textureRect.width - actualDrawWidth) * 0.5f;
            float drawOffsetY = textureRect.y + (textureRect.height - actualDrawHeight) * 0.5f;

            // マウス位置を実際の描画領域内の相対位置に変換
            // より正確な計算：実際の描画領域内でのみ計算し、境界チェックを厳密に行う
            float relativeX = e.mousePosition.x - drawOffsetX;
            float relativeY = e.mousePosition.y - drawOffsetY;

            // 実際の描画領域内でのみ有効なクリックかを再確認
            if (relativeX < 0 || relativeX > actualDrawWidth || relativeY < 0 || relativeY > actualDrawHeight)
            {
                return;
            }

            // UV座標を計算（0-1の範囲に正規化）
            float u = relativeX / actualDrawWidth;
            float v = 1f - (relativeY / actualDrawHeight); // Y軸反転

            // UVが有効範囲内かを最終確認
            if (u < 0f || u > 1f || v < 0f || v > 1f)
            {
                return;
            }

            Vector2 uv = new Vector2(u, v);

            int chosenIndex = -1;
            Vector3 chosenWorld = Vector3.zero;
            if (targets != null && targets.Count > 0)
            {
                var order = new List<int>();
                if (currentTargetIndex >= 0 && currentTargetIndex < targets.Count && targets[currentTargetIndex].Texture == texture)
                {
                    order.Add(currentTargetIndex);
                }
                for (int i = 0; i < targets.Count; i++)
                {
                    if (i == currentTargetIndex) continue;
                    if (targets[i].Texture == texture) order.Add(i);
                }

                for (int k = 0; k < order.Count; k++)
                {
                    int idx = order[k];
                    var t = targets[idx];
                    if (t.Renderer == null) continue;

                    // まず厳密な判定を試す
                    if (IsUVInsideSubmeshStrict(t.Renderer, t.SubmeshIndex, uv))
                    {
                        chosenIndex = idx;
                        break;
                    }

                    // 厳密な判定でヒットしなかった場合、近傍判定を試す
                    if (IsUVNearSubmesh(t.Renderer, t.SubmeshIndex, uv, 0.05f)) // 5%の許容範囲
                    {
                        chosenIndex = idx;
                        break;
                    }
                }
            }

            if (chosenIndex < 0 && targetRenderer != null)
            {
                // まず厳密な判定を試す
                if (IsUVInsideSubmeshStrict(targetRenderer, submeshIndex, uv))
                {
                    chosenIndex = currentTargetIndex;
                }
                // 厳密な判定でヒットしなかった場合、近傍判定を試す
                else if (IsUVNearSubmesh(targetRenderer, submeshIndex, uv, 0.05f))
                {
                    chosenIndex = currentTargetIndex;
                }
            }

            if (chosenIndex < 0)
            {
                e.Use();
                return;
            }

            var chosen = targets != null && chosenIndex >= 0 && chosenIndex < targets.Count ? targets[chosenIndex] : null;

            int existingIndex = -1;
            if (chosen != null)
            {
                var prevRenderer = targetRenderer;
                var prevSubmesh = submeshIndex;
                targetRenderer = chosen.Renderer;
                submeshIndex = chosen.SubmeshIndex;
                existingIndex = FindExistingMarkerIndexAtUV(uv);
                targetRenderer = prevRenderer;
                submeshIndex = prevSubmesh;
            }
            else
            {
                existingIndex = FindExistingMarkerIndexAtUV(uv);
            }
            if (existingIndex >= 0)
            {
                if (existingIndex < uvMasks.Count)
                {
                    var toRemove = uvMasks[existingIndex];
                    if (onRemoveMaskCallback != null)
                    {
                        onRemoveMaskCallback.Invoke(toRemove);
                        Repaint();
                    }
                    else
                    {
                        uvMasks.RemoveAt(existingIndex);
                        if (showUVMasks)
                        {
                            ClearOverlayTexture();
                            GenerateOverlayTexture();
                        }
                        Repaint();
                    }
                }
                e.Use();
                return;
            }

            if (chosen != null)
            {
                if (!TryComputeWorldPositionFromUV(chosen.Renderer, chosen.SubmeshIndex, uv, out chosenWorld))
                {
                    TryComputeWorldFromContainingTriangle(chosen.Renderer, chosen.SubmeshIndex, uv, out chosenWorld);
                }
            }

            Renderer resolvedRenderer = chosen != null ? chosen.Renderer : targetRenderer;
            int resolvedSubmesh = chosen != null ? chosen.SubmeshIndex : submeshIndex;

            if (resolvedRenderer == null)
            {
                e.Use();
                return;
            }

            string resolvedRendererPath = EditorPathUtils.GetGameObjectPath(resolvedRenderer);
            string targetMaterialName = null;
            var sharedMaterials = resolvedRenderer.sharedMaterials;
            if (sharedMaterials != null && resolvedSubmesh >= 0 && resolvedSubmesh < sharedMaterials.Length && sharedMaterials[resolvedSubmesh] != null)
            {
                targetMaterialName = sharedMaterials[resolvedSubmesh].name;
            }

            var data = new UVIslandMaskData
            {
                rendererPath = resolvedRendererPath,
                submeshIndex = resolvedSubmesh,
                seedUV = uv,
                uvPosition = uv,
                targetMatName = targetMaterialName,
                seedWorldPos = chosenWorld,
                displayName = resolvedRenderer.name,
                markerColor = ColorGenerator.GenerateMarkerColor(),
                uvThreshold = AppSettings.UV_THRESHOLD_DEFAULT
            };

            if (onAddMaskCallback != null)
            {
                onAddMaskCallback.Invoke(data);
                Repaint();
                e.Use();
                return;
            }

            if (uvMasks == null) uvMasks = new List<NolaTools.FurMaskGenerator.Data.UVIslandMaskData>();
            uvMasks.Add(data);
            if (showUVMasks)
            {
                GenerateOverlayTexture();
            }
            Repaint();
            e.Use();
        }

        private int FindExistingMarkerIndexAtUV(Vector2 uv)
        {
            if (uvMasks == null || uvMasks.Count == 0 || targetRenderer == null) return -1;
            string rPath = EditorPathUtils.GetGameObjectPath(targetRenderer);
            Mesh mesh = EditorMeshUtils.GetMeshForRenderer(targetRenderer, out bool isBakedTempMesh);
            if (mesh == null)
            {
                return -1;
            }
            try
            {
                var clickedIsland = GetUVIslandTriangles(mesh, submeshIndex, uv);
                if (clickedIsland == null || clickedIsland.Count == 0) return -1;

                for (int i = 0; i < uvMasks.Count; i++)
                {
                    var m = uvMasks[i];
                    if (m == null) continue;
                    if (m.rendererPath != rPath || m.submeshIndex != submeshIndex) continue;
                    var island = GetUVIslandTriangles(mesh, m.submeshIndex, m.seedUV);
                    if (island != null && island.Count > 0)
                    {
                        foreach (int tri in island)
                        {
                            if (clickedIsland.Contains(tri))
                            {
                                return i;
                            }
                        }
                    }
                }
                return -1;
            }
            finally
            {
                if (isBakedTempMesh)
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
        }
    }
}
#endif

