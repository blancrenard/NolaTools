using System.Collections.Generic;
using UnityEngine;

namespace GroomingTool2.Managers
{
    /// <summary>
    /// UVアイランドのデータを管理する構造体
    /// </summary>
    internal struct UvIslandData
    {
        /// <summary>三角形インデックス(ti/3) からアイランドIDへのマッピング</summary>
        public int[] TriangleToIslandId;
        
        /// <summary>アイランドID から UV AABB最小値</summary>
        public Vector2[] IslandMin;
        
        /// <summary>アイランドID から UV AABB最大値</summary>
        public Vector2[] IslandMax;
        
        /// <summary>アイランドID から 3D空間での重心</summary>
        public Vector3[] Island3DCentroids;
        
        /// <summary>アイランドの総数</summary>
        public int IslandCount;
        
        /// <summary>アイランド対称マッピング: islandId -> (targetSubMeshIndex, targetIslandId)</summary>
        public Dictionary<int, (int targetSubMeshIndex, int targetIslandId)> SymmetryTable;
    }

    /// <summary>
    /// UVアイランドの構築と管理を行うクラス
    /// メッシュの三角形から接続されたUVアイランドを検出し、対称マッピングを構築
    /// </summary>
    internal sealed class UvIslandBuilder
    {
        /// <summary>
        /// 三角形配列からUVアイランドを構築
        /// </summary>
        /// <param name="triangles">三角形インデックス配列</param>
        /// <param name="uvs">UV座標配列</param>
        /// <param name="vertices">頂点座標配列（3D重心計算用）</param>
        /// <returns>構築されたUVアイランドデータ</returns>
        public static UvIslandData Build(int[] triangles, Vector2[] uvs, Vector3[] vertices)
        {
            var result = new UvIslandData();
            
            if (triangles == null || uvs == null || triangles.Length < 3)
            {
                result.IslandCount = 0;
                return result;
            }

            int triCount = triangles.Length / 3;
            
            // 頂点共有から三角形の隣接関係を構築
            var triNeighbors = BuildTriangleAdjacency(triangles, triCount);
            
            // BFSでアイランドを検出
            var (triIslandId, islandCount) = AssignIslandIds(triNeighbors, triCount);
            
            // アイランドのAABBを計算
            var (islandMin, islandMax) = ComputeIslandBounds(triangles, uvs, triIslandId, islandCount);
            
            // 3D重心を計算（頂点がある場合）
            Vector3[] centroids = null;
            if (vertices != null && vertices.Length > 0)
            {
                centroids = ComputeIsland3DCentroids(triangles, vertices, triIslandId, islandCount);
            }
            
            result.TriangleToIslandId = triIslandId;
            result.IslandMin = islandMin;
            result.IslandMax = islandMax;
            result.Island3DCentroids = centroids;
            result.IslandCount = islandCount;
            result.SymmetryTable = new Dictionary<int, (int, int)>();
            
            return result;
        }

        /// <summary>
        /// 三角形の隣接リストを構築（頂点共有ベース）
        /// </summary>
        private static List<int>[] BuildTriangleAdjacency(int[] triangles, int triCount)
        {
            var triNeighbors = new List<int>[triCount];
            var vertexToTris = new Dictionary<int, List<int>>();

            for (int ti = 0; ti < triangles.Length; ti += 3)
            {
                int triIndex = ti / 3;
                int a = triangles[ti];
                int b = triangles[ti + 1];
                int c = triangles[ti + 2];

                // 各頂点が属する三角形を記録
                AddVertexTriangle(vertexToTris, a, triIndex);
                AddVertexTriangle(vertexToTris, b, triIndex);
                AddVertexTriangle(vertexToTris, c, triIndex);
                
                triNeighbors[triIndex] = new List<int>(6);
            }

            // 頂点共有から隣接関係を生成
            foreach (var kv in vertexToTris)
            {
                var list = kv.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        int t0 = list[i];
                        int t1 = list[j];
                        triNeighbors[t0].Add(t1);
                        triNeighbors[t1].Add(t0);
                    }
                }
            }

