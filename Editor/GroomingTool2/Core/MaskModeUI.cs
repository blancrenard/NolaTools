using GroomingTool2.Constants;
using GroomingTool2.Managers;
using GroomingTool2.State;
using UnityEditor;
using UnityEngine;

namespace GroomingTool2.Core
{
    /// <summary>
    /// マスクモードのUI描画を担当するクラス
    /// </summary>
    internal sealed class MaskModeUI
    {
        private readonly State.UvIslandMaskState maskState;
        private readonly FurDataManager furDataManager;
        private readonly UndoManager undoManager;
        private MaskSelectionMode maskSelectionMode = MaskSelectionMode.Click;

        public MaskSelectionMode SelectionMode
        {
            get => maskSelectionMode;
            set => maskSelectionMode = value;
        }

        public MaskModeUI(
            State.UvIslandMaskState maskState,
            FurDataManager furDataManager,
            UndoManager undoManager)
        {
            this.maskState = maskState ?? throw new System.ArgumentNullException(nameof(maskState));
            this.furDataManager = furDataManager ?? throw new System.ArgumentNullException(nameof(furDataManager));
            this.undoManager = undoManager ?? throw new System.ArgumentNullException(nameof(undoManager));
        }

        /// <summary>
        /// マスクモードのパラメータを描画する
        /// </summary>
        /// <param name="onMaskChanged">マスク変更時のコールバック</param>
        public void DrawParameters(System.Action onMaskChanged)
        {
            // マスクモード行全体を左詰め固定幅グループで包む
            using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(false)))
            {
                // 選択モード
                GUILayout.Label(GroomingTool2Labels.SELECTION_MODE, EditorStyles.boldLabel, GUILayout.Width(80));
                maskSelectionMode = (MaskSelectionMode)GUILayout.SelectionGrid(
                    (int)maskSelectionMode,
                    new[] { GroomingTool2Labels.MASK_CLICK, GroomingTool2Labels.MASK_RECT, GroomingTool2Labels.MASK_LASSO },
                    3,
                    EditorStyles.miniButton,
                    GUILayout.Width(240),
                    GUILayout.ExpandWidth(false)
                );

                GUILayout.Space(4f);

                // マスククリアボタン
                if (GUILayout.Button(GroomingTool2Labels.CLEAR_MASK_BUTTON, EditorStyles.miniButton, GUILayout.Width(100), GUILayout.ExpandWidth(false)))
                {
                    undoManager.SaveState(furDataManager.Data, maskState, GroomingTool2Labels.CLEAR_MASK_UNDO);
                    maskState.Clear();
                    maskState.RecalculateEffective();
                    onMaskChanged?.Invoke();
                }
            }
        }
    }
}








