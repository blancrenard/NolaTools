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

		public static void DrawGizmoArrow(Vector3 from, Vector3 to, Color color, float arrowHeadLength = 0.1f, float arrowHeadAngle = 25f)
		{
			Handles.color = color;
			Handles.DrawLine(from, to);

			Vector3 direction = (to - from).normalized;
			Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * Vector3.forward;
			Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * Vector3.forward;

			Handles.DrawLine(to, to + right * arrowHeadLength);
			Handles.DrawLine(to, to + left * arrowHeadLength);
		}

		public static void DrawGizmoArrow(Vector3 position, Vector3 direction, float length, Color color, float arrowHeadLength = 0.1f, float arrowHeadAngle = 25f)
		{
			Vector3 to = position + direction.normalized * length;
			DrawGizmoArrow(position, to, color, arrowHeadLength, arrowHeadAngle);
		}

		public static void DrawLineWithArrow(Vector3 start, Vector3 end, Color color, float arrowSize = 0.1f)
		{
			Handles.color = color;
			Handles.DrawLine(start, end);

			Vector3 direction = (end - start).normalized;
			Vector3 arrowPos = end - direction * arrowSize;
			Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 25, 0) * Vector3.forward;
			Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 25, 0) * Vector3.forward;

			Handles.DrawLine(end, arrowPos + right * arrowSize);
			Handles.DrawLine(end, arrowPos + left * arrowSize);
		}
	}
}
#endif


