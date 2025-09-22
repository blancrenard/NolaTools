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
        #region マテリアルテクスチャ作成

        private Dictionary<String, List<Texture2D>> CreateMaterialTextures()
        {
            var matTex = new Dictionary<string, List<Texture2D>>();
            for (int subIndex = 0; subIndex < subDatas.Count; subIndex++)
            {
                var (tri, mat) = subDatas[subIndex];
                Color[] buffer = new Color[texSize * texSize];
                Color initialColor = useTransparentMode ? new Color(1f, 1f, 1f, 0f) : Color.white;
                for (int i = 0; i < buffer.Length; i++) buffer[i] = initialColor;
                var rasterizedPixels = new HashSet<int>();

                int skippedTriangles = 0;
                for (int i = 0; i < tri.Length; i += 3)
                {
                    int a = tri[i], b = tri[i + 1], c = tri[i + 2];

                    if (ShouldSkipTriangle(uvs[a], uvs[b], uvs[c], texSize, texSize))
                    {
                        skippedTriangles++;
                        continue;
                    }

                    FillTriIntoBuffer(buffer, texSize, texSize, uvs[a], uvs[b], uvs[c], vDist[a], vDist[b], vDist[c], rasterizedPixels);

                    if ((i % (adaptiveProgressInterval * 3)) == 0)
                    {
                        float p = 0.62f + 0.06f * ((float)i / Mathf.Max(1, tri.Length));
                        if (EditorCoreUtils.ShowCancelableProgressThrottledAutoClear(UILabels.PROGRESS_BAR_TITLE, UILabels.RASTERIZING_LABEL, p)) break;
                    }
                }

                if (materialToRasterizedPixels.TryGetValue(mat, out var existingRasterized))
                {
                    existingRasterized.UnionWith(rasterizedPixels);
                }
                else
                {
                    materialToRasterizedPixels[mat] = rasterizedPixels;
                }
                Texture2D t = TextureOperationUtils.CreateAndApplyTexture(texSize, texSize, buffer, false);

                if (!matTex.ContainsKey(mat)) matTex[mat] = new List<Texture2D>();
                int texIdx = matTex[mat].Count;
                matTex[mat].Add(t);
                subIndexToTexLocator[subIndex] = (mat, texIdx);
            }
            return matTex;
        }

        private Dictionary<string, Texture2D> MergeSubTexturesPerMaterial(Dictionary<string, List<Texture2D>> matTex)
        {
            var finalPreview = new Dictionary<string, Texture2D>();
            foreach (var kv in matTex)
            {
                if (kv.Value == null || kv.Value.Count == 0) continue;
                Texture2D baseTex = kv.Value[0];

                if (kv.Value.Count > 1)
                {
                    for (int i = 1; i < kv.Value.Count; i++)
                    {
                        var overlayTex = kv.Value[i];
                        MergeTextures(baseTex, overlayTex);
                        EditorObjectUtils.SafeDestroy(overlayTex);
                    }
                }

                if (!Mathf.Approximately(gamma, 1.0f))
                {
                    ApplyGammaCorrection(baseTex, gamma);
                }

                // テクスチャサイズに応じてパディングサイズを動的に計算
                int textureSize = baseTex.width;
                int paddingSize = CalculatePaddingSize(textureSize);
                baseTex = ApplyEdgePadding(baseTex, paddingSize, kv.Key);

                finalPreview[kv.Key] = baseTex;
            }
            return finalPreview;
        }

        /// <summary>
        /// テクスチャサイズに応じてパディングサイズを計算する
        /// 基準: 1024x1024 → 4ピクセル
        /// サイズが2倍になるごとにパディングサイズも2倍になる
        /// </summary>
        private int CalculatePaddingSize(int textureSize)
        {
            const int baseTextureSize = 1024;
            const int basePaddingSize = 4;

            // テクスチャサイズの比率を計算
            float sizeRatio = (float)textureSize / baseTextureSize;

            // パディングサイズを計算（最小値は1）
            int paddingSize = Mathf.Max(1, Mathf.RoundToInt(basePaddingSize * sizeRatio));

            return paddingSize;
        }

        #endregion
    }
}
#endif

