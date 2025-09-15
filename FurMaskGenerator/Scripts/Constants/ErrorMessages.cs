namespace Mask.Generator.Constants
{
    /// <summary>
    /// エラーメッセージとログメッセージを管理
    /// </summary>
    public static class ErrorMessages
    {
        #region アバター関連エラー
        public const string ERROR_AVATAR_NOT_SET = "アバターが設定されていません。";
        public const string ERROR_HUMANOID_REQUIRED = "Humanoidアバターが見つかりませんでした。アバターにAnimator(Humanoid)を設定してください。";
        public const string ERROR_INVALID_HUMANOID = "選択されたオブジェクトは有効な Humanoid アバターではありません。";
        #endregion

        #region レンダラー関連エラー
        public const string ERROR_AVATAR_RENDERERS_INVALID = "アバターのレンダラーが設定されていません、無効です。";
        public const string ERROR_CLOTH_RENDERERS_INVALID = "服のレンダラーが設定されていません、無効です。";
        public const string ERROR_MESH_NOT_FOUND = "レンダラーからメッシュを取得できませんでした:";
        public const string ERROR_RENDERER_NOT_IN_LIST = "クリック対象のレンダラーが設定リストに含まれていません。";
        public const string ERROR_SKINNED_MESH_NOT_FOUND = "SkinnedMeshRenderer が見つかりません。";
        public const string ERROR_NO_VALID_RENDERERS = "有効なSkinnedMeshRendererが見つかりません。自動設定を実行してください。";
        #endregion

        #region UV関連エラー
        public const string ERROR_UV_NOT_FOUND = "メッシュに UV がありません。";
        public const string ERROR_RAYCAST_HIT_NOT_FOUND = "クリック位置から有効なメッシュ三角形を取得できませんでした。";
        #endregion

        #region 設定関連エラー
        public const string ERROR_SETTINGS_NOT_ASSIGNED = "設定ファイルが割り当てられていません。";
        public const string ERROR_SETTINGS_LOAD_FAILED = "設定ファイルの読み込みに失敗しました。ウィンドウを再起動してください。";
        public const string ERROR_NO_PREVIEW = "プレビューがありません。";
        public const string ERROR_TEXTURE_NULL = "テクスチャがnullです。";
        public const string ERROR_RENDERERS_NOT_SET = "レンダラーが設定されていません。";
        public const string ERROR_UV_MASKS_NOT_SET = "UVマスクが設定されていません。";
        public const string ERROR_VALID_UV_MASKS_NOT_FOUND = "有効なUVマスクが見つかりませんでした。";
        #endregion

        #region テクスチャ関連エラー
        public const string TEXTURE_AUTO_DETECT_TITLE = "テクスチャサイズ自動検出";
        public const string TEXTURE_AUTO_DETECT_NO_TEXTURE = "有効なテクスチャが見つかりませんでした。\n手動でサイズを設定してください。";
        public const string TEXTURE_AUTO_DETECT_NO_SIZE = "適切なテクスチャサイズが見つかりませんでした。";
        public const string ERROR_TEXTURE_NOT_READABLE = "テクスチャ '{0}' は読み取り可能になっていません。\nTexture Import Settings で 'Read/Write Enabled' を有効にしてください。";
        #endregion

        #region 処理関連エラー
        public const string ERROR_GUI_DRAW = "GUI描画中にエラーが発生しました: {0}";
        public const string ERROR_SPHERE_FOLDOUT_INDEX = "sphereFoldoutStates index out of range: {0} >= {1}";
        public const string ERROR_SPHERE_MASK_SETTINGS = "DrawSphereMaskSettingsでエラーが発生しました: {0}";
        public const string ERROR_UV_MASK_OVERLAY_GENERATION = "UVマスクオーバーレイテクスチャの生成中にエラーが発生しました: {0}";
        public const string ERROR_UV_ISLAND_ACQUISITION = "UVアイランド取得中にエラーが発生しました: {0}";
        public const string ERROR_BAKE_PROCESS = "Bake処理中にエラーが発生しました: {0}";
        public const string ERROR_COMPLETE_PROCESS = "完了処理中にエラーが発生しました: {0}";
        public const string ERROR_BAKE_EXCEPTION = "ベイク処理中にエラーが発生しました: {0}";
        public const string WARNING_NORMAL_MAP_NOT_READABLE = "ノーマルマップ '{0}' が読み取り可能になっていません。Texture Import Settings で 'Read/Write Enabled' を有効にしてください。";
        #endregion

        #region 情報メッセージ
        public const string INFO_MOUTH_BLEND_NOT_FOUND = "口系ブレンドシェイプが検出できなかったため、口元スフィアの自動追加をスキップしました。";
        #endregion

        #region デバッグログ
        public const string LOG_HIERARCHY_CHANGE_IGNORED = "[FurMaskGenerator] OnHierarchyChange ignored during bake (ClothCollider creation)";
        public const string LOG_HIERARCHY_CHANGE_CALLED = "[FurMaskGenerator] OnHierarchyChange called - AvatarRenderers before: {0}, ClothRenderers before: {1}";
        public const string LOG_HIERARCHY_CHANGE_COMPLETED = "[FurMaskGenerator] OnHierarchyChange completed - AvatarRenderers after: {0}, ClothRenderers after: {1}";
        public const string LOG_AUTO_DETECT_CALLED = "[FurMaskGenerator] AutoDetectRenderers called - AvatarRenderers before: {0}, ClothRenderers before: {1}";
        public const string LOG_AUTO_DETECT_COMPLETED = "[FurMaskGenerator] AutoDetectRenderers completed - AvatarRenderers after: {0}, ClothRenderers after: {1}";
        public const string LOG_START_BAKE = "[FurMaskGenerator] StartBake - AvatarRenderers: {0}, ClothRenderers: {1}";
        public const string LOG_START_BAKE_IGNORE_SET = "[FurMaskGenerator] StartBake - ignoreHierarchyChangeDuringBake set to true";
        public const string LOG_START_BAKE_VALIDATION_FAILED = "[FurMaskGenerator] StartBake validation failed - ignoreHierarchyChangeDuringBake set to false";
        public const string LOG_BAKE_COMPLETED_IGNORE_RESET = "[FurMaskGenerator] OnBakeCompleted - ignoreHierarchyChangeDuringBake set to false";
        public const string LOG_BAKE_COMPLETED = "[FurMaskGenerator] OnBakeCompleted - AvatarRenderers: {0}, ClothRenderers: {1}";
        public const string LOG_BAKE_CANCELLED_IGNORE_RESET = "[FurMaskGenerator] OnBakeCancelled - ignoreHierarchyChangeDuringBake set to false";
        public const string LOG_BAKE_CANCELLED = "[FurMaskGenerator] OnBakeCancelled - AvatarRenderers: {0}, ClothRenderers: {1}";
        #endregion
    }
}