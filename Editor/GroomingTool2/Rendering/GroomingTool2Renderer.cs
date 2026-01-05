using System.Collections.Generic;
using GroomingTool2.Core;
using GroomingTool2.Managers;
using GroomingTool2.State;
using UnityEditor;
using UnityEngine;

namespace GroomingTool2.Rendering
{
    /// <summary>
    /// マスク選択プレビュー描画用パラメータ
    /// </summary>
    internal struct MaskPreviewParams
    {
        public bool Active;
        public MaskSelectionMode Mode;
        public Vector2 RectStartData;
        public Vector2 RectEndData;
        public List<Vector2> LassoPointsData;
    }

    /// <summary>
    /// キャンバス描画に必要なパラメータ
    /// </summary>
    internal struct CanvasDrawParams
    {
        public Rect CanvasRect;
        public Rect ViewRect;
        public float Scale;
        public int DisplayInterval;
        public Texture2D Background;
        public Vector2 ScrollOffsetData;
        public Vector2 MousePosContent;
        public bool DrawBrushCursor;
        public Vector2[] Uv;
        public int[] Triangles;
        public List<Vector2[]> UvSets;
        public List<int[]> TriangleSets;
        public UvIslandMaskState MaskState;
        public bool ShowMaskDarkening;
        public Color WireframeColor;
        public MaskPreviewParams MaskPreview;
    }

    /// <summary>
    /// キャンバスの描画を統合管理するクラス
    /// </summary>
    internal sealed class GroomingTool2Renderer
    {
        private readonly BrushManager brushManager;
        private readonly TextureManager textureManager;
        private readonly WireframeRenderer wireframeRenderer;
        private readonly FurDataRenderer furDataRenderer;
        private readonly GpuFurDataRenderer gpuFurDataRenderer;
        
        // GPUレンダラーが利用可能かどうか（環境依存）
        private readonly bool gpuAvailable;
        private readonly bool wireframeGpuAvailable;
        
        // ユーザーがGPUレンダリングを希望しているかどうか
        private bool preferGpuRendering = true;
        
        // 実際に使用するレンダリングモード（GPU可用性とユーザー設定の組み合わせ）
        private bool useGpuRendering;
        private bool useGpuWireframe;
        
        // マスクのキャッシュ用：前回描画時のマスク状態の参照を保持
        private bool[,] cachedMaskState;

        public GroomingTool2Renderer(BrushManager brushManager, FurDataManager furDataManager)
        {
            this.brushManager = brushManager;
            textureManager = new TextureManager();
            wireframeRenderer = new WireframeRenderer(textureManager);
            furDataRenderer = new FurDataRenderer(textureManager, furDataManager);
            gpuFurDataRenderer = new GpuFurDataRenderer(furDataManager);
            
            // GPUレンダラーの可用性をチェック
            gpuAvailable = gpuFurDataRenderer.IsAvailable;
            wireframeGpuAvailable = wireframeRenderer.IsAvailable;
            
            // 初期レンダリングモードを設定
            UpdateRenderingMode();
            
            // 初期化時にGPUが利用不可の場合はログ出力
            if (!gpuAvailable)
            {
                Debug.Log("GroomingTool2: GPU fur data rendering is not available on this system.");
            }
            if (!wireframeGpuAvailable)
            {
                Debug.Log("GroomingTool2: GPU wireframe rendering is not available on this system.");
            }
        }
        
        /// <summary>
        /// レンダリングモードを設定する（GPUまたはCPU）
        /// </summary>
        /// <param name="useGpu">true: GPU優先、false: CPU使用</param>
        public void SetRenderingMode(bool useGpu)
        {
            preferGpuRendering = useGpu;
            UpdateRenderingMode();
        }
        
        /// <summary>
        /// 実際のレンダリングモードを更新（GPU可用性とユーザー設定を考慮）
        /// </summary>
        private void UpdateRenderingMode()
        {
            useGpuRendering = preferGpuRendering && gpuAvailable;
            useGpuWireframe = preferGpuRendering && wireframeGpuAvailable;
        }

