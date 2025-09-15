#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Data;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator
{
    public partial class FurMaskGenerator : EditorWindow
    {
        // Core fields
        private FurMaskSettings settings;
        private DistanceMaskBaker currentBaker;

        [SerializeField] private GameObject avatarObject;
        private List<Renderer> avatarRenderers = new();
        private List<Renderer> clothRenderers = new();
        public List<SphereData> sphereMasks => settings?.sphereMasks;
        // Note: 内部API依存を排除。リスト描画は UIDrawingUtils.DrawRendererList に委譲
        private bool baking => currentBaker != null;
        Dictionary<string, Texture2D> preview = new();
        Vector2 scr;
        private bool addUVIslandOnClick = false;
        private bool showUVMarkers = true;
        private int selectedUVIslandIndex = -1;
        [SerializeField] private bool showSphereGizmos = true;
        [SerializeField] private bool foldoutSphereSection = false;
        [SerializeField] private bool foldoutUVSection = false;
        [SerializeField] private bool foldoutBoneSection = false;
        [SerializeField] private bool foldoutNormalMapSection = false;
        private Vector2 boneMaskScroll;
        private List<bool> sphereFoldoutStates = new List<bool>();
        private readonly Dictionary<int, List<int>[]> uvAdjacencyCache = new();
        private readonly Dictionary<string, List<int[]>> uvIslandTriCache = new();
        private bool addSphereOnClick = false;
        private int selectedSphereIndex = -1;
        private bool ignoreHierarchyChangeDuringBake = false;
        // スフィア追加モード用プレビューステート
        private bool hasSphereAddHover = false;
        private Vector3 sphereAddHoverPosition = Vector3.zero;
        // UVマスク追加モード用プレビューステート
        private bool hasUVAddHover = false;
        private Vector3 uvAddHoverPosition = Vector3.zero;
        private bool isShuttingDown = false;

        [MenuItem(FileConstants.MENU_ITEM_PATH)]
        static void Open() => GetWindow<FurMaskGenerator>(FileConstants.WINDOW_TITLE);

        void OnEnable()
        {
            isShuttingDown = false;
            LoadOrCreateSettings();
            InitializeUIComponents();
            RestoreAvatarAndRendererReferences();
            RegisterEvents();
        }

        void OnDisable()
        {
            isShuttingDown = true;
            // Unity終了時にアバター情報を保存
            StoreAvatarAndRendererReferences();
            EditorAssetUtils.SaveIfDirty(settings);
            
            // 最後に使用したアバターパスを保存（再確認）
            if (avatarObject != null && !string.IsNullOrEmpty(EditorPathUtils.GetGameObjectPath(avatarObject)))
            {
                string finalPath = EditorPathUtils.GetGameObjectPath(avatarObject);
                EditorPrefs.SetString(FileConstants.LAST_AVATAR_PATH_KEY, finalPath);
            }
            
            CleanupTextures();
            // レイキャストヘルパーを破棄
            CleanupRaycastHelper();
            UnregisterEvents();
        }

        void OnDestroy()
        {
            isShuttingDown = true;
            // レイキャストヘルパーを破棄
            CleanupRaycastHelper();
            UnregisterEvents();
        }

        private void OnHierarchyChange()
        {
            if (isShuttingDown) { return; }
            // ベイク中にClothCollider作成によるhierarchyChangedイベントは無視
            if (ignoreHierarchyChangeDuringBake)
            {
                return;
            }

            // 既にアバターが設定されており、そのアバターが存在している場合は処理をスキップ
            // （Hierarchy変更でアバターオブジェクトが削除された場合のみ復元処理を実行）
            if (avatarObject != null && settings != null && !string.IsNullOrEmpty(settings.avatarObjectPath))
            {
                var currentAvatar = EditorPathUtils.FindGameObjectByPath(settings.avatarObjectPath);
                if (currentAvatar == avatarObject)
                {
                    // アバターオブジェクトが変わっていないため、スキップ
                    return;
                }
            }

            RestoreAvatarAndRendererReferences();
            Repaint();
        }

        private void OnEditorSelectionChanged()
        {
            if (isShuttingDown) { return; }
            // Hierarchyで何かが選択されたらスフィア選択を解除
            if (Selection.activeObject != null)
            {
                selectedSphereIndex = -1;
                Repaint();
            }
        }

        private void OnDirectionSphereSelected()
        {
            if (isShuttingDown) { return; }
            // 方向スフィアが選択されたら、こちらスフィア選択を解除
            selectedSphereIndex = -1;
            Repaint();
        }

        private void LoadOrCreateSettings()
        {
            try
            {
                // 最後に使用したアバターの設定を復元を試みる
                string lastAvatarPath = EditorPrefs.GetString(FileConstants.LAST_AVATAR_PATH_KEY, "");
                
                if (!string.IsNullOrEmpty(lastAvatarPath))
                {
                    var lastAvatar = EditorPathUtils.FindGameObjectByPath(lastAvatarPath);
                    
                    if (lastAvatar != null && TryLoadSettingsForAvatar(lastAvatar, out FurMaskSettings loadedSettings))
                    {
                        settings = loadedSettings;
                        avatarObject = lastAvatar;
                        return;
                    }
                }
                
                // EditorPrefsにない場合、既存のアバター設定ファイルから推定を試みる
                if (TryLoadMostRecentAvatarSettings(out FurMaskSettings recentSettings, out GameObject recentAvatar))
                {
                    settings = recentSettings;
                    avatarObject = recentAvatar;
                    // 今回見つけたアバターを次回のために保存
                    EditorPrefs.SetString(FileConstants.LAST_AVATAR_PATH_KEY, EditorPathUtils.GetGameObjectPath(recentAvatar));
                    return;
                }
                
                // 最後のアバター復元に失敗した場合、デフォルト設定を読み込み
                settings = EditorAssetUtils.LoadOrCreateAssetAtPath<FurMaskSettings>(FileConstants.SETTINGS_ASSET_PATH);
                avatarObject = null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FurMaskGenerator] Error in LoadOrCreateSettings: {ex.Message}\n{ex.StackTrace}");
                // エラーが発生した場合もデフォルト設定を読み込み
                settings = EditorAssetUtils.LoadOrCreateAssetAtPath<FurMaskSettings>(FileConstants.SETTINGS_ASSET_PATH);
                avatarObject = null;
            }
        }

        void OnEditorUpdate()
        {
            if (isShuttingDown) { return; }
            if (baking)
            {
                Repaint();
            }
        }

        void CleanupTextures()
        {
            // 生成済みプレビューの破棄クリア
            EditorObjectUtils.DestroyAndClearDictionaryValues(preview);
        }

        private void InitializeUIComponents()
        {
            // ReorderableList 生成は廃止。IMGUIの独自リスト描画を利用
        }

        private void RegisterEvents()
        {
            // 再入性対策：一度解除してから登録
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            EditorApplication.hierarchyChanged -= OnHierarchyChange;
            EditorApplication.hierarchyChanged += OnHierarchyChange;

            Selection.selectionChanged -= OnEditorSelectionChanged;
            Selection.selectionChanged += OnEditorSelectionChanged;

            NolaTools.FurMaskGenerator.Utils.EditorMeshUtils.DirectionSphereSelected -= OnDirectionSphereSelected;
            NolaTools.FurMaskGenerator.Utils.EditorMeshUtils.DirectionSphereSelected += OnDirectionSphereSelected;
        }

        private void UnregisterEvents()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.hierarchyChanged -= OnHierarchyChange;
            Selection.selectionChanged -= OnEditorSelectionChanged;
            NolaTools.FurMaskGenerator.Utils.EditorMeshUtils.DirectionSphereSelected -= OnDirectionSphereSelected;
        }

        // Persistence methods
        private const string AvatarSettingsRoot = NolaTools.FurMaskGenerator.Utils.EditorAssetPaths.FurMaskGenerator.AvatarSettingsRoot;

        private void StoreAvatarAndRendererReferences()
        {
            if (settings == null) return;
            
            string avatarPath = EditorPathUtils.GetGameObjectPath(avatarObject);
            settings.avatarObjectPath = avatarPath;
            settings.avatarRendererPaths = EditorPathUtils.GetComponentPaths(avatarRenderers);
            settings.clothRendererPaths = EditorPathUtils.GetComponentPaths(clothRenderers);
            UndoRedoUtils.SetDirtyOnly(settings);
            
            // 最後に使用したアバターパスをEditorPrefsに保存
            if (avatarObject != null)
            {
                EditorPrefs.SetString(FileConstants.LAST_AVATAR_PATH_KEY, avatarPath);
            }
        }

        private void RestoreAvatarAndRendererReferences()
        {
            if (settings == null) return;
            
            // LoadOrCreateSettingsでavatarObjectが既に設定されている場合はスキップ
            // （設定ファイルから復元された場合）
            if (avatarObject == null && !string.IsNullOrEmpty(settings.avatarObjectPath))
            {
                var go = EditorPathUtils.FindGameObjectByPath(settings.avatarObjectPath);
                if (go != null) 
                {
                    avatarObject = go;
                }
            }
            
            // レンダラーリストを復元
            avatarRenderers.Clear();
            if (settings.avatarRendererPaths != null)
            {
                foreach (var path in settings.avatarRendererPaths)
                {
                    if (string.IsNullOrEmpty(path)) { avatarRenderers.Add(null); continue; }
                    var go = EditorPathUtils.FindGameObjectByPath(path);
                    var renderer = go != null ? go.GetComponent<Renderer>() : null;
                    avatarRenderers.Add(renderer);
                }
            }
            
            clothRenderers.Clear();
            if (settings.clothRendererPaths != null)
            {
                foreach (var path in settings.clothRendererPaths)
                {
                    if (string.IsNullOrEmpty(path)) { clothRenderers.Add(null); continue; }
                    var go = EditorPathUtils.FindGameObjectByPath(path);
                    var renderer = go != null ? go.GetComponent<Renderer>() : null;
                    clothRenderers.Add(renderer);
                }
            }
        }

        private bool TryLoadSettingsForAvatar(GameObject avatar, out FurMaskSettings loaded)
        {
            return EditorAssetUtils.TryLoadAvatarSettings(AvatarSettingsRoot, nameof(FurMaskSettings), avatar, out loaded);
        }

        private FurMaskSettings CreateSettingsForAvatar(GameObject avatar)
        {
            var asset = EditorAssetUtils.CreateAvatarSettings<FurMaskSettings>(AvatarSettingsRoot, nameof(FurMaskSettings), avatar);
            asset.avatarObjectPath = EditorPathUtils.GetGameObjectPath(avatar);
            return asset;
        }

        private void ClearSettingsForNewAvatar()
        {
            if (settings == null) return;
            settings.sphereMasks.Clear();
            settings.uvIslandMasks.Clear();
            settings.boneMasks.Clear();
            settings.ResetToDefaults();
            avatarRenderers.Clear();
            clothRenderers.Clear();
            selectedSphereIndex = -1;
            UndoRedoUtils.SetDirtyOnly(settings);
        }

        /// <summary>
        /// 既存のアバター設定ファイルから最も最近のもので有効なアバターを見つける
        /// </summary>
        private bool TryLoadMostRecentAvatarSettings(out FurMaskSettings recentSettings, out GameObject recentAvatar)
        {
            recentSettings = null;
            recentAvatar = null;

            try
            {
                // フォルダが存在しない場合は早期リターン
                if (!AssetDatabase.IsValidFolder(AvatarSettingsRoot))
                {
                    return false;
                }

                string[] guids = AssetDatabase.FindAssets("t:FurMaskSettings", new[] { AvatarSettingsRoot });

                FurMaskSettings bestSettings = null;
                GameObject bestAvatar = null;
                System.DateTime bestTime = System.DateTime.MinValue;

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var settings = AssetDatabase.LoadAssetAtPath<FurMaskSettings>(path);
                    if (settings == null || string.IsNullOrEmpty(settings.avatarObjectPath)) continue;

                    var avatar = EditorPathUtils.FindGameObjectByPath(settings.avatarObjectPath);
                    if (avatar == null) continue;

                    // AssetDatabase のパスは相対（Assets/～）。実ファイル時刻を得るため絶対パスへ変換
                    string absolute = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", path));
                    System.DateTime fileTime = System.IO.File.Exists(absolute) ? System.IO.File.GetLastWriteTime(absolute) : System.DateTime.MinValue;
                    if (fileTime > bestTime)
                    {
                        bestTime = fileTime;
                        bestSettings = settings;
                        bestAvatar = avatar;
                    }
                }

                if (bestSettings != null && bestAvatar != null)
                {
                    recentSettings = bestSettings;
                    recentAvatar = bestAvatar;
                    return true;
                }

                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FurMaskGenerator] Error in TryLoadMostRecentAvatarSettings: {ex.Message}");
                return false;
            }
        }
    }
}
#endif
