#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Constants;
using NolaTools.FurMaskGenerator.Utils;

namespace NolaTools.FurMaskGenerator
{
    public partial class FurMaskGenerator
    {
        // Mask generation settings UI
        void DrawMaskGenerationSettings()
        {
            UIDrawingUtils.DrawInUIBox(() =>
            {


                // Texture size with auto-detection
                EditorGUILayout.BeginHorizontal();
                settings.textureSizeIndex = EditorGUILayout.Popup(
                    UILabels.TEXTURE_SIZE_LABEL,
                    settings.textureSizeIndex,
                    AppSettings.TEXTURE_SIZE_LABELS);
                
                if (GUILayout.Button(UILabels.AUTO_DETECT_BUTTON, GUILayout.Width(80)))
                {
                    AutoDetectTextureSize();
                }
                EditorGUILayout.EndHorizontal();

                // マスクの濃さ設定
                settings.gamma = EditorGUILayout.Slider(
                    UILabels.MASK_INTENSITY_LABEL,
                    settings.gamma,
                    0.1f,
                    5.0f);

                // 頂点ベースモード専用の設定
                // テクセルモード用ぼかし設定
                settings.texelBlurRadius = EditorGUILayout.IntSlider(
                    new GUIContent("ぼかし半径", "マスクのぼかし半径（ピクセル単位、ガウシアンブラー）。\n毛の向きのばらつきを吸収します。"),
                    settings.texelBlurRadius,
                    0,
                    16);

                // エッジパディング設定
                settings.edgePaddingSize = EditorGUILayout.IntSlider(
                    new GUIContent(UILabels.EDGE_PADDING_LABEL, UILabels.EDGE_PADDING_TOOLTIP),
                    settings.edgePaddingSize,
                    0,
                    32);

                // 透過モード設定
                settings.useTransparentMode = EditorGUILayout.Toggle(
                    new GUIContent(UILabels.TRANSPARENT_MODE_LABEL, UILabels.TRANSPARENT_MODE_TOOLTIP),
                    settings.useTransparentMode);

                // Generate button
                GUI.enabled = !baking;
                if (GUILayout.Button(UILabels.GENERATE_MASK_BUTTON))
                {
                    if (ValidateInputs())
                    {
                        StartBake();
                    }
                }
                GUI.enabled = true;
            });
        }

        /// <summary>
        /// 指定されたレンダラーとサブメッシュのメインテクスチャを取得
        /// </summary>
        private Texture2D GetMainTextureForRenderer(Renderer renderer, int submeshIndex)
        {
            return MaterialTextureUtils.GetMainTextureForRenderer(renderer, submeshIndex);
        }

        /// <summary>
        /// レンダラーを優先順位に従ってソート（bodyを最優先、それ以降は順不同）
        /// </summary>
        private IEnumerable<Renderer> GetRenderersInPriorityOrder()
        {
            var allRenderers = avatarRenderers.Concat(clothRenderers).Where(r => r != null);
            
            // bodyという名前のレンダラーを最優先で取得
            var bodyRenderers = allRenderers.Where(r => 
                r.gameObject.name.ToLowerInvariant().Contains("body"));
            
            // その他のレンダラー（body以外）
            var otherRenderers = allRenderers.Where(r => 
                !r.gameObject.name.ToLowerInvariant().Contains("body"));
            
            // bodyを先頭に、その他を後に配置
            return bodyRenderers.Concat(otherRenderers);
        }

        /// <summary>
        /// テクスチャサイズを自動検出して設定
        /// </summary>
        private void AutoDetectTextureSize()
        {
            if (avatarRenderers == null || avatarRenderers.Count == 0)
            {
                EditorUtility.DisplayDialog(UILabels.ERROR_DIALOG_TITLE, ErrorMessages.ERROR_RENDERERS_NOT_SET, UILabels.ERROR_DIALOG_OK);
                return;
            }
            
            var detectedSizes = new Dictionary<int, int>(); // サイズ -> カウント
            int totalTextures = 0;
            
            // 優先順位に従ってレンダラーを処理
            foreach (var renderer in GetRenderersInPriorityOrder())
            {
                if (renderer == null || renderer.sharedMaterials == null) continue;
                
                for (int submeshIndex = 0; submeshIndex < renderer.sharedMaterials.Length; submeshIndex++)
                {
                    var texture = GetMainTextureForRenderer(renderer, submeshIndex);
                    if (texture != null)
                    {
                        totalTextures++;
                        // 正方形テクスチャの場合のみカウント（通常のマスクテクスチャは正方形）
                        if (texture.width == texture.height)
                        {
                            int size = texture.width;
                            bool isBodyRenderer = renderer.gameObject.name.ToLowerInvariant().Contains("body");
                            
                            // bodyレンダラーのテクスチャが見つかった場合、重みを高く設定
                            int weight = isBodyRenderer ? 10 : 1;
                            
                            if (detectedSizes.ContainsKey(size))
                            {
                                detectedSizes[size] += weight;
                            }
                            else
                            {
                                detectedSizes[size] = weight;
                            }
                            
                        }
                    }
                }
            }
            
            if (detectedSizes.Count == 0)
            {
                EditorUtility.DisplayDialog(ErrorMessages.TEXTURE_AUTO_DETECT_TITLE, 
                    ErrorMessages.TEXTURE_AUTO_DETECT_NO_TEXTURE, UILabels.ERROR_DIALOG_OK);
                return;
            }
            
            // 最も多く使用されているサイズを取得（bodyレンダラーは重み10倍）
            var mostCommonSize = detectedSizes.OrderByDescending(kvp => kvp.Value).First();
            
            // AppSettings.TEXTURE_SIZESの中から最適なサイズインデックスを見つける
            int bestIndex = -1;
            for (int i = 0; i < AppSettings.TEXTURE_SIZES.Length; i++)
            {
                if (AppSettings.TEXTURE_SIZES[i] == mostCommonSize.Key)
                {
                    bestIndex = i;
                    break;
                }
            }
            
            // 完全一致がない場合、最も近いサイズを選択
            if (bestIndex == -1)
            {
                int minDiff = int.MaxValue;
                for (int i = 0; i < AppSettings.TEXTURE_SIZES.Length; i++)
                {
                    int diff = Mathf.Abs(AppSettings.TEXTURE_SIZES[i] - mostCommonSize.Key);
                    if (diff < minDiff)
                    {
                        minDiff = diff;
                        bestIndex = i;
                    }
                }
            }
            
            if (bestIndex >= 0)
            {
                UndoRedoUtils.RecordUndoSetDirtyAndScheduleSave(settings, UndoMessages.AUTO_DETECT_TEXTURE_SIZE);
                settings.textureSizeIndex = bestIndex;
            }
            else
            {
                EditorUtility.DisplayDialog(ErrorMessages.TEXTURE_AUTO_DETECT_TITLE, 
                    ErrorMessages.TEXTURE_AUTO_DETECT_NO_SIZE, UILabels.ERROR_DIALOG_OK);
            }
        }
    }
}

#endif


