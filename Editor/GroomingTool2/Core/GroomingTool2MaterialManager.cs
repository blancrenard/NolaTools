using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using GroomingTool2.Utils;

namespace GroomingTool2.Core
{
    /// <summary>
    /// マテリアル管理を担当するクラス
    /// </summary>
    internal sealed class GroomingTool2MaterialManager : IDisposable
    {
        private readonly List<MaterialEntry> materialEntries = new();
        private readonly GroomingTool2TextureProcessor textureProcessor;

        public IReadOnlyList<MaterialEntry> MaterialEntries => materialEntries;
        public int SelectedMaterialIndex { get; private set; } = -1;

        public MaterialEntry? SelectedMaterial => SelectedMaterialIndex >= 0 && SelectedMaterialIndex < materialEntries.Count
            ? materialEntries[SelectedMaterialIndex]
            : null;

        public event Action<int> OnMaterialSelected;

        public GroomingTool2MaterialManager(GroomingTool2TextureProcessor textureProcessor)
        {
            this.textureProcessor = textureProcessor ?? throw new ArgumentNullException(nameof(textureProcessor));
        }

        public void Dispose()
        {
            ClearMaterialEntries();
            OnMaterialSelected = null;
        }

        /// <summary>
        /// マテリアルリストを再構築する
        /// </summary>
        public void RebuildMaterialList(GameObject avatar)
        {
            if (avatar == null)
            {
                ClearMaterialEntries();
                return;
            }

            // 既存のリサイズ済みテクスチャを破棄
            ClearMaterialEntries();

            // すべてのSkinnedMeshRendererを取得（フィルタリングなし）
            var renderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToList();

            // テクスチャ単位でグルーピング
            var textureMap = GroupMaterialsByTexture(renderers);

            // テクスチャ名順でソートしてリストに追加
            materialEntries.AddRange(textureMap.Values.OrderBy(m => m.texture != null ? m.texture.name : m.displayName));

            if (materialEntries.Count > 0)
                SelectedMaterialIndex = 0;

            // リサイズ済みテクスチャを作成
            ResizeTextures();
        }

        private Dictionary<Texture2D, MaterialEntry> GroupMaterialsByTexture(List<SkinnedMeshRenderer> renderers)
        {
            var textureMap = new Dictionary<Texture2D, MaterialEntry>();

            foreach (var r in renderers)
            {
                if (r == null || r.sharedMaterials == null) continue;

                bool baked;
                var mesh = EditorMeshUtils.GetMeshForRenderer(r, out baked);
                Vector2[] uv = mesh != null ? mesh.uv : null;

                for (int i = 0; i < r.sharedMaterials.Length; i++)
                {
                    var mat = r.sharedMaterials[i];
                    if (mat == null) continue;
                    
                    // サブメッシュインデックスが範囲内かチェック
                    int[] subTriangles = null;
                    if (mesh != null && i < mesh.subMeshCount)
                    {
                        subTriangles = mesh.GetTriangles(i);
                    }

                    // マテリアルのメインテクスチャを基準にグルーピング
                    var tex = Utils.MaterialUtils.GetMainTextureFromMaterial(mat);
                    if (tex == null)
                        continue; // 画像列挙へ変更のため、テクスチャが無いものはスキップ

                    if (textureMap.TryGetValue(tex, out var entry))
                    {
                        entry.usages.Add((r, i));
                        if (uv != null && subTriangles != null)
                        {
                            entry.uvSets.Add(uv);
                            entry.triangleSets.Add(subTriangles);
                        }
                        textureMap[tex] = entry;
                    }
                    else
                    {
                        var display = tex.name;

                        textureMap[tex] = new MaterialEntry
                        {
                            material = mat, // 代表として最初のマテリアルを保持（現在は参照されていない）
                            texture = tex,
                            resizedTexture = null,
                            uv = uv,
                            triangles = subTriangles,
                            displayName = display,
                            usages = new List<(Renderer renderer, int submeshIndex)> { (r, i) },
                            uvSets = uv != null && subTriangles != null ? new List<Vector2[]> { uv } : new List<Vector2[]>(),
                            triangleSets = uv != null && subTriangles != null ? new List<int[]> { subTriangles } : new List<int[]>()
                        };
                    }
                }

                if (mesh != null && baked)
                {
                    Object.DestroyImmediate(mesh);
                }
            }

            return textureMap;
        }

        private void ResizeTextures()
        {
            for (int i = 0; i < materialEntries.Count; i++)
            {
                var entry = materialEntries[i];
                if (entry.texture != null)
                {
                    entry.resizedTexture = textureProcessor.ResizeTexture(entry.texture, Common.TexSize, Common.TexSize);
                    materialEntries[i] = entry;
                }
            }
        }

        /// <summary>
        /// 指定されたマテリアルインデックスを選択する
        /// </summary>
        public void SelectMaterial(int index)
        {
            if (index >= 0 && index < materialEntries.Count && index != SelectedMaterialIndex)
            {
                SelectedMaterialIndex = index;
                OnMaterialSelected?.Invoke(index);
            }
        }

        /// <summary>
        /// マテリアルエントリをクリアする
        /// </summary>
        private void ClearMaterialEntries()
        {
            foreach (var entry in materialEntries)
            {
                if (entry.resizedTexture != null)
                {
                    Object.DestroyImmediate(entry.resizedTexture);
                }
            }

            materialEntries.Clear();
            SelectedMaterialIndex = -1;
        }
    }

    /// <summary>
    /// マテリアルエントリの構造体
    /// </summary>
    internal struct MaterialEntry
    {
        public Material material;
        public Texture2D texture;
        public Texture2D resizedTexture; // リサイズ済みテクスチャ（Common.TexSize x Common.TexSize）
        public Vector2[] uv;
        public int[] triangles;
        public string displayName;
        public List<(Renderer renderer, int submeshIndex)> usages;
        public List<Vector2[]> uvSets;
        public List<int[]> triangleSets;
    }
}
