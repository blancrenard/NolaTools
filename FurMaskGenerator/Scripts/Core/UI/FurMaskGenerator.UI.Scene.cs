#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Mask.Generator.Constants;
using Mask.Generator.Utils;
using Mask.Generator.Data;
using UnityEngine.Rendering;

namespace Mask.Generator
{
    public partial class FurMaskGenerator
    {
        // Raycast helper (reuse to avoid GC spikes)
        private GameObject _raycastHelper;
        private MeshCollider _raycastHelperCollider;
        private Mesh _raycastBakedMesh;

        private void EnsureRaycastHelper()
        {
            if (_raycastHelper == null)
            {
                _raycastHelper = new GameObject(UIConstants.TEMP_RAYCAST_COLLIDER_NAME);
                _raycastHelper.hideFlags = HideFlags.HideAndDontSave;
                _raycastHelperCollider = _raycastHelper.AddComponent<MeshCollider>();
                _raycastHelperCollider.convex = false;
            }
        }

        private bool SetupRaycastMesh(Renderer renderer)
        {
            if (renderer == null) return false;
            EnsureRaycastHelper();

            // 同じ位置・回転・スケールを反映
            _raycastHelper.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
            _raycastHelper.transform.localScale = renderer.transform.lossyScale;

            // Skinned と MeshFilter 双方に対応
            if (renderer is SkinnedMeshRenderer smr)
            {
                if (_raycastBakedMesh == null)
                {
                    _raycastBakedMesh = new Mesh();
                    _raycastBakedMesh.indexFormat = IndexFormat.UInt32;
                }
                smr.BakeMesh(_raycastBakedMesh, true);
                _raycastHelperCollider.sharedMesh = _raycastBakedMesh;
                return _raycastBakedMesh.vertexCount > 0;
            }
            else if (renderer.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
            {
                _raycastHelperCollider.sharedMesh = mf.sharedMesh;
                return mf.sharedMesh.vertexCount > 0;
            }

            _raycastHelperCollider.sharedMesh = null;
            return false;
        }

        private void CleanupRaycastHelper()
        {
            if (_raycastBakedMesh != null)
            {
                UnityEngine.Object.DestroyImmediate(_raycastBakedMesh);
                _raycastBakedMesh = null;
            }
            if (_raycastHelper != null)
            {
                UnityEngine.Object.DestroyImmediate(_raycastHelper);
                _raycastHelper = null;
                _raycastHelperCollider = null;
            }
        }
        // Scene GUI methods
        void OnSceneGUI(SceneView sceneView)
        {
            if (settings == null) return;

            // スフィアのギズモ描画
            if (showSphereGizmos)
            {
                DrawSphereGizmos();
                DrawSphereHandles();
            }

            // UVマーカーの描画
            if (showUVMarkers)
            {
                DrawUVMakers();
            }

            // スフィア/UV追加モード時にSceneビュー枠へ黄色フレームとラベルを表示（クリックもここで処理し、以降のクリック処理を無効化）
            if (addSphereOnClick)
            {
                DrawAddModeSceneFrameWithLabel(sceneView, UIConstants.ADD_MODE_LABEL_SPHERE);
            }
            else if (addUVIslandOnClick)
            {
                DrawAddModeSceneFrameWithLabel(sceneView, UIConstants.ADD_MODE_LABEL_UV);
            }

            // スフィア追加モードのホバー更新と仮スフィア描画
            if (addSphereOnClick)
            {
                UpdateSphereAddHoverPosition();
                DrawSphereAddHoverPreview();
            }
            else if (addUVIslandOnClick)
            {
                UpdateUVAddHoverPosition();
                DrawUVAddHoverPreview();
            }

            // クリック処理
            HandleSceneClicks();

            // スフィア選択（ハンドル描画の後に行い、誤選択を防ぐ）
            HandleSphereSelectionClick(sceneView);
        }

        // UV追加モード: マウスホバー位置の更新（レイキャスト）
        private void UpdateUVAddHoverPosition()
        {
            Event e = Event.current;
            if (e == null) return;
            if (e.alt)
            {
                hasUVAddHover = false;
                return;
            }

            if (e.isMouse || e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                var picked = HandleUtility.PickGameObject(e.mousePosition, false);
                if (picked == null)
                {
                    hasUVAddHover = false;
                    return;
                }
                if (!picked.TryGetComponent<Renderer>(out var renderer))
                {
                    hasUVAddHover = false;
                    return;
                }

                if (!SetupRaycastMesh(renderer))
                {
                    hasUVAddHover = false;
                    return;
                }

                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (_raycastHelperCollider != null && _raycastHelperCollider.Raycast(ray, out RaycastHit hit, EditorMeshUtils.RaycastMaxDistance))
                {
                    uvAddHoverPosition = EditorMeshUtils.RoundToPrecision(hit.point, UIConstants.POSITION_PRECISION);
                    hasUVAddHover = true;
                }
                else
                {
                    hasUVAddHover = false;
                }
            }

            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)
            {
                SceneView.RepaintAll();
            }
        }

