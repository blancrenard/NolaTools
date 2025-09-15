#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Data;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator
{
    public partial class DistanceMaskBaker
    {
        #region UV Islands

        private void ApplyUVIslandsToVertexDistances()
        {
            if (settings?.UVIslandMasks == null || settings.UVIslandMasks.Count == 0) return;

            var islandVertices = new HashSet<int>();
            var expandedInsideVertices = new HashSet<int>();
            var pathToSubIndices = new Dictionary<string, List<int>>();
            for (int si = 0; si < subDatas.Count; si++)
            {
                string path = (subRendererPaths.Count > si) ? subRendererPaths[si] : null;
                if (string.IsNullOrEmpty(path)) continue;
                if (!pathToSubIndices.TryGetValue(path, out var list)) { list = new List<int>(); pathToSubIndices[path] = list; }
                list.Add(si);
            }
            foreach (var kv in pathToSubIndices)
            {
                string path = kv.Key; var subList = kv.Value;
                var masksForPath = settings.UVIslandMasks.FindAll(m => m != null && m.rendererPath == path);
                if (masksForPath == null || masksForPath.Count == 0) continue;

                var triAdjBySub = new Dictionary<int, List<List<int>>>();
                foreach (int si in subList)
                {
                    var (tri, _) = subDatas[si];
                    triAdjBySub[si] = NolaTools.FurMaskGenerator.Utils.EditorUvUtils.BuildTriangleAdjacencyListList(tri);
                }
                var edgeMap = BuildPositionEdgeMap(subList);

                foreach (var m in masksForPath)
                {
                    int startSub = -1;
                    foreach (int si in subList)
                    {
                        if (si < subMeshIndices.Count && subMeshIndices[si] == m.submeshIndex)
                        {
                            startSub = si;
                            break;
                        }
                    }
                    if (startSub < 0) continue;
                    var (seedTriArr, _) = subDatas[startSub];
                    int seedTri = FindSeedTriangleByUV(seedTriArr, m.seedUV);
                    if (seedTri < 0) continue;

                    var islandTris = GrowUVIsland(subList, startSub, seedTri, triAdjBySub, edgeMap, m.uvThreshold);
                    foreach (var (si, ti) in islandTris)
                    {
                        var (tri, _) = subDatas[si];
                        int baseIdx = ti * 3;
                        if (baseIdx + 2 < tri.Length)
                        {
                            islandVertices.Add(tri[baseIdx]);
                            islandVertices.Add(tri[baseIdx + 1]);
                            islandVertices.Add(tri[baseIdx + 2]);
                        }
                    }
                }
            }

            islandAnchorFlags = new bool[verts.Count];
            foreach (int vi in islandVertices)
            {
                if (vi < islandAnchorFlags.Length)
                {
                    islandAnchorFlags[vi] = true;
                    if (vDist != null && vi < vDist.Length)
                    {
                        vDist[vi] = 0f;
                    }
                }
            }
        }

        private int FindSeedTriangleByUV(int[] tri, Vector2 seedUV)
        {
            var uvsArr = (uvs != null) ? uvs.ToArray() : System.Array.Empty<Vector2>();
            return NolaTools.FurMaskGenerator.Utils.EditorUvUtils.FindSeedTriangleByUV(tri, uvsArr, seedUV);
        }

        private bool IsPointInUVTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector3 bCoords = EditorMeshUtils.GetBarycentric(p, a, b, c);
            return bCoords.x >= 0 && bCoords.y >= 0 && bCoords.z >= 0;
        }

        private HashSet<(int subIdx, int triIdx)> GrowUVIsland(List<int> subList, int startSub, int seedTri, Dictionary<int, List<List<int>>> triAdjBySub, Dictionary<(Vector3, Vector3), List<(int, int)>> edgeMap, float uvThreshold)
        {
            var island = new HashSet<(int, int)>();
            // 起点サブメッシュのアイランドをまず列挙
            if (!triAdjBySub.TryGetValue(startSub, out var startAdj)) return island;
            var localIsland = NolaTools.FurMaskGenerator.Utils.EditorUvUtils.EnumerateUVIslandTriangles(subDatas[startSub].tri, startAdj, seedTri);
            foreach (int t in localIsland) island.Add((startSub, t));

            return island;
        }

        private bool AreUVTrianglesConnected(int subIdx, int triA, int triB, float uvThreshold)
        {
            var tri = subDatas[subIdx].tri;
            return NolaTools.FurMaskGenerator.Utils.EditorUvUtils.AreUVTrianglesConnected(tri, uvs.ToArray(), triA, triB, uvThreshold);
        }

        private Dictionary<(Vector3, Vector3), List<(int, int)>> BuildPositionEdgeMap(List<int> subList)
        {
            var edgeMap = new Dictionary<(Vector3, Vector3), List<(int, int)>>();
            foreach (int si in subList)
            {
                var (tri, _) = subDatas[si];
                for (int ti = 0; ti < tri.Length / 3; ti++)
                {
                    int baseIdx = ti * 3;
                    if (baseIdx + 2 >= tri.Length) continue;

                    int a = tri[baseIdx], b = tri[baseIdx + 1], c = tri[baseIdx + 2];
                    if (a >= verts.Count || b >= verts.Count || c >= verts.Count) continue;

                    Vector3 va = verts[a], vb = verts[b], vc = verts[c];
                    AddEdgeToMap(edgeMap, va, vb, (si, ti));
                    AddEdgeToMap(edgeMap, vb, vc, (si, ti));
                    AddEdgeToMap(edgeMap, vc, va, (si, ti));
                }
            }
            return edgeMap;
        }

        private void AddEdgeToMap(Dictionary<(Vector3, Vector3), List<(int, int)>> edgeMap, Vector3 v1, Vector3 v2, (int subIdx, int triIdx) triInfo)
        {
            var key = v1.x < v2.x || (v1.x == v2.x && v1.y < v2.y) || (v1.x == v2.x && v1.y == v2.y && v1.z < v2.z) ?
                     (v1, v2) : (v2, v1);

            if (!edgeMap.TryGetValue(key, out var list)) { list = new List<(int, int)>(); edgeMap[key] = list; }
            if (!list.Contains(triInfo)) list.Add(triInfo);
        }

        #endregion
    }
}
#endif


