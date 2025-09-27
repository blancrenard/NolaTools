#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Constants;
using NolaTools.FurMaskGenerator.Utils;

namespace NolaTools.FurMaskGenerator
{
    public partial class FurMaskGenerator
    {
        // Mask generation settings UI
        void DrawMaskGenerationSettings()
        {
            UIDrawingUtils.DrawInUIBox(() =>
            {
                // Texture size with auto-detection
                EditorGUILayout.BeginHorizontal();
                settings.textureSizeIndex = EditorGUILayout.Popup(
                    UILabels.TEXTURE_SIZE_LABEL,
                    settings.textureSizeIndex,
                    AppSettings.TEXTURE_SIZE_LABELS);
                
                if (GUILayout.Button(UILabels.AUTO_DETECT_BUTTON, GUILayout.Width(80)))
                {
                    AutoDetectTextureSize();
                }
                EditorGUILayout.EndHorizontal();

                // マスクの濃さ設定
                settings.gamma = EditorGUILayout.Slider(
                    UILabels.MASK_INTENSITY_LABEL,
                    settings.gamma,
                    0.1f,
                    5.0f);

                // 細分化回数設定
                settings.tempSubdivisionIterations = EditorGUILayout.IntSlider(
                    new GUIContent(UILabels.SUBDIVISION_ITERATIONS_LABEL, UILabels.SUBDIVISION_ITERATIONS_TOOLTIP),
                    settings.tempSubdivisionIterations,
                    0,
                    3);

                // スムージング回数設定
                settings.uvIslandVertexSmoothIterations = EditorGUILayout.IntSlider(
                    "スムージング回数",
                    settings.uvIslandVertexSmoothIterations,
                    0,
                    8);

                // エッジパディング設定
                settings.edgePaddingSize = EditorGUILayout.IntSlider(
                    new GUIContent(UILabels.EDGE_PADDING_LABEL, UILabels.EDGE_PADDING_TOOLTIP),
                    settings.edgePaddingSize,
                    0,
                    32);

                // 透過モード設定
                settings.useTransparentMode = EditorGUILayout.Toggle(
                    new GUIContent(UILabels.TRANSPARENT_MODE_LABEL, UILabels.TRANSPARENT_MODE_TOOLTIP),
                    settings.useTransparentMode);

                // Generate button
                GUI.enabled = !baking;
                if (GUILayout.Button(UILabels.GENERATE_MASK_BUTTON))
                {
                    if (ValidateInputs())
                    {
                        StartBake();
                    }
                }
                GUI.enabled = true;
            });
        }

        /// <summary>
        /// 指定されたレンダラーとサブメッシュのメインテクスチャを取得
        /// </summary>
        private Texture2D GetMainTextureForRenderer(Renderer renderer, int submeshIndex)
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
        private bool IsMaterialPropertyValid(Material material, string propertyName)
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
        private bool IsShaderCompatible(Material material)
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
        /// レンダラーを優先順位に従ってソート（bodyを最優先、それ以降は順不同）
        /// </summary>
        private IEnumerable<Renderer> GetRenderersInPriorityOrder()
        {
            var allRenderers = avatarRenderers.Concat(clothRenderers).Where(r => r != null);
            
            // bodyという名前のレンダラーを最優先で取得
            var bodyRenderers = allRenderers.Where(r => 
                r.gameObject.name.ToLowerInvariant().Contains("body"));
            
            // その他のレンダラー（body以外）
            var otherRenderers = allRenderers.Where(r => 
                !r.gameObject.name.ToLowerInvariant().Contains("body"));
            
            // bodyを先頭に、その他を後に配置
            return bodyRenderers.Concat(otherRenderers);
        }

