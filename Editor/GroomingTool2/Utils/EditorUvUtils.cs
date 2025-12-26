#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace GroomingTool2.Utils
{
    /// <summary>
    /// UV三角形の隣接構築やシード探索を行う軽量ユーティリティ
    /// </summary>
    internal static class EditorUvUtils
    {
        private const float DefaultUvThreshold = 0.1f;

        /// <summary>
        /// 三角形隣接（List&lt;List&lt;int&gt;&gt;版）を構築する。
        /// </summary>
        public static List<List<int>> BuildTriangleAdjacencyListList(int[] triangles)
        {
            int triangleCount = triangles?.Length > 0 ? triangles.Length / 3 : 0;
            var adjacency = new List<List<int>>(triangleCount);
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
                    if (!edgeToTriangles.ContainsKey(edge))
                    {
                        edgeToTriangles[edge] = new List<int>();
                    }
                    edgeToTriangles[edge].Add(ti);
                }
            }

            foreach (var tris in edgeToTriangles.Values)
            {
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
        /// UV座標からシード三角形を見つける（内部にあれば最優先、なければ重心最寄り）。
        /// </summary>
        public static int FindSeedTriangleByUV(int[] triangles, Vector2[] uvs, Vector2 seedUV)
        {
            if (triangles == null || uvs == null) return -1;

            int triangleCount = triangles.Length / 3;
            int bestIdx = -1;
            float bestDist2 = float.MaxValue;

            for (int ti = 0; ti < triangleCount; ti++)
            {
                int baseIdx = ti * 3;
                if (baseIdx + 2 >= triangles.Length) continue;

                int a = triangles[baseIdx];
                int b = triangles[baseIdx + 1];
                int c = triangles[baseIdx + 2];
                if (a >= uvs.Length || b >= uvs.Length || c >= uvs.Length) continue;

                Vector2 uvA = uvs[a];
                Vector2 uvB = uvs[b];
                Vector2 uvC = uvs[c];

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
        /// 指定したシード三角形から、隣接リストに基づき同一UVアイランドの三角形集合を列挙する。
        /// </summary>
        public static HashSet<int> EnumerateUVIslandTriangles(int[] triangles, List<List<int>> adjacency, int seedTriangle)
        {
            var islandVisitedTris = new HashSet<int>();
            if (triangles == null || adjacency == null) return islandVisitedTris;

            int triangleCount = triangles.Length / 3;
            if (triangleCount <= 0 || seedTriangle < 0 || seedTriangle >= triangleCount) return islandVisitedTris;

            var visited = new bool[triangleCount];
            var stack = new Stack<int>();
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
                    if (visited[nb]) continue;

                    visited[nb] = true;
                    stack.Push(nb);
                }
            }

            return islandVisitedTris;
        }

        /// <summary>
        /// 2つの三角形がUV空間で接続されているかを判定する。
        /// uvThreshold 以内に頂点が存在する場合に接続とみなす。
        /// </summary>
        public static bool AreUVTrianglesConnected(int[] triangles, Vector2[] uvs, int triA, int triB, float uvThreshold = DefaultUvThreshold)
        {
            if (triangles == null || uvs == null) return false;
            if (triA * 3 + 2 >= triangles.Length || triB * 3 + 2 >= triangles.Length) return false;

            int a1 = triangles[triA * 3];
            int a2 = triangles[triA * 3 + 1];
            int a3 = triangles[triA * 3 + 2];
            int b1 = triangles[triB * 3];
            int b2 = triangles[triB * 3 + 1];
            int b3 = triangles[triB * 3 + 2];

            if (a1 >= uvs.Length || a2 >= uvs.Length || a3 >= uvs.Length ||
                b1 >= uvs.Length || b2 >= uvs.Length || b3 >= uvs.Length)
            {
                return false;
            }

            var uvA1 = uvs[a1]; var uvA2 = uvs[a2]; var uvA3 = uvs[a3];
            var uvB1 = uvs[b1]; var uvB2 = uvs[b2]; var uvB3 = uvs[b3];

            float[] distances =
            {
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
    }
}
#endif

