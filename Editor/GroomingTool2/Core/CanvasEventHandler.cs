using System.Collections.Generic;
using GroomingTool2.Managers;
using GroomingTool2.State;
using GroomingTool2.Utils;
using UnityEditor;
using UnityEngine;

namespace GroomingTool2.Core
{
    /// <summary>
    /// キャンバスのイベント処理を担当するクラス
    /// </summary>
    internal sealed class CanvasEventHandler
    {
        private readonly GroomingTool2State state;
        private readonly BrushManager brushManager;
        private readonly FurDataManager furDataManager;
        private readonly UndoManager undoManager;
        private readonly GroomingTool2UI ui;
        private readonly State.UvIslandMaskState maskState;
        private readonly GroomingTool2MaterialManager materialManager;
        private readonly MaskSelectionHandler maskSelectionHandler;
        
        // Undo/Redo処理の委譲用コールバック
        private System.Action<string> saveUndoCallback;
        private System.Func<bool> canUndoCallback;
        private System.Func<bool> canRedoCallback;
        private System.Action undoCallback;
        private System.Action redoCallback;

        private readonly List<Vector2> mouseTrail = new();
        private readonly BrushStrokeExecutor strokeExecutor;
        private bool mouseDown;
        private int frameSkipCounter;
        private const int FrameSkipInterval = 2;

        // マスク選択用の状態
        private Vector2 maskSelectionStart;
        private Vector2 maskSelectionCurrent;
        private List<Vector2> maskLassoPoints;
        private bool maskSelectionActive;

        // パン（手のひらツール）用の状態
        private bool isPanning;
        private Vector2 panStartMousePosition;
        private Vector2 panStartScrollOffset;
        private bool isSpaceKeyHeld;

        // UV領域マスク（パディング適用済み）
        private bool[,] uvRegionMask;
        private int cachedMaterialIndex = -1;
        private int cachedUvPadding = -1;

        // キャンバス状態への参照（外部で管理）
        private Vector2 scrollOffsetData;
        private Rect canvasViewRect;
        private float lastScale;

        public Vector2 ScrollOffsetData
        {
            get => scrollOffsetData;
            set => scrollOffsetData = value;
        }

        public Rect CanvasViewRect
        {
            get => canvasViewRect;
            set => canvasViewRect = value;
        }

        public float LastScale
        {
            get => lastScale;
            set => lastScale = value;
        }

        // マスク選択プレビュー用の公開プロパティ
        public bool MaskSelectionActive => maskSelectionActive;
        public Vector2 MaskSelectionStart => maskSelectionStart;
        public Vector2 MaskSelectionCurrent => maskSelectionCurrent;
        public IReadOnlyList<Vector2> MaskLassoPoints => maskLassoPoints;
        public MaskSelectionMode CurrentMaskSelectionMode => ui.MaskSelectionMode;

        // パン操作の状態（カーソル変更用）
        public bool IsPanning => isPanning;
        public bool IsSpaceKeyHeld => isSpaceKeyHeld;

        public CanvasEventHandler(
            GroomingTool2State state,
            BrushManager brushManager,
            FurDataManager furDataManager,
            UndoManager undoManager,
            GroomingTool2UI ui,
            State.UvIslandMaskState maskState,
            GroomingTool2MaterialManager materialManager,
            MaskSelectionHandler maskSelectionHandler)
        {
            this.state = state;
            this.brushManager = brushManager;
            this.furDataManager = furDataManager;
            this.undoManager = undoManager;
            this.ui = ui;
            this.maskState = maskState;
            this.materialManager = materialManager;
            this.maskSelectionHandler = maskSelectionHandler;
            this.strokeExecutor = new BrushStrokeExecutor(brushManager, furDataManager);
        }
        
        /// <summary>
        /// Undo/Redo処理のコールバックを設定
        /// </summary>
        public void SetUndoRedoCallbacks(
            System.Action<string> saveUndo,
            System.Func<bool> canUndo,
            System.Func<bool> canRedo,
            System.Action undo,
            System.Action redo)
        {
            saveUndoCallback = saveUndo;
            canUndoCallback = canUndo;
            canRedoCallback = canRedo;
            undoCallback = undo;
            redoCallback = redo;
        }

