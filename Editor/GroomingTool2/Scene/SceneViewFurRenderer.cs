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
    /// Sceneビューでの毛の描画とサンプルポイント管理を担当するクラス
    /// </summary>
    internal sealed class SceneViewFurRenderer
    {
        private readonly GroomingTool2State state;
        private readonly FurDataManager furDataManager;
        private readonly UvIslandMaskState maskState;

        // サンプル点キャッシュ
        private readonly List<SamplePoint> samplePoints = new();
        private MaterialEntry? cachedMaterialEntry;
        private int cachedSceneViewInterval = -1;
        private bool cacheDirty = true;

        // UV領域マスク（パディング適用済み）
        private bool[,] uvRegionMask;
        private int cachedUvPadding = -1;

        // 平均UV-ワールド変換率（ブラシカーソルサイズ用）
        private float averageUvToWorldScale = 1f;

        // ブラシカーソル表示用
        private Vector3 brushCursorPosition;
        private Vector3 brushCursorNormal;
        private float brushCursorWorldRadius;
        private bool brushCursorValid;

        // 深度テスト用のマテリアル（Lazy初期化）
        private static Material lineMaterial;


        // 空間グリッド（高速な近傍検索用）
        private const float SpatialGridCellSize = 0.05f; // グリッドセルサイズ（ワールド単位）
        private Dictionary<Vector3Int, List<int>> spatialGrid;
        private Vector3 spatialGridMin;
        private Vector3 spatialGridMax;
        private readonly Queue<Vector2Int> floodFillQueue = new Queue<Vector2Int>(1024);

        /// <summary>
        /// サンプル点の情報
        /// </summary>
        public struct SamplePoint
        {
            public Vector3 worldPosition;
            public Vector2 uv;
            public Vector3 tangent;    // UV空間のU方向に対応するワールド方向
            public Vector3 bitangent;  // UV空間のV方向に対応するワールド方向
            public Vector3 normal;     // 表面の法線方向
            public Renderer renderer;
            public int submeshIndex;
        }

        public IReadOnlyList<SamplePoint> SamplePoints => samplePoints;
        public bool[,] UvRegionMask => uvRegionMask;
        public float AverageUvToWorldScale => averageUvToWorldScale;
        public Vector3 BrushCursorPosition => brushCursorPosition;
        public Vector3 BrushCursorNormal => brushCursorNormal;
        public float BrushCursorWorldRadius => brushCursorWorldRadius;
        public bool BrushCursorValid => brushCursorValid;

        public SceneViewFurRenderer(GroomingTool2State state, FurDataManager furDataManager, UvIslandMaskState maskState)
        {
            this.state = state ?? throw new System.ArgumentNullException(nameof(state));
            this.furDataManager = furDataManager ?? throw new System.ArgumentNullException(nameof(furDataManager));
            this.maskState = maskState ?? throw new System.ArgumentNullException(nameof(maskState));
        }

        /// <summary>
        /// マテリアルが変更された時に呼び出す
        /// </summary>
        public void OnMaterialChanged()
        {
            cacheDirty = true;
        }

        /// <summary>
        /// キャッシュを更新する（必要な場合のみ）
        /// </summary>
        public void UpdateCacheIfNeeded(MaterialEntry? selectedMaterial)
        {
            if (!selectedMaterial.HasValue)
                return;

            var currentInterval = state.SceneViewDisplayInterval;
            var currentPadding = state.UvPadding;

            if (cacheDirty || !cachedMaterialEntry.HasValue ||
                cachedMaterialEntry.Value.texture != selectedMaterial.Value.texture ||
                cachedSceneViewInterval != currentInterval ||
                cachedUvPadding != currentPadding)
            {
                RebuildSamplePointsCache(selectedMaterial.Value);
                RebuildUvRegionMask(selectedMaterial.Value, currentPadding);
                cachedMaterialEntry = selectedMaterial.Value;
                cachedSceneViewInterval = currentInterval;
                cachedUvPadding = currentPadding;
                cacheDirty = false;
            }
        }

        /// <summary>
        /// 毛の向きを描画
        /// </summary>
        /// <param name="sceneView">描画対象のSceneView（視錐台カリング用）</param>
        public void DrawHairDirections(SceneView sceneView = null)
        {
            if (samplePoints.Count == 0)
                return;

            // Repaintイベント以外では描画しない
            if (Event.current.type != EventType.Repaint)
                return;

            var furData = furDataManager.Data;
            const float lineLength = 0.02f;

            // 視錐台カリング用のカメラとプレーンを取得
            Camera camera = sceneView?.camera;
            Plane[] frustumPlanes = null;
            Vector3 cameraForward = Vector3.forward;
            if (camera != null)
            {
                frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
                cameraForward = camera.transform.forward;
            }

            // カメラ方向へのオフセット量（ファーマテリアルより手前に描画するため）
            const float depthOffset = 0.01f;

            // GL APIで一括描画（Handles.DrawLineより高速）
            // 深度テストを有効にしたマテリアルを使用
            var mat = GetLineMaterial();
            mat.SetPass(0);

            GL.PushMatrix();
            GL.Begin(GL.LINES);
            GL.Color(state.SceneViewHairColor); // 毛の色（設定から取得）

            // Scene毛密度（SceneViewDisplayInterval）に依存し、全サンプルを描画
            for (int i = 0; i < samplePoints.Count; i++)
            {
                var sample = samplePoints[i];

                // レンダラーが非表示の場合はスキップ
                if (sample.renderer == null || !sample.renderer.enabled || !sample.renderer.gameObject.activeInHierarchy)
                    continue;

                // 視錐台カリング: カメラの視界外のポイントはスキップ
                if (frustumPlanes != null)
                {
                    // ポイントが視錐台内にあるかチェック（小さなマージンを持たせる）
                    bool isVisible = true;
                    for (int j = 0; j < 6; j++)
                    {
                        if (frustumPlanes[j].GetDistanceToPoint(sample.worldPosition) < -lineLength)
                        {
                            isVisible = false;
                            break;
                        }
                    }
                    if (!isVisible)
                        continue;
                }

                // UV座標をデータ座標に変換（Y軸を反転：UnityのUV座標v=0が下、データ座標y=0が上）
                int x = Mathf.Clamp(Mathf.RoundToInt(sample.uv.x * Common.TexSize), 0, Common.TexSize - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt((1f - sample.uv.y) * Common.TexSize), 0, Common.TexSize - 1);

                // マスクが適用されている場合、マスク範囲外のポイントはスキップ
                if (maskState != null && maskState.RestrictEditing && maskState.HasAnySelection)
                {
                    if (!maskState.IsEffectiveSelected(x, y))
                        continue;
                }

                int index = Common.GetIndex(x, y);
                var data = furData[index];

                // FurDataにデータがない場合は、法線方向（垂直に立った毛）で描画
                Vector3 worldDir;

                if (data.Inclined <= 0f)
                {
                    // 傾きがない場合は法線方向（表面に垂直）
                    worldDir = sample.normal.normalized;
                }
                else
                {
                    // 毛の向きを計算（傾きを考慮）
                    float cos = AngleLut.GetCos(data.Dir);
                    float sin = AngleLut.GetSin(data.Dir);
                    float inclined = data.Inclined;

                    // 傾きを角度（0～π/2）にマッピングして、1に近づくほど平面に沿うようにする
                    // inclined=0 → 垂直（法線方向）、inclined=1 → 完全に平面に沿う
                    float angle = Mathf.Clamp01(inclined) * Mathf.PI * 0.5f;
                    float tangentComponent = Mathf.Sin(angle);  // 接線方向の成分
                    float normalComponent = Mathf.Cos(angle);   // 法線方向の成分

                    // ワールド空間での方向を計算（タンジェント空間から変換）
                    worldDir = (sample.tangent * cos * tangentComponent
                              - sample.bitangent * sin * tangentComponent
                              + sample.normal * normalComponent).normalized;
                }

                Vector3 endPos = sample.worldPosition + worldDir * lineLength;

                // カメラ方向にオフセットを適用（ファーマテリアルより手前に描画）
                Vector3 offsetStartPos = sample.worldPosition - cameraForward * depthOffset;
                Vector3 offsetEndPos = endPos - cameraForward * depthOffset;

                // GL APIで線を描画
                GL.Vertex(offsetStartPos);
                GL.Vertex(offsetEndPos);
            }

            GL.End();
            GL.PopMatrix();
        }

        /// <summary>
        /// ブラシカーソルを描画
        /// </summary>
        public void DrawBrushCursor()
        {
            if (!brushCursorValid)
                return;

            // Repaintイベント以外では描画しない
            if (Event.current.type != EventType.Repaint)
                return;

            // ブラシ範囲を示す円を描画
            Handles.color = new Color(0f, 1f, 1f, 0.8f); // シアン色
            Handles.DrawWireDisc(brushCursorPosition, brushCursorNormal, brushCursorWorldRadius);

            // 内側にもう一つ薄い円を描画（見やすさ向上）
            Handles.color = new Color(0f, 1f, 1f, 0.3f);
            Handles.DrawSolidDisc(brushCursorPosition, brushCursorNormal, brushCursorWorldRadius * 0.1f);
        }

        /// <summary>
        /// ブラシカーソル状態を更新
        /// </summary>
        public void UpdateBrushCursorState(Vector3 position, Vector3 normal, float worldRadius, bool valid)
        {
            brushCursorPosition = position;
            brushCursorNormal = normal;
            brushCursorWorldRadius = worldRadius;
            brushCursorValid = valid;
        }

        /// <summary>
        /// ブラシサイズをワールド空間の半径に変換（平均UV-ワールド変換率を使用）
        /// </summary>
        public float CalculateWorldBrushRadius()
        {
            // ブラシサイズ（テクスチャピクセル単位）をワールド空間に変換
            // ブラシサイズはTexSize（1024）ピクセル中の半径なので、UV空間では brushSize / TexSize
            float brushSizeInUV = state.BrushSize / (float)Common.TexSize;
            float worldRadius = brushSizeInUV * averageUvToWorldScale;

            return Mathf.Max(worldRadius, 0.001f);
        }

        /// <summary>
        /// サンプル点キャッシュを再構築
        /// </summary>
        private void RebuildSamplePointsCache(MaterialEntry materialEntry)
        {
            samplePoints.Clear();

            if (materialEntry.usages == null || materialEntry.usages.Count == 0)
            {
                Debug.LogWarning("[SceneViewFurRenderer] マテリアルエントリにusagesがありません");
                return;
            }

            var interval = state.SceneViewDisplayInterval;
            var uvSets = materialEntry.uvSets;
            var triangleSets = materialEntry.triangleSets;

            if (uvSets == null || triangleSets == null || uvSets.Count != triangleSets.Count)
            {
                Debug.LogWarning($"[SceneViewFurRenderer] UVセットまたは三角形セットが不正: uvSets={uvSets?.Count ?? 0}, triangleSets={triangleSets?.Count ?? 0}");
                return;
            }

            // 平均UV-ワールド変換率を計算するための統計
            float totalUvToWorldScale = 0f;
            int scaleCount = 0;

            // 各レンダラー/サブメッシュからサンプル点を収集
            for (int usageIndex = 0; usageIndex < materialEntry.usages.Count; usageIndex++)
            {
                var (renderer, submeshIndex) = materialEntry.usages[usageIndex];
                if (renderer == null)
                    continue;

                // SkinnedMeshRendererの場合は最新姿勢を取得
                bool isBaked;
                Mesh mesh = EditorMeshUtils.GetMeshForRenderer(renderer, out isBaked);
                if (mesh == null)
                {
                    Debug.LogWarning($"[SceneViewFurRenderer] メッシュを取得できませんでした: renderer={renderer.name}, submesh={submeshIndex}");
                    continue;
                }

                try
                {
                    // メッシュから直接UVと三角形を取得（サブメッシュ対応）
                    Vector2[] meshUVs = mesh.uv;
                    int[] meshTriangles = mesh.GetTriangles(submeshIndex);

                    if (meshUVs != null && meshTriangles != null && meshTriangles.Length >= 3)
                    {
                        AddSamplePointsFromMesh(mesh, meshUVs, meshTriangles, renderer, submeshIndex, interval,
                            ref totalUvToWorldScale, ref scaleCount);
                    }
                    else
                    {
                        Debug.LogWarning($"[SceneViewFurRenderer] メッシュデータが不正: renderer={renderer.name}, submesh={submeshIndex}, uvs={meshUVs?.Length ?? 0}, triangles={meshTriangles?.Length ?? 0}");
                    }
                }
                finally
                {
                    if (isBaked)
                    {
                        Object.DestroyImmediate(mesh);
                    }
                }
            }

            // 平均UV-ワールド変換率を計算
            if (scaleCount > 0)
            {
                averageUvToWorldScale = totalUvToWorldScale / scaleCount;
            }
            else
            {
                averageUvToWorldScale = 1f;
            }

            // 空間グリッドを構築（高速な近傍検索用）
            RebuildSpatialGrid();
        }

        /// <summary>
        /// 空間グリッドを構築
        /// </summary>
        private void RebuildSpatialGrid()
        {
            spatialGrid = new Dictionary<Vector3Int, List<int>>();

            if (samplePoints.Count == 0)
                return;

            // バウンディングボックスを計算
            spatialGridMin = samplePoints[0].worldPosition;
            spatialGridMax = samplePoints[0].worldPosition;

            for (int i = 1; i < samplePoints.Count; i++)
            {
                var pos = samplePoints[i].worldPosition;
                spatialGridMin = Vector3.Min(spatialGridMin, pos);
                spatialGridMax = Vector3.Max(spatialGridMax, pos);
            }

            // 各サンプルポイントをグリッドに登録
            for (int i = 0; i < samplePoints.Count; i++)
            {
                var cellKey = WorldToGridCell(samplePoints[i].worldPosition);
                if (!spatialGrid.TryGetValue(cellKey, out var list))
                {
                    list = new List<int>();
                    spatialGrid[cellKey] = list;
                }
                list.Add(i);
            }
        }

        /// <summary>
        /// ワールド座標をグリッドセル座標に変換
        /// </summary>
        private Vector3Int WorldToGridCell(Vector3 worldPos)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / SpatialGridCellSize),
                Mathf.FloorToInt(worldPos.y / SpatialGridCellSize),
                Mathf.FloorToInt(worldPos.z / SpatialGridCellSize)
            );
        }

        /// <summary>
        /// 指定位置から半径内のサンプルポイントを検索（空間グリッドを使用した高速版）
        /// </summary>
        /// <param name="position">検索中心のワールド座標</param>
        /// <param name="radius">検索半径</param>
        /// <param name="results">結果を格納するリスト（クリアされません）</param>
        public void FindSamplePointsInRadius(Vector3 position, float radius, List<int> results)
        {
            if (spatialGrid == null || spatialGrid.Count == 0)
                return;

            float radiusSq = radius * radius;
            int cellRadius = Mathf.CeilToInt(radius / SpatialGridCellSize);
            var centerCell = WorldToGridCell(position);

            // 検索範囲内のセルをイテレート
            for (int dz = -cellRadius; dz <= cellRadius; dz++)
            {
                for (int dy = -cellRadius; dy <= cellRadius; dy++)
                {
                    for (int dx = -cellRadius; dx <= cellRadius; dx++)
                    {
                        var cellKey = new Vector3Int(centerCell.x + dx, centerCell.y + dy, centerCell.z + dz);

                        if (spatialGrid.TryGetValue(cellKey, out var indices))
                        {
                            foreach (int idx in indices)
                            {
                                float distSq = (samplePoints[idx].worldPosition - position).sqrMagnitude;
                                if (distSq <= radiusSq)
                                {
                                    results.Add(idx);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// メッシュからサンプル点を追加
        /// </summary>
        private void AddSamplePointsFromMesh(Mesh mesh, Vector2[] uvs, int[] triangles, Renderer renderer, int submeshIndex, int interval,
            ref float totalUvToWorldScale, ref int scaleCount)
        {
            if (mesh == null || uvs == null || triangles == null || triangles.Length < 3)
                return;

            var vertices = mesh.vertices;
            var normals = mesh.normals;

            if (vertices == null || vertices.Length == 0)
                return;

            // 三角形インデックス（interval個に1個サンプリングするため）
            int triangleIndex = 0;

            // 三角形ごとにサンプル点を追加
            for (int i = 0; i < triangles.Length; i += 3)
            {
                if (i + 2 >= triangles.Length)
                    break;

                int idx0 = triangles[i];
                int idx1 = triangles[i + 1];
                int idx2 = triangles[i + 2];

                if (idx0 >= vertices.Length || idx1 >= vertices.Length || idx2 >= vertices.Length)
                    continue;
                if (idx0 >= uvs.Length || idx1 >= uvs.Length || idx2 >= uvs.Length)
                    continue;

                Vector2 uv0 = uvs[idx0];
                Vector2 uv1 = uvs[idx1];
                Vector2 uv2 = uvs[idx2];

                // ワールド座標を計算
                Vector3 v0 = vertices[idx0];
                Vector3 v1 = vertices[idx1];
                Vector3 v2 = vertices[idx2];

                // UV-ワールド変換率を計算（全三角形で統計を取る）
                float uvEdge1 = (uv1 - uv0).magnitude;
                float uvEdge2 = (uv2 - uv0).magnitude;
                Vector3 worldV0 = renderer.transform.TransformPoint(v0);
                Vector3 worldV1 = renderer.transform.TransformPoint(v1);
                Vector3 worldV2 = renderer.transform.TransformPoint(v2);
                float worldEdge1 = (worldV1 - worldV0).magnitude;
                float worldEdge2 = (worldV2 - worldV0).magnitude;

                float avgUvEdge = (uvEdge1 + uvEdge2) * 0.5f;
                float avgWorldEdge = (worldEdge1 + worldEdge2) * 0.5f;
                if (avgUvEdge > 1e-6f)
                {
                    totalUvToWorldScale += avgWorldEdge / avgUvEdge;
                    scaleCount++;
                }

                // DisplayIntervalに基づいてサンプリング（interval個に1個）
                bool shouldSample = (interval <= 1) || (triangleIndex % interval == 0);
                triangleIndex++;

                if (!shouldSample)
                    continue;

                Vector3 centerPos = (v0 + v1 + v2) / 3f;

                // レンダラーのTransformを適用
                Vector3 worldPos = renderer.transform.TransformPoint(centerPos);

                // タンジェントとバイタンジェントを計算（UV座標の変化から）
                Vector3 edge1 = v1 - v0;
                Vector3 edge2 = v2 - v0;
                Vector2 deltaUV1 = uv1 - uv0;
                Vector2 deltaUV2 = uv2 - uv0;

                float det = deltaUV1.x * deltaUV2.y - deltaUV2.x * deltaUV1.y;
                if (Mathf.Abs(det) < 1e-6f)
                    continue; // 縮退した三角形はスキップ

                float f = 1.0f / det;

                // タンジェント（UV空間のU方向に対応するワールド方向）
                Vector3 tangent = new Vector3(
                    f * (deltaUV2.y * edge1.x - deltaUV1.y * edge2.x),
                    f * (deltaUV2.y * edge1.y - deltaUV1.y * edge2.y),
                    f * (deltaUV2.y * edge1.z - deltaUV1.y * edge2.z)
                ).normalized;

                // バイタンジェント（UV空間のV方向に対応するワールド方向）
                Vector3 bitangent = new Vector3(
                    f * (-deltaUV2.x * edge1.x + deltaUV1.x * edge2.x),
                    f * (-deltaUV2.x * edge1.y + deltaUV1.x * edge2.y),
                    f * (-deltaUV2.x * edge1.z + deltaUV1.x * edge2.z)
                ).normalized;

                // 法線を計算（メッシュの法線データがあれば使用、なければ三角形から計算）
                Vector3 normal;
                if (normals != null && idx0 < normals.Length && idx1 < normals.Length && idx2 < normals.Length)
                {
                    normal = ((normals[idx0] + normals[idx1] + normals[idx2]) / 3f).normalized;
                }
                else
                {
                    // 三角形の2辺のクロス積から法線を計算
                    normal = Vector3.Cross(edge1, edge2).normalized;
                }

                // レンダラーのTransformを適用
                tangent = renderer.transform.TransformDirection(tangent);
                bitangent = renderer.transform.TransformDirection(bitangent);
                normal = renderer.transform.TransformDirection(normal);

                Vector2 sampleUV = (uv0 + uv1 + uv2) / 3f;

                samplePoints.Add(new SamplePoint
                {
                    worldPosition = worldPos,
                    uv = sampleUV,
                    tangent = tangent,
                    bitangent = bitangent,
                    normal = normal,
                    renderer = renderer,
                    submeshIndex = submeshIndex
                });
            }
        }

        /// <summary>
        /// UV領域マスクを再構築（パディング適用）
        /// </summary>
        private void RebuildUvRegionMask(MaterialEntry materialEntry, int padding)
        {
            uvRegionMask = UvRegionMaskUtils.BuildUvRegionMask(materialEntry, padding);
        }

        private static Material GetLineMaterial()
        {
            if (lineMaterial == null)
            {
                // 深度テストを有効にしたシンプルなシェーダーを作成
                var shader = Shader.Find("Hidden/Internal-Colored");
                lineMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                // 深度テストを有効にする（オブジェクトの裏側は表示しない）
                lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
                lineMaterial.SetInt("_ZWrite", 0);
            }
            return lineMaterial;
        }

        public void ClearCache()
        {
            samplePoints.Clear();
            cacheDirty = true;
        }

        /// <summary>
        /// 指定座標から連続するUV領域のみを抽出したマスクを生成
        /// Flood fillアルゴリズムを使用し、UvRegionMask内で連続しているピクセルのみを含むマスクを返す
        /// </summary>
        /// <param name="startX">開始X座標</param>
        /// <param name="startY">開始Y座標</param>
        /// <param name="maxRange">検索範囲（開始点からの最大距離）</param>
        /// <param name="outputBuffer">出力バッファ（nullの場合はプールから取得、使用後はMaskBufferPool.Returnで返却推奨）</param>
        /// <returns>連続領域のみを含むマスク</returns>
        public bool[,] ExtractConnectedRegion(int startX, int startY, int maxRange, bool[,] outputBuffer = null)
        {
            bool[,] result = outputBuffer ?? MaskBufferPool.Rent();

            if (uvRegionMask == null)
                return result;

            // 開始点が範囲外またはマスク外の場合は空のマスクを返す
            if (startX < 0 || startX >= Common.TexSize || startY < 0 || startY >= Common.TexSize)
                return result;

            if (!uvRegionMask[startX, startY])
            {
                // 開始点がマスク外の場合、近くのマスク内ピクセルを探す
                int searchRadius = Mathf.Min(maxRange, 10);
                bool found = false;
                for (int r = 1; r <= searchRadius && !found; r++)
                {
                    for (int searchDy = -r; searchDy <= r && !found; searchDy++)
                    {
                        for (int searchDx = -r; searchDx <= r && !found; searchDx++)
                        {
                            if (Mathf.Abs(searchDx) != r && Mathf.Abs(searchDy) != r)
                                continue; // 現在の半径の境界のみをチェック

                            int nx = startX + searchDx;
                            int ny = startY + searchDy;
                            if (nx >= 0 && nx < Common.TexSize && ny >= 0 && ny < Common.TexSize)
                            {
                                if (uvRegionMask[nx, ny])
                                {
                                    startX = nx;
                                    startY = ny;
                                    found = true;
                                }
                            }
                        }
                    }
                }

                if (!found)
                    return result;
            }

            // Flood fillの検索範囲を制限
            int minX = Mathf.Max(0, startX - maxRange);
            int maxX = Mathf.Min(Common.TexSize - 1, startX + maxRange);
            int minY = Mathf.Max(0, startY - maxRange);
            int maxY = Mathf.Min(Common.TexSize - 1, startY + maxRange);

            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            int visitedLength = width * height;

            var queue = floodFillQueue;
            queue.Clear();

            bool[] visited = null;
            try
            {
                visited = System.Buffers.ArrayPool<bool>.Shared.Rent(visitedLength);
                System.Array.Clear(visited, 0, visitedLength);

                queue.Enqueue(new Vector2Int(startX, startY));
                visited[(startX - minX) + (startY - minY) * width] = true;
                result[startX, startY] = true;

                // 4方向の隣接ピクセル
                int[] dx = { 1, -1, 0, 0 };
                int[] dy = { 0, 0, 1, -1 };

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();

                    for (int i = 0; i < 4; i++)
                    {
                        int nx = current.x + dx[i];
                        int ny = current.y + dy[i];

                        // 範囲チェック
                        if (nx < minX || nx > maxX || ny < minY || ny > maxY)
                            continue;

                        int localIndex = (nx - minX) + (ny - minY) * width;

                        // 訪問済みチェック
                        if (visited[localIndex])
                            continue;

                        visited[localIndex] = true;

                        // マスクチェック
                        if (uvRegionMask[nx, ny])
                        {
                            result[nx, ny] = true;
                            queue.Enqueue(new Vector2Int(nx, ny));
                        }
                    }
                }
            }
            finally
            {
                if (visited != null)
                {
                    System.Buffers.ArrayPool<bool>.Shared.Return(visited);
                }
            }

            return result;
        }
    }
}

