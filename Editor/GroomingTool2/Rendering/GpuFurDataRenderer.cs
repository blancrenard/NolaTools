using System;
using System.Runtime.CompilerServices;
using GroomingTool2.Core;
using GroomingTool2.Managers;
using GroomingTool2.Utils;
using UnityEngine;

namespace GroomingTool2.Rendering
{
    /// <summary>
    /// GPU描画を使用した毛データ（ドットとライン）のレンダラー
    /// GL.Begin/Endを使用したイミディエイトモード描画
    /// </summary>
    internal sealed class GpuFurDataRenderer : IDisposable
    {
        private readonly FurDataManager furDataManager;
        private Material glMaterial;

        // 円描画のセグメント数（16でより滑らかに）
        private const int CircleSegments = 16;
        
        // 事前計算したsin/cos値（円描画の高速化）
        private static readonly float[] CircleSin = new float[CircleSegments + 1];
        private static readonly float[] CircleCos = new float[CircleSegments + 1];

        /// <summary>
        /// GPU描画が利用可能かどうか（シェーダーとマテリアルが正常に作成された場合true）
        /// </summary>
        public bool IsAvailable { get; private set; }

        // 描画データ構造体（1回のループでデータを収集）
        private struct DrawData
        {
            public float cx, cy;
            public Color dotColor;
            public float lineEndX, lineEndY;
            public bool hasLine;
            public bool isMasked;
        }

        static GpuFurDataRenderer()
        {
            for (int i = 0; i <= CircleSegments; i++)
            {
                float angle = (float)i / CircleSegments * Mathf.PI * 2f;
                CircleSin[i] = Mathf.Sin(angle);
                CircleCos[i] = Mathf.Cos(angle);
            }
        }

        public GpuFurDataRenderer(FurDataManager furDataManager)
        {
            this.furDataManager = furDataManager ?? throw new ArgumentNullException(nameof(furDataManager));
            CreateMaterial();
        }

        public void Dispose()
        {
            if (glMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(glMaterial);
                glMaterial = null;
            }
            IsAvailable = false;
        }

        private void CreateMaterial()
        {
            IsAvailable = false;
            
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                Debug.LogWarning("GpuFurDataRenderer: Hidden/Internal-Colored shader not found. Falling back to CPU rendering.");
                return;
            }

