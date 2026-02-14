#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace NolaTools.FurMaskGenerator.Utils
{
	/// <summary>
	/// Gizmo/Handles 描画の共通ユーティリティ
	/// </summary>
	public static class EditorGizmoUtils
	{
		public static void DrawWireframeSphere(Vector3 position, float radius, Color baseColor)
		{
			Handles.color = baseColor;
			Handles.DrawWireDisc(position, Vector3.up, radius);
			Handles.DrawWireDisc(position, Vector3.right, radius);
			Handles.DrawWireDisc(position, Vector3.forward, radius);
		}

		public static void SetDepthTest(bool alwaysOnTop, Action action)
		{
			var originalZTest = Handles.zTest;
			Handles.zTest = alwaysOnTop ? UnityEngine.Rendering.CompareFunction.Always : UnityEngine.Rendering.CompareFunction.LessEqual;
			action?.Invoke();
			Handles.zTest = originalZTest;
		}

	}
}
#endif


