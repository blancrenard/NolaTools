#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator.UI
{
    public partial class TexturePreviewWindow
    {
        private void ClearOverlayTexture() => ClearTexture(ref overlayTexture);

        // GL描画用マテリアル
        private Material glWireframeMaterial;

        #region GLマテリアル管理

        /// <summary>
        /// GL描画用マテリアルを作成（Hidden/Internal-Coloredシェーダー使用）
        /// </summary>
        private void EnsureGLMaterial()
        {
            if (glWireframeMaterial != null) return;

            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                Debug.LogWarning("TexturePreviewWindow: Hidden/Internal-Colored shader not found.");
                return;
            }

            glWireframeMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            glWireframeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glWireframeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glWireframeMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            glWireframeMaterial.SetInt("_ZWrite", 0);
            glWireframeMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        }

        /// <summary>
        /// GLマテリアルを破棄
        /// </summary>
        private void DestroyGLMaterial()
        {
            if (glWireframeMaterial != null)
            {
                DestroyImmediate(glWireframeMaterial);
                glWireframeMaterial = null;
            }
        }

        #endregion

        #region ワイヤーフレーム描画

        /// <summary>
        /// GL.LINESでUVワイヤーフレームを描画する
        /// GUI.BeginGroup内、Handles.BeginGUI/EndGUIの間で呼び出すこと
        /// </summary>
        /// <param name="textureDrawRect">テクスチャの描画矩形（キャンバスローカル座標、スクロール・ズーム反映済み）</param>
        /// <param name="canvasRect">キャンバス全体の矩形（クリッピング用）</param>
        private void DrawWireframeGL(Rect textureDrawRect, Rect canvasRect)
        {
            if (!showUVWireframe) return;

            EnsureGLMaterial();
            if (glWireframeMaterial == null) return;

            var wireframeData = CollectWireframeData();
            if (wireframeData == null || wireframeData.Count == 0) return;

            GL.PushMatrix();
            glWireframeMaterial.SetPass(0);
            GL.LoadPixelMatrix();

            GL.Begin(GL.LINES);
            GL.Color(AppSettings.WIREFRAME_COLOR);

            float clipW = canvasRect.width;
            float clipH = canvasRect.height;

            foreach (var data in wireframeData)
            {
                DrawTriangleWireframes(data.uvs, data.triangles, textureDrawRect, clipW, clipH);
            }

            GL.End();
            GL.PopMatrix();
        }

        /// <summary>
        /// 三角形ごとのワイヤーフレームを描画する
        /// </summary>
        private static void DrawTriangleWireframes(Vector2[] uvs, int[] triangles, Rect drawRect, float clipW, float clipH)
        {
            int triCount = triangles.Length / 3;
            float drX = drawRect.x;
            float drY = drawRect.y;
            float drW = drawRect.width;
            float drH = drawRect.height;

            for (int t = 0; t < triCount; t++)
            {
                int ia = triangles[t * 3 + 0];
                int ib = triangles[t * 3 + 1];
                int ic = triangles[t * 3 + 2];
                if (ia >= uvs.Length || ib >= uvs.Length || ic >= uvs.Length) continue;

                // UV座標をキャンバスローカル座標に変換
                float ax = drX + uvs[ia].x * drW;
                float ay = drY + (1f - uvs[ia].y) * drH;
                float bx = drX + uvs[ib].x * drW;
                float by = drY + (1f - uvs[ib].y) * drH;
                float cx = drX + uvs[ic].x * drW;
                float cy = drY + (1f - uvs[ic].y) * drH;

                // 簡易カリング: 三角形バウンディングボックスがキャンバス外なら描画をスキップ
                float minX = Mathf.Min(ax, Mathf.Min(bx, cx));
                float maxX = Mathf.Max(ax, Mathf.Max(bx, cx));
                float minY = Mathf.Min(ay, Mathf.Min(by, cy));
                float maxY = Mathf.Max(ay, Mathf.Max(by, cy));
                if (maxX < 0 || minX > clipW || maxY < 0 || minY > clipH) continue;

                // 三角形の3辺をクリッピングして描画
                DrawClippedLine(ax, ay, bx, by, clipW, clipH);
                DrawClippedLine(bx, by, cx, cy, clipW, clipH);
                DrawClippedLine(cx, cy, ax, ay, clipW, clipH);
            }
        }

        #endregion

        #region ラインクリッピング（Cohen-Sutherland）

        // Cohen-Sutherlandアウトコード定数
        private const int OutCodeInside = 0;
        private const int OutCodeLeft   = 1;
        private const int OutCodeRight  = 2;
        private const int OutCodeBottom = 4;
        private const int OutCodeTop    = 8;

        /// <summary>
        /// Cohen-Sutherlandアルゴリズムでラインをクリッピングし、可視部分のみGL描画する
        /// GL.LINESモード内で呼び出すこと
        /// </summary>
        private static void DrawClippedLine(float x0, float y0, float x1, float y1, float clipW, float clipH)
        {
            int c0 = ComputeOutCode(x0, y0, clipW, clipH);
            int c1 = ComputeOutCode(x1, y1, clipW, clipH);

            while (true)
            {
                if ((c0 | c1) == 0)
                {
                    // 両方内側：描画
                    GL.Vertex3(x0, y0, 0);
                    GL.Vertex3(x1, y1, 0);
                    return;
                }
                if ((c0 & c1) != 0)
                {
                    // 完全外側：スキップ
                    return;
                }

                int cOut = c0 != 0 ? c0 : c1;
                float x, y;
                ClipToEdge(x0, y0, x1, y1, cOut, clipW, clipH, out x, out y);

                if (cOut == c0)
                {
                    x0 = x; y0 = y;
                    c0 = ComputeOutCode(x0, y0, clipW, clipH);
                }
                else
                {
                    x1 = x; y1 = y;
                    c1 = ComputeOutCode(x1, y1, clipW, clipH);
                }
            }
        }

        /// <summary>
        /// 点のアウトコードを計算する
        /// </summary>
        private static int ComputeOutCode(float x, float y, float clipW, float clipH)
        {
            int code = OutCodeInside;
            if (x < 0) code |= OutCodeLeft;
            else if (x > clipW) code |= OutCodeRight;
            if (y < 0) code |= OutCodeBottom;
            else if (y > clipH) code |= OutCodeTop;
            return code;
        }

        /// <summary>
        /// 指定されたアウトコードに対応する辺との交点を計算する
        /// </summary>
        private static void ClipToEdge(float x0, float y0, float x1, float y1, int outCode, float clipW, float clipH, out float x, out float y)
        {
            if ((outCode & OutCodeTop) != 0)
            {
                x = x0 + (x1 - x0) * (clipH - y0) / (y1 - y0);
                y = clipH;
            }
            else if ((outCode & OutCodeBottom) != 0)
            {
                x = x0 + (x1 - x0) * -y0 / (y1 - y0);
                y = 0;
            }
            else if ((outCode & OutCodeRight) != 0)
            {
                y = y0 + (y1 - y0) * (clipW - x0) / (x1 - x0);
                x = clipW;
            }
            else // OutCodeLeft
            {
                y = y0 + (y1 - y0) * -x0 / (x1 - x0);
                x = 0;
            }
        }

        #endregion

        #region ワイヤーフレームデータ収集

        /// <summary>
        /// ワイヤーフレーム描画用データ
        /// </summary>
        private struct WireframeDrawData
        {
            public Vector2[] uvs;
            public int[] triangles;
        }

        /// <summary>
        /// 描画対象のUV/三角形データを収集する
        /// </summary>
        private List<WireframeDrawData> CollectWireframeData()
        {
            var result = new List<WireframeDrawData>();

            if (targets != null && targets.Count > 0)
            {
                CollectTargetsWireframeData(result);
            }

            // ターゲットリストからデータが得られなかった場合、単一レンダラーから取得
            if (result.Count == 0 && targetRenderer != null)
            {
                CollectSingleRendererWireframeData(result);
            }

            return result;
        }

        /// <summary>
        /// 複数ターゲットからワイヤーフレームデータを収集
        /// </summary>
        private void CollectTargetsWireframeData(List<WireframeDrawData> result)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t == null || t.Texture != texture || t.Renderer == null) continue;
                Mesh mesh = EditorMeshUtils.GetMeshForRenderer(t.Renderer, out bool isBaked);
                if (mesh == null) continue;
                try
                {
                    int sub = Mathf.Clamp(t.SubmeshIndex, 0, mesh.subMeshCount - 1);
                    if (TryGetTrianglesAndUV(mesh, sub, out var triangles, out var uvs))
                    {
                        result.Add(new WireframeDrawData { uvs = uvs, triangles = triangles });
                    }
                }
                finally
                {
                    if (isBaked) EditorObjectUtils.SafeDestroy(mesh);
                }
            }
        }

        /// <summary>
        /// 単一レンダラーからワイヤーフレームデータを収集
        /// </summary>
        private void CollectSingleRendererWireframeData(List<WireframeDrawData> result)
        {
            Mesh mesh = EditorMeshUtils.GetMeshForRenderer(targetRenderer, out bool isBaked);
            if (mesh == null) return;
            try
            {
                if (TryGetTrianglesAndUV(mesh, submeshIndex, out var triangles, out var uvs))
                {
                    result.Add(new WireframeDrawData { uvs = uvs, triangles = triangles });
                }
            }
            finally
            {
                if (isBaked) EditorObjectUtils.SafeDestroy(mesh);
            }
        }

        #endregion
    }
}
#endif
