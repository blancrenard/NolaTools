#if UNITY_EDITOR
using NolaTools;

namespace NolaTools.FurMaskGenerator.Constants
{
    /// <summary>
    /// UI表示用のラベルとメッセージを管理
    /// </summary>
    public static class UILabels
    {
        private static string L(string jp, string en) => NolaToolsLocalization.L(jp, en);

        #region 共通ダイアログ
        public static string ERROR_DIALOG_TITLE => L("エラー", "Error");
        public static string ERROR_DIALOG_OK => L("OK", "OK");
        public static string INFO_DIALOG_TITLE => L("情報", "Info");
        public static string SAVE_PANEL_TITLE => L("画像を保存", "Save Image");
        #endregion

        #region アバター関連
        public static string AVATAR_LABEL => L("アバター", "Avatar");
        public static string AVATAR_NOT_SELECTED_HINT => L("アバターオブジェクトを選択してください。", "Please select an avatar object.");
        public static string DETECT_AVATAR_RENDERERS_BUTTON => L("全レンダラー自動設定", "Auto-detect All Renderers");
        public static string DETECT_CLOTH_RENDERERS_BUTTON => L("体・耳・尻尾以外を自動設定", "Auto-detect Cloth Renderers");
        public static string ADD_VISIBLE_RENDERERS_BUTTON => L("表示中のレンダラーのみ自動設定", "Auto-detect Visible Renderers");
        #endregion

        #region レンダラー関連
        public static string AVATAR_RENDERERS_LABEL => L("体・耳・尻尾など", "Body / Ears / Tail etc.");
        public static string CLOTH_RENDERERS_LABEL => L("服・髪・飾りなど", "Clothes / Hair / Accessories etc.");
        #endregion

        #region スフィアマスク関連
        public static string SPHERE_SECTION_TITLE => L("スフィアマスク", "Sphere Mask");
        public static string ADD_SPHERE_ON_SCENE_BUTTON => L("スフィアを追加（Scene上でマスクしたい位置をクリック）", "Add Sphere (Click on the Scene where you want to mask)");
        public static string SPHERES_SHOW_TOGGLE => L("スフィアを表示", "Show Spheres");
        public static string POSITION_LABEL => L("位置", "Position");
        public static string RADIUS_LABEL => L("半径", "Radius");
        public static string SPHERE_INTENSITY_LABEL => L("濃さ", "Intensity");
        public static string BLUR_LABEL => L("ぼかし", "Blur");
        public static string SPHERE_MIRROR_LABEL => L("ミラー", "Mirror");
        #endregion

        #region UVマスク関連
        public static string UV_SECTION_TITLE => L("UVマスク", "UV Mask");
        public static string ADD_UV_ISLAND_BUTTON => L("マスクを追加（Scene上でマスクしたい位置をクリック）", "Add Mask (Click on the Scene where you want to mask)");
        public static string UV_NEIGHBOR_RADIUS_LABEL => L("UVマスクの範囲", "UV Mask Range");
        public static string UV_SHOW_TOGGLE => L("UVマスクを表示", "Show UV Mask");
        #endregion

        #region ボーンマスク関連
        public static string BONE_SECTION_TITLE => L("部位マスク", "Bone Mask");
        #endregion

        #region テクスチャ・マスク生成関連
        public static string TEXTURE_SIZE_LABEL => L("テクスチャサイズ", "Texture Size");
        public static string BLUR_RADIUS_LABEL => L("ぼかし半径", "Blur Radius");
        public static string BLUR_RADIUS_TOOLTIP => L(
            "マスクのぼかし半径（ピクセル単位、ガウシアンブラー）。\n毛の向きのばらつきを吸収します。",
            "Blur radius for the mask (pixels, Gaussian blur).\nAbsorbs variation in fur direction.");
        public static string OUTPUT_MATERIAL_LABEL => L("出力マテリアル", "Output Material");
        public static string OUTPUT_MATERIAL_TOOLTIP => L(
            "指定したマテリアルのみマスクを生成します。\nBody, Ears, Tailなどのリストにあるレンダラーから可能なマテリアルを列挙しています。",
            "Generates a mask only for the specified material.\nLists available materials from renderers in the Body, Ears, Tail etc. lists.");
        public static string DISTANCE_LABEL => L("ファーの長さ", "Fur Length");
        public static string MASK_INTENSITY_LABEL => L("マスクの濃さ", "Mask Intensity");
        public static string SUBDIVISION_ITERATIONS_LABEL => L("細分化回数", "Subdivision Iterations");
        public static string SUBDIVISION_ITERATIONS_TOOLTIP => L(
            "メッシュの細分化回数（0=細分化なし、1=4倍、2=16倍、3=64倍）",
            "Number of mesh subdivisions (0=none, 1=4x, 2=16x, 3=64x)");
        public static string EDGE_PADDING_LABEL => L("エッジパディング", "Edge Padding");
        public static string EDGE_PADDING_TOOLTIP => L(
            "テクスチャのエッジ部分にパディングを適用します（ピクセル単位）",
            "Applies padding to the edge of the texture (in pixels)");
        public static string TRANSPARENT_MODE_LABEL => L("透過モード", "Transparent Mode");
        public static string TRANSPARENT_MODE_TOOLTIP => L(
            "有効にすると、白い部分を透明に、グレーを黒の半透明で出力します",
            "When enabled, white areas become transparent and grey areas become semi-transparent black.");
        public static string GENERATE_MASK_BUTTON => L("マスクを生成", "Generate Mask");
        public static string MASK_SAVE_BUTTON => L("マスクを保存", "Save Mask");
        #endregion

