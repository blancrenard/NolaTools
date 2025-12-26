using System;
using System.Buffers;
using System.Collections.Generic;
using GroomingTool2.Managers;
using GroomingTool2.State;
using UnityEngine;

namespace GroomingTool2.Core
{
    /// <summary>
    /// ブラシストローク処理を実行するクラス
    /// キャンバスとSceneビューの両方から使用される
    /// </summary>
    internal sealed class BrushStrokeExecutor
    {
        private readonly BrushManager brushManager;
        private readonly FurDataManager furDataManager;
        private readonly List<Vector2Int> linePointsBuffer = new();
        
        // ArrayPoolでマスク配列を再利用（GC削減）
        private static readonly int MaskArraySize = Common.TexSizeSquared;

        // マスクキャッシュ
        private byte[] cachedCombinedMask;
        private int cachedMaskStateVersion = -1;
        private bool[,] cachedUvRegionMask;
        private bool cachedMaskStateRestrictEditing;
        private bool cachedMaskStateHasAnySelection;

        public BrushStrokeExecutor(BrushManager brushManager, FurDataManager furDataManager)
        {
            this.brushManager = brushManager ?? throw new System.ArgumentNullException(nameof(brushManager));
            this.furDataManager = furDataManager ?? throw new System.ArgumentNullException(nameof(furDataManager));
        }

        /// <summary>
        /// マスクキャッシュを無効化する（マスクが外部で変更された場合に呼び出す）
        /// </summary>
        public void InvalidateMaskCache()
        {
            cachedMaskStateVersion = -1;
            cachedUvRegionMask = null;
            if (cachedCombinedMask != null)
            {
                ArrayPool<byte>.Shared.Return(cachedCombinedMask);
                cachedCombinedMask = null;
            }
        }

        /// <summary>
        /// ブラシストロークを適用する（方向とUVマスクを直接指定）
        /// </summary>
        /// <param name="points">ストローク点列（データ座標系、0～TexSize-1）</param>
        /// <param name="maskState">マスク状態</param>
        /// <param name="mirrorEnabled">ミラー編集が有効か</param>
        /// <param name="eraserMode">消しゴムモードか</param>
        /// <param name="blurMode">ぼかしモードか</param>
        /// <param name="pinchMode">つまむモードか</param>
        /// <param name="inclinedOnly">傾きのみ変更か</param>
        /// <param name="dirOnly">向きのみ変更か</param>
        /// <param name="pinchInverted">つまむモードの反転か</param>
        /// <param name="overrideRadians">上書きするストローク方向（ラジアン）。nullの場合は点列から自動計算</param>
        /// <param name="uvRegionMask">UV領域マスク。nullの場合は制限なし</param>
        public void ExecuteStrokeWithDirectionAndUvMask(
            IReadOnlyList<Vector2> points,
            State.UvIslandMaskState maskState,
            bool mirrorEnabled,
            bool eraserMode,
            bool blurMode,
            bool pinchMode,
            bool inclinedOnly,
            bool dirOnly,
            bool pinchInverted,
            float? overrideRadians,
            bool[,] uvRegionMask,
            RectInt? uvRegionMaskBounds = null,
            bool allowUvMaskCache = true)
        {
            if (points == null || points.Count < 2)
                return;

            try
            {
                // マスクを構築（キャッシュ付き）
                byte[] combinedMask = BuildCombinedMaskCached(maskState, uvRegionMask, uvRegionMaskBounds, allowUvMaskCache, out _);
                if (combinedMask != null)
                {
                    furDataManager.SetMask(combinedMask);
                }

                float rad;
                if (overrideRadians.HasValue)
                {
                    rad = overrideRadians.Value;
                }
                else
                {
                    var average = brushManager.GetAverageDirection(points);
                    rad = Mathf.Atan2(average.y, average.x);
                }

                linePointsBuffer.Clear();
                for (var i = 1; i < points.Count; i++)
                {
                    var prev = Vector2Int.RoundToInt(points[i - 1]);
                    var current = Vector2Int.RoundToInt(points[i]);
                    Common.AppendLinePoints(prev.x, prev.y, current.x, current.y, linePointsBuffer);
                }

                furDataManager.UpdateWithMirror(linePointsBuffer, rad, mirrorEnabled, eraserMode, blurMode, pinchMode, inclinedOnly, dirOnly, pinchInverted);
            }
            finally
            {
                // マスクをクリア（キャッシュは保持する）
                furDataManager.ClearMask();
            }
        }

