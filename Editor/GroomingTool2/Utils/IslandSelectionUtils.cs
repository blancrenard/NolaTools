using System.Collections.Generic;
using GroomingTool2.Core;
using UnityEngine;

namespace GroomingTool2.Utils
{
    /// <summary>
    /// UVアイランド選択のユーティリティ
    /// </summary>
    internal static class IslandSelectionUtils
    {
        /// <summary>
        /// 三角形ベースでUVアイランドを抽出し、マスクに反映する（FurMaskGenerator方式）
        /// </summary>
        public static HashSet<Vector2Int> ExtractUVIslandFromMesh(
            Vector2 seedUV,
            Vector2[] uvs,
            int[] triangles,
            bool[,] mask)
        {
            var selectedPixels = new HashSet<Vector2Int>();
            
            if (uvs == null || triangles == null || triangles.Length < 3 || uvs.Length == 0)
                return selectedPixels;

            // シード三角形を見つける
            int seedTriangle = EditorUvUtils.FindSeedTriangleByUV(triangles, uvs, seedUV);
            if (seedTriangle < 0)
                return selectedPixels;

            // 隣接関係を構築
            var adjacency = EditorUvUtils.BuildTriangleAdjacencyListList(triangles);
            
            // UVアイランドの三角形を列挙
            var islandTriangles = EditorUvUtils.EnumerateUVIslandTriangles(triangles, adjacency, seedTriangle);

            // 各三角形をラスタライズしてマスクに反映
            foreach (int triIndex in islandTriangles)
            {
                int baseIdx = triIndex * 3;
                if (baseIdx + 2 >= triangles.Length)
                    continue;

                int ia = triangles[baseIdx];
                int ib = triangles[baseIdx + 1];
                int ic = triangles[baseIdx + 2];

                if (ia >= uvs.Length || ib >= uvs.Length || ic >= uvs.Length)
                    continue;

                Vector2 uvA = uvs[ia];
                Vector2 uvB = uvs[ib];
                Vector2 uvC = uvs[ic];

                // 三角形をラスタライズしてピクセル座標に変換
                RasterizeTriangleToMask(uvA, uvB, uvC, selectedPixels);
            }

            return selectedPixels;
        }

        /// <summary>
        /// 三角形をラスタライズしてマスクに反映
        /// </summary>
        private static void RasterizeTriangleToMask(Vector2 uv0, Vector2 uv1, Vector2 uv2, HashSet<Vector2Int> pixels)
        {
            TriangleRasterizer.RasterizeToSet(uv0, uv1, uv2, pixels);
        }

