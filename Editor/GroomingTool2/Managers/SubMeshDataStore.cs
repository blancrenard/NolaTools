using System.Collections.Generic;
using GroomingTool2.Utils;
using UnityEngine;

namespace GroomingTool2.Managers
{
    /// <summary>
    /// サブメッシュのデータを保持する構造体
    /// </summary>
    internal struct SubMeshEntry
    {
        public Renderer Renderer;
        public int SubmeshIndex;
        public Mesh Mesh;
        public bool IsBakedTempMesh;
        public Vector3[] Vertices;
        public Vector2[] Uvs;
        public int[] Triangles;
        public HashSet<int> ValidVertices;
        
        /// <summary>対称頂点マッピング: vertexIndex -> encodedSymmetryInfo</summary>
        public Dictionary<int, int> SymmetryTable;
        
        /// <summary>UV空間の三角形グリッド</summary>
        public UvTriangleGrid UvGrid;
        
        /// <summary>UVアイランドデータ</summary>
        public UvIslandData IslandData;
    }

    /// <summary>
    /// 複数サブメッシュのデータを管理するクラス
    /// VertexSymmetryMapperから分離されたデータストア
    /// </summary>
    internal sealed class SubMeshDataStore
    {
        private readonly List<SubMeshEntry> entries = new List<SubMeshEntry>();

        /// <summary>
        /// 登録されているサブメッシュの数
        /// </summary>
        public int Count => entries.Count;

        /// <summary>
        /// データが初期化済みかどうか
        /// </summary>
        public bool IsInitialized => entries.Count > 0;

        /// <summary>
        /// インデクサでエントリにアクセス
        /// </summary>
        public SubMeshEntry this[int index]
        {
            get => entries[index];
            set => entries[index] = value;
        }

        /// <summary>
        /// サブメッシュを追加
        /// </summary>
        /// <param name="renderer">レンダラー</param>
        /// <param name="mesh">メッシュ</param>
        /// <param name="submeshIndex">サブメッシュインデックス</param>
        /// <param name="isBaked">一時的にベイクされたメッシュか</param>
        /// <returns>追加成功時true</returns>
        public bool Add(Renderer renderer, Mesh mesh, int submeshIndex, bool isBaked)
        {
            if (mesh == null || renderer == null) return false;

            int sub = Mathf.Clamp(submeshIndex, 0, mesh.subMeshCount - 1);
            int[] triangles = mesh.GetTriangles(sub);
            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;

            if (triangles == null || triangles.Length == 0 || uvs == null || uvs.Length != mesh.vertexCount)
            {
                Debug.LogWarning($"[SubMeshDataStore] データが不正: renderer={renderer.name}, submesh={submeshIndex}");
                if (isBaked)
                {
                    Object.DestroyImmediate(mesh);
                }
                return false;
            }

            // 有効な頂点のHashSetを構築
            var validVertices = new HashSet<int>();
            foreach (int vi in triangles)
            {
                if (vi >= 0 && vi < vertices.Length)
                {
                    validVertices.Add(vi);
                }
            }

            var entry = new SubMeshEntry
            {
                Renderer = renderer,
                SubmeshIndex = submeshIndex,
                Mesh = mesh,
                IsBakedTempMesh = isBaked,
                Vertices = vertices,
                Uvs = uvs,
                Triangles = triangles,
                ValidVertices = validVertices,
                SymmetryTable = new Dictionary<int, int>(),
                UvGrid = null,
                IslandData = default
            };

            entries.Add(entry);
            return true;
        }

        /// <summary>
        /// 全エントリのUVグリッドとアイランドデータを構築
        /// </summary>
        public void BuildSpatialData()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                
                // UVグリッドを構築
                entry.UvGrid = new UvTriangleGrid();
                var tris = entry.Triangles;
                var uvs = entry.Uvs;
                
                if (tris != null && uvs != null)
                {
                    for (int ti = 0; ti < tris.Length; ti += 3)
                    {
                        int a = tris[ti];
                        int b = tris[ti + 1];
                        int c = tris[ti + 2];
                        
                        if (a >= 0 && a < uvs.Length && b >= 0 && b < uvs.Length && c >= 0 && c < uvs.Length)
                        {
                            entry.UvGrid.AddTriangle(uvs[a], uvs[b], uvs[c], ti);
                        }
                    }
                }
                
                // アイランドデータを構築
                entry.IslandData = UvIslandBuilder.Build(entry.Triangles, entry.Uvs, entry.Vertices);
                
