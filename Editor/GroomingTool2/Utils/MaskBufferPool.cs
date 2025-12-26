using System.Collections.Generic;
using GroomingTool2.Core;

namespace GroomingTool2.Utils
{
    /// <summary>
    /// bool[,] マスクバッファのプールを管理するクラス
    /// ExtractConnectedRegion や BuildStrokeMask での頻繁なメモリ確保を削減
    /// </summary>
    internal static class MaskBufferPool
    {
        private static readonly Stack<bool[,]> pool = new Stack<bool[,]>();
        private const int MaxPoolSize = 4;

        /// <summary>
        /// プールからバッファを取得（利用可能なものがなければ新規作成）
        /// </summary>
        public static bool[,] Rent()
        {
            lock (pool)
            {
                if (pool.Count > 0)
                {
                    return pool.Pop();
                }
            }
            return new bool[Common.TexSize, Common.TexSize];
        }

        /// <summary>
        /// バッファをプールに返却（自動でクリア）
        /// </summary>
        public static void Return(bool[,] buffer)
        {
            if (buffer == null)
                return;

            // サイズが合わないバッファは返却しない
            if (buffer.GetLength(0) != Common.TexSize || buffer.GetLength(1) != Common.TexSize)
                return;

            // バッファをクリア
            System.Array.Clear(buffer, 0, buffer.Length);

            lock (pool)
            {
                // プールが大きくなりすぎないように制限
                if (pool.Count < MaxPoolSize)
                {
                    pool.Push(buffer);
                }
            }
        }

        /// <summary>
        /// 指定範囲のみをクリア（高速版）
        /// </summary>
        public static void ClearRange(bool[,] buffer, int minX, int maxX, int minY, int maxY)
        {
            if (buffer == null)
                return;

            int sizeX = buffer.GetLength(0);
            int sizeY = buffer.GetLength(1);
            
            minX = System.Math.Max(0, minX);
            maxX = System.Math.Min(sizeX - 1, maxX);
            minY = System.Math.Max(0, minY);
            maxY = System.Math.Min(sizeY - 1, maxY);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    buffer[x, y] = false;
                }
            }
        }

        /// <summary>
        /// プールをクリア（メモリ解放用）
        /// </summary>
        public static void Clear()
        {
            lock (pool)
            {
                pool.Clear();
            }
        }
    }
}

