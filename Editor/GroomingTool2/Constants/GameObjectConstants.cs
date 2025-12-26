namespace GroomingTool2.Constants
{
    /// <summary>
    /// マテリアル関連で参照する最低限の定数を保持
    /// </summary>
    internal static class GameObjectConstants
    {
        /// <summary>
        /// メインテクスチャとして試行するプロパティ名一覧
        /// </summary>
        public static readonly string[] MAIN_TEXTURE_PROPERTIES = { "_MainTex", "_BaseMap", "_AlbedoMap", "_DiffuseMap" };

        // 名前フィルタリング用（耳アクセサリ除外など）
        public const string FILTER_WEAR = "wear";
        public const string FILTER_EARRING = "earring";
    }
}

