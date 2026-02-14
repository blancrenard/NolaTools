#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Constants;
using NolaTools.FurMaskGenerator.Data;

namespace NolaTools.FurMaskGenerator.UI
{
    public partial class TexturePreviewWindow : EditorWindow
    {
        private static readonly List<TexturePreviewWindow> _openWindows = new List<TexturePreviewWindow>();

        // 複数ターゲット切替用ターゲット情報
        private class Target
        {
            public Renderer Renderer;
            public int SubmeshIndex;
            public Texture2D Texture;
            public string Label;
        }

        private Texture2D texture;
        private Vector2 scrollPosition;
        private float zoom = AppSettings.DEFAULT_SCALE;
        
        // UVマスク可視化用フィールド
        private List<UVIslandMaskData> uvMasks;
        private Renderer targetRenderer;
        private int submeshIndex;
        private bool showUVMasks = true;
        private Texture2D overlayTexture;
        private bool addUVMasksOnPreview = true;
        private System.Action<UVIslandMaskData> onAddMaskCallback;
        private System.Action<UVIslandMaskData> onRemoveMaskCallback;

        // UVワイヤーフレーム表示用
        private bool showUVWireframe = true;
        private Rect lastTextureRect;
        private Rect lastCanvasRect;

        // 初回のみウィンドウにフィットさせ、その後は絶対倍率で運用
        private bool initializedToFit = false;
        private float initialFitZoom = AppSettings.DEFAULT_SCALE;

        // マウス追従十字の表示切替（UV編集モードのみ有効）
        private bool showMouseCrosshair;
        
        // 複数ターゲット切替
        private List<Target> targets;
        private int currentTargetIndex = 0;

        // GC削減用の一時リスト
        private readonly List<Texture2D> _tmpUniqueTextures = new List<Texture2D>();
        private readonly List<string> _tmpTextureNames = new List<string>();
        private readonly HashSet<int> _tmpSeen = new HashSet<int>();

        // 遅延ズーム用の状態
        private bool deferredScrollZoom;
        private float deferredScrollDelta;
        private Vector2 deferredMousePos;
        private bool deferredSliderChanged;
        private float deferredSliderValue;
        
        public static void ShowWindow(Texture2D texture)
        {
            TexturePreviewWindow window = GetWindow<TexturePreviewWindow>(string.Format(UILabels.TEXTURE_PREVIEW_TITLE_FORMAT, texture.name));
            window.texture = texture;

            // UVマスク・ワイヤーフレーム表示フラグを初期化（通常プレビュー用）
            window.showUVMasks = false;
            window.showUVWireframe = false;
            window.overlayTexture = null;

            // UVマスク編集用フィールドをクリア
            window.targets = null;
            window.uvMasks = null;
            window.targetRenderer = null;
            window.submeshIndex = 0;
            window.onAddMaskCallback = null;
            window.onRemoveMaskCallback = null;

            window.minSize = new Vector2(AppSettings.MIN_WINDOW_WIDTH, AppSettings.MIN_WINDOW_HEIGHT);
            window.showMouseCrosshair = false;
            window.initializedToFit = false;
            window.initialFitZoom = AppSettings.DEFAULT_SCALE;
            window.Show();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            if (!_openWindows.Contains(this)) _openWindows.Add(this);
            EnsureGLMaterial();

            // ウィンドウが再利用された場合の初期化（念のため）
            if (texture != null && targets == null && uvMasks == null)
            {
                showUVMasks = false;
                showUVWireframe = false;
                overlayTexture = null;
            }
        }

        private void OnDisable()
        {
            _openWindows.Remove(this);
        }

        // シーン側からUVマスク変更を通知
        public static void NotifyUVMasksChanged()
        {
            if (_openWindows == null || _openWindows.Count == 0) return;
            foreach (var w in _openWindows)
            {
                if (w == null) continue;
                if (w.showUVMasks)
                {
                    w.GenerateOverlayTexture();
                }
                w.Repaint();
            }
        }

