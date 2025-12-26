using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GroomingTool2.Managers;
using GroomingTool2.State;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using GroomingTool2.Core.Jobs;
using UnityEngine;

namespace GroomingTool2.Core
{
    /// <summary>
    /// ブラシ更新パラメータ
    /// </summary>
    internal struct BrushUpdateParameters
    {
        public float cos1;
        public float sin1;
        public float brushPowerCubed;
        public float brushPowerScale;
        public float maxInclination;
        public int brushSize;
        public Vector2 blurAverageVector;
        public float2 blurDirNorm;
        public float blurAverageLength;
        public byte blurUseFallback;
    }

    /// <summary>
    /// ブラシ処理を実行するクラス
    /// </summary>
    internal sealed class BrushProcessor
    {
        private readonly BrushManager brushManager;
        private readonly GroomingTool2State state;
        
        // Pooled native buffers
        private NativeArray<MyBrushData> pooledBrushSamples;
        
        // Pooled buffers for reducing allocations
        private NativeArray<int2> pooledPoints;
        private int pooledPointsCapacity;

        public BrushProcessor(BrushManager brushManager, GroomingTool2State state)
        {
            this.brushManager = brushManager;
            this.state = state;
        }

        public void Dispose()
        {
            if (pooledBrushSamples.IsCreated) pooledBrushSamples.Dispose();
            if (pooledPoints.IsCreated) pooledPoints.Dispose();
        }

        private void UpdateBrushSamples(List<MyBrushData> brush)
        {
            if (brush == null) return;

            if (!pooledBrushSamples.IsCreated || pooledBrushSamples.Length != brush.Count)
            {
                if (pooledBrushSamples.IsCreated) pooledBrushSamples.Dispose();
                pooledBrushSamples = new NativeArray<MyBrushData>(brush.Count, Allocator.Persistent);
            }

            // Always copy content as it might have changed
            for (int i = 0; i < brush.Count; i++)
            {
                pooledBrushSamples[i] = brush[i];
            }
        }



        /// <summary>
        /// ブラシ処理を実行（ジョブ版）
        /// NativeArray を直接操作し、コピーを最小化
        /// </summary>
        public void ProcessBrushJobs(
            NativeArray<FurData> furData,
            List<Vector2Int> points,
            float radian,
            bool eraserMode,
            bool blurMode,
            bool pinchMode,
            bool inclinedOnly,
            bool dirOnly,
            bool pinchInverted,
            NativeArray<byte> mask)
        {
            if (points == null || points.Count < 2)
                return;

            var parameters = CalculateUpdateParameters(points, radian, eraserMode, blurMode, pinchMode, furData);
            var brush = brushManager.Brush;
            UpdateBrushSamples(brush);

            // ポイントバッファを確保してコピー
            EnsurePointsBufferCapacity(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                pooledPoints[i] = new int2(points[i].x, points[i].y);
            }

            var pointsSlice = pooledPoints.GetSubArray(0, points.Count);
            bool hasMask = mask.IsCreated && mask.Length > 0;

            // Job を実行（furData を直接操作）
            var job = new ApplyBrushStrokeJob
            {
                TexSize = Common.TexSize,
                FurData = furData,
                Points = pointsSlice,
                BrushSamples = pooledBrushSamples,
                Mask = hasMask ? mask : default,

                Cos1 = parameters.cos1,
                Sin1 = parameters.sin1,
                BrushPowerCubed = parameters.brushPowerCubed,
                BrushPowerScale = parameters.brushPowerScale,
                MaxInclination = parameters.maxInclination,
                BrushSize = parameters.brushSize,
                BlurMode = (byte)(blurMode ? 1 : 0),
                PinchMode = (byte)(pinchMode ? 1 : 0),
                PinchInverted = (byte)(pinchInverted ? 1 : 0),
                InclinedOnly = (byte)(inclinedOnly ? 1 : 0),
                DirOnly = (byte)(dirOnly ? 1 : 0),
                BlurDirNormalized = parameters.blurDirNorm,
                BlurAverageLength = parameters.blurAverageLength,
                BlurUseFallback = parameters.blurUseFallback
            };

            job.Run();
        }