        public void HandleEvents(Rect contentRect, EditorWindow window)
        {
            var e = Event.current;

            // 固定キャンバス：ビューポート内ローカル座標へ変換
            var scrolledMousePos = e.mousePosition - canvasViewRect.position;

            // スペースキーの状態を追跡
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space)
            {
                isSpaceKeyHeld = true;
                e.Use();
                window.Repaint();
                return;
            }
            if (e.type == EventType.KeyUp && e.keyCode == KeyCode.Space)
            {
                isSpaceKeyHeld = false;
                e.Use();
                window.Repaint();
                return;
            }

            // パン操作の処理（ミドルマウスボタン または スペースキー＋左マウスボタン）
            if (HandlePanning(e, scrolledMousePos, window))
            {
                return;
            }

            // ローカルRectでヒット判定
            var localRect = new Rect(0f, 0f, canvasViewRect.width, canvasViewRect.height);
            if (!localRect.Contains(scrolledMousePos) && e.type != EventType.ScrollWheel)
                return;

            var dataPoint = CoordinateUtils.ViewLocalToData(scrolledMousePos, scrollOffsetData, state.Scale);

            // マスクモードかどうかで処理を分岐
            if (ui.CurrentMode == ToolMode.Mask)
            {
                HandleMaskModeEvents(contentRect, dataPoint, e, window);
            }
            else
            {
                HandleBrushModeEvents(contentRect, dataPoint, e, window);
            }
        }

        /// <summary>
        /// パン操作の処理
        /// </summary>
        /// <returns>イベントが消費された場合はtrue</returns>
        private bool HandlePanning(Event e, Vector2 scrolledMousePos, EditorWindow window)
        {
            bool isMiddleMouseButton = e.button == 2;
            bool isSpacePlusLeftMouse = isSpaceKeyHeld && e.button == 0;

            switch (e.type)
            {
                case EventType.MouseDown when isMiddleMouseButton || isSpacePlusLeftMouse:
                    isPanning = true;
                    panStartMousePosition = scrolledMousePos;
                    panStartScrollOffset = scrollOffsetData;
                    e.Use();
                    return true;

                case EventType.MouseDrag when isPanning:
                    // マウス移動量をデータ座標系に変換してオフセットを更新
                    var mouseDelta = scrolledMousePos - panStartMousePosition;
                    var dataDelta = mouseDelta / Mathf.Max(state.Scale, 1e-6f);
                    var newOffset = panStartScrollOffset - dataDelta;
                    scrollOffsetData = CoordinateUtils.ClampScrollOffsetData(
                        new Vector2(canvasViewRect.width, canvasViewRect.height),
                        state.Scale,
                        Common.TexSize,
                        newOffset);
                    window.Repaint();
                    e.Use();
                    return true;

                case EventType.MouseUp when isPanning && (isMiddleMouseButton || e.button == 0):
                    isPanning = false;
                    e.Use();
                    return true;
            }

            return false;
        }

        private void HandleBrushModeEvents(Rect contentRect, Vector2 dataPoint, Event e, EditorWindow window)
        {
            switch (e.type)
            {
                case EventType.MouseMove:
                    window.Repaint();
                    break;
                case EventType.MouseDown when e.button == 0 && !isSpaceKeyHeld:
                    GUI.FocusControl(null);
                    mouseDown = true;
                    mouseTrail.Clear();
                    mouseTrail.Add(dataPoint);
                    e.Use();
                    break;

                case EventType.MouseDrag when mouseDown:
                    mouseTrail.Add(dataPoint);
                    if (mouseTrail.Count > 5)
                        mouseTrail.RemoveAt(0);

                    // パフォーマンス最適化：フレームスキップで処理を間引く
                    frameSkipCounter++;
                    if (frameSkipCounter >= FrameSkipInterval)
                    {
                        ApplyBrushStroke(mouseTrail, window);
                        frameSkipCounter = 0;
                    }
                    window.Repaint();
                    e.Use();
                    break;

                case EventType.MouseUp when mouseDown:
                    mouseDown = false;
                    SaveUndo("毛の編集", maskState);
                    e.Use();
                    break;

                case EventType.ScrollWheel:
                    HandleScrollWheel(contentRect, e.mousePosition, e, window);
                    break;

                case EventType.KeyDown:
                    HandleKeyShortcuts(e, window);
                    break;
            }
        }

