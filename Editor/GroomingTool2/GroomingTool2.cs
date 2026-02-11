using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using GroomingTool2.Core;
using GroomingTool2.Managers;
using GroomingTool2.Rendering;
using GroomingTool2.Services;
using GroomingTool2.State;
using UnityEditor;
using UnityEngine;

namespace GroomingTool2
{
    internal sealed class GroomingTool2 : EditorWindow
    {
        // 依存関係の注入
        private GroomingTool2Context context;
        private GameObject previousAvatar;
        private AutoSaveHelper autoSaveHelper;

        // キャンバス関連の状態
        private Rect canvasViewRect;
        private Vector2 scrollOffsetData;
        private float lastScale;

        // 未保存状態の追跡
        private bool _hasUnsavedChanges;


        [MenuItem("Tools/FurTools/GroomingTool2")]
        public static void Open()
        {
            var window = GetWindow<GroomingTool2>("GroomingTool2", true);
            window.Show();
        }

        /// <summary>
        /// 未保存変更があるかどうか
        /// </summary>
        public bool HasUnsavedChanges => _hasUnsavedChanges;

        private void OnEnable()
        {
            wantsMouseMove = true;

            try
            {
                // 依存関係の初期化
                InitializeDependencies();

                // UIイベントの購読
                SubscribeToUIEvents();
                
                // Unityエディタ終了時の自動保存を購読
                EditorApplication.quitting -= OnEditorQuitting;
                EditorApplication.quitting += OnEditorQuitting;
            }
            catch (Exception ex)
            {
                Debug.LogError($"ウィンドウの初期化に失敗しました: {ex.Message}");
            }
        }

        private void InitializeDependencies()
        {
            try
            {
                context = new GroomingTool2Context();
                context.Initialize();

                // 状態変更の監視（UVパディング変更などの重い処理を事前実行）
                if (context.State != null)
                {
                    context.State.PropertyChanged -= OnStatePropertyChanged;
                    context.State.PropertyChanged += OnStatePropertyChanged;
                }

                previousAvatar = context.State?.Avatar;

                // Undo/Redo処理のコールバックを設定
                if (context.EventHandler != null)
                {
                    context.EventHandler.SetUndoRedoCallbacks(
                        description => SaveUndo(description),
                        () => context.UndoManager.CanUndo,
                        () => context.UndoManager.CanRedo,
                        Undo,
                        Redo
                    );
                }

                // 自動保存ヘルパーの初期化
                autoSaveHelper = new AutoSaveHelper(context.FileManager);

                InitializeMaterialList();
                
                // Sceneビューコントローラを初期化
                context.InitializeSceneViewController(
                    description => SaveUndo(description),
                    () => Repaint()
                );
                
                // Sceneビューコントローラに Undo/Redo コールバックを設定
                context.SceneViewController?.SetUndoRedoCallbacks(
                    () => context.UndoManager.CanUndo,
                    () => context.UndoManager.CanRedo,
                    Undo,
                    Redo
                );
                
                // 初期化完了後に初期状態を保存
                SaveInitialState();
            }
            catch (Exception ex)
            {
                Debug.LogError($"依存関係の初期化に失敗しました: {ex.Message}");
            }
        }



        private void InitializeMaterialList()
        {
            if (context?.MaterialManager == null || context?.State == null)
                return;

            context.MaterialManager.RebuildMaterialList(context.State.Avatar);
            
            // 初回のマテリアル選択時にVertexSymmetryMapperを初期化
            if (context.MaterialManager.SelectedMaterial.HasValue && context.VertexSymmetryMapper != null)
            {
                InitializeVertexSymmetryMapper();
            }
        }
        
