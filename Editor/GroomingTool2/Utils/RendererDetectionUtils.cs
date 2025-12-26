using GroomingTool2.Constants;

namespace GroomingTool2.Utils
{
    /// <summary>
    /// レンダラー検出や名前フィルタリングの簡易ユーティリティ
    /// </summary>
    internal static class RendererDetectionUtils
    {
        /// <summary>
        /// 耳アクセサリ系の名称かどうか（除外用）
        /// </summary>
        public static bool IsEarAccessoryName(string nameOrLower)
        {
            if (string.IsNullOrEmpty(nameOrLower)) return false;
            var s = nameOrLower.ToLowerInvariant();
            return s.Contains(GameObjectConstants.FILTER_WEAR) || s.Contains(GameObjectConstants.FILTER_EARRING);
        }
    }
}

