using System.Collections.Generic;
using GroomingTool2.Utils;
using UnityEngine;

namespace GroomingTool2.Managers
{
    /// <summary>
    /// 頂点の対称マッピングを管理するクラス
    /// UV座標から3D頂点を特定し、X軸対称の頂点を検索してUV座標を返す
    /// 複数のサブメッシュ・マテリアルに対応
    /// 
    /// リファクタリング後: 各責務を以下のクラスに委譲
    /// - SubMeshDataStore: サブメッシュデータの管理
    /// - UvIslandBuilder: UVアイランドの構築
    /// - UvTriangleGrid: UV空間の三角形検索
    /// - SpatialIndex3D: 3D空間の高速検索
    /// - BarycentricUtils: バリセントリック座標計算
    /// </summary>
    internal sealed class VertexSymmetryMapper
    {
        #region Constants
        
        /// <summary>対称頂点検索の許容誤差（3D空間）</summary>
        private const float SymmetryTolerance = 0.001f;
        
        /// <summary>UV距離の閾値（これを超える場合は精度が低すぎる）</summary>
        private const float UvDistanceThreshold = 0.02f;
        
        /// <summary>アイランド対称検索の許容誤差</summary>
        private const float IslandSymmetryTolerance = 0.01f;
        
        /// <summary>バリセントリック座標の許容誤差</summary>
        private const float BarycentricEpsilon = 1e-5f;
        
        /// <summary>タンジェント計算の最小ベクトル長</summary>
        private const float MinTangentLength = 0.01f;
        
        /// <summary>退化行列式の閾値</summary>
        private const float DegenerateDetThreshold = 1e-6f;
        
        #endregion

        #region Fields
        
        private readonly SubMeshDataStore dataStore = new SubMeshDataStore();
        
        #endregion

        #region Properties
        
        /// <summary>
        /// マテリアルが選択されているかどうか
        /// </summary>
        public bool IsInitialized => dataStore.IsInitialized;
        
        #endregion

        #region Initialization
        
        /// <summary>
        /// テクスチャを使用する全サブメッシュで初期化（推奨）
        /// </summary>
        public void Initialize(Texture2D texture, GameObject avatar)
        {
            ClearCache();

            if (texture == null || avatar == null)
            {
                Debug.LogWarning("[VertexSymmetryMapper] テクスチャまたはアバターがnullです");
                return;
            }

            var renderers = avatar.GetComponentsInChildren<Renderer>(true);
            int totalSubMeshCount = 0;

            foreach (var renderer in renderers)
            {
                var mesh = EditorMeshUtils.GetMeshForRenderer(renderer, out bool isBaked);
                if (mesh == null) continue;

                var sharedMaterials = renderer.sharedMaterials;
                for (int submeshIndex = 0; submeshIndex < sharedMaterials.Length; submeshIndex++)
                {
                    var material = sharedMaterials[submeshIndex];
                    if (material == null) continue;

                    if (material.mainTexture == texture)
                    {
                        if (dataStore.Add(renderer, mesh, submeshIndex, isBaked))
                        {
                            totalSubMeshCount++;
                        }
                    }
                }
            }

            if (totalSubMeshCount == 0)
            {
                Debug.LogWarning($"[VertexSymmetryMapper] 指定されたテクスチャ ({texture.name}) を使用するサブメッシュが見つかりませんでした");
                return;
            }

            // 空間データの構築
            dataStore.BuildSpatialData();
            
            // 対称マッピングテーブルを構築
            BuildSymmetryTables();
            
            // UVアイランド対称マッピングを構築
            BuildIslandSymmetryTables();
        }

        /// <summary>
        /// 指定されたレンダラーとサブメッシュで初期化（後方互換性のため残す）
        /// </summary>
        public void Initialize(Renderer renderer, int submeshIndex)
        {
            ClearCache();

            if (renderer == null)
            {
                Debug.LogWarning("[VertexSymmetryMapper] レンダラーがnullです");
                return;
            }

            var mesh = EditorMeshUtils.GetMeshForRenderer(renderer, out bool isBaked);
            if (mesh == null)
            {
                Debug.LogWarning($"[VertexSymmetryMapper] メッシュの取得に失敗: {renderer.name}");
                return;
            }

            if (!dataStore.Add(renderer, mesh, submeshIndex, isBaked))
            {
                Debug.LogWarning("[VertexSymmetryMapper] サブメッシュの追加に失敗しました");
                return;
            }

            dataStore.BuildSpatialData();
            BuildSymmetryTables();
            BuildIslandSymmetryTables();
        }

