using UnityEditor;
using UnityEngine;
using System.IO;

namespace GroomingTool2.State
{
    internal static class GroomingTool2StateRepository
    {
        private const string AssetName = "GroomingTool2State";

        public static GroomingTool2State LoadOrCreate()
        {
            var assetPath = $"Assets/Editor/{AssetName}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<GroomingTool2State>(assetPath);
            if (existing != null)
                return existing;

            EnsureAssetFolderExists();

            var instance = ScriptableObject.CreateInstance<GroomingTool2State>();
            AssetDatabase.CreateAsset(instance, assetPath);
            AssetDatabase.SaveAssets();
            return instance;
        }

        private static void EnsureAssetFolderExists()
        {
            // Ensure parent folder exists before creating the asset
            if (!AssetDatabase.IsValidFolder("Assets/Editor"))
            {
                if (!AssetDatabase.IsValidFolder("Assets"))
                {
                    // In normal Unity projects, "Assets" always exists, but guard just in case
                    Directory.CreateDirectory("Assets");
                }
                AssetDatabase.CreateFolder("Assets", "Editor");
            }
        }
    }
}
