#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Data;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator
{
    public partial class FurMaskGenerator
    {
        /// <summary>
        /// UVアイランドキャッシュを取得または作成（LRU最適化版）
        /// </summary>
        private UVIslandCache GetOrCreateUVIslandCache(string cacheKey, Mesh mesh, int submeshIndex, Renderer renderer)
        {
            try
            {
                // 既存キャッシュの確認
                if (_uvIslandCache.TryGetValue(cacheKey, out var existingCache) && existingCache.IsValid(mesh))
                {
                    // LRU: アクセス順序を更新
                    UpdateCacheAccessOrder(cacheKey);
                    existingCache.lastAccessed = DateTime.Now;
                    return existingCache;
                }

                // メモリ制限チェックとLRU削除
                EnsureCacheMemoryLimit();

                // 新しいキャッシュを作成
                var newCache = BuildUVIslandCache(mesh, submeshIndex, renderer);
                if (newCache != null)
                {
                    newCache.rendererPath = cacheKey.Split('_')[0];
                    newCache.submeshIndex = submeshIndex;
                    newCache.lastAccessed = DateTime.Now;
                    newCache.CalculateMemoryUsage();
                    
                    _uvIslandCache[cacheKey] = newCache;
                    _cacheAccessOrder.AddLast(cacheKey);
                }

                return newCache;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"UVアイランドキャッシュ作成中にエラーが発生しました: {ex}");
                return null;
            }
        }

        /// <summary>
        /// キャッシュのアクセス順序を更新（LRU用）
        /// </summary>
        private void UpdateCacheAccessOrder(string cacheKey)
        {
            // 既存のノードを削除
            var node = _cacheAccessOrder.Find(cacheKey);
            if (node != null)
            {
                _cacheAccessOrder.Remove(node);
            }
            
            // 最後尾に追加（最新アクセス）
            _cacheAccessOrder.AddLast(cacheKey);
        }

        /// <summary>
        /// キャッシュのメモリ制限を確保（LRU + メモリ使用量ベース）
        /// </summary>
        private void EnsureCacheMemoryLimit()
        {
            // サイズ制限チェック
            while (_uvIslandCache.Count >= MAX_CACHE_SIZE)
            {
                RemoveOldestCacheEntry();
            }

            // メモリ使用量制限チェック
            long totalMemoryUsage = CalculateTotalCacheMemoryUsage();
            long maxMemoryBytes = MAX_MEMORY_USAGE_MB * 1024 * 1024;
            
            while (totalMemoryUsage > maxMemoryBytes && _uvIslandCache.Count > 0)
            {
                RemoveOldestCacheEntry();
                totalMemoryUsage = CalculateTotalCacheMemoryUsage();
            }
        }

        /// <summary>
        /// 最も古いキャッシュエントリを削除（LRU）
        /// </summary>
        private void RemoveOldestCacheEntry()
        {
            if (_cacheAccessOrder.Count == 0) return;
            
            var oldestKey = _cacheAccessOrder.First.Value;
            _cacheAccessOrder.RemoveFirst();
            
            if (_uvIslandCache.TryGetValue(oldestKey, out var cache))
            {
                _uvIslandCache.Remove(oldestKey);
            }
        }

        /// <summary>
        /// 総キャッシュメモリ使用量を計算
        /// </summary>
        private long CalculateTotalCacheMemoryUsage()
        {
            long total = 0;
            foreach (var cache in _uvIslandCache.Values)
            {
                total += cache.memoryUsageBytes;
            }
            return total;
        }

        /// <summary>
        /// UVアイランドキャッシュを構築
        /// </summary>
        private UVIslandCache BuildUVIslandCache(Mesh mesh, int submeshIndex, Renderer renderer)
        {
            if (mesh == null || submeshIndex < 0 || submeshIndex >= mesh.subMeshCount)
                return null;

            var cache = new UVIslandCache
            {
                meshVertexCount = mesh.vertexCount,
                meshTriangleCount = mesh.triangles?.Length / 3 ?? 0,
                triangleToIslandMap = new Dictionary<int, HashSet<int>>(),
                uvToTriangleMap = new Dictionary<Vector2, int>()
            };

            try
            {
                int[] triangles = mesh.GetTriangles(submeshIndex);
                Vector2[] uvs = mesh.uv;
                if (triangles == null || uvs == null) return null;

                int triangleCount = triangles.Length / 3;
                var processedTriangles = new bool[triangleCount];
                var adjacency = NolaTools.FurMaskGenerator.Utils.EditorUvUtils.BuildTriangleAdjacencyListList(triangles);

                // UV->三角形マッピングを事前構築（高速検索用）
                for (int ti = 0; ti < triangleCount; ti++)
                {
                    int baseIdx = ti * 3;
                    if (baseIdx + 2 >= triangles.Length) continue;

                    int a = triangles[baseIdx], b = triangles[baseIdx + 1], c = triangles[baseIdx + 2];
                    if (a >= uvs.Length || b >= uvs.Length || c >= uvs.Length) continue;

                    Vector2 centroid = (uvs[a] + uvs[b] + uvs[c]) / 3f;
                    // UV座標の精度を落として高速検索用マップに追加
                    Vector2 quantizedUV = new Vector2(
                        Mathf.Round(centroid.x * 100f) / 100f,
                        Mathf.Round(centroid.y * 100f) / 100f
                    );
                    
                    if (!cache.uvToTriangleMap.ContainsKey(quantizedUV))
                    {
                        cache.uvToTriangleMap[quantizedUV] = ti;
                    }
                }

                // UVアイランドを順次構築
                for (int startTriangle = 0; startTriangle < triangleCount; startTriangle++)
                {
                    if (processedTriangles[startTriangle]) continue;

                    var islandTriangles = new HashSet<int>();
                    var stack = new Stack<int>();
                    
                    stack.Push(startTriangle);
                    processedTriangles[startTriangle] = true;

                    while (stack.Count > 0)
                    {
                        int currentTriangle = stack.Pop();
                        islandTriangles.Add(currentTriangle);

                        if (currentTriangle < adjacency.Count)
                        {
                            foreach (int neighborTriangle in adjacency[currentTriangle])
                            {
                                if (!processedTriangles[neighborTriangle] && 
                                    NolaTools.FurMaskGenerator.Utils.EditorUvUtils.AreUVTrianglesConnected(triangles, uvs, currentTriangle, neighborTriangle, 0.1f))
                                {
                                    processedTriangles[neighborTriangle] = true;
                                    stack.Push(neighborTriangle);
                                }
                            }
                        }
                    }

                    // 同じUVアイランドの全三角形に同じ島データを割り当て
                    foreach (int tri in islandTriangles)
                    {
                        cache.triangleToIslandMap[tri] = islandTriangles;
                    }
                }

                return cache;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"UVアイランドキャッシュ構築中にエラーが発生しました: {ex}");
                return null;
            }
        }

        /// <summary>
        /// キャッシュからUV座標を含む三角形を高速検索
        /// </summary>
        private int FindTriangleContainingUV(UVIslandCache cache, Vector2 uv)
        {
            if (cache?.uvToTriangleMap == null) return -1;

            // 量子化されたUV座標で高速検索
            Vector2 quantizedUV = new Vector2(
                Mathf.Round(uv.x * 100f) / 100f,
                Mathf.Round(uv.y * 100f) / 100f
            );

            if (cache.uvToTriangleMap.TryGetValue(quantizedUV, out int triangleIndex))
            {
                return triangleIndex;
            }

            // 完全一致しない場合は近傍検索
            float bestDistance = float.MaxValue;
            int bestTriangle = -1;

            foreach (var kvp in cache.uvToTriangleMap)
            {
                float distance = Vector2.Distance(kvp.Key, uv);
                if (distance < bestDistance && distance < 0.01f) // 1%以内の近傍
                {
                    bestDistance = distance;
                    bestTriangle = kvp.Value;
                }
            }

            return bestTriangle;
        }

        /// <summary>
        /// 指定UV座標が含まれるUVアイランドの三角形集合を取得
        /// </summary>
        private HashSet<int> GetUVIslandTriangles(Mesh mesh, int submeshIndex, Vector2 seedUV)
        {
            var result = new HashSet<int>();
            if (mesh == null || submeshIndex < 0 || submeshIndex >= mesh.subMeshCount)
                return result;

            try
            {
                // サブメッシュの三角形を取得
                int[] triangles = mesh.GetTriangles(submeshIndex);
                if (triangles == null || triangles.Length == 0)
                    return result;

                Vector2[] uvs = mesh.uv;
                if (uvs == null || uvs.Length != mesh.vertexCount)
                    return result;

                // シード三角形を見つける
                int seedTriangle = NolaTools.FurMaskGenerator.Utils.EditorUvUtils.FindSeedTriangleByUV(triangles, uvs, seedUV);
                if (seedTriangle < 0)
                    return result;

                // 隣接関係を構築
                var adjacency = NolaTools.FurMaskGenerator.Utils.EditorUvUtils.BuildTriangleAdjacencyListList(triangles);
                
                // フラッドフィルでUVアイランドを展開
                var visited = new bool[triangles.Length / 3];
                var stack = new Stack<int>();
                
                stack.Push(seedTriangle);
                visited[seedTriangle] = true;

                while (stack.Count > 0)
                {
                    int currentTriangle = stack.Pop();
                    result.Add(currentTriangle);

                    if (currentTriangle < adjacency.Count)
                    {
                        foreach (int neighborTriangle in adjacency[currentTriangle])
                        {
                            if (!visited[neighborTriangle] && 
                                NolaTools.FurMaskGenerator.Utils.EditorUvUtils.AreUVTrianglesConnected(triangles, uvs, currentTriangle, neighborTriangle, 0.1f))
                            {
                                visited[neighborTriangle] = true;
                                stack.Push(neighborTriangle);
                            }
                        }
                    }
                }

                return result;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"UVアイランド取得中にエラーが発生しました: {ex}");
                return result;
            }
        }

        private int DetermineSubmeshIndex(Mesh mesh, int triangleIndex)
        {
            if (mesh == null || triangleIndex < 0) return 0;
            int running = 0;
            int subCount = mesh.subMeshCount;
            for (int i = 0; i < subCount; i++)
            {
                var tris = mesh.GetTriangles(i);
                int triCount = tris != null ? tris.Length / 3 : 0;
                if (triangleIndex < running + triCount)
                {
                    return i;
                }
                running += triCount;
            }
            return Mathf.Clamp(subCount - 1, 0, int.MaxValue);
        }

        private string TryGetMaterialName(Renderer renderer, int submeshIndex)
        {
            if (renderer == null) return null;
            var mats = renderer.sharedMaterials;
            if (mats == null || mats.Length == 0) return null;
            int idx = Mathf.Clamp(submeshIndex, 0, mats.Length - 1);
            return mats[idx] != null ? mats[idx].name : null;
        }

        /// <summary>
        /// UVアイランドキャッシュをクリア（メッシュ変更時やアバター切り替え時に呼び出し）
        /// </summary>
        private static void ClearUVIslandCache()
        {
            _uvIslandCache.Clear();
        }

        /// <summary>
        /// 特定のレンダラーに関連するキャッシュをクリア
        /// </summary>
        private static void ClearUVIslandCacheForRenderer(string rendererPath)
        {
            var keysToRemove = _uvIslandCache.Keys.Where(key => key.StartsWith(rendererPath + "_")).ToList();
            foreach (var key in keysToRemove)
            {
                _uvIslandCache.Remove(key);
            }
        }
    }
}

#endif
