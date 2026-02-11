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

        // ドットの固定サイズ（ピクセル単位）- CPU版に合わせた見た目
        private const float FixedDotRadius = 5f;

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
            // sin/cosを事前計算
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
            
            // 頂点カラーをそのまま描画するシンプルなシェーダー
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
                // Zテストオフ、アルファブレンディング有効
                glMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                glMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                glMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                glMaterial.SetInt("_ZWrite", 0);
                glMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                
                IsAvailable = true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"GpuFurDataRenderer: Failed to create material. Falling back to CPU rendering. Error: {ex.Message}");
                if (glMaterial != null)
                {
                    UnityEngine.Object.DestroyImmediate(glMaterial);
                    glMaterial = null;
                }
            }
        }

        // 暗化用の色倍率
        private const float MaskedDarkenFactor = 0.4f;

        /// <summary>
        /// 毛データをGPU描画する
        /// Handles.BeginGUI() のコンテキスト内で呼び出すこと
        /// </summary>
        public void Draw(Rect viewRect, float scale, int interval, Vector2 scrollOffsetData, bool[,] effectiveMask = null, bool hasMaskSelection = false)
        {
            if (glMaterial == null || furDataManager?.Data == null)
                return;

            // 可視データ範囲を計算
            CoordinateUtils.GetVisibleDataRange(viewRect, scale, scrollOffsetData, Common.TexSize, 1,
                out int startX, out int endX, out int startY, out int endY);

            int visibleWData = Mathf.Max(0, endX - startX);
            int visibleHData = Mathf.Max(0, endY - startY);
            if (visibleWData <= 0 || visibleHData <= 0)
                return;

            // ステップ計算（CPU版と同じロジック）
            int stepData = Mathf.Max(1, Mathf.RoundToInt(interval / Mathf.Max(scale, 1e-6f)));
            int screenPointsX = Mathf.Max(1, Mathf.CeilToInt(viewRect.width / interval));
            int screenPointsY = Mathf.Max(1, Mathf.CeilToInt(viewRect.height / interval));
            int approxPoints = screenPointsX * screenPointsY;

            int stepMul = 1;
            if (approxPoints > FurRenderParams.TargetMaxPoints)
            {
                stepMul = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt((float)approxPoints / FurRenderParams.TargetMaxPoints)));
            }
            int step = Mathf.Max(1, stepData * stepMul);

            var furData = furDataManager.Data;

            // 共通クリップ領域を作成
            var clip = ClippingUtils.ClipRect.FromCanvasRect(viewRect);

            // GL描画開始
            GL.PushMatrix();
            glMaterial.SetPass(0);

            // GUIマトリックスを使用（Handles.BeginGUI()内なので）
            GL.LoadPixelMatrix();

            // 1回のループでドットとラインの描画データを収集
            // 最大描画数を事前計算してバッファサイズを確保
            int maxPointsX = (endX - startX) / step + 1;
            int maxPointsY = (endY - startY) / step + 1;
            int maxPoints = maxPointsX * maxPointsY;
            
            // スタック配列またはヒープ配列を使用（少量ならスタック）
            // 描画データ構造体
            Span<DrawData> drawDataBuffer = maxPoints <= 1024 
                ? stackalloc DrawData[maxPoints] 
                : new DrawData[maxPoints];
            int drawCount = 0;

            // 1回のループで全データを収集
            for (int y = startY; y < endY; y += step)
            {
                for (int x = startX; x < endX; x += step)
                {
                    int index = Common.GetIndex(x, y);
                    var data = furData[index];

                    float cos = AngleLut.GetCos(data.Dir);
                    float sin = AngleLut.GetSin(data.Dir);

                    float powerDot = data.Inclined * Common.Grid;
                    float dxDot = powerDot * cos;
                    float dyDot = powerDot * sin;
                    float dotLength = Mathf.Sqrt(dxDot * dxDot + dyDot * dyDot);
                    if (dotLength < 0.5f)
                        continue;

                    // 画面ローカルのピクセル座標に変換
                    float cx = (x - scrollOffsetData.x) * scale;
                    float cy = (y - scrollOffsetData.y) * scale;

                    // ドット色を取得
                    Color dotColor = NormalMapColorUtils.GetNormalMapColor(data);
                    
                    // マスク外の場合は暗くする
                    bool isMasked = hasMaskSelection && effectiveMask != null && !effectiveMask[x, y];
                    if (isMasked)
                    {
                        dotColor.r *= MaskedDarkenFactor;
                        dotColor.g *= MaskedDarkenFactor;
                        dotColor.b *= MaskedDarkenFactor;
                    }

                    // ライン終点を計算
                    float lineMultiplier = 0.5f + data.Inclined * 1.58f;
                    float powerLinePx = data.Inclined * Common.Grid * lineMultiplier;
                    float dxLinePx = powerLinePx * cos;
                    float dyLinePx = powerLinePx * sin;
                    float lenPx = Mathf.Sqrt(dxLinePx * dxLinePx + dyLinePx * dyLinePx);
                    
                    float maxLenPx = Mathf.Max(1f, step * scale * 0.7f);
                    if (lenPx > maxLenPx && lenPx > 0.5f)
                    {
                        float s = maxLenPx / lenPx;
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

            // まずドットを描画（ラインの下に描画されるように）
            GL.Begin(GL.TRIANGLES);
            for (int i = 0; i < drawCount; i++)
            {
                ref readonly var d = ref drawDataBuffer[i];
                // ドットの一部でもクリップ領域外に出る場合はスキップ
                if (d.cx - FixedDotRadius < clip.MinX || d.cx + FixedDotRadius > clip.MaxX ||
                    d.cy - FixedDotRadius < clip.MinY || d.cy + FixedDotRadius > clip.MaxY)
                    continue;
                DrawCircleVertices(d.cx, d.cy, FixedDotRadius, d.dotColor);
            }
            GL.End();

            // 次にラインを描画（ドットの上に描画）
            GL.Begin(GL.LINES);
            for (int i = 0; i < drawCount; i++)
            {
                ref readonly var d = ref drawDataBuffer[i];
                if (d.hasLine)
                {
                    // マスク外の場合は暗い白色、それ以外は通常の白色
                    Color lineColor = d.isMasked ? new Color(MaskedDarkenFactor, MaskedDarkenFactor, MaskedDarkenFactor) : Color.white;
                    GL.Color(lineColor);
                    // ラインをクリップ
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
                // 中心
                GL.Vertex3(cx, cy, 0);
                // 現在の点
                GL.Vertex3(cx + CircleCos[i] * radius, cy + CircleSin[i] * radius, 0);
                // 次の点
                GL.Vertex3(cx + CircleCos[i + 1] * radius, cy + CircleSin[i + 1] * radius, 0);
            }
        }
    }
}