        /// <summary>
        /// テクスチャサイズを自動検出して設定
        /// </summary>
        private void AutoDetectTextureSize()
        {
            if (avatarRenderers == null || avatarRenderers.Count == 0)
            {
                EditorUtility.DisplayDialog(UILabels.ERROR_DIALOG_TITLE, ErrorMessages.ERROR_RENDERERS_NOT_SET, UILabels.ERROR_DIALOG_OK);
                return;
            }
            
            var detectedSizes = new Dictionary<int, int>(); // サイズ -> カウント
            int totalTextures = 0;
            
            // 優先順位に従ってレンダラーを処理
            foreach (var renderer in GetRenderersInPriorityOrder())
            {
                if (renderer == null || renderer.sharedMaterials == null) continue;
                
                for (int submeshIndex = 0; submeshIndex < renderer.sharedMaterials.Length; submeshIndex++)
                {
                    var texture = GetMainTextureForRenderer(renderer, submeshIndex);
                    if (texture != null)
                    {
                        totalTextures++;
                        // 正方形テクスチャの場合のみカウント（通常のマスクテクスチャは正方形）
                        if (texture.width == texture.height)
                        {
                            int size = texture.width;
                            bool isBodyRenderer = renderer.gameObject.name.ToLowerInvariant().Contains("body");
                            
                            // bodyレンダラーのテクスチャが見つかった場合、重みを高く設定
                            int weight = isBodyRenderer ? 10 : 1;
                            
                            if (detectedSizes.ContainsKey(size))
                            {
                                detectedSizes[size] += weight;
                            }
                            else
                            {
                                detectedSizes[size] = weight;
                            }
                            
                        }
                    }
                }
            }
            
            if (detectedSizes.Count == 0)
            {
                EditorUtility.DisplayDialog(ErrorMessages.TEXTURE_AUTO_DETECT_TITLE, 
                    ErrorMessages.TEXTURE_AUTO_DETECT_NO_TEXTURE, UILabels.ERROR_DIALOG_OK);
                return;
            }
            
            // 最も多く使用されているサイズを取得（bodyレンダラーは重み10倍）
            var mostCommonSize = detectedSizes.OrderByDescending(kvp => kvp.Value).First();
            
            // AppSettings.TEXTURE_SIZESの中から最適なサイズインデックスを見つける
            int bestIndex = -1;
            for (int i = 0; i < AppSettings.TEXTURE_SIZES.Length; i++)
            {
                if (AppSettings.TEXTURE_SIZES[i] == mostCommonSize.Key)
                {
                    bestIndex = i;
                    break;
                }
            }
            
            // 完全一致がない場合、最も近いサイズを選択
            if (bestIndex == -1)
            {
                int minDiff = int.MaxValue;
                for (int i = 0; i < AppSettings.TEXTURE_SIZES.Length; i++)
                {
                    int diff = Mathf.Abs(AppSettings.TEXTURE_SIZES[i] - mostCommonSize.Key);
                    if (diff < minDiff)
                    {
                        minDiff = diff;
                        bestIndex = i;
                    }
                }
            }
            
            if (bestIndex >= 0)
            {
                UndoRedoUtils.RecordUndoSetDirtyAndScheduleSave(settings, UndoMessages.AUTO_DETECT_TEXTURE_SIZE);
                settings.textureSizeIndex = bestIndex;
            }
            else
            {
                EditorUtility.DisplayDialog(ErrorMessages.TEXTURE_AUTO_DETECT_TITLE, 
                    ErrorMessages.TEXTURE_AUTO_DETECT_NO_SIZE, UILabels.ERROR_DIALOG_OK);
            }
        }
        
        /// <summary>
        /// シェーダーキーワードスペースエラー対策の安全なテクスチャ取得
        /// 複数のフォールバック戦略を使用
        /// </summary>
        private Texture2D GetTextureFromMaterialSafely(Material material, string propertyName)
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
        private Texture2D TryGetTextureViaSerializedObject(Material material, string propertyName)
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
        private Texture2D TryGetTextureViaAssetDatabase(Material material, string propertyName)
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
        private Texture2D TryGetTextureWithMaterialReset(Material material, string propertyName)
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
        private Texture2D TryGetTextureWithNewInstance(Material material, string propertyName)
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


