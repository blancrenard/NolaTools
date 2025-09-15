#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Data;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator
{
    public partial class TextureProcessor
    {
        #region UV Island Processing

        private void ApplyUVIslandMasks(Dictionary<string, List<Texture2D>> matTex)
        {
            if (uvIslandMasks == null || uvIslandMasks.Count == 0) return;

            var masksBySub = GroupMasksBySubmesh(uvIslandMasks);

            for (int subIndex = 0; subIndex < subDatas.Count; subIndex++)
            {
                var (tri, _) = subDatas[subIndex];
                var key = (
                    subRendererPaths.Count > subIndex ? subRendererPaths[subIndex] : null,
                    subMeshIndices.Count > subIndex ? subMeshIndices[subIndex] : 0
                );
                if (string.IsNullOrEmpty(key.Item1)) continue;
                if (!masksBySub.TryGetValue((key.Item1, key.Item2), out var targets) || targets == null || targets.Count == 0) continue;

                int triangleCount = tri.Length / 3;
                var adjacency = NolaTools.FurMaskGenerator.Utils.EditorUvUtils.BuildTriangleAdjacencyListList(tri);

                if (!subIndexToTexLocator.TryGetValue(subIndex, out var locator)) continue;
                if (!matTex.TryGetValue(locator.mat, out var textures) || locator.texIdx < 0 || locator.texIdx >= textures.Count) continue;
                var targetTex = textures[locator.texIdx];

                Color[] targetBuffer = targetTex.GetPixels();
                foreach (var mask in targets)
                {
                    if (mask == null) continue;
                    var result = FloodFillIslandAndPaintBlackIntoBuffer(mask.uvPosition, tri, adjacency, triangleCount, matTex);
                }

                targetTex.SetPixels(targetBuffer);
                targetTex.Apply(false);
            }
        }

        private Dictionary<(string path, int submesh), List<UVIslandMaskData>> GroupMasksBySubmesh(List<UVIslandMaskData> masks)
        {
            var dict = new Dictionary<(string path, int submesh), List<UVIslandMaskData>>();
            foreach (var m in masks)
            {
                if (m == null) continue;
                var key = (m.rendererPath, m.submeshIndex);
                if (!dict.TryGetValue(key, out var list)) { list = new List<UVIslandMaskData>(); dict[key] = list; }
                list.Add(m);
            }
            return dict;
        }

        private (HashSet<int> islandVertices, HashSet<int> islandVisitedTriangles) FloodFillIslandAndPaintBlackIntoBuffer(
            Vector2 seedUV, int[] tri, List<List<int>> adjacency, int triangleCount, Dictionary<string, List<Texture2D>> matTex)
        {
            int seed = FindSeedTriangleByUV(tri, seedUV);
            if (seed < 0) return (new HashSet<int>(), new HashSet<int>());

            var islandVertices = new HashSet<int>();
            var islandVisitedTris = NolaTools.FurMaskGenerator.Utils.EditorUvUtils.EnumerateUVIslandTriangles(tri, adjacency, seed);

            Color[] targetBuffer = null;
            int bufferWidth = 0, bufferHeight = 0;
            string firstMat = null;
            Texture2D targetTexture = null;

            foreach (var kv in matTex)
            {
                if (kv.Value != null && kv.Value.Count > 0)
                {
                    firstMat = kv.Key;
                    targetTexture = kv.Value[0];
                    targetBuffer = targetTexture.GetPixels();
                    bufferWidth = targetTexture.width;
                    bufferHeight = targetTexture.height;
                    break;
                }
            }

            foreach (int t in islandVisitedTris)
            {
                int ia = tri[t * 3 + 0];
                int ib = tri[t * 3 + 1];
                int ic = tri[t * 3 + 2];

                if (targetBuffer != null && ia < uvs.Count && ib < uvs.Count && ic < uvs.Count)
                {
                    FillTriBlackIntoBuffer(targetBuffer, bufferWidth, bufferHeight,
                        uvs[ia], uvs[ib], uvs[ic]);
                }

                islandVertices.Add(ia);
                islandVertices.Add(ib);
                islandVertices.Add(ic);
            }

            if (targetTexture != null && targetBuffer != null)
            {
                targetTexture.SetPixels(targetBuffer);
                targetTexture.Apply(false);
            }

            return (islandVertices, islandVisitedTris);
        }


        #endregion
    }
}
#endif