        // UV追加モード: 仮十字マーカーの描画
        private void DrawUVAddHoverPreview()
        {
            if (!hasUVAddHover) return;
            Color baseColor = UIConstants.ADD_MODE_FRAME_COLOR;
            Color cross = new Color(baseColor.r, baseColor.g, baseColor.b, 0.95f);
            EditorUIUtils.DrawCross(uvAddHoverPosition, 0.006f, cross);
        }

        // スフィア追加モード: マウスホバー位置の更新（レイキャスト）
        private void UpdateSphereAddHoverPosition()
        {
            Event e = Event.current;
            if (e == null) return;
            if (e.alt)
            {
                hasSphereAddHover = false;
                return;
            }

            // マウスがSceneビュー上にある場合のみ
            if (e.isMouse || e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                // ピック対象を取得
                var picked = HandleUtility.PickGameObject(e.mousePosition, false);
                if (picked == null)
                {
                    hasSphereAddHover = false;
                    return;
                }
                if (!picked.TryGetComponent<Renderer>(out var renderer))
                {
                    hasSphereAddHover = false;
                    return;
                }

                if (!SetupRaycastMesh(renderer))
                {
                    hasSphereAddHover = false;
                    return;
                }

                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (_raycastHelperCollider != null && _raycastHelperCollider.Raycast(ray, out RaycastHit hit, EditorMeshUtils.RaycastMaxDistance))
                {
                    sphereAddHoverPosition = EditorMeshUtils.RoundToPrecision(hit.point, UIConstants.POSITION_PRECISION);
                    hasSphereAddHover = true;
                }
                else
                {
                    hasSphereAddHover = false;
                }
            }

            // 入力のたびに再描画
            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)
            {
                SceneView.RepaintAll();
            }
        }

        // スフィア追加モード: 仮スフィアの描画
        private void DrawSphereAddHoverPreview()
        {
            if (!hasSphereAddHover) return;
            Color baseColor = UIConstants.ADD_MODE_FRAME_COLOR;
            Color wire = new Color(baseColor.r, baseColor.g, baseColor.b, 0.9f);
            Color inner = new Color(baseColor.r, baseColor.g, baseColor.b, 0.5f);
            Color grad = new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f);

