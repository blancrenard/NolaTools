using System.Collections.Generic;
using System.Linq;
using GroomingTool2.Core;
using GroomingTool2.Managers;
using UnityEditor;
using UnityEngine;

namespace GroomingTool2.Services
{
    /// <summary>
    /// 自動設定機能を担当するサービスクラス
    /// 各機能は専用クラスに委譲し、このクラスはコーディネーターとして機能
    /// 
    /// 責務の分離:
    /// - BoneDetector: 耳/尻尾ボーンの自動検出
    /// - BoneDirectionCalculator: Humanoidボーンからの毛方向計算
    /// - VertexDirectionCalculator: 頂点ごとの毛方向計算
    /// - NormalMapGenerator: ノーマルマップテクスチャ生成
    /// </summary>
    internal sealed class AutoSetupService
    {
        private readonly BoneDirectionCalculator boneDirectionCalculator = new BoneDirectionCalculator();
        private readonly VertexDirectionCalculator vertexDirectionCalculator = new VertexDirectionCalculator();
        private readonly NormalMapGenerator normalMapGenerator = new NormalMapGenerator();

        /// <summary>
        /// 選択中マテリアルに対して自動設定を実行する
        /// </summary>
        public bool GenerateAndApplyAutoSetup(
            GameObject avatar,
            MaterialEntry? materialEntry,
            float surfaceLiftAmount,
            float randomnessAmount,
            FurDataManager furDataManager,
            int uvPadding = 4)
        {
            if (avatar == null || !materialEntry.HasValue)
            {
                EditorUtility.DisplayDialog("エラー", "アバターまたはマテリアルが選択されていません。", "OK");
                return false;
            }

            var animator = avatar.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                EditorUtility.DisplayDialog("エラー", "Humanoidアバターが必要です。", "OK");
                return false;
            }

            var entry = materialEntry.Value;

            // MaterialEntryからSkinnedMeshRendererとsubmeshIndexを取得
            var submeshesByRenderer = new Dictionary<SkinnedMeshRenderer, List<int>>();
            foreach (var (renderer, submeshIndex) in entry.usages)
            {
                if (renderer is SkinnedMeshRenderer smr)
                {
                    if (!submeshesByRenderer.ContainsKey(smr))
                    {
                        submeshesByRenderer[smr] = new List<int>();
                    }
                    submeshesByRenderer[smr].Add(submeshIndex);
                }
            }

            if (submeshesByRenderer.Count == 0)
            {
                EditorUtility.DisplayDialog("エラー", "SkinnedMeshRendererが見つかりませんでした。", "OK");
                return false;
            }

            // 対象となるSkinnedMeshRendererのリストを作成
            var skinnedMeshRenderers = submeshesByRenderer.Keys.ToList();

            // ボーン方向を計算
            var boneDirections = boneDirectionCalculator.Calculate(animator, skinnedMeshRenderers);

            // 頂点ごとの毛方向を計算
            var vertexDirections = vertexDirectionCalculator.Calculate(skinnedMeshRenderers, boneDirections, surfaceLiftAmount, animator);

            if (vertexDirections == null || vertexDirections.Length == 0)
            {
                EditorUtility.DisplayDialog("エラー", "毛方向の生成に失敗しました。", "OK");
                return false;
            }

            // ランダム性を適用
            if (randomnessAmount > 0f)
            {
                vertexDirectionCalculator.ApplyRandomness(vertexDirections, skinnedMeshRenderers, randomnessAmount);
            }

            // ノーマルマップを生成
            int textureSize = Common.TexSize;
            var normalMap = normalMapGenerator.Generate(vertexDirections, skinnedMeshRenderers, submeshesByRenderer, textureSize, uvPadding);

            if (normalMap == null)
            {
                EditorUtility.DisplayDialog("エラー", "ノーマルマップの生成に失敗しました。", "OK");
                return false;
            }

            try
            {
                // FurDataManagerに適用
                furDataManager.LoadNormalMap(normalMap);
            }
            finally
            {
                // 一時的なノーマルマップを破棄
                Object.DestroyImmediate(normalMap);
            }

            return true;
        }

        /// <summary>
        /// 耳/尻尾ボーンを自動検出する
        /// </summary>
        public List<Transform> AutoDetectCustomParentBones(GameObject avatarObject)
        {
            return BoneDetector.AutoDetectCustomParentBones(avatarObject);
        }
    }
}
