using System.Runtime.CompilerServices;
using UnityEngine;

namespace GroomingTool2.Rendering
{
    /// <summary>
    /// クリッピング処理の共通ユーティリティ
    /// Cohen-Sutherlandアルゴリズムによるライン・矩形クリッピング
    /// </summary>
    internal static class ClippingUtils
    {
        /// <summary>
        /// クリップ領域を表す構造体
        /// </summary>
        public readonly struct ClipRect
        {
            public readonly float MinX;
            public readonly float MaxX;
            public readonly float MinY;
            public readonly float MaxY;

            public ClipRect(float minX, float maxX, float minY, float maxY)
            {
                MinX = minX;
                MaxX = maxX;
                MinY = minY;
                MaxY = maxY;
            }

            /// <summary>
            /// Rect から ClipRect を作成（0,0 を原点とするキャンバス用）
            /// </summary>
            public static ClipRect FromCanvasRect(Rect canvasRect)
            {
                return new ClipRect(0, canvasRect.width, 0, canvasRect.height);
            }

            /// <summary>
            /// 矩形が完全にクリップ領域外かどうかを判定
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsOutside(float minX, float maxX, float minY, float maxY)
            {
                return maxX < MinX || minX > MaxX || maxY < MinY || minY > MaxY;
            }
        }

        // Cohen-Sutherland用アウトコード定数
        private const int OutCodeInside = 0; // 0000
        private const int OutCodeLeft = 1;   // 0001
        private const int OutCodeRight = 2;  // 0010
        private const int OutCodeBottom = 4; // 0100
        private const int OutCodeTop = 8;    // 1000

        /// <summary>
        /// Cohen-Sutherland用のアウトコード計算
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ComputeOutCode(float x, float y, in ClipRect clip)
        {
            int code = OutCodeInside;
            if (x < clip.MinX) code |= OutCodeLeft;
            else if (x > clip.MaxX) code |= OutCodeRight;
            if (y < clip.MinY) code |= OutCodeBottom;
            else if (y > clip.MaxY) code |= OutCodeTop;
            return code;
        }

        /// <summary>
        /// Cohen-Sutherlandアルゴリズムでラインをクリップ
        /// </summary>
        /// <param name="x0">始点X</param>
        /// <param name="y0">始点Y</param>
        /// <param name="x1">終点X</param>
        /// <param name="y1">終点Y</param>
        /// <param name="clip">クリップ領域</param>
        /// <returns>ラインが可視領域内にある場合true。クリップ後の座標はref引数で返される</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ClipLine(ref float x0, ref float y0, ref float x1, ref float y1, in ClipRect clip)
        {
            int outcode0 = ComputeOutCode(x0, y0, clip);
            int outcode1 = ComputeOutCode(x1, y1, clip);

            while (true)
            {
                if ((outcode0 | outcode1) == 0)
                {
                    // 両方が内側：描画OK
                    return true;
                }
                else if ((outcode0 & outcode1) != 0)
                {
                    // 両方が同じ外側領域：完全に外側なので描画しない
                    return false;
                }
                else
                {
                    // 少なくとも1つが外側：クリップが必要
                    int outcodeOut = outcode0 != 0 ? outcode0 : outcode1;
                    float x = 0, y = 0;

                    if ((outcodeOut & OutCodeTop) != 0)
                    {
                        x = x0 + (x1 - x0) * (clip.MaxY - y0) / (y1 - y0);
                        y = clip.MaxY;
                    }
                    else if ((outcodeOut & OutCodeBottom) != 0)
                    {
                        x = x0 + (x1 - x0) * (clip.MinY - y0) / (y1 - y0);
                        y = clip.MinY;
                    }
                    else if ((outcodeOut & OutCodeRight) != 0)
                    {
                        y = y0 + (y1 - y0) * (clip.MaxX - x0) / (x1 - x0);
                        x = clip.MaxX;
                    }
                    else if ((outcodeOut & OutCodeLeft) != 0)
                    {
                        y = y0 + (y1 - y0) * (clip.MinX - x0) / (x1 - x0);
                        x = clip.MinX;
                    }

                    if (outcodeOut == outcode0)
                    {
                        x0 = x;
                        y0 = y;
                        outcode0 = ComputeOutCode(x0, y0, clip);
                    }
                    else
                    {
                        x1 = x;
                        y1 = y;
                        outcode1 = ComputeOutCode(x1, y1, clip);
                    }
                }
            }
        }

        /// <summary>
        /// GL描画用：Cohen-Sutherlandアルゴリズムでラインをクリップして描画
        /// GL.LINESモード内で呼び出すこと
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DrawClippedLine(Vector2 p0, Vector2 p1, in ClipRect clip)
        {
            float x0 = p0.x, y0 = p0.y;
            float x1 = p1.x, y1 = p1.y;

            if (ClipLine(ref x0, ref y0, ref x1, ref y1, clip))
            {
                GL.Vertex3(x0, y0, 0);
                GL.Vertex3(x1, y1, 0);
            }
        }

        /// <summary>
        /// GL描画用：Cohen-Sutherlandアルゴリズムでラインをクリップして描画（float引数版）
        /// GL.LINESモード内で呼び出すこと
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DrawClippedLine(float x0, float y0, float x1, float y1, in ClipRect clip)
        {
            if (ClipLine(ref x0, ref y0, ref x1, ref y1, clip))
            {
                GL.Vertex3(x0, y0, 0);
                GL.Vertex3(x1, y1, 0);
            }
        }
    }
}
