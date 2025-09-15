#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mask.Generator.Utils;
using Mask.Generator.Constants;

namespace Mask.Generator.Utils
{
    /// <summary>
    /// Core utility functions for Unity Editor operations
    /// Contains validation, safe execution, progress display, and general utilities
    /// </summary>
    public static class EditorCoreUtils
    {
        #region Validation Methods

        /// <summary>
        /// 汎用的な検証メソッド（条件チェック + エラーダイアログ表示）
        /// </summary>
        private static bool ValidateCondition(Func<bool> condition, string errorTitle, string errorMessage, string okLabel)
        {
            if (condition()) return true;
            EditorUtility.DisplayDialog(errorTitle, errorMessage, okLabel);
            return false;
        }

        /// <summary>
        /// オブジェクトが割り当てられているかを検証します
        /// </summary>
        public static bool ValidateAssigned(UnityEngine.Object obj, string errorTitle, string errorMessage, string okLabel)
        {
            return ValidateCondition(() => obj != null, errorTitle, errorMessage, okLabel);
        }

        /// <summary>
        /// レンダラーのリストが空でないかを検証します
        /// </summary>
        public static bool ValidateNonEmptyRenderers(IEnumerable<Renderer> renderers, string errorTitle, string errorMessage, string okLabel)
        {
            return ValidateCondition(() => renderers != null && renderers.Any(r => r != null), errorTitle, errorMessage, okLabel);
        }

        /// <summary>
        /// メッシュにUVが存在するかを検証します
        /// </summary>
        public static bool ValidateUVsPresent(UnityEngine.Mesh mesh, string errorTitle, string errorMessage, string okLabel)
        {
            return ValidateCondition(() =>
            {
                if (mesh == null) return false;
                var uvs = mesh.uv;
                return uvs != null && uvs.Length == mesh.vertexCount;
            }, errorTitle, errorMessage, okLabel);
        }

        /// <summary>
        /// レンダラーが有効なリストに含まれているかを検証します
        /// </summary>
        public static bool ValidateRendererInLists(Renderer renderer, IEnumerable<Renderer> avatarRenderers, IEnumerable<Renderer> clothRenderers)
        {
            if (renderer == null) return false;
            return (avatarRenderers?.Contains(renderer) ?? false) || (clothRenderers?.Contains(renderer) ?? false);
        }

        /// <summary>
        /// レンダラーから有効なメッシュを取得し、検証します
        /// </summary>
        public static Mesh ValidateAndGetMesh(Renderer renderer, out bool isBakedTempMesh, string errorTitle = null, string errorMessage = null, string okLabel = null)
        {
            var mesh = EditorMeshUtils.GetMeshForRenderer(renderer, out isBakedTempMesh);
            if (mesh == null || mesh.vertexCount == 0)
            {
                if (!string.IsNullOrEmpty(errorTitle) && !string.IsNullOrEmpty(errorMessage))
                {
                    EditorUtility.DisplayDialog(errorTitle, errorMessage + " " + renderer.name, okLabel ?? UILabels.ERROR_DIALOG_OK);
                }
                return null;
            }
            return mesh;
        }

        /// <summary>
        /// レンダラーの完全な検証（リスト内確認 + メッシュ取得 + 検証）
        /// </summary>
        public static Mesh ValidateRendererAndMesh(Renderer renderer, IEnumerable<Renderer> avatarRenderers, IEnumerable<Renderer> clothRenderers, out bool isBakedTempMesh, string errorTitle, string rendererErrorMessage, string meshErrorMessage, string okLabel)
        {
            if (renderer == null)
            {
                isBakedTempMesh = false;
                return null;
            }

            if (!ValidateRendererInLists(renderer, avatarRenderers, clothRenderers))
            {
                EditorUtility.DisplayDialog(errorTitle, rendererErrorMessage, okLabel);
                isBakedTempMesh = false;
                return null;
            }

            return ValidateAndGetMesh(renderer, out isBakedTempMesh, errorTitle, meshErrorMessage, okLabel);
        }

        #endregion

        #region Safe Execution Methods

