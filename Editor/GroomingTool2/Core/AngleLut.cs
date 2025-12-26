using System.Runtime.CompilerServices;
using UnityEngine;

namespace GroomingTool2.Core
{
    internal static class AngleLut
    {
        public const int Step = 10;
        public const int MinDir = -1800;
        public const int MaxDir = 1800;
        private const int Size = MaxDir - MinDir + 1;

        private static readonly float[] CosTable;
        private static readonly float[] SinTable;

        static AngleLut()
        {
            CosTable = new float[Size];
            SinTable = new float[Size];

            for (var dir = MinDir; dir <= MaxDir; dir++)
            {
                var rad = dir * 0.1f * Mathf.Deg2Rad;
                var index = dir - MinDir;
                CosTable[index] = Mathf.Cos(rad);
                SinTable[index] = Mathf.Sin(rad);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetCos(int dir)
        {
            return CosTable[WrapDir(dir) - MinDir];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetSin(int dir)
        {
            return SinTable[WrapDir(dir) - MinDir];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WrapDir(int dir)
        {
            // 高速化：whileループの代わりに剰余演算を使用
            const int range = 3600;
            int offset = dir - MinDir;
            int wrapped = ((offset % range) + range) % range;
            return wrapped + MinDir;
        }
    }
}



