#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mask.Generator.Utils;
using Mask.Generator.Data;
using Mask.Generator.Constants;

namespace Mask.Generator
{
    public partial class DistanceMaskBaker
    {
        #region Distance Calculation

        private void BakeStep()
        {
            try
            {
                int end = Mathf.Min(curr + batchSize, verts.Count);
                for (int i = curr; i < end; i++)
                {
                    vDist[i] = CalculateVertexDistanceOptimized(i);
                }
                curr = end;

                if (curr % progressUpdateInterval == 0 || curr >= verts.Count)
                {
                    float progress = (float)curr / verts.Count;
                    if (ShouldUpdateProgressBar(progress, $"{curr}/{verts.Count}"))
                    {
                        Cancel();
                        return;
                    }
                }

                if (curr >= verts.Count)
                {
                    EditorApplication.update -= BakeStep;
                    ApplyUVIslandsToVertexDistances();
                    if (cancelRequested)
                    {
                        Cancel();
                        return;
                    }
                    if (islandAnchorFlags != null)
                    {
                        vDist = SmoothDistAnchored(vDist, settings.UvIslandVertexSmoothIterations, islandAnchorFlags);
                    }
                    else
                    {
                        vDist = SmoothDist(vDist, settings.UvIslandVertexSmoothIterations);
                    }
                    if (!Mathf.Approximately(settings.Gamma, 1.0f))
                    {
                        for (int i = 0; i < vDist.Length; i++)
                        {
                            vDist[i] = Mathf.Clamp01(vDist[i]);
                            vDist[i] = Mathf.Pow(vDist[i], settings.Gamma);
                        }
                    }
                    if (cancelRequested)
                    {
                        Cancel();
                        return;
                    }
                    Finish();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError(string.Format(ErrorMessages.ERROR_BAKE_EXCEPTION, e.Message));
                Cancel();
            }
        }

        private string GetMaterialNameForVertex(int vertexIndex)
        {
            if (vertexIndex < 0 || vertexIndex >= verts.Count) return null;

            if (vertexToMaterialName.TryGetValue(vertexIndex, out string materialName))
            {
                return materialName;
            }

            return null;
        }

        private float CheckSphereMasks(Vector3 vertexPosition)
        {
            float maskValue = 1f;
            
            // 早期終了の最適化
            if (settings.SphereMasks == null || settings.SphereMasks.Count == 0)
                return maskValue;
            
            // 最適化: 平方根計算を最小限に抑制
            float minMaskValue = 1f;
            bool needsSqrtCalculation = false;
            float closestDistSquared = float.MaxValue;
            
            foreach (var sphere in settings.SphereMasks)
            {
                float cr = Mathf.Min(sphere.radius, AppSettings.SHOW_MAX_RADIUS);
                if (cr <= 0f) continue;
                
                // 距離計算の最適化（平方根を避ける）
                float distSquared = (vertexPosition - sphere.position).sqrMagnitude;
                float crSquared = cr * cr;
                
                if (distSquared <= crSquared)
                {
                    // 平方根計算を遅延実行（最も近い球体のみ）
                    if (distSquared < closestDistSquared)
                    {
                        closestDistSquared = distSquared;
                        needsSqrtCalculation = true;
                    }
                    
                    // 内側半径での早期判定（平方根不要）
                    float innerRadiusSquared = crSquared * (1f - sphere.gradientWidth) * (1f - sphere.gradientWidth);
                    if (distSquared <= innerRadiusSquared)
                    {
                        // 完全に内側の場合、即座に0を返す
                        return 0f;
                    }
                }
                
                // ミラー機能の最適化
                if (sphere.useMirror)
                {
                    Vector3 mirroredPosition = new Vector3(-sphere.position.x, sphere.position.y, sphere.position.z);
                    float mirroredDistSquared = (vertexPosition - mirroredPosition).sqrMagnitude;
                    
                    if (mirroredDistSquared <= crSquared)
                    {
                        if (mirroredDistSquared < closestDistSquared)
                        {
                            closestDistSquared = mirroredDistSquared;
                            needsSqrtCalculation = true;
                        }
                        
                        // ミラー内側半径での早期判定
                        float innerRadiusSquared = crSquared * (1f - sphere.gradientWidth) * (1f - sphere.gradientWidth);
                        if (mirroredDistSquared <= innerRadiusSquared)
                        {
                            return 0f;
                        }
                    }
                }
            }
            
            // 必要な場合のみ平方根計算を実行
            if (needsSqrtCalculation)
            {
                float closestDist = Mathf.Sqrt(closestDistSquared);
                
                // 最も近い球体のマスク値を計算
                foreach (var sphere in settings.SphereMasks)
                {
                    float cr = Mathf.Min(sphere.radius, AppSettings.SHOW_MAX_RADIUS);
                    if (cr <= 0f) continue;
                    
                    float distSquared = (vertexPosition - sphere.position).sqrMagnitude;
                    float crSquared = cr * cr;
                    
                    if (Mathf.Abs(distSquared - closestDistSquared) < 0.001f)
                    {
                        float sphereMaskValue = CalculateSphereMaskValueOptimized(closestDist, cr, sphere);
                        minMaskValue = Mathf.Min(minMaskValue, sphereMaskValue);
                    }
                    
                    // ミラー機能
                    if (sphere.useMirror)
                    {
                        Vector3 mirroredPosition = new Vector3(-sphere.position.x, sphere.position.y, sphere.position.z);
                        float mirroredDistSquared = (vertexPosition - mirroredPosition).sqrMagnitude;
                        
                        if (Mathf.Abs(mirroredDistSquared - closestDistSquared) < 0.001f)
                        {
                            float mirroredMaskValue = CalculateSphereMaskValueOptimized(closestDist, cr, sphere);
                            minMaskValue = Mathf.Min(minMaskValue, mirroredMaskValue);
                        }
                    }
                }
                
                maskValue = minMaskValue;
            }
            
            return maskValue;
        }

        /// <summary>
        /// 球体マスク値の計算（重複コードを削除）
        /// </summary>
        private float CalculateSphereMaskValue(float distance, float radius, SphereData sphere)
        {
            float innerRadius = radius * (1f - sphere.gradientWidth);
            float v = (distance <= innerRadius) ? 0f : (distance - innerRadius) / (Mathf.Max(1e-6f, radius - innerRadius));
            float intensity = Mathf.Clamp(sphere.intensity, AppSettings.SPHERE_INTENSITY_MIN, AppSettings.SPHERE_INTENSITY_MAX);
            return Mathf.Lerp(1f, v, intensity);
        }

        /// <summary>
        /// 最適化された球体マスク値の計算（平方根計算を最小限に抑制）
        /// </summary>
        private float CalculateSphereMaskValueOptimized(float distance, float radius, SphereData sphere)
        {
            float innerRadius = radius * (1f - sphere.gradientWidth);
            float v = (distance <= innerRadius) ? 0f : (distance - innerRadius) / (Mathf.Max(1e-6f, radius - innerRadius));
            float intensity = Mathf.Clamp(sphere.intensity, AppSettings.SPHERE_INTENSITY_MIN, AppSettings.SPHERE_INTENSITY_MAX);
            return Mathf.Lerp(1f, v, intensity);
        }

        #endregion
    }
}
#endif