        /// <summary>
        /// 安全な実行メソッド（例外処理付き）
        /// </summary>
        public static void SafeExecute(Action action, string context, Action<Exception> onError = null)
        {
            try
            {
                action?.Invoke();
            }
            catch (ExitGUIException)
            {
                // UnityのGUI描画を中断するために投げられる制御用例外。ログしないで再スロー。
                throw;
            }
            catch (Exception e)
            {
                Debug.LogError($"{context} Error: {e}");
                onError?.Invoke(e);
            }
        }

        #endregion

        #region Progress Methods

        private static double _lastUpdateTime;
        private const double DefaultMinIntervalSeconds = 0.05; // 20fps

        /// <summary>
        /// キャンセル可能なプログレスバーを表示します
        /// </summary>
        public static bool ShowCancelableProgress(string title, string info, float progress)
        {
            return EditorUtility.DisplayCancelableProgressBar(title, info, progress);
        }

        /// <summary>
        /// スロットリング付きでキャンセル可能なプログレスバーを表示します
        /// </summary>
        public static bool ShowCancelableProgressThrottled(string title, string info, float progress, double minIntervalSeconds = DefaultMinIntervalSeconds)
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastUpdateTime < minIntervalSeconds)
            {
                return false;
            }
            _lastUpdateTime = now;
            return ShowCancelableProgress(title, info, progress);
        }

        /// <summary>
        /// 適応的プログレス更新（処理量に応じて間隔を動的調整）
        /// </summary>
        public static bool ShowCancelableProgressAdaptive(string title, string info, float progress, int totalItems, int currentItem, double baseIntervalSeconds = DefaultMinIntervalSeconds)
        {
            // 処理量に応じて更新間隔を動的調整
            double adaptiveInterval = CalculateAdaptiveProgressInterval(totalItems, baseIntervalSeconds);
            
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastUpdateTime < adaptiveInterval)
            {
                return false;
            }
            
            _lastUpdateTime = now;
            
            // 進捗メッセージに詳細情報を追加
            string detailedInfo = $"{info} ({currentItem}/{totalItems})";
            return ShowCancelableProgress(title, detailedInfo, progress);
        }

        /// <summary>
        /// 適応的プログレス更新間隔を計算
        /// </summary>
        private static double CalculateAdaptiveProgressInterval(int totalItems, double baseIntervalSeconds)
        {
            if (totalItems <= 1000)
                return baseIntervalSeconds * 0.5; // 小規模: より頻繁に更新
            else if (totalItems <= 10000)
                return baseIntervalSeconds; // 中規模: 標準間隔
            else if (totalItems <= 100000)
                return baseIntervalSeconds * 2; // 大規模: 間隔を広げる
            else
                return baseIntervalSeconds * 4; // 超大規模: 大幅に間隔を広げる
        }

        /// <summary>
        /// プログレスバーをクリアします
        /// </summary>
        public static void ClearProgress()
        {
            EditorUtility.ClearProgressBar();
            _lastUpdateTime = 0;
        }

        /// <summary>
        /// キャンセル時に自動でプログレスバーをクリアするヘルパーメソッド
        /// </summary>
        public static bool ShowCancelableProgressAutoClear(string title, string info, float progress)
        {
            bool cancelled = ShowCancelableProgress(title, info, progress);
            if (cancelled)
            {
                ClearProgress();
            }
            return cancelled;
        }

        /// <summary>
        /// スロットリング付きでキャンセル時に自動クリアするプログレスバーを表示します
        /// </summary>
        public static bool ShowCancelableProgressThrottledAutoClear(string title, string info, float progress, double minIntervalSeconds = DefaultMinIntervalSeconds)
        {
            bool cancelled = ShowCancelableProgressThrottled(title, info, progress, minIntervalSeconds);
            if (cancelled)
            {
                ClearProgress();
            }
            return cancelled;
        }

        #endregion

        #region General Utility Methods

        // General utility methods moved from EditorUtils for backwards compatibility
        // These methods delegate to the appropriate specialized classes


        #endregion
    }
}
#endif