        /// <summary>
        /// 初期化完了後に初期状態を保存する
        /// </summary>
        private void SaveInitialState()
        {
            if (context?.UndoManager != null && context?.FurDataManager != null && context?.MaskState != null)
            {
                // キー情報を更新
                UpdateAutoSaveKeyInfo();
                
                // 自動読み込みを試みる
                autoSaveHelper.TryAutoLoad(context.FurDataManager);
                
                // 自動読み込みの成否に関わらず、現在の状態を初期状態として保存
                // （Undoのために必ず1つ目の状態が必要）
                context.UndoManager.SaveState(context.FurDataManager.Data, context.MaskState, "初期状態");
            }
        }

        private void OnDisable()
        {
            // ツールを閉じる前に自動保存
            autoSaveHelper?.AutoSave(context?.FurDataManager);
            
            // Unityエディタ終了イベントの購読解除
            EditorApplication.quitting -= OnEditorQuitting;
            
            if (context?.State != null)
            {
                context.State.PropertyChanged -= OnStatePropertyChanged;
            }

            // イベント購読の解除
            if (context?.MaterialManager != null)
            {
                context.MaterialManager.OnMaterialSelected -= OnMaterialSelected;
            }

            // リソースのクリーンアップ
            context?.Dispose();
            context = null;
        }
        
        /// <summary>
        /// Unityエディタ終了時のコールバック
        /// </summary>
        private void OnEditorQuitting()
        {
            // Unity終了時に自動保存
            autoSaveHelper?.AutoSave(context?.FurDataManager);
        }

        /// <summary>
        /// ウィンドウタイトルを更新する（未保存変更がある場合は「*」を付加）
        /// </summary>
        private void UpdateWindowTitle()
        {
            const string baseTitle = "GroomingTool2";
            string expectedTitle = _hasUnsavedChanges ? $"{baseTitle} *" : baseTitle;
            
            if (titleContent.text != expectedTitle)
            {
                titleContent.text = expectedTitle;
            }
        }

        /// <summary>
        /// UIイベントを購読する
        /// </summary>
        private void SubscribeToUIEvents()
        {
            if (context?.UI == null || context?.Renderer == null || context?.MaterialManager == null)
                return;

            // マテリアルマネージャーのイベントを購読
            context.MaterialManager.OnMaterialSelected += OnMaterialSelected;

            context.UI.OnBlurModeChanged += OnBlurModeChanged;
            context.UI.OnPinchModeChanged += OnPinchModeChanged;
            context.UI.OnInclinedOnlyChanged += OnInclinedOnlyChanged;
            context.UI.OnDirOnlyChanged += OnDirOnlyChanged;
            context.UI.OnMirrorEnabledChanged += OnMirrorEnabledChanged;
            // OnGUI中にファイルダイアログ等を開くとレイアウト破綻しやすいため、次フレームに遅延実行
            context.UI.OnLoadFurData += () => { EditorApplication.delayCall += LoadFurData; };
            context.UI.OnSaveFurData += () => { EditorApplication.delayCall += SaveFurData; };
            context.UI.OnImportNormalMap += () => { EditorApplication.delayCall += ImportNormalMap; };
            context.UI.OnExportNormalMap += () => { EditorApplication.delayCall += ExportNormalMap; };
            context.UI.OnUndo += Undo;
            context.UI.OnRedo += Redo;
            
            // マスク変更時にキャッシュをクリア
            context.UI.OnMaskChanged += OnMaskChanged;
            
            // ワイヤフレーム色変更
            context.UI.OnWireframeColorChanged += OnWireframeColorChanged;
            
            // Scene編集トグル変更
            context.UI.OnSceneEditingEnabledChanged += OnSceneEditingEnabledChanged;
            
            // レンダリングモード変更
            context.UI.OnRenderingModeChanged += OnRenderingModeChanged;
            
            // 初期レンダリングモードを状態から反映
            if (context.State != null)
            {
                context.Renderer.SetRenderingMode(context.State.UseGpuRendering);
            }
        }
        
