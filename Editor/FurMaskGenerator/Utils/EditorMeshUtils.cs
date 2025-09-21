#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator.Utils
{
    /// <summary>
    /// Extended mesh utilities for Unity Editor operations
    /// Contains mesh operations, mathematical calculations, avatar utilities, and renderer detection
    /// </summary>
    public static class EditorMeshUtils
    {
        #region メッシュ操作メソッド

        /// <summary>
        /// 指定の Renderer からメッシュを取得する。SkinnedMeshRenderer の場合は一時メッシュに Bake して返す
        /// 一時メッシュを返した場合、isBakedTempMesh=true となるため、呼び出し側で破棄してください
        /// </summary>
        public static Mesh GetMeshForRenderer(Renderer renderer, out bool isBakedTempMesh)
        {
            isBakedTempMesh = false;
            if (renderer == null) return null;
            if (renderer is SkinnedMeshRenderer smr)
            {
                var baked = new Mesh();
                baked.indexFormat = IndexFormat.UInt32;
                smr.BakeMesh(baked, true);
                isBakedTempMesh = true;
                return baked;
            }
            if (renderer.TryGetComponent<MeshFilter>(out var mf))
            {
                return mf.sharedMesh;
            }
            return null;
        }

        /// <summary>
        /// メッシュの法線・タンジェントを必要に応じて再計算する
        /// </summary>
        public static void EnsureMeshNormalsAndTangents(Mesh mesh)
        {
            if (mesh == null) return;
            if (mesh.normals == null || mesh.normals.Length != mesh.vertexCount)
            {
                mesh.RecalculateNormals();
            }
            if (mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount)
            {
                mesh.RecalculateTangents();
            }
        }

        /// <summary>
        /// 複数のレンダラーのメッシュの法線・タンジェントを確保します
        /// </summary>
        public static void EnsureNormalsAndTangentsFor(IEnumerable<SkinnedMeshRenderer> renderers)
        {
            if (renderers == null) return;
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMesh == null) continue;
                EnsureMeshNormalsAndTangents(renderer.sharedMesh);
            }
        }

        /// <summary>
        /// メッシュからUV、法線、タンジェントを取得します
        /// </summary>
        public static bool TryGetUVNormalsTangents(
            Mesh mesh,
            out Vector2[] uvs,
            out Vector3[] normals,
            out Vector4[] tangents,
            string errorTitle,
            string errorMessage,
            string okLabel)
        {
            uvs = null; normals = null; tangents = null;
            if (mesh == null) { EditorUtility.DisplayDialog(errorTitle, errorMessage, okLabel); return false; }
            uvs = mesh.uv;
            normals = mesh.normals;
            tangents = mesh.tangents;
            if (uvs == null || uvs.Length != mesh.vertexCount)
            {
                EditorUtility.DisplayDialog(errorTitle, errorMessage, okLabel);
                return false;
            }
            return true;
        }

        #endregion

        #region 数学計算メソッド

        public const float BARYCENTRIC_DENOM_THRESHOLD = AppSettings.VALID_PIXEL_THRESHOLD;

        /// <summary>
        /// 重心座標を計算します
        /// </summary>
        public static Vector3 GetBarycentric(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 v0 = b - a, v1 = c - a, v2 = p - a;
            float d00 = Vector2.Dot(v0, v0), d01 = Vector2.Dot(v0, v1), d11 = Vector2.Dot(v1, v1);
            float d20 = Vector2.Dot(v2, v0), d21 = Vector2.Dot(v2, v1);
            float den = d00 * d11 - d01 * d01;
            if (Mathf.Abs(den) < BARYCENTRIC_DENOM_THRESHOLD) return new Vector3(-1, -1, -1);
            float v = (d11 * d20 - d01 * d21) / den, w = (d00 * d21 - d01 * d20) / den;
            return new Vector3(1f - v - w, v, w);
        }

        /// <summary>
        /// 指定された精度で値を丸めます
        /// </summary>
        public static float RoundToPrecision(float value, float precision = AppSettings.POSITION_PRECISION)
        {
            if (precision <= 0f) return value;
            return Mathf.Round(value / precision) * precision;
        }

        /// <summary>
        /// 指定された精度でベクトルを丸めます
        /// </summary>
        public static Vector3 RoundToPrecision(Vector3 vector, float precision = AppSettings.POSITION_PRECISION)
        {
            if (precision <= 0f) return vector;
            return new Vector3(
                Mathf.Round(vector.x / precision) * precision,
                Mathf.Round(vector.y / precision) * precision,
                Mathf.Round(vector.z / precision) * precision
            );
        }

        /// <summary>
        /// 点がカメラの視錐台内にあるかを判定します
        /// </summary>
        public static bool IsPointInFrustum(Camera camera, Vector3 point, float margin = 0f)
        {
            if (camera == null) return false;
            Vector3 viewportPoint = camera.WorldToViewportPoint(point);
            return viewportPoint.x >= -margin && viewportPoint.x <= 1f + margin &&
                   viewportPoint.y >= -margin && viewportPoint.y <= 1f + margin &&
                   viewportPoint.z > camera.nearClipPlane - margin && viewportPoint.z < camera.farClipPlane + margin;
        }

        /// <summary>
        /// 線分上の最も近い点を計算します
        /// </summary>
        public static Vector3 GetClosestPointOnLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
        {
            Vector3 lineDirection = lineEnd - lineStart;
            float lineLength = lineDirection.magnitude;
            if (lineLength < 1e-6f) return lineStart;

            lineDirection.Normalize();
            Vector3 pointToStart = point - lineStart;
            float projection = Vector3.Dot(pointToStart, lineDirection);
            projection = Mathf.Clamp(projection, 0f, lineLength);
            return lineStart + lineDirection * projection;
        }

        /// <summary>
        /// 点から線分までの距離を計算します
        /// </summary>
        public static float DistanceToLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
        {
            Vector3 closestPoint = GetClosestPointOnLineSegment(point, lineStart, lineEnd);
            return Vector3.Distance(point, closestPoint);
        }

        /// <summary>
        /// 平面上の点を投影します
        /// </summary>
        public static Vector3 ProjectPointOnPlane(Vector3 point, Vector3 planeNormal, Vector3 planePoint)
        {
            Vector3 planeToPoint = point - planePoint;
            float distance = Vector3.Dot(planeToPoint, planeNormal);
            return point - planeNormal * distance;
        }

        #endregion

        #region アバターユーティリティメソッド

        /// <summary>
        /// アバター切替後の共通準備（プレビュー破棄→自動検出→リスト再構築）
        /// </summary>
        public static void RunPostAvatarSwitchSetup(
            Action clearPreview,
            Action autoDetect,
            Action setupLists)
        {
            clearPreview?.Invoke();
            autoDetect?.Invoke();
            setupLists?.Invoke();
        }

        /// <summary>
        /// アバター切替時の共通フローを実行するユーティリティ
        /// - 事前保存→ロード or 新規作成→準備コールバック→まとめて保存 の順で呼び出す
        /// tryLoadSettings が null を返した場合は createSettings を呼び出す
        /// </summary>
        public static void RunAvatarSwitch<TSettings>(
            GameObject newAvatar,
            Action saveCurrent,
            Func<GameObject, TSettings> tryLoadSettings,
            Func<GameObject, TSettings> createSettings,
            Action<TSettings, GameObject, bool> onSettingsReady,
            Action onAvatarCleared,
            Action finalizeSave)
            where TSettings : class
        {
            // 現在の設定を保存
            saveCurrent?.Invoke();

            if (newAvatar != null)
            {
                TSettings loaded = tryLoadSettings != null ? tryLoadSettings(newAvatar) : null;
                bool isLoaded = loaded != null;
                TSettings settings = isLoaded ? loaded : (createSettings != null ? createSettings(newAvatar) : null);
                onSettingsReady?.Invoke(settings, newAvatar, isLoaded);
            }
            else
            {
                onAvatarCleared?.Invoke();
            }

            // まとめて保存
            finalizeSave?.Invoke();
        }

        #endregion


        #region レイキャスト設定定数

        /// <summary>
        /// レイキャストの最大距離
        /// </summary>
        public const float RaycastMaxDistance = 10000f;

        #endregion

        #region 選択ブリッジイベント

        /// <summary>
        /// マスクスフィアが選択されたときのイベント
        /// </summary>
        public static event Action MaskSphereSelected;

        /// <summary>
        /// マスクスフィア選択を通知します
        /// </summary>
        public static void NotifyMaskSphereSelected()
        {
            MaskSphereSelected?.Invoke();
        }

        #endregion
    }
}

#endif
