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
        private static readonly System.Collections.Generic.List<TexturePreviewWindow> _openWindows = new System.Collections.Generic.List<TexturePreviewWindow>();
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
        private Texture2D wireframeTexture;
        private Rect lastTextureRect;
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
        
        public static void ShowWindow(Texture2D texture)
        {
            TexturePreviewWindow window = GetWindow<TexturePreviewWindow>(string.Format(UILabels.TEXTURE_PREVIEW_TITLE_FORMAT, texture.name));
            window.texture = texture;

            // UVマスク・ワイヤーフレーム表示フラグを初期化（通常プレビュー用）
            window.showUVMasks = false;
            window.showUVWireframe = false;
            window.overlayTexture = null;
            window.wireframeTexture = null;

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

            // ウィンドウが再利用された場合の初期化（念のため）
            if (texture != null && targets == null && uvMasks == null)
            {
                // 通常プレビュー用にUVマスク・ワイヤーフレーム表示フラグを初期化
                showUVMasks = false;
                showUVWireframe = false;
                overlayTexture = null;
                wireframeTexture = null;
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
                if (w.showUVWireframe)
                {
                    w.GenerateWireframeTexture();
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
            window.GenerateWireframeTexture();
            window.Show();
        }

        /// <summary>
        /// テクスチャをウィンドウサイズにフィットさせる倍率を計算する
        /// </summary>
        /// <param name="texture">対象テクスチャ</param>
        /// <param name="windowPosition">ウィンドウの位置とサイズ</param>
        /// <returns>フィット倍率（無効な場合は-1）</returns>
        private float CalculateFitZoom(Texture2D texture, Rect windowPosition)
        {
            if (texture == null) return -1f;

            // ウィンドウの利用可能な領域を推定（ツールバーと情報表示を考慮）
            float toolbarHeight = 22f; // ツールバー高さの推定値
            float infoHeight = 18f; // 情報表示高さの推定値
            float availableHeight = windowPosition.height - toolbarHeight - infoHeight;

            // 利用可能な幅（スクロールバーを考慮）
            float availableWidth = windowPosition.width - 20f; // スクロールバー幅を考慮

            // テクスチャのアスペクト比を保持してフィット
            float textureAspect = (float)texture.width / texture.height;
            float availableAspect = availableWidth / availableHeight;

            float fitZoom;
            if (textureAspect > availableAspect)
            {
                // 横長のテクスチャ：幅に合わせる
                fitZoom = availableWidth / texture.width;
            }
            else
            {
                // 縦長のテクスチャ：高さに合わせる
                fitZoom = availableHeight / texture.height;
            }

            // 有効な倍率の場合のみ返す
            if (fitZoom > AppSettings.FIT_EPSILON && !float.IsNaN(fitZoom) && !float.IsInfinity(fitZoom))
            {
                return Mathf.Clamp(fitZoom, AppSettings.MIN_ZOOM, AppSettings.MAX_ZOOM);
            }

            return -1f;
        }
        
        private void OnGUI()
        {
            // エスケープキーでウィンドウを閉じる
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Close();
                Event.current.Use();
                return;
            }

            // 右クリックでウィンドウを閉じる
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1)
            {
                Close();
                Event.current.Use();
                return;
            }

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
            
            // Ctrl+ホイールでズーム（カーソル位置を中心に）: Fit倍率から拡縮するため、描画後に適用
            bool __deferredScrollZoom = false;
            float __deferredScrollDelta = 0f;
            Vector2 __deferredMousePos = Vector2.zero;
            if (Event.current.type == EventType.ScrollWheel && (Event.current.control || Event.current.command))
            {
                __deferredScrollZoom = true;
                __deferredScrollDelta = Event.current.delta.y;
                __deferredMousePos = Event.current.mousePosition;
                Event.current.Use();
            }
            
            // ツールバー
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            // 左側: テクスチャ選択（重複しないテクスチャを列挙）
            if (targets != null && targets.Count > 0)
            {

                // 重複排除したテクスチャ一覧を作成（参照でユニーク化）
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
                    // 選択されたテクスチャを持つ最初のターゲットに切り替え
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
            GUILayout.FlexibleSpace();

            // 複数ターゲットモードのみ表示
            if (targets != null && targets.Count > 0)
            {
                // UVマスク表示トグル
                bool newShowUVMasks = GUILayout.Toggle(showUVMasks, UILabels.UV_MASK_TOGGLE, EditorStyles.toolbarButton);
                if (newShowUVMasks != showUVMasks)
                {
                    showUVMasks = newShowUVMasks;
                    if (showUVMasks)
                    {
                        GenerateOverlayTexture();
                    }
                    else
                    {
                        ClearOverlayTexture();
                    }
                }

                // UVワイヤーフレーム表示トグル
                bool newShowWire = GUILayout.Toggle(showUVWireframe, UILabels.UV_WIREFRAME_TOGGLE, EditorStyles.toolbarButton);
                if (newShowWire != showUVWireframe)
                {
                    showUVWireframe = newShowWire;
                    if (showUVWireframe)
                    {
                        GenerateWireframeTexture();
                    }
                    else
                    {
                        ClearWireframeTexture();
                    }
                }
            }

            // ズームコントロール（スライダーはウィンドウ中心を軸に拡縮）
            GUILayout.Label(UILabels.ZOOM_LABEL, EditorStyles.toolbarButton);
            bool __deferredSliderChanged = false; float __deferredSliderValue = zoom;
            EditorGUI.BeginChangeCheck();
            float newSliderZoom = GUILayout.HorizontalSlider(zoom, AppSettings.MIN_ZOOM, AppSettings.MAX_ZOOM, GUILayout.Width(100));
            if (EditorGUI.EndChangeCheck())
            {
                __deferredSliderChanged = true;
                __deferredSliderValue = newSliderZoom;
            }
            
            // 常に数値倍率表示
            string zoomLabel = $"{zoom:F2}x";
            GUILayout.Label(zoomLabel, EditorStyles.toolbarButton, GUILayout.Width(40));

            if (GUILayout.Button(UILabels.RESET_BUTTON, EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                // 現在のウィンドウサイズに合わせてテクスチャをフィットさせる
                float fitZoom = CalculateFitZoom(texture, position);
                if (fitZoom > 0f)
                {
                    zoom = fitZoom;
                    // スクロール位置を中央にリセット
                    scrollPosition = Vector2.zero;
                }
            }
            
            GUILayout.EndHorizontal();
            
            // スクロールビューまたは初期フィット表示
            Rect currentTextureRect = Rect.zero;
            if (!initializedToFit)
            {
                // 初回はウィンドウサイズに合わせてフィット（ウィンドウに合わせるボタンと同じロジック）
                scrollPosition = GUILayout.BeginScrollView(scrollPosition);

                float fitZoom = CalculateFitZoom(texture, position);
                if (fitZoom > 0f)
                {
                    initialFitZoom = fitZoom;
                    zoom = initialFitZoom;
                    initializedToFit = true;
                }

                // 初期フィット時は空の領域を表示
                GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

                GUILayout.EndScrollView();
            }
            else
            {
                // ズーム1.0以外の場合はスクロールビューを使用
                // テクスチャの表示サイズを計算
                float scaledWidth = texture.width * zoom;
                float scaledHeight = texture.height * zoom;

                // コンテンツサイズを決定（スクロール可能な領域）
                // ウィンドウサイズを基準に最小サイズを確保
                float minWidth = position.width - 20f; // スクロールバー分を引く
                float minHeight = position.height - 80f; // ツールバー分を引く
                float contentWidth = Mathf.Max(scaledWidth, minWidth);
                float contentHeight = Mathf.Max(scaledHeight, minHeight);

                // BeginScrollView - シンプルな形式で開始
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false);

                // コンテンツ領域のスペースを確保（空のボックスで領域を確保）
                GUILayout.Box("", GUIStyle.none, GUILayout.Width(contentWidth), GUILayout.Height(contentHeight));

                // 現在のスクロールビュー内のレイアウト領域を取得
                Rect currentRect = GUILayoutUtility.GetLastRect();

                // テクスチャを現在の領域内で中央配置
                Rect textureRect = new Rect(
                    currentRect.x + (currentRect.width - scaledWidth) * AppSettings.HALF_VALUE,
                    currentRect.y + (currentRect.height - scaledHeight) * AppSettings.HALF_VALUE,
                    scaledWidth,
                    scaledHeight
                );

                GUI.DrawTexture(textureRect, texture, ScaleMode.ScaleToFit);
                lastTextureRect = textureRect;

                // UVマスクオーバーレイを描画
                if (showUVMasks && overlayTexture != null)
                {
                    GUI.DrawTexture(textureRect, overlayTexture, ScaleMode.ScaleToFit);
                }
                // UVワイヤーフレームを描画
                if (showUVWireframe && wireframeTexture != null)
                {
                    GUI.DrawTexture(textureRect, wireframeTexture, ScaleMode.ScaleToFit);
                }

                // マウス追従の十字を描画
                DrawMouseFollowerCrosshair(textureRect);

                // クリック処理（スクロールビュー内で処理して座標ズレを防止）
                HandlePreviewClick(textureRect);

                GUILayout.EndScrollView();
            }
            

            // 描画後にズーム適用（マウス位置を中心にズーム）
            if (__deferredScrollZoom && texture != null)
            {
                float effectiveOldZoom = zoom;
                if (Mathf.Approximately(zoom, AppSettings.DEFAULT_SCALE) && lastTextureRect.width > 1f && texture.width > 0)
                {
                    effectiveOldZoom = lastTextureRect.width / texture.width;
                }

                const float zoomFactorPerNotch = AppSettings.ZOOM_FACTOR_PER_NOTCH;
                float targetZoom = effectiveOldZoom;
                if (__deferredScrollDelta > 0f) targetZoom /= zoomFactorPerNotch;
                else if (__deferredScrollDelta < 0f) targetZoom *= zoomFactorPerNotch;
                targetZoom = Mathf.Clamp(targetZoom, AppSettings.MIN_ZOOM, AppSettings.MAX_ZOOM);

                if (!Mathf.Approximately(effectiveOldZoom, targetZoom))
                {
                    // マウス位置を中心としたズーム調整
                    Vector2 mousePosInScrollView = __deferredMousePos - new Vector2(lastTextureRect.x, lastTextureRect.y);

                    // テクスチャ内での相対位置を計算
                    Vector2 relativePos = Vector2.zero;
                    if (lastTextureRect.width > 0 && lastTextureRect.height > 0)
                    {
                        relativePos.x = mousePosInScrollView.x / lastTextureRect.width;
                        relativePos.y = mousePosInScrollView.y / lastTextureRect.height;
                        relativePos = Vector2.Max(Vector2.zero, Vector2.Min(Vector2.one, relativePos));
                    }

                    float oldW = texture.width * effectiveOldZoom;
                    float oldH = texture.height * effectiveOldZoom;
                    float newW = texture.width * targetZoom;
                    float newH = texture.height * targetZoom;

                    // 新しいテクスチャサイズでのマウス位置を維持
                    Vector2 newMousePosInTexture = new Vector2(relativePos.x * newW, relativePos.y * newH);
                    Vector2 oldMousePosInTexture = new Vector2(relativePos.x * oldW, relativePos.y * oldH);

                    // スクロール位置を調整してマウス位置を維持
                    Vector2 deltaScroll = oldMousePosInTexture - newMousePosInTexture;
                    scrollPosition += deltaScroll;

                    zoom = targetZoom;
                    Repaint();
                }
            }

            // スライダー変更を描画後に適用（ウィンドウ中心を基準にズーム）
            if (__deferredSliderChanged && texture != null)
            {
                float effectiveOldZoom = zoom;
                if (Mathf.Approximately(zoom, AppSettings.DEFAULT_SCALE) && lastTextureRect.width > 1f && texture.width > 0)
                {
                    effectiveOldZoom = lastTextureRect.width / texture.width;
                }
                float targetZoom = Mathf.Clamp(
                    (Mathf.Approximately(zoom, AppSettings.DEFAULT_SCALE) ? __deferredSliderValue * effectiveOldZoom : __deferredSliderValue),
                    AppSettings.MIN_ZOOM, AppSettings.MAX_ZOOM);

                if (!Mathf.Approximately(effectiveOldZoom, targetZoom))
                {
                    float oldW = texture.width * effectiveOldZoom;
                    float oldH = texture.height * effectiveOldZoom;
                    float newW = texture.width * targetZoom;
                    float newH = texture.height * targetZoom;

                    // ウィンドウの中心を基準にズーム調整
                    Vector2 windowCenter = new Vector2(position.width * AppSettings.HALF_VALUE, position.height * AppSettings.HALF_VALUE);
                    Vector2 centerInTexture = Vector2.zero;

                    if (lastTextureRect.width > 0 && lastTextureRect.height > 0)
                    {
                        centerInTexture.x = (windowCenter.x - lastTextureRect.x) / lastTextureRect.width;
                        centerInTexture.y = (windowCenter.y - lastTextureRect.y) / lastTextureRect.height;
                        centerInTexture = Vector2.Max(Vector2.zero, Vector2.Min(Vector2.one, centerInTexture));
                    }

                    // 新しいサイズでの中心位置を維持
                    Vector2 newCenterPos = new Vector2(centerInTexture.x * newW, centerInTexture.y * newH);
                    Vector2 oldCenterPos = new Vector2(centerInTexture.x * oldW, centerInTexture.y * oldH);

                    // スクロール位置を調整
                    Vector2 deltaScroll = oldCenterPos - newCenterPos;
                    scrollPosition += deltaScroll;

                    zoom = targetZoom;
                    Repaint();
                }
            }

            // 情報表示
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label($"{UILabels.SIZE_LABEL} {texture.width} x {texture.height}");
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{UILabels.FORMAT_LABEL} {texture.format}");
            GUILayout.EndHorizontal();
        }
        
        /// <summary>
        /// ウィンドウが閉じられる際のクリーンアップ
        /// </summary>
        private void OnDestroy()
        {
            ClearOverlayTexture();
            ClearWireframeTexture();
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
            GenerateWireframeTexture();
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
    }
}
#endif
