using System;
using System.Collections.Generic;
using GroomingTool2.Managers;
using GroomingTool2.State;
using GroomingTool2.Utils;
using UnityEditor;
using UnityEngine;

namespace GroomingTool2.Core
{
    /// <summary>
    /// マスク選択処理を担当するクラス
    /// </summary>
    internal sealed class MaskSelectionHandler
    {
        private readonly State.UvIslandMaskState maskState;
        private readonly FurDataManager furDataManager;
        private readonly UndoManager undoManager;
        private readonly GroomingTool2MaterialManager materialManager;
        private readonly GroomingTool2UI ui;

        public MaskSelectionHandler(
            State.UvIslandMaskState maskState,
            FurDataManager furDataManager,
            UndoManager undoManager,
            GroomingTool2MaterialManager materialManager,
            GroomingTool2UI ui)
        {
            this.maskState = maskState ?? throw new ArgumentNullException(nameof(maskState));
            this.furDataManager = furDataManager ?? throw new ArgumentNullException(nameof(furDataManager));
            this.undoManager = undoManager ?? throw new ArgumentNullException(nameof(undoManager));
            this.materialManager = materialManager ?? throw new ArgumentNullException(nameof(materialManager));
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        }

        /// <summary>
        /// クリック選択を処理
        /// </summary>
        public void HandleClick(int x, int y, Event e)
        {
            // 選択演算を決定（Shift/Alt/Ctrl）
            var operation = Utils.IslandSelectionUtils.SelectionOperation.Replace;
            if (e.shift)
                operation = Utils.IslandSelectionUtils.SelectionOperation.Add;
            else if (e.alt)
                operation = Utils.IslandSelectionUtils.SelectionOperation.Subtract;
            else if (e.control)
                operation = Utils.IslandSelectionUtils.SelectionOperation.Toggle;

            // データ座標をUV座標に変換（0-1023 → 0.0-1.0）
            // Y軸を反転（データ座標：y=0が上、UnityのUV座標：v=0が下なので、1-yで変換）
            Vector2 seedUV = new Vector2(x / (float)Common.TexSize, 1f - (y / (float)Common.TexSize));

            HashSet<Vector2Int> island = new HashSet<Vector2Int>();
            // クリック座標をマスク座標系に変換
            // データ座標とマスク座標は同じ方向（y=0が上、y=1023が下）
            // RasterizeTriangleToMaskでUV座標（v=0が下）からマスク座標（y=0が上）への変換時にY軸を反転しているため、
            // データ座標をそのまま使用できる
            Vector2Int clickPixel = new Vector2Int(x, y);

            // 選択されたマテリアルのメッシュデータを取得
            var selectedMaterial = materialManager?.SelectedMaterial;
            if (selectedMaterial.HasValue)
            {
                var entry = selectedMaterial.Value;
                
                // 表示されているテクスチャを利用しているすべてのサブメッシュを処理
                // クリック座標を含む最初のアイランドのみを選択する
                if (entry.uvSets != null && entry.triangleSets != null)
                {
                    for (int i = 0; i < entry.uvSets.Count && i < entry.triangleSets.Count; i++)
                    {
                        Vector2[] uvs = entry.uvSets[i];
                        int[] triangles = entry.triangleSets[i];
                        
                        if (uvs != null && triangles != null)
                        {
                            // シード三角形を探す
                            int seedTriangle = EditorUvUtils.FindSeedTriangleByUV(triangles, uvs, seedUV);
                            if (seedTriangle >= 0)
                            {
                                // シード三角形が見つかったサブメッシュでアイランドを抽出
                                var subIsland = Utils.IslandSelectionUtils.ExtractUVIslandFromMesh(
                                    seedUV, uvs, triangles, maskState.BaseSelected);
                                
                                // クリック座標がこのアイランド内にある場合のみ選択
                                if (subIsland.Contains(clickPixel))
                                {
                                    // 最初に見つかったアイランドのみを使用
                                    island = subIsland;
                                    break; // ループを抜ける
                                }
                            }
                        }
                    }
                }
            }

            // どのUVアイランドにも含まれない場合、何も選択しない（空のセットのまま）
            // フォールバックは行わない
            
            // アイランドが見つかった場合のみ選択演算を適用
            if (island.Count > 0)
            {
                // 選択演算を適用
                Utils.IslandSelectionUtils.ApplySelectionOperation(island, maskState.BaseSelected, operation);
            }
            else
            {
                // アイランドが見つからない場合、Replace操作の場合は何もしない（既に空）
                // 他の操作（Add/Subtract/Toggle）の場合も何もしない
                if (operation == Utils.IslandSelectionUtils.SelectionOperation.Replace)
                {
                    // Replace操作で何も選択されない場合、マスクをクリアしない（現状維持）
                    // ユーザーは意図的に空の領域をクリックしたと解釈
                    return; // 何もせずに終了
                }
                // Add/Subtract/Toggle操作でアイランドが見つからない場合も何もしない
                return; // 何もせずに終了
            }
            maskState.RecalculateEffective();
            
            // Undo状態を保存（毛データとマスク状態を一緒に保存）
            undoManager.SaveState(furDataManager.Data, maskState, $"マスク選択({operation})");

            ui.NotifyMaskChanged();
        }

