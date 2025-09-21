#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator.Utils
{
    /// <summary>
    /// UV三角形の隣接構築・シード探索・接続判定の共通ユーティリティ
    /// Editor専用の軽量ロジックとして集約
    /// </summary>
    public static class EditorUvUtils
    {
        // 共有UVアイランドキャッシュ
        private static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<int>>> SharedIslandCache
            = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<int>>>();

        public static void ClearSharedIslandCache() => SharedIslandCache.Clear();

        public static System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<int>>
            GetOrBuildIslandCache(string rendererPath, Mesh mesh, int submeshIndex)
        {
            if (mesh == null || submeshIndex < 0 || submeshIndex >= mesh.subMeshCount) return null;
            string key = rendererPath + "|" + mesh.GetInstanceID().ToString();
            if (!SharedIslandCache.TryGetValue(key, out var perSub))
            {
                perSub = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<int>>();
                SharedIslandCache[key] = perSub;
            }
            if (perSub.ContainsKey(submeshIndex)) return perSub;

            int[] triangles = mesh.GetTriangles(submeshIndex);
            Vector2[] uvs = mesh.uv;
            if (triangles == null || uvs == null) return null;
            var adjacency = BuildTriangleAdjacencyListList(triangles);
            int triCount = triangles.Length / 3;
            var processed = new bool[triCount];
            for (int start = 0; start < triCount; start++)
            {
                if (processed[start]) continue;
                var island = new System.Collections.Generic.HashSet<int>();
                var stack = new System.Collections.Generic.Stack<int>();
                processed[start] = true; stack.Push(start);
                while (stack.Count > 0)
                {
                    int t = stack.Pop(); island.Add(t);
                    foreach (int nb in adjacency[t])
                    {
                        if (!processed[nb] && AreUVTrianglesConnected(triangles, uvs, t, nb, AppSettings.UV_THRESHOLD_DEFAULT))
                        { processed[nb] = true; stack.Push(nb); }
                    }
                }
                foreach (int t in island) perSub[t] = island;
            }
            return perSub;
        }
        /// <summary>
        /// 三角形隣接（List<List<int>> 版）を構築
        /// </summary>
        public static List<List<int>> BuildTriangleAdjacencyListList(int[] triangles)
        {
            int triangleCount = triangles.Length / 3;
            var adjacency = new List<List<int>>();
            for (int i = 0; i < triangleCount; i++)
            {
                adjacency.Add(new List<int>());
            }

            var edgeToTriangles = new Dictionary<(int, int), List<int>>();
            for (int ti = 0; ti < triangleCount; ti++)
            {
                int baseIdx = ti * 3;
                if (baseIdx + 2 >= triangles.Length) continue;

                int a = triangles[baseIdx];
                int b = triangles[baseIdx + 1];
                int c = triangles[baseIdx + 2];

                var edges = new[]
                {
                    (Mathf.Min(a, b), Mathf.Max(a, b)),
                    (Mathf.Min(b, c), Mathf.Max(b, c)),
                    (Mathf.Min(c, a), Mathf.Max(c, a))
                };

                foreach (var edge in edges)
                {
                    if (!edgeToTriangles.ContainsKey(edge)) edgeToTriangles[edge] = new List<int>();
                    edgeToTriangles[edge].Add(ti);
                }
            }

            foreach (var kvp in edgeToTriangles)
            {
                var tris = kvp.Value;
                for (int i = 0; i < tris.Count; i++)
                {
                    for (int j = i + 1; j < tris.Count; j++)
                    {
                        int t1 = tris[i];
                        int t2 = tris[j];
                        adjacency[t1].Add(t2);
                        adjacency[t2].Add(t1);
                    }
                }
            }

            return adjacency;
        }


        /// <summary>
        /// UV座標からシード三角形を見つける（内部にあれば最優先、なければ重心最寄り）
        /// </summary>
        public static int FindSeedTriangleByUV(int[] triangles, Vector2[] uvs, Vector2 seedUV)
        {
            int triangleCount = triangles.Length / 3;
            int bestIdx = -1;
            float bestDist2 = float.MaxValue;

            for (int ti = 0; ti < triangleCount; ti++)
            {
                int baseIdx = ti * 3;
                if (baseIdx + 2 >= triangles.Length) continue;

                int a = triangles[baseIdx], b = triangles[baseIdx + 1], c = triangles[baseIdx + 2];
                if (a >= uvs.Length || b >= uvs.Length || c >= uvs.Length) continue;

                Vector2 uvA = uvs[a], uvB = uvs[b], uvC = uvs[c];

                Vector3 bCoords = EditorMeshUtils.GetBarycentric(seedUV, uvA, uvB, uvC);
                if (bCoords.x >= 0 && bCoords.y >= 0 && bCoords.z >= 0)
                {
                    return ti;
                }

                Vector2 centroid = (uvA + uvB + uvC) / 3f;
                float d2 = (centroid - seedUV).sqrMagnitude;
                if (d2 < bestDist2)
                {
                    bestDist2 = d2;
                    bestIdx = ti;
                }
            }

            return bestIdx;
        }

        /// <summary>
        /// 2つの三角形がUV空間で接続されているかを判定
        /// </summary>
        public static bool AreUVTrianglesConnected(int[] triangles, Vector2[] uvs, int triA, int triB, float uvThreshold)
        {
            if (triA * 3 + 2 >= triangles.Length || triB * 3 + 2 >= triangles.Length)
                return false;

            int a1 = triangles[triA * 3], a2 = triangles[triA * 3 + 1], a3 = triangles[triA * 3 + 2];
            int b1 = triangles[triB * 3], b2 = triangles[triB * 3 + 1], b3 = triangles[triB * 3 + 2];

            if (a1 >= uvs.Length || a2 >= uvs.Length || a3 >= uvs.Length ||
                b1 >= uvs.Length || b2 >= uvs.Length || b3 >= uvs.Length)
                return false;

            var uvA1 = uvs[a1]; var uvA2 = uvs[a2]; var uvA3 = uvs[a3];
            var uvB1 = uvs[b1]; var uvB2 = uvs[b2]; var uvB3 = uvs[b3];

            float[] distances = {
                Vector2.Distance(uvA1, uvB1), Vector2.Distance(uvA1, uvB2), Vector2.Distance(uvA1, uvB3),
                Vector2.Distance(uvA2, uvB1), Vector2.Distance(uvA2, uvB2), Vector2.Distance(uvA2, uvB3),
                Vector2.Distance(uvA3, uvB1), Vector2.Distance(uvA3, uvB2), Vector2.Distance(uvA3, uvB3)
            };

            foreach (float dist in distances)
            {
                if (dist <= uvThreshold) return true;
            }
            return false;
        }

        /// <summary>
        /// 指定したシード三角形から、隣接リストに基づき同一UVアイランドの三角形集合を列挙
        /// </summary>
        public static HashSet<int> EnumerateUVIslandTriangles(int[] triangles, List<List<int>> adjacency, int seedTriangle)
        {
            var islandVisitedTris = new HashSet<int>();
            if (triangles == null || adjacency == null) return islandVisitedTris;

            int triangleCount = triangles.Length / 3;
            if (triangleCount <= 0 || seedTriangle < 0 || seedTriangle >= triangleCount) return islandVisitedTris;

            var visited = new bool[triangleCount];
            var stack = new System.Collections.Generic.Stack<int>();
            stack.Push(seedTriangle);
            visited[seedTriangle] = true;

            while (stack.Count > 0)
            {
                int t = stack.Pop();
                islandVisitedTris.Add(t);

                if (t < 0 || t >= adjacency.Count) continue;
                var neighbors = adjacency[t];
                if (neighbors == null) continue;
                foreach (int nb in neighbors)
                {
                    if (nb < 0 || nb >= triangleCount) continue;
                    if (!visited[nb])
                    {
                        visited[nb] = true;
                        stack.Push(nb);
                    }
                }
            }

            return islandVisitedTris;
        }
    }
}
#endif


