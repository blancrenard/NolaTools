#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator
{
    public partial class TextureProcessor
    {
        #region ラスタライゼーション

        private void FillTriIntoBuffer(Color[] colors, int width, int height, Vector2 uv0, Vector2 uv1, Vector2 uv2, float val0, float val1, float val2, HashSet<int> rasterizedPixels = null)
        {
            Vector2 p0 = new Vector2(uv0.x * width, uv0.y * height);
            Vector2 p1 = new Vector2(uv1.x * width, uv1.y * height);
            Vector2 p2 = new Vector2(uv2.x * width, uv2.y * height);

            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.x, p1.x, p2.x)), 0, width - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.x, p1.x, p2.x)), 0, width - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.y, p1.y, p2.y)), 0, height - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.y, p1.y, p2.y)), 0, height - 1);

            if (maxX < minX || maxY < minY) return;

            // 事前計算でパフォーマンス向上
            float invWidth = 1f / width;
            float invHeight = 1f / height;
            float threshold = 1.0f - AppSettings.POSITION_PRECISION;

            for (int y = minY; y <= maxY; y++)
            {
                int yOffset = y * width;
                float yPos = y + 0.5f;
                
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3 b = EditorMeshUtils.GetBarycentric(new Vector2(x + 0.5f, yPos), p0, p1, p2);
                    if (b.x >= 0 && b.y >= 0 && b.z >= 0)
                    {
                        float val = b.x * val0 + b.y * val1 + b.z * val2;
                        val = Mathf.Clamp01(val);

                        int colorIndex = yOffset + x;
                        
                        if (useTransparentMode)
                        {
                            // 透過モードの最適化
                            if (val >= threshold)
                            {
                                colors[colorIndex] = new Color(val, val, val, 0f);
                            }
                            else
                            {
                                float alpha = 1f - val;
                                colors[colorIndex] = new Color(0f, 0f, 0f, alpha);
                            }
                        }
                        else
                        {
                            colors[colorIndex] = new Color(val, val, val, 1f);
                        }

                        rasterizedPixels?.Add(colorIndex);
                    }
                }
            }
        }

        private void FillTriBlackIntoBuffer(Color[] colors, int width, int height, Vector2 uv0, Vector2 uv1, Vector2 uv2)
        {
            Vector2 p0 = new Vector2(uv0.x * width, uv0.y * height);
            Vector2 p1 = new Vector2(uv1.x * width, uv1.y * height);
            Vector2 p2 = new Vector2(uv2.x * width, uv2.y * height);

            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.x, p1.x, p2.x)), 0, width - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.x, p1.x, p2.x)), 0, width - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.y, p1.y, p2.y)), 0, height - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.y, p1.y, p2.y)), 0, height - 1);

            if (maxX < minX || maxY < minY) return;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3 b = EditorMeshUtils.GetBarycentric(new Vector2(x + .5f, y + .5f), p0, p1, p2);
                    if (b.x >= 0 && b.y >= 0 && b.z >= 0)
                    {
                        int colorIndex = y * width + x;
                        colors[colorIndex] = Color.black;
                    }
                }
            }
        }

        #endregion
    }
}
#endif

