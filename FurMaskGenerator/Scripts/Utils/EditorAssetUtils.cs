#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mask.Generator.Constants;

namespace Mask.Generator.Utils
{
    /// <summary>
    /// Extended asset utilities for Unity Editor operations
    /// Contains asset management, path operations, file operations, and related utilities
    /// </summary>
    public static class EditorAssetUtils
    {
        private static bool _saveScheduled;
        private const double SAVE_DEBOUNCE_SECONDS = 0.25;
        private static double _lastSaveTime;

        #region Asset Management Methods

        /// <summary>
        /// 指定パスにある ScriptableObject アセットを読み込み、存在しなければ作成して返す
        /// </summary>
        public static T LoadOrCreateAssetAtPath<T>(string assetPath) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null) return asset;

            string directory = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            ScheduleSaveAssets();
            return asset;
        }

        /// <summary>
        /// 文字列から8文字のハッシュを生成します
        /// </summary>
        public static string ToHash8(string input)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input ?? ""));
                var sb = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString(UIConstants.HEX_FORMAT_UPPER));
                return (sb.Length >= 8) ? sb.ToString(0, 8) : sb.ToString();
            }
        }

        /// <summary>
        /// アバター固有のアセットパスを計算します
        /// </summary>
        public static string ComputeAvatarScopedAssetPath(string avatarSettingsRoot, string filePrefix, string avatarPath)
        {
            string hash8 = ToHash8(avatarPath);
            return $"{avatarSettingsRoot}{UIConstants.PATH_SEPARATOR}{filePrefix}_{hash8}{UIConstants.FILE_EXTENSION_ASSET}";
        }

        /// <summary>
        /// アバター設定を読み込みます
        /// </summary>
        public static bool TryLoadAvatarSettings<T>(string avatarSettingsRoot, string filePrefix, GameObject avatar, out T asset) where T : ScriptableObject
        {
            asset = null;
            if (avatar == null) return false;
            
            string avatarPath = EditorPathUtils.GetGameObjectPath(avatar);
            string settingsPath = ComputeAvatarScopedAssetPath(avatarSettingsRoot, filePrefix, avatarPath);
            
            asset = AssetDatabase.LoadAssetAtPath<T>(settingsPath);
            return asset != null;
        }

        /// <summary>
        /// アバター設定を作成します
        /// </summary>
        public static T CreateAvatarSettings<T>(string avatarSettingsRoot, string filePrefix, GameObject avatar) where T : ScriptableObject
        {
            string avatarPath = EditorPathUtils.GetGameObjectPath(avatar);
            string settingsPath = ComputeAvatarScopedAssetPath(avatarSettingsRoot, filePrefix, avatarPath);
            var created = LoadOrCreateAssetAtPath<T>(settingsPath);
            EditorUIUtils.SetDirtyAndScheduleSaveOnly(created);
            return created;
        }

        /// <summary>
        /// 変更されたオブジェクトがある場合のみアセットを保存します
        /// </summary>
        public static void SaveIfDirty(params UnityEngine.Object[] targets)
        {
            bool anyDirty = false;
            if (targets != null)
            {
                foreach (var t in targets)
                {
                    if (t != null && EditorUtility.IsDirty(t)) { anyDirty = true; break; }
                }
            }
            if (anyDirty) ScheduleSaveAssets();
        }

        /// <summary>
        /// SaveAssetsを一定時間内でまとめて実行（遅延一括保存）
        /// </summary>
        public static void ScheduleSaveAssets()
        {
            if (_saveScheduled) return;
            // 短時間の連続保存を抑制（マウスの砂時計防止）
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastSaveTime < 1.0) return;
            _saveScheduled = true;
            EditorApplication.delayCall += () =>
            {
                try
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    _lastSaveTime = EditorApplication.timeSinceStartup;
                }
                finally
                {
                    _saveScheduled = false;
                }
            };
        }

        #endregion

        #region Path Operations Methods

        /// <summary>
        /// パスリストからコンポーネントリストを復元します
        /// </summary>
        public static void RestoreComponentList<T>(System.Collections.Generic.List<T> targetList, System.Collections.Generic.List<string> pathList) where T : Component
        {
            targetList.Clear();
            if (pathList == null) return;
            foreach (var path in pathList)
            {
                if (string.IsNullOrEmpty(path)) { targetList.Add(null); continue; }
                var go = EditorPathUtils.FindGameObjectByPath(path);
                targetList.Add(go != null ? go.GetComponent<T>() : null);
            }
        }

        /// <summary>
        /// コンポーネント列からGameObjectパスの一覧を得ます
        /// </summary>
        public static System.Collections.Generic.List<string> GetComponentPaths<T>(System.Collections.Generic.IEnumerable<T> components) where T : Component
        {
            var result = new System.Collections.Generic.List<string>();
            if (components == null) return result;
            foreach (var c in components)
            {
                result.Add(EditorPathUtils.GetGameObjectPath(c));
            }
            return result;
        }

        #endregion

        #region File Operations Methods

        /// <summary>
        /// ファイル名に使用できない文字をアンダースコアに置換して安全なファイル名を返します
        /// </summary>
        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return UIConstants.DEFAULT_FILE_NAME;
            char[] invalid = Path.GetInvalidFileNameChars();
            char[] result = new char[fileName.Length];
            for (int i = 0; i < fileName.Length; i++)
            {
                char c = fileName[i];
                result[i] = System.Array.IndexOf(invalid, c) >= 0 ? '_' : c;
            }
            return new string(result);
        }

        #endregion
    }

    #region Asset Path Constants

    /// <summary>
    /// アセットパス関連の定数
    /// </summary>
    public static class EditorAssetPaths
    {
        public static class FurMaskGenerator
        {
            public const string AvatarSettingsRoot = UIConstants.AVATAR_SETTINGS_ROOT;
        }

        public static class FurDirectionGenerator
        {
            public const string AvatarSettingsRoot = UIConstants.FUR_DIRECTION_GENERATOR_AVATAR_SETTINGS_ROOT;
        }
    }

    #endregion
}
#endif




