#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mask.Generator.Utils
{
    public static class RendererDetectionUtils
    {
        public static void PartitionAvatarAndClothRenderers(GameObject avatar, IList<Renderer> avatarOut, IList<Renderer> clothOut)
        {
            if (avatar == null || avatarOut == null || clothOut == null) return;
            var all = avatar.GetComponentsInChildren<Renderer>(true);
            foreach (var r in all)
            {
                if (r == null || r.gameObject == null) continue;
                var lowerName = r.gameObject.name.ToLowerInvariant();
                if (EditorNameFilters.IsEarAccessoryName(lowerName))
                {
                    clothOut.Add(r);
                }
                else if (EditorNameFilters.IsAvatarRendererCandidate(lowerName))
                {
                    avatarOut.Add(r);
                }
                else
                {
                    clothOut.Add(r);
                }
            }
        }

        public static List<SkinnedMeshRenderer> DetectTargetSkinnedMeshRenderers(GameObject avatar)
        {
            if (avatar == null) return new List<SkinnedMeshRenderer>();
            var renderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            return renderers.Where(r =>
            {
                if (r == null || r.gameObject == null) return false;
                var lowerName = r.gameObject.name.ToLowerInvariant();
                return EditorNameFilters.IsAvatarRendererCandidate(lowerName);
            }).ToList();
        }
    }
}
#endif
