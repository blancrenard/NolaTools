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
        #region メッシュ処理

        private void AddUVIslandTrianglesAsCloth(ref System.Collections.Generic.List<CombineInstance> combine)
        {
            if (settings.UVIslandMasks == null || settings.UVIslandMasks.Count == 0) return;

            var pathToSubIndices = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>();
            for (int si = 0; si < subDatas.Count; si++)
            {
                string path = (subRendererPaths.Count > si) ? subRendererPaths[si] : null;
                if (string.IsNullOrEmpty(path)) continue;
                if (!pathToSubIndices.TryGetValue(path, out var list)) { list = new System.Collections.Generic.List<int>(); pathToSubIndices[path] = list; }
                list.Add(si);
            }

            foreach (var group in pathToSubIndices)
            {
                string path = group.Key;
                var masks = settings.UVIslandMasks.FindAll(m => m != null && m.rendererPath == path);
                if (masks == null || masks.Count == 0) continue;

                foreach (var m in masks)
                {
                    int globalSubIdx = -1;
                    foreach (int si in group.Value)
                    {
                        if (si < subMeshIndices.Count && subMeshIndices[si] == m.submeshIndex)
                        {
                            globalSubIdx = si;
                            break;
                        }
                    }
                    if (globalSubIdx < 0) continue;
                    var (tri, _) = subDatas[globalSubIdx];
                    int seed = FindSeedTriangleByUV(tri, m.seedUV);
                    if (seed < 0) continue;

                    var adj = NolaTools.FurMaskGenerator.Utils.EditorUvUtils.BuildTriangleAdjacencyListList(tri);
                    var island = new System.Collections.Generic.HashSet<int>();
                    var stack = new System.Collections.Generic.Stack<int>();
                    var visited = new System.Collections.Generic.HashSet<int>();
                    stack.Push(seed); visited.Add(seed);
                    while (stack.Count > 0)
                    {
                        int t = stack.Pop();
                        island.Add(t);
                        foreach (var nb in adj[t]) if (!visited.Contains(nb)) { visited.Add(nb); stack.Push(nb); }
                    }

                    var subset = new Mesh();
                    subset.indexFormat = IndexFormat.UInt32;
                    var vList = new System.Collections.Generic.List<Vector3>();
                    var triList = new System.Collections.Generic.List<int>();
                    var remap = new System.Collections.Generic.Dictionary<int, int>();
                    foreach (int t in island)
                    {
                        int i0 = tri[t * 3 + 0];
                        int i1 = tri[t * 3 + 1];
                        int i2 = tri[t * 3 + 2];
                        int r0 = RemapIndex(i0, vList, remap);
                        int r1 = RemapIndex(i1, vList, remap);
                        int r2 = RemapIndex(i2, vList, remap);
                        triList.Add(r0); triList.Add(r1); triList.Add(r2);
                    }
                    subset.SetVertices(vList);
                    subset.SetTriangles(triList, 0);
                    subset.RecalculateNormals();

                    var ci = new CombineInstance
                    {
                        mesh = subset,
                        transform = Matrix4x4.identity
                    };
                    combine.Add(ci);
                    createdMeshes.Add(subset);
                }
            }

            int RemapIndex(int src, System.Collections.Generic.List<Vector3> vList, System.Collections.Generic.Dictionary<int, int> remap)
            {
                if (remap.TryGetValue(src, out int dst)) return dst;
                dst = vList.Count;
                vList.Add(verts[src]);
                remap[src] = dst;
                return dst;
            }
        }

        private void ProcessAvatarMesh(Renderer r, Mesh baseM)
        {
            AddMeshData(r, baseM);

            int it = Mathf.Clamp(settings.TempSubdivisionIterations, 0, 3);
            int subMeshCount = baseM.subMeshCount;
            string rendererPath = EditorPathUtils.GetGameObjectPath(r);
            int baseOffset = verts.Count - baseM.vertexCount;

            for (int smi = 0; smi < subMeshCount; smi++)
            {
                int[] triLocal = baseM.GetTriangles(smi);
                if (triLocal == null || triLocal.Length == 0) continue;

                var triGlobal = BuildGlobalTrianglesFromLocal(triLocal, baseOffset);

                for (int k = 0; k < it; k++)
                {
                    triGlobal = SubdivideOnceGlobal(triGlobal, rendererPath);
                }

                string matName = (r.sharedMaterials != null && smi < r.sharedMaterials.Length && r.sharedMaterials[smi] != null)
                    ? r.sharedMaterials[smi].name
                    : $"{GameObjectConstants.SUBMESH_NAME_PREFIX}{smi}";

                subDatas.Add((triGlobal.ToArray(), matName));
                subRendererPaths.Add(rendererPath);
                subMeshIndices.Add(smi);
            }
        }

        private List<int> BuildGlobalTrianglesFromLocal(int[] triLocal, int baseOffset)
        {
            var list = new List<int>(triLocal.Length);
            for (int i = 0; i < triLocal.Length; i++)
            {
                list.Add(baseOffset + triLocal[i]);
            }
            return list;
        }

        private List<int> SubdivideOnceGlobal(List<int> triGlobal, string rendererPath)
        {
            var midCache = new Dictionary<(int a, int b), int>();

            int MidIdxGlobal(int a, int b)
            {
                int x = a, y = b; if (x > y) (x, y) = (y, x);
                if (midCache.TryGetValue((x, y), out int idx)) return idx;

                int newIdx = verts.Count;
                verts.Add((verts[a] + verts[b]) * 0.5f);
                norms.Add((norms[a] + norms[b]).normalized);
                uvs.Add((uvs[a] + uvs[b]) * 0.5f);

                Vector4 tangentA = (a < tangents.Count) ? tangents[a] : new Vector4(1, 0, 0, 1);
                Vector4 tangentB = (b < tangents.Count) ? tangents[b] : new Vector4(1, 0, 0, 1);
                Vector4 interpolatedTangent = (tangentA + tangentB) * 0.5f;
                tangents.Add(interpolatedTangent);

                if (vertexToMaterialName.TryGetValue(a, out string materialName))
                {
                    vertexToMaterialName[newIdx] = materialName;
                }
                else if (vertexToMaterialName.TryGetValue(b, out materialName))
                {
                    vertexToMaterialName[newIdx] = materialName;
                }

                float bm = 0f;
                if (a < boneMaskValues.Count && b < boneMaskValues.Count)
                {
                    bm = 0.5f * (boneMaskValues[a] + boneMaskValues[b]);
                }
                boneMaskValues.Add(bm);

                midCache[(x, y)] = newIdx;
                return newIdx;
            }

            var outTriangles = new List<int>(triGlobal.Count * 4);
            for (int t = 0; t < triGlobal.Count; t += 3)
            {
                if (t + 2 >= triGlobal.Count) break;
                int a = triGlobal[t];
                int b = triGlobal[t + 1];
                int c = triGlobal[t + 2];
                int ab = MidIdxGlobal(a, b);
                int bc = MidIdxGlobal(b, c);
                int ca = MidIdxGlobal(c, a);
                outTriangles.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
            }
            return outTriangles;
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
            string rendererPath = EditorPathUtils.GetGameObjectPath(r);
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
                int globalVertexIndex = verts.Count;
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

        private void SubdivideMesh(Mesh baseM, Material[] mats, Renderer r)
        {
            if (baseM == null) return;

            int subMeshCount = baseM.subMeshCount;
            string rendererPath = EditorPathUtils.GetGameObjectPath(r);

            int baseOffset = verts.Count - baseM.vertexCount;

            for (int smi = 0; smi < subMeshCount; smi++)
            {
                int[] triLocal = baseM.GetTriangles(smi);
                if (triLocal == null || triLocal.Length == 0) continue;

                var midCache = new Dictionary<(int a, int b), int>();

                int MidIdx(int aLocal, int bLocal)
                {
                    if (aLocal > bLocal) (aLocal, bLocal) = (bLocal, aLocal);
                    if (midCache.TryGetValue((aLocal, bLocal), out int idx)) return idx;

                    int aGlobal = baseOffset + aLocal;
                    int bGlobal = baseOffset + bLocal;

                    int newIdx = verts.Count;
                    verts.Add((verts[aGlobal] + verts[bGlobal]) * 0.5f);
                    norms.Add((norms[aGlobal] + norms[bGlobal]).normalized);
                    uvs.Add((uvs[aGlobal] + uvs[bGlobal]) * 0.5f);

                    Vector4 tangentAGlobal = (aGlobal < tangents.Count) ? tangents[aGlobal] : new Vector4(1, 0, 0, 1);
                    Vector4 tangentBGlobal = (bGlobal < tangents.Count) ? tangents[bGlobal] : new Vector4(1, 0, 0, 1);
                    Vector4 interpolatedTangent = (tangentAGlobal + tangentBGlobal) * 0.5f;
                    tangents.Add(interpolatedTangent);

                    if (vertexToMaterialName.TryGetValue(aGlobal, out string materialName))
                    {
                        vertexToMaterialName[newIdx] = materialName;
                    }
                    else if (vertexToMaterialName.TryGetValue(bGlobal, out materialName))
                    {
                        vertexToMaterialName[newIdx] = materialName;
                    }

                    float bm = 0f;
                    if (aGlobal < boneMaskValues.Count && bGlobal < boneMaskValues.Count)
                    {
                        bm = 0.5f * (boneMaskValues[aGlobal] + boneMaskValues[bGlobal]);
                    }
                    boneMaskValues.Add(bm);

                    midCache[(aLocal, bLocal)] = newIdx;
                    return newIdx;
                }

                var outTriangles = new List<int>(triLocal.Length * 4);
                for (int t = 0; t < triLocal.Length; t += 3)
                {
                    if (t + 2 >= triLocal.Length) break;

                    int aLocal = triLocal[t];
                    int bLocal = triLocal[t + 1];
                    int cLocal = triLocal[t + 2];

                    int a = baseOffset + aLocal;
                    int b = baseOffset + bLocal;
                    int c = baseOffset + cLocal;

                    int ab = MidIdx(aLocal, bLocal);
                    int bc = MidIdx(bLocal, cLocal);
                    int ca = MidIdx(cLocal, aLocal);

                    outTriangles.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
                }

                string matName = (mats != null && smi < mats.Length && mats[smi] != null)
                    ? mats[smi].name
                    : $"{GameObjectConstants.SUBMESH_NAME_PREFIX}{smi}";

                subDatas.Add((outTriangles.ToArray(), matName));
                subRendererPaths.Add(rendererPath);
                subMeshIndices.Add(smi);
            }
        }

        private float ResolveBoneMaskWithInheritance(Transform bone)
        {
            if (bone == null) return 0f;

            if (boneMaskMap.TryGetValue(bone, out float value))
            {
                return value;
            }

            Transform current = bone.parent;
            while (current != null)
            {
                if (boneMaskMap.TryGetValue(current, out value))
                {
                    boneMaskMap[bone] = value;
                    return value;
                }
                current = current.parent;
            }

            return 0f;
        }

        private float CalculateBoneMaskValue(BoneWeight bw, Transform[] bones, Dictionary<Transform, float> resolvedMask)
        {
            float totalWeight = 0f;
            float weightedValue = 0f;

            if (bw.boneIndex0 >= 0 && bw.boneIndex0 < bones.Length && bw.weight0 > 0)
            {
                var bone = bones[bw.boneIndex0];
                if (resolvedMask.TryGetValue(bone, out float maskValue))
                {
                    weightedValue += maskValue * bw.weight0;
                    totalWeight += bw.weight0;
                }
            }

            if (bw.boneIndex1 >= 0 && bw.boneIndex1 < bones.Length && bw.weight1 > 0)
            {
                var bone = bones[bw.boneIndex1];
                if (resolvedMask.TryGetValue(bone, out float maskValue))
                {
                    weightedValue += maskValue * bw.weight1;
                    totalWeight += bw.weight1;
                }
            }

            if (bw.boneIndex2 >= 0 && bw.boneIndex2 < bones.Length && bw.weight2 > 0)
            {
                var bone = bones[bw.boneIndex2];
                if (resolvedMask.TryGetValue(bone, out float maskValue))
                {
                    weightedValue += maskValue * bw.weight2;
                    totalWeight += bw.weight2;
                }
            }

            if (bw.boneIndex3 >= 0 && bw.boneIndex3 < bones.Length && bw.weight3 > 0)
            {
                var bone = bones[bw.boneIndex3];
                if (resolvedMask.TryGetValue(bone, out float maskValue))
                {
                    weightedValue += maskValue * bw.weight3;
                    totalWeight += bw.weight3;
                }
            }

            return totalWeight > 0 ? weightedValue / totalWeight : 0f;
        }

        

        #endregion
    }
}
#endif