        private void OnWireframeColorChanged()
        {
            context?.Renderer?.SetWireframeColor(context.State.WireframeColor);
            Repaint();
        }
        
        private void OnSceneEditingEnabledChanged(bool enabled)
        {
            // Sceneビューを再描画して毛の表示を更新
            SceneView.RepaintAll();
        }
        
        private void OnMaskChanged()
        {
            context?.Renderer?.InvalidateMaskCache();
            Repaint();
        }
        
        private void OnRenderingModeChanged(bool useGpu)
        {
            context?.Renderer?.SetRenderingMode(useGpu);
            Repaint();
        }

        private void OnMaterialSelected(int materialIndex)
        {
            // 背景切替前に現在のデータを自動保存
            autoSaveHelper?.AutoSave(context?.FurDataManager);
            
            // マテリアル切替時にマスクをクリア（セッション限定のため）
            context?.MaskState?.Clear();
            
            context?.Renderer?.InvalidateWireframe();
            context?.EventHandler?.InvalidateUvRegionMaskCache();
            context?.Renderer?.InvalidateMaskCache();
            context?.UI?.NotifyMaskChanged();
            InitializeVertexSymmetryMapper();
            
            // 毛データも新しい背景用にリセット
            context?.FurDataManager?.ClearAllData();
            context?.UndoManager?.Clear();
            
            // 新しいキー情報を更新
            UpdateAutoSaveKeyInfo();
            
            // 自動読み込みを試みる
            autoSaveHelper.TryAutoLoad(context.FurDataManager);
            
            // 自動読み込みの成否に関わらず、現在の状態を初期状態として保存
            // （Undoのために必ず1つ目の状態が必要）
            context?.UndoManager?.SaveState(context.FurDataManager.Data, context.MaskState, "初期状態");
            _hasUnsavedChanges = false;
            
            // Sceneビューコントローラにマテリアル変更を通知
            context?.SceneViewController?.OnMaterialChanged();

            // UVマスクを先に構築して初回ストロークのラグを防ぐ
            PrewarmUvRegionMaskNextFrame();
            
            Repaint();
        }
        
        private void InitializeVertexSymmetryMapper()
        {
            if (context?.State?.Avatar == null || context?.VertexSymmetryMapper == null || context?.MaterialManager == null)
            {
                context?.VertexSymmetryMapper?.ClearCache();
                return;
            }
            
            var selectedMaterial = context.MaterialManager.SelectedMaterial;
            if (!selectedMaterial.HasValue)
            {
                context.VertexSymmetryMapper.ClearCache();
                return;
            }
            
            var material = selectedMaterial.Value.material;
            if (material == null)
            {
                Debug.LogWarning("[GroomingTool2] 選択されたマテリアルがnullです");
                context.VertexSymmetryMapper.ClearCache();
                return;
            }
            
            var texture = material.mainTexture as Texture2D;
            if (texture == null)
            {
                Debug.LogWarning($"[GroomingTool2] マテリアル {material.name} にメインテクスチャがありません");
                context.VertexSymmetryMapper.ClearCache();
                return;
            }
            
            context.VertexSymmetryMapper.Initialize(texture, context.State.Avatar);
            context.FurDataManager.SetVertexSymmetryMapper(context.VertexSymmetryMapper);
        }

        private void OnBlurModeChanged(bool value) => Repaint();
        private void OnPinchModeChanged(bool value) => Repaint();
        private void OnInclinedOnlyChanged(bool value) => Repaint();
        private void OnDirOnlyChanged(bool value) => Repaint();
        private void OnMirrorEnabledChanged(bool value)
        {
            Repaint();
        }

