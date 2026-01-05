using System;
using System.Collections.Generic;
using GroomingTool2.Core;
using GroomingTool2.Managers;
using GroomingTool2.State;
using GroomingTool2.Utils;
using UnityEditor;
using UnityEngine;

namespace GroomingTool2.Scene
{
    /// <summary>
    /// Sceneビューでの毛向きプレビューと編集を担当するクラス
    /// </summary>
    internal sealed class GroomingSceneViewController : IDisposable
    {
        private readonly GroomingTool2State state;
        private readonly GroomingTool2MaterialManager materialManager;
        private readonly FurDataManager furDataManager;
        private readonly BrushStrokeExecutor strokeExecutor;
        private readonly GroomingTool2UI ui;
        private readonly UvIslandMaskState maskState;
        private readonly Action<string> saveUndoCallback;
        private readonly Action repaintCallback;
        
        // Undo/Redoコールバック
        private Action undoCallback;
        private Action redoCallback;
        private Func<bool> canUndoCallback;
        private Func<bool> canRedoCallback;

        // 描画担当
        private readonly SceneViewFurRenderer furRenderer;

        // マウス操作状態
        private bool isDragging;
        private readonly List<StrokePoint> dragTrail = new();

        // レイキャスト用ヘルパー
        private GameObject raycastHelper;
        private MeshCollider raycastHelperCollider;
        // 複数レンダラーのBakeMeshキャッシュ（レンダラーごとにメッシュを保持）
        private readonly Dictionary<Renderer, Mesh> bakedMeshCache = new();
        private int lastBakeFrame = -1;
        // MeshColliderに設定中のメッシュ（不要な再設定を避ける）
        private Mesh currentColliderMesh;
        
        // ブラシカーソル更新の最適化用
        private Vector2 lastMousePosition;
        private bool lastBrushCursorValid;
        private Vector3 lastBrushHitPosition;
        private Vector3 lastBrushHitNormal;

        /// <summary>
        /// ストローク点の情報（レンダラー/サブメッシュ情報を含む）
        /// </summary>
        private struct StrokePoint
        {
            public Vector2 dataCoord;
            public Vector2 uvCoord;
            public Vector3 worldPosition;
            public Vector3 tangent;
            public Vector3 bitangent;
            public Vector3 normal;
            public Vector3 rayDirection;  // ストローク時の視線方向（カメラからの方向）
            public Renderer renderer;
            public int submeshIndex;
        }

        public GroomingSceneViewController(
            GroomingTool2State state,
            GroomingTool2MaterialManager materialManager,
            FurDataManager furDataManager,
            BrushStrokeExecutor strokeExecutor,
            GroomingTool2UI ui,
            UvIslandMaskState maskState,
            Action<string> saveUndoCallback,
            Action repaintCallback)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.materialManager = materialManager ?? throw new ArgumentNullException(nameof(materialManager));
            this.furDataManager = furDataManager ?? throw new ArgumentNullException(nameof(furDataManager));
            this.strokeExecutor = strokeExecutor ?? throw new ArgumentNullException(nameof(strokeExecutor));
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.maskState = maskState ?? throw new ArgumentNullException(nameof(maskState));
            this.saveUndoCallback = saveUndoCallback ?? throw new ArgumentNullException(nameof(saveUndoCallback));
            this.repaintCallback = repaintCallback ?? throw new ArgumentNullException(nameof(repaintCallback));

            // 描画担当を初期化
            furRenderer = new SceneViewFurRenderer(state, furDataManager, maskState);

            // SceneView.duringSceneGuiに登録
            SceneView.duringSceneGui += OnSceneGUI;
        }

