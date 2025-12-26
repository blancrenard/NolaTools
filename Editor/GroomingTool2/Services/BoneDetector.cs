using System.Collections.Generic;
using System.Linq;
using GroomingTool2.Utils;
using UnityEngine;

namespace GroomingTool2.Services
{
    /// <summary>
    /// 耳/尻尾などのカスタムボーンを自動検出するクラス
    /// </summary>
    internal static class BoneDetector
    {
        #region Public API

        /// <summary>
        /// 耳/尻尾ボーンを自動検出する
        /// </summary>
        public static List<Transform> AutoDetectCustomParentBones(GameObject avatarObject)
        {
            if (avatarObject == null) return new List<Transform>();

            var customParentBones = new List<Transform>();
            var boneSet = CollectAllBones(avatarObject);

            var allTransforms = avatarObject.GetComponentsInChildren<Transform>();
            var topmostCandidates = new HashSet<Transform>();
            var leftEarTopCandidates = new HashSet<Transform>();
            var rightEarTopCandidates = new HashSet<Transform>();

            foreach (var t in allTransforms)
            {
                if (t == null || string.IsNullOrEmpty(t.name)) continue;
                var lower = t.name.ToLowerInvariant();
                if (lower.Contains("ear") && !IsExcludedAccessory(lower))
                {
                    AddEarCandidate(t, boneSet, topmostCandidates, leftEarTopCandidates, rightEarTopCandidates);
                }
                else if (lower.Contains("tail"))
                {
                    AddTailCandidate(t, boneSet, topmostCandidates);
                }
            }

            // 耳ボーンが見つからなかった場合のフォールバック検出
            if (leftEarTopCandidates.Count == 0 || rightEarTopCandidates.Count == 0)
            {
                FallbackEarDetection(avatarObject, boneSet, topmostCandidates, leftEarTopCandidates, rightEarTopCandidates);
            }

            foreach (var top in topmostCandidates)
            {
                if (!customParentBones.Contains(top))
                {
                    customParentBones.Add(top);
                }
            }

            return customParentBones;
        }

        /// <summary>
        /// アバターの全ボーンを収集する
        /// </summary>
        public static HashSet<Transform> CollectAllBones(GameObject avatarObject)
        {
            var boneSet = new HashSet<Transform>();
            var smrList = avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in smrList)
            {
                if (smr == null || smr.bones == null) continue;
                foreach (var b in smr.bones)
                {
                    if (b != null) boneSet.Add(b);
                }
            }
            return boneSet;
        }

        #endregion

        #region Side Detection

        /// <summary>
        /// ASCII文字かどうかを判定
        /// </summary>
        private static bool IsAsciiLetter(char c) => c >= 'a' && c <= 'z';

        /// <summary>
        /// 独立した文字トークンが含まれているかを判定
        /// </summary>
        private static bool ContainsStandaloneCharToken(string s, char ch)
        {
            int idx = s.IndexOf(ch);
            while (idx >= 0)
            {
                char prev = idx > 0 ? s[idx - 1] : '\0';
                char next = idx < s.Length - 1 ? s[idx + 1] : '\0';
                bool prevIsLetter = IsAsciiLetter(prev);
                bool nextIsLetter = IsAsciiLetter(next);
                if (!prevIsLetter && !nextIsLetter) return true;
                idx = s.IndexOf(ch, idx + 1);
            }
            return false;
        }

        /// <summary>
        /// 左側を示す名前かどうかを判定
        /// </summary>
        public static bool IsLeftSide(string s) => s.Contains("left") || ContainsStandaloneCharToken(s, 'l');

        /// <summary>
        /// 右側を示す名前かどうかを判定
        /// </summary>
        public static bool IsRightSide(string s) => s.Contains("right") || ContainsStandaloneCharToken(s, 'r');

        /// <summary>
        /// 除外対象のアクセサリ名かどうかを判定
        /// </summary>
        public static bool IsExcludedAccessory(string s)
        {
            return s.Contains("wear") || s.Contains("earring") || s.Contains("ear_ring") ||
                   s.Contains("pierce") || s.Contains("piercing") || s.Contains("accessory") || s.Contains("acc");
        }

        #endregion

        #region Ear Detection

