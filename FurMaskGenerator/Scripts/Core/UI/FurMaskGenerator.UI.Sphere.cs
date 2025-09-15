#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Constants;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Data;

namespace NolaTools.FurMaskGenerator
{
    public partial class FurMaskGenerator
    {
        // Sphere UI methods
        void DrawSphereMaskSettings()
        {
            UIDrawingUtils.DrawInUIBox(() =>
            {
                try
                {
                // タイトル付きフォールドアウト（共通ヘルパ使用）
                foldoutSphereSection = UIDrawingUtils.DrawSectionFoldout(foldoutSphereSection, UILabels.SPHERE_SECTION_TITLE);

                if (!foldoutSphereSection)
                {
                    return;
                }

                // スフィア表示・非表示チェックボックス
                showSphereGizmos = EditorGUILayout.Toggle(UILabels.SPHERES_SHOW_TOGGLE, showSphereGizmos);

                // シーン上クリックでスフィアを追加するトグル
                bool newSphereToggle = GUILayout.Toggle(addSphereOnClick, UILabels.ADD_SPHERE_ON_SCENE_BUTTON, GUI.skin.button);
                if (newSphereToggle != addSphereOnClick)
                {
                    addSphereOnClick = newSphereToggle;
                    if (addSphereOnClick)
                    {
                        // UVマスク追加モードと排他にする
                        addUVIslandOnClick = false;
                    }
                    UIDrawingUtils.RefreshUI();
                }

                if (settings == null || settings.sphereMasks == null)
                {
                    return;
                }

                UIDrawingUtils.EnsureFoldoutCount(sphereFoldoutStates, settings.sphereMasks.Count);

                for (int i = 0; i < settings.sphereMasks.Count; i++)
                {
                    var sphere = settings.sphereMasks[i];
                    // 初期化: マーカー色未設定ならビビットまたはパステルのランダム色を割り当て
                    if (sphere.markerColor.a <= 0f)
                    {
                        sphere.markerColor = ColorGenerator.GenerateMarkerColor();
                        UndoRedoUtils.SetDirtyOnly(settings);
                    }

                    if (i >= sphereFoldoutStates.Count)
                    {
                        Debug.LogError(string.Format(ErrorMessages.ERROR_SPHERE_FOLDOUT_INDEX, i, sphereFoldoutStates.Count));
                        continue;
                    }

                    DrawSphereBox(() =>
                    {
                        bool headerRequestedDelete = DrawSphereFoldoutHeader(i, sphereFoldoutStates[i],
                            (isExpanded) => {
                                if (i < sphereFoldoutStates.Count)
                                {
                                    sphereFoldoutStates[i] = isExpanded;
                                }
                            },
                            () => {
                                UndoRedoUtils.RecordUndoSetDirtyAndScheduleSave(settings, UndoMessages.FUR_MASK_GENERATOR_CHANGE);
                                settings.sphereMasks.RemoveAt(i);
                            },
                            sphere.name);

                        if (headerRequestedDelete)
                        {
                            // このスフィア項目の描画を即終了（外側の DrawSphereBox が EndUIBox を呼ぶ）
                            return;
                        }

                        if (i < settings.sphereMasks.Count && sphereFoldoutStates[i])
                        {
                            DrawSpherePropertyRow(UILabels.POSITION_LABEL, () =>
                            {
                                EditorGUI.BeginChangeCheck();
                                Vector3 newPosition = EditorGUILayout.Vector3Field("", sphere.position);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    UndoRedoUtils.RecordUndoAndSetDirty(settings, UndoMessages.MOVE_SPHERE_MASK);
                                    sphere.position = newPosition;
                                }
                            });

                            DrawSpherePropertyRow(UILabels.RADIUS_LABEL, () =>
                            {
                                EditorGUI.BeginChangeCheck();
                                sphere.radius = EditorGUILayout.Slider(sphere.radius, 0.001f, 1f);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    UndoRedoUtils.RecordUndoAndSetDirty(settings, UndoMessages.FUR_MASK_GENERATOR_CHANGE);
                                }
                            });

                            DrawSpherePropertyRow(UILabels.BLUR_LABEL, () =>
                            {
                                EditorGUI.BeginChangeCheck();
                                sphere.gradient = EditorGUILayout.Slider(sphere.gradient, 0f, 1f);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    UndoRedoUtils.RecordUndoAndSetDirty(settings, UndoMessages.FUR_MASK_GENERATOR_CHANGE);
                                }
                            });

                            // 濃さ（0.1〜1.0）
                            DrawSpherePropertyRow(UILabels.SPHERE_INTENSITY_LABEL, () =>
                            {
                                EditorGUI.BeginChangeCheck();
                                float newIntensity = EditorGUILayout.Slider(sphere.intensity, AppSettings.SPHERE_INTENSITY_MIN, AppSettings.SPHERE_INTENSITY_MAX);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    UndoRedoUtils.RecordUndoAndSetDirty(settings, UndoMessages.FUR_MASK_GENERATOR_CHANGE);
                                    sphere.intensity = newIntensity;
                                }
                            });

                            // ミラー機能チェックボックス
                            DrawSpherePropertyRow(UILabels.SPHERE_MIRROR_LABEL, () =>
                            {
                                EditorGUI.BeginChangeCheck();
                                bool newMirror = EditorGUILayout.Toggle(sphere.useMirror);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    UndoRedoUtils.RecordUndoAndSetDirty(settings, UndoMessages.FUR_MASK_GENERATOR_CHANGE);
                                    sphere.useMirror = newMirror;
                                }
                            });
                        }
                    });
                }

