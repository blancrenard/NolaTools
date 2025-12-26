using System.Collections.Generic;
using GroomingTool2.Core;
using GroomingTool2.Managers;
using UnityEngine;

namespace GroomingTool2.Utils
{
    /// <summary>
    /// UV領域マスクの構築と操作を担当するユーティリティクラス
    /// </summary>
    internal static class UvRegionMaskUtils
    {
        /// <summary>
        /// マテリアルエントリからUV領域マスクを構築（パディング適用）
        /// </summary>
        /// <param name="materialEntry">対象のマテリアルエントリ</param>
        /// <param name="padding">パディングのピクセル数</param>
        /// <returns>構築されたUV領域マスク</returns>
        public static bool[,] BuildUvRegionMask(MaterialEntry materialEntry, int padding)
        {
            var uvRegionMask = new bool[Common.TexSize, Common.TexSize];

            if (materialEntry.usages == null || materialEntry.usages.Count == 0)
                return uvRegionMask;

            // 各レンダラー/サブメッシュのUV三角形をラスタライズ
            foreach (var (renderer, submeshIndex) in materialEntry.usages)
            {
                if (renderer == null)
                    continue;

                Mesh mesh = GetMeshFromRenderer(renderer);
                if (mesh == null)
                    continue;

                Vector2[] uvs = mesh.uv;
                int[] triangles = GetTrianglesSafe(mesh, submeshIndex);

                if (uvs == null || triangles == null || triangles.Length < 3)
                    continue;

                RasterizeTriangles(uvRegionMask, uvs, triangles);
            }

            // パディングを適用（膨張処理）
            if (padding > 0)
            {
                ApplyPadding(uvRegionMask, padding);
            }

            return uvRegionMask;
        }

        /// <summary>
        /// レンダラーからメッシュを取得
        /// </summary>
        private static Mesh GetMeshFromRenderer(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer smr)
            {
                return smr.sharedMesh;
            }
            else if (renderer.TryGetComponent<MeshFilter>(out var mf))
            {
                return mf.sharedMesh;
            }
            return null;
        }

        /// <summary>
        /// 安全にサブメッシュの三角形を取得
        /// </summary>
        private static int[] GetTrianglesSafe(Mesh mesh, int submeshIndex)
        {
            try
            {
                return mesh.GetTriangles(submeshIndex);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 三角形をUVマスクにラスタライズ
        /// </summary>
        private static void RasterizeTriangles(bool[,] uvRegionMask, Vector2[] uvs, int[] triangles)
        {
            for (int i = 0; i < triangles.Length; i += 3)
            {
                if (i + 2 >= triangles.Length)
                    break;

                int idx0 = triangles[i];
                int idx1 = triangles[i + 1];
                int idx2 = triangles[i + 2];

                if (idx0 >= uvs.Length || idx1 >= uvs.Length || idx2 >= uvs.Length)
                    continue;

                RasterizeTriangleToUvMask(uvRegionMask, uvs[idx0], uvs[idx1], uvs[idx2]);
            }
        }

        /// <summary>
        /// 単一の三角形をUVマスクにラスタライズ
        /// </summary>
        private static void RasterizeTriangleToUvMask(bool[,] uvRegionMask, Vector2 uv0, Vector2 uv1, Vector2 uv2)
        {
            TriangleRasterizer.RasterizeToMask(uv0, uv1, uv2, uvRegionMask, -0.01f);
        }

        /// <summary>
        /// UVマスクにパディングを適用（セパラブルフィルタ版 - O(n²×p)に最適化）
        /// </summary>
        public static void ApplyPadding(bool[,] uvRegionMask, int padding)
        {
            if (padding <= 0)
                return;

            // 一時バッファ（水平パス結果）
            bool[,] horizontalPass = new bool[Common.TexSize, Common.TexSize];

            // 水平パス：各行で左右にpadding分膨張
            for (int y = 0; y < Common.TexSize; y++)
            {
                // スライディングウィンドウで効率的に処理
                int count = 0; // ウィンドウ内のtrue数
                
                // 初期ウィンドウ設定（0からpadding-1まで）
                for (int x = 0; x < padding && x < Common.TexSize; x++)
                {
                    if (uvRegionMask[x, y]) count++;
                }

                for (int x = 0; x < Common.TexSize; x++)
                {
                    // ウィンドウに右端を追加
                    int rightEdge = x + padding;
                    if (rightEdge < Common.TexSize && uvRegionMask[rightEdge, y])
                        count++;

                    // 現在位置に結果を記録
                    horizontalPass[x, y] = count > 0;

                    // ウィンドウから左端を削除
                    int leftEdge = x - padding;
                    if (leftEdge >= 0 && uvRegionMask[leftEdge, y])
                        count--;
                }
            }

            // 垂直パス：各列で上下にpadding分膨張
            for (int x = 0; x < Common.TexSize; x++)
            {
                int count = 0;
                
                // 初期ウィンドウ設定
                for (int y = 0; y < padding && y < Common.TexSize; y++)
                {
                    if (horizontalPass[x, y]) count++;
                }

                for (int y = 0; y < Common.TexSize; y++)
                {
                    // ウィンドウに下端を追加
                    int bottomEdge = y + padding;
                    if (bottomEdge < Common.TexSize && horizontalPass[x, bottomEdge])
                        count++;

                    // 現在位置に結果を記録
                    uvRegionMask[x, y] = count > 0;

                    // ウィンドウから上端を削除
                    int topEdge = y - padding;
                    if (topEdge >= 0 && horizontalPass[x, topEdge])
                        count--;
                }
            }
        }

        /// <summary>
        /// 2つのマスクをAND演算で結合
        /// </summary>
        /// <param name="mask1">マスク1</param>
        /// <param name="mask2">マスク2</param>
        /// <param name="outputBuffer">出力バッファ（nullの場合は新規作成）</param>
        public static bool[,] CombineMasks(bool[,] mask1, bool[,] mask2, RectInt? bounds = null, bool[,] outputBuffer = null)
        {
            if (mask1 == null) return mask2;
            if (mask2 == null) return mask1;

            bool[,] result = outputBuffer ?? new bool[Common.TexSize, Common.TexSize];

            int startX = 0;
            int startY = 0;
            int endX = Common.TexSize;
            int endY = Common.TexSize;

            if (bounds.HasValue)
            {
                var rect = bounds.Value;
                startX = Mathf.Clamp(rect.xMin, 0, Common.TexSize);
                startY = Mathf.Clamp(rect.yMin, 0, Common.TexSize);
                endX = Mathf.Clamp(rect.xMax, 0, Common.TexSize);
                endY = Mathf.Clamp(rect.yMax, 0, Common.TexSize);
            }

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    result[x, y] = mask1[x, y] && mask2[x, y];
                }
            }
            return result;
        }

        /// <summary>
        /// 2つのマスクをOR演算でマージ
        /// </summary>
        public static void MergeMasks(bool[,] target, bool[,] source, RectInt? bounds = null)
        {
            if (target == null || source == null)
                return;

            int startX = 0;
            int startY = 0;
            int endX = Common.TexSize;
            int endY = Common.TexSize;

            if (bounds.HasValue)
            {
                var rect = bounds.Value;
                startX = Mathf.Clamp(rect.xMin, 0, Common.TexSize);
                startY = Mathf.Clamp(rect.yMin, 0, Common.TexSize);
                endX = Mathf.Clamp(rect.xMax, 0, Common.TexSize);
                endY = Mathf.Clamp(rect.yMax, 0, Common.TexSize);
            }

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    if (source[x, y])
                        target[x, y] = true;
                }
            }
        }
    }
}

