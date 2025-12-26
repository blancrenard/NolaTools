using System.Runtime.CompilerServices;
using UnityEngine;

namespace GroomingTool2.Core
{
	internal static class CoordinateUtils
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void GetVisibleDataRange(Rect canvasRect, float scale, Vector2 scrollOffsetData, int texSize, int grid, out int startX, out int endX, out int startY, out int endY)
		{
			startX = Mathf.Max(grid / 2, Mathf.FloorToInt(scrollOffsetData.x));
			endX = Mathf.Min(texSize, Mathf.CeilToInt(scrollOffsetData.x + canvasRect.width / scale));
			startY = Mathf.Max(grid / 2, Mathf.FloorToInt(scrollOffsetData.y));
			endY = Mathf.Min(texSize, Mathf.CeilToInt(scrollOffsetData.y + canvasRect.height / scale));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector2 ViewLocalToData(Vector2 viewLocal, Vector2 scrollOffsetData, float scale)
		{
			return scrollOffsetData + viewLocal / Mathf.Max(scale, 1e-6f);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector2 DataToViewLocal(Vector2 dataPosition, Vector2 scrollOffsetData, float scale)
		{
			return (dataPosition - scrollOffsetData) * scale;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ClampScrollOffsetData(Vector2 viewSizePx, float scale, int texSize, Vector2 offsetData)
        {
            float visibleWData = viewSizePx.x / Mathf.Max(scale, 1e-6f);
            float visibleHData = viewSizePx.y / Mathf.Max(scale, 1e-6f);
            float maxX = Mathf.Max(0f, texSize - visibleWData);
            float maxY = Mathf.Max(0f, texSize - visibleHData);
            return new Vector2(
                Mathf.Clamp(offsetData.x, 0f, maxX),
                Mathf.Clamp(offsetData.y, 0f, maxY)
            );
        }

        /// <summary>
        /// UV座標をローカルビュー座標に変換（GUI.BeginGroup後の座標系）
        /// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 UvToViewLocal(Vector2 uv, float scale, Vector2 scrollOffsetData)
        {
            var x = uv.x * Common.TexSize * scale - scrollOffsetData.x * scale;
            var y = (1f - uv.y) * Common.TexSize * scale - scrollOffsetData.y * scale;
            return new Vector2(x, y);
        }

        /// <summary>
        /// UV座標をスクリーン座標（ローカルビュー座標）に変換
        /// データ座標を経由した明示的な変換
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 UvToScreen(Vector2 uv, float scale, Vector2 scrollOffsetData)
        {
            float dataX = uv.x * Common.TexSize;
            float dataY = (1f - uv.y) * Common.TexSize;
            float screenX = (dataX - scrollOffsetData.x) * scale;
            float screenY = (dataY - scrollOffsetData.y) * scale;
            return new Vector2(screenX, screenY);
        }
    }
}


