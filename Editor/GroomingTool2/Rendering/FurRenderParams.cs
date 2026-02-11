using GroomingTool2.Core;
using UnityEngine;

namespace GroomingTool2.Rendering
{
    /// <summary>
    /// 毛データ描画用の共通定数とユーティリティ
    /// FurDataRenderer と GpuFurDataRenderer で共有
    /// </summary>
    internal static class FurRenderParams
    {
        /// <summary>
        /// CPU レンダラー用のドット固定半径（ピクセル単位）
        /// </summary>
        public const int FixedDotRadius = 4;

        /// <summary>
        /// GPU レンダラー用のドット固定半径（ピクセル単位）
        /// </summary>
        public const float GpuFixedDotRadius = 5f;

        /// <summary>
        /// マスク外領域の暗化倍率
        /// </summary>
        public const float MaskedDarkenFactor = 0.4f;

        /// <summary>
        /// ラインの最大長をドット間隔に対する比率で制限するための係数
        /// </summary>
        public const float MaxLineLengthRatio = 0.7f;

        /// <summary>
        /// 最大ポイント数の目標値
        /// 小さすぎるとドット間隔が意図通りに表示されない（例: interval 12 が間引かれて 24 になる）ため
        /// 4Kディスプレイ + 最小間隔でも間引きが発生しない程度の値を設定
        /// </summary>
        public const int TargetMaxPoints = 60000;

        /// <summary>
        /// TargetMaxPoints を超過しないよう、必要に応じてスケールアップした画面ピクセル間隔を計算する
        /// </summary>
        /// <param name="viewRect">ビューポート矩形</param>
        /// <param name="interval">元のサンプリング間隔（画面ピクセル）</param>
        /// <returns>実効スクリーン間隔（画面ピクセル）</returns>
        public static float CalculateScreenInterval(Rect viewRect, int interval)
        {
            int screenPointsX = Mathf.Max(1, Mathf.CeilToInt(viewRect.width / interval));
            int screenPointsY = Mathf.Max(1, Mathf.CeilToInt(viewRect.height / interval));
            int approxPoints = screenPointsX * screenPointsY;

            int stepMul = 1;
            if (approxPoints > TargetMaxPoints)
            {
                stepMul = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt((float)approxPoints / TargetMaxPoints)));
            }
            return interval * stepMul;
        }

        /// <summary>
        /// 可視データ領域があるかどうかを判定する（早期リターン用）
        /// </summary>
        public static bool HasVisibleArea(Rect viewRect, float scale, Vector2 scrollOffsetData)
        {
            CoordinateUtils.GetVisibleDataRange(viewRect, scale, scrollOffsetData, Common.TexSize, 1,
                out int startX, out int endX, out int startY, out int endY);
            return (endX - startX) > 0 && (endY - startY) > 0;
        }

        /// <summary>
        /// ドット描画のためのデータ空間イテレーション範囲
        /// 浮動小数点ステップにより、ズーム倍率に関わらず正確な画面ピクセル間隔を維持する
        /// </summary>
        public readonly struct DotGridRange
        {
            /// <summary>データ空間の走査開始X（グリッドに揃えた位置）</summary>
            public readonly float StartX;
            /// <summary>データ空間の走査開始Y（グリッドに揃えた位置）</summary>
            public readonly float StartY;
            /// <summary>データ空間の走査終了X</summary>
            public readonly float EndX;
            /// <summary>データ空間の走査終了Y</summary>
            public readonly float EndY;
            /// <summary>データ空間での浮動小数点ステップ幅</summary>
            public readonly float Step;

            private DotGridRange(float startX, float startY, float endX, float endY, float step)
            {
                StartX = startX;
                StartY = startY;
                EndX = endX;
                EndY = endY;
                Step = step;
            }

            /// <summary>
            /// 画面間隔・スケール・スクロール位置からイテレーション範囲を計算する
            /// </summary>
            /// <param name="screenInterval">画面ピクセル間隔</param>
            /// <param name="scale">ズームスケール</param>
            /// <param name="scrollOffsetData">スクロールオフセット（データ座標系）</param>
            /// <param name="viewWidth">ビュー幅（ピクセル）</param>
            /// <param name="viewHeight">ビュー高さ（ピクセル）</param>
            public static DotGridRange Calculate(float screenInterval, float scale, Vector2 scrollOffsetData, float viewWidth, float viewHeight)
            {
                float invScale = 1f / Mathf.Max(scale, 1e-6f);
                float step = screenInterval * invScale;

                return new DotGridRange(
                    startX: Mathf.Floor(scrollOffsetData.x / step) * step,
                    startY: Mathf.Floor(scrollOffsetData.y / step) * step,
                    endX: scrollOffsetData.x + viewWidth * invScale,
                    endY: scrollOffsetData.y + viewHeight * invScale,
                    step: step
                );
            }
        }
    }
}
