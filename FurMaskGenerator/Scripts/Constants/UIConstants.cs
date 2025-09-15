using UnityEngine;

namespace Mask.Generator.Constants
{
    /// <summary>
    /// 統合されたUI定数管理クラス
    /// 以前のUITextとUIConstantsを統合し、一括管理を実現
    /// </summary>
    public static class UIConstants
    {
        // ================================================================================
        // === UIテキスト定数 ===
        // ================================================================================

        #region 共通ダイアログ
        public const string ERROR_DIALOG_TITLE = "エラー";
        public const string ERROR_DIALOG_OK = "OK";
        public const string INFO_DIALOG_TITLE = "情報";
        public const string SAVE_PANEL_TITLE = "画像を保存";
        #endregion

        #region アバター関連
        public const string AVATAR_LABEL = "アバター";
        public const string ERROR_AVATAR_NOT_SET = "アバターが設定されていません。";
        public const string ERROR_HUMANOID_REQUIRED = "Humanoidアバターが見つかりませんでした。アバターにAnimator(Humanoid)を設定してください。";
        public const string ERROR_INVALID_HUMANOID = "選択されたオブジェクトは有効な Humanoid アバターではありません。";
        public const string DETECT_AVATAR_RENDERERS_BUTTON = "全レンダラー自動設定";
        public const string DETECT_CLOTH_RENDERERS_BUTTON = "体・耳・尻尾以外を自動設定";
        public const string ADD_VISIBLE_RENDERERS_BUTTON = "表示中のレンダラーのみ自動設定";
        #endregion

        #region レンダラー関連
        public const string AVATAR_RENDERERS_LABEL = "体・耳・尻尾など";
        public const string CLOTH_RENDERERS_LABEL = "服・髪・飾りなど";
        public const string ERROR_AVATAR_RENDERERS_INVALID = "アバターのレンダラーが設定されていません、無効です。";
        public const string ERROR_CLOTH_RENDERERS_INVALID = "服のレンダラーが設定されていません、無効です。";
        public const string ERROR_MESH_NOT_FOUND = "レンダラーからメッシュを取得できませんでした:";
        public const string ERROR_RENDERER_NOT_IN_LIST = "クリック対象のレンダラーが設定リストに含まれていません。";
        public const string ERROR_SKINNED_MESH_NOT_FOUND = "SkinnedMeshRenderer が見つかりません。";
        public const string ERROR_NO_VALID_RENDERERS = "有効なSkinnedMeshRendererが見つかりません。自動設定を実行してください。";
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
        public const string ERROR_UV_NOT_FOUND = "メッシュに UV がありません。";
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
        
        // ボタンラベル
        public const string AUTO_DETECT_BUTTON = "自動設定";
        public const string UV_MASK_PREVIEW_BUTTON = "UVマスクをテクスチャ上で操作";
        public const string UV_MASK_TOGGLE = "UVマスクを表示";
        public const string UV_MASK_ADD_ON_PREVIEW_TOGGLE = "プレビュー上クリックでUVマスク追加";
        public const string UV_WIREFRAME_TOGGLE = "UVワイヤーフレーム";
        public const string ZOOM_FIT_LABEL = "Fit";
        #endregion

        #region 特殊なエラー
        public const string ERROR_SETTINGS_NOT_ASSIGNED = "設定ファイルが割り当てられていません。";
        public const string ERROR_SETTINGS_LOAD_FAILED = "設定ファイルの読み込みに失敗しました。ウィンドウを再起動してください。";
        public const string ERROR_NO_PREVIEW = "プレビューがありません。";
        public const string ERROR_TEXTURE_NULL = "テクスチャがnullです。";
        public const string ERROR_RAYCAST_HIT_NOT_FOUND = "クリック位置から有効なメッシュ三角形を取得できませんでした。";
        
        // 自動検出関連
        public const string ERROR_RENDERERS_NOT_SET = "レンダラーが設定されていません。";
        public const string ERROR_UV_MASKS_NOT_SET = "UVマスクが設定されていません。";
        public const string ERROR_VALID_UV_MASKS_NOT_FOUND = "有効なUVマスクが見つかりませんでした。";
        
