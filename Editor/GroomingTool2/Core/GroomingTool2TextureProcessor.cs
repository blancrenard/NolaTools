using System;
using System.Collections.Generic;
using GroomingTool2.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GroomingTool2.Core
{
    /// <summary>
    /// テクスチャ処理を共通化したクラス
    /// </summary>
    internal sealed class GroomingTool2TextureProcessor : IDisposable
    {
        private readonly Dictionary<Texture2D, Texture2D> resizedTextureCache = new();

        public void Dispose()
        {
            foreach (var texture in resizedTextureCache.Values)
            {
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }
            }
            resizedTextureCache.Clear();
        }

        /// <summary>
        /// テクスチャを指定サイズにリサイズする
        /// </summary>
        public Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            if (source == null)
                return null;

            // キャッシュから取得を試行
            if (resizedTextureCache.TryGetValue(source, out var cached))
            {
                if (cached != null && cached.width == targetWidth && cached.height == targetHeight)
                {
                    return cached;
                }
                // サイズが異なる場合はキャッシュから削除して再作成
                if (cached != null)
                {
                    Object.DestroyImmediate(cached);
                    resizedTextureCache.Remove(source);
                }
            }

            var result = CreateTextureFromSource(source, targetWidth, targetHeight);

            // キャッシュに保存
            resizedTextureCache[source] = result;
            return result;
        }

        /// <summary>
        /// テクスチャを複製する
        /// </summary>
        public Texture2D DuplicateTexture(Texture2D source)
        {
            if (source == null)
                return null;

            return CreateTextureFromSource(source, source.width, source.height);
        }

        private Texture2D CreateTextureFromSource(Texture2D source, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;

            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;

                var result = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                {
                    wrapMode = source.wrapMode,
                    filterMode = source.filterMode
                };

                result.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                result.Apply();

                return result;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>
        /// テクスチャからピクセルデータを取得する（ダウンサンプル版）
        /// </summary>
        public bool TryGetDownsampledPixels(Texture2D texture, out Color32[] pixels, out int width, out int height, int maxSize = 256)
        {
            pixels = null;
            width = 0;
            height = 0;

            if (texture == null)
                return false;

            try
            {
                // 小さめにダウンサンプルして読み取りコストを下げる
                int w = Mathf.Min(maxSize, texture.width);
                int h = Mathf.RoundToInt((float)w / texture.width * texture.height);
                if (h <= 0) h = 1;

                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                var prev = RenderTexture.active;

                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;

                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                tex.Apply(false, true); // 読み取り専用にしてGC削減

                pixels = tex.GetPixels32();
                width = w;
                height = h;

                // 後片付け
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(tex);

                return pixels != null && pixels.Length == w * h;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// ワイヤーフレームテクスチャを作成する
        /// </summary>
        public Texture2D CreateWireframeTexture(int width, int height, List<Vector2[]> uvSets, List<int[]> triangleSets, bool flipUvY)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };

            var buffer = new Color32[width * height];
            var lineColor = new Color32(0, 255, 0, 160);

            for (int s = 0; s < uvSets.Count && s < triangleSets.Count; s++)
            {
                var uv = uvSets[s];
                var tris = triangleSets[s];
                if (uv == null || tris == null || uv.Length == 0 || tris.Length < 3)
                    continue;

                for (int i = 0; i < tris.Length; i += 3)
                {
                    int i0 = tris[i];
                    int i1 = tris[i + 1];
                    int i2 = tris[i + 2];

                    var p0 = UvToPixel(uv[i0], width, height, flipUvY);
                    var p1 = UvToPixel(uv[i1], width, height, flipUvY);
                    var p2 = UvToPixel(uv[i2], width, height, flipUvY);

                    DrawLineOnBuffer(p0.x, p0.y, p1.x, p1.y, lineColor, buffer, width, height);
                    DrawLineOnBuffer(p1.x, p1.y, p2.x, p2.y, lineColor, buffer, width, height);
                    DrawLineOnBuffer(p2.x, p2.y, p0.x, p0.y, lineColor, buffer, width, height);
                }
            }

            texture.SetPixels32(buffer);
            texture.Apply();

            return texture;
        }

        private static Vector2Int UvToPixel(Vector2 uv, int width, int height, bool flipUvY)
        {
            int x = Mathf.RoundToInt(uv.x * (width - 1));
            int y = flipUvY ? Mathf.RoundToInt(uv.y * (height - 1)) : Mathf.RoundToInt((1f - uv.y) * (height - 1));
            return new Vector2Int(x, y);
        }

        private static void DrawLineOnBuffer(int x0, int y0, int x1, int y1, Color32 color, Color32[] buffer, int width, int height)
        {
            LineDrawingUtils.DrawLineOnBuffer(x0, y0, x1, y1, color, buffer, width, height);
        }
    }
}
