#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Mask.Generator.Utils;

namespace Mask.Generator.UI
{
    public partial class TexturePreviewWindow
    {
        private void ClearOverlayTexture() => ClearTexture(ref overlayTexture);

        private void ClearWireframeTexture() => ClearTexture(ref wireframeTexture);

        private void GenerateWireframeTexture()
        {
            if (texture == null) return;

            ClearWireframeTexture();
            Color[] pixels;
            wireframeTexture = CreateClearTextureAndPixels(texture.width, texture.height, out pixels);

            Color wire = new Color(1f, 1f, 1f, 0.6f);

            bool drewSomething = false;

            if (targets != null && targets.Count > 0)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var t = targets[i];
                    if (t == null || t.Texture != texture || t.Renderer == null) continue;
                    Mesh meshEach = EditorMeshUtils.GetMeshForRenderer(t.Renderer, out bool isBakedEach);
                    if (meshEach == null) continue;
                    try
                    {
                        int subEach = Mathf.Clamp(t.SubmeshIndex, 0, meshEach.subMeshCount - 1);
                        if (!TryGetTrianglesAndUV(meshEach, subEach, out var trianglesEach, out var uvsEach)) continue;

                        int triCountEach = trianglesEach.Length / 3;
                        for (int tt = 0; tt < triCountEach; tt++)
                        {
                            int ia = trianglesEach[tt * 3 + 0];
                            int ib = trianglesEach[tt * 3 + 1];
                            int ic = trianglesEach[tt * 3 + 2];
                            if (ia >= uvsEach.Length || ib >= uvsEach.Length || ic >= uvsEach.Length) continue;
                            Vector2 a = uvsEach[ia];
                            Vector2 b = uvsEach[ib];
                            Vector2 c = uvsEach[ic];
                            DrawUVLineOnPixels(pixels, a, b, wire);
                            DrawUVLineOnPixels(pixels, b, c, wire);
                            DrawUVLineOnPixels(pixels, c, a, wire);
                        }
                        drewSomething = true;
                    }
                    finally
                    {
                        if (isBakedEach)
                        {
                            EditorObjectUtils.SafeDestroy(meshEach);
                        }
                    }
                }
            }

            if (!drewSomething && targetRenderer != null)
            {
                Mesh mesh = EditorMeshUtils.GetMeshForRenderer(targetRenderer, out bool isBakedTempMesh);
                if (mesh != null)
                {
                    try
                    {
                        if (TryGetTrianglesAndUV(mesh, submeshIndex, out var triangles, out var uvs))
                        {
                            int triCount = triangles.Length / 3;
                            for (int t = 0; t < triCount; t++)
                            {
                                int ia = triangles[t * 3 + 0];
                                int ib = triangles[t * 3 + 1];
                                int ic = triangles[t * 3 + 2];
                                if (ia >= uvs.Length || ib >= uvs.Length || ic >= uvs.Length) continue;
                                Vector2 a = uvs[ia];
                                Vector2 b = uvs[ib];
                                Vector2 c = uvs[ic];
                                DrawUVLineOnPixels(pixels, a, b, wire);
                                DrawUVLineOnPixels(pixels, b, c, wire);
                                DrawUVLineOnPixels(pixels, c, a, wire);
                            }
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

            TextureOperationUtils.UpdateTexturePixels(wireframeTexture, pixels);
        }
    }
}
#endif