        // テクスチャ自動検出
        public const string TEXTURE_AUTO_DETECT_TITLE = "テクスチャサイズ自動検出";
        public const string TEXTURE_AUTO_DETECT_NO_TEXTURE = "有効なテクスチャが見つかりませんでした。\n手動でサイズを設定してください。";
        public const string TEXTURE_AUTO_DETECT_NO_SIZE = "適切なテクスチャサイズが見つかりませんでした。";
        
        // テクスチャプロパティ名
        public static readonly string[] MAIN_TEXTURE_PROPERTIES = { "_MainTex", "_BaseMap", "_AlbedoMap", "_DiffuseMap" };
        
        // エラーメッセージ
        public const string ERROR_GUI_DRAW = "GUI描画中にエラーが発生しました: {0}";
        public const string ERROR_TEXTURE_NOT_READABLE = "テクスチャ '{0}' は読み取り可能になっていません。\nTexture Import Settings で 'Read/Write Enabled' を有効にしてください。";
        public const string ERROR_SPHERE_FOLDOUT_INDEX = "sphereFoldoutStates index out of range: {0} >= {1}";
        public const string ERROR_SPHERE_MASK_SETTINGS = "DrawSphereMaskSettingsでエラーが発生しました: {0}";
        public const string ERROR_UV_MASK_OVERLAY_GENERATION = "UVマスクオーバーレイテクスチャの生成中にエラーが発生しました: {0}";
        public const string ERROR_UV_ISLAND_ACQUISITION = "UVアイランド取得中にエラーが発生しました: {0}";
        #endregion

        #region 情報メッセージ
        public const string INFO_MOUTH_BLEND_NOT_FOUND = "口系ブレンドシェイプが検出できなかったため、口元スフィアの自動追加をスキップしました。";
        #endregion

        #region Undo/Redo メッセージ
        public const string UNDO_FUR_MASK_GENERATOR_CHANGE = "Fur Mask Generator Change";
        public const string UNDO_MOVE_SPHERE_MASK = "Move Sphere Mask";
        public const string UNDO_ADD_SPHERE_MASK = "Add Sphere Mask";
        public const string UNDO_ADD_UV_ISLAND_MASK = "Add UV Island Mask";
        public const string UNDO_AUTO_DETECT_TEXTURE_SIZE = "Auto-detect texture size";
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

        #region 内部定数
        public const string TEMP_RAYCAST_COLLIDER_NAME = "FMG_TempRaycastCollider";
        public const string SPHERE_NAME_PREFIX = "Sphere ";
        public const string LAST_SAVE_DIRECTORY_KEY = "Mask.Generator.LastSaveDirectory";
        public const string LAST_AVATAR_PATH_KEY = "Mask.Generator.LastAvatarPath";
        public const string FUR_LENGTH_MASK_SUFFIX = "_FurLenMask";
        public const string SETTINGS_ASSET_FOLDER = "Assets/NolaTools/FurMaskGenerator";
        public const string SETTINGS_ASSET_FILE = "FurMaskSettings";
        public const string SETTINGS_ASSET_PATH = SETTINGS_ASSET_FOLDER + PATH_SEPARATOR + SETTINGS_ASSET_FILE + FILE_EXTENSION_ASSET;
        public const string MENU_ITEM_PATH = "Tools/NolaTools/FurMaskGenerator";
        public const string WINDOW_TITLE = "FurMaskGenerator";
        #endregion

        #region グループ順序
        public static readonly string[] GROUP_ORDER = { "Body", "Head", "Arm", "Hand", "Leg", "Foot", "Ear", "Tail" };
        #endregion

        #region デバッグログ
        public const string LOG_HIERARCHY_CHANGE_IGNORED = "[FurMaskGenerator] OnHierarchyChange ignored during bake (ClothCollider creation)";
        public const string LOG_HIERARCHY_CHANGE_CALLED = "[FurMaskGenerator] OnHierarchyChange called - AvatarRenderers before: {0}, ClothRenderers before: {1}";
        public const string LOG_HIERARCHY_CHANGE_COMPLETED = "[FurMaskGenerator] OnHierarchyChange completed - AvatarRenderers after: {0}, ClothRenderers after: {1}";
        #endregion

