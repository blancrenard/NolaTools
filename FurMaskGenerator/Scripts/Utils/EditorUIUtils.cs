#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Mask.Generator.Constants;
using Mask.Generator.UI;

namespace Mask.Generator.Utils
{
    /// <summary>
    /// Extended UI utilities for Unity Editor operations
    /// Contains UI operations, texture handling, and drawing utilities
    /// </summary>
    public static class EditorUIUtils
    {


        #region UI Refresh Methods

        /// <summary>
        /// UIを更新します
        /// </summary>
        public static void RefreshUI()
        {
            SceneView.RepaintAll();
            if (EditorWindow.focusedWindow != null)
            {
                EditorWindow.focusedWindow.Repaint();
            }
        }

        #endregion

        /// <summary>
        /// UI用のボックス描画を開始する（共通の背景色設定）
        /// </summary>
        public static void BeginUIBox(Color? backgroundColor = null)
        {
            GUI.backgroundColor = backgroundColor ?? UIConstants.BOX_BACKGROUND;
            EditorGUILayout.BeginVertical(UIConstants.GUI_STYLE_BOX);
            GUI.backgroundColor = Color.white;
        }

        /// <summary>
        /// UI用のボックス描画を終了する
        /// </summary>
        public static void EndUIBox()
        {
            EditorGUILayout.EndVertical();
        }

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
                SaveTexturePNG(texture, defaultSaveFileNameWithoutExtension, savePanelTitle);
            }
            
