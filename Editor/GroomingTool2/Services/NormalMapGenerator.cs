using System;
using System.Buffers;
using System.Collections.Generic;
using GroomingTool2.Utils;
using UnityEngine;

namespace GroomingTool2.Services
{
    /// <summary>
    /// ノーマルマップテクスチャを生成するクラス
    /// 頂点方向データからテクスチャを作成
    /// </summary>
    internal sealed class NormalMapGenerator
    {
        /// <summary>
        /// ノーマルマップテクスチャを生成
        /// </summary>
        /// <param name="vertexDirections">頂点ごとの方向ベクトル配列</param>
        /// <param name="renderers">SkinnedMeshRenderer一覧</param>
        /// <param name="submeshesByRenderer">レンダラーごとの対象サブメッシュ</param>
        /// <param name="textureSize">テクスチャサイズ</param>
        /// <param name="uvPadding">UVパディング（Dilation回数）</param>
        /// <returns>生成されたノーマルマップ（呼び出し側で破棄してください）</returns>
        public Texture2D Generate(
            Vector3[] vertexDirections,
            List<SkinnedMeshRenderer> renderers,
            Dictionary<SkinnedMeshRenderer, List<int>> submeshesByRenderer,
            int textureSize,
            int uvPadding)
        {
            var normalMap = new Texture2D(textureSize, textureSize, TextureFormat.RGB24, false);
            var pixels = new Color32[textureSize * textureSize];
            var painted = new bool[textureSize * textureSize];

            // デフォルトの法線色（上向き）
            Color32 defaultNormal = new Color32(128, 128, 255, 255);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = defaultNormal;
            }

            int vertexOffset = 0;
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMesh == null) continue;

                var mesh = renderer.sharedMesh;
                EditorMeshUtils.EnsureMeshNormalsAndTangents(mesh);

                var uvs = mesh.uv;
                var normals = mesh.normals;
                var tangents = mesh.tangents;

                if (uvs == null || uvs.Length != mesh.vertexCount) continue;

                if (!submeshesByRenderer.TryGetValue(renderer, out var submeshIndices))
                {
                    submeshIndices = new List<int>();
                    for (int s = 0; s < mesh.subMeshCount; s++)
                        submeshIndices.Add(s);
                }

