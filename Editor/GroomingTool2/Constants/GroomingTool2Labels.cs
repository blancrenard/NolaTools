using NolaTools;

namespace GroomingTool2.Constants
{
    /// <summary>
    /// GroomingTool2 のUI表示用文字列を一元管理するクラス
    /// </summary>
    internal static class GroomingTool2Labels
    {
        private static string L(string jp, string en) => NolaToolsLocalization.L(jp, en);

        #region 初期化エラー
        public static string STATE_INIT_FAILED => L("State の初期化に失敗しました。", "Failed to initialize State.");
        public static string STATE_RELOAD_BUTTON => L("State を作成/再読み込み", "Create/Reload State");
        #endregion

        #region メニューバー
        public static string FILE_MENU => L("ファイル", "File");
        public static string EDIT_MENU => L("編集", "Edit");
        public static string VIEW_MENU => L("表示", "View");
        public static string SETTINGS_MENU => L("設定", "Settings");
        #endregion

        #region ファイルメニュー
        public static string LOAD_FUR_DATA => L("毛データ読込", "Load Fur Data");
        public static string IMPORT_NORMAL_MAP => L("ノーマル読込", "Import Normal Map");
        public static string SAVE_FUR_DATA => L("毛データ保存", "Save Fur Data");
        public static string EXPORT_NORMAL_MAP => L("ノーマル保存", "Export Normal Map");
        #endregion

        #region 編集メニュー
        public static string UNDO => L("元に戻す（Undo）", "Undo");
        public static string REDO => L("やり直し（Redo）", "Redo");
        #endregion

        #region 表示メニュー
        public static string UV_COLOR_SUBMENU => L("UVの色", "UV Color");
        public static string DOT_INTERVAL_SUBMENU => L("ドット間隔", "Dot Interval");
        public static string SCENE_HAIR_COLOR_SUBMENU => L("Scene毛の色", "Scene Fur Color");
        public static string SCENE_DENSITY_SUBMENU => L("Scene毛密度", "Scene Fur Density");
        public static string COLOR_WHITE => L("白", "White");
        public static string COLOR_RED => L("赤", "Red");
        public static string COLOR_GREEN => L("緑", "Green");
        public static string COLOR_BLUE => L("青", "Blue");
        public static string COLOR_BLACK => L("黒", "Black");
        public static string DENSITY_HIGH => L("高密度", "High");
        public static string DENSITY_LOW => L("低密度", "Low");
        #endregion

        #region 設定メニュー
        public static string RENDERING_SUBMENU => L("レンダリング", "Rendering");
        public static string GPU_RENDERING => L("GPU（高速）", "GPU (Fast)");
        public static string CPU_RENDERING => L("CPU（互換性重視）", "CPU (Compatible)");
        public static string EDIT_WITHIN_UV_ONLY => L("UV内のみ編集する", "Edit within UV only");
        public static string UV_PADDING_SUBMENU => L("UVパディング", "UV Padding");
        #endregion

        #region マスクモード
        public static string SELECTION_MODE => L("選択モード", "Selection Mode");
        public static string MASK_CLICK => L("クリック", "Click");
        public static string MASK_RECT => L("矩形", "Rect");
        public static string MASK_LASSO => L("投げ縄", "Lasso");
        public static string CLEAR_MASK_BUTTON => L("マスクをクリア", "Clear Mask");
        public static string CLEAR_MASK_UNDO => L("マスククリア", "Clear Mask");
        #endregion

        #region 右パネル
        public static string AVATAR_LABEL => L("アバター", "Avatar");
        public static string AVATAR_NOT_SET_WARNING => L("アバターを指定してください。", "Please select an avatar.");
        public static string BACKGROUND_LABEL => L("背景", "Background");
        public static string NO_MATERIAL => L("（マテリアルなし）", "(No Material)");
        public static string AUTO_SETUP_SECTION => L("自動設定", "Auto-Setup");
        public static string AUTO_SETUP_BUTTON => L("自動設定", "Auto-Setup");
        #endregion

        #region 自動設定ダイアログ
        public static string AUTO_SETUP_ERROR_TITLE => L("エラー", "Error");
        public static string AUTO_SETUP_ERROR_NO_AVATAR => L(
            "アバターとマテリアルを選択してください。",
            "Please select an avatar and material.");
        public static string AUTO_SETUP_CONFIRM_TITLE => L("自動設定", "Auto-Setup");
        public static string AUTO_SETUP_CONFIRM_MESSAGE => L(
            "現在の毛データを上書きして自動設定を実行しますか？",
            "This will overwrite the current fur data. Run Auto-Setup?");
        public static string AUTO_SETUP_RUN => L("実行", "Run");
        public static string AUTO_SETUP_CANCEL => L("キャンセル", "Cancel");
        public static string AUTO_SETUP_UNDO => L("自動設定", "Auto-Setup");
        public static string AUTO_SETUP_ERROR_MESSAGE(string detail) =>
            L($"自動設定中にエラーが発生しました:\n{detail}", $"An error occurred during Auto-Setup:\n{detail}");
        #endregion
    }
}
