using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Constants;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Data;

namespace NolaTools.FurMaskGenerator
{
    public partial class FurMaskGenerator
    {
        void DrawMaskGenerationSettings()
        {
            UIDrawingUtils.DrawInUIBox(() =>
            {
                // Target Material Selection
                DrawTargetMaterialSelector();

                if (settings.targetMaterial != null)
                {
                    // Create a horizontal group to place the Auto Set button on the right, spanning two rows
                    EditorGUILayout.BeginHorizontal();
                    
                    // Left column: Normal Map Settings and Fur Length Settings
                    EditorGUILayout.BeginVertical();
                    DrawNormalMapSettings();
                    DrawFurLengthSettings(false); // Draw without button
                    EditorGUILayout.EndVertical();

                    // Right column: Auto Set Button (spans height of the left column)
                    if (GUILayout.Button(UILabels.AUTO_SET_HAIR_TILT_BUTTON, GUILayout.Width(80), GUILayout.Height(38)))
                    {
                        AutoSetHairTiltFromFurNormalMaps();
                    }

                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    // Fallback if no target material selected (though normal map extraction requires it)
                    DrawFurLengthSettings(true);
                }

                // Texture size with auto-detection
                DrawTextureSizeSettings();

                // Other Settings
                DrawAdvancedSettings();

                // Generate button
                DrawGenerateButton();
            });
        }

        private void DrawNormalMapSettings()
        {
            // データ初期化 (if needed)
            if (settings.materialNormalMaps == null)
            {
                settings.materialNormalMaps = new List<MaterialNormalMapData>();
                UndoRedoUtils.RecordUndoAndSetDirty(settings, "Initialize Material Normal Maps");
            }

            var targetMatName = settings.targetMaterial.name;
            var normalMapData = settings.materialNormalMaps.FirstOrDefault(m => m.materialName == targetMatName);
            
            if (normalMapData == null)
            {
                normalMapData = new MaterialNormalMapData { materialName = targetMatName };
                settings.materialNormalMaps.Add(normalMapData);
                UndoRedoUtils.RecordUndoAndSetDirty(settings, "Add Material Normal Map for Target");
            }

            EditorGUILayout.BeginHorizontal();
            
            // Label (Left aligned like other properties)
            var labelRect = EditorGUILayout.GetControlRect(false, 18, GUILayout.Width(EditorGUIUtility.labelWidth));
            var centeredLabelStyle = new GUIStyle(EditorStyles.label);
            centeredLabelStyle.alignment = TextAnchor.MiddleLeft;
            GUI.Label(labelRect, "ノーマルマップ", centeredLabelStyle);

            // Texture Object Field (Custom)
            var texRect = EditorGUILayout.GetControlRect(false, 18, GUILayout.Width(18));
            int pickerID = GUIUtility.GetControlID(FocusType.Keyboard);

            // Draw Box (Empty or Preview)
            if (GUIUtility.keyboardControl == pickerID)
            {
                // Draw selection highlight
                var selectionRect = texRect;
                selectionRect.xMin -= 1; selectionRect.xMax += 1;
                selectionRect.yMin -= 1; selectionRect.yMax += 1;
                EditorGUI.DrawRect(selectionRect, new Color(0.24f, 0.49f, 0.91f, 1f)); // Unity Blue
            }
            GUI.Box(texRect, GUIContent.none, EditorStyles.helpBox);
            
            if (normalMapData.normalMap != null)
            {
                EditorGUI.DrawPreviewTexture(texRect, normalMapData.normalMap);
            }

            // Drag & Drop, Select, Delete Support
            var evt = Event.current;
            if (evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace))
            {
                if (GUIUtility.keyboardControl == pickerID && normalMapData.normalMap != null)
                {
                    UndoRedoUtils.RecordUndoSetDirtyAndScheduleSave(settings, "Clear Normal Map");
                    normalMapData.normalMap = null;
                    evt.Use();
                }
            }
            