                // 旧「マスクスフィア追加」ボタンは廃止（シーン上クリック追加に統一）
            }
            catch (System.Exception e)
            {
                Debug.LogError(string.Format(ErrorMessages.ERROR_SPHERE_MASK_SETTINGS, e.Message));
            }
            });
        }

        // Helper methods for sphere UI
        private void DrawSphereBox(Action contentAction)
        {
            UIDrawingUtils.DrawInUIBox(() => { contentAction(); }, AppSettings.SPHERE_ITEM_BACKGROUND);
        }

        private void DrawSpherePropertyRow(string label, Action contentAction)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(80));
            contentAction();
            EditorGUILayout.EndHorizontal();
        }

        private bool DrawSphereFoldoutHeader(int sphereIndex, bool isExpanded,
            Action<bool> onFoldoutChange, Action onDeleteAction, string sphereName)
        {
            EditorGUILayout.BeginHorizontal();

            // Foldout
            Rect foldoutRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(AppSettings.FOLDOUT_WIDTH));
            foldoutRect.y += 1f;

            EditorGUI.BeginChangeCheck();
            bool newExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, "");
            if (EditorGUI.EndChangeCheck())
            {
                onFoldoutChange(newExpanded);
            }

            // Color swatch
            var sphere = (sphereIndex >= 0 && sphereIndex < settings.sphereMasks.Count) ? settings.sphereMasks[sphereIndex] : null;
            if (sphere != null)
            {
                bool clicked = DrawClickableColorSwatch(sphere.markerColor, EditorGUIUtility.singleLineHeight, selectedSphereIndex == sphereIndex, sphere.name);
                if (clicked)
                {
                    selectedSphereIndex = sphereIndex;
                    UIDrawingUtils.RefreshUI();
                }
            }

            // Name field
            // sphere.name = EditorGUILayout.TextField(sphere.name);

            // Delete button
            bool deleteRequested = false;
            if (GUILayout.Button(UILabels.DELETE_BUTTON, GUILayout.Width(AppSettings.DELETE_BUTTON_WIDTH)))
            {
                deleteRequested = true;
            }

            EditorGUILayout.EndHorizontal();

            if (deleteRequested)
            {
                onDeleteAction();
                return true;
            }

            return false;
        }

        private bool DrawClickableColorSwatch(Color color, float height, bool selected, string tooltip = null)
        {
            var c = (color.a > 0f) ? color : ColorGenerator.GenerateMarkerColor();
            Rect r = EditorGUILayout.GetControlRect(false, height, GUILayout.ExpandWidth(true));

            // Draw background if selected
            if (selected)
            {
                EditorGUI.DrawRect(r, new Color(0.3f, 0.8f, 1f, 0.4f)); // より鮮やかな青
            }

            // Draw color swatch
            Rect colorRect = new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);
            EditorGUI.DrawRect(colorRect, c);

            // Handle click
            Event e = Event.current;
            if (e.type == EventType.MouseDown && r.Contains(e.mousePosition))
            {
                e.Use();
                return true;
            }

            return false;
        }

        private void TryAddSphere(string name, Vector3 position, float radius, float gradient)
        {
            if (settings == null) return;

            var newSphere = new SphereData
            {
                name = name,
                position = position,
                radius = radius,
                gradient = gradient
            };

            settings.sphereMasks.Add(newSphere);
            UndoRedoUtils.RecordUndoAndSetDirty(settings, "Add Sphere Mask");
            Repaint();
        }
    }
}

#endif