        #region ベイク処理関連
        public const string CLOTH_COLLIDER_OBJECT_NAME = "ClothCollider";
        public const string SUBMESH_NAME_PREFIX = "SubMesh_";
        public const string LENGTH_MASK_PREFIX = "LengthMask_";
        public const string ERROR_BAKE_PROCESS = "Bake処理中にエラーが発生しました: {0}";
        public const string ERROR_COMPLETE_PROCESS = "完了処理中にエラーが発生しました: {0}";
        public const string ERROR_BAKE_EXCEPTION = "ベイク処理中にエラーが発生しました: {0}";
        public const string WARNING_NORMAL_MAP_NOT_READABLE = "ノーマルマップ '{0}' が読み取り可能になっていません。Texture Import Settings で 'Read/Write Enabled' を有効にしてください。";
        #endregion

        #region データ関連
        public const string ASSET_MENU_NAME = "Fur Mask Generator/Fur Mask Settings";
        public const string DEFAULT_SPHERE_NAME = "New Sphere";
        public const string SPHERE_POSITION_PREFIX = "Sphere_";
        #endregion

        #region ユーティリティ関連
        public const string GUI_STYLE_BOX = "box";
        public const string FILE_EXTENSION_PNG = "png";
        public const string FILE_EXTENSION_ASSET = ".asset";
        public const string DEFAULT_FILE_NAME = "Untitled";
        public const string DEFAULT_OK_LABEL = "OK";
        public const string PATH_SEPARATOR = "/";
        public const char PATH_SEPARATOR_CHAR = '/';
        public const string HEX_FORMAT_UPPER = "X2";
        public const string AVATAR_SETTINGS_ROOT = "Assets/NolaTools/FurMaskGenerator/AvatarSettings";
        public const string FUR_DIRECTION_GENERATOR_AVATAR_SETTINGS_ROOT = "Assets/NolaTools/FurDirectionGenerator/AvatarSettings";
        #endregion

        #region フィルター関連
        public const string FILTER_WEAR = "wear";
        public const string FILTER_EARRING = "earring";
        public const string FILTER_BODY = "body";
        public const string FILTER_TAIL = "tail";
        public const string FILTER_EAR = "ear";
        #endregion

        #region ボーン関連
        public const string BONE_GROUP_HIPS = "hips";
        public const string BONE_GROUP_SPINE = "spine";
        public const string BONE_GROUP_CHEST = "chest";
        public const string BONE_GROUP_UPPERCHEST = "upperchest";
        public const string BONE_GROUP_BODY = "Body";
        public const string BONE_GROUP_NECK = "neck";
        public const string BONE_GROUP_HEAD = "head";
        public const string BONE_GROUP_JAW = "jaw";
        public const string BONE_GROUP_LEFTEYE = "lefteye";
        public const string BONE_GROUP_RIGHTEYE = "righteye";
        public const string BONE_GROUP_EYE = "eye";
        public const string BONE_GROUP_HEAD_GROUP = "Head";
        public const string BONE_GROUP_UPPERARM = "upperarm";
        public const string BONE_GROUP_LOWERARM = "lowerarm";
        public const string BONE_GROUP_SHOULDER = "shoulder";
        public const string BONE_GROUP_ARM = "Arm";
        public const string BONE_GROUP_HAND = "hand";
        public const string BONE_GROUP_INDEX = "index";
        public const string BONE_GROUP_MIDDLE = "middle";
        public const string BONE_GROUP_RING = "ring";
        public const string BONE_GROUP_LITTLE = "little";
        public const string BONE_GROUP_THUMB = "thumb";
        public const string BONE_GROUP_HAND_GROUP = "Hand";
        public const string BONE_GROUP_UPPERLEG = "upperleg";
        public const string BONE_GROUP_LOWERLEG = "lowerleg";
        public const string BONE_GROUP_LEG = "Leg";
        public const string BONE_GROUP_FOOT = "foot";
        public const string BONE_GROUP_TOES = "toes";
        public const string BONE_GROUP_FOOT_GROUP = "Foot";
        public const string BONE_GROUP_EAR_GROUP = "Ear";
        public const string BONE_GROUP_TAIL_GROUP = "Tail";
        #endregion

