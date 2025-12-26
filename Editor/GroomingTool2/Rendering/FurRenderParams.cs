using GroomingTool2.Core;
using UnityEngine;

namespace GroomingTool2.Rendering
{
    /// <summary>
    /// 毛データ描画用の共通パラメータ
    /// FurDataRenderer と GpuFurDataRenderer で共有
    /// </summary>
    internal struct FurRenderParams
    {
        public int StartX, EndX, StartY, EndY;
        public int Step;
        public int DotsBufWidth, DotsBufHeight;
        public int DotRadiusPx;
        public float Scale;
        public Vector2 ScrollOffsetData;

        /// <summary>
        /// ドット描画の固定半径（ピクセル単位）
        /// </summary>
        public const int FixedDotRadius = 4;

        /// <summary>
        /// GPUレンダラー用のドット半径
        /// </summary>
        public const float GpuFixedDotRadius = 5f;

        /// <summary>
        /// 最大ポイント数の目標値
        /// </summary>
        private const int TargetMaxPoints = 8000;

        /// <summary>
        /// 描画パラメータを計算
        /// </summary>
        /// <param name="viewRect">ビューポート矩形</param>
        /// <param name="scale">ズームスケール</param>
        /// <param name="interval">サンプリング間隔</param>
        /// <param name="scrollOffsetData">スクロールオフセット（データ座標系）</param>
        /// <returns>計算されたパラメータ。可視領域がない場合はnull</returns>
        public static FurRenderParams? Calculate(Rect viewRect, float scale, int interval, Vector2 scrollOffsetData)
        {
            // 可視データ範囲を計算
            CoordinateUtils.GetVisibleDataRange(viewRect, scale, scrollOffsetData, Common.TexSize, 1,
                out int startX, out int endX, out int startY, out int endY);

            int visibleWData = Mathf.Max(0, endX - startX);
            int visibleHData = Mathf.Max(0, endY - startY);
            if (visibleWData <= 0 || visibleHData <= 0)
                return null;

            // ステップ計算（ズームに反比例）
            int stepData = Mathf.Max(1, Mathf.RoundToInt(interval / Mathf.Max(scale, 1e-6f)));

            // 画面ピクセル空間でのポイント数を計算（ズームに依存しない）
            int screenPointsX = Mathf.Max(1, Mathf.CeilToInt(viewRect.width / interval));
            int screenPointsY = Mathf.Max(1, Mathf.CeilToInt(viewRect.height / interval));
            int approxPoints = screenPointsX * screenPointsY;

            int stepMul = 1;
            if (approxPoints > TargetMaxPoints)
            {
                stepMul = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt((float)approxPoints / TargetMaxPoints)));
            }
            int step = Mathf.Max(1, stepData * stepMul);

            // バッファサイズ（ビューポート寸法）
            int dotsW = Mathf.Max(1, Mathf.CeilToInt(viewRect.width));
            int dotsH = Mathf.Max(1, Mathf.CeilToInt(viewRect.height));

            return new FurRenderParams
            {
                StartX = startX,
                EndX = endX,
                StartY = startY,
                EndY = endY,
                Step = step,
                DotsBufWidth = dotsW,
                DotsBufHeight = dotsH,
                DotRadiusPx = FixedDotRadius,
                Scale = scale,
                ScrollOffsetData = scrollOffsetData
            };
        }
    }
}