            EditorGizmoUtils.SetDepthTest(true, () =>
            {
                EditorUIUtils.DrawGradientSpheres(
                    sphereAddHoverPosition,
                    UIConstants.DEFAULT_RADIUS,
                    UIConstants.GRADIENT_DEFAULT,
                    wire,
                    inner,
                    grad,
                    0.6f
                );
            });

        }

        private void DrawSphereGizmos()
        {
            if (settings?.sphereMasks == null) return;

            for (int i = 0; i < settings.sphereMasks.Count; i++)
            {
                var sphere = settings.sphereMasks[i];
                if (sphere == null) continue;

                var baseColor = (sphere.markerColor.a > 0f) ? sphere.markerColor : Color.cyan;
                // 濃さに応じてアルファを調整（視覚的な強度の目安）
                float intensity = Mathf.Clamp(sphere.intensity, UIConstants.SPHERE_INTENSITY_MIN, UIConstants.SPHERE_INTENSITY_MAX);
                var wireColor = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(0.5f, 0.95f, intensity));
                var innerColor = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(0.3f, 0.7f, intensity));
                var gradColor = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(0.15f, 0.4f, intensity));

                // オリジナルスフィアの描画
                EditorUIUtils.DrawGradientSpheres(
                    sphere.position,
                    sphere.radius,
                    sphere.gradient,
                    wireColor,
                    innerColor,
                    gradColor,
                    0.6f);

                // ミラー機能が有効な場合、ミラー位置にも薄い色でスフィアを表示
                if (sphere.useMirror)
                {
                    Vector3 mirroredPosition = new Vector3(-sphere.position.x, sphere.position.y, sphere.position.z);
                    
                    // ミラー用の色（薄くする）
                    var mirrorWireColor = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(0.4f, 0.7f, intensity));
                    var mirrorInnerColor = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(0.25f, 0.5f, intensity));
                    var mirrorGradColor = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(0.15f, 0.3f, intensity));

                    EditorUIUtils.DrawGradientSpheres(
                        mirroredPosition,
                        sphere.radius,
                        sphere.gradient,
                        mirrorWireColor,
                        mirrorInnerColor,
                        mirrorGradColor,
                        0.3f);
                }

                if (i == selectedSphereIndex)
                {
                    EditorUIUtils.DrawSelectedSphereHighlight(sphere.position, sphere.radius, baseColor);
                }
            }
        }

        private void DrawSphereHandles()
        {
            if (settings?.sphereMasks == null) return;
            if (selectedSphereIndex < 0 || selectedSphereIndex >= settings.sphereMasks.Count) return;

            var sphere = settings.sphereMasks[selectedSphereIndex];
            if (sphere == null) return;

            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(sphere.position, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                if (newPos != sphere.position)
                {
                    // ドラッグ中は保存を行わず、Undo + Dirty のみにする
                    EditorUIUtils.RecordUndoAndSetDirty(settings, UIConstants.UNDO_MOVE_SPHERE_MASK);
                    sphere.position = EditorMeshUtils.RoundToPrecision(newPos, UIConstants.POSITION_PRECISION);
                    Repaint();
                }
            }
        }

        private void HandleSphereSelectionClick(SceneView sceneView)
        {
            if (!showSphereGizmos) return;
            if (settings?.sphereMasks == null || settings.sphereMasks.Count == 0) return;
            if (addSphereOnClick || addUVIslandOnClick) return;

            Event e = Event.current;
            if (e.alt) return;
            if (e.type != EventType.MouseDown || e.button != 0) return;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            int bestIndex = -1;
            float bestT = float.MaxValue;
            for (int i = 0; i < settings.sphereMasks.Count; i++)
            {
                var s = settings.sphereMasks[i];
                if (s == null) continue;
                Vector3 oc = ray.origin - s.position;
                float r = Mathf.Min(s.radius, UIConstants.SHOW_MAX_RADIUS);
                float b = Vector3.Dot(ray.direction, oc);
                float c = Vector3.Dot(oc, oc) - r * r;
                float disc = b * b - c;
                if (disc < 0f) continue;
                float t = -b - Mathf.Sqrt(disc);
                if (t < 0f) continue;
                if (t < bestT) { bestT = t; bestIndex = i; }
            }

            if (bestIndex >= 0)
            {
                selectedSphereIndex = bestIndex;
                Selection.activeObject = null;
                EditorMeshUtils.NotifyMaskSphereSelected();
                Repaint();
                e.Use();
            }
        }

        private void DrawUVMakers()
        {
            if (settings?.uvIslandMasks == null || settings.uvIslandMasks.Count == 0) return;

            foreach (var mask in settings.uvIslandMasks)
            {
                if (mask == null) continue;
                var go = EditorPathUtils.FindGameObjectByPath(mask.rendererPath);
                if (go == null) continue;
                if (!go.TryGetComponent<Renderer>(out var renderer)) continue;

                Vector3 worldPos = mask.seedWorldPos;
                if (worldPos.sqrMagnitude < 1e-10f)
                {
                    Mesh mesh = EditorMeshUtils.GetMeshForRenderer(renderer, out bool baked);
                    if (mesh != null)
                    {
                        try
                        {
                            int sub = Mathf.Clamp(mask.submeshIndex, 0, mesh.subMeshCount - 1);
                            int[] tri = mesh.GetTriangles(sub);
                            if (tri != null && tri.Length > 0)
                            {
                                var uvarr = mesh.uv;
                                int nearestVi = -1; float best = float.MaxValue;
                                for (int i = 0; i < tri.Length; i++)
                                {
                                    int vi = tri[i];
                                    float d = Vector2.SqrMagnitude(uvarr[vi] - mask.seedUV);
                                    if (d < best) { best = d; nearestVi = vi; }
                                }
                                if (nearestVi >= 0)
                                {
                                    worldPos = renderer.transform.TransformPoint(mesh.vertices[nearestVi]);
                                }
                            }
                        }
                        finally
                        {
                            if (baked)
                            {
                                UnityEngine.Object.DestroyImmediate(mesh);
                            }
                        }
                    }
                }

                if (worldPos.sqrMagnitude >= 1e-10f)
                {
                    var baseColor = (mask.markerColor.a > 0f) ? mask.markerColor : Color.cyan;
                    Color c = baseColor;
                    float size = 0.005f;
                    int idx = settings.uvIslandMasks.IndexOf(mask);
                    if (idx == selectedUVIslandIndex)
                    {
                        c = Color.white;
                        size *= 1.6f;
                        EditorUIUtils.DrawCross(worldPos, size * 1.25f, new Color(baseColor.r, baseColor.g, baseColor.b, 0.8f));
                    }
                    EditorUIUtils.DrawCross(worldPos, size, c);
                }
            }
        }

        private void DrawAddModeSceneFrameWithLabel(SceneView sceneView, string label)
        {
            Handles.BeginGUI();
            try
            {
                var viewRect = new Rect(0f, 0f, sceneView.position.width, sceneView.position.height);
                float t = UIConstants.ADD_MODE_FRAME_THICKNESS;
                Color c = UIConstants.ADD_MODE_FRAME_COLOR;

                EditorGUI.DrawRect(new Rect(viewRect.x, viewRect.y, viewRect.width, t), c);
                EditorGUI.DrawRect(new Rect(viewRect.x, viewRect.yMax - t - UIConstants.ADD_MODE_BOTTOM_UI_OFFSET, viewRect.width, t), c);
                EditorGUI.DrawRect(new Rect(viewRect.x, viewRect.y, t, viewRect.height), c);
                EditorGUI.DrawRect(new Rect(viewRect.xMax - t, viewRect.y, t, viewRect.height), c);
                var style = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = UIConstants.ADD_MODE_LABEL_TEXT_COLOR },
                    alignment = TextAnchor.MiddleCenter
                };

                Vector2 size = style.CalcSize(new GUIContent(label));
                float padding = UIConstants.ADD_MODE_LABEL_PADDING;
                float margin = UIConstants.ADD_MODE_LABEL_MARGIN;
                Rect bgRect = new Rect(
                    viewRect.xMax - size.x - padding * 2f - margin,
                    viewRect.yMax - size.y - padding * 2f - UIConstants.ADD_MODE_BOTTOM_UI_OFFSET - margin,
                    size.x + padding * 2f,
                    size.y + padding * 2f
                );
                EditorGUI.DrawRect(bgRect, UIConstants.ADD_MODE_LABEL_BG);

                if (GUI.Button(bgRect, GUIContent.none, GUIStyle.none))
                {
                    addSphereOnClick = false;
                    addUVIslandOnClick = false;
                    EditorUIUtils.RefreshUI();
                    Event.current.Use();
                }

                Rect textRect = new Rect(bgRect.x + padding, bgRect.y + padding, size.x, size.y);
                GUI.Label(textRect, label, style);
            }
            finally
            {
                Handles.EndGUI();
            }
        }

        private void HandleSceneClicks()
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (addSphereOnClick)
                {
                    HandleAddSphereOnClick(e);
                }
                else if (addUVIslandOnClick)
                {
                    HandleAddUVIslandOnClick(e);
                }
            }
        }

        private void HandleAddSphereOnClick(Event e)
        {
            if (e.alt) return;

            var picked = HandleUtility.PickGameObject(e.mousePosition, false);
            if (picked == null) return;
            if (!picked.TryGetComponent<Renderer>(out var renderer)) return;

            if (!avatarRenderers.Contains(renderer) && !clothRenderers.Contains(renderer))
            {
                EditorUtility.DisplayDialog(UIConstants.ERROR_DIALOG_TITLE, UIConstants.ERROR_RENDERER_NOT_IN_LIST, UIConstants.ERROR_DIALOG_OK);
                return;
            }

            if (!SetupRaycastMesh(renderer))
            {
                EditorUtility.DisplayDialog(UIConstants.ERROR_DIALOG_TITLE, UIConstants.ERROR_MESH_NOT_FOUND + " " + renderer.name, UIConstants.ERROR_DIALOG_OK);
                return;
            }
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (_raycastHelperCollider != null && _raycastHelperCollider.Raycast(ray, out RaycastHit hit, EditorMeshUtils.RaycastMaxDistance))
            {
                Vector3 spherePos = EditorMeshUtils.RoundToPrecision(hit.point, UIConstants.POSITION_PRECISION);
                // クリック確定時のみ保存をスケジュール
                EditorUIUtils.RecordUndoSetDirtyAndScheduleSave(settings, UIConstants.UNDO_ADD_SPHERE_MASK);
                TryAddSphere(UIConstants.SPHERE_NAME_PREFIX + (settings.sphereMasks.Count + 1), spherePos, UIConstants.DEFAULT_RADIUS, UIConstants.GRADIENT_DEFAULT);
                // 保存はヘルパがスケジュール済み
                Repaint();
                e.Use();
            }
            else
            {
                EditorUtility.DisplayDialog(UIConstants.ERROR_DIALOG_TITLE, UIConstants.ERROR_RAYCAST_HIT_NOT_FOUND, UIConstants.ERROR_DIALOG_OK);
            }
        }

        private void HandleAddUVIslandOnClick(Event e)
        {
            if (e.alt) return;

            var picked = HandleUtility.PickGameObject(e.mousePosition, false);
            if (picked == null) return;
            if (!picked.TryGetComponent<Renderer>(out var renderer)) return;

            if (!avatarRenderers.Contains(renderer) && !clothRenderers.Contains(renderer))
            {
                EditorUtility.DisplayDialog(UIConstants.ERROR_DIALOG_TITLE, UIConstants.ERROR_RENDERER_NOT_IN_LIST, UIConstants.ERROR_DIALOG_OK);
                return;
            }

            if (!SetupRaycastMesh(renderer))
            {
                EditorUtility.DisplayDialog(UIConstants.ERROR_DIALOG_TITLE, UIConstants.ERROR_MESH_NOT_FOUND + " " + renderer.name, UIConstants.ERROR_DIALOG_OK);
                return;
            }

            Ray ray2 = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (_raycastHelperCollider != null && _raycastHelperCollider.Raycast(ray2, out RaycastHit hit2, EditorMeshUtils.RaycastMaxDistance))
            {
                Vector2 uv = hit2.textureCoord;
                // サブメッシュはコライダーのsharedMeshから判定
                int subIdx = DetermineSubmeshIndex(_raycastHelperCollider.sharedMesh, hit2.triangleIndex);
                string rendererPath = EditorPathUtils.GetGameObjectPath(renderer);

                    int existingIndex = FindExistingUVMarker(rendererPath, subIdx, uv, renderer);
                    if (existingIndex >= 0)
                    {
                        // クリック確定時のみ保存をスケジュール
                        EditorUIUtils.RecordUndoSetDirtyAndScheduleSave(settings, UIConstants.UNDO_ADD_UV_ISLAND_MASK);
                        settings.uvIslandMasks.RemoveAt(existingIndex);
                        if (selectedUVIslandIndex > existingIndex) { selectedUVIslandIndex--; }
                        else if (selectedUVIslandIndex == existingIndex) { selectedUVIslandIndex = 0; }
                        // 保存はヘルパがスケジュール済み
                        Repaint();
                        Mask.Generator.UI.TexturePreviewWindow.NotifyUVMasksChanged();
                        e.Use();
                        return;
                    }
                    else
                    {
                        // クリック確定時のみ保存をスケジュール
                        EditorUIUtils.RecordUndoSetDirtyAndScheduleSave(settings, UIConstants.UNDO_ADD_UV_ISLAND_MASK);
                    }

                    var data = new UVIslandMaskData
                    {
                        rendererPath = rendererPath,
                        submeshIndex = subIdx,
                        seedUV = uv,
                        uvPosition = uv,
                        targetMatName = TryGetMaterialName(renderer, subIdx),
                        seedWorldPos = hit2.point,
                        displayName = renderer.name,
                        markerColor = EditorUIUtils.GenerateMarkerColor(),
                        uvThreshold = 0.1f
                    };
                    settings.uvIslandMasks.Add(data);
                    selectedUVIslandIndex = Mathf.Max(0, settings.uvIslandMasks.Count - 1);
                    EditorUIUtils.SetDirtyAndScheduleSaveOnly(settings);
                    Repaint();
                    Mask.Generator.UI.TexturePreviewWindow.NotifyUVMasksChanged();
                    e.Use();
            }
            else
            {
                EditorUtility.DisplayDialog(UIConstants.ERROR_DIALOG_TITLE, UIConstants.ERROR_RAYCAST_HIT_NOT_FOUND, UIConstants.ERROR_DIALOG_OK);
            }
        }
    }
}

#endif