        /// <summary>
        /// キャッシュをクリア
        /// </summary>
        public void ClearCache()
        {
            dataStore.Clear();
        }
        
        #endregion

        #region Public Methods - Symmetry Lookup
        
        /// <summary>
        /// UV座標から対称UV座標を取得（複数サブメッシュ対応）
        /// </summary>
        /// <param name="uv">元のUV座標</param>
        /// <param name="symmetricUV">対称UV座標（見つかった場合）</param>
        /// <returns>対称頂点が見つかった場合はtrue</returns>
        public bool TryGetSymmetricUV(Vector2 uv, out Vector2 symmetricUV)
        {
            symmetricUV = Vector2.zero;

            if (!IsInitialized)
            {
                Debug.LogWarning("[VertexSymmetryMapper] 初期化されていません");
                return false;
            }

            // UV座標から最近傍頂点を検索
            int vertexIndex = dataStore.FindNearestVertex(uv, out float uvDistance, out int sourceSubMeshIndex);
            if (vertexIndex < 0 || sourceSubMeshIndex < 0)
            {
                return false;
            }

            // UV距離が閾値を超える場合はスキップ
            if (uvDistance > UvDistanceThreshold)
            {
                return false;
            }

            var sourceEntry = dataStore[sourceSubMeshIndex];

            // 対称マッピングテーブルから対称頂点情報を取得
            if (!sourceEntry.SymmetryTable.TryGetValue(vertexIndex, out int encodedSymmetryInfo))
            {
                return false;
            }

            // エンコードされた情報をデコード
            DecodeSymmetryInfo(encodedSymmetryInfo, out int targetSubMeshIndex, out int symmetricVertexIndex);

            if (targetSubMeshIndex < 0 || targetSubMeshIndex >= dataStore.Count)
            {
                Debug.LogWarning($"[VertexSymmetryMapper] 不正なサブメッシュインデックス: {targetSubMeshIndex}");
                return false;
            }

            var targetEntry = dataStore[targetSubMeshIndex];

            if (symmetricVertexIndex < 0 || symmetricVertexIndex >= targetEntry.Uvs.Length)
            {
                Debug.LogWarning($"[VertexSymmetryMapper] 不正な頂点インデックス: {symmetricVertexIndex}");
                return false;
            }

            symmetricUV = targetEntry.Uvs[symmetricVertexIndex];
            return true;
        }

        /// <summary>
        /// UV座標から対称UVをバリセントリック補間で取得
        /// 三角形内の正確な位置を考慮した高精度な対称UV計算
        /// </summary>
        public bool TryGetSymmetricUVBarycentric(Vector2 uv, out Vector2 symmetricUV)
        {
            symmetricUV = Vector2.zero;

            if (!IsInitialized)
            {
                Debug.LogWarning("[VertexSymmetryMapper] 初期化されていません");
                return false;
            }

            // UV座標から三角形を特定
            if (!dataStore.FindTriangleAtUv(uv, out int subMeshIndex, out int triangleStartIndex, out Vector3 bary))
            {
                return false;
            }

            var srcEntry = dataStore[subMeshIndex];
            var (vi0, vi1, vi2) = dataStore.GetTriangleVertices(subMeshIndex, triangleStartIndex);
            
            if (vi0 < 0) return false;

            // 三角形のアイランドIDを取得
            int triIndex = triangleStartIndex / 3;
            int srcIslandId = UvIslandBuilder.GetIslandId(srcEntry.IslandData, triIndex);
            if (srcIslandId < 0) return false;

            // アイランド対称テーブルから対称アイランドを取得
            if (srcEntry.IslandData.SymmetryTable == null || 
                !srcEntry.IslandData.SymmetryTable.TryGetValue(srcIslandId, out var symmetricIsland))
            {
                return false;
            }

            int targetSubMeshIndex = symmetricIsland.targetSubMeshIndex;
            int targetIslandId = symmetricIsland.targetIslandId;

            if (targetSubMeshIndex < 0 || targetSubMeshIndex >= dataStore.Count)
            {
                return false;
            }

            var targetEntry = dataStore[targetSubMeshIndex];

            // 元の三角形の3D位置を計算
            Vector3 srcWorldPos = BarycentricUtils.Interpolate(bary, 
                srcEntry.Vertices[vi0], srcEntry.Vertices[vi1], srcEntry.Vertices[vi2]);

            // X軸反転で対称位置を計算
            Vector3 symmetricWorldPos = new Vector3(-srcWorldPos.x, srcWorldPos.y, srcWorldPos.z);

            // 対称アイランド内で最も近い三角形を探す
            return FindSymmetricUvInIsland(targetEntry, targetIslandId, symmetricWorldPos, out symmetricUV);
        }