        private void HandleMaskModeEvents(Rect contentRect, Vector2 dataPoint, Event e, EditorWindow window)
        {
            var scrolledMousePos = e.mousePosition - canvasViewRect.position;
            int dataX = Mathf.Clamp(Mathf.RoundToInt(dataPoint.x), 0, Common.TexSize - 1);
            int dataY = Mathf.Clamp(Mathf.RoundToInt(dataPoint.y), 0, Common.TexSize - 1);

            switch (e.type)
            {
                case EventType.MouseMove:
                    if (maskSelectionActive && ui.MaskSelectionMode == MaskSelectionMode.Lasso)
                    {
                        if (maskLassoPoints == null)
                            maskLassoPoints = new List<Vector2>();
                        maskLassoPoints.Add(dataPoint);
                    }
                    window.Repaint();
                    break;

                case EventType.MouseDown when e.button == 0 && !isSpaceKeyHeld:
                    GUI.FocusControl(null);
                    maskSelectionActive = true;
                    maskSelectionStart = dataPoint;
                    maskSelectionCurrent = dataPoint;
                    
                    if (ui.MaskSelectionMode == MaskSelectionMode.Click)
                    {
                        maskSelectionHandler.HandleClick(dataX, dataY, e);
                        e.Use();
                    }
                    else if (ui.MaskSelectionMode == MaskSelectionMode.Rectangle)
                    {
                        // 矩形選択はドラッグ終了時に処理
                        e.Use();
                    }
                    else if (ui.MaskSelectionMode == MaskSelectionMode.Lasso)
                    {
                        maskLassoPoints = new List<Vector2> { dataPoint };
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag when maskSelectionActive:
                    if (ui.MaskSelectionMode == MaskSelectionMode.Rectangle)
                    {
                        maskSelectionCurrent = dataPoint;
                        window.Repaint();
                        e.Use();
                    }
                    else if (ui.MaskSelectionMode == MaskSelectionMode.Lasso)
                    {
                        maskLassoPoints.Add(dataPoint);
                        window.Repaint();
                        e.Use();
                    }
                    break;

                case EventType.MouseUp when maskSelectionActive && e.button == 0:
                    if (ui.MaskSelectionMode == MaskSelectionMode.Rectangle)
                    {
                        maskSelectionHandler.HandleRectangle(maskSelectionStart, dataPoint, e);
                        e.Use();
                    }
                    else if (ui.MaskSelectionMode == MaskSelectionMode.Lasso)
                    {
                        maskSelectionHandler.HandleLasso(maskLassoPoints, e);
                        e.Use();
                    }
                    maskSelectionActive = false;
                    maskLassoPoints = null;
                    maskSelectionCurrent = Vector2.zero;
                    break;
            }

            // スクロールとキー操作は共通処理
            if (e.type == EventType.ScrollWheel)
            {
                HandleScrollWheel(contentRect, scrolledMousePos, e, window);
            }
            else if (e.type == EventType.KeyDown)
            {
                HandleKeyShortcuts(e, window);
            }
        }



        private void HandleScrollWheel(Rect contentRect, Vector2 scrolledMousePos, Event e, EditorWindow window)
        {
            // スクロール入力（水平・垂直・ズーム・ブラシサイズ）を統一的に処理
            var zoomDelta = e.delta.y;
            var horizontalRaw = Mathf.Abs(e.delta.x) > Mathf.Abs(e.delta.y) ? e.delta.x : 0f;
            var verticalRaw = e.delta.y;
            const float scrollSensitivity = 15f;

            if (e.alt)
            {
                // Altキー + マウスホイール：ブラシサイズ調整
                int sizeDelta = zoomDelta > 0 ? -1 : 1;
                int newSize = Mathf.Clamp(state.BrushSize + sizeDelta, 1, 40);
                if (newSize != state.BrushSize)
                {
                    state.BrushSize = newSize;
                }
            }
            else if (e.control)
            {
                // Ctrlキー + マウスホイール：拡大・縮小（カーソル内ならカーソル中心、外ならビュー中心）
                var oldScale = state.Scale;
                // 25%刻みでスケール調整
                float scaleStep = 0.25f;
                float newScale = zoomDelta > 0
                    ? Mathf.Max(0.25f, Mathf.Floor((oldScale - 0.01f) / scaleStep) * scaleStep)
                    : Mathf.Min(4f, Mathf.Ceil((oldScale + 0.01f) / scaleStep) * scaleStep);
                
                if (!Mathf.Approximately(oldScale, newScale))
                {
                    // ローカルRectで判定
                    var localRect = new Rect(0f, 0f, canvasViewRect.width, canvasViewRect.height);
                    bool cursorInside = localRect.Contains(scrolledMousePos);
                    // ピボットのビュー座標（ビューポート内の相対座標）
                    var desiredPivotView = cursorInside
                        ? scrolledMousePos
                        : new Vector2(canvasViewRect.width * 0.5f, canvasViewRect.height * 0.5f);
                    // ピボットのデータ座標（スケールに依存しない不変座標）
                    var pivotData = scrollOffsetData + desiredPivotView / Mathf.Max(oldScale, 1e-6f);

                    // スケールを反映
                    state.Scale = newScale;

                    // 新しいオフセットをピボット維持で再計算
                    var newOffsetData = pivotData - desiredPivotView / Mathf.Max(state.Scale, 1e-6f);
                    scrollOffsetData = CoordinateUtils.ClampScrollOffsetData(new Vector2(canvasViewRect.width, canvasViewRect.height), state.Scale, Common.TexSize, newOffsetData);

                    lastScale = state.Scale; // スクロールホイール由来の変更を記録
                }
            }
            else if (e.shift || Mathf.Abs(horizontalRaw) > 0.0001f)
            {
                // Shiftキー または 実際の水平スクロール入力：横スクロール
                var hDelta = (e.shift && Mathf.Abs(horizontalRaw) < 0.0001f) ? verticalRaw : horizontalRaw;
                var scrollDeltaX = hDelta * scrollSensitivity; // ピクセル基準
                var newOffset = scrollOffsetData;
                newOffset.x += scrollDeltaX / Mathf.Max(state.Scale, 1e-6f); // データ単位へ変換
                scrollOffsetData = CoordinateUtils.ClampScrollOffsetData(new Vector2(canvasViewRect.width, canvasViewRect.height), state.Scale, Common.TexSize, newOffset);
            }
            else
            {
                // 垂直スクロール
                var scrollDeltaY = verticalRaw * scrollSensitivity; // ピクセル基準
                var newOffset = scrollOffsetData;
                newOffset.y += scrollDeltaY / Mathf.Max(state.Scale, 1e-6f); // データ単位へ変換
                scrollOffsetData = CoordinateUtils.ClampScrollOffsetData(new Vector2(canvasViewRect.width, canvasViewRect.height), state.Scale, Common.TexSize, newOffset);
            }
            window.Repaint();
            e.Use();
        }

        private void ApplyBrushStroke(IReadOnlyList<Vector2> points, EditorWindow window)
        {
            // UV領域マスクを更新（必要に応じて再構築）
            RebuildUvRegionMaskIfNeeded();

            // UV内のみ編集する設定がオフの場合はマスクを無効化
            var effectiveUvMask = state.RestrictEditToUvRegion ? uvRegionMask : null;

            strokeExecutor.ExecuteStrokeWithDirectionAndUvMask(
                points,
                maskState,
                ui.MirrorEnabled,
                ui.EraserMode,
                ui.BlurMode,
                ui.PinchMode,
                ui.InclinedOnly,
                ui.DirOnly,
                ui.PinchInverted,
                null, // overrideRadians
                effectiveUvMask);

            // Scene編集が有効な場合、Sceneビューの毛の線もリアルタイムで更新
            if (state.SceneEditingEnabled)
            {
                SceneView.RepaintAll();
            }
        }

        private void SaveUndo(string description, State.UvIslandMaskState maskState)
        {
            if (saveUndoCallback != null)
            {
                saveUndoCallback(description);
            }
            else
            {
                // フォールバック：コールバックが設定されていない場合は直接呼び出す
                undoManager.SaveState(furDataManager.Data, maskState, description);
            }
        }

        /// <summary>
        /// キーショートカットの処理
        /// </summary>
        private void HandleKeyShortcuts(Event e, EditorWindow window)
        {
            // Ctrl+Z, Ctrl+Y はUndo/Redo（GroomingTool2に委譲）
            if (e.control)
            {
                if (e.keyCode == KeyCode.Z && canUndoCallback != null && canUndoCallback())
                {
                    undoCallback?.Invoke();
                    e.Use();
                    return;
                }
                else if (e.keyCode == KeyCode.Y && canRedoCallback != null && canRedoCallback())
                {
                    redoCallback?.Invoke();
                    e.Use();
                    return;
                }
            }

            // マスク関連のショートカット（Ctrlなし）
            if (!e.control && !e.shift && !e.alt)
            {
                switch (e.keyCode)
                {
                    case KeyCode.C:
                        // C: マスククリア（マスクモード時のみ）
                        if (ui.CurrentMode == ToolMode.Mask)
                        {
                            undoManager.SaveState(furDataManager.Data, maskState, "マスククリア");
                            maskState.Clear();
                            maskState.RecalculateEffective();
                            ui.NotifyMaskChanged();
                            window.Repaint();
                            e.Use();
                        }
                        break;
                }
            }
        }

        #region UV領域マスク

        /// <summary>
        /// UV領域マスクを必要に応じて再構築
        /// </summary>
        private void RebuildUvRegionMaskIfNeeded()
        {
            var selectedMaterial = materialManager.SelectedMaterial;
            if (!selectedMaterial.HasValue)
            {
                uvRegionMask = null;
                cachedMaterialIndex = -1;
                return;
            }

            int currentMaterialIndex = materialManager.SelectedMaterialIndex;
            int currentPadding = state.UvPadding;

            // マテリアルまたはパディングが変更された場合のみ再構築
            if (cachedMaterialIndex != currentMaterialIndex || cachedUvPadding != currentPadding)
            {
                RebuildUvRegionMask(selectedMaterial.Value, currentPadding);
                cachedMaterialIndex = currentMaterialIndex;
                cachedUvPadding = currentPadding;
            }
        }

        /// <summary>
        /// UV領域マスクを再構築（パディング適用）
        /// </summary>
        private void RebuildUvRegionMask(MaterialEntry materialEntry, int padding)
        {
            uvRegionMask = UvRegionMaskUtils.BuildUvRegionMask(materialEntry, padding);
        }

        /// <summary>
        /// UV領域マスクのキャッシュをクリアする
        /// </summary>
        public void InvalidateUvRegionMaskCache()
        {
            cachedMaterialIndex = -1;
            cachedUvPadding = -1;
        }

        /// <summary>
        /// UV領域マスクを事前に構築して初回ストロークのラグを防ぐ
        /// </summary>
        public void PrewarmUvRegionMask()
        {
            RebuildUvRegionMaskIfNeeded();
        }

        #endregion

        /// <summary>
        /// リソースを解放する
        /// </summary>
        public void Dispose()
        {
            strokeExecutor?.Dispose();
        }
    }
}

