using System.Collections.Generic;
using GroomingTool2.Managers;
using UnityEngine;

namespace GroomingTool2.Services
{
    /// <summary>
    /// 対称処理の実装
    /// </summary>
    internal sealed class SymmetryService : ISymmetryService
    {
        private readonly VertexSymmetryMapper vertexSymmetryMapper;
        private readonly List<Vector2Int> mirrorBuffer = new List<Vector2Int>(4096);
        // UV→対称UVキャッシュ（キーはピクセル座標を16bitずつにパック）
        private readonly Dictionary<int, Vector2> mirrorUvCache = new Dictionary<int, Vector2>(8192);
        private const int MaxCacheEntries = 32768;

        public SymmetryService(VertexSymmetryMapper mapper)
        {
            vertexSymmetryMapper = mapper;
        }

        public List<Vector2Int> GetMirrorPoints(IReadOnlyList<Vector2Int> points, out bool allMirrored)
        {
            mirrorBuffer.Clear();
            allMirrored = true;

            foreach (var point in points)
            {
                // UV座標を0-1の範囲に正規化
                Vector2 uv = new Vector2(
                    point.x / (float)Core.Common.TexSize,
                    1.0f - (point.y / (float)Core.Common.TexSize)
                );

                // キャッシュキー（ピクセル座標をパック）
                int key = (point.x << 16) ^ (point.y & 0xFFFF);
                Vector2 symmetricUV;
                if (!mirrorUvCache.TryGetValue(key, out symmetricUV))
                {
                    // 対称UV座標を取得（バリセン補間）
                    if (!vertexSymmetryMapper.TryGetSymmetricUVBarycentric(uv, out symmetricUV))
                    {
                        allMirrored = false;
                        continue;
                    }
                    if (mirrorUvCache.Count > MaxCacheEntries)
                        mirrorUvCache.Clear();
                    mirrorUvCache[key] = symmetricUV;
                }

                // 0-1の範囲からテクスチャ座標に変換
                Vector2Int symmetricPoint = new Vector2Int(
                    Mathf.RoundToInt(symmetricUV.x * Core.Common.TexSize),
                    Mathf.RoundToInt((1.0f - symmetricUV.y) * Core.Common.TexSize)
                );

                // 範囲チェック
                if (symmetricPoint.x >= 0 && symmetricPoint.x < Core.Common.TexSize &&
                    symmetricPoint.y >= 0 && symmetricPoint.y < Core.Common.TexSize)
                {
                    // 重複除去せず、元のストロークと同数のサンプル密度を維持
                    mirrorBuffer.Add(symmetricPoint);
                }
                else
                {
                    allMirrored = false;
                }
            }

            return mirrorBuffer;
        }
    }
}
