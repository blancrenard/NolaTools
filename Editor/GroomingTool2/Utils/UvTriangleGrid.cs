using System.Collections.Generic;
using UnityEngine;

namespace GroomingTool2.Utils
{
    /// <summary>
    /// UV空間の三角形をグリッドで管理するクラス
    /// UV座標から所属する三角形を高速に検索可能
    /// </summary>
    internal sealed class UvTriangleGrid
    {
        /// <summary>
        /// グリッド解像度（デフォルト: 64x64）
        /// </summary>
        public const int DefaultResolution = 64;

        private readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
        private readonly int resolution;
        private readonly float cellSize;

        /// <summary>
        /// UV空間のAABB
        /// </summary>
        public Vector2 UvMin { get; private set; } = new Vector2(float.MaxValue, float.MaxValue);
        public Vector2 UvMax { get; private set; } = new Vector2(float.MinValue, float.MinValue);

        /// <summary>
        /// UV三角形グリッドを作成
        /// </summary>
        /// <param name="resolution">グリッド解像度（デフォルト: 64）</param>
        public UvTriangleGrid(int resolution = DefaultResolution)
        {
            this.resolution = resolution;
            this.cellSize = 1f / resolution;
        }

        /// <summary>
        /// 三角形を追加（三角形インデックスはtriangles配列の開始位置）
        /// </summary>
        /// <param name="uvA">頂点AのUV</param>
        /// <param name="uvB">頂点BのUV</param>
        /// <param name="uvC">頂点CのUV</param>
        /// <param name="triangleStartIndex">triangles配列での開始インデックス（0, 3, 6, ...）</param>
        public void AddTriangle(Vector2 uvA, Vector2 uvB, Vector2 uvC, int triangleStartIndex)
        {
            // UV AABBを更新
            UvMin = Vector2.Min(UvMin, uvA);
            UvMin = Vector2.Min(UvMin, uvB);
            UvMin = Vector2.Min(UvMin, uvC);
            UvMax = Vector2.Max(UvMax, uvA);
            UvMax = Vector2.Max(UvMax, uvB);
            UvMax = Vector2.Max(UvMax, uvC);

            // 三角形のAABBを計算
            float minX = Mathf.Min(uvA.x, Mathf.Min(uvB.x, uvC.x));
            float maxX = Mathf.Max(uvA.x, Mathf.Max(uvB.x, uvC.x));
            float minY = Mathf.Min(uvA.y, Mathf.Min(uvB.y, uvC.y));
            float maxY = Mathf.Max(uvA.y, Mathf.Max(uvB.y, uvC.y));

            // AABBが交差するグリッドセルに登録
            int gx0 = Mathf.Clamp(Mathf.FloorToInt(minX / cellSize), 0, resolution - 1);
            int gx1 = Mathf.Clamp(Mathf.FloorToInt(maxX / cellSize), 0, resolution - 1);
            int gy0 = Mathf.Clamp(Mathf.FloorToInt(minY / cellSize), 0, resolution - 1);
            int gy1 = Mathf.Clamp(Mathf.FloorToInt(maxY / cellSize), 0, resolution - 1);

            for (int gx = gx0; gx <= gx1; gx++)
            {
                for (int gy = gy0; gy <= gy1; gy++)
                {
                    long key = ((long)gx << 32) | (uint)gy;
                    if (!grid.TryGetValue(key, out var list))
                    {
                        list = new List<int>(4);
                        grid[key] = list;
                    }
                    list.Add(triangleStartIndex);
                }
            }
        }

        /// <summary>
        /// 指定UV座標を含む可能性のある三角形の候補リストを取得
        /// </summary>
        /// <param name="uv">検索するUV座標</param>
        /// <returns>三角形開始インデックスのリスト（見つからない場合はnullまたは空）</returns>
        public List<int> GetCandidates(Vector2 uv)
        {
            // 中心セルをチェック
            int gx = Mathf.Clamp(Mathf.FloorToInt(uv.x / cellSize), 0, resolution - 1);
            int gy = Mathf.Clamp(Mathf.FloorToInt(uv.y / cellSize), 0, resolution - 1);
            long key = ((long)gx << 32) | (uint)gy;
            
            if (grid.TryGetValue(key, out var list) && list != null && list.Count > 0)
            {
                return list;
            }

            // 近傍セルもチェック（境界付近の精度向上）
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    
                    int ngx = gx + dx;
                    int ngy = gy + dy;
                    if (ngx < 0 || ngx >= resolution || ngy < 0 || ngy >= resolution) continue;
                    
                    long nkey = ((long)ngx << 32) | (uint)ngy;
                    if (grid.TryGetValue(nkey, out var nlist) && nlist != null && nlist.Count > 0)
                    {
                        return nlist;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 指定UV座標がこのグリッドのAABB内にあるかチェック
        /// </summary>
        /// <param name="uv">チェックするUV座標</param>
        /// <param name="margin">マージン（デフォルト: 1e-4f）</param>
        /// <returns>AABB内の場合true</returns>
        public bool IsInBounds(Vector2 uv, float margin = 1e-4f)
        {
            return uv.x >= UvMin.x - margin && uv.x <= UvMax.x + margin &&
                   uv.y >= UvMin.y - margin && uv.y <= UvMax.y + margin;
        }

        /// <summary>
        /// グリッドをクリア
        /// </summary>
        public void Clear()
        {
            grid.Clear();
            UvMin = new Vector2(float.MaxValue, float.MaxValue);
            UvMax = new Vector2(float.MinValue, float.MinValue);
        }

        /// <summary>
        /// グリッドが空かどうか
        /// </summary>
        public bool IsEmpty => grid.Count == 0;

        /// <summary>
        /// グリッド解像度
        /// </summary>
        public int Resolution => resolution;

        /// <summary>
        /// セルサイズ
        /// </summary>
        public float CellSize => cellSize;
    }
}
