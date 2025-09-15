namespace Mask.Generator.Constants
{
    /// <summary>
    /// ゲームオブジェクト、ボーン、ブレンドシェイプ関連の定数を管理
    /// </summary>
    public static class GameObjectConstants
    {
        #region オブジェクト名
        public const string TEMP_RAYCAST_COLLIDER_NAME = "FMG_TempRaycastCollider";
        public const string CLOTH_COLLIDER_OBJECT_NAME = "ClothCollider";
        public const string DEFAULT_SPHERE_NAME = "New Sphere";
        public const string SPHERE_NAME_PREFIX = "Sphere ";
        public const string SPHERE_POSITION_PREFIX = "Sphere_";
        public const string SUBMESH_NAME_PREFIX = "SubMesh_";
        public const string LENGTH_MASK_PREFIX = "LengthMask_";
        #endregion

        #region 情報メッセージ
        public const string INFO_MOUTH_BLEND_NOT_FOUND = "口系ブレンドシェイプが検出できなかったため、口元スフィアの自動追加をスキップしました。";
        #endregion

        #region フィルター関連
        public const string FILTER_WEAR = "wear";
        public const string FILTER_EARRING = "earring";
        public const string FILTER_BODY = "body";
        public const string FILTER_TAIL = "tail";
        public const string FILTER_EAR = "ear";
        #endregion

        #region ボーン関連
        public const string BONE_GROUP_HIPS = "hips";
        public const string BONE_GROUP_SPINE = "spine";
        public const string BONE_GROUP_CHEST = "chest";
        public const string BONE_GROUP_UPPERCHEST = "upperchest";
        public const string BONE_GROUP_BODY = "Body";
        public const string BONE_GROUP_NECK = "neck";
        public const string BONE_GROUP_HEAD = "head";
        public const string BONE_GROUP_JAW = "jaw";
        public const string BONE_GROUP_LEFTEYE = "lefteye";
        public const string BONE_GROUP_RIGHTEYE = "righteye";
        public const string BONE_GROUP_EYE = "eye";
        public const string BONE_GROUP_HEAD_GROUP = "Head";
        public const string BONE_GROUP_UPPERARM = "upperarm";
        public const string BONE_GROUP_LOWERARM = "lowerarm";
        public const string BONE_GROUP_SHOULDER = "shoulder";
        public const string BONE_GROUP_ARM = "Arm";
        public const string BONE_GROUP_HAND = "hand";
        public const string BONE_GROUP_INDEX = "index";
        public const string BONE_GROUP_MIDDLE = "middle";
        public const string BONE_GROUP_RING = "ring";
        public const string BONE_GROUP_LITTLE = "little";
        public const string BONE_GROUP_THUMB = "thumb";
        public const string BONE_GROUP_HAND_GROUP = "Hand";
        public const string BONE_GROUP_UPPERLEG = "upperleg";
        public const string BONE_GROUP_LOWERLEG = "lowerleg";
        public const string BONE_GROUP_LEG = "Leg";
        public const string BONE_GROUP_FOOT = "foot";
        public const string BONE_GROUP_TOES = "toes";
        public const string BONE_GROUP_FOOT_GROUP = "Foot";
        public const string BONE_GROUP_EAR_GROUP = "Ear";
        public const string BONE_GROUP_TAIL_GROUP = "Tail";
        #endregion

        #region 顔認識関連
        public const string SPHERE_NAME_LEFT_EYE = "Left Eye";
        public const string SPHERE_NAME_RIGHT_EYE = "Right Eye";
        public const string SPHERE_NAME_NOSE_TIP = "Nose Tip";
        public const string SPHERE_NAME_MOUTH_INSIDE = "Mouth Inside";
        #endregion

        #region ブレンドシェイプ関連
        public const string BLENDSHAPE_V_AA = "vrc.v_aa";
        public const string BLENDSHAPE_V_OH = "vrc.v_oh";
        public const string BLENDSHAPE_V_OU = "vrc.v_ou";
        public const string BLENDSHAPE_V_IH = "vrc.v_ih";
        public const string BLENDSHAPE_V_EE = "vrc.v_ee";
        #endregion

        #region グループ順序
        public static readonly string[] GROUP_ORDER = { "Body", "Head", "Arm", "Hand", "Leg", "Foot", "Ear", "Tail" };
        #endregion

        #region テクスチャプロパティ名
        public static readonly string[] MAIN_TEXTURE_PROPERTIES = { "_MainTex", "_BaseMap", "_AlbedoMap", "_DiffuseMap" };
        #endregion
    }
}