        /// <summary>
        /// 耳ボーンの候補を追加
        /// </summary>
        private static void AddEarCandidate(
            Transform t,
            HashSet<Transform> boneSet,
            HashSet<Transform> topmostCandidates,
            HashSet<Transform> leftEarTopCandidates,
            HashSet<Transform> rightEarTopCandidates)
        {
            string lower = t.name.ToLowerInvariant();
            if (IsExcludedAccessory(lower)) return;
            bool left = IsLeftSide(lower);
            bool right = IsRightSide(lower);
            if (!left && !right) return;

            if (left)
            {
                var cur = FindTopmostEarParent(t, boneSet, isLeft: true);
                if (cur != null && boneSet.Contains(cur))
                {
                    topmostCandidates.Add(cur);
                    leftEarTopCandidates.Add(cur);
                }
            }

            if (right)
            {
                var cur = FindTopmostEarParent(t, boneSet, isLeft: false);
                if (cur != null && boneSet.Contains(cur))
                {
                    topmostCandidates.Add(cur);
                    rightEarTopCandidates.Add(cur);
                }
            }
        }

        /// <summary>
        /// 最上位の耳ボーン親を探索
        /// </summary>
        private static Transform FindTopmostEarParent(Transform t, HashSet<Transform> boneSet, bool isLeft)
        {
            Transform cur = t;
            while (cur.parent != null)
            {
                var p = cur.parent;
                var pLower = p.name.ToLowerInvariant();
                bool matchesSide = isLeft ? IsLeftSide(pLower) : IsRightSide(pLower);
                if (!IsExcludedAccessory(pLower) && pLower.Contains("ear") && matchesSide)
                {
                    cur = p;
                    continue;
                }
                break;
            }
            return boneSet.Contains(cur) ? cur : null;
        }

        /// <summary>
        /// 耳ボーンが見つからなかった場合のフォールバック検出
        /// </summary>
        private static void FallbackEarDetection(
            GameObject avatarObject,
            HashSet<Transform> boneSet,
            HashSet<Transform> topmostCandidates,
            HashSet<Transform> leftEarTopCandidates,
            HashSet<Transform> rightEarTopCandidates)
        {
            var animator = avatarObject.GetComponent<Animator>();
            Transform head = null;
            if (animator != null && animator.isHuman)
            {
                head = animator.GetBoneTransform(HumanBodyBones.Head);
            }
            var reference = head != null ? head : avatarObject.transform;

            var earBones = boneSet.Where(b =>
            {
                if (b == null) return false;
                var nm = b.name.ToLowerInvariant();
                return nm.Contains("ear") && !RendererDetectionUtils.IsEarAccessoryName(nm);
            }).ToList();

            if (leftEarTopCandidates.Count == 0)
            {
                var topLeft = GetTopmostEarForSide(earBones, reference, isRight: false);
                if (topLeft != null)
                {
                    topmostCandidates.Add(topLeft);
                }
            }
            if (rightEarTopCandidates.Count == 0)
            {
                var topRight = GetTopmostEarForSide(earBones, reference, isRight: true);
                if (topRight != null)
                {
                    topmostCandidates.Add(topRight);
                }
            }
        }

        /// <summary>
        /// 指定側の最上位耳ボーンを取得
        /// </summary>
        private static Transform GetTopmostEarForSide(List<Transform> earBones, Transform reference, bool isRight)
        {
            var sideBones = earBones.Where(b =>
            {
                var local = reference.InverseTransformPoint(b.position);
                return isRight ? local.x >= 0f : local.x <= 0f;
            }).ToList();
            if (sideBones.Count == 0) return null;

            Transform best = null;
            float bestAbsX = float.NegativeInfinity;
            foreach (var b in sideBones)
            {
                var top = ChooseTopmostEar(b);
                var x = reference.InverseTransformPoint(top.position).x;
                var score = Mathf.Abs(x);
                if (score > bestAbsX)
                {
                    bestAbsX = score;
                    best = top;
                }
            }
            return best;
        }

        /// <summary>
        /// 耳ボーンチェーンの最上位を選択
        /// </summary>
        private static Transform ChooseTopmostEar(Transform t)
        {
            Transform cur = t;
            while (cur.parent != null)
            {
                var p = cur.parent;
                if (p.name.ToLowerInvariant().Contains("ear"))
                {
                    cur = p;
                    continue;
                }
                break;
            }
            return cur;
        }

        #endregion

        #region Tail Detection

        /// <summary>
        /// 尻尾ボーンの候補を追加
        /// </summary>
        private static void AddTailCandidate(Transform t, HashSet<Transform> boneSet, HashSet<Transform> topmostCandidates)
        {
            var chain = new List<Transform> { t };
            Transform cur = t;
            while (cur.parent != null)
            {
                var p = cur.parent;
                var pLower = p.name.ToLowerInvariant();
                if (pLower.Contains("tail"))
                {
                    chain.Add(p);
                    cur = p;
                    continue;
                }
                break;
            }
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                var candidate = chain[i];
                if (boneSet.Contains(candidate))
                {
                    topmostCandidates.Add(candidate);
                    break;
                }
            }
        }

        #endregion
    }
}
