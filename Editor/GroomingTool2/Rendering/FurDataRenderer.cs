using System.IO;
using System.Runtime.CompilerServices;
using GroomingTool2.Core;
using GroomingTool2.Managers;
using GroomingTool2.Utils;
using UnityEditor;
using UnityEngine;

namespace GroomingTool2.Rendering
{
    /// <summary>
    /// 毛データ（ドットとライン）の描画を担当するクラス（CPUレンダラー）
    /// </summary>
    internal sealed class FurDataRenderer
    {
        private readonly TextureManager textureManager;
        private readonly FurDataManager furDataManager;
        private Rect? dotsDrawRect;

        public FurDataRenderer(TextureManager textureManager, FurDataManager furDataManager)
        {
            this.textureManager = textureManager ?? throw new System.ArgumentNullException(nameof(textureManager));
            this.furDataManager = furDataManager ?? throw new System.ArgumentNullException(nameof(furDataManager));
        }

        public Rect? Draw(Rect viewRect, float scale, int interval, Vector2 scrollOffsetData, bool[,] effectiveMask = null, bool hasMaskSelection = false)
        {
            if (!FurRenderParams.HasVisibleArea(viewRect, scale, scrollOffsetData))
                return null;

            float screenInterval = FurRenderParams.CalculateScreenInterval(viewRect, interval);

            int bufW = Mathf.Max(1, Mathf.CeilToInt(viewRect.width));
            int bufH = Mathf.Max(1, Mathf.CeilToInt(viewRect.height));

            DrawDotsAndLines(bufW, bufH, scale, screenInterval, scrollOffsetData, effectiveMask, hasMaskSelection);
            return CalculateDrawRect(bufW, bufH);
        }

        // ───────── ドットとライン描画 ─────────

        private void DrawDotsAndLines(int bufW, int bufH, float scale, float screenInterval,
            Vector2 scrollOffsetData, bool[,] effectiveMask, bool hasMaskSelection)
        {
            textureManager.EnsureDotsTexture(bufW, bufH);
            textureManager.ClearDotsBuffer();

            var furData = furDataManager.Data;
            var dotsBuffer = textureManager.DotsBuffer;
            var grid = FurRenderParams.DotGridRange.Calculate(screenInterval, scale, scrollOffsetData, bufW, bufH);
            float maxLinePx = Mathf.Max(1f, screenInterval * FurRenderParams.MaxLineLengthRatio);

            for (float dataY = grid.StartY; dataY < grid.EndY; dataY += grid.Step)
            {
                for (float dataX = grid.StartX; dataX < grid.EndX; dataX += grid.Step)
                {
                    int x = Mathf.RoundToInt(dataX);
                    int y = Mathf.RoundToInt(dataY);
                    if ((uint)x >= (uint)Common.TexSize || (uint)y >= (uint)Common.TexSize)
                        continue;

                    int index = Common.GetIndex(x, y);
                    var data = furData[index];
                    float cos = AngleLut.GetCos(data.Dir);
                    float sin = AngleLut.GetSin(data.Dir);

                    // 傾きが小さすぎるドットはスキップ
                    float powerDot = data.Inclined * Common.Grid;
                    if (powerDot * powerDot * (cos * cos + sin * sin) < 0.25f) // dotLength < 0.5
                        continue;

                    // 浮動小数点データ座標から画面ピクセル座標へ変換（正確な間隔を保つ）
                    int cx = Mathf.RoundToInt((dataX - scrollOffsetData.x) * scale);
                    int cy = Mathf.RoundToInt((dataY - scrollOffsetData.y) * scale);

                    // マスク外かどうかを判定
                    bool isMasked = hasMaskSelection && effectiveMask != null && !effectiveMask[x, y];

                    var dotColor = (Color32)NormalMapColorUtils.GetNormalMapColor(data);
                    if (isMasked)
                    {
                        dotColor.r = (byte)(dotColor.r * FurRenderParams.MaskedDarkenFactor);
                        dotColor.g = (byte)(dotColor.g * FurRenderParams.MaskedDarkenFactor);
                        dotColor.b = (byte)(dotColor.b * FurRenderParams.MaskedDarkenFactor);
                    }

                    StampDot(dotsBuffer, bufW, bufH, cx, cy, FurRenderParams.FixedDotRadius, dotColor);
                    DrawLineIfNeeded(data, cos, sin, maxLinePx, bufW, bufH, cx, cy, dotsBuffer, isMasked);
                }
            }

            textureManager.ApplyDotsTexture();
        }

