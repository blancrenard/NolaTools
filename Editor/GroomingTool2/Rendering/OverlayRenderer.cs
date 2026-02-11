using System.Collections.Generic;
using GroomingTool2.Core;
using UnityEditor;
using UnityEngine;

namespace GroomingTool2.Rendering
{
    /// <summary>
    /// オーバーレイ描画（マスクプレビュー、ブラシカーソル）を担当するクラス
    /// </summary>
    internal static class OverlayRenderer
    {
        /// <summary>
        /// マスク選択プレビューを点線で描画
        /// </summary>
        public static void DrawMaskPreview(MaskPreviewParams preview, float scale, Vector2 scrollOffsetData)
        {
            Handles.color = Color.white;
            const float dotSpacing = 5f;
            
            if (preview.Mode == MaskSelectionMode.Rectangle)
            {
                // 矩形選択：データ座標をコンテンツ座標に変換
                var startContent = CoordinateUtils.DataToViewLocal(preview.RectStartData, scrollOffsetData, scale);
                var endContent = CoordinateUtils.DataToViewLocal(preview.RectEndData, scrollOffsetData, scale);
                
                // 矩形の4隅を計算
                var minX = Mathf.Min(startContent.x, endContent.x);
                var maxX = Mathf.Max(startContent.x, endContent.x);
                var minY = Mathf.Min(startContent.y, endContent.y);
                var maxY = Mathf.Max(startContent.y, endContent.y);
                
                var topLeft = new Vector3(minX, minY, 0);
                var topRight = new Vector3(maxX, minY, 0);
                var bottomLeft = new Vector3(minX, maxY, 0);
                var bottomRight = new Vector3(maxX, maxY, 0);
                
                // 4辺を点線で描画
                Handles.DrawDottedLine(topLeft, topRight, dotSpacing);
                Handles.DrawDottedLine(topRight, bottomRight, dotSpacing);
                Handles.DrawDottedLine(bottomRight, bottomLeft, dotSpacing);
                Handles.DrawDottedLine(bottomLeft, topLeft, dotSpacing);
            }
            else if (preview.Mode == MaskSelectionMode.Lasso && preview.LassoPointsData != null && preview.LassoPointsData.Count >= 2)
            {
                // 投げ縄選択：データ座標をコンテンツ座標に変換して点線で描画
                for (int i = 0; i < preview.LassoPointsData.Count - 1; i++)
                {
                    var p0 = CoordinateUtils.DataToViewLocal(preview.LassoPointsData[i], scrollOffsetData, scale);
                    var p1 = CoordinateUtils.DataToViewLocal(preview.LassoPointsData[i + 1], scrollOffsetData, scale);
                    Handles.DrawDottedLine(new Vector3(p0.x, p0.y, 0), new Vector3(p1.x, p1.y, 0), dotSpacing);
                }
                
                // 始点と終点を接続（閉じた形状にする）
                if (preview.LassoPointsData.Count >= 3)
                {
                    var first = CoordinateUtils.DataToViewLocal(preview.LassoPointsData[0], scrollOffsetData, scale);
                    var last = CoordinateUtils.DataToViewLocal(preview.LassoPointsData[preview.LassoPointsData.Count - 1], scrollOffsetData, scale);
                    Handles.DrawDottedLine(new Vector3(first.x, first.y, 0), new Vector3(last.x, last.y, 0), dotSpacing);
                }
            }
        }

        /// <summary>
        /// ブラシカーソルを描画
        /// </summary>
        public static void DrawBrushCursor(Vector2 mousePosition, float brushSize, Color brushColor)
        {
            var radius = brushSize * 2f;
            Handles.color = brushColor;
            Handles.DrawWireDisc(mousePosition, Vector3.forward, radius);
        }
    }
}

