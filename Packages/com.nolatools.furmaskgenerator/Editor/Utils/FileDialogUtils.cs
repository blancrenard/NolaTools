#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator.Utils
{
    /// <summary>
    /// ファイルダイアログ操作専用のユーティリティクラス
    /// </summary>
    public static class FileDialogUtils
    {
        #region File Save Operations

        /// <summary>
        /// SaveFilePanel を表示してテクスチャをPNG で保存する。
        /// 初期フォルダは前回保存先（EditorPrefs）を優先し、未設定時はOSのピクチャ→デスクトップ→プロジェクトルート。
        /// </summary>
        public static void SaveTexturePNG(Texture2D texture, string defaultFileName, string panelTitle)
        {
            if (texture == null)
            {
                EditorUtility.DisplayDialog(UILabels.ERROR_DIALOG_TITLE, ErrorMessages.ERROR_TEXTURE_NULL, UILabels.ERROR_DIALOG_OK);
                return;
            }

            string safeName = EditorAssetUtils.SanitizeFileName(defaultFileName);
            // 既定名の末尾に _FurLenMask を付与（重複付与は回避）
            safeName = EnsureSuffixWithoutExtension(safeName, FileConstants.FUR_LENGTH_MASK_SUFFIX);
            string initialDirectory = GetInitialSaveDirectory();
            string path = EditorUtility.SaveFilePanel(panelTitle, initialDirectory, safeName, FileConstants.FILE_EXTENSION_PNG);
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
                try
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        EditorPrefs.SetString(FileConstants.LAST_SAVE_DIRECTORY_KEY, dir);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[FurMaskGenerator] 保存先ディレクトリの記録に失敗: {ex.Message}");
                }
                AssetDatabase.Refresh();
            }
        }

        #endregion

        #region Private Helper Methods

        private static string EnsureSuffixWithoutExtension(string name, string suffix)
        {
            string baseName = Path.GetFileNameWithoutExtension(name) ?? string.Empty;
            if (!baseName.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
            {
                baseName += suffix;
            }
            return baseName;
        }

        private static string GetInitialSaveDirectory()
        {
            try
            {
                string last = EditorPrefs.GetString(FileConstants.LAST_SAVE_DIRECTORY_KEY, null);
                if (!string.IsNullOrEmpty(last) && Directory.Exists(last)) return last;

                // 未保存時はプロジェクトルートを最優先
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                if (Directory.Exists(projectRoot)) return projectRoot;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FurMaskGenerator] 初期保存ディレクトリの取得に失敗: {ex.Message}");
            }

            return Application.dataPath;
        }

        #endregion
    }
}
#endif