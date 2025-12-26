using System;
using GroomingTool2.Core;
using GroomingTool2.Utils;
using UnityEngine;

namespace GroomingTool2.State
{
    /// <summary>
    /// UVアイランドマスクの状態を管理するクラス（セッション限定）
    /// </summary>
    internal sealed class UvIslandMaskState
    {
        private bool[,] baseSelected;
        private bool[,] effectiveSelected;
        private bool restrictEditing;
        private readonly GroomingTool2State state;
        
        // キャッシュ用のバージョン番号と選択有無フラグ
        private int version;
        private bool hasAnySelection;

        /// <summary>
        /// マスクのバージョン番号（変更検出用）
        /// </summary>
        public int Version => version;

        /// <summary>
        /// 選択されているピクセルがあるかどうか（高速判定用）
        /// </summary>
        public bool HasAnySelection => hasAnySelection;

        public bool RestrictEditing
        {
            get => restrictEditing;
            set
            {
                if (restrictEditing != value)
                {
                    restrictEditing = value;
                    version++;
                }
            }
        }

        /// <summary>
        /// エッジパディング（GroomingTool2State.UvPaddingを参照）
        /// </summary>
        public int EdgePaddingPx => state?.UvPadding ?? 4;

        public bool[,] BaseSelected => baseSelected;
        public bool[,] EffectiveSelected => effectiveSelected;

        public UvIslandMaskState(GroomingTool2State state)
        {
            this.state = state;
            baseSelected = new bool[Common.TexSize, Common.TexSize];
            effectiveSelected = new bool[Common.TexSize, Common.TexSize];
            restrictEditing = true;
            
            // 初期状態はどこもマスクされていない状態にする
            Array.Clear(baseSelected, 0, baseSelected.Length);
            Array.Clear(effectiveSelected, 0, effectiveSelected.Length);
            RecalculateEffective();
        }

        /// <summary>
        /// ベースマスクをクリアする
        /// </summary>
        public void Clear()
        {
            Array.Clear(baseSelected, 0, baseSelected.Length);
            Array.Clear(effectiveSelected, 0, effectiveSelected.Length);
            hasAnySelection = false;
            version++;
        }

        /// <summary>
        /// 指定座標がベースマスク内かチェック
        /// </summary>
        public bool IsBaseSelected(int x, int y)
        {
            if (x < 0 || x >= Common.TexSize || y < 0 || y >= Common.TexSize)
                return false;
            return baseSelected[x, y];
        }

        /// <summary>
        /// 指定座標が有効マスク内かチェック
        /// </summary>
        public bool IsEffectiveSelected(int x, int y)
        {
            if (x < 0 || x >= Common.TexSize || y < 0 || y >= Common.TexSize)
                return false;
            return effectiveSelected[x, y];
        }

        /// <summary>
        /// ベースマスクの指定座標を設定
        /// </summary>
        public void SetBasePixel(int x, int y, bool selected)
        {
            if (x < 0 || x >= Common.TexSize || y < 0 || y >= Common.TexSize)
                return;
            if (baseSelected[x, y] != selected)
            {
                baseSelected[x, y] = selected;
                if (selected)
                {
                    hasAnySelection = true;
                }
                // 注意: selected=false の場合、hasAnySelection は RecalculateEffective で更新される
                version++;
            }
        }

        /// <summary>
        /// ベースマスクを更新した後、有効マスクを再計算する
        /// </summary>
        public void RecalculateEffective()
        {
            int padding = EdgePaddingPx;
            
            // hasAnySelection をベースマスクから再計算
            hasAnySelection = false;
            for (int y = 0; y < Common.TexSize && !hasAnySelection; y++)
            {
                for (int x = 0; x < Common.TexSize; x++)
                {
                    if (baseSelected[x, y])
                    {
                        hasAnySelection = true;
                        break;
                    }
                }
            }
            
            if (padding <= 0)
            {
                Array.Copy(baseSelected, effectiveSelected, baseSelected.Length);
                version++;
                return;
            }

            // MaskBufferPoolを使用してアロケーションを削減
            // ダブルバッファリング: tempとnextを交互に使用
            var temp = MaskBufferPool.Rent();
            var next = MaskBufferPool.Rent();
            
            try
            {
                Array.Copy(baseSelected, temp, baseSelected.Length);

                for (int iteration = 0; iteration < padding; iteration++)
                {
                    // nextバッファをクリア（前回の内容が残っている可能性があるため）
                    Array.Clear(next, 0, next.Length);
                    
                    for (int y = 0; y < Common.TexSize; y++)
                    {
                        for (int x = 0; x < Common.TexSize; x++)
                        {
                            bool expanded = temp[x, y];
                            // 8近傍チェック
                            for (int dy = -1; dy <= 1 && !expanded; dy++)
                            {
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    int nx = x + dx;
                                    int ny = y + dy;
                                    if (nx >= 0 && nx < Common.TexSize && ny >= 0 && ny < Common.TexSize)
                                    {
                                        if (temp[nx, ny])
                                        {
                                            expanded = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            next[x, y] = expanded;
                        }
                    }
                    
                    // バッファを交換（参照のスワップのみ、コピーなし）
                    var swap = temp;
                    temp = next;
                    next = swap;
                }

                Array.Copy(temp, effectiveSelected, temp.Length);
            }
            finally
            {
                // プールに返却（MaskBufferPool.Returnで自動クリアされる）
                MaskBufferPool.Return(temp);
                MaskBufferPool.Return(next);
            }
            
            version++;
        }

        /// <summary>
        /// 有効マスクを1D byte配列に変換（NativeArray用）
        /// </summary>
        public byte[] GetEffectiveMaskAsBytes()
        {
            var bytes = new byte[Common.TexSize * Common.TexSize];
            for (int y = 0; y < Common.TexSize; y++)
            {
                for (int x = 0; x < Common.TexSize; x++)
                {
                    int index = y * Common.TexSize + x;
                    bytes[index] = effectiveSelected[x, y] ? (byte)1 : (byte)0;
                }
            }
            return bytes;
        }

        /// <summary>
        /// ベースマスクのコピーを作成（Undo/Redo用）
        /// </summary>
        public bool[,] CloneBaseSelected()
        {
            var clone = new bool[Common.TexSize, Common.TexSize];
            Array.Copy(baseSelected, clone, baseSelected.Length);
            return clone;
        }

        /// <summary>
        /// ベースマスクを復元（Undo/Redo用）
        /// </summary>
        public void RestoreBaseSelected(bool[,] restored)
        {
            if (restored == null || restored.GetLength(0) != Common.TexSize || restored.GetLength(1) != Common.TexSize)
                return;
            Array.Copy(restored, baseSelected, restored.Length);
            RecalculateEffective();
        }

        /// <summary>
        /// マスクを反転する
        /// </summary>
        public void Invert()
        {
            for (int y = 0; y < Common.TexSize; y++)
            {
                for (int x = 0; x < Common.TexSize; x++)
                {
                    baseSelected[x, y] = !baseSelected[x, y];
                }
            }
            RecalculateEffective();
        }

        /// <summary>
        /// 全選択する
        /// </summary>
        public void SelectAll()
        {
            for (int y = 0; y < Common.TexSize; y++)
            {
                for (int x = 0; x < Common.TexSize; x++)
                {
                    baseSelected[x, y] = true;
                }
            }
            hasAnySelection = true;
            RecalculateEffective();
        }

        /// <summary>
        /// 強制的にバージョンを更新する（外部からの変更通知用）
        /// </summary>
        public void IncrementVersion()
        {
            version++;
        }
    }
}

