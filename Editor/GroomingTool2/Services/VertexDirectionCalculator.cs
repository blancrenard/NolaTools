using System.Collections.Generic;
using GroomingTool2.Utils;
using UnityEngine;

namespace GroomingTool2.Services
{
    /// <summary>
    /// 頂点ごとの毛方向を計算するクラス
    /// ボーン方向を元に各頂点の方向ベクトルを算出
    /// </summary>
    internal sealed class VertexDirectionCalculator
    {
        /// <summary>
        /// 頂点ごとの毛方向を計算
        /// </summary>
        /// <param name="skinnedMeshRenderers">対象のSkinnedMeshRenderer一覧</param>
        /// <param name="boneDirections">ボーン->方向のマッピング</param>
        /// <param name="surfaceLiftAmount">表面からの浮き上がり量 (0-1)</param>
        /// <param name="animator">対象のAnimator</param>
        /// <returns>頂点ごとの方向ベクトル配列</returns>
        public Vector3[] Calculate(
            List<SkinnedMeshRenderer> skinnedMeshRenderers,
            Dictionary<Transform, Vector3> boneDirections,
            float surfaceLiftAmount,
            Animator animator)
        {
            int totalVertexCount = 0;
            foreach (var renderer in skinnedMeshRenderers)
            {
                if (renderer != null && renderer.sharedMesh != null)
                {
                    EditorMeshUtils.EnsureMeshNormalsAndTangents(renderer.sharedMesh);
                    totalVertexCount += renderer.sharedMesh.vertexCount;
                }
            }

            var directions = new Vector3[totalVertexCount];
            Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);

            // 鼻位置を検出
            Vector3? nosePosition = FindNosePosition(skinnedMeshRenderers, headBone, animator);

            // 頭の前方向を計算（目の位置で補正）
            Vector3 headForward = CalculateHeadForward(headBone, animator);

            int vertexOffset = 0;
            foreach (var renderer in skinnedMeshRenderers)
            {
                if (renderer == null || renderer.sharedMesh == null) continue;

                ProcessRendererVertices(
                    renderer, boneDirections, directions, ref vertexOffset,
                    headBone, nosePosition, headForward, surfaceLiftAmount);
            }

            return directions;
        }

        /// <summary>
        /// ランダム性を適用
        /// </summary>
        /// <param name="directions">方向ベクトル配列</param>
        /// <param name="renderers">SkinnedMeshRenderer一覧</param>
        /// <param name="amount">ランダム量 (0-1)</param>
        public void ApplyRandomness(Vector3[] directions, List<SkinnedMeshRenderer> renderers, float amount)
        {
            int vertexOffset = 0;
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMesh == null) continue;

                var normals = renderer.sharedMesh.normals;
                for (int i = 0; i < normals.Length; i++)
                {
                    var randomVec = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
                    var worldNormal = renderer.transform.TransformDirection(normals[i]);
                    var projectedRandom = Vector3.ProjectOnPlane(randomVec, worldNormal).normalized;
                    directions[i + vertexOffset] = Vector3.Slerp(directions[i + vertexOffset], projectedRandom, amount).normalized;
                }