        /// <summary>
        /// 3D空間経由でミラー方向を計算（タンジェントは内部で計算）
        /// </summary>
        public bool TryCalculateMirrorDirectionVia3D(Vector2 srcUV, float srcRadians, out float mirrorRadians)
        {
            mirrorRadians = 0f;

            if (!IsInitialized)
                return false;

            // UV座標からタンジェント空間を計算
            if (!TryGetTangentSpaceFromUV(srcUV, out var srcTangent, out var srcBitangent))
                return false;

            return TryCalculateMirrorDirectionVia3D(srcUV, srcRadians, srcTangent, srcBitangent, out mirrorRadians);
        }

        /// <summary>
        /// 3D空間経由でミラー方向を計算（タンジェント指定版）
        /// </summary>
        public bool TryCalculateMirrorDirectionVia3D(
            Vector2 srcUV,
            float srcRadians,
            Vector3 srcTangent,
            Vector3 srcBitangent,
            out float mirrorRadians)
        {
            mirrorRadians = 0f;

            if (!IsInitialized)
                return false;

            // タンジェント/バイタンジェントの正規化と検証
            srcTangent = srcTangent.normalized;
            srcBitangent = srcBitangent.normalized;

            if (srcTangent.sqrMagnitude < MinTangentLength || srcBitangent.sqrMagnitude < MinTangentLength)
                return false;

            // 1. UV方向を3D方向に変換
            float cosR = Mathf.Cos(srcRadians);
            float sinR = Mathf.Sin(srcRadians);
            Vector3 dir3D = (cosR * srcTangent + sinR * srcBitangent).normalized;

            // 2. X軸で反転（3D空間での左右対称）
            Vector3 mirrorDir3D = new Vector3(-dir3D.x, dir3D.y, dir3D.z);

            // 3. 対称位置のタンジェント空間を取得
            if (!TryGetSymmetricTangentSpace(srcUV, out var dstTangent, out var dstBitangent))
                return false;

            // 4. 3D方向を対称UV空間の方向に変換
            float u = Vector3.Dot(mirrorDir3D, dstTangent);
            float v = Vector3.Dot(mirrorDir3D, dstBitangent);

            if (u * u + v * v < 1e-10f)
                return false;

            mirrorRadians = Mathf.Atan2(v, u);
            return true;
        }
        
        #endregion

        #region Private Methods - Symmetry Table Building
        