        /// <summary>
        /// maskStateとuvRegionMaskを組み合わせたマスクを構築（キャッシュ付き）
        /// </summary>
        private byte[] BuildCombinedMaskCached(
            State.UvIslandMaskState maskState,
            bool[,] uvRegionMask,
            RectInt? uvRegionMaskBounds,
            bool allowUvMaskCache,
            out bool useCache)
        {
            useCache = false;
            
            bool hasMaskState = maskState != null && maskState.RestrictEditing && 
                                maskState.EffectiveSelected != null && maskState.HasAnySelection;
            bool hasUvMask = uvRegionMask != null;

            if (!hasMaskState && !hasUvMask)
                return null;

            // キャッシュが有効かチェック
            bool cacheValid = allowUvMaskCache &&
                              cachedCombinedMask != null &&
                              maskState != null &&
                              cachedMaskStateVersion == maskState.Version &&
                              cachedUvRegionMask == uvRegionMask &&
                              cachedMaskStateRestrictEditing == maskState.RestrictEditing &&
                              cachedMaskStateHasAnySelection == maskState.HasAnySelection;

            if (cacheValid)
            {
                useCache = true;
                return cachedCombinedMask;
            }

            // キャッシュが無効な場合、新しいマスクを構築
            // 既存のキャッシュがあれば返却
            if (cachedCombinedMask != null)
            {
                ArrayPool<byte>.Shared.Return(cachedCombinedMask);
            }

            // ArrayPoolから配列をレンタル
            cachedCombinedMask = ArrayPool<byte>.Shared.Rent(MaskArraySize);

            // マスクを構築（有効領域のAABBに限定）
            var maskBounds = hasMaskState ? GetActiveBounds(maskState.EffectiveSelected) : null;
            var uvBounds = hasUvMask ? (uvRegionMaskBounds ?? GetActiveBounds(uvRegionMask)) : null;
            var combinedBounds = UnionBounds(maskBounds, uvBounds);

            if (!combinedBounds.HasValue)
            {
                Array.Clear(cachedCombinedMask, 0, MaskArraySize);
                // 有効領域がない場合はマスク不要
                cachedMaskStateVersion = maskState?.Version ?? -1;
                cachedUvRegionMask = allowUvMaskCache ? uvRegionMask : null;
                cachedMaskStateRestrictEditing = maskState?.RestrictEditing ?? false;
                cachedMaskStateHasAnySelection = maskState?.HasAnySelection ?? false;
                useCache = true;
                return cachedCombinedMask;
            }

            var rect = combinedBounds.Value;
            Array.Clear(cachedCombinedMask, 0, MaskArraySize);

            // AABB内のみ走査して書き込み
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                int rowOffset = y * Common.TexSize;
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    bool maskStateAllow = !hasMaskState || maskState.EffectiveSelected[x, y];
                    bool uvMaskAllow = !hasUvMask || uvRegionMask[x, y];
                    
                    if (maskStateAllow && uvMaskAllow)
                    {
                        cachedCombinedMask[rowOffset + x] = 1;
                    }
                }
            }

            // キャッシュ状態を更新
            cachedMaskStateVersion = maskState?.Version ?? -1;
            cachedUvRegionMask = allowUvMaskCache ? uvRegionMask : null;
            cachedMaskStateRestrictEditing = maskState?.RestrictEditing ?? false;
            cachedMaskStateHasAnySelection = maskState?.HasAnySelection ?? false;

