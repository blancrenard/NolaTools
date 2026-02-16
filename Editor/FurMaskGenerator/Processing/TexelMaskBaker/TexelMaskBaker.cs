#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Data;
using NolaTools.FurMaskGenerator.Constants;
using UnityEngine.Rendering;

namespace NolaTools.FurMaskGenerator
{
    /// <summary>
    /// テクセルベースのマスクベイカー
    /// UV空間のピクセル単位で距離計算を行い、頂点密度に依存しないマスクを生成する
    /// </summary>
    public partial class TexelMaskBaker
    {
        #region コアフィールド

        private readonly TexelBakerSettings settings;

        // メッシュデータ
        private List<Vector3> verts;
        private List<Vector3> norms;
        private List<Vector2> uvs;
        private List<(int[] tri, string mat)> subDatas;
        private List<string> subRendererPaths;
        private List<int> subMeshIndices;
        private List<float> boneMaskValues;
        private Dictionary<Transform, float> boneMaskMap;
        private List<Vector4> tangents;

        // ノーマルマップ関連
        private Dictionary<string, MaterialNormalMapData> normalMapCache;
        private Dictionary<int, string> vertexToMaterialName;
        private Dictionary<string, bool> cachedIsPackedAG;

        // コライダー
        private GameObject clothColliderObject;
        private MeshCollider clothCollider;
        private List<Mesh> createdMeshes = new();

        // テクスチャ処理
        private int texSize;
        private float maxM;
        private float maxMSquared;

        // テクセルベイク用
        private Dictionary<string, Color[]> materialBuffers;
        private Dictionary<string, HashSet<int>> materialRasterizedPixels;
        private int totalTexelsToProcess;
        private int processedTexels;
        private int currentSubIndex;
        private int currentTriIndex;
        private int batchSize;

        // 進捗管理
        private int progressUpdateInterval;
        private float lastProgressBarUpdate;
        private bool cancelRequested;
        private bool originalQueriesHitBackfaces;

        // レイキャスト最適化用
        private Vector3[] rayDirections;

        // プレビュー結果
        private Dictionary<string, Texture2D> preview = new();

        // **NEW** 一時テクスチャ（読み取り不能なノーマルマップの対策用）
        private List<Texture2D> temporaryTextures = new();

        #endregion

        #region コンストラクタとライフサイクル

        public TexelMaskBaker(TexelBakerSettings settings)
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

        private void Cancel()
        {
            EditorCoreUtils.ClearProgress();
            EditorApplication.update -= BakeStep;
            PerformCommonCleanup();
            settings.OnCancelled?.Invoke();
        }

        private void PerformCommonCleanup()
        {
            Physics.queriesHitBackfaces = originalQueriesHitBackfaces;
            CleanupCreatedMeshes();
            CleanupClothCollider();
            CleanupTemporaryTextures(); // **NEW**
        }

        private void Finish()
        {
            try
            {
                // マテリアルバッファからテクスチャ生成
                var finalPreview = BuildFinalTextures();
                if (finalPreview == null)
                {
                    PerformCommonCleanup();
                    settings.OnCancelled?.Invoke();
                    return;
                }

                PerformCommonCleanup();
                settings.OnCompleted?.Invoke(finalPreview);
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

        private void CleanupTemporaryTextures()
        {
            if (temporaryTextures != null)
            {
                foreach (var tex in temporaryTextures)
                {
                    if (tex != null)
                    {
                        EditorObjectUtils.SafeDestroy(tex);
                    }
                }
                temporaryTextures.Clear();
            }
        }

        #endregion
    }
}
#endif