		private void OnGUI()
        {
            if (context?.UI == null || context?.State == null)
            {
                EditorGUILayout.HelpBox("初期化に失敗しました。ウィンドウを再起動してください。", MessageType.Error);
                return;
            }

            // ウィンドウタイトルに未保存マークを表示
            UpdateWindowTitle();

            // アバター変更を検知
            CheckAvatarChanged();

            // レイアウト計算
            var layout = WindowLayoutCalculator.Calculate(position.width, position.height);

            // 上メニュー描画
            GUILayout.BeginArea(layout.TopMenu);
            context.UI.DrawTopToolbar();
            GUILayout.EndArea();

            // 右上メニュー描画
            GUILayout.BeginArea(layout.RightTopMenu);
            context.UI.DrawRightTopMenu();
            GUILayout.EndArea();

            // 左メニュー描画
            GUILayout.BeginArea(layout.LeftMenu);
            context.UI.DrawToolbar();
            GUILayout.EndArea();

            // キャンバス描画
            DrawCanvasInRect(layout.Canvas);

            // 右メニュー描画
            GUILayout.BeginArea(layout.RightMenu);
            DrawRightMenu();
            GUILayout.EndArea();
        }

        /// <summary>
        /// 右メニューを描画する
        /// </summary>
        private void DrawRightMenu()
        {
            // 編集対象アバター + 背景セクション
            using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                // アバター選択セクション
                GUILayout.Label("アバター", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                EditorGUI.BeginChangeCheck();
                var newAvatar = (GameObject)EditorGUILayout.ObjectField(context.State.Avatar, typeof(GameObject), true);
                if (EditorGUI.EndChangeCheck())
                {
                    context.State.Avatar = newAvatar;
                    OnAvatarChanged();
                }

                if (context.State.Avatar == null)
                {
                    EditorGUILayout.Space(8);
                    EditorGUILayout.HelpBox("アバターを指定してください。", MessageType.Warning);
                }

                EditorGUILayout.Space(12);

                // 背景（マテリアル）選択セクション
                GUILayout.Label("背景", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                var materialEntries = context.MaterialManager?.MaterialEntries;
                if (context.State.Avatar != null && materialEntries != null && materialEntries.Count > 0)
                {
                    var entries = materialEntries.ToList();
                    var displayNames = entries.Select(e => e.displayName).ToArray();
                    int currentIndex = context.MaterialManager.SelectedMaterialIndex;

                    EditorGUI.BeginChangeCheck();
                    int newIndex = EditorGUILayout.Popup(currentIndex, displayNames);
                    if (EditorGUI.EndChangeCheck() && newIndex != currentIndex)
                    {
                        context.MaterialManager.SelectMaterial(newIndex);
                    }
                }
                else
                {
                    using (new EditorGUI.DisabledGroupScope(true))
                    {
                        EditorGUILayout.Popup(0, new[] { "（マテリアルなし）" });
                    }
                }
            }

            GUILayout.Space(4);

            // 自動設定セクション
            using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                GUILayout.Label("自動設定", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                // スライダーのテキストボックス幅を狭くする
                float oldFieldWidth = EditorGUIUtility.fieldWidth;
                EditorGUIUtility.fieldWidth = GroomingTool2Styles.SliderFieldWidth;

                GUILayout.Label(GroomingTool2Styles.AutoSetupSurfaceLiftLabel, EditorStyles.label);
                context.State.AutoSetupSurfaceLift = EditorGUILayout.Slider(context.State.AutoSetupSurfaceLift, 0f, 0.95f);

                EditorGUILayout.Space(4);

                GUILayout.Label(GroomingTool2Styles.AutoSetupRandomnessLabel, EditorStyles.label);
                context.State.AutoSetupRandomness = EditorGUILayout.Slider(context.State.AutoSetupRandomness, 0f, 0.5f);
                
                EditorGUIUtility.fieldWidth = oldFieldWidth;

                EditorGUILayout.Space(8);

                // 自動設定はHumanoidアバターでのみ実行可能
                var animator = context.State.Avatar?.GetComponent<Animator>();
                bool hasHumanoidAvatar = animator != null && animator.isHuman;
                bool canExecute = context.State.Avatar != null
                    && context.MaterialManager?.SelectedMaterial.HasValue == true
                    && hasHumanoidAvatar;
                using (new EditorGUI.DisabledGroupScope(!canExecute))
                {
                    if (GUILayout.Button("自動設定"))
                    {
                        EditorApplication.delayCall += ExecuteAutoSetup;
                    }
                }
            }
        }

        /// <summary>
        /// アバターの変更を検知する
        /// </summary>
        private void CheckAvatarChanged()
        {
            if (context?.State == null) return;

            if (previousAvatar != context.State.Avatar)
            {
                OnAvatarChanged();
            }
        }

        /// <summary>
        /// アバターが変更されたときの処理
        /// </summary>
        private void OnAvatarChanged()
        {
            if (context?.State == null) return;

            // アバター切替前に現在のデータを自動保存
            autoSaveHelper?.AutoSave(context?.FurDataManager);

            previousAvatar = context.State.Avatar;

            // マテリアルリストを再構築
            InitializeMaterialList();

            // アバター切替時にUV/マスク関連のキャッシュをクリアして前回の情報を表示しない
            context?.Renderer?.InvalidateWireframe();
            context?.EventHandler?.InvalidateUvRegionMaskCache();
            context?.MaskState?.Clear();
            context?.Renderer?.InvalidateMaskCache();
            context?.UI?.NotifyMaskChanged();

            // 毛データも新しいアバター用にリセット
            context?.FurDataManager?.ClearAllData();
            context?.UndoManager?.Clear();
            
            // 新しいキー情報を更新
            UpdateAutoSaveKeyInfo();
            
            // 自動読み込みを試みる
            autoSaveHelper.TryAutoLoad(context.FurDataManager);
            
            // 自動読み込みの成否に関わらず、現在の状態を初期状態として保存
            // （Undoのために必ず1つ目の状態が必要）
            context?.UndoManager?.SaveState(context.FurDataManager.Data, context.MaskState, "初期状態");
            _hasUnsavedChanges = false;

            // VertexSymmetryMapperを再初期化
            if (context.MaterialManager?.SelectedMaterial.HasValue == true)
            {
                InitializeVertexSymmetryMapper();
            }

            // Sceneビューコントローラにアバター変更を通知
            context.SceneViewController?.OnMaterialChanged();

            // UVマスクを先に構築して初回ストロークのラグを防ぐ
            PrewarmUvRegionMaskNextFrame();

            Repaint();
        }


        /// <summary>
        /// 指定されたRectにキャンバスを描画する（グリッドレイアウト用）
        /// </summary>
        private void DrawCanvasInRect(Rect viewRect)
        {
            if (context?.MaterialManager == null || context?.Renderer == null || context?.FileManager == null)
            {
                GUI.Box(viewRect, "レンダラーが初期化されていません。");
                return;
            }

            canvasViewRect = viewRect;

            // スライダー等でスケールが変更された場合は、ウィンドウ中心をピボットにscrollOffsetDataを再計算（Repaint時に実施）
            if (context.State.Scale != lastScale && Event.current.type == EventType.Repaint)
            {
                var desiredPivotView = new Vector2(viewRect.width * 0.5f, viewRect.height * 0.5f);
                // 旧スケールでのピボットに対応するデータ座標
                var pivotData = scrollOffsetData + desiredPivotView / Mathf.Max(lastScale, 1e-6f);

                // 新スケールでピボットを同じ画面位置に保つためのオフセット再計算
                var newOffsetData = pivotData - desiredPivotView / Mathf.Max(context.State.Scale, 1e-6f);
                // 表示範囲に収まるようクランプ
                scrollOffsetData = CoordinateUtils.ClampScrollOffsetData(new Vector2(viewRect.width, viewRect.height), context.State.Scale, Common.TexSize, newOffsetData);

                lastScale = context.State.Scale;
                context.EventHandler.LastScale = lastScale;
            }

            // 選択されたマテリアルを取得
            var selectedMaterial = context.MaterialManager.SelectedMaterial;
            Texture2D background = null;
            List<Vector2[]> uvSets = null;
            List<int[]> triangleSets = null;

            if (selectedMaterial.HasValue)
            {
                var entry = selectedMaterial.Value;
                background = entry.resizedTexture ?? entry.texture;
                uvSets = entry.uvSets;
                triangleSets = entry.triangleSets;
            }

            background ??= context.FileManager.GetBackground(context.State.Scale);

            // 固定キャンバス（スクロールビュー未使用）
            // ローカル座標（キャンバス内座標）に変換
            var localMousePos = Event.current.mousePosition - viewRect.position;
            bool isInPanMode = context.EventHandler.IsPanning || context.EventHandler.IsSpaceKeyHeld;
            bool shouldDrawBrushCursor = new Rect(0f, 0f, viewRect.width, viewRect.height).Contains(localMousePos) 
                                         && context.UI.CurrentMode != ToolMode.Mask
                                         && !isInPanMode;

            // イベントハンドラーの状態を更新
            context.EventHandler.ScrollOffsetData = scrollOffsetData;
            context.EventHandler.CanvasViewRect = canvasViewRect;
            context.EventHandler.LastScale = lastScale;

            // マスク選択プレビュー情報を取得
            bool maskPreviewActive = context.EventHandler.MaskSelectionActive && 
                                     (context.EventHandler.CurrentMaskSelectionMode == MaskSelectionMode.Rectangle || 
                                      context.EventHandler.CurrentMaskSelectionMode == MaskSelectionMode.Lasso);

            context.Renderer.SetWireframeColor(context.State.WireframeColor);
            var drawParams = new CanvasDrawParams
            {
				CanvasRect = viewRect,
                ViewRect = viewRect,
                Scale = context.State.Scale,
                DisplayInterval = context.State.DisplayInterval,
                Background = background,
                ScrollOffsetData = scrollOffsetData,
                MousePosContent = localMousePos,
                DrawBrushCursor = shouldDrawBrushCursor,
                Uv = null,
                Triangles = null,
                UvSets = uvSets,
                TriangleSets = triangleSets,
                MaskState = context.MaskState,
                ShowMaskDarkening = context.UI?.ShowMaskDarkening ?? false,
                WireframeColor = context.State.WireframeColor,
                MaskPreview = new MaskPreviewParams
                {
                    Active = maskPreviewActive,
                    Mode = context.EventHandler.CurrentMaskSelectionMode,
                    RectStartData = context.EventHandler.MaskSelectionStart,
                    RectEndData = context.EventHandler.MaskSelectionCurrent,
                    LassoPointsData = context.EventHandler.MaskLassoPoints != null ? new List<Vector2>(context.EventHandler.MaskLassoPoints) : null
                }
            };
            context.Renderer.DrawCanvas(drawParams);

			// 固定キャンバス用カスタムスクロールバー（毛データのみスクロール）
			{
				float visibleWData = viewRect.width / Mathf.Max(context.State.Scale, 1e-6f);
				float visibleHData = viewRect.height / Mathf.Max(context.State.Scale, 1e-6f);
				bool showH = Common.TexSize > visibleWData + 0.5f;
				bool showV = Common.TexSize > visibleHData + 0.5f;

				const float barSize = 14f;
				if (showH)
				{
					var hRect = new Rect(viewRect.x, viewRect.yMax - barSize, viewRect.width - (showV ? barSize : 0f), barSize);
					var newX = GUI.HorizontalScrollbar(hRect, scrollOffsetData.x, visibleWData, 0f, Common.TexSize);
					if (!Mathf.Approximately(newX, scrollOffsetData.x))
						scrollOffsetData.x = newX;
				}

				if (showV)
				{
					var vRect = new Rect(viewRect.xMax - barSize, viewRect.y, barSize, viewRect.height - (showH ? barSize : 0f));
					var newY = GUI.VerticalScrollbar(vRect, scrollOffsetData.y, visibleHData, 0f, Common.TexSize);
					if (!Mathf.Approximately(newY, scrollOffsetData.y))
						scrollOffsetData.y = newY;
				}

				// 念のためクランプ
				scrollOffsetData = CoordinateUtils.ClampScrollOffsetData(new Vector2(viewRect.width, viewRect.height), context.State.Scale, Common.TexSize, scrollOffsetData);
				// イベントハンドラーへ反映
				context.EventHandler.ScrollOffsetData = scrollOffsetData;
			}

			// 固定キャンバス内でイベント処理
			context.EventHandler.HandleEvents(viewRect, this);
			// ハンドラー側で更新されたオフセットを取り込み（次フレームで上書きされないよう保持）
			scrollOffsetData = context.EventHandler.ScrollOffsetData;

			// パン操作中またはスペースキー押下中はカーソルを手のひらに変更
			if (context.EventHandler.IsPanning || context.EventHandler.IsSpaceKeyHeld)
			{
				EditorGUIUtility.AddCursorRect(viewRect, MouseCursor.Pan);
			}

			canvasViewRect = viewRect;
        }

        /// <summary>
        /// 毛データを読み込む
        /// </summary>
        public void LoadFurData()
        {
            var path = context.FileManager.LoadFurDialog();
            if (!string.IsNullOrEmpty(path))
            {
                context.FileManager.LoadFurData(path, context.FurDataManager, null);
                context.MaskState?.Clear();
                context.UndoManager.Clear();
                SaveUndo("ファイル読み込み");
                _hasUnsavedChanges = false;
                Repaint();
            }
        }

        /// <summary>
        /// 毛データを保存する
        /// </summary>
        public void SaveFurData()
        {
            var path = context.FileManager.SaveFurDialog();
            if (!string.IsNullOrEmpty(path))
            {
                context.FileManager.SaveFurData(path, context.FurDataManager, null);
                _hasUnsavedChanges = false;
            }
        }

        /// <summary>
        /// ノーマルマップを出力する
        /// </summary>
        public void ExportNormalMap()
        {
            var path = context.FileManager.SaveNormalMapDialog();
            if (!string.IsNullOrEmpty(path))
            {
                context.Renderer.SaveNormalMap(path);
            }
        }

        /// <summary>
        /// ノーマルマップを読み込む
        /// </summary>
        public void ImportNormalMap()
        {
            var path = context.FileManager.LoadNormalMapDialog();
            if (!string.IsNullOrEmpty(path))
            {
                var texture = context.FileManager.LoadTextureReadable(path);
                if (texture != null)
                {
                    context.FurDataManager.LoadNormalMap(texture);
                    SaveUndo("ノーマル読込");
                    Repaint();
                }
            }
        }

        /// <summary>
        /// マテリアルリストを再検出する
        /// </summary>
        public void RedetectMaterials()
        {
            context.MaterialManager.RebuildMaterialList(context.State.Avatar);
            Repaint();
        }

        /// <summary>
        /// Undoを実行する
        /// </summary>
        public void Undo()
        {
            if (context.UndoManager.CanUndo)
            {
                RestoreUndoState(context.UndoManager.Undo());
            }
        }

        /// <summary>
        /// Redoを実行する
        /// </summary>
        public void Redo()
        {
            if (context.UndoManager.CanRedo)
            {
                RestoreUndoState(context.UndoManager.Redo());
            }
        }
        
        /// <summary>
        /// Undo/Redo状態を復元する（共通処理）
        /// </summary>
        private void RestoreUndoState(Managers.UndoState undoState)
        {
            if (undoState == null)
                return;
                
            context.FurDataManager.RestoreFromUndoState(undoState);
            
            // マスク状態も復元
            if (undoState.HasMaskState)
            {
                context.MaskState.RestoreBaseSelected(undoState.MaskBaseSelected);
                context.MaskState.RestrictEditing = undoState.MaskRestrictEditing;
                context.UI?.NotifyMaskChanged();
                context.UI?.NotifyMaskRestrictEditingChanged();
            }
            
            Repaint();
        }
        
        /// <summary>
        /// Undo状態を保存する（共通処理）
        /// </summary>
        private void SaveUndo(string description)
        {
            context.UndoManager.SaveState(context.FurDataManager.Data, context.MaskState, description);
            
            // 未保存フラグを設定（初期状態とファイル読み込み時以外）
            if (description != "初期状態" && description != "ファイル読み込み")
            {
                _hasUnsavedChanges = true;
            }
        }

        /// <summary>
        /// 自動保存用のキー情報（アバター名・テクスチャ名）を更新
        /// </summary>
        private void UpdateAutoSaveKeyInfo()
        {
            if (autoSaveHelper == null) return;
            
            string avatarName = context?.State?.Avatar?.name;
            string textureName = null;
            
            if (context?.MaterialManager?.SelectedMaterial.HasValue == true)
            {
                var entry = context.MaterialManager.SelectedMaterial.Value;
                textureName = entry.texture?.name ?? entry.displayName;
            }
            
            autoSaveHelper.UpdateKeyInfo(avatarName, textureName);
        }

        /// <summary>
        /// 自動設定を実行する
        /// </summary>
        private void ExecuteAutoSetup()
        {
            if (context?.State == null || context?.MaterialManager == null || context?.FurDataManager == null)
            {
                EditorUtility.DisplayDialog("エラー", "初期化が完了していません。", "OK");
                return;
            }

            var avatar = context.State.Avatar;
            var materialEntry = context.MaterialManager.SelectedMaterial;

            if (avatar == null || !materialEntry.HasValue)
            {
                EditorUtility.DisplayDialog("エラー", "アバターとマテリアルを選択してください。", "OK");
                return;
            }

            // 確認ダイアログ
            if (!EditorUtility.DisplayDialog(
                "自動設定",
                "現在の毛データを上書きして自動設定を実行しますか？",
                "実行",
                "キャンセル"))
            {
                return;
            }

            // 毛の傾きが0の場合はリセット処理
            if (Mathf.Approximately(context.State.AutoSetupSurfaceLift, 0f))
            {
                context.FurDataManager.ClearAllData();
                SaveUndo("自動設定");
                Repaint();
                return;
            }

            try
            {
                var autoSetupService = new AutoSetupService();
                bool success = autoSetupService.GenerateAndApplyAutoSetup(
                    avatar,
                    materialEntry,
                    context.State.AutoSetupSurfaceLift,
                    context.State.AutoSetupRandomness,
                    context.FurDataManager,
                    context.State.UvPadding);

                if (success)
                {
                    SaveUndo("自動設定");
                    Repaint();
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("エラー", $"自動設定中にエラーが発生しました:\n{ex.Message}", "OK");
                Debug.LogError($"自動設定エラー: {ex}");
            }
        }

        /// <summary>
        /// Stateのプロパティ変更を監視し、UVパディング変更時に事前計算を行う
        /// </summary>
        private void OnStatePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GroomingTool2State.UvPadding))
            {
                context?.EventHandler?.InvalidateUvRegionMaskCache();
                PrewarmUvRegionMaskNextFrame();
            }
        }

        /// <summary>
        /// UV領域マスクのプリウォームを次フレームで行う（OnGUIレイアウトを避ける）
        /// </summary>
        private void PrewarmUvRegionMaskNextFrame()
        {
            if (context?.EventHandler == null)
                return;

            EditorApplication.delayCall += () => context?.EventHandler?.PrewarmUvRegionMask();
        }
    }
}