                entries[i] = entry;
            }
        }

        /// <summary>
        /// 全エントリをクリアしてリソースを解放
        /// </summary>
        public void Clear()
        {
            foreach (var entry in entries)
            {
                if (entry.IsBakedTempMesh && entry.Mesh != null)
                {
                    Object.DestroyImmediate(entry.Mesh);
                }
            }
            entries.Clear();
        }

        /// <summary>
        /// 列挙子を取得
        /// </summary>
        public IEnumerator<SubMeshEntry> GetEnumerator() => entries.GetEnumerator();

        /// <summary>
        /// UV座標から最近傍頂点インデックスを検索
        /// </summary>
        /// <param name="uv">検索するUV座標</param>
        /// <param name="distance">出力: 見つかった頂点までのUV距離</param>
        /// <param name="subMeshIndex">出力: 見つかったサブメッシュのインデックス</param>
        /// <returns>頂点インデックス（見つからない場合は-1）</returns>
        public int FindNearestVertex(Vector2 uv, out float distance, out int subMeshIndex)
        {
            distance = float.MaxValue;
            int nearestVi = -1;
            subMeshIndex = -1;

            for (int si = 0; si < entries.Count; si++)
            {
                var entry = entries[si];
                float bestDistSqr = float.MaxValue;
                int bestVi = -1;

                foreach (int vi in entry.ValidVertices)
                {
                    float distSqr = Vector2.SqrMagnitude(entry.Uvs[vi] - uv);
                    if (distSqr < bestDistSqr)
                    {
                        bestDistSqr = distSqr;
                        bestVi = vi;
                    }
                }

                if (bestVi >= 0)
                {
                    float dist = Mathf.Sqrt(bestDistSqr);
                    if (dist < distance)
                    {
                        distance = dist;
                        nearestVi = bestVi;
                        subMeshIndex = si;
                    }
                }
            }

            return nearestVi;
        }

        /// <summary>
        /// UV座標から所属する三角形を検索
        /// </summary>
        /// <param name="uv">検索するUV座標</param>
        /// <param name="subMeshIndex">出力: 見つかったサブメッシュのインデックス</param>
        /// <param name="triangleStartIndex">出力: 三角形の開始インデックス</param>
        /// <param name="barycentric">出力: バリセントリック座標</param>
        /// <returns>見つかった場合true</returns>
        public bool FindTriangleAtUv(Vector2 uv, out int subMeshIndex, out int triangleStartIndex, out Vector3 barycentric)
        {
            subMeshIndex = -1;
            triangleStartIndex = -1;
            barycentric = default;

            const float epsilon = 1e-5f;

            for (int si = 0; si < entries.Count; si++)
            {
                var entry = entries[si];
                
                // AABB早期スキップ
                if (entry.UvGrid != null && !entry.UvGrid.IsInBounds(uv))
                    continue;

                var candidates = entry.UvGrid?.GetCandidates(uv);
                if (candidates == null || candidates.Count == 0)
                    continue;

                var tris = entry.Triangles;
                var uvs = entry.Uvs;

                foreach (int ti in candidates)
                {
                    int a = tris[ti];
                    int b = tris[ti + 1];
                    int c = tris[ti + 2];

                    if (a < 0 || a >= uvs.Length || b < 0 || b >= uvs.Length || c < 0 || c >= uvs.Length)
                        continue;

                    var ua = uvs[a];
                    var ub = uvs[b];
                    var uc = uvs[c];

                    if (BarycentricUtils.TryCompute2D(uv, ua, ub, uc, out var bary))
                    {
                        if (BarycentricUtils.IsInsideTriangle(bary, epsilon))
                        {
                            subMeshIndex = si;
                            triangleStartIndex = ti;
                            barycentric = bary;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 指定位置の頂点インデックスを取得
        /// </summary>
        public (int a, int b, int c) GetTriangleVertices(int subMeshIndex, int triangleStartIndex)
        {
            if (subMeshIndex < 0 || subMeshIndex >= entries.Count)
                return (-1, -1, -1);

            var tris = entries[subMeshIndex].Triangles;
            if (triangleStartIndex < 0 || triangleStartIndex + 2 >= tris.Length)
                return (-1, -1, -1);

            return (tris[triangleStartIndex], tris[triangleStartIndex + 1], tris[triangleStartIndex + 2]);
        }
    }
}
