#if UNITY_EDITOR
using NolaTools;

namespace NolaTools.FurMaskGenerator.Constants
{
    /// <summary>
    /// エラーメッセージとログメッセージを管理
    /// </summary>
    public static class ErrorMessages
    {
        private static string L(string jp, string en) => NolaToolsLocalization.L(jp, en);

        #region アバター関連エラー
        public static string ERROR_AVATAR_NOT_SET => L(
            "アバターが設定されていません。",
            "Avatar is not set.");
        public static string ERROR_HUMANOID_REQUIRED => L(
            "Humanoidアバターが見つかりませんでした。アバターにAnimator(Humanoid)を設定してください。",
            "No Humanoid avatar found. Please set an Animator (Humanoid) on the avatar.");
        public static string ERROR_INVALID_HUMANOID => L(
            "選択されたオブジェクトは有効な Humanoid アバターではありません。",
            "The selected object is not a valid Humanoid avatar.");
        #endregion

        #region レンダラー関連エラー
        public static string ERROR_AVATAR_RENDERERS_INVALID => L(
            "アバターのレンダラーが設定されていません、無効です。",
            "Avatar renderers are not set or invalid.");
        public static string ERROR_CLOTH_RENDERERS_INVALID => L(
            "服のレンダラーが設定されていません、無効です。",
            "Cloth renderers are not set or invalid.");
        public static string ERROR_MESH_NOT_FOUND => L(
            "レンダラーからメッシュを取得できませんでした:",
            "Could not get mesh from renderer:");
        public static string ERROR_RENDERER_NOT_IN_LIST => L(
            "クリック対象のレンダラーが設定リストに含まれていません。",
            "The clicked renderer is not in the configured list.");
        public static string ERROR_SKINNED_MESH_NOT_FOUND => L(
            "SkinnedMeshRenderer が見つかりません。",
            "SkinnedMeshRenderer not found.");
        public static string ERROR_NO_VALID_RENDERERS => L(
            "有効なSkinnedMeshRendererが見つかりません。自動設定を実行してください。",
            "No valid SkinnedMeshRenderer found. Please run auto-detect.");
        #endregion

        #region UV関連エラー
        public static string ERROR_UV_NOT_FOUND => L(
            "メッシュに UV がありません。",
            "The mesh has no UVs.");
        public static string ERROR_RAYCAST_HIT_NOT_FOUND => L(
            "クリック位置から有効なメッシュ三角形を取得できませんでした。",
            "Could not get a valid mesh triangle from the click position.");
        #endregion

        #region 設定関連エラー
        public static string ERROR_SETTINGS_NOT_ASSIGNED => L(
            "設定ファイルが割り当てられていません。",
            "Settings file is not assigned.");
        public static string ERROR_SETTINGS_LOAD_FAILED => L(
            "設定ファイルの読み込みに失敗しました。ウィンドウを再起動してください。",
            "Failed to load settings file. Please restart the window.");
        public static string ERROR_NO_PREVIEW => L(
            "プレビューがありません。",
            "No preview available.");
        public static string ERROR_TEXTURE_NULL => L(
            "テクスチャがnullです。",
            "Texture is null.");
        public static string ERROR_RENDERERS_NOT_SET => L(
            "レンダラーが設定されていません。",
            "Renderers are not set.");
        public static string ERROR_UV_MASKS_NOT_SET => L(
            "UVマスクが設定されていません。",
            "UV masks are not set.");
        public static string ERROR_VALID_UV_MASKS_NOT_FOUND => L(
            "有効なUVマスクが見つかりませんでした。",
            "No valid UV masks found.");
        #endregion

        #region テクスチャ関連エラー
        public static string TEXTURE_AUTO_DETECT_TITLE => L(
            "テクスチャサイズ自動検出",
            "Auto-detect Texture Size");
        public static string TEXTURE_AUTO_DETECT_NO_TEXTURE => L(
            "有効なテクスチャが見つかりませんでした。\n手動でサイズを設定してください。",
            "No valid texture found.\nPlease set the size manually.");
        public static string TEXTURE_AUTO_DETECT_NO_SIZE => L(
            "適切なテクスチャサイズが見つかりませんでした。",
            "No appropriate texture size found.");
        public static string ERROR_TEXTURE_NOT_READABLE => L(
            "テクスチャ '{0}' は読み取り可能になっていません。\nTexture Import Settings で 'Read/Write Enabled' を有効にしてください。",
            "Texture '{0}' is not readable.\nPlease enable 'Read/Write Enabled' in Texture Import Settings.");
        #endregion

        #region 処理関連エラー
        public static string ERROR_GUI_DRAW => L(
            "GUI描画中にエラーが発生しました: {0}",
            "An error occurred while drawing the GUI: {0}");
        public const string ERROR_SPHERE_FOLDOUT_INDEX = "sphereFoldoutStates index out of range: {0} >= {1}";
        public static string ERROR_SPHERE_MASK_SETTINGS => L(
            "DrawSphereMaskSettingsでエラーが発生しました: {0}",
            "An error occurred in DrawSphereMaskSettings: {0}");
        public static string ERROR_UV_MASK_OVERLAY_GENERATION => L(
            "UVマスクオーバーレイテクスチャの生成中にエラーが発生しました: {0}",
            "An error occurred while generating the UV mask overlay texture: {0}");
        public static string ERROR_UV_ISLAND_ACQUISITION => L(
            "UVアイランド取得中にエラーが発生しました: {0}",
            "An error occurred while acquiring UV islands: {0}");
        public static string ERROR_BAKE_PROCESS => L(
            "Bake処理中にエラーが発生しました: {0}",
            "An error occurred during baking: {0}");
        public static string ERROR_COMPLETE_PROCESS => L(
            "完了処理中にエラーが発生しました: {0}",
            "An error occurred during completion: {0}");
        public static string ERROR_BAKE_EXCEPTION => L(
            "ベイク処理中にエラーが発生しました: {0}",
            "An exception occurred during baking: {0}");
        public static string WARNING_NORMAL_MAP_NOT_READABLE => L(
            "ノーマルマップ '{0}' が読み取り可能になっていません。Texture Import Settings で 'Read/Write Enabled' を有効にしてください。",
            "Normal map '{0}' is not readable. Please enable 'Read/Write Enabled' in Texture Import Settings.");
        #endregion

        #region ノーマルマップ自動検出
        public static string ERROR_RENDERERS_NOT_CONFIGURED => L(
            "レンダラーが設定されていません。先にアバターとレンダラーを設定してください。",
            "Renderers are not configured. Please set up the avatar and renderers first.");
        public static string INFO_NORMAL_MAP_NOT_FOUND_LENGTH_SET => L(
            "ノーマルマップは見つかりませんでしたが、長さを自動設定しました。",
            "No normal map found, but fur length was auto-set.");
        public static string INFO_NORMAL_MAP_AND_LENGTH_NOT_FOUND => L(
            "ノーマルマップと長さの設定が見つかりませんでした。",
            "Neither normal map nor fur length setting was found.");
        public const string LOG_PREFIX = "[FurMaskGenerator] {0}";
        #endregion
    }
}
#endif
