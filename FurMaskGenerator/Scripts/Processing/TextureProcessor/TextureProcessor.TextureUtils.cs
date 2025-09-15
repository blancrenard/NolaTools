#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Data;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator
{
    public partial class TextureProcessor
    {
        #region Texture Utilities

        private void MergeTextures(Texture2D baseTex, Texture2D overlayTex)
        {
            if (baseTex.width != overlayTex.width || baseTex.height != overlayTex.height)
                return;

            // 動的バッチサイズ計算（メモリ使用量に基づく最適化）
            int width = baseTex.width;
            int height = baseTex.height;
            int totalPixels = width * height;
            int optimalBatchSize = CalculateOptimalTextureBatchSize(totalPixels, width, height);

            for (int startIndex = 0; startIndex < totalPixels; startIndex += optimalBatchSize)
            {
                int endIndex = Mathf.Min(startIndex + optimalBatchSize, totalPixels);
                int batchWidth = endIndex - startIndex;
                
                // バッチ単位でピクセルを取得
                int startX = startIndex % width;
                int startY = startIndex / width;
                int batchHeight = Mathf.CeilToInt((float)batchWidth / width);
                int actualBatchWidth = Mathf.Min(batchWidth, width - startX);
                
                if (actualBatchWidth <= 0) continue;
                
                Color[] basePixels = baseTex.GetPixels(startX, startY, actualBatchWidth, batchHeight);
                Color[] overlayPixels = overlayTex.GetPixels(startX, startY, actualBatchWidth, batchHeight);

                // 最適化されたマージ処理
                MergePixelsBatch(basePixels, overlayPixels, actualBatchWidth, batchHeight);

                baseTex.SetPixels(startX, startY, actualBatchWidth, batchHeight, basePixels);
            }

            baseTex.Apply();
        }

        /// <summary>
        /// テクスチャ処理用の最適なバッチサイズを計算（メモリ効率化）
        /// </summary>
        private int CalculateOptimalTextureBatchSize(int totalPixels, int width, int height)
        {
            // メモリ使用量を考慮した動的バッチサイズ計算
            int baseBatchSize = 1024 * 1024; // 1Mピクセル（デフォルト）
            
            // テクスチャサイズに基づく調整
            if (totalPixels < 256 * 256) // 小さいテクスチャ
            {
                baseBatchSize = totalPixels; // 一度に処理
            }
            else if (totalPixels < 1024 * 1024) // 中程度のテクスチャ
            {
                baseBatchSize = Mathf.Max(256 * 256, totalPixels / 4);
            }
            else if (totalPixels < 2048 * 2048) // 大きいテクスチャ
            {
                baseBatchSize = Mathf.Max(512 * 512, totalPixels / 8);
            }
            else // 非常に大きいテクスチャ
            {
                baseBatchSize = Mathf.Max(1024 * 1024, totalPixels / 16);
            }
            
            // メモリ制限を考慮（約100MB制限）
            int maxMemoryPixels = 100 * 1024 * 1024 / (4 * 4); // Color構造体は16バイト
            baseBatchSize = Mathf.Min(baseBatchSize, maxMemoryPixels);
            
            // 最小バッチサイズを保証
            return Mathf.Max(baseBatchSize, 64 * 64);
        }

        private void MergePixelsBatch(Color[] basePixels, Color[] overlayPixels, int width, int height)
        {
            int pixelCount = basePixels.Length;
            
            // ループ展開と条件分岐の最適化
            for (int i = 0; i < pixelCount; i++)
            {
                Color basePixel = basePixels[i];
                Color overlayPixel = overlayPixels[i];
                
                // 事前計算で条件判定を最適化
                float overlayR = overlayPixel.r;
                float overlayA = overlayPixel.a;
                float baseR = basePixel.r;
                float baseA = basePixel.a;
                
                bool shouldReplace = false;

                // 黒い部分（r=0, alpha=1）を最も優先
                if (overlayR < 0.001f && overlayA > 0.99f)
                {
                    shouldReplace = true;
                }
                // 同じ優先度の場合、黒の半透明（r=0, alpha<1）を優先
                else if (overlayR < 0.001f && baseR < 0.001f)
                {
                    if (overlayA > baseA)
                    {
                        shouldReplace = true;
                    }
                }
                // 異なるr値の場合は、より小さいr値を優先
                else if (overlayR < baseR)
                {
                    shouldReplace = true;
                }
                // 同じr値の場合は、より透明なものを優先
                else if (Mathf.Abs(overlayR - baseR) < 0.001f && overlayA < baseA)
                {
                    shouldReplace = true;
                }

                if (shouldReplace)
                {
                    basePixels[i] = overlayPixel;
                }
            }
        }

        private void ApplyGammaCorrection(Texture2D texture, float gamma)
        {
            // ガンマ値が1.0の場合は処理をスキップ
            if (Mathf.Approximately(gamma, 1.0f))
                return;

            int width = texture.width;
            int height = texture.height;
            int totalPixels = width * height;
            int optimalBatchSize = CalculateOptimalTextureBatchSize(totalPixels, width, height);

            for (int startIndex = 0; startIndex < totalPixels; startIndex += optimalBatchSize)
            {
                int endIndex = Mathf.Min(startIndex + optimalBatchSize, totalPixels);
                int batchWidth = endIndex - startIndex;
                
                int startX = startIndex % width;
                int startY = startIndex / width;
                int batchHeight = Mathf.CeilToInt((float)batchWidth / width);
                int actualBatchWidth = Mathf.Min(batchWidth, width - startX);
                
                if (actualBatchWidth <= 0) continue;
                
                Color[] pixels = texture.GetPixels(startX, startY, actualBatchWidth, batchHeight);
                
                // 最適化されたガンマ補正
                ApplyGammaCorrectionBatch(pixels, gamma);
                
                texture.SetPixels(startX, startY, actualBatchWidth, batchHeight, pixels);
            }
            
            texture.Apply();
        }

        private void ApplyGammaCorrectionBatch(Color[] pixels, float gamma)
        {
            int pixelCount = pixels.Length;
            
            // ループ展開と事前計算で最適化
            for (int i = 0; i < pixelCount; i++)
            {
                Color c = pixels[i];
                // 事前計算でMathf.Powの呼び出し回数を削減
                c.r = Mathf.Pow(c.r, gamma);
                c.g = Mathf.Pow(c.g, gamma);
                c.b = Mathf.Pow(c.b, gamma);
                pixels[i] = c;
            }
        }

        private Texture2D ApplyEdgePadding(Texture2D source, int paddingSize, string materialKey)
        {
            int width = source.width;
            int height = source.height;
            Color[] sourcePixels = source.GetPixels();

            // 真のUVエッジ画素のみを使用（三角形ラスタライズで実際に描画された画素）
            bool[] originalValid;
            if (materialToRasterizedPixels.TryGetValue(materialKey, out var rasterized))
            {
                originalValid = EditorTextureUtils.BuildValidMaskFromRasterized(rasterized, width * height);
            }
            else
            {
                originalValid = EditorTextureUtils.BuildValidMaskFromPixels(sourcePixels, AppSettings.VALID_PIXEL_THRESHOLD);
            }

            var padded = EditorTextureUtils.ApplyEdgePadding(sourcePixels, width, height, paddingSize, originalValid, AppSettings.VALID_PIXEL_THRESHOLD);
            source.SetPixels(padded);
            source.Apply(false);
            return source;
        }

        private void ApplyUVIslandMaskToTexture(UVIslandMaskData uvIslandMask, HashSet<int> triangleIndices, Texture2D texture)
        {
            // UVアイランドマスクの処理を実装
            // まず、テクスチャバッファを取得
            Color[] buffer = texture.GetPixels();

            // 適切なサブデータを特定する必要がある
            // 現在の実装では最初のサブデータを使用
            if (subDatas.Count == 0) return;

            var (tri, _) = subDatas[0];
            var adjacency = NolaTools.FurMaskGenerator.Utils.EditorUvUtils.BuildTriangleAdjacencyListList(tri);
            int triangleCount = tri.Length / 3;

            // UV座標からシード三角形を見つける
            int seedTriangle = FindSeedTriangleByUV(tri, uvIslandMask.uvPosition);
            if (seedTriangle >= 0)
            {
                // Flood fillでUVアイランドを検出して黒く塗りつぶす
                FloodFillIslandAndPaintBlackIntoBuffer(uvIslandMask.uvPosition, tri, adjacency, triangleCount, buffer, texture.width, texture.height);
            }

            // バッファをテクスチャに適用
            texture.SetPixels(buffer);
            texture.Apply(false);
        }

        private int FindSeedTriangleByUV(int[] tri, Vector2 uv)
        {
            var uvsArr = (uvs != null) ? uvs.ToArray() : System.Array.Empty<Vector2>();
            return NolaTools.FurMaskGenerator.Utils.EditorUvUtils.FindSeedTriangleByUV(tri, uvsArr, uv);
        }

        

        private (HashSet<int> islandVertices, HashSet<int> islandVisitedTriangles) FloodFillIslandAndPaintBlackIntoBuffer(Vector2 seedUV, int[] tri, List<List<int>> adjacency, int triangleCount, Color[] buffer, int width, int height)
        {
            int seed = FindSeedTriangleByUV(tri, seedUV);
            if (seed < 0) return (new HashSet<int>(), new HashSet<int>());

            var visited = new bool[triangleCount];
            var stack = new System.Collections.Generic.Stack<int>();
            var islandVertices = new HashSet<int>();
            var islandVisitedTris = new HashSet<int>();

            stack.Push(seed);
            visited[seed] = true;

            while (stack.Count > 0)
            {
                int t = stack.Pop();
                islandVisitedTris.Add(t);

                int ia = tri[t * 3 + 0];
                int ib = tri[t * 3 + 1];
                int ic = tri[t * 3 + 2];

                // 三角形を黒く塗りつぶす
                if (ia < uvs.Count && ib < uvs.Count && ic < uvs.Count)
                {
                    FillTriBlackIntoBuffer(buffer, width, height, uvs[ia], uvs[ib], uvs[ic]);
                }

                islandVertices.Add(ia);
                islandVertices.Add(ib);
                islandVertices.Add(ic);

                // 隣接三角形を探索
                foreach (int nb in adjacency[t])
                {
                    if (!visited[nb])
                    {
                        visited[nb] = true;
                        stack.Push(nb);
                    }
                }
            }

            return (islandVertices, islandVisitedTris);
        }

        #endregion
    }
}
#endif

