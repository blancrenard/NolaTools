using UnityEngine;

namespace GroomingTool2.Utils
{
    /// <summary>
    /// バリセントリック（重心）座標の計算ユーティリティ
    /// 2D/3D の三角形内の点の位置を計算するための共通処理
    /// </summary>
    internal static class BarycentricUtils
    {
        /// <summary>
        /// 最小許容誤差（退化三角形判定用）
        /// </summary>
        private const float DegenerateTolerance = 1e-12f;

        /// <summary>
        /// 2D三角形のバリセントリック座標を計算
        /// </summary>
        /// <param name="p">計算対象の点</param>
        /// <param name="a">三角形の頂点A</param>
        /// <param name="b">三角形の頂点B</param>
        /// <param name="c">三角形の頂点C</param>
        /// <param name="bary">出力: バリセントリック座標 (u, v, w) where p = u*a + v*b + w*c</param>
        /// <returns>計算成功時はtrue、退化三角形の場合はfalse</returns>
        public static bool TryCompute2D(Vector2 p, Vector2 a, Vector2 b, Vector2 c, out Vector3 bary)
        {
            var v0 = b - a;
            var v1 = c - a;
            var v2 = p - a;

            float d00 = Vector2.Dot(v0, v0);
            float d01 = Vector2.Dot(v0, v1);
            float d11 = Vector2.Dot(v1, v1);
            float d20 = Vector2.Dot(v2, v0);
            float d21 = Vector2.Dot(v2, v1);

            float denom = d00 * d11 - d01 * d01;
            if (Mathf.Abs(denom) < DegenerateTolerance)
            {
                bary = default;
                return false;
            }

            float v = (d11 * d20 - d01 * d21) / denom;
            float w = (d00 * d21 - d01 * d20) / denom;
            float u = 1f - v - w;
            bary = new Vector3(u, v, w);
            return true;
        }

        /// <summary>
        /// 3D三角形のバリセントリック座標を計算
        /// </summary>
        /// <param name="p">計算対象の点</param>
        /// <param name="a">三角形の頂点A</param>
        /// <param name="b">三角形の頂点B</param>
        /// <param name="c">三角形の頂点C</param>
        /// <param name="bary">出力: バリセントリック座標 (u, v, w) where p = u*a + v*b + w*c</param>
        /// <returns>計算成功時はtrue、退化三角形の場合はfalse</returns>
        public static bool TryCompute3D(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out Vector3 bary)
        {
            Vector3 v0 = b - a;
            Vector3 v1 = c - a;
            Vector3 v2 = p - a;

            float d00 = Vector3.Dot(v0, v0);
            float d01 = Vector3.Dot(v0, v1);
            float d11 = Vector3.Dot(v1, v1);
            float d20 = Vector3.Dot(v2, v0);
            float d21 = Vector3.Dot(v2, v1);

            float denom = d00 * d11 - d01 * d01;
            if (Mathf.Abs(denom) < DegenerateTolerance)
            {
                bary = default;
                return false;
            }

            float v = (d11 * d20 - d01 * d21) / denom;
            float w = (d00 * d21 - d01 * d20) / denom;
            float u = 1f - v - w;
            bary = new Vector3(u, v, w);
            return true;
        }

        /// <summary>
        /// バリセントリック座標が三角形内部を示しているかチェック
        /// </summary>
        /// <param name="bary">バリセントリック座標</param>
        /// <param name="epsilon">許容誤差（デフォルト: 1e-5f）</param>
        /// <returns>三角形内部または境界上の場合true</returns>
        public static bool IsInsideTriangle(Vector3 bary, float epsilon = 1e-5f)
        {
            return bary.x >= -epsilon && bary.y >= -epsilon && bary.z >= -epsilon;
        }

        /// <summary>
        /// バリセントリック座標を正規化（合計が1になるようにクランプ）
        /// </summary>
        /// <param name="bary">入力バリセントリック座標</param>
        /// <returns>正規化されたバリセントリック座標</returns>
        public static Vector3 Normalize(Vector3 bary)
        {
            float bx = Mathf.Clamp01(bary.x);
            float by = Mathf.Clamp01(bary.y);
            float bz = Mathf.Clamp01(bary.z);
            float sum = bx + by + bz;
            
            if (sum > 0f)
            {
                return new Vector3(bx / sum, by / sum, bz / sum);
            }
            
            return new Vector3(1f / 3f, 1f / 3f, 1f / 3f); // 中心点
        }

        /// <summary>
        /// バリセントリック座標を使用して2D点を補間
        /// </summary>
        public static Vector2 Interpolate(Vector3 bary, Vector2 a, Vector2 b, Vector2 c)
        {
            return a * bary.x + b * bary.y + c * bary.z;
        }

        /// <summary>
        /// バリセントリック座標を使用して3D点を補間
        /// </summary>
        public static Vector3 Interpolate(Vector3 bary, Vector3 a, Vector3 b, Vector3 c)
        {
            return a * bary.x + b * bary.y + c * bary.z;
        }
    }
}
