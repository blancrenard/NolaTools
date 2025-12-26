using System.Collections.Generic;
using UnityEngine;

namespace GroomingTool2.Services
{
    /// <summary>
    /// ストローク処理を提供するサービス
    /// </summary>
    internal interface IStrokeService
    {
        /// <summary>
        /// ストローク点列から平均方向を計算
        /// </summary>
        Vector2 CalculateAverageDirection(IReadOnlyList<Vector2> points);

        /// <summary>
        /// ストローク点列から平均方向を計算（ラジアン）
        /// </summary>
        float CalculateAverageDirectionRadian(IReadOnlyList<Vector2> points);
    }
}