        /// <summary>
        /// ポイントバッファの容量を確保（必要に応じて拡張）
        /// </summary>
        private void EnsurePointsBufferCapacity(int requiredSize)
        {
            if (pooledPointsCapacity < requiredSize)
            {
                int newCapacity = Mathf.Max(requiredSize, pooledPointsCapacity * 2, 256);
                
                if (pooledPoints.IsCreated) pooledPoints.Dispose();
                pooledPoints = new NativeArray<int2>(newCapacity, Allocator.Persistent);
                
                pooledPointsCapacity = newCapacity;
            }
        }

        /// <summary>
        /// 更新処理に共通のパラメータを計算
        /// </summary>
        private BrushUpdateParameters CalculateUpdateParameters(
            List<Vector2Int> points,
            float radian,
            bool eraserMode,
            bool blurMode,
            bool pinchMode,
            NativeArray<FurData> furData)
        {
            Vector2 blurAverageVector = Vector2.zero;
            if (blurMode && points.Count > 0)
            {
                blurAverageVector = GetAverageLength(points[points.Count - 1], brushManager.Brush, furData, useFilter: false);
            }

            var cos1 = Mathf.Cos(radian);
            var sin1 = Mathf.Sin(radian);
            var brushPowerCubed = Mathf.Pow(brushManager.BrushPower, 3f);
            var brushPowerScale = 0.2f;
            // 消しゴムモードではmaxInclinationを0に、つまむモードではstate.Inclinedを使用
            var maxInclination = eraserMode ? 0f : (pinchMode && state != null ? state.Inclined : brushManager.MaxInclination);
            var brushSize = brushManager.BrushSize;

            float2 blurDirNorm = default;
            float blurAverageLength = 0f;
            byte blurUseFallback = 0;
            if (blurMode)
            {
                var pLenSqr = blurAverageVector.x * blurAverageVector.x + blurAverageVector.y * blurAverageVector.y;
                if (pLenSqr > 1e-12f)
                {
                    var pLen = Mathf.Sqrt(pLenSqr);
                    blurDirNorm = new float2(blurAverageVector.x / pLen, blurAverageVector.y / pLen);
                    blurAverageLength = pLen;
                    blurUseFallback = 0;
                }
                else
                {
                    blurDirNorm = new float2(1f, 0f);
                    blurAverageLength = 0f;
                    blurUseFallback = 1;
                }
            }

            return new BrushUpdateParameters
            {
                cos1 = cos1,
                sin1 = sin1,
                brushPowerCubed = brushPowerCubed,
                brushPowerScale = brushPowerScale,
                maxInclination = maxInclination,
                brushSize = brushSize,
                blurAverageVector = blurAverageVector,
                blurDirNorm = blurDirNorm,
                blurAverageLength = blurAverageLength,
                blurUseFallback = blurUseFallback
            };
        }

        /// <summary>
        /// ブラシ範囲内の平均ベクトルを計算
        /// </summary>
        private static Vector2 GetAverageLength(Vector2Int point, List<MyBrushData> brush, NativeArray<FurData> furData, bool useFilter = true)
        {
            var total = Vector2.zero;
            var count = 0f;
            var averageInfluence = 0f;

            // 影響度フィルタを使用する場合は平均値を計算
            if (useFilter)
            {
                foreach (var item in brush)
                    averageInfluence += item.Influence;
                averageInfluence /= brush.Count;
            }

            foreach (var item in brush)
            {
                // 影響度フィルタを使用する場合は閾値チェック
                if (useFilter && item.Influence < averageInfluence)
                    continue;

                int indexX = point.x + item.X;
                int indexY = point.y + item.Y;
                
                // 高速な範囲チェック
                if (indexX < 0 || indexX >= Common.TexSize || indexY < 0 || indexY >= Common.TexSize)
                    continue;
                
                int index = Common.GetIndex(indexX, indexY);
                var data = furData[index];
                
                // ニュートラル状態のベクトルも含めて平均を計算
                total.x += data.Inclined * AngleLut.GetCos(data.Dir);
                total.y += data.Inclined * AngleLut.GetSin(data.Dir);
                count += 1f;
            }

            if (count <= 0f)
            {
                int pointIndex = Common.GetIndex(point.x, point.y);
                var data = furData[pointIndex];
                total = new Vector2(
                    data.Inclined * AngleLut.GetCos(data.Dir),
                    data.Inclined * AngleLut.GetSin(data.Dir));
                count = 1f;
            }

            return total / Mathf.Max(count, 1f);
        }
    }
}
