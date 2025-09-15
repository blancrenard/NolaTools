#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Data;
using NolaTools.FurMaskGenerator.Constants;

namespace NolaTools.FurMaskGenerator.Utils
{
    /// <summary>
    /// マテリアルからファーノーマルマップを取得するユーティリティ
    /// </summary>
    public static class FurNormalMapUtils
    {
        // ファーノーマルマッププロパティ名
        private const string FUR_VECTOR_TEX_PROPERTY = "_FurVectorTex";
        private const string FUR_VECTOR_SCALE_PROPERTY = "_FurVectorScale";
        
        // ファーの長さプロパティ名（_FurVectorのw成分）
        private const string FUR_VECTOR_PROPERTY = "_FurVector";
        
        /// <summary>
        /// 指定されたレンダラーリストからファーノーマルマップを取得
        /// </summary>
        /// <param name="renderers">対象レンダラーリスト</param>
        /// <returns>マテリアル名とファーノーマルマップのペア</returns>
        public static Dictionary<string, Texture2D> GetFurNormalMapsFromRenderers(List<Renderer> renderers)
        {
            var furNormalMaps = new Dictionary<string, Texture2D>();
            
            if (renderers == null) return furNormalMaps;
            
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMaterials == null) continue;
                
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    
                    // 対応シェーダーかどうかチェック
                    if (!IsSupportedShader(material)) continue;
                    
                    // ファーノーマルマップを取得
                    var furNormalMap = GetFurNormalMapFromMaterial(material);
                    if (furNormalMap != null)
                    {
                        furNormalMaps[material.name] = furNormalMap;
                    }
                }
            }
            
            return furNormalMaps;
        }
        
        /// <summary>
        /// マテリアルが対応シェーダーかどうかチェック
        /// </summary>
        /// <param name="material">チェック対象マテリアル</param>
        /// <returns>対応シェーダーの場合true</returns>
        public static bool IsSupportedShader(Material material)
        {
            if (material == null || material.shader == null) return false;
            
            string shaderName = material.shader.name.ToLower();
            return shaderName.Contains("liltoon") || shaderName.Contains("lts");
        }
        
        /// <summary>
        /// マテリアルからファーノーマルマップを取得
        /// </summary>
        /// <param name="material">対象マテリアル</param>
        /// <returns>ファーノーマルマップテクスチャ（設定されていない場合はnull）</returns>
        public static Texture2D GetFurNormalMapFromMaterial(Material material)
        {
            if (material == null) return null;
            
            // ファーノーマルマッププロパティが存在するかチェック
            if (!material.HasProperty(FUR_VECTOR_TEX_PROPERTY)) return null;
            
            // ファーノーマルマップテクスチャを取得
            var furNormalMap = material.GetTexture(FUR_VECTOR_TEX_PROPERTY) as Texture2D;
            return furNormalMap;
        }
        
        /// <summary>
        /// マテリアルからファーノーマルマップのスケール値を取得
        /// </summary>
        /// <param name="material">対象マテリアル</param>
        /// <returns>ファーノーマルマップのスケール値（デフォルト1.0）</returns>
        public static float GetFurNormalMapScale(Material material)
        {
            if (material == null) return 1.0f;
            
            if (!material.HasProperty(FUR_VECTOR_SCALE_PROPERTY)) return 1.0f;
            
            return material.GetFloat(FUR_VECTOR_SCALE_PROPERTY);
        }
        
        /// <summary>
        /// マテリアルからファーの長さ値を取得（_FurVectorのw成分）
        /// </summary>
        /// <param name="material">対象マテリアル</param>
        /// <returns>ファーの長さ値（デフォルト0.04）</returns>
        public static float GetFurLength(Material material)
        {
            if (material == null) return 0.04f; // FurMaskSettingsのDEFAULT_MAX_DISTANCE
            
            if (!material.HasProperty(FUR_VECTOR_PROPERTY)) return 0.04f;
            
            // _FurVectorのw成分（4番目の値）がファーの長さ
            Vector4 furVector = material.GetVector(FUR_VECTOR_PROPERTY);
            return furVector.w;
        }
        
        /// <summary>
        /// 指定されたレンダラーリストから最適なファーの長さを取得
        /// </summary>
        /// <param name="renderers">対象レンダラーリスト</param>
        /// <returns>最も多く使用されているファーの長さ値（デフォルト0.04）</returns>
        public static float GetOptimalFurLength(List<Renderer> renderers)
        {
            var lengthCounts = new Dictionary<float, int>();
            int totalCount = 0;
            
            if (renderers == null) return 0.04f;
            
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMaterials == null) continue;
                
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || !IsSupportedShader(material)) continue;
                    
                    // ファーノーマルマップが設定されているマテリアルのみを対象とする
                    if (!material.HasProperty(FUR_VECTOR_TEX_PROPERTY)) continue;
                    var furNormalMap = material.GetTexture(FUR_VECTOR_TEX_PROPERTY) as Texture2D;
                    if (furNormalMap == null) continue;
                    
                    // _FurVectorプロパティが存在することも確認
                    if (!material.HasProperty(FUR_VECTOR_PROPERTY)) continue;
                    
                    var furLength = GetFurLength(material);
                    totalCount++;
                    
                    if (lengthCounts.ContainsKey(furLength))
                    {
                        lengthCounts[furLength]++;
                    }
                    else
                    {
                        lengthCounts[furLength] = 1;
                    }
                }
            }
            
            if (lengthCounts.Count == 0) return 0.04f;
            
            // 最も多く使用されている長さを返す
            float mostCommonLength = 0.04f;
            int maxCount = 0;
            
            foreach (var kvp in lengthCounts)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    mostCommonLength = kvp.Key;
                }
            }
            
            return mostCommonLength;
        }
        
        /// <summary>
        /// ファーノーマルマップをFurMaskGeneratorのファーの傾き設定に変換
        /// </summary>
        /// <param name="furNormalMaps">ファーノーマルマップの辞書</param>
        /// <param name="renderers">対象レンダラーリスト（スケール値取得用）</param>
        /// <returns>MaterialNormalMapDataのリスト</returns>
        public static List<MaterialNormalMapData> ConvertToMaterialNormalMapData(
            Dictionary<string, Texture2D> furNormalMaps, 
            List<Renderer> renderers)
        {
            var normalMapDataList = new List<MaterialNormalMapData>();
            
            if (furNormalMaps == null) return normalMapDataList;
            
            // レンダラーからマテリアル名とスケール値のマッピングを作成
            var materialScaleMap = new Dictionary<string, float>();
            if (renderers != null)
            {
                foreach (var renderer in renderers)
                {
                    if (renderer == null || renderer.sharedMaterials == null) continue;
                    
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null || !IsSupportedShader(material)) continue;
                        
                        var scale = GetFurNormalMapScale(material);
                        materialScaleMap[material.name] = scale;
                    }
                }
            }
            
            // ファーノーマルマップをMaterialNormalMapDataに変換
            foreach (var kvp in furNormalMaps)
            {
                var materialName = kvp.Key;
                var furNormalMap = kvp.Value;
                
                var normalMapData = new MaterialNormalMapData
                {
                    materialName = materialName,
                    normalMap = furNormalMap,
                    intensity = 1.0f,
                    isPackedAG = false, // ファーノーマルマップは通常RGB形式
                    normalStrength = materialScaleMap.TryGetValue(materialName, out float scale) ? scale : 1.0f
                };
                
                normalMapDataList.Add(normalMapData);
            }
            
            return normalMapDataList;
        }
    }
}
#endif
