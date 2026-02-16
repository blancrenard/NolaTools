#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Constants;
using NolaTools.FurMaskGenerator.Utils;

namespace NolaTools.FurMaskGenerator
{
    public partial class FurMaskGenerator
    {
        // Preview UI
        void DrawPreview()
        {
            UIDrawingUtils.DrawInUIBox(() =>
            {
                EditorGUILayout.LabelField(UILabels.PREVIEW_SECTION_TITLE);

                if (preview.Count > 0)
                {
                    foreach (var kvp in preview)
                    {
                        EditorGUILayout.LabelField(kvp.Key, EditorStyles.boldLabel);
                        EditorGUILayout.BeginHorizontal();

                        // Length Mask
                        EditorGUILayout.BeginVertical();
                        TextureOperationUtils.DrawTexturePreviewItem(
                            kvp.Value.LengthMask,
                            UILabels.LENGTH_MASK_LABEL,
                            UILabels.MASK_SAVE_BUTTON,
                            $"{kvp.Key}_Length",
                            128,
                            UILabels.SAVE_MASK_BUTTON);
                        EditorGUILayout.EndVertical();

                        // Alpha Mask
                        EditorGUILayout.BeginVertical();
                        TextureOperationUtils.DrawTexturePreviewItem(
                            kvp.Value.AlphaMask,
                            UILabels.ALPHA_MASK_LABEL,
                            UILabels.MASK_SAVE_BUTTON,
                            $"{kvp.Key}_Alpha",
                            128,
                            UILabels.SAVE_MASK_BUTTON);
                        EditorGUILayout.EndVertical();

                        EditorGUILayout.EndHorizontal();
                        GUILayout.Space(10);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(ErrorMessages.ERROR_NO_PREVIEW, MessageType.Info);
                }
            });
        }
    }
}

#endif


