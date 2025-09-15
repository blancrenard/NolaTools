#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Data;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator
{
    /// <summary>
    /// Processes textures for fur mask generation
    /// Handles UV islands, rasterization, spatial operations, and texture output
    /// </summary>
    public partial class TextureProcessor
    {
        #region Core Fields

        private readonly List<Vector3> verts;
        private readonly List<Vector2> uvs;
        private readonly List<(int[] tri, string mat)> subDatas;
        private readonly float[] vDist;
        private readonly int texSize;
        private readonly float gamma;
        private readonly List<UVIslandMaskData> uvIslandMasks;
        // サブメッシュ同定のための追加メタ情報（DistanceMaskBaker から渡す前提）
        private readonly List<string> subRendererPaths;
        private readonly List<int> subMeshIndices;
        private readonly bool useTransparentMode; // 透過モード設定

        // subIndex -> (matKey, textureListIndex) の対応マップ
        private readonly Dictionary<int, (string mat, int texIdx)> subIndexToTexLocator = new();
        
        // 真のUVカバレッジ（三角形ラスタライズで描画された画素）を記録
        private readonly Dictionary<string, HashSet<int>> materialToRasterizedPixels = new();

        // 近接メッシュのための空間探索（レイ判定統合により廃止）
        
        // ラスタライズ最適化用
        private int adaptiveProgressInterval;

        #endregion

        #region Constructor

        public TextureProcessor(List<Vector3> verts, List<Vector2> uvs, List<(int[] tri, string mat)> subDatas, float[] vDist, int texSize, float gamma, List<UVIslandMaskData> uvIslandMasks = null, List<string> subRendererPaths = null, List<int> subMeshIndices = null, int vertexSmoothIterations = 1, bool useTransparentMode = false)
        {
            this.verts = verts;
            this.uvs = uvs;
            this.subDatas = subDatas;
            this.vDist = vDist;
            this.texSize = texSize;
            this.gamma = gamma;
            this.uvIslandMasks = uvIslandMasks ?? new List<UVIslandMaskData>();
            this.subRendererPaths = subRendererPaths ?? new List<string>();
            this.subMeshIndices = subMeshIndices ?? new List<int>();
            this.useTransparentMode = useTransparentMode;
            
            // ラスタライズ最適化の初期化
            CalculateAdaptiveProgressInterval();
        }

        #endregion

        #region Public Interface

        public Dictionary<string, Texture2D> CreateFinalTextures()
        {
            if (uvs == null || verts == null || uvs.Count != verts.Count)
            {
                EditorUtility.DisplayDialog(UILabels.ERROR_DIALOG_TITLE, ErrorMessages.ERROR_UV_NOT_FOUND, UILabels.ERROR_DIALOG_OK);
                return new Dictionary<string, Texture2D>();
            }
            if (EditorCoreUtils.ShowCancelableProgressAutoClear(UILabels.PROGRESS_BAR_TITLE, UILabels.PROGRESS_RASTERIZING_START_JP, 0.62f)) return null;
            var matTex = CreateMaterialTextures();
            if (EditorCoreUtils.ShowCancelableProgressAutoClear(UILabels.PROGRESS_BAR_TITLE, UILabels.RASTERIZING_LABEL, 0.7f)) return null;
            // UV島の直接塗りはレイ判定統合に移行したため無効化
            if (EditorCoreUtils.ShowCancelableProgressAutoClear(UILabels.PROGRESS_BAR_TITLE, UILabels.DILATING_LABEL, 0.9f)) return null;
            var finalPreview = MergeSubTexturesPerMaterial(matTex);
            EditorCoreUtils.ClearProgress();
            return finalPreview;
        }

        public static void SaveTex(Texture2D tex, string m)
        {
            string defaultName = $"{GameObjectConstants.LENGTH_MASK_PREFIX}{m}";
            FileDialogUtils.SaveTexturePNG(tex, defaultName, UILabels.MASK_SAVE_BUTTON);
        }

        #endregion
    }
}
#endif
