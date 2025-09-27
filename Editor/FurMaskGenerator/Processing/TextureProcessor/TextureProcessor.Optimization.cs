#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator
{
    public partial class TextureProcessor
    {
        #region 最適化ヘルパーメソッド

        private void CalculateAdaptiveProgressInterval()
        {
            int totalTriangles = 0;
            foreach (var (tri, _) in subDatas)
            {
                totalTriangles += tri.Length / 3;
            }

            int baseInterval = AppSettings.PROGRESS_UPDATE_INTERVAL;
            float complexityFactor = (float)totalTriangles / (texSize * texSize);

            if (complexityFactor > AppSettings.UV_THRESHOLD_DEFAULT)
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

        #endregion
    }
}
#endif

