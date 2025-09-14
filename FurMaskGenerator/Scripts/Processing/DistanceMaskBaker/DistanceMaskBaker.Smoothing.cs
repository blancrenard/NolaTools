#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mask.Generator.Utils;
using Mask.Generator.Data;
using Mask.Generator.Constants;

namespace Mask.Generator
{
    public partial class DistanceMaskBaker
    {
        #region Smoothing

        private float[] SmoothDist(float[] src, int it)
        {
            var adj = BuildVertexAdjacency();

            Array.Copy(src, smoothBuffer1, src.Length);
            float[] cur = smoothBuffer1, tmp = smoothBuffer2;

            const float convergenceThreshold = 0.0001f;
            
            for (int k = 0; k < it; k++)
            {
                float maxChange = 0f;
                
                for (int i = 0; i < cur.Length; i++)
                {
                    float sum = cur[i];
                    int ct = 1;
                    if(adj[i] != null)
                    {
                        foreach (int n in adj[i])
                        {
                            sum += cur[n];
                            ct++;
                        }
                    }
                    
                    float newValue = sum / ct;
                    float change = Mathf.Abs(newValue - cur[i]);
                    maxChange = Mathf.Max(maxChange, change);
                    tmp[i] = newValue;

                    if ((i % progressUpdateInterval) == 0)
                    {
                        float prog = 0.8f + 0.2f * ((k * cur.Length + i) / (float)(it * cur.Length));
                        if (ShouldUpdateProgressBar(prog, $"スムージング {k+1}/{it}"))
                        {
                            cancelRequested = true;
                            return cur;
                        }
                    }
                }
                
                (cur, tmp) = (tmp, cur);
                
                if (maxChange < convergenceThreshold)
                {
                    break;
                }
            }
            return cur;
        }

        private float[] SmoothDistAnchored(float[] src, int it, bool[] anchors)
        {
            var adj = BuildVertexAdjacency();

            Array.Copy(src, smoothBuffer1, src.Length);
            float[] cur = smoothBuffer1, tmp = smoothBuffer2;

            const float convergenceThreshold = 0.0001f;

            for (int k = 0; k < it; k++)
            {
                float maxChange = 0f;
                
                for (int i = 0; i < cur.Length; i++)
                {
                    if (anchors != null && i < anchors.Length && anchors[i])
                    {
                        tmp[i] = cur[i];
                        continue;
                    }
                    
                    float sum = cur[i];
                    int ct = 1;
                    if(adj[i] != null)
                    {
                        foreach (int n in adj[i])
                        {
                            sum += cur[n];
                            ct++;
                        }
                    }
                    
                    float newValue = sum / ct;
                    float change = Mathf.Abs(newValue - cur[i]);
                    maxChange = Mathf.Max(maxChange, change);
                    tmp[i] = newValue;

                    if ((i % progressUpdateInterval) == 0)
                    {
                        float prog = 0.8f + 0.2f * ((k * cur.Length + i) / (float)(it * cur.Length));
                        if (ShouldUpdateProgressBar(prog, $"スムージング {k+1}/{it}"))
                        {
                            cancelRequested = true;
                            return cur;
                        }
                    }
                }
                
                (cur, tmp) = (tmp, cur);
                
                if (maxChange < convergenceThreshold)
                {
                    break;
                }
            }
            return cur;
        }

        private HashSet<int>[] BuildVertexAdjacency()
        {
            var adj = new HashSet<int>[verts.Count];
            for (int i = 0; i < adj.Length; i++) adj[i] = new HashSet<int>();
            foreach (var (tri, _) in subDatas)
            {
                for (int i = 0; i < tri.Length; i += 3)
                {
                    AddEdge(adj, tri[i], tri[i + 1]);
                    AddEdge(adj, tri[i], tri[i + 2]);
                    AddEdge(adj, tri[i + 1], tri[i + 2]);
                }
            }
            return adj;
        }

        private void AddEdge(HashSet<int>[] adj, int x, int y)
        {
            if (x < adj.Length && y < adj.Length)
            {
                adj[x].Add(y);
                adj[y].Add(x);
            }
        }

        #endregion
    }
}
#endif


