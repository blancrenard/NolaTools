using System.Collections.Generic;
using UnityEngine;

namespace GroomingTool2.Services
{
    /// <summary>
    /// 対称処理を提供するサービス
    /// </summary>
    internal interface ISymmetryService
    {
        /// <summary>
        /// UV座標列から対称UV座標列を生成
        /// </summary>
        /// <param name="points">元のUV座標列</param>
        /// <param name="allMirrored">すべてのポイントで対称UV座標を取得できた場合true</param>
        /// <returns>対称UV座標列</returns>
        List<Vector2Int> GetMirrorPoints(IReadOnlyList<Vector2Int> points, out bool allMirrored);
    }
}
