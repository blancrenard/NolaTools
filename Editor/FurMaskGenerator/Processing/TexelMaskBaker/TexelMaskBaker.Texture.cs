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
    public partial class TexelMaskBaker
    {
        #region テクスチャ生成

        /// <summary>
        /// ベイク結果のバッファからテクスチャを生成し、エッジパディングを適用
        /// </summary>
        private Dictionary<string, MaskResult> BuildFinalTextures()
        {
            var finalPreview = new Dictionary<string, MaskResult>();

            foreach (var kv in materialBuffers)
            {
                string matKey = kv.Key;
                Color[] buffer = kv.Value;

                if (EditorCoreUtils.ShowCancelableProgressAutoClear(
                    UILabels.PROGRESS_BAR_TITLE_TEXEL, UILabels.RASTERIZING_LABEL, 0.85f))
                    return null;

                // ぼかし適用（エッジパディングの前に実行）
                int blurRadius = settings.BlurRadius;
                if (blurRadius > 0)
                {
                    bool[] validMask = null;
                    if (materialRasterizedPixels.TryGetValue(matKey, out var rasterized))
                    {
                        validMask = EditorTextureUtils.BuildValidMaskFromRasterized(rasterized, texSize * texSize);
                    }
                    buffer = ApplyGaussianBlur(buffer, texSize, texSize, blurRadius, validMask);
                }

                // テクスチャ作成
                Texture2D tex = TextureOperationUtils.CreateAndApplyTexture(texSize, texSize, buffer, false);

                // エッジパディング適用
                int paddingSize = settings.EdgePaddingSize;
                if (paddingSize > 0)
                {
                    bool[] validMask;
                    if (materialRasterizedPixels.TryGetValue(matKey, out var rasterized))
                    {
                        validMask = EditorTextureUtils.BuildValidMaskFromRasterized(rasterized, texSize * texSize);
                    }
                    else
                    {
                        Color[] pixels = tex.GetPixels();
                        validMask = EditorTextureUtils.BuildValidMaskFromPixels(pixels, AppSettings.VALID_PIXEL_THRESHOLD);
                    }

                    Color[] sourcePixels = tex.GetPixels();
                    var padded = EditorTextureUtils.ApplyEdgePadding(sourcePixels, texSize, texSize, paddingSize, validMask, AppSettings.VALID_PIXEL_THRESHOLD);
                    tex.SetPixels(padded);
                    tex.Apply(false);
                }


                // Alpha Mask生成
                Texture2D alphaTex = GenerateAlphaMask(tex);

                finalPreview[matKey] = new MaskResult(tex, alphaTex);
            }

            EditorCoreUtils.ClearProgress();
            return finalPreview;
        }

        /// <summary>
        /// 長さマスクからアルファマスク（2値化）を生成
        /// </summary>
        private Texture2D GenerateAlphaMask(Texture2D lengthMask)
        {
            Texture2D alphaTex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            Color[] lengthPixels = lengthMask.GetPixels();
            Color[] alphaPixels = new Color[lengthPixels.Length];
            float threshold = 0.4f; // #666666

            bool useAlpha = settings.UseTransparentMode;

            for (int i = 0; i < lengthPixels.Length; i++)
            {
                float val;
                if (useAlpha)
                {
                    val = 1f - lengthPixels[i].a;
                }
                else
                {
                    val = lengthPixels[i].maxColorComponent;
                }

                // LengthMaskの明るさが閾値を超えているか判定
                if (val > threshold)
                {
                    if (useAlpha)
                    {
                        alphaPixels[i] = new Color(0f, 0f, 0f, 0f);
                    }
                    else
                    {
                        alphaPixels[i] = Color.white;
                    }
                }
                else
                {
                    alphaPixels[i] = Color.black;
                }
            }
            alphaTex.SetPixels(alphaPixels);
            alphaTex.Apply(false);
            return alphaTex;
        }

        /// <summary>
        /// 1Dガウシアンカーネルを動的に生成
        /// </summary>
        private static float[] BuildGaussianKernel(int radius)
        {
            int size = radius * 2 + 1;
            float[] kernel = new float[size];
            float sigma = Mathf.Max(radius * 0.5f, 0.5f);
            float twoSigmaSq = 2f * sigma * sigma;
            float sum = 0;

            for (int i = 0; i < size; i++)
            {
                int x = i - radius;
                kernel[i] = Mathf.Exp(-(x * x) / twoSigmaSq);
                sum += kernel[i];
            }

            // 正規化
            float invSum = 1f / sum;
            for (int i = 0; i < size; i++)
            {
                kernel[i] *= invSum;
            }

            return kernel;
        }

        /// <summary>
        /// ラスタライズ済みピクセルのみを対象とした分離ガウシアンブラー
        /// 水平パス→垂直パスの2パスで高品質かつ高効率なぼかしを実現
        /// blurRadius でカーネル半径を直接指定（ピクセル単位）
        /// </summary>
        private static Color[] ApplyGaussianBlur(Color[] source, int width, int height, int blurRadius, bool[] validMask)
        {
            float[] kernel = BuildGaussianKernel(blurRadius);

            Color[] current = (Color[])source.Clone();
            Color[] temp = new Color[current.Length];

            // === 水平パス ===
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;

                    if (validMask != null && !validMask[idx])
                    {
                        temp[idx] = current[idx];
                        continue;
                    }

                    float r = 0, g = 0, b = 0, a = 0;
                    float weightSum = 0;

                    for (int k = -blurRadius; k <= blurRadius; k++)
                    {
                        int nx = x + k;
                        if (nx < 0 || nx >= width) continue;

                        int nIdx = y * width + nx;
                        if (validMask != null && !validMask[nIdx]) continue;

                        float w = kernel[k + blurRadius];
                        r += current[nIdx].r * w;
                        g += current[nIdx].g * w;
                        b += current[nIdx].b * w;
                        a += current[nIdx].a * w;
                        weightSum += w;
                    }

                    if (weightSum > 0)
                    {
                        float invW = 1f / weightSum;
                        temp[idx] = new Color(r * invW, g * invW, b * invW, a * invW);
                    }
                    else
                    {
                        temp[idx] = current[idx];
                    }
                }
            }

            // === 垂直パス ===
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;

                    if (validMask != null && !validMask[idx])
                    {
                        current[idx] = temp[idx];
                        continue;
                    }

                    float r = 0, g = 0, b = 0, a = 0;
                    float weightSum = 0;

                    for (int k = -blurRadius; k <= blurRadius; k++)
                    {
                        int ny = y + k;
                        if (ny < 0 || ny >= height) continue;

                        int nIdx = ny * width + x;
                        if (validMask != null && !validMask[nIdx]) continue;

                        float w = kernel[k + blurRadius];
                        r += temp[nIdx].r * w;
                        g += temp[nIdx].g * w;
                        b += temp[nIdx].b * w;
                        a += temp[nIdx].a * w;
                        weightSum += w;
                    }

                    if (weightSum > 0)
                    {
                        float invW = 1f / weightSum;
                        current[idx] = new Color(r * invW, g * invW, b * invW, a * invW);
                    }
                    else
                    {
                        current[idx] = temp[idx];
                    }
                }
            }

            return current;
        }

        #endregion
    }
}
#endif
