using GroomingTool2.Core;
using UnityEngine;

namespace GroomingTool2.Rendering
{
    /// <summary>
    /// 描画用テクスチャの管理を担当するクラス
    /// </summary>
    internal sealed class TextureManager
    {
        private Texture2D previewTexture;
        private Color32[] pixelBuffer;
        private Texture2D wireframeTexture;
        private Texture2D dotsTexture;
        private Color32[] dotsBuffer;
        private Texture2D darkeningTexture;
        private Color32[] darkeningBuffer;

        public Texture2D PreviewTexture => previewTexture;
        public Color32[] PixelBuffer => pixelBuffer;
        public Texture2D WireframeTexture => wireframeTexture;
        public Texture2D DotsTexture => dotsTexture;
        public Color32[] DotsBuffer => dotsBuffer;
        public Texture2D DarkeningTexture => darkeningTexture;

        public void EnsurePreviewTexture()
        {
            if (previewTexture != null)
                return;

            previewTexture = new Texture2D(Common.TexSize, Common.TexSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            // 線形カラー空間で作成（必要に応じて）

            pixelBuffer = new Color32[Common.TexSize * Common.TexSize];
        }

        public void EnsureWireframeTexture(int width, int height)
        {
            if (wireframeTexture != null && (wireframeTexture.width != width || wireframeTexture.height != height))
            {
                Object.DestroyImmediate(wireframeTexture);
                wireframeTexture = null;
            }

            if (wireframeTexture == null)
            {
                wireframeTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    // Bilinearフィルタリングを使用してGPUスケーリング時の見栄えを向上
                    filterMode = FilterMode.Bilinear
                };
            }
        }

        public void EnsureDotsTexture(int width, int height)
        {
            int w = Mathf.Max(1, width);
            int h = Mathf.Max(1, height);
            bool needCreate = dotsTexture == null || dotsTexture.width != w || dotsTexture.height != h || dotsBuffer == null || dotsBuffer.Length != w * h;
            if (needCreate)
            {
                if (dotsTexture != null)
                {
                    Object.DestroyImmediate(dotsTexture);
                    dotsTexture = null;
                }
                dotsTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point
                };
                dotsBuffer = new Color32[w * h];
            }
        }

        public void EnsureDarkeningTexture()
        {
            if (darkeningTexture != null)
                return;

            darkeningTexture = new Texture2D(Common.TexSize, Common.TexSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            darkeningBuffer = new Color32[Common.TexSize * Common.TexSize];
        }

        /// <summary>
        /// 暗化テクスチャを更新する
        /// </summary>
        /// <param name="effectiveSelected">選択状態の配列</param>
        /// <param name="hasAnySelection">選択がある場合true（MaskStateから取得して渡すことで全ピクセル走査を省略）</param>
        public void UpdateDarkeningTexture(bool[,] effectiveSelected, bool hasAnySelection)
        {
            EnsureDarkeningTexture();
            
            if (effectiveSelected == null || darkeningBuffer == null)
                return;

            // マスクが空の場合は暗化テクスチャを完全に透明にする
            if (!hasAnySelection)
            {
                System.Array.Clear(darkeningBuffer, 0, darkeningBuffer.Length);
                darkeningTexture.SetPixels32(darkeningBuffer);
                darkeningTexture.Apply(false, false);
                return;
            }

            var transparentColor = new Color32(0, 0, 0, 0); // 透明
            var darkenColor = new Color32(0, 0, 0, 128); // 半透明黒 (alpha = 128 = 0.5)
            var buffer = darkeningBuffer;
            var texSize = Common.TexSize;

            // マスク座標系（y=0が上）からテクスチャ座標系（y=0が下）への変換
            // Parallel.Forで並列処理（各行を並列に処理）
            System.Threading.Tasks.Parallel.For(0, texSize, maskY =>
            {
                int textureY = texSize - 1 - maskY; // Y軸を反転
                int rowOffset = textureY * texSize;
                for (int x = 0; x < texSize; x++)
                {
                    // 非選択領域を暗く表示
                    buffer[rowOffset + x] = effectiveSelected[x, maskY] ? transparentColor : darkenColor;
                }
            });

            darkeningTexture.SetPixels32(darkeningBuffer);
            darkeningTexture.Apply(false, false);
        }

        public void InvalidateDarkeningTexture()
        {
            if (darkeningTexture != null)
            {
                Object.DestroyImmediate(darkeningTexture);
                darkeningTexture = null;
            }
            darkeningBuffer = null;
        }

        public void ClearDotsBuffer()
        {
            if (dotsBuffer != null)
            {
                System.Array.Clear(dotsBuffer, 0, dotsBuffer.Length);
            }
        }

        public void ApplyWireframeTexture(Color32[] buffer)
        {
            if (wireframeTexture != null && buffer != null)
            {
                wireframeTexture.SetPixels32(buffer);
                wireframeTexture.Apply();
            }
        }

        public void ApplyDotsTexture()
        {
            if (dotsTexture != null && dotsBuffer != null)
            {
                dotsTexture.SetPixels32(dotsBuffer);
                dotsTexture.Apply(false, false);
            }
        }

        public void Dispose()
        {
            if (previewTexture != null)
            {
                Object.DestroyImmediate(previewTexture);
                previewTexture = null;
            }
            if (wireframeTexture != null)
            {
                Object.DestroyImmediate(wireframeTexture);
                wireframeTexture = null;
            }
            if (dotsTexture != null)
            {
                Object.DestroyImmediate(dotsTexture);
                dotsTexture = null;
            }
            if (darkeningTexture != null)
            {
                Object.DestroyImmediate(darkeningTexture);
                darkeningTexture = null;
            }
            pixelBuffer = null;
            dotsBuffer = null;
            darkeningBuffer = null;
        }
    }
}

