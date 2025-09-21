#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator.Utils
{
    /// <summary>
    /// UI描画専用のユーティリティクラス
    /// </summary>
    public static class UIDrawingUtils
    {
        #region UIボックス描画

        /// <summary>
        /// UI用のボックス描画を開始する（共通の背景色設定）
        /// </summary>
        public static void BeginUIBox(Color? backgroundColor = null)
        {
            GUI.backgroundColor = backgroundColor ?? AppSettings.BOX_BACKGROUND;
            EditorGUILayout.BeginVertical("box");
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

        #endregion

        #region リスト描画

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
                    if (GUILayout.Button(UILabels.DELETE_BUTTON, GUILayout.Width(AppSettings.DELETE_BUTTON_WIDTH))) removeIndex = i;
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

        #endregion

        #region セクション描画

        /// <summary>
        /// セクション用の標準Foldoutヘッダを描画し、新しい展開状態を返す
        /// </summary>
        public static bool DrawSectionFoldout(bool currentState, string title)
        {
            Rect headerRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            return EditorGUI.Foldout(headerRect, currentState, title, true);
        }

        #endregion

        #region フォールドアウトユーティリティ

        /// <summary>
        /// フォールドアウト状態のサイズを対象件数に同期させる
        /// </summary>
        public static void EnsureFoldoutCount(List<bool> states, int targetCount, bool defaultValue = true)
        {
            if (states == null) return;
            while (states.Count < targetCount) states.Add(defaultValue);
            while (states.Count > targetCount) states.RemoveAt(states.Count - 1);
        }

        #endregion

        #region UI更新

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

        #region 3D描画ヘルパー

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
        /// 3D空間に十字マーカーを描画します
        /// </summary>
        public static void DrawCross(Vector3 position, float baseSize, Color color)
        {
            EditorGizmoUtils.SetDepthTest(true, () =>
            {
                Handles.color = color;
                float dynamicSize = Mathf.Max(baseSize, HandleUtility.GetHandleSize(position) * AppSettings.CROSS_SIZE_MULTIPLIER);
                Handles.DrawLine(position + Vector3.right * dynamicSize, position - Vector3.right * dynamicSize);
                Handles.DrawLine(position + Vector3.up * dynamicSize, position - Vector3.up * dynamicSize);
                Handles.DrawLine(position + Vector3.forward * dynamicSize, position - Vector3.forward * dynamicSize);
            });
        }

        #endregion
    }
}
#endif