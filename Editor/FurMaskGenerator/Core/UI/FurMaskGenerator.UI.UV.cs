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
        // UVアイランド検出キャッシュ（パフォーマンス最適化用）
        private static readonly Dictionary<string, UVIslandCache> _uvIslandCache = new Dictionary<string, UVIslandCache>();
        private static readonly LinkedList<string> _cacheAccessOrder = new LinkedList<string>(); // LRU用のアクセス順序
        private const int MAX_CACHE_SIZE = 10; // 最大キャッシュサイズ
        private const int MAX_MEMORY_USAGE_MB = 50; // 最大メモリ使用量（MB）

        /// <summary>
        /// UVアイランド検出結果のキャッシュ構造
        /// </summary>
        private class UVIslandCache
        {
            public string rendererPath;
            public int submeshIndex;
            // meshSnapshot は未使用のため削除
            public Dictionary<int, HashSet<int>> triangleToIslandMap; // 三角形インデックス -> UVアイランド三角形集合
            public Dictionary<Vector2, int> uvToTriangleMap; // UV座標 -> 三角形インデックス（近似マッピング）
            public DateTime lastAccessed;
            public int meshVertexCount;
            public int meshTriangleCount;
            public long memoryUsageBytes; // メモリ使用量（バイト）

            public bool IsValid(Mesh currentMesh)
            {
                return currentMesh != null &&
                       meshVertexCount == currentMesh.vertexCount &&
                       meshTriangleCount == currentMesh.triangles?.Length / 3;
            }

            /// <summary>
            /// メモリ使用量を計算
            /// </summary>
            public long CalculateMemoryUsage()
            {
                long usage = 0;
                
                // Dictionaryのメモリ使用量を概算
                if (triangleToIslandMap != null)
                {
                    usage += triangleToIslandMap.Count * (sizeof(int) + sizeof(int) * 4); // キー + 平均的なHashSetサイズ
                }
                
                if (uvToTriangleMap != null)
                {
                    usage += uvToTriangleMap.Count * (sizeof(float) * 2 + sizeof(int)); // Vector2 + int
                }
                
                memoryUsageBytes = usage;
                return usage;
            }
        }

        // UV UI methods
        void DrawUVIslandClickUI()
        {
            UIDrawingUtils.DrawInUIBox(() =>
            {
                // タイトル付きフォールドアウト（共通ヘルパ使用）
                foldoutUVSection = UIDrawingUtils.DrawSectionFoldout(foldoutUVSection, UILabels.UV_SECTION_TITLE);

                if (!foldoutUVSection)
                {
                    return;
                }

                // UV島表示設定
                showUVMarkers = EditorGUILayout.Toggle(UILabels.UV_SHOW_TOGGLE, showUVMarkers);

                // UVマスクプレビュー表示ボタン（常時押下可能）
                if (GUILayout.Button(UILabels.UV_MASK_PREVIEW_BUTTON))
                {
                    ShowUVMaskPreview();
                }

                EditorGUILayout.Space();
                
                // マスク追加ボタン（表示トグル直下へ移動）
                DrawUVIslandActionButtons();

                // 近傍関連パラメータはレイ判定方式に統合したためUIから廃止

                EditorGUILayout.Space();

                // UV島リスト
                DrawUVIslandList();
            });
        }

        private void DrawUVIslandList()
        {
            if (settings?.uvIslandMasks == null) return;

            for (int i = 0; i < settings.uvIslandMasks.Count; i++)
            {
                DrawUVIslandListItem(i);
            }
        }

        private void DrawUVIslandListItem(int index)
        {
            var uvIsland = settings.uvIslandMasks[index];
            if (uvIsland == null) return;

            EditorGUILayout.BeginHorizontal();

            // カラースウォッチ（クリックで選択）
            bool isSelected = (index == selectedUVIslandIndex);
            if (uvIsland.markerColor.a <= 0f)
            {
                uvIsland.markerColor = ColorGenerator.GenerateMarkerColor();
                UndoRedoUtils.SetDirtyOnly(settings);
            }
            float rowH = EditorGUIUtility.singleLineHeight;
            Rect swatchRect = EditorGUILayout.GetControlRect(false, rowH, GUILayout.ExpandWidth(true));
            if (isSelected)
            {
                EditorGUI.DrawRect(swatchRect, new Color(0.3f, 0.8f, 1f, 0.4f)); // より鮮やかな青
            }
            Rect colorRect = new Rect(swatchRect.x + 2, swatchRect.y + 2, swatchRect.width - 4, swatchRect.height - 4);
            EditorGUI.DrawRect(colorRect, uvIsland.markerColor.a > 0f ? uvIsland.markerColor : ColorGenerator.GenerateMarkerColor());
            Event ev = Event.current;
            if (ev.type == EventType.MouseDown && swatchRect.Contains(ev.mousePosition))
            {
                selectedUVIslandIndex = index;
                ev.Use();
            }

            // ラベルは非表示（旧仕様どおりスウォッチのみ）

            // 削除ボタン
            if (GUILayout.Button(UILabels.DELETE_BUTTON, GUILayout.Width(AppSettings.DELETE_BUTTON_WIDTH)))
            {
                UndoRedoUtils.RecordUndoSetDirtyAndScheduleSave(settings, UndoMessages.ADD_UV_ISLAND_MASK);
                settings.uvIslandMasks.RemoveAt(index);
                if (selectedUVIslandIndex >= index) selectedUVIslandIndex = Mathf.Max(0, selectedUVIslandIndex - 1);
                UIDrawingUtils.RefreshUI();
                NolaTools.FurMaskGenerator.UI.TexturePreviewWindow.NotifyUVMasksChanged();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawUVIslandActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            // シーン上クリックでUVマスクを追加するトグル（ボタンスタイル）
            bool newAddToggle = GUILayout.Toggle(addUVIslandOnClick, UILabels.ADD_UV_ISLAND_BUTTON, GUI.skin.button);
            if (newAddToggle != addUVIslandOnClick)
            {
                addUVIslandOnClick = newAddToggle;
                if (addUVIslandOnClick)
                {
                    // スフィア追加モードと排他にする
                    addSphereOnClick = false;
                }
                UIDrawingUtils.RefreshUI();
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// UVマスク可視化用のテクスチャプレビューを表示
        /// </summary>
        private void ShowUVMaskPreview()
        {
            // 1) マスクが空なら、対象レンダラーのテクスチャを列挙して、切替可能な単一ウィンドウで開く
            if (settings?.uvIslandMasks == null || settings.uvIslandMasks.Count == 0)
            {
                var rs = new List<Renderer>();
                if (avatarRenderers != null) rs.AddRange(avatarRenderers);
                if (clothRenderers != null) rs.AddRange(clothRenderers);
                if (rs.Count == 0)
                {
                    EditorUtility.DisplayDialog(UILabels.ERROR_DIALOG_TITLE, ErrorMessages.ERROR_RENDERERS_NOT_SET, UILabels.ERROR_DIALOG_OK);
                    return;
                }

                var multiTargets = new List<(Renderer renderer, int submesh, Texture2D texture, string label)>();
                foreach (var r in rs)
                {
                    if (r == null || r.sharedMaterials == null || r.sharedMaterials.Length == 0) continue;
                    for (int sub = 0; sub < r.sharedMaterials.Length; sub++)
                    {
                        var tex = GetMainTextureForRenderer(r, sub);
                        if (tex == null) continue;
                        string label = r.name + " / Sub " + sub + " / " + (r.sharedMaterials[sub] != null ? r.sharedMaterials[sub].name : tex.name);
                        multiTargets.Add((r, sub, tex, label));
                    }
                }
                if (multiTargets.Count == 0)
                {
                    EditorUtility.DisplayDialog(UILabels.ERROR_DIALOG_TITLE, ErrorMessages.ERROR_VALID_UV_MASKS_NOT_FOUND, UILabels.ERROR_DIALOG_OK);
                    return;
                }

                NolaTools.FurMaskGenerator.UI.TexturePreviewWindow.ShowWindowWithTargets(
                    settings.uvIslandMasks,
                    multiTargets,
                    0,
                    (newMask) =>
                    {
                        if (newMask == null) return;
                        if (settings == null) return;
                        AddUvIslandMask(newMask);
                    },
                    (removeMask) =>
                    {
                        if (removeMask == null) return;
                        if (settings == null || settings.uvIslandMasks == null) return;
                        RemoveUvIslandMask(removeMask);
                    }
                );
                return;
            }

            // 2) 既存マスクあり: すべての使用テクスチャを列挙して、プルダウンで切替可能な単一ウィンドウで開く
            var allRenderers = new List<Renderer>();
            if (avatarRenderers != null) allRenderers.AddRange(avatarRenderers);
            if (clothRenderers != null) allRenderers.AddRange(clothRenderers);
            if (allRenderers.Count == 0)
            {
                EditorUtility.DisplayDialog(UILabels.ERROR_DIALOG_TITLE, ErrorMessages.ERROR_RENDERERS_NOT_SET, UILabels.ERROR_DIALOG_OK);
                return;
            }

            var allTargets = new List<(Renderer renderer, int submesh, Texture2D texture, string label)>();
            foreach (var r in allRenderers)
            {
                if (r == null || r.sharedMaterials == null || r.sharedMaterials.Length == 0) continue;
                for (int sub = 0; sub < r.sharedMaterials.Length; sub++)
                {
                    var tex = GetMainTextureForRenderer(r, sub);
                    if (tex == null) continue;
                    string label = r.name + " / Sub " + sub + " / " + (r.sharedMaterials[sub] != null ? r.sharedMaterials[sub].name : tex.name);
                    allTargets.Add((r, sub, tex, label));
                }
            }

            if (allTargets.Count == 0)
            {
                EditorUtility.DisplayDialog(UILabels.ERROR_DIALOG_TITLE, ErrorMessages.ERROR_VALID_UV_MASKS_NOT_FOUND, UILabels.ERROR_DIALOG_OK);
                return;
            }

            // 初期表示インデックスを決定（選択中のUV島、なければ最初のマスク）
            int initialIndex = 0;
            UVIslandMaskData seedMask = null;
            if (selectedUVIslandIndex >= 0 && selectedUVIslandIndex < settings.uvIslandMasks.Count)
            {
                seedMask = settings.uvIslandMasks[selectedUVIslandIndex];
            }
            if (seedMask == null)
            {
                seedMask = settings.uvIslandMasks.FirstOrDefault(m => !string.IsNullOrEmpty(m?.rendererPath));
            }
            if (seedMask != null)
            {
                var seedRenderer = EditorPathUtils.FindGameObjectByPath(seedMask.rendererPath)?.GetComponent<Renderer>();
                if (seedRenderer != null)
                {
                    for (int i = 0; i < allTargets.Count; i++)
                    {
                        if (allTargets[i].renderer == seedRenderer && allTargets[i].submesh == seedMask.submeshIndex)
                        {
                            initialIndex = i;
                            break;
                        }
                    }
                }
            }

            NolaTools.FurMaskGenerator.UI.TexturePreviewWindow.ShowWindowWithTargets(
                settings.uvIslandMasks,
                allTargets,
                initialIndex,
                (newMask) =>
                {
                    if (newMask == null) return;
                    if (settings == null) return;
                    AddUvIslandMask(newMask);
                },
                (removeMask) =>
                {
                    if (removeMask == null) return;
                    if (settings == null || settings.uvIslandMasks == null) return;
                    RemoveUvIslandMask(removeMask);
                }
            );
        }

        private void AddUvIslandMask(UVIslandMaskData newMask)
        {
            UndoRedoUtils.RecordUndoSetDirtyAndScheduleSave(settings, UndoMessages.ADD_UV_ISLAND_MASK);
            if (settings.uvIslandMasks == null) { settings.uvIslandMasks = new List<UVIslandMaskData>(); }
            settings.uvIslandMasks.Add(newMask);
            UIDrawingUtils.RefreshUI();
            NolaTools.FurMaskGenerator.UI.TexturePreviewWindow.NotifyUVMasksChanged();
        }

        private void RemoveUvIslandMask(UVIslandMaskData removeMask)
        {
            UndoRedoUtils.RecordUndoSetDirtyAndScheduleSave(settings, UndoMessages.ADD_UV_ISLAND_MASK);
            settings.uvIslandMasks.Remove(removeMask);
            UIDrawingUtils.RefreshUI();
            NolaTools.FurMaskGenerator.UI.TexturePreviewWindow.NotifyUVMasksChanged();
        }

        /// <summary>
        /// 既存のUVマーカーで同じレンダラー・サブメッシュ・UVアイランド内のものを検索（軽量化版）
        /// </summary>
        private int FindExistingUVMarker(string rendererPath, int submeshIndex, Vector2 uv, Renderer renderer)
        {
            if (settings?.uvIslandMasks == null || string.IsNullOrEmpty(rendererPath) || renderer == null)
                return -1;

            // 同一レンダラー・サブメッシュの既存マーカーのみを対象に絞り込み（早期フィルタリング）
            var candidateMarkers = new List<int>();
            for (int i = 0; i < settings.uvIslandMasks.Count; i++)
            {
                var existing = settings.uvIslandMasks[i];
                if (existing?.rendererPath == rendererPath && existing.submeshIndex == submeshIndex)
                {
                    candidateMarkers.Add(i);
                }
            }

            if (candidateMarkers.Count == 0)
                return -1; // 同一レンダラー・サブメッシュに既存マーカーなし

            // メッシュを取得
            Mesh mesh = EditorMeshUtils.GetMeshForRenderer(renderer, out bool isBakedTempMesh);
            if (mesh == null || mesh.vertexCount == 0)
                return -1;

            try
            {
                // キャッシュから高速検索を試行（tris/uvs を一度だけ取得して再利用）
                var perSubCache = NolaTools.FurMaskGenerator.Utils.EditorUvUtils.GetOrBuildIslandCache(rendererPath, mesh, submeshIndex);
                
                if (perSubCache != null)
                {
                    int[] tris = mesh.GetTriangles(submeshIndex);
                    Vector2[] uvs = mesh.uv;
                    int triCount = tris.Length / 3;
                    
                    // キャッシュを使用した高速チェック（重心最寄り）
                    int newTriangleIndex = -1;
                    float best = float.MaxValue;
                    for (int ti = 0; ti < triCount; ti++)
                    {
                        int a = tris[ti*3], b = tris[ti*3+1], c = tris[ti*3+2];
                        if (a >= uvs.Length || b >= uvs.Length || c >= uvs.Length) continue;
                        Vector2 centroid = (uvs[a] + uvs[b] + uvs[c]) / 3f;
                        float d = (centroid - uv).sqrMagnitude;
                        if (d < best) { best = d; newTriangleIndex = ti; }
                    }
                    if (newTriangleIndex >= 0 && perSubCache.TryGetValue(newTriangleIndex, out var newIslandTriangles))
                    {
                        // 既存マーカーとの重複チェック
                        foreach (int markerIndex in candidateMarkers)
                        {
                            var existing = settings.uvIslandMasks[markerIndex];
                            int existingTriangleIndex = -1;
                            float best2 = float.MaxValue;
                            for (int ti = 0; ti < triCount; ti++)
                            {
                                int a = tris[ti*3], b = tris[ti*3+1], c = tris[ti*3+2];
                                if (a >= uvs.Length || b >= uvs.Length || c >= uvs.Length) continue;
                                Vector2 centroid = (uvs[a] + uvs[b] + uvs[c]) / 3f;
                                float d = (centroid - existing.seedUV).sqrMagnitude;
                                if (d < best2) { best2 = d; existingTriangleIndex = ti; }
                            }
                            if (existingTriangleIndex >= 0 && perSubCache.TryGetValue(existingTriangleIndex, out var existingIslandTriangles))
                            {
                                if (newIslandTriangles.Overlaps(existingIslandTriangles))
                                {
                                    return markerIndex; // 重複発見
                                }
                            }
                        }
                    }
                }
                else
                {
                    // キャッシュが無効な場合は従来の方法でフォールバック（最小限の処理）
                    return FindExistingUVMarkerFallback(mesh, submeshIndex, uv, candidateMarkers);
                }

                return -1; // 重複なし
            }
            finally
            {
                if (isBakedTempMesh)
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
        }

        /// <summary>
        /// フォールバック用の軽量重複チェック（簡易UV距離判定）
        /// </summary>
        private int FindExistingUVMarkerFallback(Mesh mesh, int submeshIndex, Vector2 uv, List<int> candidateMarkers)
        {
            // 簡易版: UV距離による近似判定（UVアイランド検出を省略）
            const float SIMPLE_UV_THRESHOLD = 0.05f; // 簡易判定用の閾値

            foreach (int markerIndex in candidateMarkers)
            {
                var existing = settings.uvIslandMasks[markerIndex];
                float uvDistance = Vector2.Distance(existing.seedUV, uv);
                if (uvDistance <= SIMPLE_UV_THRESHOLD)
                {
                    return markerIndex; // 近接重複として判定
                }
            }

            return -1;
        }
    }
}

#endif
