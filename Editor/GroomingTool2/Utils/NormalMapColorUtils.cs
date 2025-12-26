using System.Runtime.CompilerServices;
using GroomingTool2.Core;
using UnityEngine;

namespace GroomingTool2.Utils
{
    /// <summary>
    /// ノーマルマップ色変換のユーティリティ
    /// FurDataRenderer と GpuFurDataRenderer で共有
    /// </summary>
    internal static class NormalMapColorUtils
    {
        /// <summary>
        /// FurDataからノーマルマップ色を計算
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color GetNormalMapColor(FurData data)
        {
            float cos = AngleLut.GetCos(data.Dir);
            float sin = AngleLut.GetSin(data.Dir);
            float inclined = Mathf.Min(data.Inclined, 0.95f);
            var normal = new Vector3(
                cos * inclined,
                sin * inclined,
                Mathf.Sqrt(1f - Mathf.Clamp01(inclined * inclined))
            );
            var mapped = normal.normalized * 0.5f + Vector3.one * 0.5f;
            mapped.y = 1f - mapped.y; // 上下反転（Gチャンネル反転）
            return new Color(mapped.x, mapped.y, mapped.z, 1f);
        }
    }
}