        #region 顔認識関連
        public const string SPHERE_NAME_LEFT_EYE = "Left Eye";
        public const string SPHERE_NAME_RIGHT_EYE = "Right Eye";
        public const string SPHERE_NAME_NOSE_TIP = "Nose Tip";
        public const string SPHERE_NAME_MOUTH_INSIDE = "Mouth Inside";
        #endregion

        #region ブレンドシェイプ関連
        public const string BLENDSHAPE_V_AA = "vrc.v_aa";
        public const string BLENDSHAPE_V_OH = "vrc.v_oh";
        public const string BLENDSHAPE_V_OU = "vrc.v_ou";
        public const string BLENDSHAPE_V_IH = "vrc.v_ih";
        public const string BLENDSHAPE_V_EE = "vrc.v_ee";
        #endregion

        #region デバッグログ（Features）
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

        // ================================================================================
        // === UI設定定数 ===
        // ================================================================================

        #region 色設定
        public static readonly Color BOX_BACKGROUND = new Color(0.95f, 0.95f, 0.95f, 1f);
        public static readonly Color SPHERE_ITEM_BACKGROUND = new Color(0.85f, 0.85f, 0.85f, 1f);
        #endregion

        #region レイアウト設定
        public const float LARGE_SPACE = 10f;
        public const float FOLDOUT_WIDTH = 12f;
        public const float DELETE_BUTTON_WIDTH = 20f;
        #endregion

        #region 処理設定
        public const int MAX_BATCH_SIZE = 1000;
        public const float MIN_DISTANCE = 0f;
        public const float MAX_DISTANCE = 0.1f; // 0.01スケールで最大0.1
        public const int PROGRESS_UPDATE_INTERVAL = 50;
        #endregion

        #region スフィア設定
        public const float POSITION_PRECISION = 0.001f;
        public const float DEFAULT_RADIUS = 0.01f;
        public const float SHOW_MAX_RADIUS = 0.5f;
        #endregion

        #region 範囲設定
        public const float GRADIENT_DEFAULT = 0.5f;
        public const float SPHERE_INTENSITY_MIN = 0.1f;
        public const float SPHERE_INTENSITY_MAX = 1.0f;
        public const float SPHERE_INTENSITY_DEFAULT = 1.0f;
        #endregion

        #region UI設定
        public static readonly string[] TEXTURE_SIZE_LABELS = { "512x512", "1024x1024", "2048x2048", "4096x4096" };
        public static readonly int[] TEXTURE_SIZES = { 512, 1024, 2048, 4096 };
        public const float MIN_GAMMA = 0.1f;
        public const float MAX_GAMMA = 5.0f;
        public const float VALID_PIXEL_THRESHOLD = 1e-5f;
        #endregion

        #region シーンフレーム（追加モード表示）
        public static readonly Color ADD_MODE_FRAME_COLOR = new Color(1f, 0.9f, 0f, 0.9f); // 濃い黄色
        public const float ADD_MODE_FRAME_THICKNESS = 4f;
        public const float ADD_MODE_BOTTOM_UI_OFFSET = 26f; // SceneView 下部UI分のオフセット
        public const float ADD_MODE_LABEL_MARGIN = 8f;
        public const float ADD_MODE_LABEL_PADDING = 6f;
        public static readonly Color ADD_MODE_LABEL_BG = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color ADD_MODE_LABEL_TEXT_COLOR = new Color(1f, 0.95f, 0.2f, 1f);
        public const string ADD_MODE_LABEL_SPHERE = "スフィア追加モード";
        public const string ADD_MODE_LABEL_UV = "UVマスク追加モード";
        #endregion

        #region ユーティリティアクション
        public static readonly System.Action EmptyAction = () => { };
        #endregion
    }
}
