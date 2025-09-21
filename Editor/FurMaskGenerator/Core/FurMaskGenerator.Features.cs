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
        // Auto-detection methods
        private void AutoDetectRenderers()
        {
            avatarRenderers.Clear();
            clothRenderers.Clear();
            if (avatarObject == null) return;

            // 古い実装のRendererDetectionUtilsを使用してレンダラーを分類
            RendererDetectionUtils.PartitionAvatarAndClothRenderers(avatarObject, avatarRenderers, clothRenderers);
        }

        /// <summary>
        /// 現在シーンで有効かつ描画されるレンダラーのみをリストに追加する
        /// 押下時に各リストは一度クリアされる
        /// </summary>
        private void AddVisibleRenderers()
        {
            avatarRenderers.Clear();
            clothRenderers.Clear();
            if (avatarObject == null) return;

            var allRenderers = avatarObject.GetComponentsInChildren<Renderer>(true);
            foreach (var r in allRenderers)
            {
                if (r == null || r.gameObject == null) continue;
                // 表示と描画対象の簡易判定: activeInHierarchy, enabled, alpha等は考慮せず
                if (!r.gameObject.activeInHierarchy) continue;
                if (!r.enabled) continue;
                if (!r.isVisible) continue; // シーンビュー/ゲームビューいずれかで可視

                string lowerName = r.gameObject.name.ToLowerInvariant();
                if (EditorNameFilters.IsAvatarRendererCandidate(lowerName))
                {
                    avatarRenderers.Add(r);
                }
                else
                {
                    clothRenderers.Add(r);
                }
            }
        }

        // Bake methods
        void StartBake()
        {
            // マスク生成中にClothCollider作成によるhierarchyChangedイベントを無視するフラグを設定
            ignoreHierarchyChangeDuringBake = true;

            preview.Clear();
            if (!EditorCoreUtils.ValidateNonEmptyRenderers(avatarRenderers, UILabels.ERROR_DIALOG_TITLE, ErrorMessages.ERROR_AVATAR_RENDERERS_INVALID, UILabels.ERROR_DIALOG_OK))
            {
                // 検証失敗時はフラグをクリア
                ignoreHierarchyChangeDuringBake = false;
                return;
            }
            // UIの値に0.001を加算して内部計算に使用
            float internalMaxDistance = settings.maxDistance + AppSettings.POSITION_PRECISION;
            // ベイク時のみ固定値を適用（恒久設定は変更しない）
            const int tempSubdivisionIterations = 1;
            
            var bakerSettings = new DistanceBakerSettings(
                avatarRenderers,
                clothRenderers,
                settings.sphereMasks.Select(s => s.Clone()).ToList(),
                new List<UVIslandMaskData>(settings.uvIslandMasks),
                new List<BoneMaskData>(settings.boneMasks),
                new List<MaterialNormalMapData>(settings.materialNormalMaps),
                settings.textureSizeIndex,
                internalMaxDistance,
                settings.gamma,
                tempSubdivisionIterations,
                settings.uvIslandNeighborRadius,
                settings.uvIslandVertexSmoothIterations,
                settings.useTransparentMode,
                OnBakeCompleted,
                OnBakeCancelled
            );
            foreach (var s in bakerSettings.SphereMasks)
            {
                if (s != null) s.radius = Mathf.Min(s.radius, AppSettings.SHOW_MAX_RADIUS);
            }
            currentBaker = new DistanceMaskBaker(bakerSettings);
            currentBaker.StartBake();
        }

        private void OnBakeCompleted(Dictionary<string, Texture2D> result)
        {
            preview = result;
            currentBaker = null;

            // ベイク完了後にフラグをクリア
            ignoreHierarchyChangeDuringBake = false;

            Repaint();
        }

        private void OnBakeCancelled()
        {
            currentBaker = null;

            // ベイクキャンセル後にフラグをクリア
            ignoreHierarchyChangeDuringBake = false;

            Repaint();
        }

        bool ValidateInputs()
        {
            if (avatarRenderers.All(r => r == null))
            {
                EditorUtility.DisplayDialog(UILabels.ERROR_DIALOG_TITLE, ErrorMessages.ERROR_AVATAR_RENDERERS_INVALID, UILabels.ERROR_DIALOG_OK);
                return false;
            }
            return true;
        }

        /// <summary>
        /// ノーマルマップからファーの傾き設定を自動設定（ファーの長さも自動設定）
        /// </summary>
        private void AutoSetHairTiltFromFurNormalMaps()
        {
            if (settings == null) return;

            // データ初期化
            if (settings.materialNormalMaps == null)
            {
                settings.materialNormalMaps = new List<MaterialNormalMapData>();
            }

            // 対象レンダラーを取得
            var targetRenderers = new List<Renderer>();
            targetRenderers.AddRange(avatarRenderers);
            targetRenderers.AddRange(clothRenderers);

            if (targetRenderers.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    UILabels.ERROR_DIALOG_TITLE, 
                    "レンダラーが設定されていません。先にアバターとレンダラーを設定してください。", 
                    UILabels.ERROR_DIALOG_OK);
                return;
            }

            // ファーノーマルマップを取得
            var furNormalMaps = FurNormalMapUtils.GetFurNormalMapsFromRenderers(targetRenderers);
            
            bool hasNormalMaps = furNormalMaps.Count > 0;
            
            if (hasNormalMaps)
            {
                // 既存の設定をクリア（オプション：既存設定を保持したい場合はコメントアウト）
                settings.materialNormalMaps.Clear();

                // ファーノーマルマップをMaterialNormalMapDataに変換
                var newNormalMapData = FurNormalMapUtils.ConvertToMaterialNormalMapData(furNormalMaps, targetRenderers);
                
                // 設定に追加
                foreach (var normalMapData in newNormalMapData)
                {
                    settings.materialNormalMaps.Add(normalMapData);
                }
            }

            // ファーの長さを自動設定（ノーマルマップが無くても実行）
            float optimalFurLength = FurNormalMapUtils.GetOptimalFurLength(targetRenderers);
            
            // 結果に応じたメッセージ表示
            if (hasNormalMaps)
            {
                // Undo対応でファーの傾きと長さを設定
                UndoRedoUtils.RecordUndoSetDirtyAndScheduleSave(settings, "ファーの傾きと長さの自動設定");
                settings.maxDistance = optimalFurLength;
            }
            else
            {
                // 長さのみ設定の場合
                UndoRedoUtils.RecordUndoSetDirtyAndScheduleSave(settings, "ファーの長さの自動設定");
                settings.maxDistance = optimalFurLength;
                
                string message = optimalFurLength != 0.04f 
                    ? $"ノーマルマップは見つかりませんでしたが、長さを自動設定しました。"
                    : "ノーマルマップと長さの設定が見つかりませんでした。";
                
                EditorUtility.DisplayDialog(
                    UILabels.INFO_DIALOG_TITLE, 
                    message, 
                    UILabels.ERROR_DIALOG_OK);
            }

            // 変更は遅延一括保存に任せる（二重保存を避ける）

            Repaint();
        }
    }
}
#endif