        /// <summary>
        /// 全サブメッシュの対称マッピングテーブルを構築
        /// </summary>
        private void BuildSymmetryTables()
        {
            // 空間インデックスを構築
            var spatialIndex = new SpatialIndex3D<(int subMeshIndex, int vertexIndex)>(SymmetryTolerance * 2f);

            for (int si = 0; si < dataStore.Count; si++)
            {
                var entry = dataStore[si];
                foreach (int vi in entry.ValidVertices)
                {
                    if (vi >= 0 && vi < entry.Vertices.Length)
                    {
                        spatialIndex.Add(entry.Vertices[vi], (si, vi));
                    }
                }
            }

            int totalFoundCount = 0;

            for (int si = 0; si < dataStore.Count; si++)
            {
                var entry = dataStore[si];
                entry.SymmetryTable = new Dictionary<int, int>();

                foreach (int vi in entry.ValidVertices)
                {
                    if (vi < 0 || vi >= entry.Vertices.Length)
                        continue;

                    Vector3 localPos = entry.Vertices[vi];
                    Vector3 symmetricPos = new Vector3(-localPos.x, localPos.y, localPos.z);

                    // 空間インデックスで対称頂点を検索
                    if (spatialIndex.TryFindNearest(symmetricPos, 
                        item => dataStore[item.subMeshIndex].Vertices[item.vertexIndex],
                        SymmetryTolerance, 
                        out var result, 
                        out _))
                    {
                        int encoded = EncodeSymmetryInfo(result.subMeshIndex, result.vertexIndex);
                        entry.SymmetryTable[vi] = encoded;
                        totalFoundCount++;
                    }
                }

                dataStore[si] = entry;
            }

            if (totalFoundCount == 0)
            {
                Debug.LogWarning("[VertexSymmetryMapper] 警告: 対称頂点が1つも見つかりませんでした。モデルが非対称か、許容誤差が小さすぎる可能性があります");
            }
        }

        /// <summary>
        /// UVアイランド単位の対称マッピングテーブルを構築
        /// </summary>
        private void BuildIslandSymmetryTables()
        {
            // 全アイランドの空間インデックスを構築
            var islandIndex = new SpatialIndex3D<(int subMeshIndex, int islandId, Vector3 centroid)>(SymmetryTolerance * 4f);

            for (int si = 0; si < dataStore.Count; si++)
            {
                var entry = dataStore[si];
                var islandData = entry.IslandData;
                
                if (islandData.Island3DCentroids == null)
                    continue;

                for (int islandId = 0; islandId < islandData.IslandCount; islandId++)
                {
                    var centroid = islandData.Island3DCentroids[islandId];
                    islandIndex.Add(centroid, (si, islandId, centroid));
                }
            }

            // 各アイランドの対称アイランドを検索
            for (int si = 0; si < dataStore.Count; si++)
            {
                var entry = dataStore[si];
                var islandData = entry.IslandData;
                
                if (islandData.Island3DCentroids == null)
                    continue;

                islandData.SymmetryTable = new Dictionary<int, (int, int)>();

                for (int islandId = 0; islandId < islandData.IslandCount; islandId++)
                {
                    Vector3 centroid = islandData.Island3DCentroids[islandId];
                    Vector3 symmetricCentroid = new Vector3(-centroid.x, centroid.y, centroid.z);

                    if (islandIndex.TryFindNearest(symmetricCentroid,
                        item => item.centroid,
                        IslandSymmetryTolerance,
                        out var result,
                        out _))
                    {
                        islandData.SymmetryTable[islandId] = (result.subMeshIndex, result.islandId);
                    }
                }

                entry.IslandData = islandData;
                dataStore[si] = entry;
            }
        }
        
        #endregion

        #region Private Methods - Tangent Space
        
        /// <summary>
        /// UV座標からタンジェント空間を計算
        /// </summary>
        private bool TryGetTangentSpaceFromUV(Vector2 uv, out Vector3 tangent, out Vector3 bitangent)
        {
            tangent = Vector3.right;
            bitangent = Vector3.up;

            if (!dataStore.FindTriangleAtUv(uv, out int subMeshIndex, out int triangleStartIndex, out _))
                return false;

            var entry = dataStore[subMeshIndex];
            var (a, b, c) = dataStore.GetTriangleVertices(subMeshIndex, triangleStartIndex);
            
            if (a < 0) return false;

            return CalculateTangentSpace(entry, a, b, c, out tangent, out bitangent);
        }