        public void Dispose()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            furRenderer.ClearCache();
            CleanupRaycastHelper();
        }

        /// <summary>
        /// マテリアルが変更された時に呼び出す
        /// </summary>
        public void OnMaterialChanged()
        {
            furRenderer.OnMaterialChanged();
        }

        /// <summary>
        /// Undo/Redoコールバックを設定する
        /// </summary>
        public void SetUndoRedoCallbacks(
            Func<bool> canUndo,
            Func<bool> canRedo,
            Action undo,
            Action redo)
        {
            canUndoCallback = canUndo;
            canRedoCallback = canRedo;
            undoCallback = undo;
            redoCallback = redo;
        }

        /// <summary>
        /// SceneビューのGUI処理
        /// </summary>
        private void OnSceneGUI(SceneView sceneView)
        {
            if (state?.Avatar == null)
                return;

            // Scene編集が無効の場合はすべてスキップ
            if (!state.SceneEditingEnabled)
                return;

            var selectedMaterial = materialManager.SelectedMaterial;
            if (!selectedMaterial.HasValue)
                return;

            // キャッシュを更新
            furRenderer.UpdateCacheIfNeeded(selectedMaterial);

            // 毛向きの描画（視錐台カリング有効）
            furRenderer.DrawHairDirections(sceneView);

            // マウス操作の処理（マスクモード以外）
            if (ui.CurrentMode != ToolMode.Mask)
            {
                // ブラシカーソルの更新
                UpdateBrushCursor(sceneView, selectedMaterial.Value);

                // ブラシカーソルの描画
                furRenderer.DrawBrushCursor();

                HandleMouseInput(sceneView, selectedMaterial.Value);
            }

            // キーボードショートカットの処理
            HandleKeyboardShortcuts();

            // Sceneビューを再描画（マウス移動時など）
            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)
            {
                SceneView.RepaintAll();
            }
        }

        /// <summary>
        /// キーボードショートカットの処理
        /// </summary>
        private void HandleKeyboardShortcuts()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown)
                return;

            // Ctrl+Z: Undo
            // Scene編集中は必ずイベントを消費してUnityのUndoをブロックする
            if (e.control && e.keyCode == KeyCode.Z)
            {
                if (canUndoCallback != null && canUndoCallback())
                {
                    undoCallback?.Invoke();
                    repaintCallback?.Invoke();
                    SceneView.RepaintAll();
                }
                // Undo可能かどうかに関わらず、Scene編集中は必ずイベントを消費
                e.Use();
            }
            // Ctrl+Y: Redo
            // Scene編集中は必ずイベントを消費してUnityのRedoをブロックする
            else if (e.control && e.keyCode == KeyCode.Y)
            {
                if (canRedoCallback != null && canRedoCallback())
                {
                    redoCallback?.Invoke();
                    repaintCallback?.Invoke();
                    SceneView.RepaintAll();
                }
                // Redo可能かどうかに関わらず、Scene編集中は必ずイベントを消費
                e.Use();
            }
        }

        #region レイキャスト処理

        private void EnsureRaycastHelper()
        {
            if (raycastHelper == null)
            {
                raycastHelper = new GameObject("GroomingTool2_RaycastHelper");
                raycastHelper.hideFlags = HideFlags.HideAndDontSave;
                raycastHelperCollider = raycastHelper.AddComponent<MeshCollider>();
                raycastHelperCollider.convex = false;
            }
        }

        private bool SetupRaycastMesh(Renderer renderer)
        {
            if (renderer == null)
                return false;

            EnsureRaycastHelper();

            raycastHelper.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
            raycastHelper.transform.localScale = renderer.transform.lossyScale;

            Mesh meshToUse = null;

            if (renderer is SkinnedMeshRenderer smr)
            {
                int currentFrame = Time.frameCount;
                
                // フレームが変わったらキャッシュをクリア（古いBakeMesh結果を使わない）
                if (lastBakeFrame != currentFrame)
                {
                    lastBakeFrame = currentFrame;
                    // キャッシュはクリアせず、各レンダラーのメッシュを再利用する
                }

                // このレンダラーのキャッシュされたメッシュを取得または作成
                if (!bakedMeshCache.TryGetValue(renderer, out var cachedMesh))
                {
                    cachedMesh = new Mesh();
                    cachedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    bakedMeshCache[renderer] = cachedMesh;
                }

                // 同一フレーム内で同じレンダラーに対しては、キャッシュにあればBakeMeshをスキップ
                // ただし、フレームが変わったら再Bakeが必要
                // vertexCountが0の場合は未Bakeなので実行
                if (cachedMesh.vertexCount == 0 || !IsMeshBakedThisFrame(renderer))
                {
                    smr.BakeMesh(cachedMesh, true);
                    MarkMeshBakedThisFrame(renderer);
                }

                meshToUse = cachedMesh;
            }
            else if (renderer.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
            {
                meshToUse = mf.sharedMesh;
            }

            if (meshToUse == null || meshToUse.vertexCount == 0)
            {
                if (currentColliderMesh != null)
                {
                    raycastHelperCollider.sharedMesh = null;
                    currentColliderMesh = null;
                }
                return false;
            }

            // MeshColliderへの設定は、メッシュが変わった場合のみ行う（BVH再構築を避ける）
            if (currentColliderMesh != meshToUse)
            {
                raycastHelperCollider.sharedMesh = meshToUse;
                currentColliderMesh = meshToUse;
            }

            return true;
        }
        
        // フレームごとのBake済みフラグ（同一フレーム内での重複Bakeを防ぐ）
        private readonly HashSet<Renderer> bakedThisFrame = new();
        private int bakedThisFrameToken = -1;
        
        private bool IsMeshBakedThisFrame(Renderer renderer)
        {
            int currentFrame = Time.frameCount;
            if (bakedThisFrameToken != currentFrame)
            {
                bakedThisFrame.Clear();
                bakedThisFrameToken = currentFrame;
            }
            return bakedThisFrame.Contains(renderer);
        }
        
        private void MarkMeshBakedThisFrame(Renderer renderer)
        {
            int currentFrame = Time.frameCount;
            if (bakedThisFrameToken != currentFrame)
            {
                bakedThisFrame.Clear();
                bakedThisFrameToken = currentFrame;
            }
            bakedThisFrame.Add(renderer);
        }

        private void CleanupRaycastHelper()
        {
            // キャッシュされた全てのBakedMeshを破棄
            foreach (var mesh in bakedMeshCache.Values)
            {
                if (mesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
            bakedMeshCache.Clear();
            bakedThisFrame.Clear();
            currentColliderMesh = null;
            
            if (raycastHelper != null)
            {
                UnityEngine.Object.DestroyImmediate(raycastHelper);
                raycastHelper = null;
                raycastHelperCollider = null;
            }
        }

        #endregion

        #region ブラシカーソル処理

        private void UpdateBrushCursor(SceneView sceneView, MaterialEntry materialEntry)
        {
            Event e = Event.current;
            Vector2 currentMousePos = e.mousePosition;
            
            // マウス位置が変わっていない場合は前回の結果を再利用（レイキャスト処理をスキップ）
            if (Vector2.Distance(currentMousePos, lastMousePosition) < 0.5f)
            {
                // 前回の結果を使用
                if (lastBrushCursorValid)
                {
                    float worldRadius = furRenderer.CalculateWorldBrushRadius();
                    furRenderer.UpdateBrushCursorState(lastBrushHitPosition, lastBrushHitNormal, worldRadius, true);
                }
                else
                {
                    furRenderer.UpdateBrushCursorState(Vector3.zero, Vector3.up, 0f, false);
                }
                return;
            }
            lastMousePosition = currentMousePos;
            
            Ray ray = HandleUtility.GUIPointToWorldRay(currentMousePos);

            float minDistance = float.MaxValue;
            Vector3 hitPosition = Vector3.zero;
            Vector3 hitNormal = Vector3.up;
            bool hitFound = false;

            // マスクチェックが必要かどうか
            bool checkMask = maskState != null && maskState.RestrictEditing && maskState.HasAnySelection;

            foreach (var (renderer, submeshIndex) in materialEntry.usages)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (!SetupRaycastMesh(renderer))
                    continue;

                if (raycastHelperCollider != null && raycastHelperCollider.Raycast(ray, out RaycastHit hit, 1000f))
                {
                    float dotProduct = Vector3.Dot(ray.direction, hit.normal);
                    if (dotProduct >= 0f)
                        continue;

                    // マスクチェック: ヒットしたUV座標がマスク範囲外ならスキップ
                    if (checkMask)
                    {
                        Vector2 hitUV = hit.textureCoord;
                        int dataX = Mathf.Clamp(Mathf.RoundToInt(hitUV.x * Common.TexSize), 0, Common.TexSize - 1);
                        int dataY = Mathf.Clamp(Mathf.RoundToInt((1f - hitUV.y) * Common.TexSize), 0, Common.TexSize - 1);
                        if (!maskState.IsEffectiveSelected(dataX, dataY))
                            continue;
                    }

                    if (hit.distance < minDistance)
                    {
                        minDistance = hit.distance;
                        hitPosition = hit.point;
                        hitNormal = hit.normal;
                        hitFound = true;
                    }
                }
            }

            // 結果をキャッシュ
            lastBrushCursorValid = hitFound;
            lastBrushHitPosition = hitPosition;
            lastBrushHitNormal = hitNormal;
            
            if (hitFound)
            {
                float worldRadius = furRenderer.CalculateWorldBrushRadius();
                furRenderer.UpdateBrushCursorState(hitPosition, hitNormal, worldRadius, true);
            }
            else
            {
                furRenderer.UpdateBrushCursorState(Vector3.zero, Vector3.up, 0f, false);
            }
        }

        #endregion

        #region マウス入力処理

        private void HandleMouseInput(SceneView sceneView, MaterialEntry materialEntry)
        {
            Event e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                isDragging = true;
                dragTrail.Clear();
                var strokePoint = GetStrokePointFromMousePosition(sceneView, e.mousePosition, materialEntry);
                if (strokePoint.HasValue)
                {
                    dragTrail.Add(strokePoint.Value);
                }
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && isDragging && e.button == 0)
            {
                var strokePoint = GetStrokePointFromMousePosition(sceneView, e.mousePosition, materialEntry);
                if (strokePoint.HasValue)
                {
                    bool isSameIsland = dragTrail.Count > 0 &&
                        ArePointsOnSameUVIsland(dragTrail[dragTrail.Count - 1], strokePoint.Value);

                    if (!isSameIsland && dragTrail.Count >= 2)
                    {
                        ApplyStrokeFromUVTrail();
                        dragTrail.Clear();
                        dragTrail.Add(strokePoint.Value);
                    }
                    else
                    {
                        dragTrail.Add(strokePoint.Value);
                        if (dragTrail.Count > 5)
                            dragTrail.RemoveAt(0);

                        if (dragTrail.Count >= 2)
                        {
                            ApplyStrokeFromUVTrail();
                        }
                    }
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && isDragging && e.button == 0)
            {
                isDragging = false;
                if (dragTrail.Count >= 2)
                {
                    saveUndoCallback?.Invoke("Sceneビューでの毛の編集");
                }
                dragTrail.Clear();
                e.Use();
            }
        }

        private StrokePoint? GetStrokePointFromMousePosition(SceneView sceneView, Vector2 mousePosition, MaterialEntry materialEntry)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            float minDistance = float.MaxValue;
            Vector2? closestUV = null;
            Vector3 closestWorldPos = Vector3.zero;
            Vector3 closestTangent = Vector3.right;
            Vector3 closestBitangent = Vector3.up;
            Vector3 closestNormal = Vector3.up;
            Renderer closestRenderer = null;
            int closestSubmeshIndex = -1;
            int closestTriangleIndex = -1;

            // マスクチェックが必要かどうか
            bool checkMask = maskState != null && maskState.RestrictEditing && maskState.HasAnySelection;

            foreach (var (renderer, submeshIndex) in materialEntry.usages)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (!SetupRaycastMesh(renderer))
                    continue;

                if (raycastHelperCollider != null && raycastHelperCollider.Raycast(ray, out RaycastHit hit, 1000f))
                {
                    float dotProduct = Vector3.Dot(ray.direction, hit.normal);
                    if (dotProduct >= 0f)
                        continue;

                    // マスクチェック: ヒットしたUV座標がマスク範囲外ならスキップ
                    if (checkMask)
                    {
                        Vector2 hitUV = hit.textureCoord;
                        int dataX = Mathf.Clamp(Mathf.RoundToInt(hitUV.x * Common.TexSize), 0, Common.TexSize - 1);
                        int dataY = Mathf.Clamp(Mathf.RoundToInt((1f - hitUV.y) * Common.TexSize), 0, Common.TexSize - 1);
                        if (!maskState.IsEffectiveSelected(dataX, dataY))
                            continue;
                    }

                    if (hit.distance < minDistance)
                    {
                        minDistance = hit.distance;
                        closestUV = hit.textureCoord;
                        closestWorldPos = hit.point;
                        closestNormal = hit.normal;
                        closestRenderer = renderer;
                        closestSubmeshIndex = submeshIndex;
                        closestTriangleIndex = hit.triangleIndex;
                    }
                }
            }

            if (closestUV.HasValue && closestRenderer != null)
            {
                CalculateTangentSpace(closestRenderer, closestTriangleIndex, out closestTangent, out closestBitangent);
                Vector2 dataCoord = new Vector2(
                    closestUV.Value.x * Common.TexSize,
                    (1f - closestUV.Value.y) * Common.TexSize
                );
                return new StrokePoint
                {
                    dataCoord = dataCoord,
                    uvCoord = closestUV.Value,
                    worldPosition = closestWorldPos,
                    tangent = closestTangent,
                    bitangent = closestBitangent,
                    normal = closestNormal,
                    rayDirection = ray.direction,
                    renderer = closestRenderer,
                    submeshIndex = closestSubmeshIndex
                };
            }

            return null;
        }

        private void CalculateTangentSpace(Renderer renderer, int triangleIndex, out Vector3 tangent, out Vector3 bitangent)
        {
            tangent = Vector3.right;
            bitangent = Vector3.up;

            if (triangleIndex < 0)
                return;

            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer)
            {
                // キャッシュからBakedMeshを取得
                bakedMeshCache.TryGetValue(renderer, out mesh);
            }
            else if (renderer.TryGetComponent<MeshFilter>(out var mf))
            {
                mesh = mf.sharedMesh;
            }

            if (mesh == null)
                return;

            var vertices = mesh.vertices;
            var uvs = mesh.uv;
            var triangles = mesh.triangles;

            if (vertices == null || uvs == null || triangles == null)
                return;

            int baseIdx = triangleIndex * 3;
            if (baseIdx + 2 >= triangles.Length)
                return;

            int idx0 = triangles[baseIdx];
            int idx1 = triangles[baseIdx + 1];
            int idx2 = triangles[baseIdx + 2];

            if (idx0 >= vertices.Length || idx1 >= vertices.Length || idx2 >= vertices.Length)
                return;
            if (idx0 >= uvs.Length || idx1 >= uvs.Length || idx2 >= uvs.Length)
                return;

            Vector3 v0 = vertices[idx0];
            Vector3 v1 = vertices[idx1];
            Vector3 v2 = vertices[idx2];
            Vector2 uv0 = uvs[idx0];
            Vector2 uv1 = uvs[idx1];
            Vector2 uv2 = uvs[idx2];

            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector2 deltaUV1 = uv1 - uv0;
            Vector2 deltaUV2 = uv2 - uv0;

            float det = deltaUV1.x * deltaUV2.y - deltaUV2.x * deltaUV1.y;
            if (Mathf.Abs(det) < 1e-6f)
                return;

            float f = 1.0f / det;

            Vector3 localTangent = new Vector3(
                f * (deltaUV2.y * edge1.x - deltaUV1.y * edge2.x),
                f * (deltaUV2.y * edge1.y - deltaUV1.y * edge2.y),
                f * (deltaUV2.y * edge1.z - deltaUV1.y * edge2.z)
            ).normalized;

            Vector3 localBitangent = new Vector3(
                f * (-deltaUV2.x * edge1.x + deltaUV1.x * edge2.x),
                f * (-deltaUV2.x * edge1.y + deltaUV1.x * edge2.y),
                f * (-deltaUV2.x * edge1.z + deltaUV1.x * edge2.z)
            ).normalized;

            tangent = renderer.transform.TransformDirection(localTangent);
            bitangent = renderer.transform.TransformDirection(localBitangent);
        }

        private bool ArePointsOnSameUVIsland(StrokePoint point1, StrokePoint point2)
        {
            if (point1.renderer != point2.renderer || point1.submeshIndex != point2.submeshIndex)
                return false;

            float dataDistance = Vector2.Distance(point1.dataCoord, point2.dataCoord);
            float threshold = state.BrushSize * 4f;

            return dataDistance <= threshold;
        }

        #endregion

        #region ストローク適用処理

        private void ApplyStrokeFromUVTrail()
        {
            if (dragTrail.Count < 2)
                return;

            var points = new List<Vector2>(dragTrail.Count);
            foreach (var point in dragTrail)
            {
                points.Add(point.dataCoord);
            }

            float? uvSpaceRadians = CalculateStrokeDirectionInUVSpace();

            // ストロークの全ポイントから連続するUV領域を抽出してマージしたマスクを生成
            // これにより、ストロークが複数のUVアイランドにまたがる場合（体→腕など）も正しく処理される
            // 同時に、UV空間で近くにあってもストローク上にない別のUVアイランドには塗られない
            int maxRange = state.BrushSize * 3; // 検索範囲をブラシサイズの3倍に制限
            
            bool[,] strokeSpecificMask = null;
            RectInt strokeMaskBounds = new RectInt(0, 0, 0, 0);

            try
            {
                // UV内のみ編集する設定がオンの場合のみマスクを構築
                if (state.RestrictEditToUvRegion)
                {
                    strokeSpecificMask = BuildStrokeMask(maxRange, out strokeMaskBounds);
                }

                // 元のストロークを処理（ミラーは手動で処理するため無効）
                strokeExecutor.ExecuteStrokeWithDirectionAndUvMask(
                    points,
                    maskState,
                    mirrorEnabled: false,  // ミラーは下で手動処理
                    ui.EraserMode,
                    ui.BlurMode,
                    ui.PinchMode,
                    ui.InclinedOnly,
                    ui.DirOnly,
                    ui.PinchInverted,
                    uvSpaceRadians,
                    strokeSpecificMask,
                    strokeMaskBounds.width > 0 ? strokeMaskBounds : (RectInt?)null,
                    allowUvMaskCache: false);

                // ミラー処理（有効かつ初期化済みの場合）
                if (ui.MirrorEnabled && furDataManager.IsMirrorInitialized)
                {
                    ApplyMirrorStroke(points, uvSpaceRadians, maxRange);
                }

                ApplyStrokeToNearbyUVIslands(uvSpaceRadians);
            }
            finally
            {
                if (strokeSpecificMask != null)
                {
                    MaskBufferPool.Return(strokeSpecificMask);
                }
            }

            repaintCallback?.Invoke();
            // Sceneビューの毛の線もリアルタイムで更新
            SceneView.RepaintAll();
        }

        /// <summary>
        /// ミラー座標用のストロークを適用
        /// </summary>
        private void ApplyMirrorStroke(List<Vector2> originalPoints, float? uvSpaceRadians, int maxRange)
        {
            ExecuteMirrorStrokeInternal(originalPoints, uvSpaceRadians, maxRange);
        }

        /// <summary>
        /// ミラーストローク処理の共通実装
        /// </summary>
        /// <param name="sourcePoints">ソースポイント（Vector2座標）</param>
        /// <param name="sourceRadians">ソースのストローク方向（ラジアン）</param>
        /// <param name="maxRange">マスク生成の検索範囲</param>
        private void ExecuteMirrorStrokeInternal(List<Vector2> sourcePoints, float? sourceRadians, int maxRange)
        {
            // ソースポイントをVector2Intに変換
            var sourceDataCoords = new List<Vector2Int>(sourcePoints.Count);
            foreach (var point in sourcePoints)
            {
                sourceDataCoords.Add(new Vector2Int(
                    Mathf.RoundToInt(point.x),
                    Mathf.RoundToInt(point.y)));
            }

            // ミラー座標を取得
            var mirrorCoordsList = furDataManager.GetMirrorDataCoords(sourceDataCoords);
            if (mirrorCoordsList.Count == 0)
                return;

            // ミラー座標をVector2リストに変換
            var mirrorPoints = new List<Vector2>(mirrorCoordsList.Count);
            foreach (var coord in mirrorCoordsList)
            {
                mirrorPoints.Add(new Vector2(coord.x, coord.y));
            }

            // ミラー座標用のマスクを生成（UV内のみ編集する設定がオンの場合のみ）
            var mirrorCoordsSet = new HashSet<Vector2Int>(mirrorCoordsList);
            bool[,] mirrorMask = null;
            RectInt mirrorMaskBounds = new RectInt(0, 0, 0, 0);
            try
            {
                if (state.RestrictEditToUvRegion)
                {
                    mirrorMask = BuildStrokeMaskForPoints(mirrorCoordsSet, maxRange, out mirrorMaskBounds);
                }

                // ミラー側のストローク方向を計算（3D空間ベース）
                float mirrorRadians = 0f;
                bool mirrorDirectionValid = false;
                
                // dragTrailからタンジェント情報を取得して3D空間ベースの計算を試みる
                if (dragTrail.Count > 0 && sourceRadians.HasValue)
                {
                    // 最新のストロークポイントからタンジェント情報を取得
                    var lastStrokePoint = dragTrail[dragTrail.Count - 1];
                    Vector2 srcUV = lastStrokePoint.uvCoord;
                    Vector3 srcTangent = lastStrokePoint.tangent;
                    Vector3 srcBitangent = lastStrokePoint.bitangent;
                    
                    // VertexSymmetryMapperを取得して3D空間ベースの計算を行う
                    var mapper = furDataManager.GetVertexSymmetryMapper();
                    if (mapper != null && mapper.TryCalculateMirrorDirectionVia3D(
                        srcUV, sourceRadians.Value, srcTangent, srcBitangent, out mirrorRadians))
                    {
                        mirrorDirectionValid = true;
                    }
                }
                
                if (!mirrorDirectionValid)
                    return;

                // 無効（NaN/Infinity）になった場合は何もしない
                if (float.IsNaN(mirrorRadians) || float.IsInfinity(mirrorRadians))
                {
                    return;
                }

                // ミラー座標でストロークを実行（ライン補間あり、元のストロークと同じ密度）
                strokeExecutor.ExecuteAtPointsWithLineInterpolation(
                    mirrorPoints,
                    maskState,
                    ui.EraserMode,
                    ui.BlurMode,
                    ui.PinchMode,
                    ui.InclinedOnly,
                    ui.DirOnly,
                    ui.PinchInverted,
                    mirrorRadians,
                    mirrorMask,
                    mirrorMaskBounds.width > 0 ? mirrorMaskBounds : (RectInt?)null,
                    allowUvMaskCache: false);
            }
            finally
            {
                if (mirrorMask != null)
                {
                    MaskBufferPool.Return(mirrorMask);
                }
            }
        }

        /// <summary>
        /// 指定されたポイント群からストロークマスクを生成
        /// </summary>
        private bool[,] BuildStrokeMaskForPoints(IEnumerable<Vector2Int> dataCoords, int maxRange, out RectInt strokeBounds)
        {
            bool[,] mergedMask = MaskBufferPool.Rent();
            var processedPoints = new HashSet<Vector2Int>();
            float proximityThreshold = maxRange * 0.5f;
            int minX = Common.TexSize;
            int minY = Common.TexSize;
            int maxX = -1;
            int maxY = -1;

            // 一時バッファをプールから取得
            bool[,] tempRegionMask = MaskBufferPool.Rent();

            try
            {
                foreach (var coord in dataCoords)
                {
                    int x = Mathf.Clamp(coord.x, 0, Common.TexSize - 1);
                    int y = Mathf.Clamp(coord.y, 0, Common.TexSize - 1);
                    var clampedCoord = new Vector2Int(x, y);

                    // 既に処理済みのポイントに近い場合はスキップ
                    if (IsNearProcessedPoint(clampedCoord, processedPoints, proximityThreshold))
                        continue;

                    processedPoints.Add(clampedCoord);

                    // クリアして連続領域を抽出
                    int clearMinX = Mathf.Max(0, x - maxRange);
                    int clearMaxX = Mathf.Min(Common.TexSize - 1, x + maxRange);
                    int clearMinY = Mathf.Max(0, y - maxRange);
                    int clearMaxY = Mathf.Min(Common.TexSize - 1, y + maxRange);
                    MaskBufferPool.ClearRange(tempRegionMask, clearMinX, clearMaxX, clearMinY, clearMaxY);

                    furRenderer.ExtractConnectedRegion(x, y, maxRange, tempRegionMask);
                    var localBounds = new RectInt(clearMinX, clearMinY, clearMaxX - clearMinX + 1, clearMaxY - clearMinY + 1);
                    UvRegionMaskUtils.MergeMasks(mergedMask, tempRegionMask, localBounds);
                    minX = Math.Min(minX, clearMinX);
                    minY = Math.Min(minY, clearMinY);
                    maxX = Math.Max(maxX, clearMaxX + 1);
                    maxY = Math.Max(maxY, clearMaxY + 1);
                }
            }
            finally
            {
                MaskBufferPool.Return(tempRegionMask);
            }

            if (maxX < 0 || maxY < 0)
            {
                strokeBounds = new RectInt(0, 0, 0, 0);
            }
            else
            {
                strokeBounds = new RectInt(minX, minY, maxX - minX, maxY - minY);
            }

            return mergedMask;
        }

        /// <summary>
        /// ストロークの全ポイントから連続するUV領域を抽出してマージしたマスクを生成
        /// </summary>
        private bool[,] BuildStrokeMask(int maxRange, out RectInt strokeBounds)
        {
            // dragTrailからサンプリングした座標を抽出
            var sampledCoords = SampleDragTrailCoords();
            return BuildStrokeMaskForPoints(sampledCoords, maxRange, out strokeBounds);
        }

        /// <summary>
        /// dragTrailから一定間隔でサンプリングした座標を取得
        /// </summary>
        private IEnumerable<Vector2Int> SampleDragTrailCoords()
        {
            if (dragTrail.Count == 0)
                yield break;

            // サンプリングするインデックスを決定（一定間隔 + 最後のポイント）
            int step = Mathf.Max(1, dragTrail.Count / 5);
            var processedIndices = new HashSet<int>();
            
            for (int i = 0; i < dragTrail.Count; i += step)
            {
                processedIndices.Add(i);
                var point = dragTrail[i];
                yield return new Vector2Int(
                    Mathf.RoundToInt(point.dataCoord.x),
                    Mathf.RoundToInt(point.dataCoord.y));
            }
            
            // 最後のポイントも必ず含める
            int lastIndex = dragTrail.Count - 1;
            if (dragTrail.Count > 1 && !processedIndices.Contains(lastIndex))
            {
                var lastPoint = dragTrail[lastIndex];
                yield return new Vector2Int(
                    Mathf.RoundToInt(lastPoint.dataCoord.x),
                    Mathf.RoundToInt(lastPoint.dataCoord.y));
            }
        }

        /// <summary>
        /// 指定座標が処理済みポイントの近くにあるかチェック
        /// </summary>
        private static bool IsNearProcessedPoint(Vector2Int coord, HashSet<Vector2Int> processedPoints, float threshold)
        {
            foreach (var processed in processedPoints)
            {
                if (Vector2Int.Distance(coord, processed) < threshold)
                    return true;
            }
            return false;
        }

        // 空間検索結果のバッファ（GC削減のため再利用）
        private readonly List<int> spatialQueryBuffer = new List<int>(256);

        private void ApplyStrokeToNearbyUVIslands(float? uvSpaceRadians)
        {
            if (dragTrail.Count < 1 || furRenderer.SamplePoints.Count == 0)
                return;

            float worldBrushRadius = furRenderer.BrushCursorWorldRadius;
            if (worldBrushRadius <= 0f)
                worldBrushRadius = 0.05f;

            var originalDataCoords = new HashSet<Vector2Int>();
            int brushSize = state.BrushSize;
            foreach (var point in dragTrail)
            {
                int px = Mathf.RoundToInt(point.dataCoord.x);
                int py = Mathf.RoundToInt(point.dataCoord.y);
                for (int dy = -brushSize; dy <= brushSize; dy++)
                {
                    for (int dx = -brushSize; dx <= brushSize; dx++)
                    {
                        if (dx * dx + dy * dy <= brushSize * brushSize)
                        {
                            originalDataCoords.Add(new Vector2Int(px + dx, py + dy));
                        }
                    }
                }
            }

            var excludedDataCoords = new HashSet<Vector2Int>(originalDataCoords);
            if (ui.MirrorEnabled && furDataManager.IsMirrorInitialized)
            {
                var strokeCenterCoords = new List<Vector2Int>(dragTrail.Count);
                foreach (var point in dragTrail)
                {
                    strokeCenterCoords.Add(new Vector2Int(
                        Mathf.RoundToInt(point.dataCoord.x),
                        Mathf.RoundToInt(point.dataCoord.y)));
                }

                var mirrorCenterCoords = furDataManager.GetMirrorDataCoords(strokeCenterCoords);
                foreach (var mirrorCoord in mirrorCenterCoords)
                {
                    for (int dy = -brushSize; dy <= brushSize; dy++)
                    {
                        for (int dx = -brushSize; dx <= brushSize; dx++)
                        {
                            if (dx * dx + dy * dy <= brushSize * brushSize)
                            {
                                excludedDataCoords.Add(new Vector2Int(mirrorCoord.x + dx, mirrorCoord.y + dy));
                            }
                        }
                    }
                }
            }

            var additionalPoints = new Dictionary<Vector2Int, (SceneViewFurRenderer.SamplePoint sample, float influence)>();
            var allSamplePoints = furRenderer.SamplePoints;

            // 空間グリッドを使用した高速な近傍検索
            // 法線角度のしきい値（cos値）: 0.0 = 90度、0.5 = 60度
            const float normalAngleThreshold = 0.0f;
            
            foreach (var strokePoint in dragTrail)
            {
                spatialQueryBuffer.Clear();
                furRenderer.FindSamplePointsInRadius(strokePoint.worldPosition, worldBrushRadius, spatialQueryBuffer);

                foreach (int sampleIdx in spatialQueryBuffer)
                {
                    var sample = allSamplePoints[sampleIdx];
                    
                    // チェック1: 法線方向のチェック
                    // ストローク点の法線と対象サンプルの法線が大きく異なる場合は除外
                    // これにより、尻尾の先端など細い部分で反対側の面が影響を受けることを防ぐ
                    float normalDot = Vector3.Dot(strokePoint.normal, sample.normal);
                    if (normalDot < normalAngleThreshold)
                        continue;
                    
                    // チェック2: 視線方向チェック
                    // レイの方向と対象サンプルの法線が同じ向き（裏面）の場合は除外
                    // 内積が負 = 法線がカメラの方を向いている（表面）→ OK
                    // 内積が正 = 法線がカメラと反対向き（裏面）→ 除外
                    float viewDot = Vector3.Dot(strokePoint.rayDirection, sample.normal);
                    if (viewDot >= 0f)
                        continue;
                    
                    float worldDistance = Vector3.Distance(strokePoint.worldPosition, sample.worldPosition);
                    
                    // FindSamplePointsInRadiusは近似なので、正確な距離チェックを行う
                    if (worldDistance <= worldBrushRadius)
                    {
                        int dataX = Mathf.Clamp(Mathf.RoundToInt(sample.uv.x * Common.TexSize), 0, Common.TexSize - 1);
                        int dataY = Mathf.Clamp(Mathf.RoundToInt((1f - sample.uv.y) * Common.TexSize), 0, Common.TexSize - 1);
                        Vector2Int dataCoord = new Vector2Int(dataX, dataY);

                        if (!excludedDataCoords.Contains(dataCoord))
                        {
                            float normalizedDist = worldDistance / worldBrushRadius;
                            float influence = 1f - normalizedDist * normalizedDist;

                            if (!additionalPoints.TryGetValue(dataCoord, out var existing) || existing.influence < influence)
                            {
                                additionalPoints[dataCoord] = (sample, influence);
                            }
                        }
                    }
                }
            }

            if (additionalPoints.Count == 0)
                return;

            var clusters = ClusterAdditionalPointsWithInfluence(additionalPoints, brushSize);
            if (clusters.Count == 0)
                return;

            // プールからバッファを取得（ループ内で再利用）
            bool[,] clusterMaskBuffer = MaskBufferPool.Rent();
            bool[,] combinedMaskBuffer = MaskBufferPool.Rent();

            try
            {
                foreach (var cluster in clusters)
                {
                    if (cluster.Count < 1)
                        continue;

                    var clusterPoints = new List<Vector2>();
                    Vector2 clusterCenter = Vector2.zero;
                    Vector3 avgTangent = Vector3.zero;
                    Vector3 avgBitangent = Vector3.zero;

                    foreach (var kvp in cluster)
                    {
                        clusterCenter += new Vector2(kvp.Key.x, kvp.Key.y);
                        avgTangent += kvp.Value.sample.tangent;
                        avgBitangent += kvp.Value.sample.bitangent;
                    }
                    clusterCenter /= cluster.Count;
                    avgTangent /= cluster.Count;
                    avgBitangent /= cluster.Count;

                    Vector3 worldDirection = Vector3.zero;
                    for (int i = 1; i < dragTrail.Count; i++)
                    {
                        worldDirection += dragTrail[i].worldPosition - dragTrail[i - 1].worldPosition;
                    }
                    if (dragTrail.Count > 1)
                        worldDirection /= (dragTrail.Count - 1);

                    float? clusterRadians = null;
                    if (worldDirection.sqrMagnitude > 1e-10f && avgTangent.sqrMagnitude > 1e-6f && avgBitangent.sqrMagnitude > 1e-6f)
                    {
                        avgTangent = avgTangent.normalized;
                        avgBitangent = avgBitangent.normalized;
                        float uComponent = Vector3.Dot(worldDirection, avgTangent);
                        float vComponent = -Vector3.Dot(worldDirection, avgBitangent);
                        if (uComponent * uComponent + vComponent * vComponent > 1e-10f)
                        {
                            clusterRadians = Mathf.Atan2(vComponent, uComponent);
                        }
                    }

                    if (!clusterRadians.HasValue)
                        clusterRadians = uvSpaceRadians;

                    float offset = brushSize * 0.5f;
                    Vector2 dir = clusterRadians.HasValue
                        ? new Vector2(Mathf.Cos(clusterRadians.Value), Mathf.Sin(clusterRadians.Value))
                        : Vector2.right;

                    clusterPoints.Add(clusterCenter - dir * offset);
                    clusterPoints.Add(clusterCenter + dir * offset);

                    // バッファをクリアして再利用
                    System.Array.Clear(clusterMaskBuffer, 0, clusterMaskBuffer.Length);
                    CreateClusterMask(cluster, brushSize, clusterMaskBuffer);

                    // クラスタ領域のAABBを計算して結合範囲を絞る
                    int minX = Common.TexSize;
                    int minY = Common.TexSize;
                    int maxX = -1;
                    int maxY = -1;
                    foreach (var kvp in cluster)
                    {
                        minX = Math.Min(minX, kvp.Key.x - brushSize);
                        minY = Math.Min(minY, kvp.Key.y - brushSize);
                        maxX = Math.Max(maxX, kvp.Key.x + brushSize);
                        maxY = Math.Max(maxY, kvp.Key.y + brushSize);
                    }

                    RectInt? clusterBounds = null;
                    if (maxX >= 0 && maxY >= 0)
                    {
                        int clampedMinX = Mathf.Clamp(minX, 0, Common.TexSize - 1);
                        int clampedMinY = Mathf.Clamp(minY, 0, Common.TexSize - 1);
                        int clampedMaxX = Mathf.Clamp(maxX, 0, Common.TexSize - 1);
                        int clampedMaxY = Mathf.Clamp(maxY, 0, Common.TexSize - 1);
                        clusterBounds = new RectInt(clampedMinX, clampedMinY, (clampedMaxX - clampedMinX) + 1, (clampedMaxY - clampedMinY) + 1);
                    }

                    System.Array.Clear(combinedMaskBuffer, 0, combinedMaskBuffer.Length);

                    // UV内のみ編集する設定がオフの場合はUV領域マスクを無効化
                    var effectiveUvRegionMask = state.RestrictEditToUvRegion ? furRenderer.UvRegionMask : null;

                    UvRegionMaskUtils.CombineMasks(
                        effectiveUvRegionMask,
                        clusterMaskBuffer,
                        clusterBounds,
                        combinedMaskBuffer);

                    // クラスタのストロークを実行（ミラーは手動処理のため無効）
                    strokeExecutor.ExecuteStrokeWithDirectionAndUvMask(
                        clusterPoints,
                        maskState,
                        mirrorEnabled: false,
                        ui.EraserMode,
                        ui.BlurMode,
                        ui.PinchMode,
                        ui.InclinedOnly,
                        ui.DirOnly,
                        ui.PinchInverted,
                        clusterRadians,
                        combinedMaskBuffer);

                    // クラスタのミラー処理（有効かつ初期化済みの場合）
                    if (ui.MirrorEnabled && furDataManager.IsMirrorInitialized)
                    {
                        ApplyClusterMirrorStroke(clusterPoints, clusterRadians, brushSize);
                    }
                }
            }
            finally
            {
                // バッファをプールに返却
                MaskBufferPool.Return(clusterMaskBuffer);
                MaskBufferPool.Return(combinedMaskBuffer);
            }
        }

        /// <summary>
        /// クラスタのミラー座標用のストロークを適用
        /// </summary>
        private void ApplyClusterMirrorStroke(List<Vector2> clusterPoints, float? clusterRadians, int brushSize)
        {
            int maxRange = brushSize * 3;
            ExecuteMirrorStrokeInternal(clusterPoints, clusterRadians, maxRange);
        }

        private bool[,] CreateClusterMask(Dictionary<Vector2Int, (SceneViewFurRenderer.SamplePoint sample, float influence)> cluster, int brushSize, bool[,] outputBuffer = null)
        {
            bool[,] mask = outputBuffer ?? MaskBufferPool.Rent();

            foreach (var kvp in cluster)
            {
                Vector2Int coord = kvp.Key;
                float influence = kvp.Value.influence;
                int range = Mathf.Max(1, Mathf.RoundToInt(brushSize * 0.5f * influence));

                for (int dy = -range; dy <= range; dy++)
                {
                    for (int dx = -range; dx <= range; dx++)
                    {
                        if (dx * dx + dy * dy <= range * range)
                        {
                            int nx = coord.x + dx;
                            int ny = coord.y + dy;
                            if (nx >= 0 && nx < Common.TexSize && ny >= 0 && ny < Common.TexSize)
                            {
                                mask[nx, ny] = true;
                            }
                        }
                    }
                }
            }

            return mask;
        }


        private List<Dictionary<Vector2Int, (SceneViewFurRenderer.SamplePoint sample, float influence)>> ClusterAdditionalPointsWithInfluence(
            Dictionary<Vector2Int, (SceneViewFurRenderer.SamplePoint sample, float influence)> points, int brushSize)
        {
            var clusters = new List<Dictionary<Vector2Int, (SceneViewFurRenderer.SamplePoint sample, float influence)>>();
            if (points.Count == 0)
                return clusters;

            float clusterThreshold = brushSize * 2f;
            
            // 空間グリッドを使用した高速近傍検索（O(n²) → O(n) に改善）
            int cellSize = Mathf.Max(1, Mathf.CeilToInt(clusterThreshold));
            var spatialGrid = new Dictionary<Vector2Int, List<Vector2Int>>();
            
            // 全ポイントをグリッドに登録
            foreach (var kvp in points)
            {
                var cellKey = new Vector2Int(kvp.Key.x / cellSize, kvp.Key.y / cellSize);
                if (!spatialGrid.TryGetValue(cellKey, out var cellList))
                {
                    cellList = new List<Vector2Int>();
                    spatialGrid[cellKey] = cellList;
                }
                cellList.Add(kvp.Key);
            }

            var visited = new HashSet<Vector2Int>();
            float thresholdSq = clusterThreshold * clusterThreshold;

            foreach (var kvp in points)
            {
                if (visited.Contains(kvp.Key))
                    continue;

                var cluster = new Dictionary<Vector2Int, (SceneViewFurRenderer.SamplePoint sample, float influence)>();
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(kvp.Key);
                visited.Add(kvp.Key);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    cluster[current] = points[current];

                    // 近傍セルのみを検索（最大9セル）
                    var currentCell = new Vector2Int(current.x / cellSize, current.y / cellSize);
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            var neighborCell = new Vector2Int(currentCell.x + dx, currentCell.y + dy);
                            if (!spatialGrid.TryGetValue(neighborCell, out var cellPoints))
                                continue;

                            foreach (var other in cellPoints)
                            {
                                if (visited.Contains(other))
                                    continue;

                                // 距離の二乗で比較（sqrtを避ける）
                                float distSq = (current.x - other.x) * (current.x - other.x) +
                                               (current.y - other.y) * (current.y - other.y);
                                if (distSq <= thresholdSq)
                                {
                                    queue.Enqueue(other);
                                    visited.Add(other);
                                }
                            }
                        }
                    }
                }

                if (cluster.Count > 0)
                {
                    clusters.Add(cluster);
                }
            }

            return clusters;
        }

        private float? CalculateStrokeDirectionInUVSpace()
        {
            if (dragTrail.Count < 2)
                return null;

            Vector3 worldDirection = Vector3.zero;
            for (int i = 1; i < dragTrail.Count; i++)
            {
                worldDirection += dragTrail[i].worldPosition - dragTrail[i - 1].worldPosition;
            }
            worldDirection /= (dragTrail.Count - 1);

            if (worldDirection.sqrMagnitude < 1e-10f)
                return null;

            var latestPoint = dragTrail[dragTrail.Count - 1];
            Vector3 tangent = latestPoint.tangent;
            Vector3 bitangent = latestPoint.bitangent;

            if (tangent.sqrMagnitude < 1e-6f || bitangent.sqrMagnitude < 1e-6f)
                return null;

            tangent = tangent.normalized;
            bitangent = bitangent.normalized;

            float uComponent = Vector3.Dot(worldDirection, tangent);
            float vComponent = Vector3.Dot(worldDirection, bitangent);

            if (uComponent * uComponent + vComponent * vComponent < 1e-10f)
                return null;

            vComponent = -vComponent;

            float radians = Mathf.Atan2(vComponent, uComponent);
            return radians;
        }

        #endregion
    }
}
