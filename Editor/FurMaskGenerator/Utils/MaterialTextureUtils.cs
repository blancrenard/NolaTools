#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator.Utils
{
    /// <summary>
    /// マテリアルからテクスチャを安全に取得するためのユーティリティクラス
    /// 複数のフォールバック戦略を使用してシェーダーキーワードスペースエラーに対応
    /// </summary>
    public static class MaterialTextureUtils
    {
        /// <summary>
        /// 指定されたレンダラーとサブメッシュのメインテクスチャを取得
        /// </summary>
        public static Texture2D GetMainTextureForRenderer(Renderer renderer, int submeshIndex)
        {
            if (renderer == null || renderer.sharedMaterials == null || renderer.sharedMaterials.Length == 0)
                return null;
                
            int materialIndex = Mathf.Clamp(submeshIndex, 0, renderer.sharedMaterials.Length - 1);
            Material material = renderer.sharedMaterials[materialIndex];
            
            if (material == null) return null;
            
            // シェーダーが有効かチェック
            if (material.shader == null || !material.shader.isSupported)
            {
                return null;
            }
            
            // シェーダーの互換性をチェック
            if (!IsShaderCompatible(material))
            {
                return null;
            }
            
            // 一般的なメインテクスチャプロパティを試行
            foreach (string property in GameObjectConstants.MAIN_TEXTURE_PROPERTIES)
            {
                // プロパティ名が有効かチェック
                if (string.IsNullOrEmpty(property) || !property.StartsWith("_"))
                {
                    continue;
                }
                
                try
                {
                    // より安全なプロパティチェック
                    if (IsMaterialPropertyValid(material, property))
                    {
                        // シェーダーキーワードスペース対応の安全なテクスチャ取得
                        Texture2D texture2D = GetTextureFromMaterialSafely(material, property);
                        if (texture2D != null)
                        {
                            return texture2D;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    // プロパティ名が無効な場合やマテリアルが破損している場合はスキップ
                    Debug.LogWarning($"[FurMaskGenerator] マテリアルプロパティ '{property}' のチェック中にエラー (Material: {material.name}, Shader: {material.shader?.name}): {ex.Message}");
                    continue;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// マテリアルプロパティが有効かチェック（安全版）
        /// </summary>
        public static bool IsMaterialPropertyValid(Material material, string propertyName)
        {
            if (material == null || string.IsNullOrEmpty(propertyName))
                return false;
                
            try
            {
                // シェーダーが有効かチェック
                if (material.shader == null || !material.shader.isSupported)
                    return false;
                
                // プロパティ名の基本チェック
                if (!propertyName.StartsWith("_"))
                    return false;
                
                // シェーダーのプロパティ数を取得してチェック
                int propertyCount = material.shader.GetPropertyCount();
                if (propertyCount <= 0)
                    return false;
                
                // プロパティ名を直接チェック（HasPropertyの代替）
                for (int i = 0; i < propertyCount; i++)
                {
                    if (material.shader.GetPropertyName(i) == propertyName)
                    {
                        // プロパティタイプがテクスチャかチェック
                        var propertyType = material.shader.GetPropertyType(i);
                        return propertyType == UnityEngine.Rendering.ShaderPropertyType.Texture;
                    }
                }
                
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FurMaskGenerator] プロパティ '{propertyName}' の検証中にエラー: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// シェーダーの互換性をチェック
        /// </summary>
        public static bool IsShaderCompatible(Material material)
        {
            if (material == null || material.shader == null)
                return false;
                
            try
            {
                // シェーダーが有効かチェック
                if (!material.shader.isSupported)
                {
                    Debug.LogWarning($"[FurMaskGenerator] シェーダー '{material.shader.name}' はサポートされていません。");
                    return false;
                }
                
                // シェーダーのプロパティ数が0でないかチェック
                int propertyCount = material.shader.GetPropertyCount();
                if (propertyCount <= 0)
                {
                    Debug.LogWarning($"[FurMaskGenerator] シェーダー '{material.shader.name}' にプロパティがありません。");
                    return false;
                }
                
                // シェーダーのキーワードをチェック（安全に）
                try
                {
                    var keywords = material.shaderKeywords;
                    if (keywords != null && keywords.Length > 0)
                    {
                        // 互換性のないキーワードがないかチェック
                        foreach (string keyword in keywords)
                        {
                            if (string.IsNullOrEmpty(keyword))
                                continue;
                                
                            // 特定のキーワードパターンをチェック
                            if (keyword.Contains("_INCOMPATIBLE") || keyword.Contains("_UNSUPPORTED"))
                            {
                                Debug.LogWarning($"[FurMaskGenerator] 互換性のないキーワード '{keyword}' が検出されました。");
                                return false;
                            }
                        }
                    }
                }
                catch (System.Exception keywordEx)
                {
                    // キーワードチェックでエラーが発生した場合は警告を出して続行
                    Debug.LogWarning($"[FurMaskGenerator] キーワードチェック中にエラーが発生しましたが、続行します: {keywordEx.Message}");
                }
                
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FurMaskGenerator] シェーダー互換性チェック中にエラーが発生しました: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// シェーダーキーワードスペースエラー対策の安全なテクスチャ取得
        /// 複数のフォールバック戦略を使用
        /// </summary>
        public static Texture2D GetTextureFromMaterialSafely(Material material, string propertyName)
        {
            if (material == null || string.IsNullOrEmpty(propertyName))
                return null;
                
            // 戦略1: SerializedObjectによる直接アクセス（最も安全）
            Texture2D result = TryGetTextureViaSerializedObject(material, propertyName);
            if (result != null) return result;
            
            // 戦略2: AssetDatabaseによるアクセス
            result = TryGetTextureViaAssetDatabase(material, propertyName);
            if (result != null) return result;
            
            // 戦略3: マテリアル状態の完全リセット後の取得
            result = TryGetTextureWithMaterialReset(material, propertyName);
            if (result != null) return result;
            
            // 戦略4: 新しいマテリアルインスタンスでの取得
            result = TryGetTextureWithNewInstance(material, propertyName);
            if (result != null) return result;
            
            Debug.LogWarning($"[FurMaskGenerator] 全ての戦略でテクスチャ取得に失敗 (Property: {propertyName}, Material: {material.name})");
            return null;
        }
        
        /// <summary>
        /// SerializedObjectを使った安全なテクスチャ取得
        /// </summary>
        private static Texture2D TryGetTextureViaSerializedObject(Material material, string propertyName)
        {
            try
            {
                var serializedMaterial = new SerializedObject(material);
                serializedMaterial.Update();
                
                // m_SavedProperties.m_TexEnvsから該当プロパティを検索
                var texEnvsProperty = serializedMaterial.FindProperty("m_SavedProperties.m_TexEnvs");
                if (texEnvsProperty != null && texEnvsProperty.isArray)
                {
                    for (int i = 0; i < texEnvsProperty.arraySize; i++)
                    {
                        var element = texEnvsProperty.GetArrayElementAtIndex(i);
                        var firstProperty = element.FindPropertyRelative("first");
                        
                        if (firstProperty != null && firstProperty.stringValue == propertyName)
                        {
                            var textureProperty = element.FindPropertyRelative("second.m_Texture");
                            if (textureProperty != null && textureProperty.objectReferenceValue is Texture2D texture2D)
                            {
                                return texture2D;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FurMaskGenerator] SerializedObject戦略失敗 (Property: {propertyName}): {ex.Message}");
            }
            return null;
        }
        
        /// <summary>
        /// AssetDatabaseを使った安全なテクスチャ取得
        /// </summary>
        private static Texture2D TryGetTextureViaAssetDatabase(Material material, string propertyName)
        {
            try
            {
                // マテリアルの依存関係からテクスチャを取得
                string materialPath = AssetDatabase.GetAssetPath(material);
                if (!string.IsNullOrEmpty(materialPath))
                {
                    var dependencies = AssetDatabase.GetDependencies(materialPath, false);
                    
                    // メインテクスチャの命名パターンを優先的に検索
                    string[] mainTexturePatterns = { "_MainTex", "_BaseMap", "_DiffuseMap", "main", "diffuse", "albedo" };
                    
                    foreach (string depPath in dependencies)
                    {
                        if (depPath.EndsWith(".png") || depPath.EndsWith(".jpg") || depPath.EndsWith(".tga") || depPath.EndsWith(".exr"))
                        {
                            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(depPath);
                            if (texture != null)
                            {
                                // プロパティ名またはテクスチャ名にメインテクスチャのパターンが含まれているかチェック
                                string textureName = texture.name.ToLower();
                                string propertyLower = propertyName.ToLower();
                                
                                if (propertyLower.Contains("main") || propertyLower.Contains("diffuse") || propertyLower.Contains("albedo") ||
                                    textureName.Contains("main") || textureName.Contains("diffuse") || textureName.Contains("albedo"))
                                {
                                    return texture;
                                }
                            }
                        }
                    }
                    
                    // パターンマッチしない場合は最初のテクスチャを返す
                    foreach (string depPath in dependencies)
                    {
                        if (depPath.EndsWith(".png") || depPath.EndsWith(".jpg") || depPath.EndsWith(".tga") || depPath.EndsWith(".exr"))
                        {
                            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(depPath);
                            if (texture != null)
                            {
                                return texture;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FurMaskGenerator] AssetDatabase戦略失敗 (Property: {propertyName}): {ex.Message}");
            }
            return null;
        }
        
        /// <summary>
        /// マテリアル状態の完全リセット後のテクスチャ取得
        /// </summary>
        private static Texture2D TryGetTextureWithMaterialReset(Material material, string propertyName)
        {
            try
            {
                // マテリアル状態の完全リセット
                var originalKeywords = material.shaderKeywords;
                
                try
                {
                    // 段階的なキーワードリセット
                    material.shaderKeywords = new string[0];
                    
                    // シェーダーの再適用を試行
                    var originalShader = material.shader;
                    material.shader = null;
                    material.shader = originalShader;
                    
                    // CRCの再計算
                    material.ComputeCRC();
                    
                    // テクスチャ取得を試行
                    var texture = material.GetTexture(propertyName) as Texture2D;
                    
                    // キーワードを復元
                    material.shaderKeywords = originalKeywords;
                    
                    return texture;
                }
                catch (System.Exception innerEx)
                {
                    // キーワードを復元
                    try
                    {
                        material.shaderKeywords = originalKeywords;
                    }
                    catch (System.Exception restoreEx)
                    {
                        Debug.LogWarning($"[FurMaskGenerator] キーワード復元に失敗: {restoreEx.Message}");
                    }
                    
                    Debug.LogWarning($"[FurMaskGenerator] マテリアルリセット中にエラー: {innerEx.Message}");
                    return null;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FurMaskGenerator] マテリアルリセット戦略失敗 (Property: {propertyName}): {ex.Message}");
            }
            return null;
        }
        
        /// <summary>
        /// 新しいマテリアルインスタンスでのテクスチャ取得
        /// </summary>
        private static Texture2D TryGetTextureWithNewInstance(Material material, string propertyName)
        {
            try
            {
                // 一時的なマテリアルインスタンスを作成
                var tempMaterial = new Material(material.shader);
                tempMaterial.CopyPropertiesFromMaterial(material);
                
                // 新しいインスタンスでテクスチャ取得を試行
                var texture = tempMaterial.GetTexture(propertyName) as Texture2D;
                
                // 一時マテリアルをクリーンアップ
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(tempMaterial);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(tempMaterial);
                }
                
                return texture;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FurMaskGenerator] 新しいインスタンス戦略失敗 (Property: {propertyName}): {ex.Message}");
            }
            return null;
        }
    }
}

#endif