                foreach (var submeshIndex in submeshIndices)
                {
                    var triangles = mesh.GetTriangles(submeshIndex);

                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int idx0 = triangles[i];
                        int idx1 = triangles[i + 1];
                        int idx2 = triangles[i + 2];

                        Vector2 uv0 = uvs[idx0] * textureSize;
                        Vector2 uv1 = uvs[idx1] * textureSize;
                        Vector2 uv2 = uvs[idx2] * textureSize;

                        Vector3 dir0 = vertexDirections[idx0 + vertexOffset];
                        Vector3 dir1 = vertexDirections[idx1 + vertexOffset];
                        Vector3 dir2 = vertexDirections[idx2 + vertexOffset];

                        Vector3 n0 = normals[idx0];
                        Vector3 n1 = normals[idx1];
                        Vector3 n2 = normals[idx2];

                        Vector4 t0 = tangents[idx0];
                        Vector4 t1 = tangents[idx1];
                        Vector4 t2 = tangents[idx2];

                        PaintTriangle(textureSize, uv0, uv1, uv2, dir0, dir1, dir2, n0, n1, n2, t0, t1, t2, renderer.transform, pixels, painted);
                    }
                }

                vertexOffset += mesh.vertexCount;
            }

            // Dilation（塗られていない隣接ピクセルを埋める）- UVパディング値分膨張
            int dilationSteps = Mathf.Max(uvPadding, 1); // 最低1回は実行
            for (int step = 0; step < dilationSteps; step++)
            {
                Dilate(pixels, painted, textureSize);
            }

            normalMap.SetPixels32(pixels);
            normalMap.Apply();
            return normalMap;
        }

        /// <summary>
        /// 三角形を塗りつぶす
        /// </summary>
        private void PaintTriangle(
            int textureSize,
            Vector2 uv0, Vector2 uv1, Vector2 uv2,
            Vector3 dir0, Vector3 dir1, Vector3 dir2,
            Vector3 n0, Vector3 n1, Vector3 n2,
            Vector4 t0, Vector4 t1, Vector4 t2,
            Transform rendererTransform,
            Color32[] pixels, bool[] painted)
        {
            int minX = (int)Mathf.Min(uv0.x, uv1.x, uv2.x);
            int maxX = (int)Mathf.Ceil(Mathf.Max(uv0.x, uv1.x, uv2.x));
            int minY = (int)Mathf.Min(uv0.y, uv1.y, uv2.y);
            int maxY = (int)Mathf.Ceil(Mathf.Max(uv0.y, uv1.y, uv2.y));

            // 頂点ごとにワールド空間のT/Nを前計算
            Vector3 t0w = rendererTransform.TransformDirection(new Vector3(t0.x, t0.y, t0.z));
            Vector3 t1w = rendererTransform.TransformDirection(new Vector3(t1.x, t1.y, t1.z));
            Vector3 t2w = rendererTransform.TransformDirection(new Vector3(t2.x, t2.y, t2.z));

            Vector3 n0w = rendererTransform.TransformDirection(n0);
            Vector3 n1w = rendererTransform.TransformDirection(n1);
            Vector3 n2w = rendererTransform.TransformDirection(n2);

            for (int y = Mathf.Clamp(minY, 0, textureSize - 1); y <= Mathf.Clamp(maxY, 0, textureSize - 1); y++)
            {
                for (int x = Mathf.Clamp(minX, 0, textureSize - 1); x <= Mathf.Clamp(maxX, 0, textureSize - 1); x++)
                {
                    Vector3 b = EditorMeshUtils.GetBarycentric(new Vector2(x, y), uv0, uv1, uv2);
                    if (b.x >= 0 && b.y >= 0 && b.z >= 0)
                    {
                        Vector3 worldDir = (dir0 * b.x + dir1 * b.y + dir2 * b.z).normalized;

                        // 頂点でワールドに変換済みの T/N を補間
                        Vector3 Tw = (t0w * b.x + t1w * b.y + t2w * b.z).normalized;
                        Vector3 Nw = (n0w * b.x + n1w * b.y + n2w * b.z).normalized;
                        float handedness = t0.w * b.x + t1.w * b.y + t2.w * b.z;
                        Vector3 Bw = Vector3.Cross(Nw, Tw);
                        if (handedness < 0f) Bw = -Bw;
                        Bw.Normalize();

                        // 直接内積でタンジェント空間へ変換
                        Vector3 tangentSpaceDir = new Vector3(
                            Vector3.Dot(worldDir, Tw),
                            Vector3.Dot(worldDir, Bw),
                            Vector3.Dot(worldDir, Nw)
                        ).normalized;

                        int idx = y * textureSize + x;
                        float fx = tangentSpaceDir.x * 0.5f + 0.5f;
                        float fy = tangentSpaceDir.y * 0.5f + 0.5f;
                        float fz = tangentSpaceDir.z * 0.5f + 0.5f;
                        byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(fx * 255f), 0, 255);
                        byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(fy * 255f), 0, 255);
                        byte bz = (byte)Mathf.Clamp(Mathf.RoundToInt(fz * 255f), 0, 255);
                        pixels[idx] = new Color32(r, g, bz, 255);
                        painted[idx] = true;
                    }
                }
            }
        }

        /// <summary>
        /// ダイレーション処理（塗られていない隣接ピクセルを埋める）
        /// ArrayPoolを使用してGCアロケーションを削減
        /// </summary>
        private static void Dilate(Color32[] pixels, bool[] painted, int size)
        {
            int length = pixels.Length;
            var tempPixels = ArrayPool<Color32>.Shared.Rent(length);
            var tempPainted = ArrayPool<bool>.Shared.Rent(length);
            Array.Copy(pixels, tempPixels, length);
            Array.Copy(painted, tempPainted, length);

            try
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int idx = y * size + x;
                        if (!tempPainted[idx])
                        {
                            int neighbors = 0;
                            int sumR = 0, sumG = 0, sumB = 0;
                            int px = x - 1, nx = x + 1, py = y - 1, ny = y + 1;

                            // 8近傍チェック
                            if (px >= 0 && tempPainted[idx - 1]) { var c = tempPixels[idx - 1]; sumR += c.r; sumG += c.g; sumB += c.b; neighbors++; }
                            if (nx < size && tempPainted[idx + 1]) { var c = tempPixels[idx + 1]; sumR += c.r; sumG += c.g; sumB += c.b; neighbors++; }
                            if (py >= 0)
                            {
                                int idy = (y - 1) * size + x;
                                if (tempPainted[idy]) { var c = tempPixels[idy]; sumR += c.r; sumG += c.g; sumB += c.b; neighbors++; }
                                if (px >= 0 && tempPainted[idy - 1]) { var c2 = tempPixels[idy - 1]; sumR += c2.r; sumG += c2.g; sumB += c2.b; neighbors++; }
                                if (nx < size && tempPainted[idy + 1]) { var c3 = tempPixels[idy + 1]; sumR += c3.r; sumG += c3.g; sumB += c3.b; neighbors++; }
                            }
                            if (ny < size)
                            {
                                int idy = (y + 1) * size + x;
                                if (tempPainted[idy]) { var c = tempPixels[idy]; sumR += c.r; sumG += c.g; sumB += c.b; neighbors++; }
                                if (px >= 0 && tempPainted[idy - 1]) { var c2 = tempPixels[idy - 1]; sumR += c2.r; sumG += c2.g; sumB += c2.b; neighbors++; }
                                if (nx < size && tempPainted[idy + 1]) { var c3 = tempPixels[idy + 1]; sumR += c3.r; sumG += c3.g; sumB += c3.b; neighbors++; }
                            }

                            if (neighbors > 0)
                            {
                                byte r = (byte)(sumR / neighbors);
                                byte g = (byte)(sumG / neighbors);
                                byte bz = (byte)(sumB / neighbors);
                                pixels[idx] = new Color32(r, g, bz, 255);
                                painted[idx] = true;
                            }
                        }
                    }
                }
            }
            finally
            {
                ArrayPool<Color32>.Shared.Return(tempPixels);
                ArrayPool<bool>.Shared.Return(tempPainted);
            }
        }
    }
}
