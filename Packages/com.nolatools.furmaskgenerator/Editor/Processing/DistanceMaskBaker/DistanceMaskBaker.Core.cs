#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Data;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator
{
    /// <summary>
    /// Bakes distance masks for fur generation
    /// Handles mesh processing, distance calculations, UV islands, and smoothing
    /// </summary>
    public partial class DistanceMaskBaker
    {
        #region Core Fields

        // Input settings
        private readonly DistanceBakerSettings settings;

        // Baking process state
        private List<Vector3> verts;
        private List<Vector3> norms;
        private List<Vector2> uvs;
        private List<(int[] tri, string mat)> subDatas;
        private List<string> subRendererPaths;
        private List<int> subMeshIndices;
        private List<float> boneMaskValues; // 頂点ごとのボーンコントロール値（0=無効、1=強マスク）
        private Dictionary<Transform, float> boneMaskMap; // 保持されたTransform→マスク値のマッピング

        // Normal map related fields
        private Dictionary<string, MaterialNormalMapData> normalMapCache; // materialName -> normal map data
        private Dictionary<int, string> vertexToMaterialName; // vertexIndex -> materialName mapping
        private List<Vector4> tangents; // 頂点ごとのTangentデータ
        // 自動判別のキャッシュ
        private Dictionary<string, bool> cachedIsPackedAG; // materialName -> isPackedAG
        // G(Y)反転は使用しない方針のためキャッシュ廃止

        private GameObject clothColliderObject;
        private MeshCollider clothCollider;
        private float maxM;
        private float[] vDist;
        private int texSize, curr, batchSize;
        private float[] smoothBuffer1, smoothBuffer2;
        
        // 進捗更新最適化用
        private int progressUpdateInterval;
        private float lastProgressBarUpdate;
        
        // レイキャスト最適化用
        private float maxMSquared;
        private Vector3[] rayDirections;
        private float[] cachedDistances;
        private bool[] islandAnchorFlags; // UV島の頂点はスムージングで固定
        private List<Mesh> createdMeshes = new();
        private Dictionary<string, Texture2D> preview = new();
        private bool originalQueriesHitBackfaces;
        private bool cancelRequested;

        #endregion

        #region Constructor and Lifecycle

        public DistanceMaskBaker(DistanceBakerSettings settings)
        {
            this.settings = settings;
        }

        public void StartBake()
        {
            try
            {
                originalQueriesHitBackfaces = Physics.queriesHitBackfaces;
                Physics.queriesHitBackfaces = true;
                cancelRequested = false;
                Prepare();
                if (cancelRequested) { return; }
                EditorApplication.update += BakeStep;
            }
            catch (System.Exception e)
            {
                Debug.LogError(string.Format(ErrorMessages.ERROR_BAKE_PROCESS, e.Message));
                Cancel();
            }
        }

        /// <summary>
        /// 共通のクリーンアップ処理を実行
        /// </summary>
        private void PerformCommonCleanup()
        {
            Physics.queriesHitBackfaces = originalQueriesHitBackfaces;
            CleanupCreatedMeshes();
            CleanupClothCollider();
        }

        private void Cancel()
        {
            EditorCoreUtils.ClearProgress();
            EditorApplication.update -= BakeStep;
            PerformCommonCleanup();
            settings.OnCancelled?.Invoke();
        }

        private void Finish()
        {
            try
            {
                var textureProcessor = new TextureProcessor(
                    verts,
                    uvs,
                    subDatas,
                    vDist,
                    texSize,
                    1.0f,
                    settings.UVIslandMasks,
                    subRendererPaths,
                    subMeshIndices,
                    settings.UvIslandVertexSmoothIterations,
                    settings.UseTransparentMode
                );
                preview = textureProcessor.CreateFinalTextures();

                PerformCommonCleanup();
                settings.OnCompleted?.Invoke(preview);
            }
            catch (System.Exception e)
            {
                Debug.LogError(string.Format(ErrorMessages.ERROR_COMPLETE_PROCESS, e.Message));
                PerformCommonCleanup();
            }
        }

        private void CleanupCreatedMeshes()
        {
            foreach (var mesh in createdMeshes)
            {
                if (mesh != null)
                {
                    EditorObjectUtils.SafeDestroy(mesh);
                }
            }
            createdMeshes.Clear();
        }

        private void CleanupClothCollider()
        {
            if (clothCollider != null)
            {
                EditorObjectUtils.SafeDestroy(clothCollider);
                clothCollider = null;
            }
            if (clothColliderObject != null)
            {
                EditorObjectUtils.SafeDestroy(clothColliderObject);
                clothColliderObject = null;
            }
        }

        #endregion
    }
}
#endif


