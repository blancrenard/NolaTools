using System;
using System.Collections.Generic;
using UnityEngine;
using Mask.Generator.Constants;
using Mask.Generator.Utils;

namespace Mask.Generator.Data
{
    #region Main Settings

    /// <summary>
    /// Fur Mask Generatorのメイン設定
    /// マスク生成の全設定データを保持
    /// </summary>
    [CreateAssetMenu(fileName = "FurMaskSettings", menuName = FileConstants.ASSET_MENU_NAME)]
    public class FurMaskSettings : ScriptableObject
    {
        // Default value constants
        public const float DEFAULT_MAX_DISTANCE = AppSettings.DEFAULT_MAX_DISTANCE;
        public const float DEFAULT_GAMMA = AppSettings.DEFAULT_GAMMA;
        public const int DEFAULT_TEXTURE_SIZE_INDEX = AppSettings.DEFAULT_TEXTURE_SIZE_INDEX;

        // Avatar path (for persistence)
        public string avatarObjectPath;

        // Renderer paths for persistence
        public List<string> avatarRendererPaths = new List<string>();
        public List<string> clothRendererPaths = new List<string>();

        // Sphere masks (This remains as is, as SphereMask is serializable)
        public List<SphereData> sphereMasks = new List<SphereData>();

        // Basic settings (These remain as is)
        public int textureSizeIndex = DEFAULT_TEXTURE_SIZE_INDEX;
        public float maxDistance = DEFAULT_MAX_DISTANCE;
        public float gamma = DEFAULT_GAMMA;

        // 透過モード設定
        public bool useTransparentMode = false; // デフォルトは白背景モード

        // 一時メッシュ細分化回数（1固定）
        [Range(0, 3)] public int tempSubdivisionIterations = 1;

        // クリックUVアイランドマスク
        public System.Collections.Generic.List<UVIslandMaskData> uvIslandMasks = new System.Collections.Generic.List<UVIslandMaskData>();

        // 隣接グラデーション距離（メートル）
        public float uvIslandNeighborRadius = 0.015f; // デフォルト1.5cm

        // UV島詳細パラメータ
        [Range(0, 8)] public int uvIslandVertexSmoothIterations = 1; // 頂点ラプラシアン平滑回数 固定: 1

        // ボーンマスク (Transformパスで指定、0=黒く強くマスク、1=影響なし)
        public System.Collections.Generic.List<BoneMaskData> boneMasks = new System.Collections.Generic.List<BoneMaskData>();

        // ノーマルマップ設定 (マテリアルごとのノーマルマップ指定)
        public System.Collections.Generic.List<MaterialNormalMapData> materialNormalMaps = new System.Collections.Generic.List<MaterialNormalMapData>();

        /// <summary>
        /// 設定を初期値にリセットする（保存済み設定が無効な場合に使用）
        /// </summary>
        public void ResetToDefaults()
        {
            textureSizeIndex = DEFAULT_TEXTURE_SIZE_INDEX;
            maxDistance = DEFAULT_MAX_DISTANCE;
            gamma = DEFAULT_GAMMA;
            useTransparentMode = false; // デフォルトは白背景モード
            uvIslandNeighborRadius = 0.015f;
            uvIslandVertexSmoothIterations = 2;
            tempSubdivisionIterations = 1;
        }
    }

    #endregion

    #region Sphere Data

    /// <summary>
    /// 毛生成で使用するスフィアマスクのデータクラス
    /// </summary>
    [System.Serializable]
    public class SphereData
    {
        public string name = GameObjectConstants.DEFAULT_SPHERE_NAME;
        public Vector3 position;
        public float radius = 0.01f;
        public float gradientWidth = 0.1f;
        public float gradient
        {
            get => gradientWidth;
            set => gradientWidth = value;
        } // gradientWidthのエイリアス
        // マスクの濃さ（0.1〜1.0） 1.0で従来通り、0.1で最も薄い
        [Range(AppSettings.SPHERE_INTENSITY_MIN, AppSettings.SPHERE_INTENSITY_MAX)] public float intensity = AppSettings.SPHERE_INTENSITY_DEFAULT;
        public Color markerColor = new Color(0, 0, 0, 0); // UI/シーン表示用カラー 未設定時は透明
        public int addedSequence = -1; // 追加順序（統合表示用）
        
        // ミラー機能
        public bool useMirror = false; // X軸を反転した位置にもスフィアがある状態として処理

        public SphereData() { }

        public SphereData(Vector3 position, float radius)
        {
            this.position = position;
            this.radius = radius;
            this.name = $"{GameObjectConstants.SPHERE_POSITION_PREFIX}{position.ToString()}";
        }

        public SphereData(string name, Vector3 position, float radius)
        {
            this.name = name;
            this.position = position;
            this.radius = radius;
        }

        public SphereData(string name, Vector3 position, float radius, float gradientWidth, Color markerColor)
        {
            this.name = name;
            this.position = position;
            this.radius = radius;
            this.gradientWidth = gradientWidth;
            this.markerColor = markerColor;
        }

        public SphereData Clone()
        {
            return MemberwiseClone() as SphereData;
        }

