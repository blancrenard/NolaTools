using System;
using System.ComponentModel;
using System.Linq;
using GroomingTool2.Core;
using GroomingTool2.Managers;
using GroomingTool2.State;
using UnityEditor;
using UnityEngine;

namespace GroomingTool2.Core
{
    /// <summary>
    /// UI描画とイベント処理を担当するクラス
    /// </summary>
    internal sealed class GroomingTool2UI : IDisposable
    {
        private readonly GroomingTool2State state;
        private readonly GroomingTool2MaterialManager materialManager;
        private readonly BrushManager brushManager;
        private readonly FurDataManager furDataManager;
        private readonly FileManager fileManager;
        private readonly UndoManager undoManager;
        private readonly State.UvIslandMaskState maskState;
        private readonly MaskModeUI maskModeUI;

        // モード定義
        private static readonly GUIContent[] ModeContents = {
            GroomingTool2Styles.BrushModeLabel,
            GroomingTool2Styles.EraserModeLabel,
            GroomingTool2Styles.BlurModeLabel,
            GroomingTool2Styles.PinchModeLabel,
            GroomingTool2Styles.SpreadModeLabel,
            GroomingTool2Styles.MaskModeLabel
        };
        private static readonly ToolMode[] Modes = { ToolMode.Brush, ToolMode.Eraser, ToolMode.Blur, ToolMode.Pinch, ToolMode.Spread, ToolMode.Mask };

        // UI状態
        private ToolMode currentMode = ToolMode.Brush;
        private bool eraserMode;
        private bool blurMode;
        private bool pinchMode;
        private bool spreadMode;
        private bool inclinedOnly;
        private bool dirOnly;
        private bool mirrorEnabled;
        
        // メニューバー状態
        private Rect fileMenuRect;
        private Rect editMenuRect;
        private Rect viewMenuRect;
        private Rect settingsMenuRect;


        public bool EraserMode => eraserMode;
        public bool BlurMode => blurMode;
        public bool PinchMode => pinchMode;
        public bool SpreadMode => spreadMode;
        public bool InclinedOnly => inclinedOnly;
        public bool DirOnly => dirOnly;
        public bool MirrorEnabled => mirrorEnabled;
        public bool PinchInverted => spreadMode; // 拡散モードは内部的にはpinchInvertedとして動作

        public event Action<bool> OnEraserModeChanged;
        public event Action<bool> OnBlurModeChanged;
        public event Action<bool> OnPinchModeChanged;
        public event Action<bool> OnSpreadModeChanged;
        public event Action<bool> OnInclinedOnlyChanged;
        public event Action<bool> OnDirOnlyChanged;
        public event Action<bool> OnMirrorEnabledChanged;
        public event Action<bool> OnPinchInvertedChanged;

        // メインクラスの操作用イベント
        public event Action OnLoadFurData;
        public event Action OnSaveFurData;
        public event Action OnImportNormalMap;
        public event Action OnExportNormalMap;
        public event Action OnUndo;
        public event Action OnRedo;

        // マスク操作イベント
        public event Action OnMaskChanged;
        public event Action OnMaskRestrictEditingChanged;

        // 表示設定イベント
        public event Action OnWireframeColorChanged;
        
        // Scene編集変更イベント
        public event Action<bool> OnSceneEditingEnabledChanged;
        
        // レンダリングモード変更イベント
        public event Action<bool> OnRenderingModeChanged;

