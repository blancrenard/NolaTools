#if UNITY_EDITOR
using UnityEngine;

namespace Mask.Generator.Utils
{
	/// <summary>
	/// テクスチャ操作の共通ユーティリティ（Editor専用）
	/// </summary>
	public static class EditorTextureUtils
	{
		/// <summary>
		/// エッジパディングを適用する。originalValidMask が指定されない場合は sourcePixels から推定する。
		/// </summary>
		public static Color[] ApplyEdgePadding(Color[] sourcePixels, int width, int height, int paddingSize, bool[] originalValidMask = null, float validPixelThreshold = 1e-5f)
		{
			if (sourcePixels == null || sourcePixels.Length != width * height || width <= 0 || height <= 0)
			{
				return sourcePixels;
			}

			bool[] originalValid = originalValidMask;
			if (originalValid == null)
			{
				originalValid = BuildValidMaskFromPixels(sourcePixels, validPixelThreshold);
			}

			Color[] paddedPixels = new Color[width * height];
			System.Array.Copy(sourcePixels, paddedPixels, sourcePixels.Length);

			if (paddingSize <= 0)
			{
				return paddedPixels;
			}

			int r = Mathf.Max(1, paddingSize);
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int idx = y * width + x;
					if (idx < 0 || idx >= originalValid.Length) continue;
					if (originalValid[idx]) continue; // もともと描画済みは対象外

					bool found = false;
					Color picked = default;
					float bestD2 = float.MaxValue;
					int minY = Mathf.Max(0, y - r), maxY = Mathf.Min(height - 1, y + r);
					int minX = Mathf.Max(0, x - r), maxX = Mathf.Min(width - 1, x + r);
					for (int ny = minY; ny <= maxY; ny++)
					{
						int dy = ny - y; int dy2 = dy * dy;
						for (int nx = minX; nx <= maxX; nx++)
						{
							int dx = nx - x; int d2 = dx * dx + dy2;
							if (d2 == 0 || d2 > bestD2) continue;
							int nidx = ny * width + nx;
							if (nidx >= 0 && nidx < originalValid.Length && originalValid[nidx])
							{
								bestD2 = d2;
								picked = sourcePixels[nidx];
								found = true;
							}
						}
					}

					if (found)
					{
						paddedPixels[idx] = picked;
					}
				}
			}

			return paddedPixels;
		}

		/// <summary>
		/// ラスタライズされた画素インデックス集合から有効マスクを構築する
		/// </summary>
		public static bool[] BuildValidMaskFromRasterized(System.Collections.Generic.HashSet<int> rasterizedPixels, int totalPixels)
		{
			bool[] mask = new bool[totalPixels];
			if (rasterizedPixels == null) return mask;
			foreach (int pixelIdx in rasterizedPixels)
			{
				if (pixelIdx >= 0 && pixelIdx < totalPixels)
				{
					mask[pixelIdx] = true;
				}
			}
			return mask;
		}

		/// <summary>
		/// ピクセル値から有効マスクを推定する（白から十分離れている画素を有効とする）
		/// </summary>
		public static bool[] BuildValidMaskFromPixels(Color[] pixels, float validPixelThreshold)
		{
			bool[] mask = new bool[pixels.Length];
			for (int i = 0; i < pixels.Length; i++)
			{
				mask[i] = pixels[i].r < 1f - validPixelThreshold;
			}
			return mask;
		}
	}
}
#endif