            return triNeighbors;
        }

        private static void AddVertexTriangle(Dictionary<int, List<int>> vertexToTris, int vertexIndex, int triIndex)
        {
            if (!vertexToTris.TryGetValue(vertexIndex, out var list))
            {
                list = new List<int>(4);
                vertexToTris[vertexIndex] = list;
            }
            list.Add(triIndex);
        }

        /// <summary>
        /// BFSでアイランドIDを割り当て
        /// </summary>
        private static (int[] triIslandId, int islandCount) AssignIslandIds(List<int>[] triNeighbors, int triCount)
        {
            var triIslandId = new int[triCount];
            for (int i = 0; i < triCount; i++) triIslandId[i] = -1;

            int islandId = 0;
            var queue = new Queue<int>();

            for (int t = 0; t < triCount; t++)
            {
                if (triIslandId[t] >= 0) continue;
                
                triIslandId[t] = islandId;
                queue.Clear();
                queue.Enqueue(t);

                while (queue.Count > 0)
                {
                    int cur = queue.Dequeue();
                    var neighbors = triNeighbors[cur];
                    if (neighbors == null) continue;

                    for (int k = 0; k < neighbors.Count; k++)
                    {
                        int nt = neighbors[k];
                        if (triIslandId[nt] < 0)
                        {
                            triIslandId[nt] = islandId;
                            queue.Enqueue(nt);
                        }
                    }
                }
                
                islandId++;
            }

            return (triIslandId, islandId);
        }

        /// <summary>
        /// 各アイランドのUV AABBを計算
        /// </summary>
        private static (Vector2[] islandMin, Vector2[] islandMax) ComputeIslandBounds(
            int[] triangles, Vector2[] uvs, int[] triIslandId, int islandCount)
        {
            var islandMin = new Vector2[islandCount];
            var islandMax = new Vector2[islandCount];
            
            for (int i = 0; i < islandCount; i++)
            {
                islandMin[i] = new Vector2(float.MaxValue, float.MaxValue);
                islandMax[i] = new Vector2(float.MinValue, float.MinValue);
            }

            for (int ti = 0; ti < triangles.Length; ti += 3)
            {
                int triIndex = ti / 3;
                int id = triIslandId[triIndex];
                if (id < 0) continue;

                int a = triangles[ti];
                int b = triangles[ti + 1];
                int c = triangles[ti + 2];
                
                if (a >= uvs.Length || b >= uvs.Length || c >= uvs.Length) continue;

                var ua = uvs[a];
                var ub = uvs[b];
                var uc = uvs[c];
                
                islandMin[id] = Vector2.Min(islandMin[id], ua);
                islandMin[id] = Vector2.Min(islandMin[id], ub);
                islandMin[id] = Vector2.Min(islandMin[id], uc);
                islandMax[id] = Vector2.Max(islandMax[id], ua);
                islandMax[id] = Vector2.Max(islandMax[id], ub);
                islandMax[id] = Vector2.Max(islandMax[id], uc);
            }

            return (islandMin, islandMax);
        }

        /// <summary>
        /// 各アイランドの3D空間での重心を計算
        /// </summary>
        private static Vector3[] ComputeIsland3DCentroids(
            int[] triangles, Vector3[] vertices, int[] triIslandId, int islandCount)
        {
            var centroids = new Vector3[islandCount];
            var vertexCounts = new int[islandCount];

            for (int ti = 0; ti < triangles.Length; ti += 3)
            {
                int triIndex = ti / 3;
                if (triIndex >= triIslandId.Length) continue;
                
                int islandId = triIslandId[triIndex];
                if (islandId < 0 || islandId >= islandCount) continue;

                int a = triangles[ti];
                int b = triangles[ti + 1];
                int c = triangles[ti + 2];

                if (a >= 0 && a < vertices.Length)
                {
                    centroids[islandId] += vertices[a];
                    vertexCounts[islandId]++;
                }
                if (b >= 0 && b < vertices.Length)
                {
                    centroids[islandId] += vertices[b];
                    vertexCounts[islandId]++;
                }
                if (c >= 0 && c < vertices.Length)
                {
                    centroids[islandId] += vertices[c];
                    vertexCounts[islandId]++;
                }
            }

            // 平均して重心に
            for (int i = 0; i < islandCount; i++)
            {
                if (vertexCounts[i] > 0)
                {
                    centroids[i] /= vertexCounts[i];
                }
            }

            return centroids;
        }

        /// <summary>
        /// UV座標が指定アイランドのAABB内にあるかチェック
        /// </summary>
        public static bool IsInIslandBounds(UvIslandData islandData, int islandId, Vector2 uv, float margin = 1e-4f)
        {
            if (islandId < 0 || islandData.IslandMin == null || islandId >= islandData.IslandMin.Length)
                return false;

            var min = islandData.IslandMin[islandId];
            var max = islandData.IslandMax[islandId];
            
            return uv.x >= min.x - margin && uv.x <= max.x + margin &&
                   uv.y >= min.y - margin && uv.y <= max.y + margin;
        }

        /// <summary>
        /// 三角形インデックスからアイランドIDを取得
        /// </summary>
        public static int GetIslandId(UvIslandData islandData, int triangleIndex)
        {
            if (islandData.TriangleToIslandId == null || triangleIndex < 0 || 
                triangleIndex >= islandData.TriangleToIslandId.Length)
            {
                return -1;
            }
            return islandData.TriangleToIslandId[triangleIndex];
        }
    }
}
