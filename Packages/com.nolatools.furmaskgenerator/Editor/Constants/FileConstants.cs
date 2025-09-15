#if UNITY_EDITOR
namespace NolaTools.FurMaskGenerator.Constants
{
    /// <summary>
    /// ファイル関連の定数とパスを管理
    /// </summary>
    public static class FileConstants
    {
        #region ファイル拡張子
        public const string FILE_EXTENSION_PNG = "png";
        public const string FILE_EXTENSION_ASSET = ".asset";
        #endregion

        #region パス関連
        public const string PATH_SEPARATOR = "/";
        public const char PATH_SEPARATOR_CHAR = '/';
        public const string SETTINGS_ASSET_FOLDER = "Assets/FurMaskGenerator";
        public const string SETTINGS_ASSET_FILE = "FurMaskSettings";
        public const string SETTINGS_ASSET_PATH = SETTINGS_ASSET_FOLDER + PATH_SEPARATOR + SETTINGS_ASSET_FILE + FILE_EXTENSION_ASSET;
        public const string AVATAR_SETTINGS_ROOT = "Assets/FurMaskGenerator/AvatarSettings";
        #endregion

        #region メニューとウィンドウ
        public const string MENU_ITEM_PATH = "Tools/NolaTools/FurMaskGenerator";
        public const string WINDOW_TITLE = "FurMaskGenerator";
        #endregion

        #region EditorPrefs キー
        public const string LAST_SAVE_DIRECTORY_KEY = "NolaTools.FurMaskGenerator.LastSaveDirectory";
        public const string LAST_AVATAR_PATH_KEY = "NolaTools.FurMaskGenerator.LastAvatarPath";
        #endregion

        #region ファイル名関連
        public const string FUR_LENGTH_MASK_SUFFIX = "_FurLenMask";
        public const string DEFAULT_FILE_NAME = "Untitled";
        public const string HEX_FORMAT_UPPER = "X2";
        public const string GUI_STYLE_BOX = "box";
        #endregion

        #region アセット関連
        public const string ASSET_MENU_NAME = "Fur Mask Generator/Fur Mask Settings";
        #endregion
    }
}
#endif