        /// <summary>
        /// 対称位置のタンジェント空間を取得
        /// </summary>
        private bool TryGetSymmetricTangentSpace(Vector2 srcUV, out Vector3 dstTangent, out Vector3 dstBitangent)
        {
            dstTangent = Vector3.right;
            dstBitangent = Vector3.up;

            // 元のUV座標から三角形を特定
            if (!dataStore.FindTriangleAtUv(srcUV, out int subMeshIndex, out int triangleStartIndex, out Vector3 bary))
                return false;

            var srcEntry = dataStore[subMeshIndex];
            var (vi0, vi1, vi2) = dataStore.GetTriangleVertices(subMeshIndex, triangleStartIndex);
            
            if (vi0 < 0) return false;

            // 元の3D位置を計算
            Vector3 srcPos = BarycentricUtils.Interpolate(bary,
                srcEntry.Vertices[vi0], srcEntry.Vertices[vi1], srcEntry.Vertices[vi2]);

            // X軸反転で対称位置
            Vector3 symmetricPos = new Vector3(-srcPos.x, srcPos.y, srcPos.z);

            // アイランド対称テーブルを使用して対称三角形を探す
            int triIndex = triangleStartIndex / 3;
            int srcIslandId = UvIslandBuilder.GetIslandId(srcEntry.IslandData, triIndex);
            if (srcIslandId < 0) return false;

            if (srcEntry.IslandData.SymmetryTable == null ||
                !srcEntry.IslandData.SymmetryTable.TryGetValue(srcIslandId, out var symmetricIsland))
                return false;

            int targetSubMeshIndex = symmetricIsland.targetSubMeshIndex;
            int targetIslandId = symmetricIsland.targetIslandId;

            if (targetSubMeshIndex < 0 || targetSubMeshIndex >= dataStore.Count)
                return false;

            var targetEntry = dataStore[targetSubMeshIndex];

            // 対称アイランド内で最も近い三角形を探す
            if (!FindClosestTriangleInIsland(targetEntry, targetIslandId, symmetricPos, out int bestTa, out int bestTb, out int bestTc))
                return false;

            return CalculateTangentSpace(targetEntry, bestTa, bestTb, bestTc, out dstTangent, out dstBitangent);
        }

        /// <summary>
        /// 三角形のタンジェント空間を計算
        /// </summary>
        private bool CalculateTangentSpace(SubMeshEntry entry, int idx0, int idx1, int idx2, out Vector3 tangent, out Vector3 bitangent)
        {
            tangent = Vector3.right;
            bitangent = Vector3.up;

            if (idx0 >= entry.Vertices.Length || idx1 >= entry.Vertices.Length || idx2 >= entry.Vertices.Length)
                return false;
            if (idx0 >= entry.Uvs.Length || idx1 >= entry.Uvs.Length || idx2 >= entry.Uvs.Length)
                return false;

            Vector3 v0 = entry.Vertices[idx0];
            Vector3 v1 = entry.Vertices[idx1];
            Vector3 v2 = entry.Vertices[idx2];
            Vector2 uv0 = entry.Uvs[idx0];
            Vector2 uv1 = entry.Uvs[idx1];
            Vector2 uv2 = entry.Uvs[idx2];

            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector2 deltaUV1 = uv1 - uv0;
            Vector2 deltaUV2 = uv2 - uv0;

            float det = deltaUV1.x * deltaUV2.y - deltaUV2.x * deltaUV1.y;
            if (Mathf.Abs(det) < DegenerateDetThreshold)
                return false;

            float f = 1.0f / det;

            tangent = new Vector3(
                f * (deltaUV2.y * edge1.x - deltaUV1.y * edge2.x),
                f * (deltaUV2.y * edge1.y - deltaUV1.y * edge2.y),
                f * (deltaUV2.y * edge1.z - deltaUV1.y * edge2.z)
            ).normalized;

            bitangent = new Vector3(
                f * (-deltaUV2.x * edge1.x + deltaUV1.x * edge2.x),
                f * (-deltaUV2.x * edge1.y + deltaUV1.x * edge2.y),
                f * (-deltaUV2.x * edge1.z + deltaUV1.x * edge2.z)
            ).normalized;

            // レンダラーのトランスフォームでワールド空間に変換
            if (entry.Renderer != null)
            {
                tangent = entry.Renderer.transform.TransformDirection(tangent);
                bitangent = entry.Renderer.transform.TransformDirection(bitangent);
            }

            return tangent.sqrMagnitude > MinTangentLength && bitangent.sqrMagnitude > MinTangentLength;
        }
        
