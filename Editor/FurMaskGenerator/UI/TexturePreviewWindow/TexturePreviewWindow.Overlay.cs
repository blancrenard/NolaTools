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
        private void ClearOverlayTexture()
        {
            ClearTexture(ref overlayTexture);
            _overlayPixels = null;
        }

        private bool EnsureOverlayBacking()
        {
            if (texture == null) return false;

            int w = texture.width;
            int h = texture.height;
            int pixelCount = w * h;

            if (overlayTexture != null
                && overlayTexture.width == w
                && overlayTexture.height == h
                && _overlayPixels != null
                && _overlayPixels.Length == pixelCount)
            {
                return true;
            }

            ClearTexture(ref overlayTexture);
            _overlayPixels = null;
            overlayTexture = CreateClearTextureAndPixels(w, h, out _overlayPixels);
            return overlayTexture != null && _overlayPixels != null;
        }

        private void ClearOverlayPixelBuffer()
        {
            if (_overlayPixels == null) return;
            for (int i = 0; i < _overlayPixels.Length; i++)
            {
                _overlayPixels[i] = Color.clear;
            }
        }

        private void FlushOverlayPixels()
        {
            if (overlayTexture != null && _overlayPixels != null)
            {
                TextureOperationUtils.UpdateTexturePixels(overlayTexture, _overlayPixels);
            }
        }

        /// <summary>
        /// 追加されたマスク1件だけオーバーレイに描画する（成功時 true）
        /// </summary>
        private bool TryAppendMaskToOverlay(UVIslandMaskData mask)
        {
            if (!showUVMasks || texture == null || mask == null) return false;
            if (!EnsureOverlayBacking()) return false;

            var pathToRenderer = BuildRendererPathMap();
            if (!pathToRenderer.TryGetValue(mask.rendererPath, out var renderer))
            {
                return false;
            }

            if (!MaskMatchesPreviewMaterial(mask, renderer))
            {
                return true;
            }

            DrawUVMaskOnTextureForRenderer(_overlayPixels, mask, renderer);
            FlushOverlayPixels();
            return true;
        }

        private void GenerateOverlayTexture()
        {
            if (texture == null)
            {
                ClearOverlayTexture();
                return;
            }

            if (uvMasks == null || uvMasks.Count == 0)
            {
                ClearOverlayTexture();
                return;
            }

            try
            {
                if (!EnsureOverlayBacking())
                {
                    ClearOverlayTexture();
                    return;
                }

                ClearOverlayPixelBuffer();

                var pathToRenderer = BuildRendererPathMap();

                foreach (var uvMask in uvMasks)
                {
                    if (uvMask == null) continue;
                    if (pathToRenderer.TryGetValue(uvMask.rendererPath, out var r))
                    {
                        if (!MaskMatchesPreviewMaterial(uvMask, r)) continue;
                        DrawUVMaskOnTextureForRenderer(_overlayPixels, uvMask, r);
                    }
                }

                FlushOverlayPixels();
            }
            catch (System.Exception ex)
            {
                Debug.LogError(string.Format(ErrorMessages.ERROR_UV_MASK_OVERLAY_GENERATION, ex.Message));
                ClearOverlayTexture();
            }
        }

        private void DrawUVMaskOnTextureForRenderer(Color[] pixels, UVIslandMaskData uvMask, Renderer renderer)
        {
            if (pixels == null || uvMask == null || renderer == null) return;
            Mesh mesh = EditorMeshUtils.GetMeshForRenderer(renderer, out bool isBakedTempMesh);
            if (mesh == null) return;
            try
            {
                string rendererPath = !string.IsNullOrEmpty(uvMask.rendererPath)
                    ? uvMask.rendererPath
                    : EditorPathUtils.GetGameObjectPath(renderer);
                var islandTriangles = GetUVIslandTriangles(rendererPath, mesh, uvMask.submeshIndex, uvMask.seedUV);
                if (islandTriangles.Count == 0) return;

                Color maskColor = uvMask.markerColor;
                Color.RGBToHSV(maskColor, out float h, out float s, out float v);
                s = Mathf.Clamp01(s * 1.2f);
                v = Mathf.Clamp01(v * 0.9f);
                maskColor = Color.HSVToRGB(h, s, v);
                maskColor.a = 0.35f;

                int[] triangles = mesh.GetTriangles(uvMask.submeshIndex);
                Vector2[] uvs = mesh.uv;

                foreach (int triangleIndex in islandTriangles)
                {
                    if (triangleIndex * 3 + 2 >= triangles.Length) continue;
                    int v0 = triangles[triangleIndex * 3];
                    int v1 = triangles[triangleIndex * 3 + 1];
                    int v2 = triangles[triangleIndex * 3 + 2];
                    if (v0 >= uvs.Length || v1 >= uvs.Length || v2 >= uvs.Length) continue;
                    Vector2 uv0 = uvs[v0];
                    Vector2 uv1 = uvs[v1];
                    Vector2 uv2 = uvs[v2];
                    FillTriangleOnTexture(pixels, uv0, uv1, uv2, maskColor);
                }
            }
            finally
            {
                if (isBakedTempMesh)
                {
                    EditorObjectUtils.SafeDestroy(mesh);
                }
            }
        }
    }
}
#endif
