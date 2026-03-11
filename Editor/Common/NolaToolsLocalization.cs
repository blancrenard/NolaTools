#if UNITY_EDITOR
using System;
using UnityEditor;

namespace NolaTools
{
    /// <summary>
    /// NolaTools 共通ローカライゼーション管理クラス
    /// </summary>
    public static class NolaToolsLocalization
    {
        public enum Language { Japanese, English }

        private const string PREFS_KEY = "NolaTools.Language";

        public static Language Current
        {
            get => (Language)EditorPrefs.GetInt(PREFS_KEY, (int)Language.Japanese);
            set
            {
                EditorPrefs.SetInt(PREFS_KEY, (int)value);
                OnLanguageChanged?.Invoke();
            }
        }

        public static bool IsJapanese => Current == Language.Japanese;

        /// <summary>
        /// 言語変更時に発火するイベント
        /// </summary>
        public static event Action OnLanguageChanged;

        /// <summary>
        /// 現在の言語設定に応じた文字列を返す
        /// </summary>
        public static string L(string jp, string en) => IsJapanese ? jp : en;
    }
}
#endif