        private static void DrawLineIfNeeded(FurData data, float cos, float sin, float maxLinePx,
            int bufW, int bufH, int cx, int cy, Color32[] dotsBuffer, bool isMasked)
        {
            // 傾きに応じて倍率を変える（傾きが大きいほど長いライン）
            // Inclined が 0.0 の時は倍率 0.5、0.95 の時は倍率 2.0 になるように線形補間
            float lineMultiplier = 0.5f + data.Inclined * 1.58f;
            float powerLinePx = data.Inclined * Common.Grid * lineMultiplier;
            float dxLinePx = powerLinePx * cos;
            float dyLinePx = powerLinePx * sin;
            float lenPx = Mathf.Sqrt(dxLinePx * dxLinePx + dyLinePx * dyLinePx);
            if (lenPx < 0.5f)
                return;

            // 画面ピクセル空間での上限（可視サンプル間隔に応じて）
            if (lenPx > maxLinePx)
            {
                float s = maxLinePx / lenPx;
                dxLinePx *= s;
                dyLinePx *= s;
            }

            int x1 = Mathf.RoundToInt(cx + dxLinePx);
            int y1 = Mathf.RoundToInt(cy + dyLinePx);

            byte lineColorValue = isMasked ? (byte)(255 * FurRenderParams.MaskedDarkenFactor) : (byte)255;
            var lineColor = new Color32(lineColorValue, lineColorValue, lineColorValue, 255);
            LineDrawingUtils.DrawLineOnBufferFlippedY(cx, cy, x1, y1, lineColor, dotsBuffer, bufW, bufH);
        }

        // ───────── ドットスタンプ ─────────

        private static void StampDot(Color32[] buf, int bufWidth, int bufHeight, int cxSS, int cySS, int radiusPx, Color32 color)
        {
            float radius = radiusPx;
            float r2 = radius * radius;

            int searchRadius = radiusPx + 1;

            for (int dy = -searchRadius; dy <= searchRadius; dy++)
            {
                int py = cySS + dy;
                if ((uint)py >= (uint)bufHeight) continue;

                for (int dx = -searchRadius; dx <= searchRadius; dx++)
                {
                    int px = cxSS + dx;
                    if ((uint)px >= (uint)bufWidth) continue;

                    float dist2 = dx * dx + dy * dy;

                    if (dist2 <= r2)
                    {
                        int iy = (bufHeight - 1) - py;
                        buf[iy * bufWidth + px] = color;
                    }
                    else
                    {
                        float dist = Mathf.Sqrt(dist2);
                        float diff = dist - radius;

                        if (diff < 1.0f)
                        {
                            float alpha = Mathf.Clamp01(1.0f - diff);
                            int iy = (bufHeight - 1) - py;
                            int idx = iy * bufWidth + px;

                            Color32 existing = buf[idx];
                            byte blendedR = (byte)(color.r * alpha + existing.r * (1 - alpha));
                            byte blendedG = (byte)(color.g * alpha + existing.g * (1 - alpha));
                            byte blendedB = (byte)(color.b * alpha + existing.b * (1 - alpha));
                            byte blendedA = (byte)Mathf.Max(color.a * alpha, existing.a);

                            buf[idx] = new Color32(blendedR, blendedG, blendedB, blendedA);
                        }
                    }
                }
            }
        }

        // ───────── 描画矩形 ─────────

        private Rect? CalculateDrawRect(int bufW, int bufH)
        {
            if (textureManager.DotsTexture == null)
                return null;

            dotsDrawRect = new Rect(0f, 0f, bufW, bufH);
            return dotsDrawRect;
        }

        // ───────── ノーマルマップ書き出し ─────────

        /// <summary>
        /// ノーマルマップを保存する
        /// </summary>
        public void SaveNormalMap(string path)
        {
            textureManager.EnsurePreviewTexture();
            var furData = furDataManager.Data;

            for (var y = 0; y < Common.TexSize; y++)
            {
                for (var x = 0; x < Common.TexSize; x++)
                {
                    int index = Common.GetIndex(x, y);
                    var data = furData[index];
                    var cos = AngleLut.GetCos(data.Dir);
                    var sin = AngleLut.GetSin(data.Dir);
                    var inclined = Mathf.Min(data.Inclined, 0.95f);
                    var normal = new Vector3(cos * inclined, sin * inclined, Mathf.Sqrt(1f - Mathf.Clamp01(inclined * inclined)));
                    // 画像空間のY向き差異に合わせてG(Y)成分の符号を反転して保存
                    normal.y = -normal.y;
                    normal = normal.normalized * 0.5f + Vector3.one * 0.5f;

                    var color = new Color(normal.x, normal.y, normal.z, 1f);
                    int iy = Common.TexSize - 1 - y;
                    textureManager.PixelBuffer[iy * Common.TexSize + x] = color;
                }
            }

            textureManager.PreviewTexture.SetPixels32(textureManager.PixelBuffer);
            textureManager.PreviewTexture.Apply();

            var bytes = textureManager.PreviewTexture.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            AssetDatabase.Refresh();
        }
    }
}
