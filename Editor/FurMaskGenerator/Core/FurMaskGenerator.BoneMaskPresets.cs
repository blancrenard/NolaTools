#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Data;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator
{
    public partial class FurMaskGenerator
    {
        private string GetTailName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            int idx = path.LastIndexOf('/');
            if (idx >= 0 && idx < path.Length - 1) return path.Substring(idx + 1);
            return path;
        }

        private string GetGroupLabel(string tail)
        {
            if (string.IsNullOrEmpty(tail)) return "";
            string t = tail.ToLowerInvariant();
            if (t.Contains(GameObjectConstants.BONE_GROUP_HIPS) || t.Contains(GameObjectConstants.BONE_GROUP_SPINE) || t.Contains(GameObjectConstants.BONE_GROUP_CHEST) || t.Contains(GameObjectConstants.BONE_GROUP_UPPERCHEST)) return GameObjectConstants.BONE_GROUP_BODY;
            if (t.Contains(GameObjectConstants.BONE_GROUP_NECK) || t.Contains(GameObjectConstants.BONE_GROUP_HEAD) || t.Contains(GameObjectConstants.BONE_GROUP_JAW) || t.Contains(GameObjectConstants.BONE_GROUP_LEFTEYE) || t.Contains(GameObjectConstants.BONE_GROUP_RIGHTEYE) || t.Contains(GameObjectConstants.BONE_GROUP_EYE)) return GameObjectConstants.BONE_GROUP_HEAD_GROUP;
            if (t.Contains(GameObjectConstants.BONE_GROUP_UPPERARM) || t.Contains(GameObjectConstants.BONE_GROUP_LOWERARM) || t.Contains(GameObjectConstants.BONE_GROUP_SHOULDER)) return GameObjectConstants.BONE_GROUP_ARM;
            if (t.Contains(GameObjectConstants.BONE_GROUP_HAND) || t.Contains(GameObjectConstants.BONE_GROUP_INDEX) || t.Contains(GameObjectConstants.BONE_GROUP_MIDDLE) || t.Contains(GameObjectConstants.BONE_GROUP_RING) || t.Contains(GameObjectConstants.BONE_GROUP_LITTLE) || t.Contains(GameObjectConstants.BONE_GROUP_THUMB)) return GameObjectConstants.BONE_GROUP_HAND_GROUP;
            if (t.Contains(GameObjectConstants.BONE_GROUP_UPPERLEG) || t.Contains(GameObjectConstants.BONE_GROUP_LOWERLEG)) return GameObjectConstants.BONE_GROUP_LEG;
            if (t.Contains(GameObjectConstants.BONE_GROUP_FOOT) || t.Contains(GameObjectConstants.BONE_GROUP_TOES)) return GameObjectConstants.BONE_GROUP_FOOT_GROUP;
            if (t.Contains(GameObjectConstants.FILTER_EAR)) return GameObjectConstants.BONE_GROUP_EAR_GROUP;
            if (t.Contains(GameObjectConstants.FILTER_TAIL)) return GameObjectConstants.BONE_GROUP_TAIL_GROUP;
            return tail;
        }

        private const float BONE_DEFAULT_VALUE = 0.0f;

        /// <summary>
        /// ヒューマノイドプリセットに含めるボーン一覧
        /// </summary>
        private static readonly HumanBodyBones[] HUMANOID_PRESET_BONES = new[]
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
            HumanBodyBones.Neck, HumanBodyBones.Head, HumanBodyBones.Jaw, HumanBodyBones.LeftEye, HumanBodyBones.RightEye,
            HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
            HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
            HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
            HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
            HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
            HumanBodyBones.LeftToes, HumanBodyBones.RightToes
        };

        void TryAddHumanoidPreset()
        {
            if (avatarObject == null) return;
            var animator = avatarObject.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) return;

            foreach (var bone in HUMANOID_PRESET_BONES)
            {
                var t = animator.GetBoneTransform(bone);
                if (t == null) continue;
                string path = EditorPathUtils.GetGameObjectPath(t.gameObject);
                if (settings.boneMasks.Find(b => b.bonePath == path) == null)
                {
                    settings.boneMasks.Add(new BoneMaskData { bonePath = path, value = BONE_DEFAULT_VALUE });
                }
            }
            UndoRedoUtils.RecordUndoAndSetDirty(settings, "Add Humanoid Preset");
        }

        void EnsureHumanoidPreset()
        {
            if (avatarObject == null) return;
            var animator = avatarObject.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) return;
            bool hasAny = false;
            foreach (var bone in new []{ HumanBodyBones.Head, HumanBodyBones.LeftHand, HumanBodyBones.RightFoot })
            {
                var t = animator.GetBoneTransform(bone);
                if (t == null) continue;
                string path = EditorPathUtils.GetGameObjectPath(t.gameObject);
                if (settings.boneMasks.Exists(b => b.bonePath == path)) { hasAny = true; break; }
            }
            if (!hasAny)
            {
                TryAddHumanoidPreset();
            }
        }

        void EnsureNonHumanoidPreset()
        {
            if (avatarObject == null) return;
            var trs = avatarObject.GetComponentsInChildren<Transform>(true);
            foreach (var t in trs)
            {
                if (t == null || t.gameObject == null) continue;
                string name = t.gameObject.name;
                string grp = GetGroupLabel(name);
                if (grp != GameObjectConstants.BONE_GROUP_EAR_GROUP && grp != GameObjectConstants.BONE_GROUP_TAIL_GROUP) continue;
                string path = EditorPathUtils.GetGameObjectPath(t.gameObject);
                if (settings.boneMasks.Exists(b => b.bonePath == path)) continue;
                settings.boneMasks.Add(new BoneMaskData { bonePath = path, value = BONE_DEFAULT_VALUE });
            }
            UndoRedoUtils.RecordUndoAndSetDirty(settings, "Remove Humanoid Preset");
        }
    }
}
#endif
