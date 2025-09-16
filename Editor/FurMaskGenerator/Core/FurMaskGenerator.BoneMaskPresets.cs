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

        private float GetDefaultValueForGroup(string group)
        {
            // 0=無効 / 1=強くマスク の新仕様に合わせて初期値は0
            return 0.0f;
        }

        void TryAddHumanoidPreset()
        {
            if (avatarObject == null) return;
            var animator = avatarObject.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) return;

            void add(HumanBodyBones bone)
            {
                var t = animator.GetBoneTransform(bone);
                if (t == null) return;
                string path = EditorPathUtils.GetGameObjectPath(t.gameObject);
                if (settings.boneMasks.Find(b => b.bonePath == path) == null)
                {
                    string tail = GetTailName(path);
                    string grp = GetGroupLabel(tail);
                    float v = GetDefaultValueForGroup(grp);
                    settings.boneMasks.Add(new BoneMaskData { bonePath = path, value = v });
                }
            }

            add(HumanBodyBones.Hips);
            add(HumanBodyBones.Spine);
            add(HumanBodyBones.Chest);
            add(HumanBodyBones.UpperChest);
            add(HumanBodyBones.Neck);
            add(HumanBodyBones.Head);
            add(HumanBodyBones.Jaw);
            add(HumanBodyBones.LeftEye);
            add(HumanBodyBones.RightEye);
            add(HumanBodyBones.LeftShoulder);
            add(HumanBodyBones.RightShoulder);
            add(HumanBodyBones.LeftUpperArm);
            add(HumanBodyBones.RightUpperArm);
            add(HumanBodyBones.LeftLowerArm);
            add(HumanBodyBones.RightLowerArm);
            add(HumanBodyBones.LeftHand);
            add(HumanBodyBones.RightHand);
            add(HumanBodyBones.LeftThumbProximal);
            add(HumanBodyBones.LeftThumbIntermediate);
            add(HumanBodyBones.LeftThumbDistal);
            add(HumanBodyBones.LeftIndexProximal);
            add(HumanBodyBones.LeftIndexIntermediate);
            add(HumanBodyBones.LeftIndexDistal);
            add(HumanBodyBones.LeftMiddleProximal);
            add(HumanBodyBones.LeftMiddleIntermediate);
            add(HumanBodyBones.LeftMiddleDistal);
            add(HumanBodyBones.LeftRingProximal);
            add(HumanBodyBones.LeftRingIntermediate);
            add(HumanBodyBones.LeftRingDistal);
            add(HumanBodyBones.LeftLittleProximal);
            add(HumanBodyBones.LeftLittleIntermediate);
            add(HumanBodyBones.LeftLittleDistal);
            add(HumanBodyBones.RightThumbProximal);
            add(HumanBodyBones.RightThumbIntermediate);
            add(HumanBodyBones.RightThumbDistal);
            add(HumanBodyBones.RightIndexProximal);
            add(HumanBodyBones.RightIndexIntermediate);
            add(HumanBodyBones.RightIndexDistal);
            add(HumanBodyBones.RightMiddleProximal);
            add(HumanBodyBones.RightMiddleIntermediate);
            add(HumanBodyBones.RightMiddleDistal);
            add(HumanBodyBones.RightRingProximal);
            add(HumanBodyBones.RightRingIntermediate);
            add(HumanBodyBones.RightRingDistal);
            add(HumanBodyBones.RightLittleProximal);
            add(HumanBodyBones.RightLittleIntermediate);
            add(HumanBodyBones.RightLittleDistal);
            add(HumanBodyBones.LeftUpperLeg);
            add(HumanBodyBones.RightUpperLeg);
            add(HumanBodyBones.LeftLowerLeg);
            add(HumanBodyBones.RightLowerLeg);
            add(HumanBodyBones.LeftFoot);
            add(HumanBodyBones.RightFoot);
            add(HumanBodyBones.LeftToes);
            add(HumanBodyBones.RightToes);
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
                float v = GetDefaultValueForGroup(grp);
                settings.boneMasks.Add(new BoneMaskData { bonePath = path, value = v });
            }
            UndoRedoUtils.RecordUndoAndSetDirty(settings, "Remove Humanoid Preset");
        }
    }
}
#endif
