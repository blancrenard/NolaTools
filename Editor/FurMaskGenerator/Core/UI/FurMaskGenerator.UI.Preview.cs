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
                        TextureOperationUtils.DrawTexturePreviewItem(
                            kvp.Value,
                            $"{kvp.Key}",
                            UILabels.MASK_SAVE_BUTTON,
                            kvp.Key,
                            128,
                            UILabels.SAVE_MASK_BUTTON);
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