                vertexOffset += renderer.sharedMesh.vertexCount;
            }
        }

        /// <summary>
        /// 頭の前方向を計算
        /// </summary>
        private static Vector3 CalculateHeadForward(Transform headBone, Animator animator)
        {
            Vector3 headForward = headBone != null ? headBone.forward : Vector3.forward;
            Transform leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            Transform rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            if (leftEye != null && rightEye != null && headBone != null)
            {
                Vector3 eyeCenter = (leftEye.position + rightEye.position) * 0.5f;
                Vector3 eyeDir = (eyeCenter - headBone.position).normalized;
                if (Vector3.Dot(headForward, eyeDir) < 0f)
                {
                    headForward = -headForward;
                }
            }
            return headForward;
        }

        /// <summary>
        /// レンダラーの頂点を処理
        /// </summary>
        private void ProcessRendererVertices(
            SkinnedMeshRenderer renderer,
            Dictionary<Transform, Vector3> boneDirections,
            Vector3[] directions,
            ref int vertexOffset,
            Transform headBone,
            Vector3? nosePosition,
            Vector3 headForward,
            float surfaceLiftAmount)
        {
            var mesh = renderer.sharedMesh;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var bones = renderer.bones;
            var boneWeights = mesh.boneWeights;

            // 頭ボーン配下かどうかのフラグを事前計算
            bool[] isHeadFamilyBone = new bool[bones.Length];
            for (int bi = 0; bi < bones.Length; bi++)
            {
                var b = bones[bi];
                if (b == null || headBone == null)
                {
                    isHeadFamilyBone[bi] = false;
                }
                else
                {
                    isHeadFamilyBone[bi] = (b == headBone) || b.IsChildOf(headBone);
                }
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 boneDirection = Vector3.zero;
                float totalWeight = 0;
                float headFamilyWeightSum = 0f;

                BoneWeight weight = boneWeights[i];
                var weightData = new[]
                {
                    (idx: weight.boneIndex0, w: weight.weight0),
                    (idx: weight.boneIndex1, w: weight.weight1),
                    (idx: weight.boneIndex2, w: weight.weight2),
                    (idx: weight.boneIndex3, w: weight.weight3)
                };

                foreach (var (idx, w) in weightData)
                {
                    if (w > 0 && idx < bones.Length)
                    {
                        if (bones[idx] != null && boneDirections.TryGetValue(bones[idx], out Vector3 dir))
                        {
                            boneDirection += dir * w;
                            totalWeight += w;
                        }
                        if (idx >= 0 && idx < isHeadFamilyBone.Length && isHeadFamilyBone[idx])
                        {
                            headFamilyWeightSum += w;
                        }
                    }
                }

                Vector3 worldNormal = renderer.transform.TransformDirection(normals[i]);
                Vector3 worldPos = renderer.transform.TransformPoint(vertices[i]);

                if (totalWeight > 0)
                {
                    Vector3 finalDirection;

                    // 頭部の場合は鼻から放射状＋後頭部は下向き
                    bool headFamilySignificant = headFamilyWeightSum >= 0.2f;
                    if (headFamilySignificant && nosePosition.HasValue && headBone != null)
                    {
                        finalDirection = CalculateHeadFurDirection(
                            worldPos, nosePosition.Value, headBone, headForward, worldNormal, surfaceLiftAmount);
                    }
                    else
                    {
                        Vector3 projectedDirection = Vector3.ProjectOnPlane(boneDirection.normalized, worldNormal);
                        finalDirection = Vector3.Slerp(worldNormal, projectedDirection, surfaceLiftAmount).normalized;
                    }

                    directions[i + vertexOffset] = finalDirection;
                }
                else
                {
                    directions[i + vertexOffset] = Vector3.ProjectOnPlane(Vector3.down, worldNormal).normalized;
                }
            }

            vertexOffset += mesh.vertexCount;
        }

        /// <summary>
        /// 鼻の位置を自動検出する
        /// 頭ボーンに影響を受ける頂点のうち、最も前方に突き出ている位置を鼻とする
        /// </summary>
        private Vector3? FindNosePosition(List<SkinnedMeshRenderer> renderers, Transform headBone, Animator animator)
        {
            if (headBone == null) return null;

            // 目の位置から顔の前方向を推定
            Transform leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            Transform rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            Vector3 headForward = headBone.forward;

            // 目があればその方向で補正
            if (leftEye != null && rightEye != null)
            {
                Vector3 eyeCenter = (leftEye.position + rightEye.position) * 0.5f;
                Vector3 eyeDir = (eyeCenter - headBone.position).normalized;
                if (Vector3.Dot(headForward, eyeDir) < 0f)
                {
                    headForward = -headForward;
                }
            }

            Vector3 bestNosePos = headBone.position + headForward * 0.1f; // デフォルト
            float maxForwardScore = float.NegativeInfinity;

            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMesh == null) continue;

                var mesh = renderer.sharedMesh;
                var vertices = mesh.vertices;
                var boneWeights = mesh.boneWeights;
                var bones = renderer.bones;

                // 頭ボーン配下かどうかを確認
                bool[] isHeadFamily = new bool[bones.Length];
                for (int bi = 0; bi < bones.Length; bi++)
                {
                    var b = bones[bi];
                    if (b != null)
                    {
                        isHeadFamily[bi] = (b == headBone) || b.IsChildOf(headBone);
                    }
                }

                for (int i = 0; i < vertices.Length; i++)
                {
                    // 頭ボーンに影響を受けているか確認
                    BoneWeight weight = boneWeights[i];
                    float headWeight = 0f;
                    if (weight.boneIndex0 < isHeadFamily.Length && isHeadFamily[weight.boneIndex0]) headWeight += weight.weight0;
                    if (weight.boneIndex1 < isHeadFamily.Length && isHeadFamily[weight.boneIndex1]) headWeight += weight.weight1;
                    if (weight.boneIndex2 < isHeadFamily.Length && isHeadFamily[weight.boneIndex2]) headWeight += weight.weight2;
                    if (weight.boneIndex3 < isHeadFamily.Length && isHeadFamily[weight.boneIndex3]) headWeight += weight.weight3;

                    if (headWeight < 0.5f) continue; // 頭への影響が弱い頂点はスキップ

                    Vector3 worldPos = renderer.transform.TransformPoint(vertices[i]);
                    Vector3 localToHead = headBone.InverseTransformPoint(worldPos);

                    // 頭ボーンのローカルZ方向（前方）への突出度をスコアとする
                    // Y軸（上下）が極端に離れている頂点は除外（頭頂や顎下）
                    if (Mathf.Abs(localToHead.y) > 0.15f) continue;

                    float forwardScore = localToHead.z;
                    if (forwardScore > maxForwardScore)
                    {
                        maxForwardScore = forwardScore;
                        bestNosePos = worldPos;
                    }
                }
            }

            return bestNosePos;
        }

        /// <summary>
        /// 頭部頂点の毛方向を計算（鼻から放射状、後頭部は下向き）
        /// </summary>
        private Vector3 CalculateHeadFurDirection(
            Vector3 vertexWorldPos,
            Vector3 nosePos,
            Transform headBone,
            Vector3 headForward,
            Vector3 worldNormal,
            float surfaceLift)
        {
            // 後頭部かどうかの判定（-1=真後ろ, +1=正面）
            Vector3 toVertex = (vertexWorldPos - headBone.position).normalized;
            float faceDot = Vector3.Dot(toVertex, headForward);

            // 鼻から放射状の方向
            Vector3 radialDir = (vertexWorldPos - nosePos).normalized;
            Vector3 projectedRadial = Vector3.ProjectOnPlane(radialDir, worldNormal).normalized;

            // 下向きの方向（後頭部用）
            Vector3 projectedDown = Vector3.ProjectOnPlane(Vector3.down, worldNormal).normalized;

            // 後頭部ほど下向き、顔ほど放射状にブレンド
            // faceDot: -0.3以下で完全に下向き、0.2以上で完全に放射状
            const float rearStart = -0.3f;
            const float rearEnd = 0.2f;
            float blendT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rearStart, rearEnd, faceDot));

            Vector3 blendedDir = Vector3.Slerp(projectedDown, projectedRadial, blendT).normalized;

            // ゼロベクトル対策
            if (blendedDir.sqrMagnitude < 0.001f)
            {
                blendedDir = projectedDown.sqrMagnitude > 0.001f ? projectedDown : Vector3.down;
            }

            return Vector3.Slerp(worldNormal, blendedDir, surfaceLift).normalized;
        }
    }
}
