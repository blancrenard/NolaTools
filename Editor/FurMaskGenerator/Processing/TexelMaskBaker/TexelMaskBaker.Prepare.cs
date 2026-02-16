#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NolaTools.FurMaskGenerator.Utils;
using NolaTools.FurMaskGenerator.Data;
using NolaTools.FurMaskGenerator.Constants;
using UnityEngine.Rendering;

namespace NolaTools.FurMaskGenerator
{
    public partial class TexelMaskBaker
    {
        #region 準備

        private void Prepare()
        {
            InitializeSettings();
            InitializeDataStructures();
            BuildBoneMaskMap();
            BuildNormalMapCache();
            CollectMeshData();
            BuildClothCollider();

            // 頂点→マテリアル名マッピング構築
            BuildVertexToMaterialMapping();

            // レイキャスト方向を事前計算
            PrepareRayDirections();

            // テクセルバッファ初期化
            InitializeTexelBuffers();

            // バッチサイズと進捗更新間隔を設定
            InitializeProgressParams();
        }

        private void InitializeSettings()
        {
            texSize = AppSettings.TEXTURE_SIZES[settings.TextureSizeIndex];
            maxM = settings.MaxDistance;
            maxMSquared = maxM * maxM;
        }

        private void InitializeDataStructures()
        {
            verts = new List<Vector3>();
            norms = new List<Vector3>();
            uvs = new List<Vector2>();
            subDatas = new List<(int[] tri, string mat)>();
            subRendererPaths = new List<string>();
            subMeshIndices = new List<int>();
            boneMaskValues = new List<float>();
            tangents = new List<Vector4>();

            boneMaskMap = new Dictionary<Transform, float>();
            normalMapCache = new Dictionary<string, MaterialNormalMapData>();
            vertexToMaterialName = new Dictionary<int, string>();
            cachedIsPackedAG = new Dictionary<string, bool>();
        }

        private void BuildBoneMaskMap()
        {
            if (settings.BoneMasks == null) return;
            
            foreach (var bm in settings.BoneMasks)
            {
                if (bm == null || string.IsNullOrEmpty(bm.bonePath)) continue;
                var go = EditorPathUtils.FindGameObjectByPath(bm.bonePath);
                if (go != null)
                {
                    boneMaskMap[go.transform] = Mathf.Clamp01(bm.value);
                }
            }
        }

        private void BuildNormalMapCache()
        {
            if (settings.MaterialNormalMaps == null) return;

            foreach (var nm in settings.MaterialNormalMaps)
            {
                if (nm == null || string.IsNullOrEmpty(nm.materialName) || nm.normalMap == null) continue;
                
                if (nm.normalMap.isReadable)
                {
                    normalMapCache[nm.materialName] = nm;
                }
                else
                {
                    // 読み取り不可能な場合は一時的に複製を作成
                    Texture2D readableCopy = DuplicateTextureReadable(nm.normalMap);
                    if (readableCopy != null)
                    {
                        var tempNM = new MaterialNormalMapData
                        {
                            materialName = nm.materialName,
                            normalMap = readableCopy,
                            normalStrength = nm.normalStrength,
                            isPackedAG = nm.isPackedAG
                        };
                        
                        normalMapCache[nm.materialName] = tempNM;
                        temporaryTextures.Add(readableCopy);
                    }
                }
            }
        }

        private void CollectMeshData()
        {
            foreach (var r in settings.AvatarRenderers)
            {
                if (r == null) continue;

                // ターゲットマテリアルフィルタリング
                if (settings.TargetMaterial != null)
                {
                    bool hasTarget = false;
                    if (r.sharedMaterials != null)
                    {
                        foreach (var m in r.sharedMaterials)
                        {
                            if (m == settings.TargetMaterial)
                            {
                                hasTarget = true;
                                break;
                            }
                        }
                    }
                    if (!hasTarget) continue;
                }

                var mesh = EditorMeshUtils.GetMeshForRenderer(r, out bool isTemp);
                if (mesh == null) continue;

                // BakeMeshではタンジェントがスキニング変形と不整合になる場合があるため、
                // 現在のジオメトリに基づいてタンジェントを再計算する
                if (isTemp && normalMapCache.Count > 0)
                {
                    mesh.RecalculateTangents();
                }

                AddMeshData(r, mesh);
                ProcessSubMeshes(r, mesh);

                if (isTemp) createdMeshes.Add(mesh);
            }
        }

