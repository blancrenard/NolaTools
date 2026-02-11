using System;
using System.Collections.Generic;
using GroomingTool2.Core;
using UnityEngine;

namespace GroomingTool2.Rendering
{
    /// <summary>
    /// ワイヤーフレーム描画を担当するクラス（GPU版）
    /// GL.LINESを使用した直接描画
    /// </summary>
    internal sealed class WireframeRenderer : IDisposable
    {
        private Color currentWireColor = new Color(0f, 1f, 0f, 0.63f);
        private Material glMaterial;

        /// <summary>
        /// GPU描画が利用可能かどうか（シェーダーとマテリアルが正常に作成された場合true）
        /// </summary>
        public bool IsAvailable { get; private set; }

        public WireframeRenderer()
        {
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
                Debug.LogWarning("WireframeRenderer: Hidden/Internal-Colored shader not found. Falling back to CPU rendering.");
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
            catch (System.Exception ex)
            {
                Debug.LogWarning($"WireframeRenderer: Failed to create material. Falling back to CPU rendering. Error: {ex.Message}");
                if (glMaterial != null)
                {
                    UnityEngine.Object.DestroyImmediate(glMaterial);
                    glMaterial = null;
                }
            }
        }

        public void Invalidate()
        {
            // GPU版ではテクスチャキャッシュがないので何もしない
        }

        public void SetColor(Color color)
        {
            currentWireColor = color;
        }

        public void Draw(Rect canvasRect, List<Vector2[]> uvSets, List<int[]> triangleSets, float scale, Vector2 scrollOffsetData)
        {
            if (glMaterial == null || uvSets == null || triangleSets == null || uvSets.Count == 0)
                return;

            // 共通クリップ領域を作成
            var clip = ClippingUtils.ClipRect.FromCanvasRect(canvasRect);

            // GL描画開始
            GL.PushMatrix();
            glMaterial.SetPass(0);
            GL.LoadPixelMatrix();

            GL.Begin(GL.LINES);
            GL.Color(currentWireColor);

            for (int s = 0; s < uvSets.Count && s < triangleSets.Count; s++)
            {
                var uv = uvSets[s];
                var tris = triangleSets[s];
                if (uv == null || tris == null || uv.Length == 0 || tris.Length < 3)
                    continue;

                for (int i = 0; i < tris.Length; i += 3)
                {
                    int i0 = tris[i];
                    int i1 = tris[i + 1];
                    int i2 = tris[i + 2];

                    // UVをスクリーン座標に変換
                    var p0 = CoordinateUtils.UvToScreen(uv[i0], scale, scrollOffsetData);
                    var p1 = CoordinateUtils.UvToScreen(uv[i1], scale, scrollOffsetData);
                    var p2 = CoordinateUtils.UvToScreen(uv[i2], scale, scrollOffsetData);

                    // 三角形がビュー領域と全く交差しない場合はスキップ（簡易カリング）
                    float minX = Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x));
                    float maxX = Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x));
                    float minY = Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y));
                    float maxY = Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y));
                    
                    if (clip.IsOutside(minX, maxX, minY, maxY))
                        continue;

                    // 三角形の3辺をクリッピングして描画
                    ClippingUtils.DrawClippedLine(p0, p1, clip);
                    ClippingUtils.DrawClippedLine(p1, p2, clip);
                    ClippingUtils.DrawClippedLine(p2, p0, clip);
                }
            }

            GL.End();
            GL.PopMatrix();
        }
    }
}
