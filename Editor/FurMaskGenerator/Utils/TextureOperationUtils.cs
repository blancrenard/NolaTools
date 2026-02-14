#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Constants;
using NolaTools.FurMaskGenerator.UI;

namespace NolaTools.FurMaskGenerator.Utils
{
    /// <summary>
    /// テクスチャ操作専用のユーティリティクラス
    /// </summary>
    public static class TextureOperationUtils
    {
        #region テクスチャプレビュー描画

        /// <summary>
        /// サムネイル付きのテクスチャプレビューと保存ボタンを描画する
        /// </summary>
        public static void DrawTexturePreviewItem(
            Texture2D texture,
            string titleLabel,
            string saveButtonLabel,
            string defaultSaveFileNameWithoutExtension,
            float maxWidth,
            string savePanelTitle)
        {
            if (texture == null) return;

            if (!string.IsNullOrEmpty(titleLabel))
            {
                EditorGUILayout.LabelField(titleLabel, EditorStyles.label);
            }

            float clampedWidth = Mathf.Max(32f, maxWidth);
            Rect r = GUILayoutUtility.GetAspectRect(1f, GUILayout.Width(clampedWidth));
            GUI.DrawTexture(r, texture, ScaleMode.ScaleToFit);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                TexturePreviewWindow.ShowWindow(texture);
            }

            if (GUILayout.Button(saveButtonLabel))
            {
                FileDialogUtils.SaveTexturePNG(texture, defaultSaveFileNameWithoutExtension, savePanelTitle);
            }
            
            GUILayout.Space(AppSettings.LARGE_SPACE);
        }

        #endregion

        #region テクスチャ作成と操作

        /// <summary>
        /// 指定サイズのテクスチャを作成し、ピクセルデータを設定してApplyする
        /// </summary>
        public static Texture2D CreateAndApplyTexture(int width, int height, Color[] pixels, bool linear = false)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
            texture.SetPixels(pixels);
            texture.Apply(false);
            return texture;
        }

        /// <summary>
        /// テクスチャのピクセルデータを更新してApplyする
        /// </summary>
        public static void UpdateTexturePixels(Texture2D texture, Color[] pixels)
        {
            if (texture == null || pixels == null) return;
            texture.SetPixels(pixels);
            texture.Apply(false);
        }

        #endregion
    }
}
#endif