            GUILayout.Space(UIConstants.LARGE_SPACE);
        }

        public static void DrawTexturePreviewList(
            IEnumerable<(Texture2D texture, string title, string fileKey)> items,
            float maxWidth,
            string saveButtonBaseLabel,
            string savePanelTitle)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (item.texture == null) continue;
                BeginUIBox();
                DrawTexturePreviewItem(
                    item.texture,
                    item.title,
                    string.IsNullOrEmpty(item.title) ? saveButtonBaseLabel : $"{saveButtonBaseLabel} {item.title}",
                    item.fileKey,
                    maxWidth,
                    savePanelTitle
                );
                EndUIBox();
                GUILayout.Space(UIConstants.LARGE_SPACE);
            }
        }

        // フォールドアウト状態のサイズを対象件数に同期させる
        public static void EnsureFoldoutCount(List<bool> states, int targetCount, bool defaultValue = true)
        {
            if (states == null) return;
            while (states.Count < targetCount) states.Add(defaultValue);
            while (states.Count > targetCount) states.RemoveAt(states.Count - 1);
        }

        public enum MarkerColorVariant
        {
            Vivid,
            HighSaturation,
            Pastel,
            Random
        }

        // 色生成システムの設定
        private static List<Color> generatedColorHistory = new List<Color>();
        private const int MAX_COLOR_HISTORY = 50;
        private const float MIN_COLOR_DIFFERENCE = 0.4f;
        private const float MIN_HUE_DIFFERENCE = 0.15f;
        private const float MIN_RECENT_COLOR_DIFFERENCE = 0.6f;
        
        // ゴールデンアングル分散システム
        private const float GOLDEN_ANGLE = 0.618033988749895f;
        private const string PREFS_HUE_INDEX_KEY = "Mask.Generator.Utils.EditorUIUtils.HueIndex";
        private const string PREFS_HUE_OFFSET_KEY = "Mask.Generator.Utils.EditorUIUtils.HueOffset";
        private static int _hueIndex = -1;
        private static float _hueOffset = -1f;

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

        // 高彩度色生成用の定数
        private const float HIGH_SATURATION = 1.0f;
        private const float HIGH_VALUE_MIN = 0.7f;
        private const float HIGH_VALUE_MAX = 1.0f;

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

        /// <summary>
        /// ゴールデンアングル高彩度マーカー色を生成
        /// </summary>
        public static Color GenerateMarkerColor()
        {
            float hue = GetNextHue();
            DeriveSVFromHue(hue, out float saturation, out float value);
            return Color.HSVToRGB(hue, saturation, value);
        }

        /// <summary>
        /// Renderer の一覧をIMGUIで描画（独自の簡易リオーダー付き）
        /// </summary>
        public static void DrawRendererList(List<Renderer> list, string header)
        {
            DrawObjectList(list, header);
        }

        /// <summary>
        /// オブジェクトの一覧をIMGUIで描画（上下移動/削除/追加）
        /// </summary>
        public static void DrawObjectList<T>(List<T> list, string header) where T : UnityEngine.Object
        {
            if (list == null) return;
            BeginUIBox();
            try
            {
                if (!string.IsNullOrEmpty(header))
                {
                    EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
                }

                int removeIndex = -1;
                int moveUpIndex = -1;
                int moveDownIndex = -1;

                for (int i = 0; i < list.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    var newVal = (T)EditorGUILayout.ObjectField(list[i], typeof(T), true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        list[i] = newVal;
                    }

                    GUI.enabled = i > 0;
                    if (GUILayout.Button("▲", GUILayout.Width(24))) moveUpIndex = i;
                    GUI.enabled = i < list.Count - 1;
                    if (GUILayout.Button("▼", GUILayout.Width(24))) moveDownIndex = i;
                    GUI.enabled = true;
                    if (GUILayout.Button(UIConstants.DELETE_BUTTON, GUILayout.Width(UIConstants.DELETE_BUTTON_WIDTH))) removeIndex = i;
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space();
                if (GUILayout.Button("追加"))
                {
                    list.Add(null);
                }

                if (removeIndex >= 0 && removeIndex < list.Count)
                {
                    list.RemoveAt(removeIndex);
                }
                if (moveUpIndex > 0)
                {
                    var tmp = list[moveUpIndex - 1];
                    list[moveUpIndex - 1] = list[moveUpIndex];
                    list[moveUpIndex] = tmp;
                }
                if (moveDownIndex >= 0 && moveDownIndex < list.Count - 1)
                {
                    var tmp = list[moveDownIndex + 1];
                    list[moveDownIndex + 1] = list[moveDownIndex];
                    list[moveDownIndex] = tmp;
                }
            }
            finally
            {
                EndUIBox();
            }
        }

        /// <summary>
        /// UIボックス内でアクションを実行する（BeginUIBox/EndUIBoxのtry-finallyパターンを共通化）
        /// </summary>
        public static void DrawInUIBox(System.Action drawAction, Color? backgroundColor = null)
        {
            BeginUIBox(backgroundColor);
            try
            {
                drawAction?.Invoke();
            }
            finally
            {
                EndUIBox();
            }
        }

        /// <summary>
        /// セクション用の標準Foldoutヘッダを描画し、新しい展開状態を返す
        /// </summary>
        public static bool DrawSectionFoldout(bool currentState, string title)
        {
            Rect headerRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            return EditorGUI.Foldout(headerRect, currentState, title, true);
        }

        /// <summary>
        /// Undo.RecordObject と SetDirty と遅延保存をまとめて行うヘルパ
        /// </summary>
        public static void RecordUndoSetDirtyAndScheduleSave(UnityEngine.Object target, string undoMessage)
        {
            if (target == null) return;
            Undo.RecordObject(target, undoMessage);
            EditorUtility.SetDirty(target);
            EditorAssetUtils.ScheduleSaveAssets();
        }

        /// <summary>
        /// Undo.RecordObject と SetDirty を行う（保存は呼ばない）。ドラッグ等の高頻度更新用
        /// </summary>
        public static void RecordUndoAndSetDirty(UnityEngine.Object target, string undoMessage)
        {
            if (target == null) return;
            Undo.RecordObject(target, undoMessage);
            EditorUtility.SetDirty(target);
        }

        #region Enhanced Undo/SetDirty Utilities

        /// <summary>
        /// 複数オブジェクトのUndo記録とSetDirtyを一括実行
        /// </summary>
        public static void RecordUndoAndSetDirtyMultiple(UnityEngine.Object[] targets, string undoMessage)
        {
            if (targets == null) return;
            foreach (var target in targets)
            {
                if (target != null)
                {
                    Undo.RecordObject(target, undoMessage);
                    EditorUtility.SetDirty(target);
                }
            }
        }

        /// <summary>
        /// 複数オブジェクトのUndo記録、SetDirty、遅延保存を一括実行
        /// </summary>
        public static void RecordUndoSetDirtyAndScheduleSaveMultiple(UnityEngine.Object[] targets, string undoMessage)
        {
            if (targets == null) return;
            bool anyDirty = false;
            foreach (var target in targets)
            {
                if (target != null)
                {
                    Undo.RecordObject(target, undoMessage);
                    EditorUtility.SetDirty(target);
                    anyDirty = true;
                }
            }
            if (anyDirty)
            {
                EditorAssetUtils.ScheduleSaveAssets();
            }
        }

        /// <summary>
        /// 条件付きUndo記録とSetDirty（変更があった場合のみ実行）
        /// </summary>
        public static void RecordUndoAndSetDirtyIfChanged(UnityEngine.Object target, string undoMessage, bool hasChanged)
        {
            if (target == null || !hasChanged) return;
            Undo.RecordObject(target, undoMessage);
            EditorUtility.SetDirty(target);
        }

        /// <summary>
        /// 条件付きUndo記録、SetDirty、遅延保存（変更があった場合のみ実行）
        /// </summary>
        public static void RecordUndoSetDirtyAndScheduleSaveIfChanged(UnityEngine.Object target, string undoMessage, bool hasChanged)
        {
            if (target == null || !hasChanged) return;
            Undo.RecordObject(target, undoMessage);
            EditorUtility.SetDirty(target);
            EditorAssetUtils.ScheduleSaveAssets();
        }

        /// <summary>
        /// 高頻度更新用の軽量SetDirty（Undo記録なし）
        /// 注意: このメソッドはUndo記録を行わないため、慎重に使用してください
        /// </summary>
        public static void SetDirtyOnly(UnityEngine.Object target)
        {
            if (target == null) return;
            EditorUtility.SetDirty(target);
        }

        /// <summary>
        /// 高頻度更新用の軽量SetDirty + 遅延保存（Undo記録なし）
        /// 注意: このメソッドはUndo記録を行わないため、慎重に使用してください
        /// </summary>
        public static void SetDirtyAndScheduleSaveOnly(UnityEngine.Object target)
        {
            if (target == null) return;
            EditorUtility.SetDirty(target);
            EditorAssetUtils.ScheduleSaveAssets();
        }

        #endregion

        /// <summary>
        /// 互換API：直接の Begin/End は DrawInUIBox の利用を推奨
        /// </summary>
        [Obsolete("Use DrawInUIBox(Action) instead.")]
        public static void BeginUIBox(Color? backgroundColor, bool deprecated = true) => BeginUIBox(backgroundColor);
        [Obsolete("Use DrawInUIBox(Action) instead.")]
        public static void EndUIBox(bool deprecated = true) => EndUIBox();

        // #endregion

        #region Texture Operations Methods

        /// <summary>
        /// SaveFilePanel を表示してテクスチャをPNG で保存する。
        /// 初期フォルダは前回保存先（EditorPrefs）を優先し、未設定時はOSのピクチャ→デスクトップ→プロジェクトルート。
        /// </summary>
        public static void SaveTexturePNG(Texture2D texture, string defaultFileName, string panelTitle)
        {
            if (texture == null)
            {
                EditorUtility.DisplayDialog(UIConstants.ERROR_DIALOG_TITLE, UIConstants.ERROR_TEXTURE_NULL, UIConstants.ERROR_DIALOG_OK);
                return;
            }

            string safeName = EditorAssetUtils.SanitizeFileName(defaultFileName);
            // 既定名の末尾に _FurLenMask を付与（重複付与は回避）
            safeName = EnsureSuffixWithoutExtension(safeName, UIConstants.FUR_LENGTH_MASK_SUFFIX);
            string initialDirectory = GetInitialSaveDirectory();
            string path = EditorUtility.SaveFilePanel(panelTitle, initialDirectory, safeName, UIConstants.FILE_EXTENSION_PNG);
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
                try
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        EditorPrefs.SetString(UIConstants.LAST_SAVE_DIRECTORY_KEY, dir);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[FurMaskGenerator] 保存先ディレクトリの記録に失敗: {ex.Message}");
                }
                AssetDatabase.Refresh();
            }
        }

        private static string EnsureSuffixWithoutExtension(string name, string suffix)
        {
            string baseName = Path.GetFileNameWithoutExtension(name) ?? string.Empty;
            if (!baseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                baseName += suffix;
            }
            return baseName;
        }

        private static string GetInitialSaveDirectory()
        {
            try
            {
                string last = EditorPrefs.GetString(UIConstants.LAST_SAVE_DIRECTORY_KEY, null);
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

        /// <summary>
        /// テクスチャにエッジパディングを適用する
        /// </summary>
        public static Texture2D ApplyEdgePadding(Texture2D sourceTexture, int padding)
        {
            if (sourceTexture == null) return null;
            int width = sourceTexture.width;
            int height = sourceTexture.height;
            Color[] sourcePixels = sourceTexture.GetPixels();
            var padded = EditorTextureUtils.ApplyEdgePadding(sourcePixels, width, height, padding, null, Mask.Generator.Constants.UIConstants.VALID_PIXEL_THRESHOLD);
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            result.SetPixels(padded);
            result.Apply(false);
            return result;
        }

        /// <summary>
        /// エッジパディングを元テクスチャに直接適用（有効画素マスク対応）
        /// </summary>
        public static void ApplyEdgePaddingInPlace(Texture2D sourceTexture, int padding, bool[] originalValidMask = null, float validPixelThreshold = Mask.Generator.Constants.UIConstants.VALID_PIXEL_THRESHOLD)
        {
            if (sourceTexture == null) return;
            int width = sourceTexture.width;
            int height = sourceTexture.height;
            Color[] sourcePixels = sourceTexture.GetPixels();
            var padded = EditorTextureUtils.ApplyEdgePadding(sourcePixels, width, height, padding, originalValidMask, validPixelThreshold);
            sourceTexture.SetPixels(padded);
            sourceTexture.Apply(false);
        }


        #endregion

        #region Drawing Operations Methods

        /// <summary>
        /// 選択中のスフィア外周をハイライトして描画します
        /// </summary>
        public static void DrawSelectedSphereHighlight(Vector3 position, float radius, Color baseColor)
        {
            EditorGizmoUtils.SetDepthTest(true, () =>
            {
                Color glow = new Color(1f, 1f, 1f, 0.65f);
                EditorGizmoUtils.DrawWireframeSphere(position, radius * 1.02f, glow);
                EditorGizmoUtils.DrawWireframeSphere(position, radius * 0.98f, new Color(baseColor.r, baseColor.g, baseColor.b, 0.85f));
            });
        }

        /// <summary>
        /// 外周/内周（グラデーション）とアウトラインをまとめて描画
        /// </summary>
        public static void DrawGradientSpheres(
            Vector3 position,
            float radius,
            float gradientWidth,
            Color baseWireColor,
            Color innerMaskColor,
            Color gradientAreaColor,
            float outlineAlpha)
        {
            EditorGizmoUtils.DrawWireframeSphere(position, radius, baseWireColor);

            float innerRadius = radius * (1f - gradientWidth);
            if (gradientWidth > 0f && innerRadius > 0f)
            {
                EditorGizmoUtils.DrawWireframeSphere(position, innerRadius, innerMaskColor);
            }
        }

        /// <summary>
        /// アバターレンダラーのリストから中心点を計算します
        /// </summary>
        public static Vector3 CalculateAvatarCenter(List<Renderer> avatarRenderers)
        {
            if (avatarRenderers.Count == 0) return Vector3.zero;

            Vector3 center = Vector3.zero;
            int validCount = 0;

            foreach (var renderer in avatarRenderers)
            {
                if (renderer != null)
                {
                    center += renderer.bounds.center;
                    validCount++;
                }
            }

            if (validCount > 0)
            {
                center /= validCount;
            }

            return center;
        }

        #endregion

        #region Marker Helpers

        /// <summary>
        /// 3D空間に十字マーカーを描画します
        /// </summary>
        public static void DrawCross(Vector3 position, float baseSize, Color color)
        {
            EditorGizmoUtils.SetDepthTest(true, () =>
            {
                Handles.color = color;
                float dynamicSize = Mathf.Max(baseSize, HandleUtility.GetHandleSize(position) * 0.02f);
                Handles.DrawLine(position + Vector3.right * dynamicSize, position - Vector3.right * dynamicSize);
                Handles.DrawLine(position + Vector3.up * dynamicSize, position - Vector3.up * dynamicSize);
                Handles.DrawLine(position + Vector3.forward * dynamicSize, position - Vector3.forward * dynamicSize);
            });
        }

        #endregion
    }
}
#endif
