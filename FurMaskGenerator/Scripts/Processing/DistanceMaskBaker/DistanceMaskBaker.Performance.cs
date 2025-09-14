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
        #region Performance Optimization

        private int CalculateOptimalBatchSize()
        {
            int baseBatchSize = verts.Count / 25;

            int totalTriangles = 0;
            foreach (var (tri, _) in subDatas)
            {
                totalTriangles += tri.Length / 3;
            }

            float complexityRatio = verts.Count > 0 ? (float)totalTriangles / verts.Count : 1f;

            if (complexityRatio > 2.5f)
            {
                baseBatchSize = Mathf.RoundToInt(baseBatchSize * 0.6f);
            }
            else if (complexityRatio > 1.8f)
            {
                baseBatchSize = Mathf.RoundToInt(baseBatchSize * 0.8f);
            }

            if (HasComplexCollision())
            {
                baseBatchSize = Mathf.RoundToInt(baseBatchSize * 0.7f);
            }

            int minBatch = Mathf.Max(50, verts.Count / 100);
            int maxBatch = Mathf.Min(UIConstants.MAX_BATCH_SIZE, verts.Count / 5);

            return Mathf.Clamp(baseBatchSize, minBatch, maxBatch);
        }

        private void PreAllocateMemory()
        {
            int estimatedVertexCount = 0;
            int estimatedTriangleCount = 0;
            int estimatedSubMeshCount = 0;

            int subdivisionFactor = Mathf.RoundToInt(Mathf.Pow(4f, settings.TempSubdivisionIterations));

            foreach (var renderer in settings.AvatarRenderers)
            {
                if (renderer == null) continue;

                var mesh = EditorMeshUtils.GetMeshForRenderer(renderer, out _);
                if (mesh == null) continue;

                estimatedVertexCount += mesh.vertexCount * subdivisionFactor;
                estimatedSubMeshCount += mesh.subMeshCount;

                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    var triangles = mesh.GetTriangles(i);
                    estimatedTriangleCount += triangles.Length / 3 * subdivisionFactor;
                }
            }

            // より精密なメモリ予測と段階的拡張
            estimatedVertexCount = CalculateOptimalVertexCount(estimatedVertexCount, estimatedTriangleCount);
            int optimalCapacity = CalculateDynamicMemoryCapacity(estimatedVertexCount, estimatedTriangleCount);

            if (optimalCapacity > 0)
            {
                // 段階的拡張でメモリ効率を向上
                InitializeMemoryWithCapacity(optimalCapacity);
            }
            else
            {
                InitializeMemoryWithDefaultCapacity();
            }

            subDatas = new List<(int[] tri, string mat)>(estimatedSubMeshCount);
            subRendererPaths = new List<string>(estimatedSubMeshCount);
            subMeshIndices = new List<int>(estimatedSubMeshCount);
        }

        /// <summary>
        /// 動的メモリ容量計算（過去の実行履歴とシステムリソースを考慮）
        /// </summary>
        private int CalculateDynamicMemoryCapacity(int baseVertexCount, int triangleCount)
        {
            // 基本容量の計算
            int baseCapacity = Mathf.Max(baseVertexCount, 1000);
            
            // 過去の実行履歴を考慮（EditorPrefsから取得）
            string historyKey = $"FurMaskGenerator_MemoryHistory_{baseVertexCount}_{triangleCount}";
            float historicalFactor = EditorPrefs.GetFloat(historyKey, 1.0f);
            
            // システムメモリ使用量を考慮
            long systemMemory = System.GC.GetTotalMemory(false);
            float memoryPressure = systemMemory / (1024f * 1024f * 1024f); // GB単位
            
            float memoryFactor = 1.0f;
            if (memoryPressure > 4.0f) // 4GB以上使用中
            {
                memoryFactor = 0.8f; // 控えめに予約
            }
            else if (memoryPressure < 2.0f) // 2GB未満
            {
                memoryFactor = 1.2f; // 多めに予約
            }
            
            // 複雑度に基づく調整
            float complexityFactor = CalculateComplexityFactor(baseVertexCount, triangleCount);
            
            // 最終容量計算
            int finalCapacity = Mathf.RoundToInt(baseCapacity * historicalFactor * memoryFactor * complexityFactor);
            
            // 実行履歴を更新（次回の参考用）
            EditorPrefs.SetFloat(historyKey, Mathf.Lerp(historicalFactor, 1.0f, 0.1f));
            
            return Mathf.Max(finalCapacity, 1000); // 最小容量保証
        }

        /// <summary>
        /// 複雑度係数を計算
        /// </summary>
        private float CalculateComplexityFactor(int vertexCount, int triangleCount)
        {
            if (triangleCount == 0) return 1.0f;
            
            float density = (float)vertexCount / triangleCount;
            
            if (density > 0.8f) // 高密度メッシュ
            {
                return 1.1f;
            }
            else if (density < 0.3f) // 低密度メッシュ
            {
                return 1.3f;
            }
            else // 標準密度
            {
                return 1.2f;
            }
        }

        /// <summary>
        /// 指定容量でメモリを初期化
        /// </summary>
        private void InitializeMemoryWithCapacity(int capacity)
        {
            verts = new List<Vector3>(capacity);
            norms = new List<Vector3>(capacity);
            uvs = new List<Vector2>(capacity);
            tangents = new List<Vector4>(capacity);
            boneMaskValues = new List<float>(capacity);
        }

        /// <summary>
        /// デフォルト容量でメモリを初期化
        /// </summary>
        private void InitializeMemoryWithDefaultCapacity()
        {
            verts = new List<Vector3>();
            norms = new List<Vector3>();
            uvs = new List<Vector2>();
            tangents = new List<Vector4>();
            boneMaskValues = new List<float>();
        }

        /// <summary>
        /// 最適な頂点数を計算（過去の実行履歴を考慮）
        /// </summary>
        private int CalculateOptimalVertexCount(int baseVertexCount, int triangleCount)
        {
            // 複雑度に基づく動的調整
            float complexityRatio = triangleCount > 0 ? (float)baseVertexCount / triangleCount : 1f;
            
            float adjustmentFactor = 1.0f;
            
            if (complexityRatio > 0.8f) // 高密度メッシュ
            {
                adjustmentFactor = 1.1f; // 少し多めに予約
            }
            else if (complexityRatio < 0.3f) // 低密度メッシュ
            {
                adjustmentFactor = 1.3f; // より多く予約
            }
            else // 標準密度
            {
                adjustmentFactor = 1.2f; // 従来通り
            }
            
            return Mathf.RoundToInt(baseVertexCount * adjustmentFactor);
        }

        private void CalculateProgressUpdateInterval()
        {
            int baseInterval = UIConstants.PROGRESS_UPDATE_INTERVAL;

            if (verts.Count > 50000)
            {
                progressUpdateInterval = baseInterval * 4;
            }
            else if (verts.Count > 20000)
            {
                progressUpdateInterval = baseInterval * 2;
            }
            else if (verts.Count < 5000)
            {
                progressUpdateInterval = baseInterval / 2;
            }
            else
            {
                progressUpdateInterval = baseInterval;
            }

            if (HasComplexCollision())
            {
                progressUpdateInterval = Mathf.RoundToInt(progressUpdateInterval * 1.5f);
            }

            progressUpdateInterval = Mathf.Max(progressUpdateInterval, 1);
        }

        private bool HasComplexCollision()
        {
            return clothCollider != null &&
                   ((settings.ClothRenderers?.Count ?? 0) > 3 ||
                    (settings.UVIslandMasks?.Count ?? 0) > 5);
        }

        private bool ShouldUpdateProgressBar(float progress, string message)
        {
            float currentTime = (float)EditorApplication.timeSinceStartup;

            if (currentTime - lastProgressBarUpdate < 0.1f && progress < 0.99f)
            {
                return false;
            }

            if (EditorCoreUtils.ShowCancelableProgressThrottledAutoClear(UIConstants.PROGRESS_BAR_TITLE, message, progress))
            {
                return true;
            }

            lastProgressBarUpdate = currentTime;
            return false;
        }

        private void PrepareRaycastOptimization()
        {
            maxMSquared = maxM * maxM;

            rayDirections = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 n = norms[i].normalized;
                string materialName = GetMaterialNameForVertex(i);
                Vector2 uv = (i < uvs.Count) ? uvs[i] : Vector2.zero;
                rayDirections[i] = SampleNormalMap(materialName, uv, n, i).normalized;
            }

            cachedDistances = new float[verts.Count];
            for (int i = 0; i < cachedDistances.Length; i++)
            {
                cachedDistances[i] = -1f;
            }
        }

        private float CalculateVertexDistanceOptimized(int vertexIndex)
        {
            if (vertexIndex < 0 || vertexIndex >= verts.Count) return 1f;

            if (cachedDistances[vertexIndex] >= 0f)
            {
                return cachedDistances[vertexIndex];
            }

            Vector3 v = verts[vertexIndex];

            float sphereMaskValue = CheckSphereMasks(v);
            float boneControl = (boneMaskValues != null && vertexIndex < boneMaskValues.Count) ? boneMaskValues[vertexIndex] : 0f;
            float boneMaskValue = 1f - Mathf.Clamp01(boneControl);

            float minMaskValue = Mathf.Min(sphereMaskValue, boneMaskValue);
            if (minMaskValue <= UIConstants.POSITION_PRECISION)
            {
                cachedDistances[vertexIndex] = 0f;
                return 0f;
            }

            if (clothCollider == null)
            {
                cachedDistances[vertexIndex] = minMaskValue;
                return minMaskValue;
            }

            float hitDistance = maxM;
            Vector3 n = rayDirections[vertexIndex];

            const float rayOffset = UIConstants.POSITION_PRECISION;
            var ray = new Ray(v - n * rayOffset, n);

            if (clothCollider.Raycast(ray, out RaycastHit hitInfo, maxM + rayOffset))
            {
                hitDistance = Mathf.Max(0, hitInfo.distance - rayOffset);
            }

            float distMask = hitDistance / maxM;
            float finalResult = Mathf.Min(minMaskValue, distMask);

            cachedDistances[vertexIndex] = finalResult;
            return finalResult;
        }

        #endregion
    }
}
#endif