        public void Dispose()
        {
            textureManager?.Dispose();
            gpuFurDataRenderer?.Dispose();
            wireframeRenderer?.Dispose();
        }

        public void DrawCanvas(CanvasDrawParams drawParams)
        {
            textureManager.EnsurePreviewTexture();

            // 固定キャンバス内でのローカル描画
            GUI.BeginGroup(drawParams.CanvasRect);

            // 描画レイヤ順序：
            // 1. 背景テクスチャ
            DrawBackground(drawParams);
            // 2. ワイヤーフレーム（UVメッシュ）
            DrawWireframe(drawParams);
            // 3. マスク暗化（非選択領域を暗く）
            if (drawParams.MaskState != null && drawParams.ShowMaskDarkening)
            {
                DrawMaskDarkening(drawParams);
            }
            // 4. 毛データ（ドットとライン）
            DrawFurData(drawParams);
            // 5. オーバーレイ（UVオーバーレイ、マスクプレビュー、ブラシカーソル）
            DrawOverlays(drawParams);

            GUI.EndGroup();
        }

        private void DrawBackground(CanvasDrawParams p)
        {
            var drawRect = GetCanvasDrawRect(p.ScrollOffsetData, p.Scale);
            if (p.Background != null)
                GUI.DrawTexture(drawRect, p.Background, ScaleMode.StretchToFill);
            else
                GUI.DrawTexture(drawRect, textureManager.PreviewTexture, ScaleMode.StretchToFill);
        }

        private void DrawWireframe(CanvasDrawParams drawParams)
        {
            if (drawParams.Background == null || drawParams.UvSets == null || drawParams.TriangleSets == null || drawParams.UvSets.Count == 0)
                return;
            
            Handles.BeginGUI();
            
            if (useGpuWireframe)
            {
                // GPU描画：GL.LINESを使用
                wireframeRenderer.Draw(drawParams.CanvasRect, drawParams.Background, drawParams.UvSets, drawParams.TriangleSets, drawParams.Scale, drawParams.ScrollOffsetData);
            }
            else
            {
                // CPU描画（フォールバック）：Handles.DrawLineを使用
                DrawWireframeCpu(drawParams);
            }
            
            Handles.EndGUI();
        }
        
        /// <summary>
        /// CPU描画によるワイヤーフレーム描画（GPUが使えない場合のフォールバック）
        /// </summary>
        private void DrawWireframeCpu(CanvasDrawParams drawParams)
        {
            Handles.color = drawParams.WireframeColor;
            
            for (int s = 0; s < drawParams.UvSets.Count && s < drawParams.TriangleSets.Count; s++)
            {
                var uv = drawParams.UvSets[s];
                var tris = drawParams.TriangleSets[s];
                if (uv == null || tris == null || uv.Length == 0 || tris.Length < 3)
                    continue;
                
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int i0 = tris[i];
                    int i1 = tris[i + 1];
                    int i2 = tris[i + 2];
                    
                    // UVをビューローカル座標に変換（canvasRect原点からの相対座標）
                    var p0 = CoordinateUtils.UvToViewLocal(uv[i0], drawParams.Scale, drawParams.ScrollOffsetData);
                    var p1 = CoordinateUtils.UvToViewLocal(uv[i1], drawParams.Scale, drawParams.ScrollOffsetData);
                    var p2 = CoordinateUtils.UvToViewLocal(uv[i2], drawParams.Scale, drawParams.ScrollOffsetData);
                    
                    // 三角形の3辺を描画
                    Handles.DrawLine(p0, p1);
                    Handles.DrawLine(p1, p2);
                    Handles.DrawLine(p2, p0);
                }
            }
        }

