using System.Collections.Generic;
using UnityEngine;

namespace GroomingTool2.Services
{
    /// <summary>
    /// ストローク処理の実装
    /// </summary>
    internal sealed class StrokeService : IStrokeService
    {
        public Vector2 CalculateAverageDirection(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 2)
                return Vector2.zero;

            var total = Vector2.zero;
            var prev = points[0];

            for (var i = 1; i < points.Count; i++)
            {
                var current = points[i];
                total += current - prev;
                prev = current;
            }

            return total / (points.Count - 1);
        }

        public float CalculateAverageDirectionRadian(IReadOnlyList<Vector2> points)
        {
            var avgDirection = CalculateAverageDirection(points);
            float avgLengthSqr = avgDirection.sqrMagnitude;

            if (avgLengthSqr < 0.0001f) // 0.01^2 = 0.0001
            {
                return 0f; // デフォルト値
            }

            return Mathf.Atan2(avgDirection.y, avgDirection.x);
        }
    }
}

