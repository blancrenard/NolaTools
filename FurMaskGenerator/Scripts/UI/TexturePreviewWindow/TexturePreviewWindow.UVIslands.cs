#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mask.Generator.Constants;

namespace Mask.Generator.UI
{
    public partial class TexturePreviewWindow
    {
        private HashSet<int> GetUVIslandTriangles(Mesh mesh, int submeshIndex, Vector2 seedUV)
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

                int seedTriangle = FindSeedTriangleByUV(triangles, uvs, seedUV);
                if (seedTriangle < 0)
                    return result;

                var adjacency = BuildTriangleAdjacency(triangles);

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
                            if (!visited[neighborTriangle] &&
                                AreUVTrianglesConnected(triangles, uvs, currentTriangle, neighborTriangle, 0.1f))
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

        private int FindSeedTriangleByUV(int[] triangles, Vector2[] uvs, Vector2 seedUV)
        {
            return Mask.Generator.Utils.EditorUvUtils.FindSeedTriangleByUV(triangles, uvs, seedUV);
        }

        private System.Collections.Generic.List<System.Collections.Generic.List<int>> BuildTriangleAdjacency(int[] triangles)
        {
            return Mask.Generator.Utils.EditorUvUtils.BuildTriangleAdjacencyListList(triangles);
        }

        private bool AreUVTrianglesConnected(int[] triangles, Vector2[] uvs, int triA, int triB, float uvThreshold)
        {
            return Mask.Generator.Utils.EditorUvUtils.AreUVTrianglesConnected(triangles, uvs, triA, triB, uvThreshold);
        }
    }
}
#endif