        /// <summary>
        /// 複数ターゲットを切り替え可能なプレビューを表示
        /// </summary>
        public static void ShowWindowWithTargets(
            List<UVIslandMaskData> uvMasks,
            List<(Renderer renderer, int submesh, Texture2D texture, string label)> multiTargets,
            int initialIndex,
            System.Action<UVIslandMaskData> onAddMask,
            System.Action<UVIslandMaskData> onRemoveMask)
        {
            if (multiTargets == null || multiTargets.Count == 0) return;
            int idx = Mathf.Clamp(initialIndex, 0, multiTargets.Count - 1);
            var t = multiTargets[idx];
            var window = GetWindow<TexturePreviewWindow>(string.Format(UILabels.TEXTURE_PREVIEW_TITLE_FORMAT, (t.texture != null ? t.texture.name : "UV") + " (UV Masks)"));
            window.targets = new List<Target>();
            foreach (var mt in multiTargets)
            {
                window.targets.Add(new Target
                {
                    Renderer = mt.renderer,
                    SubmeshIndex = mt.submesh,
                    Texture = mt.texture,
                    Label = string.IsNullOrEmpty(mt.label) ? null : mt.label
                });
            }
            window.currentTargetIndex = idx;
            window.texture = t.texture;
            window.uvMasks = uvMasks;
            window.targetRenderer = t.renderer;
            window.submeshIndex = t.submesh;
            window.onAddMaskCallback = onAddMask;
            window.onRemoveMaskCallback = onRemoveMask;

            // UVマスク編集モードではデフォルトで表示ON
            window.showUVMasks = true;
            window.showUVWireframe = true;

            window.showMouseCrosshair = true;
            window.minSize = new Vector2(AppSettings.MIN_WINDOW_WIDTH, AppSettings.MIN_WINDOW_HEIGHT);
            window.GenerateOverlayTexture();
            window.EnsureGLMaterial();
            window.Show();
        }

        /// <summary>
        /// テクスチャをウィンドウサイズにフィットさせる倍率を計算する
        /// </summary>
        private float CalculateFitZoom(Texture2D tex, Rect windowPosition)
        {
            if (tex == null) return -1f;

            float availableHeight = windowPosition.height - AppSettings.TOOLBAR_HEIGHT - AppSettings.INFO_HEIGHT;
            float availableWidth = windowPosition.width - AppSettings.SCROLLBAR_WIDTH;
            float textureAspect = (float)tex.width / tex.height;
            float availableAspect = availableWidth / availableHeight;

            float fitZoom = textureAspect > availableAspect
                ? availableWidth / tex.width
                : availableHeight / tex.height;

            if (fitZoom > AppSettings.FIT_EPSILON && !float.IsNaN(fitZoom) && !float.IsInfinity(fitZoom))
            {
                return Mathf.Clamp(fitZoom, AppSettings.MIN_ZOOM, AppSettings.MAX_ZOOM);
            }
            return -1f;
        }
        
        private void OnGUI()
        {
            // キー・マウスによるウィンドウ制御
            if (HandleWindowCloseEvents()) return;

            if (texture == null)
            {
                EditorGUILayout.HelpBox(UILabels.TEXTURE_PREVIEW_EMPTY, MessageType.Error);
                return;
            }
            
            // マウス移動/ドラッグ時に再描画（クロスヘア追従用）
            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)
            {
                Repaint();
            }

            // 入力イベント処理（ズーム・スクロール・パン）
            HandleInputEvents();
            
            // ツールバー描画
            DrawToolbar();
            
            // 初回フィット処理
            EnsureInitialFit();

            // キャンバス描画（テクスチャ、オーバーレイ、ワイヤーフレーム、クロスヘア、クリック処理）
            DrawCanvasArea();

            // 遅延ズーム適用
            ApplyDeferredZoom();

            // 情報表示バー
            DrawInfoBar();
        }

        #region ウィンドウ制御

