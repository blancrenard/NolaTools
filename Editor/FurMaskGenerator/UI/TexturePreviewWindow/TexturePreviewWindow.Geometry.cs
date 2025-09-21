#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator.UI
{
    public partial class TexturePreviewWindow
    {
        // UV座標からワールド座標を算出（サブメッシュのUV三角形に基づく）。失敗時は最近傍頂点へフォールバック
        private bool TryComputeWorldPositionFromUV(Renderer renderer, int subIndex, Vector2 uv, out Vector3 worldPos)
        {
            worldPos = Vector3.zero;
            if (renderer == null) return false;
            Mesh mesh = EditorMeshUtils.GetMeshForRenderer(renderer, out bool isBakedTempMesh);
            if (mesh == null) return false;
            try
            {
                int sub = Mathf.Clamp(subIndex, 0, mesh.subMeshCount - 1);
                int[] triangles = mesh.GetTriangles(sub);
                Vector2[] uvs = mesh.uv;
                if (triangles == null || triangles.Length == 0 || uvs == null || uvs.Length != mesh.vertexCount)
                {
                    return false;
                }

                int triIdx = FindSeedTriangleByUV(triangles, uvs, uv);
                if (triIdx >= 0)
                {
                    int baseIdx = triIdx * 3;
                    if (baseIdx + 2 < triangles.Length)
                    {
                        int ia = triangles[baseIdx + 0];
                        int ib = triangles[baseIdx + 1];
                        int ic = triangles[baseIdx + 2];
                        if (ia < uvs.Length && ib < uvs.Length && ic < uvs.Length)
                        {
                            Vector3 bary = EditorMeshUtils.GetBarycentric(uv, uvs[ia], uvs[ib], uvs[ic]);
                            if (bary.x >= 0f && bary.y >= 0f && bary.z >= 0f)
                            {
                                Vector3 va = mesh.vertices[ia];
                                Vector3 vb = mesh.vertices[ib];
                                Vector3 vc = mesh.vertices[ic];
                                Vector3 local = va * bary.x + vb * bary.y + vc * bary.z;
                                worldPos = renderer.transform.TransformPoint(local);
                                return true;
                            }
                        }
                    }
                }

                // フォールバック: 近傍UVの頂点をワールド座標として採用
                int nearestVi = -1; float best = float.MaxValue;
                for (int i = 0; i < triangles.Length; i++)
                {
                    int vi = triangles[i];
                    if (vi >= 0 && vi < uvs.Length)
                    {
                        float d = Vector2.SqrMagnitude(uvs[vi] - uv);
                        if (d < best)
                        {
                            best = d;
                            nearestVi = vi;
                        }
                    }
                }
                if (nearestVi >= 0)
                {
                    worldPos = renderer.transform.TransformPoint(mesh.vertices[nearestVi]);
                    return true;
                }
                return false;
            }
            finally
            {
                if (isBakedTempMesh)
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
        }

        // フォールバック: 含有三角形の重心からワールド座標を推定
        private bool TryComputeWorldFromContainingTriangle(Renderer renderer, int subIndex, Vector2 uv, out Vector3 world)
        {
            world = Vector3.zero;
            if (renderer == null) return false;
            Mesh mesh = EditorMeshUtils.GetMeshForRenderer(renderer, out bool baked);
            if (mesh == null) return false;
            try
            {
                int sub = Mathf.Clamp(subIndex, 0, mesh.subMeshCount - 1);
                int[] triangles = mesh.GetTriangles(sub);
                Vector2[] uvs = mesh.uv;
                if (triangles == null || triangles.Length == 0 || uvs == null || uvs.Length != mesh.vertexCount) return false;
                int triIdx = FindSeedTriangleByUV(triangles, uvs, uv);
                if (triIdx < 0) return false;
                int ia = triangles[triIdx * 3 + 0];
                int ib = triangles[triIdx * 3 + 1];
                int ic = triangles[triIdx * 3 + 2];
                if (ia >= uvs.Length || ib >= uvs.Length || ic >= uvs.Length) return false;
                Vector3 va = mesh.vertices[ia];
                Vector3 vb = mesh.vertices[ib];
                Vector3 vc = mesh.vertices[ic];
                Vector3 local = (va + vb + vc) / 3f;
                world = renderer.transform.TransformPoint(local);
                return true;
            }
            finally
            {
                if (baked)
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
        }

        // 厳密: UVがサブメッシュのいずれかの三角形内にあるか
        private bool IsUVInsideSubmeshStrict(Renderer renderer, int subIndex, Vector2 uv)
        {
            if (renderer == null) return false;
            Mesh mesh = EditorMeshUtils.GetMeshForRenderer(renderer, out bool baked);
            if (mesh == null)
            {
                return false;
            }
            try
            {
                int sub = Mathf.Clamp(subIndex, 0, mesh.subMeshCount - 1);
                int[] triangles = mesh.GetTriangles(sub);
                Vector2[] uvs = mesh.uv;
                if (triangles == null || triangles.Length == 0 || uvs == null || uvs.Length != mesh.vertexCount) return false;
                int triIdx = FindSeedTriangleByUV(triangles, uvs, uv);
                if (triIdx < 0) return false;
                int ia = triangles[triIdx * 3 + 0];
                int ib = triangles[triIdx * 3 + 1];
                int ic = triangles[triIdx * 3 + 2];
                if (ia >= uvs.Length || ib >= uvs.Length || ic >= uvs.Length) return false;
                return IsPointInTriangleUV(uv, uvs[ia], uvs[ib], uvs[ic]);
            }
            finally
            {
                if (baked)
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
        }

        private bool IsPointInTriangleUV(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            const float epsilon = AppSettings.VALID_PIXEL_THRESHOLD; // 境界付近を内側として扱う許容誤差
            float s = a.y * c.x - a.x * c.y + (c.y - a.y) * p.x + (a.x - c.x) * p.y;
            float t = a.x * b.y - a.y * b.x + (a.y - b.y) * p.x + (b.x - a.x) * p.y;
            if ((s < -epsilon) != (t < -epsilon)) return false;
            float area = -b.y * c.x + a.y * (c.x - b.x) + a.x * (b.y - c.y) + b.x * c.y;
            if (area < 0.0f)
            {
                s = -s; t = -t; area = -area;
            }
            return s >= -epsilon && t >= -epsilon && (s + t) <= area + epsilon;
        }

        /// <summary>
        /// UV座標がサブメッシュの近傍にあるかを判定（許容範囲内）
        /// </summary>
        private bool IsUVNearSubmesh(Renderer renderer, int subIndex, Vector2 uv, float tolerance)
        {
            if (renderer == null) return false;
            Mesh mesh = EditorMeshUtils.GetMeshForRenderer(renderer, out bool baked);
            if (mesh == null) return false;

            try
            {
                int sub = Mathf.Clamp(subIndex, 0, mesh.subMeshCount - 1);
                int[] triangles = mesh.GetTriangles(sub);
                Vector2[] uvs = mesh.uv;
                if (triangles == null || triangles.Length == 0 || uvs == null || uvs.Length != mesh.vertexCount)
                    return false;

                // すべてのUV頂点との距離をチェック
                for (int i = 0; i < triangles.Length; i++)
                {
                    int vertexIndex = triangles[i];
                    if (vertexIndex >= 0 && vertexIndex < uvs.Length)
                    {
                        float distance = Vector2.Distance(uv, uvs[vertexIndex]);
                        if (distance <= tolerance)
                        {
                            return true;
                        }
                    }
                }

                // 三角形の重心との距離もチェック
                int triangleCount = triangles.Length / 3;
                for (int ti = 0; ti < triangleCount; ti++)
                {
                    int baseIdx = ti * 3;
                    if (baseIdx + 2 >= triangles.Length) continue;

                    int ia = triangles[baseIdx];
                    int ib = triangles[baseIdx + 1];
                    int ic = triangles[baseIdx + 2];

                    if (ia >= uvs.Length || ib >= uvs.Length || ic >= uvs.Length) continue;

                    Vector2 centroid = (uvs[ia] + uvs[ib] + uvs[ic]) / 3f;
                    float distance = Vector2.Distance(uv, centroid);
                    if (distance <= tolerance)
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                if (baked)
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
        }
    }
}
#endif

