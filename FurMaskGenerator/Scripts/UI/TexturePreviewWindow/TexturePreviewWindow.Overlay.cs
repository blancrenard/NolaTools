#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mask.Generator.Data;
using Mask.Generator.Utils;
using Mask.Generator.Constants;

namespace Mask.Generator.UI
{
    public partial class TexturePreviewWindow
    {
        private void GenerateOverlayTexture()
        {
            if (texture == null || uvMasks == null || uvMasks.Count == 0)
            {
                return;
            }

            try
            {
                ClearTexture(ref overlayTexture);

                Color[] pixels;
                overlayTexture = CreateClearTextureAndPixels(texture.width, texture.height, out pixels);

                var pathToRenderer = BuildRendererPathMap();

                foreach (var uvMask in uvMasks)
                {
                    if (uvMask == null) continue;
                    if (pathToRenderer.TryGetValue(uvMask.rendererPath, out var r))
                    {
                        DrawUVMaskOnTextureForRenderer(pixels, uvMask, r);
                    }
                }

                EditorUIUtils.UpdateTexturePixels(overlayTexture, pixels);
            }
            catch (System.Exception ex)
            {
                Debug.LogError(string.Format(UIConstants.ERROR_UV_MASK_OVERLAY_GENERATION, ex.Message));
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
                var islandTriangles = GetUVIslandTriangles(mesh, uvMask.submeshIndex, uvMask.seedUV);
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