        /// <summary>
        /// 8近傍連結で島を抽出（Flood Fill）
        /// MaskBufferPool を使用してGCアロケーションを削減
        /// </summary>
        public static HashSet<Vector2Int> ExtractIsland(int startX, int startY, bool[,] mask, bool includeEmpty = false)
        {
            var island = new HashSet<Vector2Int>();
            if (startX < 0 || startX >= Common.TexSize || startY < 0 || startY >= Common.TexSize)
                return island;

            var stack = new Stack<Vector2Int>();
            var visited = MaskBufferPool.Rent();
            
            try
            {
                System.Array.Clear(visited, 0, visited.Length);
                bool targetValue = includeEmpty ? false : mask[startX, startY];

                stack.Push(new Vector2Int(startX, startY));

                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    int x = current.x;
                    int y = current.y;

                    if (x < 0 || x >= Common.TexSize || y < 0 || y >= Common.TexSize)
                        continue;

                    if (visited[x, y])
                        continue;

                    bool currentValue = includeEmpty ? false : mask[x, y];
                    if (currentValue != targetValue)
                        continue;

                    visited[x, y] = true;
                    island.Add(current);

                    // 8近傍を探索
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0)
                                continue;

                            int nx = x + dx;
                            int ny = y + dy;

                            if (nx >= 0 && nx < Common.TexSize && ny >= 0 && ny < Common.TexSize && !visited[nx, ny])
                            {
                                stack.Push(new Vector2Int(nx, ny));
                            }
                        }
                    }
                }
            }
            finally
            {
                MaskBufferPool.Return(visited);
            }

            return island;
        }

        /// <summary>
        /// 矩形領域に触れる島を全て抽出
        /// MaskBufferPool を使用してGCアロケーションを削減
        /// </summary>
        public static HashSet<Vector2Int> ExtractIslandsInRectangle(RectInt rect, bool[,] mask)
        {
            var islands = new HashSet<Vector2Int>();
            var processed = MaskBufferPool.Rent();

            try
            {
                System.Array.Clear(processed, 0, processed.Length);
                
                // 矩形内の各ピクセルから未処理の島を抽出
                for (int y = rect.yMin; y < rect.yMax && y < Common.TexSize; y++)
                {
                    for (int x = rect.xMin; x < rect.xMax && x < Common.TexSize; x++)
                    {
                        if (x < 0 || y < 0 || processed[x, y])
                            continue;

                        // マスクが選択状態のピクセルから島を抽出
                        if (mask[x, y])
                        {
                            var island = ExtractIsland(x, y, mask);
                            foreach (var p in island)
                            {
                                if (p.x >= 0 && p.x < Common.TexSize && p.y >= 0 && p.y < Common.TexSize)
                                {
                                    processed[p.x, p.y] = true;
                                    islands.Add(p);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                MaskBufferPool.Return(processed);
            }

            return islands;
        }

        /// <summary>
        /// 投げ縄領域内のピクセルを抽出（任意領域マスク用：ポリゴン内点判定）
        /// </summary>
        public static HashSet<Vector2Int> ExtractPixelsInLasso(List<Vector2> lassoPoints)
        {
            var pixels = new HashSet<Vector2Int>();
            if (lassoPoints == null || lassoPoints.Count < 3)
                return pixels;

            // バウンディングボックスを計算
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in lassoPoints)
            {
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            int xMin = Mathf.Max(0, Mathf.FloorToInt(minX));
            int xMax = Mathf.Min(Common.TexSize - 1, Mathf.CeilToInt(maxX));
            int yMin = Mathf.Max(0, Mathf.FloorToInt(minY));
            int yMax = Mathf.Min(Common.TexSize - 1, Mathf.CeilToInt(maxY));

            // バウンディングボックス内でポリゴン内点判定
            // ピクセルの中心点で判定
            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    Vector2 pixelCenter = new Vector2(x + 0.5f, y + 0.5f);
                    if (IsPointInPolygon(pixelCenter, lassoPoints))
                    {
                        pixels.Add(new Vector2Int(x, y));
                    }
                }
            }

            return pixels;
        }

        /// <summary>
        /// 投げ縄領域内の島を抽出（簡易実装：ポリゴン内点判定）
        /// MaskBufferPool を使用してGCアロケーションを削減
        /// </summary>
        public static HashSet<Vector2Int> ExtractIslandsInLasso(List<Vector2> lassoPoints, bool[,] mask)
        {
            var islands = new HashSet<Vector2Int>();
            if (lassoPoints == null || lassoPoints.Count < 3)
                return islands;

            var processed = MaskBufferPool.Rent();

            try
            {
                System.Array.Clear(processed, 0, processed.Length);

                // バウンディングボックスを計算
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                foreach (var p in lassoPoints)
                {
                    minX = Mathf.Min(minX, p.x);
                    maxX = Mathf.Max(maxX, p.x);
                    minY = Mathf.Min(minY, p.y);
                    maxY = Mathf.Max(maxY, p.y);
                }

                int xMin = Mathf.Max(0, Mathf.FloorToInt(minX));
                int xMax = Mathf.Min(Common.TexSize - 1, Mathf.CeilToInt(maxX));
                int yMin = Mathf.Max(0, Mathf.FloorToInt(minY));
                int yMax = Mathf.Min(Common.TexSize - 1, Mathf.CeilToInt(maxY));

                // バウンディングボックス内でポリゴン内点判定
                for (int y = yMin; y <= yMax; y++)
                {
                    for (int x = xMin; x <= xMax; x++)
                    {
                        if (processed[x, y])
                            continue;

                        if (IsPointInPolygon(new Vector2(x, y), lassoPoints))
                        {
                            if (mask[x, y])
                            {
                                var island = ExtractIsland(x, y, mask);
                                foreach (var p in island)
                                {
                                    if (p.x >= 0 && p.x < Common.TexSize && p.y >= 0 && p.y < Common.TexSize)
                                    {
                                        processed[p.x, p.y] = true;
                                        islands.Add(p);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                MaskBufferPool.Return(processed);
            }

            return islands;
        }

        /// <summary>
        /// ポイントがポリゴン内にあるか判定（Ray Casting Algorithm）
        /// </summary>
        private static bool IsPointInPolygon(Vector2 point, List<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3)
                return false;

            bool inside = false;
            int j = polygon.Count - 1;

            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 pi = polygon[i];
                Vector2 pj = polygon[j];

                if (((pi.y > point.y) != (pj.y > point.y)) &&
                    (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x))
                {
                    inside = !inside;
                }

                j = i;
            }

            return inside;
        }

        /// <summary>
        /// 選択演算を適用
        /// </summary>
        public enum SelectionOperation
        {
            Replace,    // 置換
            Add,        // 追加（Shift）
            Subtract,   // 減算（Alt）
            Toggle      // 切替（Ctrl）
        }

        public static void ApplySelectionOperation(
            HashSet<Vector2Int> island,
            bool[,] baseMask,
            SelectionOperation operation)
        {
            foreach (var p in island)
            {
                int x = p.x;
                int y = p.y;
                if (x < 0 || x >= Common.TexSize || y < 0 || y >= Common.TexSize)
                    continue;

                switch (operation)
                {
                    case SelectionOperation.Replace:
                        baseMask[x, y] = true;
                        break;
                    case SelectionOperation.Add:
                        baseMask[x, y] = true;
                        break;
                    case SelectionOperation.Subtract:
                        baseMask[x, y] = false;
                        break;
                    case SelectionOperation.Toggle:
                        baseMask[x, y] = !baseMask[x, y];
                        break;
                }
            }
        }
    }
}

