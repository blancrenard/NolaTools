#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Mask.Generator.Utils;

namespace Mask.Generator.UI
{
    public partial class TexturePreviewWindow
    {
        // プレビュー上に十字マーカーを重ね描き
        private void DrawCrossMarkersOnPreview(Rect textureRect)
        {
            if (uvMasks == null || uvMasks.Count == 0 || targetRenderer == null) return;
            string rPath = EditorPathUtils.GetGameObjectPath(targetRenderer);
            Handles.BeginGUI();
            try
            {
                foreach (var uvMask in uvMasks)
                {
                    if (uvMask == null) continue;
                    if (uvMask.rendererPath != rPath || uvMask.submeshIndex != submeshIndex) continue;
                    Vector2 uv = (uvMask.uvPosition.sqrMagnitude > 0f) ? uvMask.uvPosition : uvMask.seedUV;
                    float x = textureRect.x + uv.x * Mathf.Max(1f, textureRect.width);
                    float y = textureRect.y + (1f - uv.y) * Mathf.Max(1f, textureRect.height);
                    float size = Mathf.Clamp(Mathf.Min(textureRect.width, textureRect.height) * 0.012f, 5f, 12f);
                    Color c = (uvMask.markerColor.a > 0f) ? uvMask.markerColor : Color.cyan;
                    c.a = 0.95f;
                    Handles.color = c;
                    Vector3 h0 = new Vector3(x - size, y, 0f);
                    Vector3 h1 = new Vector3(x + size, y, 0f);
                    Vector3 v0 = new Vector3(x, y - size, 0f);
                    Vector3 v1 = new Vector3(x, y + size, 0f);
                    Handles.DrawLine(h0, h1);
                    Handles.DrawLine(v0, v1);
                }
            }
            finally
            {
                Handles.EndGUI();
            }
        }

        // マウス追従の黄色い十字を描画（UV編集モードのみ）
        private void DrawMouseFollowerCrosshair(Rect textureRect)
        {
            if (!showMouseCrosshair) return;
            Event e = Event.current;
            if (e == null) return;
            // テクスチャ矩形上にマウスがあるときのみ描画
            if (!textureRect.Contains(e.mousePosition)) return;

            Handles.BeginGUI();
            try
            {
                float size = Mathf.Clamp(Mathf.Min(textureRect.width, textureRect.height) * 0.012f, 5f, 12f);
                Color prev = Handles.color;
                Handles.color = Color.yellow;
                Vector3 h0 = new Vector3(e.mousePosition.x - size, e.mousePosition.y, 0f);
                Vector3 h1 = new Vector3(e.mousePosition.x + size, e.mousePosition.y, 0f);
                Vector3 v0 = new Vector3(e.mousePosition.x, e.mousePosition.y - size, 0f);
                Vector3 v1 = new Vector3(e.mousePosition.x, e.mousePosition.y + size, 0f);
                Handles.DrawLine(h0, h1);
                Handles.DrawLine(v0, v1);
                Handles.color = prev;
            }
            finally
            {
                Handles.EndGUI();
            }
        }
    }
}
#endif