        #endregion

        #region Private Methods - Island Search
        
        /// <summary>
        /// 対称アイランド内で最も近い三角形を探し、対称UVを計算
        /// </summary>
        private bool FindSymmetricUvInIsland(SubMeshEntry targetEntry, int targetIslandId, Vector3 symmetricWorldPos, out Vector2 symmetricUV)
        {
            symmetricUV = Vector2.zero;
            
            float bestDist = float.MaxValue;
            bool found = false;

            var tris = targetEntry.Triangles;
            var vertices = targetEntry.Vertices;
            var uvs = targetEntry.Uvs;
            var islandData = targetEntry.IslandData;

            for (int ti = 0; ti < tris.Length; ti += 3)
            {
                int triIndex = ti / 3;
                int islandId = UvIslandBuilder.GetIslandId(islandData, triIndex);
                
                if (islandId != targetIslandId)
                    continue;

                int ta = tris[ti];
                int tb = tris[ti + 1];
                int tc = tris[ti + 2];

                if (ta >= vertices.Length || tb >= vertices.Length || tc >= vertices.Length)
                    continue;

                Vector3 pa = vertices[ta];
                Vector3 pb = vertices[tb];
                Vector3 pc = vertices[tc];

                Vector3 triCenter = (pa + pb + pc) / 3f;
                float dist = Vector3.Distance(triCenter, symmetricWorldPos);

                if (dist < bestDist)
                {
                    if (BarycentricUtils.TryCompute3D(symmetricWorldPos, pa, pb, pc, out var targetBary))
                    {
                        const float BaryTolerance = 0.5f;
                        if (targetBary.x >= -BaryTolerance && targetBary.y >= -BaryTolerance && targetBary.z >= -BaryTolerance)
                        {
                            if (ta < uvs.Length && tb < uvs.Length && tc < uvs.Length)
                            {
                                var normalizedBary = BarycentricUtils.Normalize(targetBary);
                                symmetricUV = BarycentricUtils.Interpolate(normalizedBary, uvs[ta], uvs[tb], uvs[tc]);
                                bestDist = dist;
                                found = true;
                            }
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// 指定アイランド内で最も近い三角形を検索
        /// </summary>
        private bool FindClosestTriangleInIsland(SubMeshEntry entry, int islandId, Vector3 position, out int ta, out int tb, out int tc)
        {
            ta = tb = tc = -1;
            float bestDist = float.MaxValue;

            var tris = entry.Triangles;
            var vertices = entry.Vertices;
            var islandData = entry.IslandData;

            for (int ti = 0; ti < tris.Length; ti += 3)
            {
                int triIndex = ti / 3;
                int triIslandId = UvIslandBuilder.GetIslandId(islandData, triIndex);
                
                if (triIslandId != islandId)
                    continue;

                int a = tris[ti];
                int b = tris[ti + 1];
                int c = tris[ti + 2];

                if (a >= vertices.Length || b >= vertices.Length || c >= vertices.Length)
                    continue;

                Vector3 center = (vertices[a] + vertices[b] + vertices[c]) / 3f;
                float dist = Vector3.Distance(center, position);

                if (dist < bestDist)
                {
                    bestDist = dist;
                    ta = a;
                    tb = b;
                    tc = c;
                }
            }

            return ta >= 0;
        }
        
        #endregion

        #region Private Methods - Encoding
        
        /// <summary>
        /// サブメッシュインデックスと頂点インデックスを1つのintにエンコード
        /// </summary>
        private static int EncodeSymmetryInfo(int subMeshIndex, int vertexIndex)
        {
            return (subMeshIndex << 16) | (vertexIndex & 0xFFFF);
        }

        /// <summary>
        /// エンコードされた対称情報をデコード
        /// </summary>
        private static void DecodeSymmetryInfo(int encoded, out int subMeshIndex, out int vertexIndex)
        {
            subMeshIndex = (encoded >> 16) & 0xFFFF;
            vertexIndex = encoded & 0xFFFF;
        }
        
        #endregion
    }
}
