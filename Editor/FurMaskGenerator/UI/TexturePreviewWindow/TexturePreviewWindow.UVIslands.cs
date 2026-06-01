#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using NolaTools.FurMaskGenerator.Constants;
using NolaTools.FurMaskGenerator.Utils;

namespace NolaTools.FurMaskGenerator.UI
{
    public partial class TexturePreviewWindow
    {
        private HashSet<int> GetUVIslandTriangles(string rendererPath, Mesh mesh, int submeshIndex, Vector2 seedUV)
        {
            if (!string.IsNullOrEmpty(rendererPath)
                && EditorUvUtils.TryGetUVIslandTriangles(rendererPath, mesh, submeshIndex, seedUV, out var cached)
                && cached != null)
            {
                return cached;
            }

            return GetUVIslandTrianglesUncached(mesh, submeshIndex, seedUV);
        }

        /// <summary>
        /// 共有キャッシュが使えない場合のフォールバック（従来と同じ flood fill）
        /// </summary>
        private HashSet<int> GetUVIslandTrianglesUncached(Mesh mesh, int submeshIndex, Vector2 seedUV)
        {
            var result = new HashSet<int>();
            if (mesh == null || submeshIndex < 0 || submeshIndex >= mesh.subMeshCount)
                return result;

            try
            {
                int[] triangles = mesh.GetTriangles(submeshIndex);
                if (triangles == null || triangles.Length == 0)
                    return result;

                Vector2[] uvs = mesh.uv;
                if (uvs == null || uvs.Length != mesh.vertexCount)
                    return result;

                int seedTriangle = EditorUvUtils.FindSeedTriangleByUV(triangles, uvs, seedUV);
                if (seedTriangle < 0)
                    return result;

                var adjacency = EditorUvUtils.BuildTriangleAdjacencyListList(triangles);
                var visited = new bool[triangles.Length / 3];
                var stack = new Stack<int>();

                stack.Push(seedTriangle);
                visited[seedTriangle] = true;

                while (stack.Count > 0)
                {
                    int currentTriangle = stack.Pop();
                    result.Add(currentTriangle);

                    if (currentTriangle < adjacency.Count)
                    {
                        foreach (int neighborTriangle in adjacency[currentTriangle])
                        {
                            if (!visited[neighborTriangle]
                                && EditorUvUtils.AreUVTrianglesConnected(
                                    triangles, uvs, currentTriangle, neighborTriangle, AppSettings.UV_THRESHOLD_DEFAULT))
                            {
                                visited[neighborTriangle] = true;
                                stack.Push(neighborTriangle);
                            }
                        }
                    }
                }

                return result;
            }
            catch (System.Exception ex)
            {
                Debug.LogError(string.Format(ErrorMessages.ERROR_UV_ISLAND_ACQUISITION, ex.Message));
                return result;
            }
        }
    }
}
#endif
