#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator.Utils
{
    /// <summary>
    /// Undo/Redo操作専用のユーティリティクラス
    /// </summary>
    public static class UndoRedoUtils
    {
        #region 拡張Undo/SetDirtyユーティリティ

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
    }
}
#endif