            if (texRect.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.Use();
                }
                else if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    if (DragAndDrop.objectReferences.Length > 0 && DragAndDrop.objectReferences[0] is Texture2D droppedTex)
                    {
                        UndoRedoUtils.RecordUndoSetDirtyAndScheduleSave(settings, "Change Normal Map");
                        normalMapData.normalMap = droppedTex;


                    }
                    GUIUtility.keyboardControl = pickerID; // Auto-focus on drop
                    evt.Use();
                }
                else if (evt.type == EventType.MouseDown)
                {
                    // Select field (Focus)
                    GUIUtility.keyboardControl = pickerID;
                    
                    if (normalMapData.normalMap != null)
                    {
                        // Highlight in Project window on click
                        EditorGUIUtility.PingObject(normalMapData.normalMap);
                        Selection.activeObject = normalMapData.normalMap; 
                    }
                    evt.Use();
                }
            }

            // Picker Button (Circle) & Strength Slider
            EditorGUILayout.BeginVertical();
            GUILayout.Space(2); // Center vertically: (28 - 18) / 2 = 5
            
            EditorGUILayout.BeginHorizontal();

            // Picker Button
            var selectorStyle = GUI.skin.FindStyle("ObjectFieldButton");
            if (selectorStyle == null) selectorStyle = EditorStyles.radioButton;

            if (GUILayout.Button(GUIContent.none, selectorStyle, GUILayout.Width(18), GUILayout.Height(18)))
            {
                EditorGUIUtility.ShowObjectPicker<Texture2D>(normalMapData.normalMap, false, "", pickerID);
                evt.Use();
            }

            // Strength Slider
            EditorGUI.BeginChangeCheck();
            var newStrength = EditorGUILayout.Slider(
                normalMapData.normalStrength,
                -10f,
                10f);
            normalMapData.normalStrength = (float)System.Math.Round(newStrength, 1);
                if (EditorGUI.EndChangeCheck())
            {
                UndoRedoUtils.SetDirtyOnly(settings);
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            // Handle Picker Selection Result (Must cover entire area or be checked here)
            if (Event.current.commandName == "ObjectSelectorUpdated" && EditorGUIUtility.GetObjectPickerControlID() == pickerID)
            {
                var picked = EditorGUIUtility.GetObjectPickerObject() as Texture2D;
                if (normalMapData.normalMap != picked)
                {
                    UndoRedoUtils.RecordUndoSetDirtyAndScheduleSave(settings, "Change Normal Map");
                    normalMapData.normalMap = picked;
                    

                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFurLengthSettings(bool showAutoSetButton)
        {
            EditorGUILayout.BeginHorizontal();
            settings.maxDistance = EditorGUILayout.Slider(
                UILabels.DISTANCE_LABEL,
                settings.maxDistance,
                AppSettings.MIN_DISTANCE,
                AppSettings.MAX_DISTANCE);
            
            // Auto Set Button
            if (showAutoSetButton)
            {
                if (GUILayout.Button(UILabels.AUTO_SET_HAIR_TILT_BUTTON, GUILayout.Width(80)))
                {
                    AutoSetHairTiltFromFurNormalMaps();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTextureSizeSettings()
        {
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
        }

        private void DrawAdvancedSettings()
        {
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
        }

        private void DrawGenerateButton()
        {
            GUI.enabled = !baking;
            if (GUILayout.Button(UILabels.GENERATE_MASK_BUTTON))
            {
                if (ValidateInputs())
                {
                    StartBake();
                }
            }
            GUI.enabled = true;
        }

        private void DrawTargetMaterialSelector()
        {
            var allRenderers = new List<Renderer>();
            if (avatarRenderers != null) allRenderers.AddRange(avatarRenderers);
            // 服・髪・装飾品などのレンダラーは除外する
            // if (clothRenderers != null) allRenderers.AddRange(clothRenderers);

            var uniqueMaterials = new HashSet<Material>();
            var materialList = new List<Material>();
            var displayNames = new List<string>();

            foreach (var r in allRenderers)
            {
                if (r == null || r.sharedMaterials == null) continue;
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat != null && !uniqueMaterials.Contains(mat))
                    {
                        uniqueMaterials.Add(mat);
                        materialList.Add(mat);
                        displayNames.Add(mat.name);
                    }
                }
            }

            int currentIndex = 0;
            if (settings.targetMaterial != null)
            {
                int index = materialList.IndexOf(settings.targetMaterial);
                if (index >= 0) currentIndex = index;
            }
            
            // リストが空でなければ、未選択状態(null)を回避して最初の要素を選択済みにする
            if (settings.targetMaterial == null && materialList.Count > 0)
            {
                settings.targetMaterial = materialList[0];
                currentIndex = 0;
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(
                new GUIContent("出力マテリアル", "指定したマテリアルのみマスクを生成します。\nBody, Ears, Tailなどのリストにあるレンダラーから可能なマテリアルを列挙しています。"),
                currentIndex,
                displayNames.ToArray()
            );

            if (EditorGUI.EndChangeCheck())
            {
                UndoRedoUtils.RecordUndoSetDirtyAndScheduleSave(settings, "Change Target Material");
                settings.targetMaterial = materialList[newIndex];
            }
        }

        /// <summary>
        /// 指定されたレンダラーとサブメッシュのメインテクスチャを取得
        /// </summary>
        private Texture2D GetMainTextureForRenderer(Renderer renderer, int submeshIndex)
        {
            return MaterialTextureUtils.GetMainTextureForRenderer(renderer, submeshIndex);
        }

        /// <summary>
        /// 自動的に最適なテクスチャサイズを判定して設定
        /// </summary>
        private void AutoDetectTextureSize()
        {
            int maxSize = 512;
            bool found = false;

            // ターゲットマテリアルが選択されている場合はそのテクスチャサイズを優先
            if (settings.targetMaterial != null && settings.targetMaterial.mainTexture != null)
            {
                var tex = settings.targetMaterial.mainTexture;
                maxSize = Mathf.Max(tex.width, tex.height);
                found = true;
            }
            else if (avatarRenderers != null)
            {
                // 全ての対象レンダラー（avatarRenderersのみ）をチェック
                foreach (var r in avatarRenderers)
                {
                    if (r == null || r.sharedMaterials == null) continue;
                    foreach (var mat in r.sharedMaterials)
                    {
                        if (mat != null && mat.mainTexture != null)
                        {
                            var tex = mat.mainTexture;
                            int size = Mathf.Max(tex.width, tex.height);
                            if (size > maxSize) maxSize = size;
                            found = true;
                        }
                    }
                }
            }

            if (!found)
            {
                maxSize = 1024; // Default if nothing found
            }

            // 最も近いサイズ（以上）を選択
            int bestIndex = 0;
            for (int i = 0; i < AppSettings.TEXTURE_SIZES.Length; i++)
            {
                if (AppSettings.TEXTURE_SIZES[i] >= maxSize)
                {
                    bestIndex = i;
                    break;
                }
                bestIndex = i; // Keep last if maxSize is larger than all options
            }

            UndoRedoUtils.RecordUndoSetDirtyAndScheduleSave(settings, "Auto Detect Texture Size");
            settings.textureSizeIndex = bestIndex;


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
    }
}