        private void ProcessSubMeshes(Renderer r, Mesh mesh)
        {
            int subMeshCount = mesh.subMeshCount;
            string rendererPath = EditorPathUtils.GetGameObjectPath(r);
            int baseOffset = verts.Count - mesh.vertexCount;

            for (int smi = 0; smi < subMeshCount; smi++)
            {
                // Filter by target material
                if (settings.TargetMaterial != null)
                {
                    var currentMaterial = (r.sharedMaterials != null && smi < r.sharedMaterials.Length) ? r.sharedMaterials[smi] : null;
                    if (currentMaterial != settings.TargetMaterial) continue;
                }

                int[] triLocal = mesh.GetTriangles(smi);
                if (triLocal == null || triLocal.Length == 0) continue;

                int[] triGlobal = new int[triLocal.Length];
                for (int i = 0; i < triLocal.Length; i++)
                {
                    triGlobal[i] = triLocal[i] + baseOffset;
                }

                string matName = (r.sharedMaterials != null && smi < r.sharedMaterials.Length && r.sharedMaterials[smi] != null)
                    ? r.sharedMaterials[smi].name
                    : $"{GameObjectConstants.SUBMESH_NAME_PREFIX}{smi}";

                subDatas.Add((triGlobal, matName));
                subRendererPaths.Add(rendererPath);
                subMeshIndices.Add(smi);
            }
        }

        private void BuildClothCollider()
        {
            bool hasCloth = settings.ClothRenderers != null && settings.ClothRenderers.Count > 0;
            bool hasUVMasks = settings.UVIslandMasks != null && settings.UVIslandMasks.Count > 0;
            
            if (!hasCloth && !hasUVMasks) return;

            clothColliderObject = new GameObject(GameObjectConstants.CLOTH_COLLIDER_OBJECT_NAME);
            clothCollider = clothColliderObject.AddComponent<MeshCollider>();
            clothColliderObject.hideFlags = HideFlags.HideAndDontSave;

            var combine = new List<CombineInstance>();

            if (hasCloth)
            {
                foreach (var r in settings.ClothRenderers)
                {
                    if (r == null) continue;
                    var mesh = EditorMeshUtils.GetMeshForRenderer(r, out bool isTemp);
                    if (mesh == null) continue;

                    var ci = new CombineInstance { mesh = mesh, transform = r.transform.localToWorldMatrix };
                    combine.Add(ci);
                    if (isTemp) createdMeshes.Add(mesh);
                }
            }

            if (hasUVMasks)
            {
                AddUVIslandTrianglesAsCloth(ref combine);
            }

            if (combine.Count > 0)
            {
                Mesh combinedMesh = new Mesh();
                combinedMesh.indexFormat = IndexFormat.UInt32;
                combinedMesh.CombineMeshes(combine.ToArray());
                clothCollider.sharedMesh = combinedMesh;
                createdMeshes.Add(combinedMesh);
            }
            else
            {
                EditorObjectUtils.SafeDestroy(clothCollider);
                clothCollider = null;
                EditorObjectUtils.SafeDestroy(clothColliderObject);
                clothColliderObject = null;
            }
        }

        private void InitializeProgressParams()
        {
            batchSize = Mathf.Max(100, texSize * texSize / 100);
            progressUpdateInterval = Mathf.Max(1, totalTexelsToProcess / 100);
            lastProgressBarUpdate = 0f;

            // ベイク開始位置
            currentSubIndex = 0;
            currentTriIndex = 0;
            processedTexels = 0;
        }

        private void AddMeshData(Renderer r, Mesh baseM)
        {
            Vector3[] v = baseM.vertices;
            Vector3[] n = baseM.normals;
            Vector2[] u = baseM.uv;
            Vector4[] t = baseM.tangents;
            bool isSMR = r is SkinnedMeshRenderer;
            BoneWeight[] weights = null;
            Transform[] smrBones = null;
            Dictionary<Transform, float> resolvedBoneMask = null;

            if (isSMR)
            {
                var smr = (SkinnedMeshRenderer)r;
                weights = smr.sharedMesh != null ? smr.sharedMesh.boneWeights : null;
                smrBones = smr.bones;
                if (smrBones != null)
                {
                    resolvedBoneMask = new Dictionary<Transform, float>(smrBones.Length);
                    for (int bi = 0; bi < smrBones.Length; bi++)
                    {
                        var bt = smrBones[bi];
                        float mv = ResolveBoneMaskWithInheritance(bt);
                        resolvedBoneMask[bt] = mv;
                    }
                }
            }

            for (int i = 0; i < v.Length; i++)
            {
                verts.Add(r.transform.TransformPoint(v[i]));
                norms.Add(r.transform.TransformDirection(n[i]));
                uvs.Add(u[i]);

                if (t != null && i < t.Length)
                {
                    Vector3 tangent = r.transform.TransformDirection(t[i]);
                    float w = t[i].w;
                    tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, w));
                }
                else
                {
                    tangents.Add(new Vector4(1, 0, 0, 1));
                }

