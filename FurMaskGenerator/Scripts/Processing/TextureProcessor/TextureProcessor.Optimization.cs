#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mask.Generator.Constants;

namespace Mask.Generator
{
    public partial class TextureProcessor
    {
        #region Optimization Helper Methods

        private void CalculateAdaptiveProgressInterval()
        {
            int totalTriangles = 0;
            foreach (var (tri, _) in subDatas)
            {
                totalTriangles += tri.Length / 3;
            }

            int baseInterval = AppSettings.PROGRESS_UPDATE_INTERVAL;
            float complexityFactor = (float)totalTriangles / (texSize * texSize);

            if (complexityFactor > 0.1f)
            {
                adaptiveProgressInterval = baseInterval * 6;
            }
            else if (complexityFactor > 0.05f)
            {
                adaptiveProgressInterval = baseInterval * 3;
            }
            else
            {
                adaptiveProgressInterval = baseInterval;
            }
        }

        private bool ShouldSkipTriangle(Vector2 uv0, Vector2 uv1, Vector2 uv2, int width, int height)
        {
            Vector2 p0 = new Vector2(uv0.x * width, uv0.y * height);
            Vector2 p1 = new Vector2(uv1.x * width, uv1.y * height);
            Vector2 p2 = new Vector2(uv2.x * width, uv2.y * height);

            float minX = Mathf.Min(p0.x, p1.x, p2.x);
            float maxX = Mathf.Max(p0.x, p1.x, p2.x);
            float minY = Mathf.Min(p0.y, p1.y, p2.y);
            float maxY = Mathf.Max(p0.y, p1.y, p2.y);

            if (maxX < 0 || minX >= width || maxY < 0 || minY >= height)
            {
                return true;
            }

            float area = 0.5f * Mathf.Abs((p1.x - p0.x) * (p2.y - p0.y) - (p2.x - p0.x) * (p1.y - p0.y));
            if (area < 0.5f)
            {
                return true;
            }

            return false;
        }

        #endregion
    }
}
#endif

