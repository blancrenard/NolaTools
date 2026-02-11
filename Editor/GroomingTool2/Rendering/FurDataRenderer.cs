using System.IO;
using System.Runtime.CompilerServices;
using GroomingTool2.Core;
using GroomingTool2.Managers;
using GroomingTool2.Utils;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace GroomingTool2.Rendering
{
    /// <summary>
    /// 毛データ（ドットとライン）の描画を担当するクラス
    /// </summary>
    internal sealed class FurDataRenderer
    {
        private readonly TextureManager textureManager;
        private readonly FurDataManager furDataManager;
        private int dotsBufWidth;
        private int dotsBufHeight;
        private Rect? dotsDrawRect;
        
        // 暗化用の色倍率
        private const float MaskedDarkenFactor = 0.4f;

        public FurDataRenderer(TextureManager textureManager, FurDataManager furDataManager)
        {
            this.textureManager = textureManager ?? throw new System.ArgumentNullException(nameof(textureManager));
            this.furDataManager = furDataManager ?? throw new System.ArgumentNullException(nameof(furDataManager));
        }

        public Rect? Draw(Rect viewRect, float scale, int interval, Vector2 scrollOffsetData, bool[,] effectiveMask = null, bool hasMaskSelection = false)
        {
            var renderParams = CalculateRenderParams(viewRect, scale, interval, scrollOffsetData, effectiveMask, hasMaskSelection);
            if (!renderParams.HasValue)
                return null;

            var renderParamsValue = renderParams.Value;
            DrawDotsAndLines(renderParamsValue);
            return CalculateDrawRect(renderParamsValue);
        }

        private struct RenderParams
        {
            public int StartX, EndX, StartY, EndY;
            public int Step;
            public int DotsBufWidth, DotsBufHeight;
            public int DotRadiusPx;
            public int VisibleWData, VisibleHData;
            public float Scale;
            public Vector2 ScrollOffsetData;
            public bool[,] EffectiveMask;
            public bool HasMaskSelection;
        }

        private RenderParams? CalculateRenderParams(Rect viewRect, float scale, int interval, Vector2 scrollOffsetData, bool[,] effectiveMask, bool hasMaskSelection)
        {
            int startX, endX, startY, endY;
            // 可視データ範囲（グリッドオフセットには依存しない）
            CoordinateUtils.GetVisibleDataRange(viewRect, scale, scrollOffsetData, Common.TexSize, 1, out startX, out endX, out startY, out endY);

            int visibleWData = Mathf.Max(0, endX - startX);
            int visibleHData = Mathf.Max(0, endY - startY);
            if (visibleWData <= 0 || visibleHData <= 0)
                return null;

            // 画面ピクセル間隔 interval をデータ座標のステップへ変換（ズームに反比例）
            int stepData = Mathf.Max(1, Mathf.RoundToInt(interval / Mathf.Max(scale, 1e-6f)));

            // 画面ピクセル空間でのポイント数を計算（ズームに依存しない一定数のドット）
            int screenPointsX = Mathf.Max(1, Mathf.CeilToInt(viewRect.width / interval));
            int screenPointsY = Mathf.Max(1, Mathf.CeilToInt(viewRect.height / interval));
            int approxPoints = screenPointsX * screenPointsY;

            int stepMul = 1;
            if (approxPoints > FurRenderParams.TargetMaxPoints)
            {
                stepMul = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt((float)approxPoints / FurRenderParams.TargetMaxPoints)));
            }
            int step = Mathf.Max(1, stepData * stepMul);

            // 画面ピクセル基準の固定サイズバッファ（ビューポート寸法）
            int dotsW = Mathf.Max(1, Mathf.CeilToInt(viewRect.width));
            int dotsH = Mathf.Max(1, Mathf.CeilToInt(viewRect.height));
            // ドットの半径を固定値にする（スケールに依存しない、ピクセル単位）
            const int FixedRadius = 4;
            int radius = FixedRadius;

            dotsBufWidth = dotsW;
            dotsBufHeight = dotsH;

            return new RenderParams
            {
                StartX = startX,
                EndX = endX,
                StartY = startY,
                EndY = endY,
                Step = step,
                DotsBufWidth = dotsW,
                DotsBufHeight = dotsH,
                DotRadiusPx = radius,
                VisibleWData = visibleWData,
                VisibleHData = visibleHData,
                Scale = scale,
                ScrollOffsetData = scrollOffsetData,
                EffectiveMask = effectiveMask,
                HasMaskSelection = hasMaskSelection
            };
        }

        private void DrawDotsAndLines(RenderParams renderParams)
        {
            textureManager.EnsureDotsTexture(renderParams.DotsBufWidth, renderParams.DotsBufHeight);
            textureManager.ClearDotsBuffer();

            var furData = furDataManager.Data;
            var dotsBuffer = textureManager.DotsBuffer;

            for (var y = renderParams.StartY; y < renderParams.EndY; y += renderParams.Step)
            {
                for (var x = renderParams.StartX; x < renderParams.EndX; x += renderParams.Step)
                {
                    int index = Common.GetIndex(x, y);
                    var data = furData[index];
                    var cos = AngleLut.GetCos(data.Dir);
                    var sin = AngleLut.GetSin(data.Dir);

                    var powerDot = data.Inclined * Common.Grid;
                    var dxDot = powerDot * cos;
                    var dyDot = powerDot * sin;
                    // 傾きが0より大きければドットを表示（ベクトルの長さで判定）
                    float dotLength = Mathf.Sqrt(dxDot * dxDot + dyDot * dyDot);
                    if (dotLength < 0.5f)
                        continue;

                    // 画面ローカルのピクセル座標に変換
                    int cx = Mathf.RoundToInt((x - renderParams.ScrollOffsetData.x) * renderParams.Scale);
                    int cy = Mathf.RoundToInt((y - renderParams.ScrollOffsetData.y) * renderParams.Scale);

                    // マスク外かどうかを判定
                    bool isMasked = renderParams.HasMaskSelection && renderParams.EffectiveMask != null && !renderParams.EffectiveMask[x, y];

                    var dotColor = (Color32)NormalMapColorUtils.GetNormalMapColor(data);
                    
                    // マスク外の場合は暗くする
                    if (isMasked)
                    {
                        dotColor.r = (byte)(dotColor.r * MaskedDarkenFactor);
                        dotColor.g = (byte)(dotColor.g * MaskedDarkenFactor);
                        dotColor.b = (byte)(dotColor.b * MaskedDarkenFactor);
                    }
                    
                    StampDot(dotsBuffer, renderParams.DotsBufWidth, renderParams.DotsBufHeight, cx, cy, renderParams.DotRadiusPx, dotColor);

                    DrawLineIfNeeded(data, cos, sin, renderParams, cx, cy, dotsBuffer, isMasked);
                }
            }

            textureManager.ApplyDotsTexture();
        }

        private void DrawLineIfNeeded(Core.FurData data, float cos, float sin, RenderParams renderParams, int cx, int cy, Color32[] dotsBuffer, bool isMasked)
        {
            // 傾きに応じて倍率を変える（傾きが大きいほど長いライン）
            // Inclined が 0.0 の時は倍率 0.5、0.95 の時は倍率 2.0 になるように線形補間
            var lineMultiplier = 0.5f + data.Inclined * 1.58f; // 0.5 ~ 2.0 の範囲
            // ピクセル空間での基準長さ（ズームに依存しない）
            var powerLinePx = data.Inclined * Common.Grid * lineMultiplier;
            var dxLinePx = powerLinePx * cos;
            var dyLinePx = powerLinePx * sin;
            // 傾きが0より大きければラインを表示（ピクセル長さで判定）
            float lenPx = Mathf.Sqrt(dxLinePx * dxLinePx + dyLinePx * dyLinePx);
            if (lenPx < 0.5f)
                return;

            // 画面ピクセル空間での上限（可視サンプル間隔に応じて）
            float maxLenPx = Mathf.Max(1f, renderParams.Step * renderParams.Scale * 0.7f);
            if (lenPx > maxLenPx)
            {
                float s = maxLenPx / lenPx;
                dxLinePx *= s;
                dyLinePx *= s;
            }

            // ピクセル座標で線分を描画（ズームに依存しない）
            int x1 = Mathf.RoundToInt(cx + dxLinePx);
            int y1 = Mathf.RoundToInt(cy + dyLinePx);

            // マスク外の場合は暗い白色、それ以外は通常の白色
            byte lineColorValue = isMasked ? (byte)(255 * MaskedDarkenFactor) : (byte)255;
            var lineColor = new Color32(lineColorValue, lineColorValue, lineColorValue, 255);
            DrawLineOnBufferSS(cx, cy, x1, y1, lineColor, dotsBuffer, renderParams.DotsBufWidth, renderParams.DotsBufHeight);
        }

        private Rect? CalculateDrawRect(RenderParams renderParams)
        {
            if (textureManager.DotsTexture == null)
                return null;

            // ビューポート全体に描画（グループ座標系）
            float drawW = renderParams.DotsBufWidth;
            float drawH = renderParams.DotsBufHeight;
            dotsDrawRect = new Rect(0f, 0f, drawW, drawH);
            return dotsDrawRect;
        }



        private static void StampDot(Color32[] buf, int bufWidth, int bufHeight, int cxSS, int cySS, int radiusPx, Color32 color)
        {
            float radius = radiusPx;
            float r2 = radius * radius;
            
            // アンチエイリアシング用の範囲を少し広げる
            int searchRadius = radiusPx + 1;
            
            for (int dy = -searchRadius; dy <= searchRadius; dy++)
            {
                int py = cySS + dy;
                if ((uint)py >= (uint)bufHeight) continue;
                
                for (int dx = -searchRadius; dx <= searchRadius; dx++)
                {
                    int px = cxSS + dx;
                    if ((uint)px >= (uint)bufWidth) continue;
                    
                    // 中心からの距離を計算
                    float dist2 = dx * dx + dy * dy;
                    
                    if (dist2 <= r2)
                    {
                        // 完全に円の内側
                        int iy = (bufHeight - 1) - py;
                        buf[iy * bufWidth + px] = color;
                    }
                    else
                    {
                        // アンチエイリアシング領域（境界から1ピクセル程度）
                        float dist = Mathf.Sqrt(dist2);
                        float diff = dist - radius;
                        
                        if (diff < 1.0f)
                        {
                            // 境界付近：距離に応じてアルファブレンディング
                            float alpha = 1.0f - diff;
                            alpha = Mathf.Clamp01(alpha);
                            
                            int iy = (bufHeight - 1) - py;
                            int idx = iy * bufWidth + px;
                            
                            // 既存のピクセルとアルファブレンディング
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

        private static void DrawLineOnBufferSS(int x0, int y0, int x1, int y1, Color32 color, Color32[] buffer, int width, int height)
        {
            LineDrawingUtils.DrawLineOnBufferFlippedY(x0, y0, x1, y1, color, buffer, width, height);
        }

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
                    // Texture2D.SetPixels32 は下から上に並ぶため、出力見た目を上基準に揃えるにはYを反転して格納
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