        public float GetMaskValue(Vector3 vertexPosition)
        {
            float maskValue = 1f;
            
            // オリジナル位置でのマスク値計算
            float dist = Vector3.Distance(vertexPosition, position);
            if (dist <= radius)
            {
                float innerRadius = radius * (1f - gradientWidth);
                if (dist <= innerRadius) 
                {
                    maskValue = 0f;
                }
                else
                {
                    maskValue = (dist - innerRadius) / (radius - innerRadius);
                }
            }
            
            // ミラー機能が有効な場合、X軸反転位置でも計算
            if (useMirror)
            {
                Vector3 mirroredPosition = new Vector3(-position.x, position.y, position.z);
                float mirroredDist = Vector3.Distance(vertexPosition, mirroredPosition);
                if (mirroredDist <= radius)
                {
                    float innerRadius = radius * (1f - gradientWidth);
                    float mirroredMaskValue;
                    if (mirroredDist <= innerRadius)
                    {
                        mirroredMaskValue = 0f;
                    }
                    else
                    {
                        mirroredMaskValue = (mirroredDist - innerRadius) / (radius - innerRadius);
                    }
                    // オリジナルとミラーの小さい方（より強いマスク）を採用
                    maskValue = Mathf.Min(maskValue, mirroredMaskValue);
                }
            }
            
            return maskValue;
        }
    }

    #endregion

    #region UV Island Mask Data

    /// <summary>
    /// シーン上クリックで追加されるUVアイランドマスクの情報
    /// - どのレンダラー/サブメッシュか特定用
    /// - クリック位置のUV座標（ベイク側はマテリアル名で対象サブメッシュを特定）
    /// </summary>
    [Serializable]
    public class UVIslandMaskData
    {
        public string rendererPath;
        public int submeshIndex;
        public Vector2 seedUV; // クリック位置のUV
        public Vector2 uvPosition; // UV座標 (seedUVのエイリアス)
        public string targetMatName; // 対象マテリアル名
        public Vector3 seedWorldPos; // クリック時のワールド座標（簡易表示用）
        public string displayName;
        public Color markerColor = new Color(0, 0, 0, 0); // UI/シーン表示用カラー 未設定時は透明
        public float uvThreshold = 0.1f; // UV閾値
    }

    #endregion

    #region Bone Mask Data

    /// <summary>
    /// ボーン単位のマスク設定（0=無効 / 1=黒で強くマスク）
    /// Transformパスで一意に識別する
    /// </summary>
    [Serializable]
    public class BoneMaskData
    {
        public string bonePath;
        public float value = 0.0f;
    }

    #endregion

    #region Normal Map Data

    /// <summary>
    /// マテリアルごとのノーマルマップ設定
    /// UVマップに対応したノーマルマップを指定して法線方向を修正
    /// </summary>
    [Serializable]
    public class MaterialNormalMapData
    {
        public string materialName; // マテリアル名（一意識別用）
        public Texture2D normalMap; // ノーマルマップテクスチャ
        [Range(0f, 1f)] public float intensity = 1.0f; // ノーマルマップの影響強度
        // 追加設定: フォーマット差異と強度調整
        public bool isPackedAG = false; // DXT5nm等のAGパック (A=X, G=Y, Z再構築)
        [Range(-10f, 10f)] public float normalStrength = 1.0f; // XY強度スケール（法線の傾き量）

        public MaterialNormalMapData Clone()
        {
            return MemberwiseClone() as MaterialNormalMapData;
        }
    }

    #endregion

    #region Distance Baker Settings

    /// <summary>
    /// Settings for distance mask baking process
    /// Contains all parameters needed for the baking operation
    /// </summary>
    public class DistanceBakerSettings
    {
        public readonly List<Renderer> AvatarRenderers;
        public readonly List<Renderer> ClothRenderers;
        public readonly List<SphereData> SphereMasks;
        public readonly List<UVIslandMaskData> UVIslandMasks;
        public readonly List<BoneMaskData> BoneMasks;
        public readonly List<MaterialNormalMapData> MaterialNormalMaps;
        public readonly int TextureSizeIndex;
        public readonly float MaxDistance;
        public readonly float Gamma;
        public readonly int TempSubdivisionIterations;
        public readonly float UvIslandNeighborRadius;
        public readonly int UvIslandVertexSmoothIterations;
        public readonly bool UseTransparentMode;
        public readonly Action<Dictionary<string, Texture2D>> OnCompleted;
        public readonly Action OnCancelled;

        public DistanceBakerSettings(
            List<Renderer> avatarRenderers,
            List<Renderer> clothRenderers,
            List<SphereData> sphereMasks,
            List<UVIslandMaskData> uvIslandMasks,
            List<BoneMaskData> boneMasks,
            List<MaterialNormalMapData> materialNormalMaps,
            int textureSizeIndex,
            float maxDistance,
            float gamma,
            int tempSubdivisionIterations,
            float uvIslandNeighborRadius,
            int uvIslandVertexSmoothIterations,
            bool useTransparentMode,
            Action<Dictionary<string, Texture2D>> onCompleted,
            Action onCancelled)
        {
            AvatarRenderers = avatarRenderers;
            ClothRenderers = clothRenderers;
            SphereMasks = sphereMasks;
            UVIslandMasks = uvIslandMasks;
            BoneMasks = boneMasks ?? new List<BoneMaskData>();
            MaterialNormalMaps = materialNormalMaps ?? new List<MaterialNormalMapData>();
            TextureSizeIndex = textureSizeIndex;
            MaxDistance = maxDistance;
            Gamma = gamma;
            TempSubdivisionIterations = Mathf.Clamp(tempSubdivisionIterations, 0, 3);
            UvIslandNeighborRadius = uvIslandNeighborRadius;
            UvIslandVertexSmoothIterations = Mathf.Clamp(uvIslandVertexSmoothIterations, 0, 8);
            UseTransparentMode = useTransparentMode;
            OnCompleted = onCompleted;
            OnCancelled = onCancelled;
        }
    }

    #endregion
}