        /// <summary>
        /// Escape/右クリックによるウィンドウ閉じ処理
        /// </summary>
        /// <returns>ウィンドウが閉じられた場合true</returns>
        private bool HandleWindowCloseEvents()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Close();
                Event.current.Use();
                return true;
            }
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1)
            {
                Close();
                Event.current.Use();
                return true;
            }
            return false;
        }

        #endregion

        #region 入力イベント

        /// <summary>
        /// スクロールホイール・パン操作のイベント処理
        /// ズームはテクスチャ描画後に遅延適用するため、フラグのみ設定する
        /// </summary>
        private void HandleInputEvents()
        {
            // 遅延ズームフラグをリセット
            deferredScrollZoom = false;
            deferredScrollDelta = 0f;
            deferredMousePos = Vector2.zero;

            if (Event.current.type == EventType.ScrollWheel)
            {
                if (Event.current.control || Event.current.command)
                {
                    // Ctrl+ホイール: ズーム（描画後に遅延適用）
                    deferredScrollZoom = true;
                    deferredScrollDelta = Event.current.delta.y;
                    deferredMousePos = Event.current.mousePosition;
                    Event.current.Use();
                }
                else
                {
                    HandleScrollWheel();
                }
            }

            // 中ボタンドラッグでパン操作
            if (Event.current.type == EventType.MouseDrag && Event.current.button == 2)
            {
                scrollPosition -= Event.current.delta;
                Event.current.Use();
                Repaint();
            }
        }

        /// <summary>
        /// 通常スクロールホイール処理（Ctrl無し）
        /// Shift押下時またはdelta.xが大きい場合は横スクロール
        /// </summary>
        private void HandleScrollWheel()
        {
            Vector2 delta = Event.current.delta;
            bool useHorizontal = Event.current.shift || (Mathf.Abs(delta.x) > Mathf.Abs(delta.y));

            if (useHorizontal)
            {
                float d = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? delta.x : delta.y;
                scrollPosition.x += d * AppSettings.SCROLL_WHEEL_SPEED;
            }
            else
            {
                scrollPosition.y += delta.y * AppSettings.SCROLL_WHEEL_SPEED;
            }
            Event.current.Use();
            Repaint();
        }

        #endregion

        #region ツールバー

        /// <summary>
        /// ツールバーUI（テクスチャ選択、トグルボタン、ズームコントロール）を描画
        /// </summary>
        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 左側: テクスチャ選択
            DrawTextureSelector();

            GUILayout.FlexibleSpace();

            // 複数ターゲットモードのトグルボタン
            DrawToggleButtons();

            // ズームコントロール
            DrawZoomControls();

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// テクスチャ選択ドロップダウンを描画
        /// </summary>
        private void DrawTextureSelector()
        {
            if (targets != null && targets.Count > 0)
            {
                // 重複排除したテクスチャ一覧を作成
                _tmpUniqueTextures.Clear();
                _tmpTextureNames.Clear();
                _tmpSeen.Clear();
                for (int i = 0; i < targets.Count; i++)
                {
                    var tex = targets[i].Texture;
                    if (tex == null) continue;
                    int id = tex.GetInstanceID();
                    if (_tmpSeen.Add(id))
                    {
                        _tmpUniqueTextures.Add(tex);
                        _tmpTextureNames.Add(tex.name);
                    }
                }

                // 現在のテクスチャに対応する選択インデックスを決定
                int currentTextureIndex = 0;
                for (int i = 0; i < _tmpUniqueTextures.Count; i++)
                {
                    if (_tmpUniqueTextures[i] == texture)
                    {
                        currentTextureIndex = i;
                        break;
                    }
                }

                int newTextureIndex = EditorGUILayout.Popup(currentTextureIndex, _tmpTextureNames.ToArray(), GUILayout.MaxWidth(300));
                if (newTextureIndex != currentTextureIndex && newTextureIndex >= 0 && newTextureIndex < _tmpUniqueTextures.Count)
                {
                    Texture2D selectedTex = _tmpUniqueTextures[newTextureIndex];
                    int targetIndex = -1;
                    for (int i = 0; i < targets.Count; i++)
                    {
                        if (targets[i].Texture == selectedTex)
                        {
                            targetIndex = i;
                            break;
                        }
                    }
                    if (targetIndex >= 0)
                    {
                        SwitchTarget(targetIndex);
                    }
                }
            }
            else
            {
                GUILayout.Label(texture.name, EditorStyles.toolbarButton);
            }
        }

        /// <summary>
        /// UVマスク・ワイヤーフレーム表示トグルを描画
        /// </summary>
        private void DrawToggleButtons()
        {
            if (targets == null || targets.Count <= 0) return;

            // UVマスク表示トグル
            bool newShowUVMasks = GUILayout.Toggle(showUVMasks, UILabels.UV_MASK_TOGGLE, EditorStyles.toolbarButton);
            if (newShowUVMasks != showUVMasks)
            {
                showUVMasks = newShowUVMasks;
                if (showUVMasks) GenerateOverlayTexture();
                else ClearOverlayTexture();
            }

            // UVワイヤーフレーム表示トグル
            bool newShowWire = GUILayout.Toggle(showUVWireframe, UILabels.UV_WIREFRAME_TOGGLE, EditorStyles.toolbarButton);
            if (newShowWire != showUVWireframe)
            {
                showUVWireframe = newShowWire;
                Repaint();
            }
        }

        /// <summary>
        /// ズームスライダー・数値表示・リセットボタンを描画
        /// </summary>
        private void DrawZoomControls()
        {
            GUILayout.Label(UILabels.ZOOM_LABEL, EditorStyles.toolbarButton);

            deferredSliderChanged = false;
            deferredSliderValue = zoom;

            EditorGUI.BeginChangeCheck();
            float newSliderZoom = GUILayout.HorizontalSlider(zoom, AppSettings.MIN_ZOOM, AppSettings.MAX_ZOOM, GUILayout.Width(100));
            if (EditorGUI.EndChangeCheck())
            {
                deferredSliderChanged = true;
                deferredSliderValue = newSliderZoom;
            }
            
            GUILayout.Label($"{zoom:F2}x", EditorStyles.toolbarButton, GUILayout.Width(40));

            if (GUILayout.Button(UILabels.RESET_BUTTON, EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                float fitZoom = CalculateFitZoom(texture, position);
                if (fitZoom > 0f)
                {
                    zoom = fitZoom;
                    scrollPosition = Vector2.zero;
                }
            }
        }

        #endregion

        #region キャンバス描画

        /// <summary>
        /// 初回のフィット処理を適用
        /// </summary>
        private void EnsureInitialFit()
        {
            if (initializedToFit) return;
            float fitZoom = CalculateFitZoom(texture, position);
            if (fitZoom > 0f)
            {
                initialFitZoom = fitZoom;
                zoom = initialFitZoom;
                initializedToFit = true;
            }
        }

        /// <summary>
        /// テクスチャのキャンバス内描画位置を計算する
        /// テクスチャがキャンバスに収まる場合は中央配置、大きい場合はスクロールオフセットを適用
        /// </summary>
        private Rect CalculateTextureDrawRect(float scaledWidth, float scaledHeight, Rect canvasRect)
        {
            if (scaledWidth <= canvasRect.width && scaledHeight <= canvasRect.height)
            {
                // テクスチャがキャンバスに収まる場合: 中央配置
                return new Rect(
                    (canvasRect.width - scaledWidth) * AppSettings.HALF_VALUE,
                    (canvasRect.height - scaledHeight) * AppSettings.HALF_VALUE,
                    scaledWidth,
                    scaledHeight
                );
            }

            // テクスチャがキャンバスより大きい場合: 軸ごとにスクロールまたは中央配置
            float ox = scaledWidth <= canvasRect.width
                ? (canvasRect.width - scaledWidth) * AppSettings.HALF_VALUE
                : -scrollPosition.x;
            float oy = scaledHeight <= canvasRect.height
                ? (canvasRect.height - scaledHeight) * AppSettings.HALF_VALUE
                : -scrollPosition.y;
            return new Rect(ox, oy, scaledWidth, scaledHeight);
        }

        /// <summary>
        /// マウス位置がスクロールバー領域内かどうかを判定する
        /// </summary>
        private bool IsMouseInScrollbarArea(bool hasHBar, bool hasVBar, float canvasWidth, float canvasHeight)
        {
            if (!Event.current.isMouse && Event.current.type != EventType.ScrollWheel) return false;

            Vector2 localMouse = Event.current.mousePosition;
            if (hasHBar && localMouse.y >= canvasHeight - AppSettings.CANVAS_SCROLLBAR_SIZE)
                return true;
            if (hasVBar && localMouse.x >= canvasWidth - AppSettings.CANVAS_SCROLLBAR_SIZE)
                return true;
            return false;
        }

        /// <summary>
        /// キャンバス領域全体を描画する
        /// GUI.BeginGroup でローカル座標系を確立し、テクスチャ・オーバーレイ・ワイヤーフレームを描画
        /// </summary>
        private void DrawCanvasArea()
        {
            // キャンバス領域を確保
            Rect canvasRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            lastCanvasRect = canvasRect;

            float scaledWidth = texture.width * zoom;
            float scaledHeight = texture.height * zoom;

            // スクロールオフセットをクランプ
            ClampScrollPosition(scaledWidth, scaledHeight, canvasRect);

            // テクスチャ描画位置を計算
            Rect textureDrawRect = CalculateTextureDrawRect(scaledWidth, scaledHeight, canvasRect);

            // スクロールバーの表示判定
            bool hasHBar = scaledWidth > canvasRect.width + AppSettings.CANVAS_SCROLLBAR_OVERLAP_TOLERANCE;
            bool hasVBar = scaledHeight > canvasRect.height + AppSettings.CANVAS_SCROLLBAR_OVERLAP_TOLERANCE;

            // GUI.BeginGroup でローカル座標系を確立
            GUI.BeginGroup(canvasRect);
            {
                // テクスチャ描画
                GUI.DrawTexture(textureDrawRect, texture, ScaleMode.ScaleToFit);
                lastTextureRect = textureDrawRect;

                // UVマスクオーバーレイ描画
                if (showUVMasks && overlayTexture != null)
                {
                    GUI.DrawTexture(textureDrawRect, overlayTexture, ScaleMode.ScaleToFit);
                }

                // UVワイヤーフレームGL描画
                if (Event.current.type == EventType.Repaint)
                {
                    Handles.BeginGUI();
                    DrawWireframeGL(textureDrawRect, canvasRect);
                    Handles.EndGUI();
                }

                // スクロールバー領域判定
                bool isInScrollbarArea = IsMouseInScrollbarArea(hasHBar, hasVBar, canvasRect.width, canvasRect.height);

                // マウス追従クロスヘア・クリック処理（スクロールバー領域以外のみ）
                if (!isInScrollbarArea)
                {
                    DrawMouseFollowerCrosshair(textureDrawRect);
                    HandlePreviewClick(textureDrawRect);
                }
            }
            GUI.EndGroup();

            // スクロールバー描画
            DrawScrollbars(canvasRect, scaledWidth, scaledHeight, hasHBar, hasVBar);
        }

        /// <summary>
        /// スクロール位置を有効範囲にクランプする
        /// </summary>
        private void ClampScrollPosition(float scaledWidth, float scaledHeight, Rect canvasRect)
        {
            float maxScrollX = Mathf.Max(0f, scaledWidth - canvasRect.width);
            float maxScrollY = Mathf.Max(0f, scaledHeight - canvasRect.height);
            scrollPosition.x = Mathf.Clamp(scrollPosition.x, 0f, maxScrollX);
            scrollPosition.y = Mathf.Clamp(scrollPosition.y, 0f, maxScrollY);
        }

        /// <summary>
        /// 手動スクロールバーを描画する
        /// </summary>
        private void DrawScrollbars(Rect canvasRect, float scaledWidth, float scaledHeight, bool hasHBar, bool hasVBar)
        {
            if (hasHBar)
            {
                Rect hRect = new Rect(
                    canvasRect.x,
                    canvasRect.yMax - AppSettings.CANVAS_SCROLLBAR_SIZE,
                    canvasRect.width - (hasVBar ? AppSettings.CANVAS_SCROLLBAR_SIZE : 0f),
                    AppSettings.CANVAS_SCROLLBAR_SIZE);
                float newX = GUI.HorizontalScrollbar(hRect, scrollPosition.x, canvasRect.width, 0f, scaledWidth);
                if (!Mathf.Approximately(newX, scrollPosition.x))
                {
                    scrollPosition.x = newX;
                    Repaint();
                }
            }

            if (hasVBar)
            {
                Rect vRect = new Rect(
                    canvasRect.xMax - AppSettings.CANVAS_SCROLLBAR_SIZE,
                    canvasRect.y,
                    AppSettings.CANVAS_SCROLLBAR_SIZE,
                    canvasRect.height - (hasHBar ? AppSettings.CANVAS_SCROLLBAR_SIZE : 0f));
                float newY = GUI.VerticalScrollbar(vRect, scrollPosition.y, canvasRect.height, 0f, scaledHeight);
                if (!Mathf.Approximately(newY, scrollPosition.y))
                {
                    scrollPosition.y = newY;
                    Repaint();
                }
            }
        }

        #endregion

        #region ズーム処理

        /// <summary>
        /// 遅延ズーム処理を適用する（スクロールホイール・スライダー）
        /// テクスチャ描画後に呼び出すことで、正確な座標変換を行う
        /// </summary>
        private void ApplyDeferredZoom()
        {
            if (texture == null) return;

            if (deferredScrollZoom)
            {
                ApplyScrollWheelZoom();
            }

            if (deferredSliderChanged)
            {
                ApplySliderZoom();
            }
        }

        /// <summary>
        /// 現在のズーム状態から実効倍率を取得する
        /// </summary>
        private float GetEffectiveZoom()
        {
            if (Mathf.Approximately(zoom, AppSettings.DEFAULT_SCALE) && lastTextureRect.width > 1f && texture.width > 0)
            {
                return lastTextureRect.width / texture.width;
            }
            return zoom;
        }

        /// <summary>
        /// ズーム変更に伴うスクロール位置の調整を計算する
        /// anchorInTexture はテクスチャ上のアンカーポイント（0-1の正規化座標）
        /// </summary>
        private Vector2 CalculateZoomScrollDelta(float oldZoom, float newZoom, Vector2 anchorInTexture)
        {
            float oldW = texture.width * oldZoom;
            float oldH = texture.height * oldZoom;
            float newW = texture.width * newZoom;
            float newH = texture.height * newZoom;

            Vector2 newAnchorPos = new Vector2(anchorInTexture.x * newW, anchorInTexture.y * newH);
            Vector2 oldAnchorPos = new Vector2(anchorInTexture.x * oldW, anchorInTexture.y * oldH);

            return newAnchorPos - oldAnchorPos;
        }

        /// <summary>
        /// テクスチャ上の正規化位置を計算（0-1にクランプ）
        /// </summary>
        private Vector2 CalculateNormalizedTexturePosition(Vector2 canvasLocalPos)
        {
            if (lastTextureRect.width <= 0 || lastTextureRect.height <= 0)
                return Vector2.zero;

            Vector2 posInTexture = canvasLocalPos - new Vector2(lastTextureRect.x, lastTextureRect.y);
            Vector2 normalized = new Vector2(
                posInTexture.x / lastTextureRect.width,
                posInTexture.y / lastTextureRect.height
            );
            return Vector2.Max(Vector2.zero, Vector2.Min(Vector2.one, normalized));
        }

        /// <summary>
        /// Ctrl+スクロールホイールによるズーム（マウス位置を中心に）
        /// </summary>
        private void ApplyScrollWheelZoom()
        {
            float effectiveOldZoom = GetEffectiveZoom();

            float targetZoom = effectiveOldZoom;
            if (deferredScrollDelta > 0f) targetZoom /= AppSettings.ZOOM_FACTOR_PER_NOTCH;
            else if (deferredScrollDelta < 0f) targetZoom *= AppSettings.ZOOM_FACTOR_PER_NOTCH;
            targetZoom = Mathf.Clamp(targetZoom, AppSettings.MIN_ZOOM, AppSettings.MAX_ZOOM);

            if (Mathf.Approximately(effectiveOldZoom, targetZoom)) return;

            // マウス位置をアンカーポイントとして使用
            Vector2 mousePosInCanvas = deferredMousePos - lastCanvasRect.position;
            Vector2 anchor = CalculateNormalizedTexturePosition(mousePosInCanvas);

            scrollPosition += CalculateZoomScrollDelta(effectiveOldZoom, targetZoom, anchor);
            zoom = targetZoom;
            Repaint();
        }

        /// <summary>
        /// スライダーによるズーム（ウィンドウ中心を基準に）
        /// </summary>
        private void ApplySliderZoom()
        {
            float effectiveOldZoom = GetEffectiveZoom();

            float targetZoom = Mathf.Approximately(zoom, AppSettings.DEFAULT_SCALE)
                ? deferredSliderValue * effectiveOldZoom
                : deferredSliderValue;
            targetZoom = Mathf.Clamp(targetZoom, AppSettings.MIN_ZOOM, AppSettings.MAX_ZOOM);

            if (Mathf.Approximately(effectiveOldZoom, targetZoom)) return;

            // ウィンドウ中心をアンカーポイントとして使用
            Vector2 windowCenter = new Vector2(position.width * AppSettings.HALF_VALUE, position.height * AppSettings.HALF_VALUE);
            Vector2 canvasLocalCenter = windowCenter - lastCanvasRect.position;
            Vector2 anchor = CalculateNormalizedTexturePosition(canvasLocalCenter);

            scrollPosition += CalculateZoomScrollDelta(effectiveOldZoom, targetZoom, anchor);
            zoom = targetZoom;
            Repaint();
        }

        #endregion

        #region 情報バー

        /// <summary>
        /// テクスチャ情報の表示バーを描画
        /// </summary>
        private void DrawInfoBar()
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label($"{UILabels.SIZE_LABEL} {texture.width} x {texture.height}");
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{UILabels.FORMAT_LABEL} {texture.format}");
            GUILayout.EndHorizontal();
        }

        #endregion

        #region ライフサイクル・ユーティリティ

        /// <summary>
        /// ウィンドウが閉じられる際のクリーンアップ
        /// </summary>
        private void OnDestroy()
        {
            ClearOverlayTexture();
            DestroyGLMaterial();
            _openWindows.Remove(this);
        }

        private void SwitchTarget(int index)
        {
            if (targets == null || index < 0 || index >= targets.Count) return;
            currentTargetIndex = index;
            var t = targets[index];
            texture = t.Texture;
            targetRenderer = t.Renderer;
            submeshIndex = t.SubmeshIndex;
            GenerateOverlayTexture();
            Repaint();
        }

        private string BuildTargetLabel(Renderer r, int sub, Texture2D tex)
        {
            string rName = r != null ? r.name : "(None)";
            string mName = null;
            if (r != null && r.sharedMaterials != null && sub >= 0 && sub < r.sharedMaterials.Length && r.sharedMaterials[sub] != null)
            {
                mName = r.sharedMaterials[sub].name;
            }
            string tName = tex != null ? tex.name : "(NoTex)";
            return string.Format("{0} / Sub {1} / {2}", rName, sub, string.IsNullOrEmpty(mName) ? tName : mName);
        }

        #endregion
    }
}
#endif
