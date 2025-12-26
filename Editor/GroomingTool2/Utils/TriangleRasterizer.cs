using System.Collections.Generic;
using GroomingTool2.Core;
using UnityEngine;

namespace GroomingTool2.Utils
{
    /// <summary>
    /// 三角形のUVラスタライズユーティリティ
    /// UvRegionMaskUtils と IslandSelectionUtils で共有
    /// </summary>
    internal static class TriangleRasterizer
    {
        /// <summary>
        /// UV三角形をマスクにラスタライズ
        /// </summary>
        /// <param name="uv0">頂点0のUV座標</param>
        /// <param name="uv1">頂点1のUV座標</param>
        /// <param name="uv2">頂点2のUV座標</param>
        /// <param name="mask">出力マスク</param>
        /// <param name="tolerance">重心座標の許容誤差（デフォルト: -0.01f）</param>
        public static void RasterizeToMask(Vector2 uv0, Vector2 uv1, Vector2 uv2, bool[,] mask, float tolerance = -0.01f)
        {
            // UV座標をデータ座標に変換（Y軸反転）
            Vector2 p0 = new Vector2(uv0.x * Common.TexSize, (1f - uv0.y) * Common.TexSize);
            Vector2 p1 = new Vector2(uv1.x * Common.TexSize, (1f - uv1.y) * Common.TexSize);
            Vector2 p2 = new Vector2(uv2.x * Common.TexSize, (1f - uv2.y) * Common.TexSize);

            // バウンディングボックスを計算
            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.x, p1.x, p2.x)), 0, Common.TexSize - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.x, p1.x, p2.x)), 0, Common.TexSize - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.y, p1.y, p2.y)), 0, Common.TexSize - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.y, p1.y, p2.y)), 0, Common.TexSize - 1);

            if (maxX < minX || maxY < minY)
                return;

            // 各ピクセルが三角形内にあるかを判定
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 pixelPos = new Vector2(x + 0.5f, y + 0.5f);
                    Vector3 barycentric = EditorMeshUtils.GetBarycentric(pixelPos, p0, p1, p2);

                    // 重心座標が許容範囲内なら三角形内
                    if (barycentric.x >= tolerance && barycentric.y >= tolerance && barycentric.z >= tolerance)
                    {
                        mask[x, y] = true;
                    }
                }
            }
        }

        /// <summary>
        /// UV三角形をHashSetにラスタライズ
        /// </summary>
        /// <param name="uv0">頂点0のUV座標</param>
        /// <param name="uv1">頂点1のUV座標</param>
        /// <param name="uv2">頂点2のUV座標</param>
        /// <param name="pixels">出力ピクセル集合</param>
        public static void RasterizeToSet(Vector2 uv0, Vector2 uv1, Vector2 uv2, HashSet<Vector2Int> pixels)
        {
            // UV座標をデータ座標に変換（Y軸反転）
            Vector2 p0 = new Vector2(uv0.x * Common.TexSize, (1f - uv0.y) * Common.TexSize);
            Vector2 p1 = new Vector2(uv1.x * Common.TexSize, (1f - uv1.y) * Common.TexSize);
            Vector2 p2 = new Vector2(uv2.x * Common.TexSize, (1f - uv2.y) * Common.TexSize);

            // バウンディングボックスを計算
            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.x, p1.x, p2.x)), 0, Common.TexSize - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.x, p1.x, p2.x)), 0, Common.TexSize - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.y, p1.y, p2.y)), 0, Common.TexSize - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.y, p1.y, p2.y)), 0, Common.TexSize - 1);

            if (maxX < minX || maxY < minY)
                return;

            // 各ピクセルが三角形内にあるかを判定
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 pixelPos = new Vector2(x + 0.5f, y + 0.5f);
                    Vector3 barycentric = EditorMeshUtils.GetBarycentric(pixelPos, p0, p1, p2);

                    // 重心座標が全て非負なら三角形内
                    if (barycentric.x >= 0 && barycentric.y >= 0 && barycentric.z >= 0)
                    {
                        pixels.Add(new Vector2Int(x, y));
                    }
                }
            }
        }
    }
}
