#if UNITY_EDITOR
using UnityEngine;

namespace NolaTools.FurMaskGenerator.Constants
{
    /// <summary>
    /// アプリケーション設定値と定数を管理
    /// </summary>
    public static class AppSettings
    {
        #region 色設定
        public static readonly Color BOX_BACKGROUND = new Color(0.95f, 0.95f, 0.95f, 1f);
        public static readonly Color SPHERE_ITEM_BACKGROUND = new Color(0.85f, 0.85f, 0.85f, 1f);
        public static readonly Color ADD_MODE_FRAME_COLOR = new Color(1f, 0.9f, 0f, 0.9f);
        public static readonly Color ADD_MODE_LABEL_BG = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color ADD_MODE_LABEL_TEXT_COLOR = new Color(1f, 0.95f, 0.2f, 1f);
        #endregion

        #region レイアウト設定
        public const float LARGE_SPACE = 10f;
        public const float FOLDOUT_WIDTH = 12f;
        public const float DELETE_BUTTON_WIDTH = 20f;
        public const float ADD_MODE_FRAME_THICKNESS = 4f;
        public const float ADD_MODE_BOTTOM_UI_OFFSET = 26f;
        public const float ADD_MODE_LABEL_MARGIN = 8f;
        public const float ADD_MODE_LABEL_PADDING = 6f;
        #endregion

        #region 処理設定
        public const int MAX_BATCH_SIZE = 1000;
        public const float MIN_DISTANCE = 0f;
        public const float MAX_DISTANCE = 0.1f;
        public const int PROGRESS_UPDATE_INTERVAL = 50;
        public const int MAX_POLYGON_COUNT = 1000000; // 最大ポリゴン数制限（100万）
        #endregion

        #region スフィア設定
        public const float POSITION_PRECISION = 0.001f;
        public const float DEFAULT_RADIUS = 0.01f;
        public const float SHOW_MAX_RADIUS = 0.5f;
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

        #region デフォルト値
        public const float DEFAULT_MAX_DISTANCE = 0.04f;
        public const float DEFAULT_GAMMA = 2f;
        public const int DEFAULT_TEXTURE_SIZE_INDEX = 1;
        public const float DEFAULT_UV_ISLAND_NEIGHBOR_RADIUS = 0.015f;
        public const int DEFAULT_EDGE_PADDING_SIZE = 4;
        #endregion

        #region 計算・処理定数
        public const float DEFAULT_INTENSITY = 1.0f;
        public const float DEFAULT_SCALE = 1.0f;
        public const float HALF_VALUE = 0.5f;
        public const float TWENTY_PERCENT = 0.2f;
        public const float SIXTY_PERCENT = 0.6f;
        public const float EIGHTY_PERCENT = 0.8f;
        public const float NINETY_FIVE_PERCENT = 0.95f;
        public const float MEMORY_PRESSURE_THRESHOLD = 4.0f;
        public const float COMPLEXITY_HIGH_THRESHOLD = 1.8f;
        public const float COMPLEXITY_MEDIUM_THRESHOLD = 0.8f;
        public const float UV_THRESHOLD_DEFAULT = 0.1f;
        public const float ZOOM_FACTOR_PER_NOTCH = 1.1f;
        public const float CROSS_SIZE_MULTIPLIER = 0.02f;
        public const float SPHERE_GLOW_MULTIPLIER = 1.02f;
        public const float SPHERE_DIM_MULTIPLIER = 0.98f;
        public const float MARKER_SIZE_MULTIPLIER = 1.25f;
        public const float MARKER_SIZE_MULTIPLIER_LARGE = 1.6f;
        public const float CONVERGENCE_THRESHOLD = 0.0001f;
        public const float RAY_OFFSET_MULTIPLIER = 1.0f;

        #endregion

        #region 色生成定数
        public const float HIGH_VALUE_MIN = 0.7f;
        public const float WIRE_ALPHA = 0.9f;
        public const float GRADIENT_ALPHA = 0.35f;
        public const float GRADIENT_ALPHA_MIN = 0.15f;
        public const float MIRROR_INNER_ALPHA_MIN = 0.25f;
        #endregion

        #region UIプレビュー定数
        public const float MIN_ZOOM = 0.1f;
        public const float MAX_ZOOM = 5.0f;
        public const float FIT_EPSILON = 1e-3f;
        public const float MIN_WINDOW_WIDTH = 400f;
        public const float MIN_WINDOW_HEIGHT = 300f;
        public const float WINDOW_CENTER_OFFSET_X = 0.5f;
        public const float WINDOW_CENTER_OFFSET_Y = 0.5f;
        public const float SCROLLBAR_WIDTH = 20f;
        public const float TOOLBAR_HEIGHT = 22f;
        public const float INFO_HEIGHT = 18f;
        public const float TOTAL_UI_OFFSET = 80f;
        public const float MARKER_SIZE_MIN = 5f;
        public const float MARKER_SIZE_MAX = 12f;
        public const float MARKER_SIZE_MULTIPLIER_MIN = 0.012f;
        public const float CROSS_DRAW_SIZE = 0.006f;
        public const float SCROLL_WHEEL_SPEED = 40f;
        public const float CANVAS_SCROLLBAR_SIZE = 14f;
        public const float CANVAS_SCROLLBAR_OVERLAP_TOLERANCE = 0.5f;
        public static readonly Color WIREFRAME_COLOR = new Color(1f, 1f, 1f, 0.6f);
        #endregion
    }
}
#endif