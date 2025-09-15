#if UNITY_EDITOR
using UnityEngine;

namespace Mask.Generator.Constants
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
        #endregion
    }
}
#endif