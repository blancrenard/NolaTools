using UnityEngine;

namespace Mask.Generator.Constants
{
    /// <summary>
    /// UI表示用のラベルとメッセージを管理
    /// </summary>
    public static class UILabels
    {
        #region 共通ダイアログ
        public const string ERROR_DIALOG_TITLE = "エラー";
        public const string ERROR_DIALOG_OK = "OK";
        public const string INFO_DIALOG_TITLE = "情報";
        public const string SAVE_PANEL_TITLE = "画像を保存";
        #endregion

        #region アバター関連
        public const string AVATAR_LABEL = "アバター";
        public const string DETECT_AVATAR_RENDERERS_BUTTON = "全レンダラー自動設定";
        public const string DETECT_CLOTH_RENDERERS_BUTTON = "体・耳・尻尾以外を自動設定";
        public const string ADD_VISIBLE_RENDERERS_BUTTON = "表示中のレンダラーのみ自動設定";
        #endregion

        #region レンダラー関連
        public const string AVATAR_RENDERERS_LABEL = "体・耳・尻尾など";
        public const string CLOTH_RENDERERS_LABEL = "服・髪・飾りなど";
        #endregion

        #region スフィアマスク関連
        public const string SPHERE_SECTION_TITLE = "スフィアマスク";
        public const string ADD_SPHERE_ON_SCENE_BUTTON = "スフィアを追加（Scene上でマスクしたい位置をクリック）";
        public const string SPHERES_SHOW_TOGGLE = "スフィアを表示";
        public const string POSITION_LABEL = "位置";
        public const string RADIUS_LABEL = "半径";
        public const string SPHERE_INTENSITY_LABEL = "濃さ";
        public const string BLUR_LABEL = "ぼかし";
        public const string SPHERE_MIRROR_LABEL = "ミラー";
        #endregion

        #region UVマスク関連
        public const string UV_SECTION_TITLE = "UVマスク";
        public const string ADD_UV_ISLAND_BUTTON = "マスクを追加（Scene上でマスクしたい位置をクリック）";
        public const string UV_NEIGHBOR_RADIUS_LABEL = "UVマスクの範囲";
        public const string UV_SHOW_TOGGLE = "UVマスクを表示";
        #endregion

        #region ボーンマスク関連
        public const string BONE_SECTION_TITLE = "部位マスク";
        #endregion

        #region テクスチャ・マスク生成関連
        public const string TEXTURE_SIZE_LABEL = "テクスチャサイズ";
        public const string DISTANCE_LABEL = "長さ";
        public const string MASK_INTENSITY_LABEL = "マスクの濃さ";
        public const string TRANSPARENT_MODE_LABEL = "透過モード";
        public const string TRANSPARENT_MODE_TOOLTIP = "有効にすると、白い部分を透明に、グレーを黒の半透明で出力します";
        public const string GENERATE_MASK_BUTTON = "マスクを生成";
        public const string MASK_SAVE_BUTTON = "マスクを保存";
        #endregion

        #region 処理関連
        public const string PROGRESS_BAR_TITLE = "距離マスクをベイク中";
        public const string RASTERIZING_LABEL = "Rasterizing...";
        public const string DILATING_LABEL = "Dilating...";
        public const string PROGRESS_RASTERIZING_START_JP = "ラスタライズ開始...";
        #endregion

        #region 汎用UI
        public const string DELETE_BUTTON = "X";
        public const string RESET_BUTTON = "ウィンドウに合わせる";
        public const string TEXTURE_LABEL = "";
        public const string ZOOM_LABEL = "ズーム:";
        public const string SIZE_LABEL = "サイズ:";
        public const string FORMAT_LABEL = "フォーマット";
        public const string TEXTURE_PREVIEW_TITLE_FORMAT = "Texture Preview - {0}";
        public const string TEXTURE_PREVIEW_EMPTY = "テクスチャがありません";
        public const string AUTO_DETECT_BUTTON = "自動設定";
        public const string UV_MASK_PREVIEW_BUTTON = "UVマスクをテクスチャ上で操作";
        public const string UV_MASK_TOGGLE = "UVマスクを表示";
        public const string UV_MASK_ADD_ON_PREVIEW_TOGGLE = "プレビュー上クリックでUVマスク追加";
        public const string UV_WIREFRAME_TOGGLE = "UVワイヤーフレーム";
        public const string ZOOM_FIT_LABEL = "Fit";
        #endregion

        #region ノーマルマップ関連
        public const string NORMAL_MAP_SECTION_TITLE = "ノーマルマップ";
        public const string HAIR_TILT_SECTION_TITLE = "ファー設定";
        public const string HAIR_TILT_LABEL = "ファー設定";
        public const string ADD_HAIR_TILT_BUTTON = "傾き設定を追加";
        public const string AUTO_SET_HAIR_TILT_BUTTON = "自動設定";
        public const string MATERIAL_LABEL = "マテリアル";
        public const string SELECT_MATERIAL_PLACEHOLDER = "マテリアルを選択...";
        #endregion

        #region プレビュー関連
        public const string PREVIEW_SECTION_TITLE = "プレビュー";
        public const string SAVE_MASK_BUTTON = "Save Mask";
        #endregion

        #region 追加モード表示
        public const string ADD_MODE_LABEL_SPHERE = "スフィア追加モード";
        public const string ADD_MODE_LABEL_UV = "UVマスク追加モード";
        #endregion
    }
}