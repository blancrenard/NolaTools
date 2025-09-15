#if UNITY_EDITOR
namespace Mask.Generator.Constants
{
    /// <summary>
    /// Undo/Redo操作用のメッセージを管理
    /// </summary>
    public static class UndoMessages
    {
        public const string FUR_MASK_GENERATOR_CHANGE = "Fur Mask Generator Change";
        public const string MOVE_SPHERE_MASK = "Move Sphere Mask";
        public const string ADD_SPHERE_MASK = "Add Sphere Mask";
        public const string ADD_UV_ISLAND_MASK = "Add UV Island Mask";
        public const string AUTO_DETECT_TEXTURE_SIZE = "Auto-detect texture size";
    }
}
#endif