                float bm = 0f;
                if (isSMR && weights != null && smrBones != null && i < weights.Length)
                {
                    var bw = weights[i];
                    bm = CalculateBoneMaskValue(bw, smrBones, resolvedBoneMask);
                }
                boneMaskValues.Add(bm);
            }
        }

        private void BuildVertexToMaterialMapping()
        {
            BakerUtils.BuildVertexToMaterialMapping(subDatas, verts.Count, vertexToMaterialName);
        }

        private void PrepareRayDirections()
        {
            rayDirections = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                rayDirections[i] = norms[i].normalized;
            }
        }

        private void InitializeTexelBuffers()
        {
            materialBuffers = new Dictionary<string, Color[]>();
            materialRasterizedPixels = new Dictionary<string, HashSet<int>>();
            totalTexelsToProcess = 0;

            // 三角形数の合計を予測用に計算
            foreach (var (tri, mat) in subDatas)
            {
                totalTexelsToProcess += tri.Length / 3; // 三角形ごとにテクセル処理
            }

            // 各マテリアルのバッファを初期化
            var materialSet = new HashSet<string>();
            foreach (var (_, mat) in subDatas)
            {
                if (!materialSet.Contains(mat))
                {
                    materialSet.Add(mat);
                    Color[] buffer = new Color[texSize * texSize];
                    Color initialColor = settings.UseTransparentMode ? new Color(0f, 0f, 0f, 0f) : Color.white;
                    for (int i = 0; i < buffer.Length; i++) buffer[i] = initialColor;
                    materialBuffers[mat] = buffer;
                    materialRasterizedPixels[mat] = new HashSet<int>();
                }
            }
        }

        /// <summary>
        /// UVアイランドマスクの三角形を服コライダーとして追加
        /// </summary>
        private void AddUVIslandTrianglesAsCloth(ref List<CombineInstance> combine)
        {
            if (settings.UVIslandMasks == null || settings.UVIslandMasks.Count == 0) return;

            foreach (var r in settings.AvatarRenderers)
            {
                if (r == null) continue;
                var mesh = EditorMeshUtils.GetMeshForRenderer(r, out bool isTemp);
                if (mesh == null) continue;
                string rendererPath = EditorPathUtils.GetGameObjectPath(r);

                foreach (var mask in settings.UVIslandMasks)
                {
                    if (mask == null || mask.rendererPath != rendererPath) continue;
                    if (mask.submeshIndex >= mesh.subMeshCount) continue;

                    int[] tri = mesh.GetTriangles(mask.submeshIndex);
                    if (tri == null || tri.Length == 0) continue;

                    Vector2[] meshUVs = mesh.uv;
                    Vector3[] meshVerts = mesh.vertices;

                    // UV アイランドを特定して、その三角形をコライダーに追加
                    var uvsArr = meshUVs;
                    int seedTri = EditorUvUtils.FindSeedTriangleByUV(tri, uvsArr, mask.seedUV);
                    if (seedTri < 0) continue;

                    var adjacency = EditorUvUtils.BuildTriangleAdjacencyListList(tri);
                    var islandTris = EditorUvUtils.EnumerateUVIslandTriangles(tri, adjacency, seedTri);

                    List<Vector3> islandVerts = new List<Vector3>();
                    List<int> islandTriangles = new List<int>();
                    Dictionary<int, int> vertexMap = new Dictionary<int, int>();

                    foreach (int t in islandTris)
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            int vi = tri[t * 3 + j];
                            if (!vertexMap.ContainsKey(vi))
                            {
                                vertexMap[vi] = islandVerts.Count;
                                islandVerts.Add(r.transform.TransformPoint(meshVerts[vi]));
                            }
                            islandTriangles.Add(vertexMap[vi]);
                        }
                    }

                    if (islandVerts.Count > 0 && islandTriangles.Count > 0)
                    {
                        Mesh islandMesh = new Mesh();
                        islandMesh.SetVertices(islandVerts);
                        islandMesh.SetTriangles(islandTriangles, 0);
                        islandMesh.RecalculateNormals();
                        createdMeshes.Add(islandMesh);

                        var ci = new CombineInstance { mesh = islandMesh, transform = Matrix4x4.identity };
                        combine.Add(ci);
                    }
                }

                if (isTemp)
                {
                    createdMeshes.Add(mesh);
                }
            }
        }

        #endregion


        /// <summary>
        /// 読み取り不可能なテクスチャをRenderTexture経由で複製して読み取り可能にする
        /// </summary>
        private Texture2D DuplicateTextureReadable(Texture2D source)
        {
            if (source == null) return null;

            RenderTexture renderTex = RenderTexture.GetTemporary(
                source.width, 
                source.height, 
                0, 
                RenderTextureFormat.Default, 
                RenderTextureReadWrite.Linear);

            Graphics.Blit(source, renderTex);
            
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;
            
            Texture2D readableText = new Texture2D(source.width, source.height);
            readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableText.Apply();
            
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            
            return readableText;
        }


    }
}
#endif
