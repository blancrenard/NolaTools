using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GroomingTool2.Core;
using GroomingTool2.State;
using UnityEditor;
using UnityEngine;

namespace GroomingTool2.Managers
{
    internal sealed class FileManager
    {
        private readonly Dictionary<int, Texture2D> backgroundCache = new();
        private string backgroundPath = string.Empty;
        
        // 自動保存用のディレクトリパス
        private static string AutoSaveDirectory => Path.Combine(Application.temporaryCachePath, "GroomingTool2AutoSave");

        public Texture2D GetBackground(float scale)
        {
            if (backgroundCache.TryGetValue(Mathf.RoundToInt(scale * 1000f), out var texture))
                return texture;
            return null;
        }

        public void LoadBackground(string path, IEnumerable<float> scales)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException("背景画像が見つかりません", path);

            DisposeBackgrounds();

            var sourceBytes = File.ReadAllBytes(path);
            var original = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            original.LoadImage(sourceBytes);

            foreach (var scale in scales)
            {
                var key = Mathf.RoundToInt(scale * 1000f);
                var size = Mathf.RoundToInt(Common.TexSize * scale);
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
                Graphics.ConvertTexture(original, texture);
                backgroundCache[key] = texture;
            }

            UnityEngine.Object.DestroyImmediate(original);
            backgroundPath = path;
        }

        public void SaveFurData(string path, FurDataManager furDataManager, object _ = null)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("保存パスが無効です", nameof(path));

            var serialized = new FurDataSerialized();
            furDataManager.SaveToSerialized(serialized);

            using var writer = new StreamWriter(path);
            writer.WriteLine(backgroundPath);

            for (var y = 0; y < Common.TexSize; y++)
            {
                for (var x = 0; x < Common.TexSize; x++)
                {
                    var index = y * Common.TexSize + x;
                    writer.WriteLine($"{serialized.Dir[index]}:{serialized.Inclined[index]}");
                }
            }

            // 互換性のため、ミラー枠セクションは空で書き出す
            writer.WriteLine("---MIRROR_FRAMES---");
            writer.WriteLine("0");
        }

        public void LoadFurData(string path, FurDataManager furDataManager, object _ = null)
        {
            using var reader = new StreamReader(path);

            backgroundPath = reader.ReadLine();

            var serialized = new FurDataSerialized
            {
                Dir = new int[Common.TexSize * Common.TexSize],
                Inclined = new float[Common.TexSize * Common.TexSize]
            };

            var index = 0;
            for (var y = 0; y < Common.TexSize; y++)
            {
                for (var x = 0; x < Common.TexSize; x++)
                {
                    var line = reader.ReadLine();
                    if (line == null)
                        throw new InvalidDataException("毛データの読み込みに失敗しました");

                    var parts = line.Split(':');
                    serialized.Dir[index] = int.Parse(parts[0]);
                    serialized.Inclined[index] = float.Parse(parts[1]);
                    index++;
                }
            }

            furDataManager.RestoreFromSerialized(serialized);

            // 旧バージョンで作成されたファイルの互換性のため、ミラー枠セクションを読み飛ばす
            if (reader.ReadLine() == "---MIRROR_FRAMES---")
            {
                var frameCount = int.Parse(reader.ReadLine() ?? "0");
                for (var i = 0; i < frameCount; i++)
                {
                    reader.ReadLine(); // ミラー枠情報を読み飛ばす
                }
            }
        }

        public string OpenBackgroundDialog()
        {
            return EditorUtility.OpenFilePanel("背景画像を読み込む", string.Empty, "png,jpg,jpeg");
        }

        public string SaveNormalMapDialog()
        {
            return EditorUtility.SaveFilePanel("ノーマルマップを書き出す", string.Empty, "normal.png", "png");
        }

        public string SaveFurDialog()
        {
            return EditorUtility.SaveFilePanel("毛データを書き出す", string.Empty, "furdata.txt", "txt");
        }

        public string LoadFurDialog()
        {
            return EditorUtility.OpenFilePanel("毛データを読み込む", string.Empty, "txt");
        }

        public string LoadNormalMapDialog()
        {
            return EditorUtility.OpenFilePanel("ノーマルマップを読み込む", string.Empty, "png,jpg,jpeg");
        }

        public Texture2D LoadTextureReadable(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            tex.LoadImage(bytes);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        public void DisposeBackgrounds()
        {
            foreach (var texture in backgroundCache.Values)
            {
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }

            backgroundCache.Clear();
        }

        /// <summary>
        /// アバター名とテクスチャ名から一意のキーを生成する
        /// </summary>
        private static string GenerateAutoSaveKey(string avatarName, string textureName)
        {
            var combined = $"{avatarName}_{textureName}";
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(combined));
            var sb = new StringBuilder();
            foreach (var b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 自動保存ファイルのパスを取得する
        /// </summary>
        private static string GetAutoSavePath(string avatarName, string textureName)
        {
            var key = GenerateAutoSaveKey(avatarName, textureName);
            return Path.Combine(AutoSaveDirectory, $"{key}.txt");
        }

        /// <summary>
        /// 毛データを自動保存する
        /// </summary>
        /// <param name="avatarName">アバター名</param>
        /// <param name="textureName">テクスチャ名</param>
        /// <param name="furDataManager">毛データマネージャー</param>
        /// <returns>保存成功した場合はtrue</returns>
        public bool AutoSaveFurData(string avatarName, string textureName, FurDataManager furDataManager)
        {
            if (string.IsNullOrEmpty(avatarName) || string.IsNullOrEmpty(textureName) || furDataManager == null)
                return false;

            try
            {
                // ディレクトリが存在しない場合は作成
                if (!Directory.Exists(AutoSaveDirectory))
                {
                    Directory.CreateDirectory(AutoSaveDirectory);
                }

                var path = GetAutoSavePath(avatarName, textureName);
                SaveFurData(path, furDataManager, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GroomingTool2] 自動保存に失敗しました: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 毛データを自動読み込みする
        /// </summary>
        /// <param name="avatarName">アバター名</param>
        /// <param name="textureName">テクスチャ名</param>
        /// <param name="furDataManager">毛データマネージャー</param>
        /// <returns>読み込み成功した場合はtrue</returns>
        public bool AutoLoadFurData(string avatarName, string textureName, FurDataManager furDataManager)
        {
            if (string.IsNullOrEmpty(avatarName) || string.IsNullOrEmpty(textureName) || furDataManager == null)
                return false;

            try
            {
                var path = GetAutoSavePath(avatarName, textureName);
                if (!File.Exists(path))
                    return false;

                LoadFurData(path, furDataManager, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GroomingTool2] 自動読み込みに失敗しました: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 自動保存データが存在するかどうかを確認する
        /// </summary>
        /// <param name="avatarName">アバター名</param>
        /// <param name="textureName">テクスチャ名</param>
        /// <returns>存在する場合はtrue</returns>
        public bool HasAutoSaveData(string avatarName, string textureName)
        {
            if (string.IsNullOrEmpty(avatarName) || string.IsNullOrEmpty(textureName))
                return false;

            var path = GetAutoSavePath(avatarName, textureName);
            return File.Exists(path);
        }
    }
}



