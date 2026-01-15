using UnityEngine;
using UnityEditor;
using System.IO;

namespace NolaTools.Editor
{
    public class MaterialConverter : EditorWindow
    {
        private Material sourceMaterial;
        private Texture2D furNormalMap;
        private Texture2D furLengthMask;
        private int furTypeIndex = 0;

        private readonly string[] furTypeNames = new string[]
        {
            "ふさふさ",
            "もこもこ",
            "ふさふさ（軽量）"
        };

        private const string FUR_SHADER_TWO_PASS_PATH = "Packages/jp.lilxyzw.liltoon/Shader/lts_fur_two.shader";
        private const string FUR_SHADER_CUTOUT_PATH = "Packages/jp.lilxyzw.liltoon/Shader/lts_fur_cutout.shader";
        private const string FUR_NOISE_TEXTURE_PATH = "Packages/jp.lilxyzw.liltoon/Texture/lil_noise_fur.png";

        [MenuItem("Tools/FurTools/MaterialConverter for lilToon")]
        public static void ShowWindow()
        {
            GetWindow<MaterialConverter>("MaterialConverter for lilToon");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(5);

            // Source Material
            sourceMaterial = (Material)EditorGUILayout.ObjectField(
                "Material",
                sourceMaterial,
                typeof(Material),
                false
            );

            EditorGUILayout.Space(5);

            // Fur Normal Map
            furNormalMap = (Texture2D)EditorGUILayout.ObjectField(
                "ノーマルマップ",
                furNormalMap,
                typeof(Texture2D),
                false
            );

            EditorGUILayout.Space(5);

            // Fur Length Mask
            furLengthMask = (Texture2D)EditorGUILayout.ObjectField(
                "マスク",
                furLengthMask,
                typeof(Texture2D),
                false
            );

            EditorGUILayout.Space(5);

            // Fur Type Selection
            furTypeIndex = EditorGUILayout.Popup("質感", furTypeIndex, furTypeNames);

            EditorGUILayout.Space(10);

            // Convert Button
            EditorGUI.BeginDisabledGroup(sourceMaterial == null);
            if (GUILayout.Button("ファー用マテリアルに変換", GUILayout.Height(24)))
            {
                ConvertToFurMaterial();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void ConvertToFurMaterial()
        {
            // Validate source material uses lilToon shader
            if (sourceMaterial == null)
            {
                EditorUtility.DisplayDialog("エラー", "マテリアルが設定されていません。", "OK");
                return;
            }

            if (!sourceMaterial.shader.name.Contains("lilToon"))
            {
                EditorUtility.DisplayDialog("エラー", "lilToonシェーダーを使用したマテリアルではありません。", "OK");
                return;
            }

            // Determine shader path based on fur type
            string shaderPath = (furTypeIndex == 0) ? FUR_SHADER_TWO_PASS_PATH : FUR_SHADER_CUTOUT_PATH;

            // Load Fur shader from path
            Shader furShader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (furShader == null)
            {
                EditorUtility.DisplayDialog(
                    "エラー",
                    $"lilToonのファーシェーダーが見つかりません。\nlilToonがインストールされているか確認してください。\n\nパス: {shaderPath}",
                    "OK"
                );
                return;
            }

            // Load fur noise texture
            Texture2D furNoiseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(FUR_NOISE_TEXTURE_PATH);
            if (furNoiseTexture == null)
            {
                EditorUtility.DisplayDialog(
                    "エラー",
                    $"ファー用ノイズテクスチャが見つかりません。\nlilToonがインストールされているか確認してください。\n\nパス: {FUR_NOISE_TEXTURE_PATH}",
                    "OK"
                );
                return;
            }

            // Get source material path and directory
            string sourcePath = AssetDatabase.GetAssetPath(sourceMaterial);
            string sourceDirectory = Path.GetDirectoryName(sourcePath);
            string sourceName = Path.GetFileNameWithoutExtension(sourcePath);

            // Generate unique file name
            string newMaterialName = GenerateUniqueName(sourceDirectory, sourceName + "_fur");
            string newMaterialPath = Path.Combine(sourceDirectory, newMaterialName + ".mat");

            // Create new material by copying source
            Material newMaterial = new Material(sourceMaterial);
            newMaterial.name = newMaterialName;

            // Change shader to Fur shader
            newMaterial.shader = furShader;

            // Apply settings based on fur type
            ApplyFurSettings(newMaterial, furNoiseTexture);

            // Set Fur Normal Map (FurVectorTex) if specified
            if (furNormalMap != null)
            {
                newMaterial.SetTexture("_FurVectorTex", furNormalMap);
            }

            // Set Fur Length Mask and Fur Mask if specified
            if (furLengthMask != null)
            {
                newMaterial.SetTexture("_FurLengthMask", furLengthMask);
                newMaterial.SetTexture("_FurMask", furLengthMask);
            }

            // Save the new material
            AssetDatabase.CreateAsset(newMaterial, newMaterialPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Select the new material in Project window
            Selection.activeObject = newMaterial;
            EditorGUIUtility.PingObject(newMaterial);

            EditorUtility.DisplayDialog(
                "完了",
                $"ファーマテリアルを作成しました。\n\n保存先: {newMaterialPath}",
                "OK"
            );
        }

        private void ApplyFurSettings(Material material, Texture2D furNoiseTexture)
        {
            // Common settings
            material.SetFloat("_FurGravity", 0.1f);
            material.SetFloat("_FurRandomize", 0.1f);
            material.SetTexture("_FurNoiseMask", furNoiseTexture);

            switch (furTypeIndex)
            {
                case 0: // ふさふさ (Two Pass - Transparent)
                    // Blend mode for transparent fur rendering
                    // SrcAlpha = 5, OneMinusSrcAlpha = 10
                    material.SetFloat("_SrcBlend", 5f);
                    material.SetFloat("_DstBlend", 10f);
                    material.SetFloat("_FurSrcBlend", 5f);
                    material.SetFloat("_FurDstBlend", 10f);
                    material.SetFloat("_Cutoff", 0.001f);
                    // Tiling X:64, Y:64
                    material.SetTextureScale("_FurNoiseMask", new Vector2(64f, 64f));
                    break;

                case 1: // もこもこ (Cutout)
                    // Blend mode for cutout fur rendering
                    // One = 1, Zero = 0
                    material.SetFloat("_SrcBlend", 1f);
                    material.SetFloat("_DstBlend", 0f);
                    material.SetFloat("_FurSrcBlend", 1f);
                    material.SetFloat("_FurDstBlend", 0f);
                    material.SetFloat("_Cutoff", 0.5f);
                    // Tiling X:4, Y:4
                    material.SetTextureScale("_FurNoiseMask", new Vector2(4f, 4f));
                    break;

                case 2: // ふさふさ（軽量） (Cutout - Lightweight)
                    // Blend mode for cutout fur rendering
                    // One = 1, Zero = 0
                    material.SetFloat("_SrcBlend", 1f);
                    material.SetFloat("_DstBlend", 0f);
                    material.SetFloat("_FurSrcBlend", 1f);
                    material.SetFloat("_FurDstBlend", 0f);
                    material.SetFloat("_Cutoff", 0.5f);
                    // Tiling X:64, Y:64
                    material.SetTextureScale("_FurNoiseMask", new Vector2(64f, 64f));
                    // Layer count: 1
                    material.SetFloat("_FurLayerNum", 1f);
                    break;
            }
        }

        private string GenerateUniqueName(string directory, string baseName)
        {
            string name = baseName;
            string path = Path.Combine(directory, name + ".mat");

            if (!File.Exists(path))
            {
                return name;
            }

            int counter = 1;
            while (true)
            {
                name = $"{baseName} {counter}";
                path = Path.Combine(directory, name + ".mat");

                if (!File.Exists(path))
                {
                    return name;
                }

                counter++;
            }
        }
    }
}