        private void DrawFurData(CanvasDrawParams drawParams)
        {
            var effectiveInterval = drawParams.DisplayInterval;
            
            // マスク情報を取得
            bool[,] effectiveMask = drawParams.MaskState?.EffectiveSelected;
            bool hasMaskSelection = drawParams.MaskState?.HasAnySelection ?? false;
            
            if (useGpuRendering)
            {
                // GPU描画：GL.Begin/Endを使用
                Handles.BeginGUI();
                gpuFurDataRenderer.Draw(drawParams.ViewRect, drawParams.Scale, effectiveInterval, drawParams.ScrollOffsetData, effectiveMask, hasMaskSelection);
                Handles.EndGUI();
                return;
            }
            
            // CPU描画（フォールバック）：テクスチャベース
            var dotsDrawRect = furDataRenderer.Draw(drawParams.ViewRect, drawParams.Scale, effectiveInterval, drawParams.ScrollOffsetData, effectiveMask, hasMaskSelection);
            
            if (textureManager.DotsTexture != null && dotsDrawRect.HasValue)
            {
                var drawRect = dotsDrawRect.Value; // グループ内ローカル
                GUI.DrawTexture(drawRect, textureManager.DotsTexture, ScaleMode.StretchToFill, true);
            }
        }

        private void DrawOverlays(CanvasDrawParams drawParams)
        {
            Handles.BeginGUI();

            if (drawParams.Uv != null && drawParams.Triangles != null && drawParams.Background != null)
            {
                OverlayRenderer.DrawUvOverlay(drawParams.Uv, drawParams.Triangles, drawParams.Scale, drawParams.ScrollOffsetData, drawParams.WireframeColor);
            }
            
            // マスク選択プレビューの描画
            if (drawParams.MaskPreview.Active)
            {
                OverlayRenderer.DrawMaskPreview(drawParams.MaskPreview, drawParams.Scale, drawParams.ScrollOffsetData);
            }

            if (drawParams.DrawBrushCursor)
            {
                OverlayRenderer.DrawBrushCursor(drawParams.MousePosContent, brushManager.BrushSize * drawParams.Scale, brushManager.FurColorPrimary);
            }
            
            Handles.EndGUI();
        }

        public void InvalidateWireframe()
        {
            wireframeRenderer.Invalidate();
        }

        public void SetWireframeColor(Color color)
        {
            wireframeRenderer.SetColor(color);
        }

        public void SaveNormalMap(string path)
        {
            furDataRenderer.SaveNormalMap(path);
        }

        /// <summary>
        /// マスクの暗化描画（非選択領域を暗く）
        /// テクスチャベースの描画でパフォーマンスを改善
        /// </summary>
        private void DrawMaskDarkening(CanvasDrawParams drawParams)
        {
            var maskState = drawParams.MaskState;
            if (maskState?.EffectiveSelected == null)
                return;

            var effective = maskState.EffectiveSelected;
            
            // マスク状態が変更された場合のみテクスチャを更新
            bool maskChanged = cachedMaskState != effective;
            if (maskChanged)
            {
                // MaskStateのHasAnySelectionを使用して全ピクセル走査を省略
                textureManager.UpdateDarkeningTexture(effective, maskState.HasAnySelection);
                cachedMaskState = effective;
            }

            // テクスチャを描画（一度の呼び出しで全体を描画）
            if (textureManager.DarkeningTexture != null)
            {
                var drawRect = GetCanvasDrawRect(drawParams.ScrollOffsetData, drawParams.Scale);
                GUI.DrawTexture(drawRect, textureManager.DarkeningTexture, ScaleMode.StretchToFill, true);
            }
        }

        private Rect GetCanvasDrawRect(Vector2 scrollOffset, float scale)
        {
            return new Rect(
                -scrollOffset.x * scale,
                -scrollOffset.y * scale,
                Common.TexSize * scale,
                Common.TexSize * scale
            );
        }
        
        /// <summary>
        /// マスク状態のキャッシュをクリア（マスク変更時に呼び出す）
        /// </summary>
        public void InvalidateMaskCache()
        {
            cachedMaskState = null;
            textureManager.InvalidateDarkeningTexture();
        }
    }
}



