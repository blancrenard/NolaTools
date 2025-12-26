using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GroomingTool2.Services
{
    /// <summary>
    /// ボーン方向の計算を担当するクラス
    /// Humanoidボーン階層から毛の流れ方向を算出
    /// </summary>
    internal sealed class BoneDirectionCalculator
    {
        /// <summary>
        /// Humanoidボーンからの毛方向を計算
        /// </summary>
        /// <param name="animator">対象のAnimator</param>
        /// <param name="skinnedMeshRenderers">対象のSkinnedMeshRenderer一覧</param>
        /// <returns>ボーン -> 方向ベクトル のマッピング</returns>
        public Dictionary<Transform, Vector3> Calculate(
            Animator animator,
            List<SkinnedMeshRenderer> skinnedMeshRenderers)
        {
            var boneDirections = new Dictionary<Transform, Vector3>();
            var parentToChildDirections = new Dictionary<Transform, List<Vector3>>();

            // Humanoidボーンのセットを作成
            var humanoidBones = new HashSet<Transform>();
            foreach (HumanBodyBones boneEnum in System.Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (boneEnum == HumanBodyBones.LastBone) continue;
                var bone = animator.GetBoneTransform(boneEnum);
                if (bone != null) humanoidBones.Add(bone);
            }

            var boneMappingList = BuildBoneMappingList(animator);

            // Humanoidボーンの方向を計算
            foreach (var (parentEnum, childEnum) in boneMappingList)
            {
                var parentBone = animator.GetBoneTransform(parentEnum);
                var childBone = animator.GetBoneTransform(childEnum);

                if (childBone == null && parentBone != null)
                {
                    // つま先が見つからない場合のフォールバック
                    if (childEnum == HumanBodyBones.LeftToes || childEnum == HumanBodyBones.RightToes)
                    {
                        if (parentBone.childCount > 0)
                        {
                            childBone = parentBone.GetChild(0);
                        }
                    }
                }

                if (parentBone != null && childBone != null)
                {
                    Vector3 direction = (childBone.position - parentBone.position).normalized;
                    boneDirections[childBone] = direction;
                    if (!parentToChildDirections.ContainsKey(parentBone))
                        parentToChildDirections[parentBone] = new List<Vector3>();
                    parentToChildDirections[parentBone].Add(direction);
                }
            }

            // Humanoidボーン以外のすべてのボーンチェーンを親→子方向で処理
            var nonHumanoidRoots = FindNonHumanoidRootBones(skinnedMeshRenderers, humanoidBones);
            foreach (var rootBone in nonHumanoidRoots)
            {
                if (rootBone == null) continue;
                ProcessBoneChainRecursive(rootBone, boneDirections, parentToChildDirections);
            }

            // 親ボーンの方向を子方向の平均として計算
            foreach (var kvp in parentToChildDirections)
            {
                boneDirections[kvp.Key] = kvp.Value.Aggregate(Vector3.zero, (sum, dir) => sum + dir).normalized;
            }

            // レンダラーのボーンで未設定のものは親を辿って設定
            foreach (var renderer in skinnedMeshRenderers)
            {
                if (renderer == null || renderer.bones == null) continue;
                foreach (var bone in renderer.bones)
                {
                    if (boneDirections.ContainsKey(bone)) continue;
                    Transform current = bone;
                    while (current != null && current.parent != null)
                    {
                        current = current.parent;
                        if (boneDirections.TryGetValue(current, out Vector3 direction))
                        {
                            boneDirections[bone] = direction;
                            break;
                        }
                    }
                }
            }

            return boneDirections;
        }

        /// <summary>
        /// Humanoidボーンのマッピングリストを構築
        /// </summary>
        private static List<(HumanBodyBones parent, HumanBodyBones child)> BuildBoneMappingList(Animator animator)
        {
            var boneMappingList = new List<(HumanBodyBones parent, HumanBodyBones child)>
            {
                (HumanBodyBones.Spine, HumanBodyBones.Hips),
                (HumanBodyBones.Chest, HumanBodyBones.Spine),
                (HumanBodyBones.Head, HumanBodyBones.Neck),
                (HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm),
                (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm),
                (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
                (HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm),
                (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm),
                (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
                (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg),
                (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
                (HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes),
                (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg),
                (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
                (HumanBodyBones.RightFoot, HumanBodyBones.RightToes),
            };

            if (animator.GetBoneTransform(HumanBodyBones.UpperChest) != null)
            {
                boneMappingList.Add((HumanBodyBones.Neck, HumanBodyBones.UpperChest));
                boneMappingList.Add((HumanBodyBones.UpperChest, HumanBodyBones.Chest));
            }
            else
            {
                boneMappingList.Add((HumanBodyBones.Neck, HumanBodyBones.Chest));
            }

            // 指のボーンマッピングを追加
            AddFingerBoneMappings(boneMappingList);

            return boneMappingList;
        }

        /// <summary>
        /// 指のボーンマッピングを追加
        /// </summary>
        private static void AddFingerBoneMappings(List<(HumanBodyBones parent, HumanBodyBones child)> boneMappingList)
        {
            HumanBodyBones[] hands = { HumanBodyBones.LeftHand, HumanBodyBones.RightHand };
            HumanBodyBones[] fingerRoots = { HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftLittleProximal };
            HumanBodyBones[] fingerMids = { HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftLittleIntermediate };
            HumanBodyBones[] fingerTips = { HumanBodyBones.LeftThumbDistal, HumanBodyBones.LeftIndexDistal, HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftRingDistal, HumanBodyBones.LeftLittleDistal };

            for (int i = 0; i < hands.Length; i++)
            {
                var hand = hands[i];
                for (int j = 0; j < fingerRoots.Length; j++)
                {
                    int offset = (i == 0) ? 0 : (HumanBodyBones.RightThumbProximal - HumanBodyBones.LeftThumbProximal);
                    boneMappingList.Add((hand, fingerRoots[j] + offset));
                    boneMappingList.Add((fingerRoots[j] + offset, fingerMids[j] + offset));
                    boneMappingList.Add((fingerMids[j] + offset, fingerTips[j] + offset));
                }
            }
        }

        /// <summary>
        /// Humanoidボーン以外のボーンチェーンのルートを検出
        /// </summary>
        private HashSet<Transform> FindNonHumanoidRootBones(
            List<SkinnedMeshRenderer> skinnedMeshRenderers,
            HashSet<Transform> humanoidBones)
        {
            var nonHumanoidRoots = new HashSet<Transform>();

            foreach (var renderer in skinnedMeshRenderers)
            {
                if (renderer == null || renderer.bones == null) continue;

                foreach (var bone in renderer.bones)
                {
                    if (bone == null || humanoidBones.Contains(bone)) continue;

                    // このボーンの最上位の非Humanoidボーン（親がHumanoidボーンまたはnull）を見つける
                    Transform root = bone;
                    Transform current = bone.parent;
                    while (current != null && !humanoidBones.Contains(current))
                    {
                        root = current;
                        current = current.parent;
                    }

                    nonHumanoidRoots.Add(root);
                }
            }

            return nonHumanoidRoots;
        }

        /// <summary>
        /// ボーンチェーンを再帰的に処理して方向を設定する（耳/尻尾用）
        /// </summary>
        private void ProcessBoneChainRecursive(
            Transform bone,
            Dictionary<Transform, Vector3> boneDirections,
            Dictionary<Transform, List<Vector3>> parentToChildDirections)
        {
            if (bone == null || bone.childCount == 0) return;

            if (!parentToChildDirections.ContainsKey(bone))
                parentToChildDirections[bone] = new List<Vector3>();

            for (int i = 0; i < bone.childCount; i++)
            {
                var childBone = bone.GetChild(i);
                var direction = (childBone.position - bone.position).normalized;

                // 方向がゼロでなければ設定
                if (direction.sqrMagnitude > 0.001f)
                {
                    boneDirections[childBone] = direction;
                    parentToChildDirections[bone].Add(direction);
                }

                // 子ボーンも再帰的に処理
                ProcessBoneChainRecursive(childBone, boneDirections, parentToChildDirections);
            }
        }
    }
}