        public GroomingTool2UI(
            GroomingTool2State state,
            GroomingTool2MaterialManager materialManager,
            BrushManager brushManager,
            FurDataManager furDataManager,
            FileManager fileManager,
            UndoManager undoManager,
            State.UvIslandMaskState maskState)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.materialManager = materialManager ?? throw new ArgumentNullException(nameof(materialManager));
            this.brushManager = brushManager ?? throw new ArgumentNullException(nameof(brushManager));
            this.furDataManager = furDataManager ?? throw new ArgumentNullException(nameof(furDataManager));
            this.fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));
            this.undoManager = undoManager ?? throw new ArgumentNullException(nameof(undoManager));
            this.maskState = maskState ?? throw new ArgumentNullException(nameof(maskState));
            
            maskModeUI = new MaskModeUI(maskState, furDataManager, undoManager);

            // 状態変更の購読
            SubscribeToStateChanges();
        }

        /// <summary>
        /// 状態変更を購読する
        /// </summary>
        private void SubscribeToStateChanges()
        {
            state.PropertyChanged += OnStatePropertyChanged;
        }

        private void OnStatePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(GroomingTool2State.BrushSize):
                    brushManager.SetBrushSize(state.BrushSize);
                    break;
                case nameof(GroomingTool2State.BrushPower):
                    brushManager.SetBrushPower(state.BrushPower);
                    break;
                case nameof(GroomingTool2State.Inclined):
                    brushManager.SetMaxInclination(state.Inclined);
                    break;
            }
        }

        public void Dispose()
        {
            // 状態変更の購読を解除
            state.PropertyChanged -= OnStatePropertyChanged;
        }

        /// <summary>
        /// 左サイドメニュー（ツールバー形式のモード選択）を描画する
        /// </summary>
        public void DrawToolbar()
        {
            if (state == null)
            {
                EditorGUILayout.HelpBox("State の初期化に失敗しました。", MessageType.Error);
                if (GUILayout.Button("State を作成/再読み込み"))
                {
                    // この処理はメインクラスに委譲
                }
                return;
            }

            // Scene編集トグル（ブラシの上に配置）
            using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                if (DrawToggleButton(GroomingTool2Styles.SceneEditingLabel, state.SceneEditingEnabled))
                {
                    bool newSceneEditingEnabled = !state.SceneEditingEnabled;
                    state.SceneEditingEnabled = newSceneEditingEnabled;
                    OnSceneEditingEnabledChanged?.Invoke(newSceneEditingEnabled);
                }
            }

            GUILayout.Space(4);

            using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                // モード選択ツールバー（縦並び）
                for (int i = 0; i < ModeContents.Length; i++)
                {
                    bool isSelected = currentMode == Modes[i];
                    if (DrawToggleButton(ModeContents[i], isSelected))
                    {
                        ToolMode newMode = Modes[i];
                        if (newMode != currentMode)
                        {
                            currentMode = newMode;
                            UpdateModeStates();
                        }
                    }
                }
            }
        }

        // 色だけを変更しつつ角丸スタイルを維持してボタンを描画する
        private bool DrawToggleButton(string label, bool isSelected, params GUILayoutOption[] options)
        {
            return DrawToggleButton(new GUIContent(label), isSelected, options);
        }

        // GUIContent版（ツールチップ対応）
        private bool DrawToggleButton(GUIContent content, bool isSelected, params GUILayoutOption[] options)
        {
            var oldBg = GUI.backgroundColor;
            var oldContent = GUI.contentColor;
            if (isSelected)
            {
                GUI.backgroundColor = new Color(0.2f, 0.4f, 0.8f, 1f);
                GUI.contentColor = Color.white;
            }
            bool clicked = GUILayout.Button(content, EditorStyles.miniButton, options);
            GUI.backgroundColor = oldBg;
            GUI.contentColor = oldContent;
            return clicked;
        }

        /// <summary>
        /// モード切り替え時に状態を更新する
        /// </summary>
        private void UpdateModeStates()
        {
            bool oldEraserMode = eraserMode;
            bool oldBlurMode = blurMode;
            bool oldPinchMode = pinchMode;
            bool oldSpreadMode = spreadMode;

            // 状態をリセット
            eraserMode = currentMode == ToolMode.Eraser;
            blurMode = currentMode == ToolMode.Blur;
            pinchMode = currentMode == ToolMode.Pinch || currentMode == ToolMode.Spread; // 拡散も内部的にはpinchMode
            spreadMode = currentMode == ToolMode.Spread;
            inclinedOnly = false;
            dirOnly = false;

            // 状態変更イベントを発火
            if (oldEraserMode != eraserMode)
                OnEraserModeChanged?.Invoke(eraserMode);
            if (oldBlurMode != blurMode)
                OnBlurModeChanged?.Invoke(blurMode);
            if (oldPinchMode != pinchMode)
                OnPinchModeChanged?.Invoke(pinchMode);
            if (oldSpreadMode != spreadMode)
            {
                OnSpreadModeChanged?.Invoke(spreadMode);
                OnPinchInvertedChanged?.Invoke(spreadMode);
            }
            OnInclinedOnlyChanged?.Invoke(inclinedOnly);
            OnDirOnlyChanged?.Invoke(dirOnly);
        }

        /// <summary>
        /// 上メニュー（トップツールバー）を描画する
        /// </summary>
        public void DrawTopToolbar()
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                // 1行目: メニューバー
                using (new GUILayout.HorizontalScope())
                {
                    DrawMenuBar();
                }

                // 2行目: 選択されたモード専用のパラメータ
                using (new GUILayout.HorizontalScope())
                {
                    DrawModeSpecificParameters();
                }
            }
        }

        /// <summary>
        /// 右上メニュー（拡大縮小）を描画する
        /// </summary>
        public void DrawRightTopMenu()
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                GUILayout.Label(GroomingTool2Styles.ScaleLabel);
                
                using (new GUILayout.HorizontalScope())
                {
                    // スライダーのテキストボックス幅を狭くする
                    float oldFieldWidth = EditorGUIUtility.fieldWidth;
                    EditorGUIUtility.fieldWidth = GroomingTool2Styles.SliderFieldWidth;
                    
                    // スライダーで25%刻みに変更
                    EditorGUI.BeginChangeCheck();
                    float newScale = EditorGUILayout.Slider(state.Scale, 0.25f, 4f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        // 25%刻みにスナップ
                        state.Scale = Mathf.Round(newScale * 4f) / 4f;
                    }
                    
                    EditorGUIUtility.fieldWidth = oldFieldWidth;
                }
            }
        }

        /// <summary>
        /// メニューバーを描画する
        /// </summary>
        private void DrawMenuBar()
        {
            // ファイルメニュー
            if (GUILayout.Button("ファイル", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                ShowFileMenu();
            }
            if (Event.current.type == EventType.Repaint)
            {
                fileMenuRect = GUILayoutUtility.GetLastRect();
            }

            // 編集メニュー
            if (GUILayout.Button("編集", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                ShowEditMenu();
            }
            if (Event.current.type == EventType.Repaint)
            {
                editMenuRect = GUILayoutUtility.GetLastRect();
            }

            // 表示メニュー
            if (GUILayout.Button("表示", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                ShowViewMenu();
            }
            if (Event.current.type == EventType.Repaint)
            {
                viewMenuRect = GUILayoutUtility.GetLastRect();
            }

            // 設定メニュー
            if (GUILayout.Button("設定", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                ShowSettingsMenu();
            }
            if (Event.current.type == EventType.Repaint)
            {
                settingsMenuRect = GUILayoutUtility.GetLastRect();
            }
        }

        /// <summary>
        /// ファイルメニューを表示する
        /// </summary>
        private void ShowFileMenu()
        {
            var menu = new GenericMenu();
            
            menu.AddItem(new GUIContent("毛データ読込"), false, () => OnLoadFurData?.Invoke());
            menu.AddItem(new GUIContent("ノーマル読込"), false, () => OnImportNormalMap?.Invoke());
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("毛データ保存"), false, () => OnSaveFurData?.Invoke());
            menu.AddItem(new GUIContent("ノーマル保存"), false, () => OnExportNormalMap?.Invoke());
            
            menu.DropDown(fileMenuRect);
        }

        /// <summary>
        /// 編集メニューを表示する
        /// </summary>
        private void ShowEditMenu()
        {
            var menu = new GenericMenu();
            
            if (undoManager.CanUndo)
            {
                menu.AddItem(new GUIContent("元に戻す（Undo）"), false, () => OnUndo?.Invoke());
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("元に戻す（Undo）"));
            }
            
            if (undoManager.CanRedo)
            {
                menu.AddItem(new GUIContent("やり直し（Redo）"), false, () => OnRedo?.Invoke());
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("やり直し（Redo）"));
            }
            
            menu.DropDown(editMenuRect);
        }

        /// <summary>
        /// 表示メニューを表示する
        /// </summary>
        private void ShowViewMenu()
        {
            var menu = new GenericMenu();
            
            // UVの色サブメニュー
            var uvColorOptions = new (string name, Color color)[]
            {
                ("白", new Color(1f, 1f, 1f, 0.3f)),
                ("赤", new Color(1f, 0f, 0f, 0.3f)),
                ("緑", new Color(0f, 1f, 0f, 0.3f)),
                ("青", new Color(0f, 0f, 1f, 0.3f)),
                ("黒", new Color(0f, 0f, 0f, 0.3f)),
            };
            foreach (var (name, color) in uvColorOptions)
            {
                // アルファを除いたRGB成分で比較
                bool isSelected = Mathf.Approximately(state.WireframeColor.r, color.r) &&
                                  Mathf.Approximately(state.WireframeColor.g, color.g) &&
                                  Mathf.Approximately(state.WireframeColor.b, color.b);
                menu.AddItem(new GUIContent($"UVの色/{name}"), isSelected, () =>
                {
                    state.WireframeColor = color;
                    OnWireframeColorChanged?.Invoke();
                });
            }
            
            // ドット間隔サブメニュー
            int[] intervals = { 12, 16, 24, 32 };
            foreach (int interval in intervals)
            {
                bool isSelected = state.DisplayInterval == interval;
                menu.AddItem(new GUIContent($"ドット間隔/{interval}"), isSelected, () => state.DisplayInterval = interval);
            }
            
            // Scene毛の色サブメニュー
            var hairColorOptions = new (string name, Color color)[]
            {
                ("白", Color.white),
                ("赤", Color.red),
                ("緑", Color.green),
                ("青", Color.blue),
                ("黒", Color.black),
            };
            foreach (var (name, color) in hairColorOptions)
            {
                bool isSelected = state.SceneViewHairColor == color;
                menu.AddItem(new GUIContent($"Scene毛の色/{name}"), isSelected, () =>
                {
                    state.SceneViewHairColor = color;
                    SceneView.RepaintAll();
                });
            }
            
            // Scene毛密度サブメニュー
            var sceneViewDensityOptions = new (string name, int interval)[]
            {
                ("高密度", 1),
                ("低密度", 2),
            };
            foreach (var (name, interval) in sceneViewDensityOptions)
            {
                bool isSelected = state.SceneViewDisplayInterval == interval;
                menu.AddItem(new GUIContent($"Scene毛密度/{name}"), isSelected, () =>
                {
                    state.SceneViewDisplayInterval = interval;
                    SceneView.RepaintAll(); // Sceneビューを再描画してサンプルポイントを再構築
                });
            }
            
            menu.DropDown(viewMenuRect);
        }

        /// <summary>
        /// 設定メニューを表示する
        /// </summary>
        private void ShowSettingsMenu()
        {
            var menu = new GenericMenu();
            
            // マスクを有効にする（一旦非表示）
            // menu.AddItem(new GUIContent("マスクを有効にする"), maskState.RestrictEditing, () =>
            // {
            //     bool newRestrictEditing = !maskState.RestrictEditing;
            //     undoManager.SaveState(furDataManager.Data, maskState, "マスク有効/無効切替");
            //     maskState.RestrictEditing = newRestrictEditing;
            //     OnMaskRestrictEditingChanged?.Invoke();
            // });
            // 
            // menu.AddSeparator("");

            // レンダリングモードサブメニュー
            menu.AddItem(new GUIContent("レンダリング/GPU（高速）"), state.UseGpuRendering, () =>
            {
                if (!state.UseGpuRendering)
                {
                    state.UseGpuRendering = true;
                    OnRenderingModeChanged?.Invoke(true);
                }
            });
            menu.AddItem(new GUIContent("レンダリング/CPU（互換性重視）"), !state.UseGpuRendering, () =>
            {
                if (state.UseGpuRendering)
                {
                    state.UseGpuRendering = false;
                    OnRenderingModeChanged?.Invoke(false);
                }
            });
            

            // UV内のみ編集する
            menu.AddItem(new GUIContent("UV内のみ編集する"), state.RestrictEditToUvRegion, () =>
            {
                state.RestrictEditToUvRegion = !state.RestrictEditToUvRegion;
            });

            // UVパディングサブメニュー
            int[] paddingValues = { 0, 2, 4, 8, 16, 32 };
            foreach (int padding in paddingValues)
            {
                bool isSelected = state.UvPadding == padding;
                menu.AddItem(new GUIContent($"UVパディング/{padding}px"), isSelected, () =>
                {
                    state.UvPadding = padding;
                });
            }
            
            menu.DropDown(settingsMenuRect);
        }

        /// <summary>
        /// 選択されたモードに応じた専用パラメータを描画する
        /// </summary>
        private void DrawModeSpecificParameters()
        {
            // スライダーのテキストボックス幅を狭くする
            float oldFieldWidth = EditorGUIUtility.fieldWidth;
            EditorGUIUtility.fieldWidth = GroomingTool2Styles.SliderFieldWidth;
            
            switch (currentMode)
            {
                case ToolMode.Brush:
                    DrawBrushModeParameters();
                    break;
                case ToolMode.Eraser:
                    DrawEraserModeParameters();
                    break;
                case ToolMode.Blur:
                    DrawBlurModeParameters();
                    break;
                case ToolMode.Pinch:
                    DrawPinchModeParameters();
                    break;
                case ToolMode.Spread:
                    DrawSpreadModeParameters();
                    break;
                case ToolMode.Mask:
                    maskModeUI.DrawParameters(() => OnMaskChanged?.Invoke());
                    break;
            }
            
            EditorGUIUtility.fieldWidth = oldFieldWidth;
        }

        /// <summary>
        /// ブラシサイズと強さの描画（共通処理）
        /// </summary>
        private void DrawBrushSizeAndPower()
        {
            GUILayout.Label(GroomingTool2Styles.BrushSizeLabel, GroomingTool2Styles.LabelWidth);
            state.BrushSize = Mathf.RoundToInt(EditorGUILayout.Slider(state.BrushSize, 1, 40, GroomingTool2Styles.SliderWidth));

            GUILayout.Space(GroomingTool2Styles.Spacing);

            GUILayout.Label(GroomingTool2Styles.BrushPowerLabel, GroomingTool2Styles.MediumLabelWidth);
            state.BrushPower = EditorGUILayout.Slider(state.BrushPower, 0f, 1f, GroomingTool2Styles.SliderWidth);
            // 状態変更はOnStatePropertyChangedで処理されるため、ここでの設定は不要
        }

        /// <summary>
        /// 向きのみ変更と傾きのみ変更のトグル描画（共通処理）
        /// </summary>
        /// <param name="showInclinedToggle">傾きのみ変更トグルを表示するか</param>
        /// <param name="enableExclusiveBehavior">inclinedOnlyとの排他処理を有効化するか</param>
        private void DrawDirectionToggles(bool showInclinedToggle, bool enableExclusiveBehavior = true)
        {
            if (DrawToggleButton(GroomingTool2Styles.DirectionOnlyLabel, dirOnly, GroomingTool2Styles.ToggleWidth))
            {
                bool newDirOnly = !dirOnly;
                if (enableExclusiveBehavior && newDirOnly && inclinedOnly)
                {
                    inclinedOnly = false;
                    OnInclinedOnlyChanged?.Invoke(inclinedOnly);
                }
                dirOnly = newDirOnly;
                OnDirOnlyChanged?.Invoke(dirOnly);
            }

            if (showInclinedToggle)
            {
                GUILayout.Space(GroomingTool2Styles.SmallSpacing);

                if (DrawToggleButton(GroomingTool2Styles.InclinedOnlyLabel, inclinedOnly, GroomingTool2Styles.ToggleWidth))
                {
                    bool newInclinedOnly = !inclinedOnly;
                    if (enableExclusiveBehavior && newInclinedOnly && dirOnly)
                    {
                        dirOnly = false;
                        OnDirOnlyChanged?.Invoke(dirOnly);
                    }
                    inclinedOnly = newInclinedOnly;
                    OnInclinedOnlyChanged?.Invoke(inclinedOnly);
                }
            }
        }

        /// <summary>
        /// ミラートグルの描画（共通処理）
        /// </summary>
        private void DrawMirrorToggle()
        {
            bool hasMaterial = materialManager.SelectedMaterial.HasValue;
            using (new EditorGUI.DisabledGroupScope(!hasMaterial))
            {
                if (DrawToggleButton(GroomingTool2Styles.MirrorLabel, mirrorEnabled, GroomingTool2Styles.NarrowLabelWidth))
                {
                    mirrorEnabled = !mirrorEnabled;
                    OnMirrorEnabledChanged?.Invoke(mirrorEnabled);
                }
            }
        }

        /// <summary>
        /// ブラシモードのパラメータを描画する
        /// </summary>
        private void DrawBrushModeParameters()
        {
            DrawBrushToolParameters(showInclinedSlider: true, showInclinedToggle: true);
        }

        /// <summary>
        /// 消しゴムモードのパラメータを描画する
        /// </summary>
        private void DrawEraserModeParameters()
        {
            // 消しゴムモードでは毛の傾き・向きのみ変更・傾きのみ変更を非表示
            DrawBrushSizeAndPower();

            GUILayout.Space(GroomingTool2Styles.Spacing);

            DrawMirrorToggle();
        }

        /// <summary>
        /// ぼかしモードのパラメータを描画する
        /// </summary>
        private void DrawBlurModeParameters()
        {
            DrawBrushSizeAndPower();

            GUILayout.Space(GroomingTool2Styles.Spacing);

            DrawDirectionToggles(showInclinedToggle: true);

            GUILayout.Space(GroomingTool2Styles.Spacing);

            DrawMirrorToggle();
        }

        /// <summary>
        /// つまむモードのパラメータを描画する
        /// </summary>
        private void DrawPinchModeParameters()
        {
            DrawBrushToolParameters(showInclinedSlider: true, showInclinedToggle: false);
        }

        /// <summary>
        /// 拡散モードのパラメータを描画する
        /// </summary>
        private void DrawSpreadModeParameters()
        {
            DrawBrushToolParameters(showInclinedSlider: true, showInclinedToggle: false);
        }

        /// <summary>
        /// ブラシ系ツールの共通パラメータ描画
        /// </summary>
        /// <param name="showInclinedSlider">傾きスライダーを表示するか</param>
        /// <param name="showInclinedToggle">傾きのみ変更トグルを表示するか</param>
        private void DrawBrushToolParameters(bool showInclinedSlider, bool showInclinedToggle)
        {
            if (showInclinedSlider)
            {
                GUILayout.Label(GroomingTool2Styles.InclinedLabel, GroomingTool2Styles.NarrowLabelWidth);
                state.Inclined = EditorGUILayout.Slider(state.Inclined, 0f, 0.95f, GroomingTool2Styles.SliderWidth);

                GUILayout.Space(GroomingTool2Styles.Spacing);
            }

            DrawBrushSizeAndPower();

            GUILayout.Space(GroomingTool2Styles.Spacing);

            DrawDirectionToggles(showInclinedToggle: showInclinedToggle, enableExclusiveBehavior: showInclinedToggle);

            GUILayout.Space(GroomingTool2Styles.Spacing);

            DrawMirrorToggle();
        }

        public ToolMode CurrentMode => currentMode;
        public MaskSelectionMode MaskSelectionMode => maskModeUI.SelectionMode;
		public bool ShowMaskDarkening => maskState.RestrictEditing;


        /// <summary>
        /// マスク変更イベントを発火する（外部から呼び出し用）
        /// </summary>
        public void NotifyMaskChanged()
        {
            OnMaskChanged?.Invoke();
        }

        /// <summary>
        /// マスク制限編集変更イベントを発火する（外部から呼び出し用）
        /// </summary>
        public void NotifyMaskRestrictEditingChanged()
        {
            OnMaskRestrictEditingChanged?.Invoke();
        }
    }
}
