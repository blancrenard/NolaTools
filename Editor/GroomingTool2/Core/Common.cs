using System;
using System.Collections.Generic;
using UnityEngine;

namespace GroomingTool2.Core
{
    public static class Common
    {
        public const int TexSize = 1024;
        public const int TexSizeSquared = TexSize * TexSize;
        public const int Grid = 16;

        /// <summary>
        /// 2次元座標を1次元インデックスに変換
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static int GetIndex(int x, int y) => y * TexSize + x;

        public static List<MyBrushData> CreateBrush(int size)
        {
            var diameter = size * 2;
            var circlePoints = GetCircle(diameter);

            // パフォーマンス最適化：大きなブラシサイズ時はサンプリングを間引く
            if (size > 64)
            {
                // 大きなブラシ時は処理を間引いてパフォーマンスを向上
                var skipStep = Mathf.Max(1, size / 32);
                var filteredPoints = new List<Vector2Int>();

                for (var i = 0; i < circlePoints.Count; i += skipStep)
                {
                    filteredPoints.Add(circlePoints[i]);
                }

                circlePoints = filteredPoints;
            }

            circlePoints.Sort((a, b) => a.y.CompareTo(b.y));

            var filledPoints = new List<Vector2Int>(circlePoints);
            var currentY = circlePoints.Count > 0 ? circlePoints[0].y : 0;
            var rowPoints = new List<Vector2Int>();

            foreach (var point in circlePoints)
            {
                if (point.y != currentY)
                {
                    FillRowPoints(rowPoints, filledPoints, currentY);
                    currentY = point.y;
                    rowPoints.Clear();
                }

                rowPoints.Add(point);
            }

            FillRowPoints(rowPoints, filledPoints, currentY);

            var brush = new List<MyBrushData>(filledPoints.Count);
            var radiusSquared = (size * 2f) * (size * 2f);

            foreach (var point in filledPoints)
            {
                var distanceSquared = point.x * point.x + point.y * point.y;
                var influence = ((radiusSquared - distanceSquared) / radiusSquared) * 0.9f + 0.1f;
                if (influence <= 0f)
                    continue;

                var clampedInfluence = Mathf.Clamp01(influence);
                brush.Add(new MyBrushData(point.x, point.y, clampedInfluence));
            }

            return brush;
        }

        private static void FillRowPoints(List<Vector2Int> rowPoints, List<Vector2Int> filledPoints, int rowY)
        {
            if (rowPoints.Count == 0)
                return;

            var minX = int.MaxValue;
            var maxX = int.MinValue;

            // HashSetを使用して存在チェックをO(1)に改善
            var existingXs = new HashSet<int>();
            foreach (var point in rowPoints)
            {
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                existingXs.Add(point.x);
            }

            for (var x = minX + 1; x < maxX; x++)
            {
                if (!existingXs.Contains(x))
                    filledPoints.Add(new Vector2Int(x, rowY));
            }
        }

        public static List<Vector2Int> GetLine(int x1, int y1, int x2, int y2)
        {
            var points = new List<Vector2Int>();
            AppendLinePoints(x1, y1, x2, y2, points);
            return points;
        }

        public static void AppendLinePoints(int x1, int y1, int x2, int y2, List<Vector2Int> dst)
        {
            var dx = Mathf.Abs(x2 - x1);
            var dy = Mathf.Abs(y2 - y1);
            var sx = x1 < x2 ? 1 : -1;
            var sy = y1 < y2 ? 1 : -1;

            var totalLength = Mathf.Max(dx, dy);
            var step = 1;
            if (totalLength > 100)
            {
                step = totalLength / 50;
            }
            else if (totalLength > 50)
            {
                step = totalLength / 25;
            }

            if (dx >= dy)
            {
                var err = dx / 2;
                var stepsTaken = 0;
                for (var _ = 0; _ <= dx; _++)
                {
                    if (stepsTaken % step == 0)
                    {
                        dst.Add(new Vector2Int(x1, y1));
                    }
                    x1 += sx;
                    err += dy;
                    stepsTaken++;
                    if (err >= dx)
                    {
                        y1 += sy;
                        err -= dx;
                    }
                }
            }
            else
            {
                var err = dy / 2;
                var stepsTaken = 0;
                for (var _ = 0; _ <= dy; _++)
                {
                    if (stepsTaken % step == 0)
                    {
                        dst.Add(new Vector2Int(x1, y1));
                    }
                    y1 += sy;
                    err += dx;
                    stepsTaken++;
                    if (err >= dy)
                    {
                        x1 += sx;
                        err -= dy;
                    }
                }
            }

            if (dst.Count == 0 || dst[dst.Count - 1] != new Vector2Int(x2, y2))
            {
                dst.Add(new Vector2Int(x2, y2));
            }
        }

        /// <summary>
        /// radius は偶数のみを受け付ける
        /// </summary>
        public static List<Vector2Int> GetCircle(int radius)
        {
            if (radius % 2 == 1)
                throw new ArgumentException("radius は偶数のみ指定可能です", nameof(radius));

            var points = new List<Vector2Int>();
            var cx = 0;
            var cy = radius;
            var d = 2 - 2 * radius;

            points.Add(new Vector2Int(cx, cy));
            points.Add(new Vector2Int(cx, -cy));
            points.Add(new Vector2Int(cy, cx));
            points.Add(new Vector2Int(-cy, cx));

            while (true)
            {
                if (d > -cy)
                {
                    cy--;
                    d += 1 - 2 * cy;
                }

                if (d <= cx)
                {
                    cx++;
                    d += 1 + 2 * cx;
                }

                if (cy == 0)
                    break;

                points.Add(new Vector2Int(cx, cy));
                points.Add(new Vector2Int(-cx, cy));
                points.Add(new Vector2Int(-cx, -cy));
                points.Add(new Vector2Int(cx, -cy));
            }

            return points;
        }
    }
}



