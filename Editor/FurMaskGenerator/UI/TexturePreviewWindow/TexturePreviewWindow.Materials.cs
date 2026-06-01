#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Data;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator.UI
{
    public partial class TexturePreviewWindow
    {
        private Material currentPreviewMaterial;
        private readonly List<Material> _previewMaterials = new List<Material>();

        private static Material GetTargetMaterial(Target t)
        {
            if (t?.Renderer?.sharedMaterials == null) return null;
            int sub = Mathf.Clamp(t.SubmeshIndex, 0, t.Renderer.sharedMaterials.Length - 1);
            return t.Renderer.sharedMaterials[sub];
        }

        private bool TargetMatchesPreviewMaterial(Target t)
        {
            if (t == null || currentPreviewMaterial == null) return false;
            return MaterialTextureUtils.MatchesTargetMaterial(GetTargetMaterial(t), currentPreviewMaterial);
        }

        private bool MaskMatchesPreviewMaterial(UVIslandMaskData mask, Renderer renderer)
        {
            if (mask == null || renderer == null) return false;
            if (currentPreviewMaterial == null) return true;
            var mats = renderer.sharedMaterials;
            if (mats == null || mask.submeshIndex < 0 || mask.submeshIndex >= mats.Length) return false;
            return MaterialTextureUtils.MatchesTargetMaterial(mats[mask.submeshIndex], currentPreviewMaterial);
        }

        /// <summary>
        /// ターゲット一覧からユニークなマテリアルリストを構築（出力マテリアル選択と同様）
        /// </summary>
        private void RebuildPreviewMaterialList()
        {
            _previewMaterials.Clear();
            _tmpTextureNames.Clear();
            if (targets == null) return;

            foreach (var t in targets)
            {
                var mat = GetTargetMaterial(t);
                if (mat == null) continue;

                bool exists = false;
                foreach (var existing in _previewMaterials)
                {
                    if (MaterialTextureUtils.MatchesTargetMaterial(existing, mat))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    _previewMaterials.Add(mat);
                    _tmpTextureNames.Add(mat.name);
                }
            }
        }

        private void ApplyPreviewMaterial(Material mat)
        {
            if (mat == null || targets == null || targets.Count == 0) return;

            currentPreviewMaterial = mat;
            for (int i = 0; i < _previewMaterials.Count; i++)
            {
                if (MaterialTextureUtils.MatchesTargetMaterial(_previewMaterials[i], mat))
                {
                    currentPreviewMaterial = _previewMaterials[i];
                    break;
                }
            }

            for (int i = 0; i < targets.Count; i++)
            {
                if (!TargetMatchesPreviewMaterial(targets[i])) continue;
                currentTargetIndex = i;
                var t = targets[i];
                texture = t.Texture;
                targetRenderer = t.Renderer;
                submeshIndex = t.SubmeshIndex;
                break;
            }

            if (showUVMasks) GenerateOverlayTexture();
            Repaint();
        }

        /// <summary>
        /// 出力マテリアルと同様のマテリアル単位プルダウン
        /// </summary>
        private void DrawMaterialSelector()
        {
            if (_previewMaterials.Count == 0)
            {
                GUILayout.Label(texture != null ? texture.name : "(No Material)", EditorStyles.toolbarButton);
                return;
            }

            int currentIndex = 0;
            if (currentPreviewMaterial != null)
            {
                int index = _previewMaterials.IndexOf(currentPreviewMaterial);
                if (index < 0)
                {
                    index = _previewMaterials.FindIndex(m =>
                        MaterialTextureUtils.MatchesTargetMaterial(m, currentPreviewMaterial));
                }
                if (index >= 0) currentIndex = index;
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(UILabels.OUTPUT_MATERIAL_LABEL, UILabels.OUTPUT_MATERIAL_TOOLTIP),
                currentIndex,
                _tmpTextureNames.ToArray(),
                GUILayout.MaxWidth(300));
            if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < _previewMaterials.Count)
            {
                ApplyPreviewMaterial(_previewMaterials[newIndex]);
            }
        }
    }
}
#endif
