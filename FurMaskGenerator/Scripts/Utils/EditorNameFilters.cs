#if UNITY_EDITOR
using System;
using Mask.Generator.Constants;

namespace Mask.Generator.Utils
{
    public static class EditorNameFilters
    {
        public static bool IsEarAccessoryName(string nameOrLower)
        {
            if (string.IsNullOrEmpty(nameOrLower)) return false;
            var s = nameOrLower.ToLowerInvariant();
            return s.Contains(UIConstants.FILTER_WEAR) || s.Contains(UIConstants.FILTER_EARRING);
        }

        public static bool IsAvatarRendererCandidate(string nameOrLower)
        {
            if (string.IsNullOrEmpty(nameOrLower)) return false;
            var s = nameOrLower.ToLowerInvariant();
            if (IsEarAccessoryName(s)) return false;
            return s.Contains(UIConstants.FILTER_BODY) || s.Contains(UIConstants.FILTER_TAIL) || s.Contains(UIConstants.FILTER_EAR);
        }
    }
}
#endif
