#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using NolaTools.FurMaskGenerator.Data;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator.Utils
{
    /// <summary>
    /// DistanceMaskBaker / TexelMaskBaker 共通のユーティリティ
    /// </summary>
    public static class BakerUtils
    {
        /// <summary>
        /// 球体マスク値の計算（距離・半径・グラデーション・強度から算出）
        /// </summary>
        public static float CalculateSphereMaskValue(float distance, float radius, SphereData sphere)
        {
            float innerRadius = radius * (1f - sphere.gradientWidth);
            float v = (distance <= innerRadius)
                ? 0f
                : (distance - innerRadius) / (Mathf.Max(AppSettings.POSITION_PRECISION * AppSettings.POSITION_PRECISION, radius - innerRadius));
            float intensity = Mathf.Clamp(sphere.intensity, AppSettings.SPHERE_INTENSITY_MIN, AppSettings.SPHERE_INTENSITY_MAX);
            return Mathf.Lerp(1f, v, intensity);
        }

        /// <summary>
        /// サブメッシュデータから頂点→マテリアル名マッピングを構築する
        /// </summary>
        public static void BuildVertexToMaterialMapping(
            List<(int[] tri, string mat)> subDatas,
            int vertCount,
            Dictionary<int, string> vertexToMaterialName)
        {
            for (int subIndex = 0; subIndex < subDatas.Count; subIndex++)
            {
                var (triangles, materialName) = subDatas[subIndex];
                if (triangles == null || string.IsNullOrEmpty(materialName)) continue;

                var usedVertices = new HashSet<int>();
                foreach (int vertexIdx in triangles)
                {
                    usedVertices.Add(vertexIdx);
                }

                foreach (int vertexIdx in usedVertices)
                {
                    if (vertexIdx >= 0 && vertexIdx < vertCount)
                    {
                        vertexToMaterialName[vertexIdx] = materialName;
                    }
                }
            }
        }
    }
}
#endif
