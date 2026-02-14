#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator.Utils
{
    /// <summary>
    /// 色生成専用のユーティリティクラス
    /// </summary>
    public static class ColorGenerator
    {
        #region 色生成設定

        // ゴールデンアングル分散システム
        private const float GOLDEN_ANGLE = 0.618033988749895f;
        private const string PREFS_HUE_INDEX_KEY = "NolaTools.FurMaskGenerator.Utils.ColorGenerator.HueIndex";
        private const string PREFS_HUE_OFFSET_KEY = "NolaTools.FurMaskGenerator.Utils.ColorGenerator.HueOffset";
        private static int _hueIndex = -1;
        private static float _hueOffset = -1f;

        // 高彩度色生成用の定数
        private const float HIGH_SATURATION = AppSettings.DEFAULT_SCALE;
        private const float HIGH_VALUE_MIN = AppSettings.HIGH_VALUE_MIN;
        private const float HIGH_VALUE_MAX = AppSettings.DEFAULT_SCALE;

        #endregion

        #region 色生成メソッド

        /// <summary>
        /// ゴールデンアングル高彩度マーカー色を生成
        /// </summary>
        public static Color GenerateMarkerColor()
        {
            float hue = GetNextHue();
            DeriveSVFromHue(hue, out float saturation, out float value);
            return Color.HSVToRGB(hue, saturation, value);
        }

        #endregion

        #region プライベートヘルパーメソッド

        private static void EnsureHueState()
        {
            if (_hueIndex < 0)
            {
                _hueIndex = EditorPrefs.GetInt(PREFS_HUE_INDEX_KEY, 0);
            }
            if (_hueOffset < 0f)
            {
                _hueOffset = EditorPrefs.GetFloat(PREFS_HUE_OFFSET_KEY, 0f);
                if (_hueOffset == 0f)
                {
                    _hueOffset = Mathf.Repeat((float)System.DateTime.Now.Ticks * 0.00000001f, 1f);
                    EditorPrefs.SetFloat(PREFS_HUE_OFFSET_KEY, _hueOffset);
                }
            }
        }

        private static float GetNextHue()
        {
            EnsureHueState();
            float hue = Mathf.Repeat(_hueOffset + _hueIndex * GOLDEN_ANGLE, 1f);
            _hueIndex++;
            EditorPrefs.SetInt(PREFS_HUE_INDEX_KEY, _hueIndex);
            return hue;
        }

        private static void DeriveSVFromHue(float hue, out float saturation, out float value)
        {
            // 彩度は100%固定
            saturation = HIGH_SATURATION;
            
            // 明度は70-100%の範囲で変化
            float valueVariation = GeneratePseudoRandom(hue, 23.1459f, 45.678f);
            value = Mathf.Lerp(HIGH_VALUE_MIN, HIGH_VALUE_MAX, valueVariation);
        }

        /// <summary>
        /// 決定論的な擬似乱数を生成
        /// </summary>
        private static float GeneratePseudoRandom(float seed, float mult1, float mult2)
        {
            float x = Mathf.Repeat(seed * mult1 + mult2, 1f);
            return Mathf.Repeat(x * 43758.5453f, 1f);
        }

        #endregion
    }
}
#endif