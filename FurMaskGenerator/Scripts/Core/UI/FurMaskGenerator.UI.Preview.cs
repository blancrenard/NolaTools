#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Mask.Generator.Constants;
using Mask.Generator.Utils;

namespace Mask.Generator
{
    public partial class FurMaskGenerator
    {
        // Preview UI
        void DrawPreview()
        {
            EditorUIUtils.DrawInUIBox(() =>
            {
                EditorGUILayout.LabelField(UIConstants.PREVIEW_SECTION_TITLE);

                if (preview.Count > 0)
                {
                    foreach (var kvp in preview)
                    {
                        EditorUIUtils.DrawTexturePreviewItem(
                            kvp.Value,
                            $"{kvp.Key}",
                            UIConstants.MASK_SAVE_BUTTON,
                            kvp.Key,
                            128,
                            UIConstants.SAVE_MASK_BUTTON);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(UIConstants.ERROR_NO_PREVIEW, MessageType.Info);
                }
            });
        }
    }
}

#endif


