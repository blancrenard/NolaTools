#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Rendering;

namespace GroomingTool2.Utils
{
    /// <summary>
    /// メッシュ取得や重心座標計算など、Editor依存の軽量ユーティリティ
    /// </summary>
    internal static class EditorMeshUtils
    {
        /// <summary>
        /// Renderer からメッシュを取得する。SkinnedMeshRenderer の場合は一時メッシュに Bake して返す。
        /// 呼び出し側で isBakedTempMesh が true のとき DestroyImmediate してください。
        /// </summary>
        public static Mesh GetMeshForRenderer(Renderer renderer, out bool isBakedTempMesh)
        {
            isBakedTempMesh = false;
            if (renderer == null) return null;

            if (renderer is SkinnedMeshRenderer smr)
            {
                var baked = new Mesh { indexFormat = IndexFormat.UInt32 };
                smr.BakeMesh(baked, true);
                isBakedTempMesh = true;
                return baked;
            }

            if (renderer.TryGetComponent<MeshFilter>(out var mf))
            {
                return mf.sharedMesh;
            }

            return null;
        }

        /// <summary>
        /// 法線・タンジェントが欠けていれば再計算する。
        /// </summary>
        public static void EnsureMeshNormalsAndTangents(Mesh mesh)
        {
            if (mesh == null) return;

            if (mesh.normals == null || mesh.normals.Length != mesh.vertexCount)
            {
                mesh.RecalculateNormals();
            }

            if (mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount)
            {
                mesh.RecalculateTangents();
            }
        }

        /// <summary>
        /// 2D平面上での重心座標を計算する。退化三角形の場合は (-1,-1,-1) を返す。
        /// </summary>
        public static Vector3 GetBarycentric(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            if (BarycentricUtils.TryCompute2D(p, a, b, c, out var bary))
                return bary;
            return new Vector3(-1f, -1f, -1f);
        }
    }
}
#endif

