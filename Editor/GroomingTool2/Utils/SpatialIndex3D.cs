using System.Collections.Generic;
using UnityEngine;

namespace GroomingTool2.Utils
{
    /// <summary>
    /// 3D空間のグリッドベース空間インデックス
    /// 頂点やアイランドの高速検索に使用
    /// </summary>
    /// <typeparam name="T">格納するデータ型</typeparam>
    internal sealed class SpatialIndex3D<T>
    {
        private readonly Dictionary<long, List<T>> grid = new Dictionary<long, List<T>>();
        private readonly float cellSize;

        /// <summary>
        /// 空間インデックスを作成
        /// </summary>
        /// <param name="cellSize">グリッドセルのサイズ</param>
        public SpatialIndex3D(float cellSize)
        {
            this.cellSize = cellSize > 0f ? cellSize : 0.002f; // デフォルト: 2mm
        }

        /// <summary>
        /// アイテムを追加
        /// </summary>
        /// <param name="position">3D位置</param>
        /// <param name="item">格納するアイテム</param>
        public void Add(Vector3 position, T item)
        {
            long key = PositionToKey(position);
            if (!grid.TryGetValue(key, out var list))
            {
                list = new List<T>(4);
                grid[key] = list;
            }
            list.Add(item);
        }

        /// <summary>
        /// 指定位置のセルに含まれるアイテムを取得
        /// </summary>
        /// <param name="position">検索位置</param>
        /// <returns>アイテムリスト（見つからない場合はnull）</returns>
        public List<T> GetAt(Vector3 position)
        {
            long key = PositionToKey(position);
            return grid.TryGetValue(key, out var list) ? list : null;
        }

        /// <summary>
        /// 指定位置とその近傍セルに含まれるアイテムを取得
        /// </summary>
        /// <param name="position">検索位置</param>
        /// <param name="results">結果を格納するリスト（クリアされます）</param>
        public void GetWithNeighbors(Vector3 position, List<T> results)
        {
            results.Clear();
            
            // 中心セル
            long centerKey = PositionToKey(position);
            if (grid.TryGetValue(centerKey, out var centerList))
            {
                results.AddRange(centerList);
            }

            // 近傍26セル
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dy == 0 && dz == 0) continue;

                        Vector3 neighborPos = position + new Vector3(dx * cellSize, dy * cellSize, dz * cellSize);
                        long neighborKey = PositionToKey(neighborPos);
                        
                        if (grid.TryGetValue(neighborKey, out var neighborList))
                        {
                            results.AddRange(neighborList);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 指定位置に最も近いアイテムを検索
        /// </summary>
        /// <param name="position">検索位置</param>
        /// <param name="getPosition">アイテムから位置を取得する関数</param>
        /// <param name="maxDistance">最大検索距離</param>
        /// <param name="result">見つかったアイテム</param>
        /// <param name="foundDistance">見つかったアイテムまでの距離</param>
        /// <returns>見つかった場合true</returns>
        public bool TryFindNearest(
            Vector3 position, 
            System.Func<T, Vector3> getPosition, 
            float maxDistance, 
            out T result, 
            out float foundDistance)
        {
            result = default;
            foundDistance = float.MaxValue;
            bool found = false;

            var candidates = new List<T>();
            GetWithNeighbors(position, candidates);

            foreach (var item in candidates)
            {
                Vector3 itemPos = getPosition(item);
                float dist = Vector3.Distance(itemPos, position);
                
                if (dist <= maxDistance && dist < foundDistance)
                {
                    foundDistance = dist;
                    result = item;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// インデックスをクリア
        /// </summary>
        public void Clear()
        {
            grid.Clear();
        }

        /// <summary>
        /// 3D位置をグリッドキーに変換
        /// </summary>
        private long PositionToKey(Vector3 pos)
        {
            int x = Mathf.RoundToInt(pos.x / cellSize);
            int y = Mathf.RoundToInt(pos.y / cellSize);
            int z = Mathf.RoundToInt(pos.z / cellSize);
            // 3つのintを1つのlongにパック
            return ((long)x << 32) | ((long)(y & 0xFFFF) << 16) | (long)(z & 0xFFFF);
        }

        /// <summary>
        /// グリッドセルのサイズ
        /// </summary>
        public float CellSize => cellSize;

        /// <summary>
        /// 格納されているセル数
        /// </summary>
        public int CellCount => grid.Count;
    }
}