            useCache = true; // キャッシュとして保持するのでtrueを返す
            return cachedCombinedMask;
        }

        private static RectInt? GetActiveBounds(bool[,] mask)
        {
            if (mask == null)
                return null;

            int width = Common.TexSize;
            int height = Common.TexSize;
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!mask[x, y])
                        continue;

                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0 || maxY < 0)
                return null;

            return new RectInt(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
        }

        private static RectInt? UnionBounds(RectInt? a, RectInt? b)
        {
            if (a.HasValue && b.HasValue)
            {
                var ra = a.Value;
                var rb = b.Value;
                int minX = Math.Min(ra.xMin, rb.xMin);
                int minY = Math.Min(ra.yMin, rb.yMin);
                int maxX = Math.Max(ra.xMax, rb.xMax);
                int maxY = Math.Max(ra.yMax, rb.yMax);
                return new RectInt(minX, minY, maxX - minX, maxY - minY);
            }
            return a ?? b;
        }

        /// <summary>
        /// 各ポイント間をライン補間して処理（ミラーストローク用）
        /// 元のストロークと同じ密度でブラシを適用する
        /// </summary>
        /// <param name="points">処理するポイント（データ座標系、0～TexSize-1）</param>
        /// <param name="maskState">マスク状態</param>
        /// <param name="eraserMode">消しゴムモードか</param>
        /// <param name="blurMode">ぼかしモードか</param>
        /// <param name="pinchMode">つまむモードか</param>
        /// <param name="inclinedOnly">傾きのみ変更か</param>
        /// <param name="dirOnly">向きのみ変更か</param>
        /// <param name="pinchInverted">つまむモードの反転か</param>
        /// <param name="overrideRadians">ストローク方向（ラジアン）</param>
        /// <param name="uvRegionMask">UV領域マスク。nullの場合は制限なし</param>
        public void ExecuteAtPointsWithLineInterpolation(
            IReadOnlyList<Vector2> points,
            State.UvIslandMaskState maskState,
            bool eraserMode,
            bool blurMode,
            bool pinchMode,
            bool inclinedOnly,
            bool dirOnly,
            bool pinchInverted,
            float overrideRadians,
            bool[,] uvRegionMask,
            RectInt? uvRegionMaskBounds = null,
            bool allowUvMaskCache = true)
        {
            if (points == null || points.Count == 0)
                return;

            try
            {
                // マスクを構築（キャッシュ付き）
                byte[] combinedMask = BuildCombinedMaskCached(maskState, uvRegionMask, uvRegionMaskBounds, allowUvMaskCache, out _);
                if (combinedMask != null)
                {
                    furDataManager.SetMask(combinedMask);
                }

                // ポイント間をライン補間（元のストロークと同じ処理）
                linePointsBuffer.Clear();
                if (points.Count >= 2)
                {
                    for (var i = 1; i < points.Count; i++)
                    {
                        var prev = Vector2Int.RoundToInt(points[i - 1]);
                        var current = Vector2Int.RoundToInt(points[i]);
                        Common.AppendLinePoints(prev.x, prev.y, current.x, current.y, linePointsBuffer);
                    }
                }
                else if (points.Count == 1)
                {
                    var intPoint = Vector2Int.RoundToInt(points[0]);
                    if (intPoint.x >= 0 && intPoint.x < Common.TexSize &&
                        intPoint.y >= 0 && intPoint.y < Common.TexSize)
                    {
                        linePointsBuffer.Add(intPoint);
                    }
                }

                if (linePointsBuffer.Count > 0)
                {
                    // ミラーは呼び出し側で処理済みなので無効
                    furDataManager.UpdateWithMirror(linePointsBuffer, overrideRadians, false, eraserMode, blurMode, pinchMode, inclinedOnly, dirOnly, pinchInverted);
                }
            }
            finally
            {
                // マスクをクリア（キャッシュは保持する）
                furDataManager.ClearMask();
            }
        }

        /// <summary>
        /// リソースを解放する
        /// </summary>
        public void Dispose()
        {
            if (cachedCombinedMask != null)
            {
                ArrayPool<byte>.Shared.Return(cachedCombinedMask);
                cachedCombinedMask = null;
            }
        }
    }
}


