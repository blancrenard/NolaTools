using UnityEngine;
using GroomingTool2.Core;
using GroomingTool2.Constants;

namespace GroomingTool2.Utils
{
    internal static class MaterialUtils
    {
        public static Texture2D GetMainTextureFromMaterial(Material material)
        {
            if (material == null || material.shader == null) return null;
            foreach (var prop in GameObjectConstants.MAIN_TEXTURE_PROPERTIES)
            {
                try
                {
                    if (!string.IsNullOrEmpty(prop) && material.HasProperty(prop))
                    {
                        var tex = material.GetTexture(prop) as Texture2D;
                        if (tex != null) return tex;
                    }
                }
                catch { }
            }
            return null;
        }
    }
}
