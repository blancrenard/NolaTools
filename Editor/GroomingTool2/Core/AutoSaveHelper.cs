using GroomingTool2.Managers;

namespace GroomingTool2.Core
{
    /// <summary>
    /// 毛データの自動保存/読み込みを管理するヘルパークラス
    /// </summary>
    internal sealed class AutoSaveHelper
    {
        private readonly FileManager fileManager;
        
        /// <summary>現在のアバター名</summary>
        public string CurrentAvatarName { get; private set; }
        
        /// <summary>現在のテクスチャ名</summary>  
        public string CurrentTextureName { get; private set; }

        public AutoSaveHelper(FileManager fileManager)
        {
            this.fileManager = fileManager;
        }

        /// <summary>
        /// 現在のキー情報（アバター名・テクスチャ名）を更新
        /// </summary>
        /// <param name="avatarName">アバター名</param>
        /// <param name="textureName">テクスチャ名</param>
        public void UpdateKeyInfo(string avatarName, string textureName)
        {
            CurrentAvatarName = avatarName;
            CurrentTextureName = textureName;
        }

        /// <summary>
        /// 現在の毛データを自動保存
        /// </summary>
        /// <param name="furDataManager">保存対象のFurDataManager</param>
        /// <returns>保存成功時はtrue</returns>
        public bool AutoSave(FurDataManager furDataManager)
        {
            if (string.IsNullOrEmpty(CurrentAvatarName) || string.IsNullOrEmpty(CurrentTextureName))
                return false;
            
            if (fileManager == null || furDataManager == null)
                return false;

            fileManager.AutoSaveFurData(CurrentAvatarName, CurrentTextureName, furDataManager);
            return true;
        }

        /// <summary>
        /// 毛データの自動読み込みを試みる
        /// </summary>
        /// <param name="furDataManager">読み込み先のFurDataManager</param>
        /// <returns>読み込み成功時はtrue</returns>
        public bool TryAutoLoad(FurDataManager furDataManager)
        {
            if (string.IsNullOrEmpty(CurrentAvatarName) || string.IsNullOrEmpty(CurrentTextureName))
                return false;
            
            if (fileManager == null || furDataManager == null)
                return false;

            return fileManager.AutoLoadFurData(CurrentAvatarName, CurrentTextureName, furDataManager);
        }

        /// <summary>
        /// キー情報をクリア
        /// </summary>
        public void Clear()
        {
            CurrentAvatarName = null;
            CurrentTextureName = null;
        }
    }
}
