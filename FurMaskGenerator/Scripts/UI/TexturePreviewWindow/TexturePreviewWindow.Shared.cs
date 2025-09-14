#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mask.Generator.Utils;

namespace Mask.Generator.UI
{
    public partial class TexturePreviewWindow
    {
        private Texture2D CreateClearTextureAndPixels(int width, int height, out Color[] pixels)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;
            return tex;
        }

        private void ClearTexture(ref Texture2D tex)
        {
            if (tex != null)
            {
                UnityEngine.Object.DestroyImmediate(tex);
                tex = null;
            }
        }

        private static bool TryGetTrianglesAndUV(Mesh mesh, int submesh, out int[] triangles, out Vector2[] uvs)
        {
            triangles = null;
            uvs = null;
            if (mesh == null) return false;
            int sub = Mathf.Clamp(submesh, 0, Mathf.Max(0, mesh.subMeshCount - 1));
            var tri = mesh.GetTriangles(sub);
            var uv = mesh.uv;
            if (tri == null || tri.Length == 0 || uv == null || uv.Length != mesh.vertexCount) return false;
            triangles = tri;
            uvs = uv;
            return true;
        }

        private static Vector2Int UvToPixel(Texture2D tex, Vector2 uv)
        {
            int x = Mathf.RoundToInt(uv.x * (tex.width - 1));
            int y = Mathf.RoundToInt(uv.y * (tex.height - 1));
            return new Vector2Int(x, y);
        }

        // 共通: UV直線をピクセル配列に描画（ブレゼンハム）
        private void DrawUVLineOnPixels(Color[] pixels, Vector2 uv0, Vector2 uv1, Color color)
        {
            if (texture == null || pixels == null) return;
            int x0 = Mathf.RoundToInt(uv0.x * (texture.width - 1));
            int y0 = Mathf.RoundToInt(uv0.y * (texture.height - 1));
            int x1 = Mathf.RoundToInt(uv1.x * (texture.width - 1));
            int y1 = Mathf.RoundToInt(uv1.y * (texture.height - 1));

            int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                int idx = y0 * texture.width + x0;
                if (idx >= 0 && idx < pixels.Length)
                {
                    pixels[idx] = color;
                }
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        // 共通: UV三角形塗りつぶし
        private void FillTriangleOnTexture(Color[] pixels, Vector2 uv0, Vector2 uv1, Vector2 uv2, Color color)
        {
            if (texture == null || pixels == null) return;
            int x0 = Mathf.RoundToInt(uv0.x * (texture.width - 1));
            int y0 = Mathf.RoundToInt(uv0.y * (texture.height - 1));
            int x1 = Mathf.RoundToInt(uv1.x * (texture.width - 1));
            int y1 = Mathf.RoundToInt(uv1.y * (texture.height - 1));
            int x2 = Mathf.RoundToInt(uv2.x * (texture.width - 1));
            int y2 = Mathf.RoundToInt(uv2.y * (texture.height - 1));

            int minX = Mathf.Max(0, Mathf.Min(x0, Mathf.Min(x1, x2)));
            int maxX = Mathf.Min(texture.width - 1, Mathf.Max(x0, Mathf.Max(x1, x2)));
            int minY = Mathf.Max(0, Mathf.Min(y0, Mathf.Min(y1, y2)));
            int maxY = Mathf.Min(texture.height - 1, Mathf.Max(y0, Mathf.Max(y1, y2)));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (IsPointInTriangle(x, y, x0, y0, x1, y1, x2, y2))
                    {
                        int index = y * texture.width + x;
                        if (index >= 0 && index < pixels.Length)
                        {
                            pixels[index] = color;
                        }
                    }
                }
            }
        }

        // 共通: 画素座標の三角形内判定
        private bool IsPointInTriangle(int px, int py, int x0, int y0, int x1, int y1, int x2, int y2)
        {
            float denom = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2);
            if (Mathf.Abs(denom) < 1e-6f) return false;

            float a = ((y1 - y2) * (px - x2) + (x2 - x1) * (py - y2)) / denom;
            float b = ((y2 - y0) * (px - x2) + (x0 - x2) * (py - y2)) / denom;
            float c = 1 - a - b;

            return a >= 0 && b >= 0 && c >= 0;
        }

        private Dictionary<string, Renderer> BuildRendererPathMap()
        {
            var map = new Dictionary<string, Renderer>();
            if (targets != null && targets.Count > 0)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var t = targets[i];
                    if (t == null || t.Texture != texture || t.Renderer == null) continue;
                    string p = EditorPathUtils.GetGameObjectPath(t.Renderer);
                    if (!string.IsNullOrEmpty(p) && !map.ContainsKey(p))
                    {
                        map[p] = t.Renderer;
                    }
                }
            }
            else if (targetRenderer != null)
            {
                string p = EditorPathUtils.GetGameObjectPath(targetRenderer);
                if (!string.IsNullOrEmpty(p)) map[p] = targetRenderer;
            }
            return map;
        }
    }
}
#endif


