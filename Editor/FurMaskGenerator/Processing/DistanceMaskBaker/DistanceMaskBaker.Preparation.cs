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
    public partial class DistanceMaskBaker
    {
        #region 準備

        private void Prepare()
        {
            texSize = AppSettings.TEXTURE_SIZES[settings.TextureSizeIndex];
            maxM = settings.MaxDistance;

            PreAllocateMemory();

            boneMaskMap = new Dictionary<Transform, float>();
            normalMapCache = new Dictionary<string, MaterialNormalMapData>();
            vertexToMaterialName = new Dictionary<int, string>();
            cachedIsPackedAG = new Dictionary<string, bool>();
            if (settings.BoneMasks != null)
            {
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

            if (settings.MaterialNormalMaps != null)
            {
                foreach (var nm in settings.MaterialNormalMaps)
                {
                    if (nm == null || string.IsNullOrEmpty(nm.materialName) || nm.normalMap == null) continue;
                    normalMapCache[nm.materialName] = nm;
                }
            }

            foreach (var r in settings.AvatarRenderers)
            {
                if (r == null) continue;

                var mesh = EditorMeshUtils.GetMeshForRenderer(r, out bool isTemp);
                if (mesh == null) continue;

                ProcessAvatarMesh(r, mesh);

                if (isTemp)
                {
                    createdMeshes.Add(mesh);
                }
            }

            bool hasCloth = settings.ClothRenderers != null && settings.ClothRenderers.Count > 0;
            bool hasUVMasks = settings.UVIslandMasks != null && settings.UVIslandMasks.Count > 0;
            if (hasCloth || hasUVMasks)
            {
                clothColliderObject = new GameObject(GameObjectConstants.CLOTH_COLLIDER_OBJECT_NAME);
                clothCollider = clothColliderObject.AddComponent<MeshCollider>();
                clothColliderObject.hideFlags = HideFlags.HideAndDontSave;

                var combine = new System.Collections.Generic.List<CombineInstance>();

                if (hasCloth)
                {
                    for (int i = 0; i < settings.ClothRenderers.Count; i++)
                    {
                        var r = settings.ClothRenderers[i];
                        if (r == null) continue;

                        var mesh = EditorMeshUtils.GetMeshForRenderer(r, out bool isTemp);
                        if (mesh == null) continue;

                        var ci = new CombineInstance { mesh = mesh, transform = r.transform.localToWorldMatrix };
                        combine.Add(ci);

                        if (isTemp)
                        {
                            createdMeshes.Add(mesh);
                        }
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

            vDist = new float[verts.Count];
            batchSize = CalculateOptimalBatchSize();
            curr = 0;
            smoothBuffer1 = new float[verts.Count];
            smoothBuffer2 = new float[verts.Count];
            islandAnchorFlags = null;

            CalculateProgressUpdateInterval();
            lastProgressBarUpdate = 0f;

            BuildVertexToMaterialMapping();

            PrepareRaycastOptimization();
        }

        private void BuildVertexToMaterialMapping()
        {
            BakerUtils.BuildVertexToMaterialMapping(subDatas, verts.Count, vertexToMaterialName);
        }

        #endregion
    }
}
#endif


