#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mask.Generator.Data;
using Mask.Generator.Constants;
using Mask.Generator.Utils;

namespace Mask.Generator
{
    public partial class FurMaskGenerator
    {
        // Normal map settings UI
        void DrawNormalMapSettings()
        {
            UIDrawingUtils.DrawInUIBox(() =>
            {
                // タイトル付きフォールドアウト（共通ヘルパ使用）
                foldoutNormalMapSection = UIDrawingUtils.DrawSectionFoldout(foldoutNormalMapSection, UILabels.HAIR_TILT_SECTION_TITLE);

                // データ初期化
                if (settings.materialNormalMaps == null)
                {
                    settings.materialNormalMaps = new List<MaterialNormalMapData>();
                    UndoRedoUtils.RecordUndoAndSetDirty(settings, "Initialize Material Normal Maps");
                }

                if (!foldoutNormalMapSection) return;

                // 自動設定ボタン
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(UILabels.AUTO_SET_HAIR_TILT_BUTTON))
                {
                    AutoSetHairTiltFromFurNormalMaps();
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space();

                // ファーの長さ設定
                settings.maxDistance = EditorGUILayout.Slider(
                    UILabels.DISTANCE_LABEL,
                    settings.maxDistance,
                    AppSettings.MIN_DISTANCE,
                    AppSettings.MAX_DISTANCE);
                
                EditorGUILayout.Space();

                // 「体・耳・尻尾など」のリストにあるレンダラーに設定されているマテリアルのみを取得
                var allMaterials = new HashSet<string>();
                if (avatarObject != null)
                {
                    // avatarRenderersとclothRenderersに含まれるレンダラーのマテリアルのみ取得
                    var targetRenderers = new List<Renderer>();
                    targetRenderers.AddRange(avatarRenderers);
                    targetRenderers.AddRange(clothRenderers);

                    foreach (var r in targetRenderers)
                    {
                        if (r != null && r.sharedMaterials != null)
                        {
                            foreach (var mat in r.sharedMaterials)
                            {
                                if (mat != null)
                                {
                                    allMaterials.Add(mat.name);
                                }
                            }
                        }
                    }
                }

                // 削除対象のインデックスを記録
                var indicesToRemove = new List<int>();

                // 既存のノーマルマップ設定を表示・編集
                for (int i = settings.materialNormalMaps.Count - 1; i >= 0; i--)
                {
                    var normalMapData = settings.materialNormalMaps[i];
                    if (normalMapData == null) continue;

                    UIDrawingUtils.DrawInUIBox(() =>
                    {
                    EditorGUILayout.BeginHorizontal();

                    // マテリアル選択ドロップダウン
                    var materialNames = new List<string> { UILabels.SELECT_MATERIAL_PLACEHOLDER };
                    materialNames.AddRange(allMaterials);

                    int currentIndex = 0;
                    if (!string.IsNullOrEmpty(normalMapData.materialName))
                    {
                        currentIndex = materialNames.FindIndex(name => name == normalMapData.materialName);
                        if (currentIndex == -1) currentIndex = 0; // 見つからない場合はデフォルト
                    }

                    int newIndex = EditorGUILayout.Popup(UILabels.MATERIAL_LABEL, currentIndex, materialNames.ToArray());
                    if (newIndex != currentIndex && newIndex > 0)
                    {
                        normalMapData.materialName = materialNames[newIndex];
                        UndoRedoUtils.SetDirtyOnly(settings);
                    }

                    // 削除ボタン（削除対象を記録）
                    bool deleted = false;
                    if (GUILayout.Button(UILabels.DELETE_BUTTON, GUILayout.Width(AppSettings.DELETE_BUTTON_WIDTH)))
                    {
                        indicesToRemove.Add(i);
                        deleted = true;
                    }

                    EditorGUILayout.EndHorizontal();
                    if (deleted) { return; }

                    // ノーマルマップテクスチャ選択
                    EditorGUI.BeginChangeCheck();
                    var prevTexture = normalMapData.normalMap;
                    normalMapData.normalMap = (Texture2D)EditorGUILayout.ObjectField(
                        UILabels.NORMAL_MAP_SECTION_TITLE,
                        normalMapData.normalMap,
                        typeof(Texture2D),
                        false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        UndoRedoUtils.SetDirtyOnly(settings);

                        // 新しくテクスチャが設定された場合、readableかどうかをチェック
                        if (normalMapData.normalMap != null && !normalMapData.normalMap.isReadable)
                        {
                            EditorGUILayout.HelpBox(
                                string.Format(ErrorMessages.ERROR_TEXTURE_NOT_READABLE, normalMapData.normalMap.name),
                                MessageType.Warning);
                        }
                    }
                    else if (normalMapData.normalMap != null && !normalMapData.normalMap.isReadable)
                    {
                        // 既存のテクスチャがreadableでない場合も警告を表示
                        EditorGUILayout.HelpBox(
                            string.Format(ErrorMessages.ERROR_TEXTURE_NOT_READABLE, normalMapData.normalMap.name),
                            MessageType.Warning);
                    }

                    // ファーの傾きは法線の傾き量で制御（UI非表示）
                    normalMapData.intensity = 1.0f;
                    
                    // G(Y)反転はUnity側で処理されるため、ツールでは非対応（UI非表示）
                    if (EditorGUI.EndChangeCheck())
                    {
                        UndoRedoUtils.SetDirtyOnly(settings);
                    }

                    // ノーマル強度スライダー
                    EditorGUI.BeginChangeCheck();
                    normalMapData.normalStrength = EditorGUILayout.Slider(
                        "強度",
                        normalMapData.normalStrength,
                        -10f,
                        10f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        UndoRedoUtils.SetDirtyOnly(settings);
                    }

                    }, AppSettings.SPHERE_ITEM_BACKGROUND);
                    EditorGUILayout.Space();
                }

                // 削除対象をまとめて削除（GUIレイアウトを正しく保つため）
                foreach (int index in indicesToRemove.OrderByDescending(x => x))
                {
                    if (index < settings.materialNormalMaps.Count)
                    {
                        settings.materialNormalMaps.RemoveAt(index);
                        UndoRedoUtils.RecordUndoAndSetDirty(settings, "Remove Material Normal Map");
                    }
                }

                // 新規追加ボタン
                if (GUILayout.Button(UILabels.ADD_HAIR_TILT_BUTTON))
                {
                    settings.materialNormalMaps.Add(new MaterialNormalMapData());
                    UndoRedoUtils.RecordUndoAndSetDirty(settings, "Add Material Normal Map");
                }
            });
        }
    }
}

#endif