        /// <summary>
        /// 矩形選択を処理
        /// </summary>
        public void HandleRectangle(Vector2 start, Vector2 end, Event e)
        {
            int startX = Mathf.Clamp(Mathf.RoundToInt(start.x), 0, Common.TexSize - 1);
            int startY = Mathf.Clamp(Mathf.RoundToInt(start.y), 0, Common.TexSize - 1);
            int endX = Mathf.Clamp(Mathf.RoundToInt(end.x), 0, Common.TexSize - 1);
            int endY = Mathf.Clamp(Mathf.RoundToInt(end.y), 0, Common.TexSize - 1);

            var rect = new RectInt(
                Mathf.Min(startX, endX),
                Mathf.Min(startY, endY),
                Mathf.Abs(endX - startX) + 1,
                Mathf.Abs(endY - startY) + 1
            );

            // 選択演算を決定
            var operation = Utils.IslandSelectionUtils.SelectionOperation.Replace;
            if (e.shift)
                operation = Utils.IslandSelectionUtils.SelectionOperation.Add;
            else if (e.alt)
                operation = Utils.IslandSelectionUtils.SelectionOperation.Subtract;
            else if (e.control)
                operation = Utils.IslandSelectionUtils.SelectionOperation.Toggle;

            // 矩形内の島を抽出（簡易版：矩形内の全ピクセルを選択として扱う）
            var selectedPixels = new HashSet<Vector2Int>();
            
            for (int y = rect.yMin; y < rect.yMax && y < Common.TexSize; y++)
            {
                for (int x = rect.xMin; x < rect.xMax && x < Common.TexSize; x++)
                {
                    if (x >= 0 && y >= 0)
                    {
                        selectedPixels.Add(new Vector2Int(x, y));
                    }
                }
            }

            Utils.IslandSelectionUtils.ApplySelectionOperation(selectedPixels, maskState.BaseSelected, operation);
            maskState.RecalculateEffective();

            // Undo状態を保存（毛データとマスク状態を一緒に保存）
            undoManager.SaveState(furDataManager.Data, maskState, $"マスク矩形選択({operation})");

            ui.NotifyMaskChanged();
        }

        /// <summary>
        /// 投げ縄選択を処理
        /// </summary>
        public void HandleLasso(List<Vector2> lassoPoints, Event e)
        {
            if (lassoPoints == null || lassoPoints.Count < 3)
                return;

            // 選択演算を決定（Shift=加算、Ctrl=減算、通常=置換）
            var operation = Utils.IslandSelectionUtils.SelectionOperation.Replace;
            if (e.shift)
                operation = Utils.IslandSelectionUtils.SelectionOperation.Add;
            else if (e.control)
                operation = Utils.IslandSelectionUtils.SelectionOperation.Subtract;
            
            // 投げ縄内のピクセルを抽出（任意領域マスク）
            var selectedPixels = Utils.IslandSelectionUtils.ExtractPixelsInLasso(lassoPoints);
            
            // Replaceの場合は既存マスクをクリア
            if (operation == Utils.IslandSelectionUtils.SelectionOperation.Replace)
            {
                maskState.Clear();
            }
            
            Utils.IslandSelectionUtils.ApplySelectionOperation(selectedPixels, maskState.BaseSelected, operation);
            maskState.RecalculateEffective();

            // Undo状態を保存（毛データとマスク状態を一緒に保存）
            undoManager.SaveState(furDataManager.Data, maskState, $"マスク投げ縄選択({operation})");

            ui.NotifyMaskChanged();
        }
    }
}