            try
            {
                glMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                glMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                glMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                glMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                glMaterial.SetInt("_ZWrite", 0);
                glMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                
                IsAvailable = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"GpuFurDataRenderer: Failed to create material. Falling back to CPU rendering. Error: {ex.Message}");
                if (glMaterial != null)
                {
                    UnityEngine.Object.DestroyImmediate(glMaterial);
                    glMaterial = null;
                }
            }
        }

        /// <summary>
        /// 毛データをGPU描画する
        /// Handles.BeginGUI() のコンテキスト内で呼び出すこと
        /// </summary>
        public void Draw(Rect viewRect, float scale, int interval, Vector2 scrollOffsetData, bool[,] effectiveMask = null, bool hasMaskSelection = false)
        {
            if (glMaterial == null || furDataManager?.Data == null)
                return;

            if (!FurRenderParams.HasVisibleArea(viewRect, scale, scrollOffsetData))
                return;

            float screenInterval = FurRenderParams.CalculateScreenInterval(viewRect, interval);
            var furData = furDataManager.Data;
            var clip = ClippingUtils.ClipRect.FromCanvasRect(viewRect);

            // GL描画開始
            GL.PushMatrix();
            glMaterial.SetPass(0);
            GL.LoadPixelMatrix();

            // データ空間のイテレーション範囲を計算
            var grid = FurRenderParams.DotGridRange.Calculate(screenInterval, scale, scrollOffsetData, viewRect.width, viewRect.height);
            float maxLinePx = Mathf.Max(1f, screenInterval * FurRenderParams.MaxLineLengthRatio);

            // バッファサイズ確保
            int maxPointsX = Mathf.Max(1, Mathf.CeilToInt((grid.EndX - grid.StartX) / grid.Step)) + 1;
            int maxPointsY = Mathf.Max(1, Mathf.CeilToInt((grid.EndY - grid.StartY) / grid.Step)) + 1;
            int maxPoints = maxPointsX * maxPointsY;

            Span<DrawData> drawDataBuffer = maxPoints <= 1024 
                ? stackalloc DrawData[maxPoints] 
                : new DrawData[maxPoints];
            int drawCount = 0;

            // 描画データを収集（浮動小数点ステップで正確な画面間隔を保つ）
            for (float dataY = grid.StartY; dataY < grid.EndY; dataY += grid.Step)
            {
                for (float dataX = grid.StartX; dataX < grid.EndX; dataX += grid.Step)
                {
                    int x = Mathf.RoundToInt(dataX);
                    int y = Mathf.RoundToInt(dataY);
                    if ((uint)x >= (uint)Common.TexSize || (uint)y >= (uint)Common.TexSize)
                        continue;

                    int index = Common.GetIndex(x, y);
                    var data = furData[index];

                    float cos = AngleLut.GetCos(data.Dir);
                    float sin = AngleLut.GetSin(data.Dir);

                    float powerDot = data.Inclined * Common.Grid;
                    float dxDot = powerDot * cos;
                    float dyDot = powerDot * sin;
                    if (dxDot * dxDot + dyDot * dyDot < 0.25f) // dotLength < 0.5
                        continue;

                    // 浮動小数点データ座標から画面ピクセル座標へ変換（正確な間隔を保つ）
                    float cx = (dataX - scrollOffsetData.x) * scale;
                    float cy = (dataY - scrollOffsetData.y) * scale;

                    Color dotColor = NormalMapColorUtils.GetNormalMapColor(data);
                    
                    bool isMasked = hasMaskSelection && effectiveMask != null && !effectiveMask[x, y];
                    if (isMasked)
                    {
                        dotColor.r *= FurRenderParams.MaskedDarkenFactor;
                        dotColor.g *= FurRenderParams.MaskedDarkenFactor;
                        dotColor.b *= FurRenderParams.MaskedDarkenFactor;
                    }

                    // ライン終点を計算
                    float lineMultiplier = 0.5f + data.Inclined * 1.58f;
                    float powerLinePx = data.Inclined * Common.Grid * lineMultiplier;
                    float dxLinePx = powerLinePx * cos;
                    float dyLinePx = powerLinePx * sin;
                    float lenPx = Mathf.Sqrt(dxLinePx * dxLinePx + dyLinePx * dyLinePx);
                    
                    if (lenPx > maxLinePx && lenPx > 0.5f)
                    {
                        float s = maxLinePx / lenPx;
                        dxLinePx *= s;
                        dyLinePx *= s;
                    }

                    drawDataBuffer[drawCount++] = new DrawData
                    {
                        cx = cx,
                        cy = cy,
                        dotColor = dotColor,
                        lineEndX = cx + dxLinePx,
                        lineEndY = cy + dyLinePx,
                        hasLine = lenPx >= 0.5f,
                        isMasked = isMasked
                    };
                }
            }

            // ドットを描画（ラインの下に描画されるように先に描画）
            GL.Begin(GL.TRIANGLES);
            for (int i = 0; i < drawCount; i++)
            {
                ref readonly var d = ref drawDataBuffer[i];
                if (d.cx - FurRenderParams.GpuFixedDotRadius < clip.MinX || d.cx + FurRenderParams.GpuFixedDotRadius > clip.MaxX ||
                    d.cy - FurRenderParams.GpuFixedDotRadius < clip.MinY || d.cy + FurRenderParams.GpuFixedDotRadius > clip.MaxY)
                    continue;
                DrawCircleVertices(d.cx, d.cy, FurRenderParams.GpuFixedDotRadius, d.dotColor);
            }
            GL.End();

            // ラインを描画（ドットの上に描画）
            GL.Begin(GL.LINES);
            for (int i = 0; i < drawCount; i++)
            {
                ref readonly var d = ref drawDataBuffer[i];
                if (d.hasLine)
                {
                    Color lineColor = d.isMasked
                        ? new Color(FurRenderParams.MaskedDarkenFactor, FurRenderParams.MaskedDarkenFactor, FurRenderParams.MaskedDarkenFactor)
                        : Color.white;
                    GL.Color(lineColor);
                    ClippingUtils.DrawClippedLine(d.cx, d.cy, d.lineEndX, d.lineEndY, clip);
                }
            }
            GL.End();

            GL.PopMatrix();
        }

        /// <summary>
        /// 円を三角形ファンで描画（GL.TRIANGLESモード内で呼び出す）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DrawCircleVertices(float cx, float cy, float radius, Color color)
        {
            GL.Color(color);

            for (int i = 0; i < CircleSegments; i++)
            {
                GL.Vertex3(cx, cy, 0);
                GL.Vertex3(cx + CircleCos[i] * radius, cy + CircleSin[i] * radius, 0);
                GL.Vertex3(cx + CircleCos[i + 1] * radius, cy + CircleSin[i + 1] * radius, 0);
            }
        }
    }
}