        #region ベイクモード関連
        public static string BAKE_MODE_LABEL => L("ベイクモード", "Bake Mode");
        public static string BAKE_MODE_VERTEX => L("頂点ベース（従来方式）", "Vertex-based (Legacy)");
        public static string BAKE_MODE_TEXEL => L("テクセルベース（高精度）", "Texel-based (High Precision)");
        public static string[] BAKE_MODE_LABELS => new[] { BAKE_MODE_VERTEX, BAKE_MODE_TEXEL };
        #endregion

        #region 処理関連
        public static string PROGRESS_BAR_TITLE => L("距離マスクをベイク中", "Baking Distance Mask");
        public static string PROGRESS_BAR_TITLE_TEXEL => L("テクセルマスクをベイク中", "Baking Texel Mask");
        public const string RASTERIZING_LABEL = "Rasterizing...";
        public const string DILATING_LABEL = "Dilating...";
        public static string PROGRESS_RASTERIZING_START_JP => L("ラスタライズ開始...", "Starting rasterization...");
        #endregion

        #region 汎用UI
        public const string DELETE_BUTTON = "X";
        public static string RESET_BUTTON => L("ウィンドウに合わせる", "Fit to Window");
        public const string TEXTURE_LABEL = "";
        public static string ZOOM_LABEL => L("ズーム:", "Zoom:");
        public static string SIZE_LABEL => L("サイズ:", "Size:");
        public static string FORMAT_LABEL => L("フォーマット", "Format");
        public const string TEXTURE_PREVIEW_TITLE_FORMAT = "Texture Preview - {0}";
        public static string TEXTURE_PREVIEW_EMPTY => L("テクスチャがありません", "No texture");
        public static string AUTO_DETECT_BUTTON => L("自動設定", "Auto-detect");
        public static string UV_MASK_PREVIEW_BUTTON => L("UVマスクをテクスチャ上で操作", "Edit UV Mask on Texture");
        public static string UV_MASK_TOGGLE => L("UVマスクを表示", "Show UV Mask");
        public static string UV_MASK_ADD_ON_PREVIEW_TOGGLE => L("プレビュー上クリックでUVマスク追加", "Click on Preview to Add UV Mask");
        public static string UV_WIREFRAME_TOGGLE => L("UVワイヤーフレーム", "UV Wireframe");
        public const string ZOOM_FIT_LABEL = "Fit";
        #endregion

        #region ノーマルマップ関連
        public static string NORMAL_MAP_SECTION_TITLE => L("ノーマルマップ", "Normal Map");
        public static string HAIR_TILT_SECTION_TITLE => L("ファー設定", "Fur Settings");
        public static string HAIR_TILT_LABEL => L("ファー設定", "Fur Settings");
        public static string ADD_HAIR_TILT_BUTTON => L("傾き設定を追加", "Add Tilt Setting");
        public static string AUTO_SET_HAIR_TILT_BUTTON => L("自動設定", "Auto-detect");
        public static string MATERIAL_LABEL => L("マテリアル", "Material");
        public static string SELECT_MATERIAL_PLACEHOLDER => L("マテリアルを選択...", "Select material...");
        #endregion

        #region プレビュー関連
        public static string PREVIEW_SECTION_TITLE => L("プレビュー", "Preview");
        public const string SAVE_MASK_BUTTON = "Save Mask";
        public static string LENGTH_MASK_LABEL => L("長さマスク", "Length Mask");
        public static string ALPHA_MASK_LABEL => L("アルファマスク", "Alpha Mask");
        #endregion

        #region 追加モード表示
        public static string ADD_MODE_LABEL_SPHERE => L("スフィア追加モード", "Sphere Add Mode");
        public static string ADD_MODE_LABEL_UV => L("UVマスク追加モード", "UV Mask Add Mode");
        #endregion

        #region デバッグ表示
        public static string DEBUG_POLYGON_COUNT_BEFORE => L("細分化前ポリゴン数:", "Polygon count (before):");
        public static string DEBUG_POLYGON_COUNT_AFTER => L("細分化後ポリゴン数:", "Polygon count (after):");
        public static string DEBUG_POLYGON_COUNT_RATIO => L("細分化倍率:", "Subdivision ratio:");
        #endregion
    }
}
#endif
