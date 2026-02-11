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
        /// 目ボーンによる前方向の判定に必要な最低限の内積値。
        /// 目の方向がheadForwardとほぼ直交している場合（目が頭のほぼ真上にある場合）、
        /// 目ボーンによる補正は信頼できないため、アバターroot前方向にフォールバックする。
        /// </summary>
        private const float kEyeDotReliabilityThreshold = 0.1f;

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

            // 頭の前方向を計算（目の位置またはアバターroot方向で補正）
            Vector3 headForward = CalculateHeadForward(headBone, animator);

            // 鼻位置を検出（補正済みheadForwardを使用）
            Vector3? nosePosition = FindNosePosition(skinnedMeshRenderers, headBone, headForward);

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
        /// 頭の前方向を計算する。
        /// 目ボーンの位置で補正し、信頼性が低い場合はアバターroot前方向にフォールバックする。
        /// </summary>
        private static Vector3 CalculateHeadForward(Transform headBone, Animator animator)
        {
            if (headBone == null) return Vector3.forward;
            return CorrectHeadForward(headBone.forward, headBone, animator);
        }

        /// <summary>
        /// headForwardを目ボーンまたはアバターroot前方向で補正する。
        /// 目ボーンの方向がheadForwardとほぼ直交している場合（目が頭のほぼ真上にある場合）、
        /// 目ボーンによる補正は信頼できないため、アバターroot前方向にフォールバックする。
        /// </summary>
        private static Vector3 CorrectHeadForward(Vector3 headForward, Transform headBone, Animator animator)
        {
            Transform leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            Transform rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);

            if (leftEye != null && rightEye != null)
            {
                Vector3 eyeCenter = (leftEye.position + rightEye.position) * 0.5f;
                Vector3 eyeDir = (eyeCenter - headBone.position).normalized;
                float dot = Vector3.Dot(headForward, eyeDir);

                if (Mathf.Abs(dot) >= kEyeDotReliabilityThreshold)
                {
                    // 目の方向にheadForward成分が十分にある → 目ボーンで補正
                    return dot < 0f ? -headForward : headForward;
                }
                // |dot| が閾値未満: 目ボーンの方向がheadForwardとほぼ直交しており信頼できない
                // → アバターroot前方向にフォールバック
            }

            // 目ボーンが無い、または目ボーン補正が信頼できない場合
            Vector3 avatarForward = animator.transform.forward;
            return Vector3.Dot(headForward, avatarForward) < 0f ? -headForward : headForward;
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
            bool[] isHeadFamilyBone = BuildIsHeadFamilyFlags(bones, headBone);

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
        /// 鼻の位置を自動検出する。
        /// 頭ボーンに影響を受ける頂点のうち、補正済みheadForward方向に最も突き出ている位置を鼻とする。
        /// </summary>
        private static Vector3? FindNosePosition(List<SkinnedMeshRenderer> renderers, Transform headBone, Vector3 headForward)
        {
            if (headBone == null) return null;

            // headForward基準の上方向を計算（世界のupからheadForward成分を除去）
            Vector3 headUp = (Vector3.up - Vector3.Dot(Vector3.up, headForward) * headForward).normalized;
            if (headUp.sqrMagnitude < 0.001f) headUp = Vector3.up;

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
                bool[] isHeadFamily = BuildIsHeadFamilyFlags(bones, headBone);

                for (int i = 0; i < vertices.Length; i++)
                {
                    float headWeight = CalculateHeadBoneWeight(boneWeights[i], isHeadFamily);
                    if (headWeight < 0.5f) continue; // 頭への影響が弱い頂点はスキップ

                    Vector3 worldPos = renderer.transform.TransformPoint(vertices[i]);
                    Vector3 fromHead = worldPos - headBone.position;

                    // 補正済みheadForward基準で上下方向のフィルタリング
                    // 上下が極端に離れている頂点は除外（頭頂や顎下）
                    float verticalDist = Vector3.Dot(fromHead, headUp);
                    if (Mathf.Abs(verticalDist) > 0.15f) continue;

                    // 補正済みheadForward方向への突出度をスコアとする
                    float forwardScore = Vector3.Dot(fromHead, headForward);
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
        /// ボーン配列から頭ボーン配下かどうかのフラグ配列を構築する
        /// </summary>
        private static bool[] BuildIsHeadFamilyFlags(Transform[] bones, Transform headBone)
        {
            var flags = new bool[bones.Length];
            if (headBone == null) return flags;

            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b != null)
                {
                    flags[i] = (b == headBone) || b.IsChildOf(headBone);
                }
            }
            return flags;
        }

        /// <summary>
        /// BoneWeightから頭ボーン配下のウェイト合計を計算する
        /// </summary>
        private static float CalculateHeadBoneWeight(BoneWeight weight, bool[] isHeadFamily)
        {
            float headWeight = 0f;
            if (weight.boneIndex0 < isHeadFamily.Length && isHeadFamily[weight.boneIndex0]) headWeight += weight.weight0;
            if (weight.boneIndex1 < isHeadFamily.Length && isHeadFamily[weight.boneIndex1]) headWeight += weight.weight1;
            if (weight.boneIndex2 < isHeadFamily.Length && isHeadFamily[weight.boneIndex2]) headWeight += weight.weight2;
            if (weight.boneIndex3 < isHeadFamily.Length && isHeadFamily[weight.boneIndex3]) headWeight += weight.weight3;
            return headWeight;
        }

        /// <summary>
        /// 頭部頂点の毛方向を計算（鼻から放射状、後頭部は下向き）
        /// </summary>
        private static Vector3 CalculateHeadFurDirection(
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
