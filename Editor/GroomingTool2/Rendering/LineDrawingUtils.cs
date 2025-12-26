using UnityEngine;

namespace GroomingTool2.Rendering
{
    /// <summary>
    /// バッファへのライン描画を行う共通ユーティリティ
    /// </summary>
    internal static class LineDrawingUtils
    {
        /// <summary>
        /// バッファにラインを描画する（標準座標系）
        /// </summary>
        public static void DrawLineOnBuffer(int x0, int y0, int x1, int y1, Color32 color, Color32[] buffer, int width, int height)
        {
            DrawLineOnBufferInternal(x0, y0, x1, y1, color, buffer, width, height, flipY: false);
        }

        /// <summary>
        /// バッファにラインを描画する（Texture2D座標系：Y軸が反転）
        /// </summary>
        public static void DrawLineOnBufferFlippedY(int x0, int y0, int x1, int y1, Color32 color, Color32[] buffer, int width, int height)
        {
            DrawLineOnBufferInternal(x0, y0, x1, y1, color, buffer, width, height, flipY: true);
        }

        private static void DrawLineOnBufferInternal(int x0, int y0, int x1, int y1, Color32 color, Color32[] buffer, int width, int height, bool flipY)
        {
            // Xiaolin Wu's line algorithmを使用したアンチエイリアシング付きライン描画
            bool steep = Mathf.Abs(y1 - y0) > Mathf.Abs(x1 - x0);
            
            if (steep)
            {
                // 傾きが急な場合はx/yを入れ替える
                int temp = x0; x0 = y0; y0 = temp;
                temp = x1; x1 = y1; y1 = temp;
            }
            
            if (x0 > x1)
            {
                // x0が常にx1より小さくなるようにする
                int temp = x0; x0 = x1; x1 = temp;
                temp = y0; y0 = y1; y1 = temp;
            }
            
            float dx = x1 - x0;
            float dy = y1 - y0;
            float gradient = dx == 0 ? 1.0f : dy / dx;
            
            // 開始点の処理
            float xEnd = Mathf.Round(x0);
            float yEnd = y0 + gradient * (xEnd - x0);
            float xGap = 1.0f - ((x0 + 0.5f) - Mathf.Floor(x0 + 0.5f));
            int xPixel1 = (int)xEnd;
            int yPixel1 = (int)Mathf.Floor(yEnd);
            
            PlotAntialiasedPixel(xPixel1, yPixel1, (1.0f - (yEnd - Mathf.Floor(yEnd))) * xGap, color, buffer, width, height, steep, flipY);
            PlotAntialiasedPixel(xPixel1, yPixel1 + 1, (yEnd - Mathf.Floor(yEnd)) * xGap, color, buffer, width, height, steep, flipY);
            
            float intery = yEnd + gradient;
            
            // 終了点の処理
            xEnd = Mathf.Round(x1);
            yEnd = y1 + gradient * (xEnd - x1);
            xGap = (x1 + 0.5f) - Mathf.Floor(x1 + 0.5f);
            int xPixel2 = (int)xEnd;
            int yPixel2 = (int)Mathf.Floor(yEnd);
            
            PlotAntialiasedPixel(xPixel2, yPixel2, (1.0f - (yEnd - Mathf.Floor(yEnd))) * xGap, color, buffer, width, height, steep, flipY);
            PlotAntialiasedPixel(xPixel2, yPixel2 + 1, (yEnd - Mathf.Floor(yEnd)) * xGap, color, buffer, width, height, steep, flipY);
            
            // メインループ
            for (int x = xPixel1 + 1; x < xPixel2; x++)
            {
                int yFloor = (int)Mathf.Floor(intery);
                float frac = intery - yFloor;
                
                PlotAntialiasedPixel(x, yFloor, 1.0f - frac, color, buffer, width, height, steep, flipY);
                PlotAntialiasedPixel(x, yFloor + 1, frac, color, buffer, width, height, steep, flipY);
                
                intery += gradient;
            }
        }
        
        private static void PlotAntialiasedPixel(int x, int y, float alpha, Color32 color, Color32[] buffer, int width, int height, bool steep, bool flipY)
        {
            // steepの場合は座標を入れ替える
            if (steep)
            {
                int temp = x;
                x = y;
                y = temp;
            }
            
            // 範囲チェック
            if ((uint)x >= (uint)width || (uint)y >= (uint)height)
                return;
            
            // Y軸反転処理
            int bufferY = flipY ? (height - 1) - y : y;
            int idx = bufferY * width + x;
            
            // アルファブレンディング
            alpha = Mathf.Clamp01(alpha);
            Color32 existing = buffer[idx];
            
            byte blendedR = (byte)(color.r * alpha + existing.r * (1 - alpha));
            byte blendedG = (byte)(color.g * alpha + existing.g * (1 - alpha));
            byte blendedB = (byte)(color.b * alpha + existing.b * (1 - alpha));
            byte blendedA = (byte)Mathf.Max(color.a * alpha, existing.a);
            
            buffer[idx] = new Color32(blendedR, blendedG, blendedB, blendedA);
        }
    }
}

