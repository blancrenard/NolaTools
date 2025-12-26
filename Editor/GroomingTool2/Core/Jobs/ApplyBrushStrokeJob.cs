using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using GroomingTool2.Core;

namespace GroomingTool2.Core.Jobs
{
    /// <summary>
    /// ブラシストローク適用ジョブ（IJob版 - シングルスレッド）
    /// NativeArray を直接操作し、コピーを最小化
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    internal struct ApplyBrushStrokeJob : IJob
    {
        // Texture size (for index calculation)
        public int TexSize;

        // Data - 全体配列を直接操作
        public NativeArray<FurData> FurData; // Size: TexSize * TexSize
        [ReadOnly] public NativeArray<int2> Points;
        [ReadOnly] public NativeArray<MyBrushData> BrushSamples;
        [ReadOnly] public NativeArray<byte> Mask; // Size: TexSize * TexSize (optional)

        // Stroke parameters
        public float Cos1;
        public float Sin1;
        public float BrushPowerCubed;
        public float BrushPowerScale;
        public float MaxInclination;
        public int BrushSize;
        public byte BlurMode;
        public byte PinchMode;
        public byte PinchInverted;
        public byte InclinedOnly;
        public byte DirOnly;
        public float2 BlurDirNormalized;
        public float BlurAverageLength;
        public byte BlurUseFallback;

        public void Execute()
        {
            int pointsLen = Points.Length;
            int brushLen = BrushSamples.Length;

            for (int i = 0; i < pointsLen; i++)
            {
                var point = Points[i];

                for (int b = 0; b < brushLen; b++)
                {
                    var sample = BrushSamples[b];
                    int gx = point.x + sample.X;
                    int gy = point.y + sample.Y;

                    // 境界チェック
                    if (gx < 0 || gx >= TexSize || gy < 0 || gy >= TexSize)
                        continue;

                    // 全体配列への直接インデックス
                    int index = gy * TexSize + gx;

                    // Mask check
                    if (Mask.IsCreated && Mask.Length > 0)
                    {
                        if (Mask[index] == 0) continue;
                    }

                    var data = FurData[index];
                    ApplyBrushToPixel(ref data, sample);
                    FurData[index] = data;
                }
            }
        }

        private void ApplyBrushToPixel(ref FurData data, MyBrushData sample)
        {
            // Calculate current vector from data
            float rad = data.Dir * 0.1f * 0.0174532925f; // Deg2Rad
            float cos2 = math.cos(rad);
            float sin2 = math.sin(rad);
            float currentLen = data.Inclined;
            float currX = currentLen * cos2;
            float currY = currentLen * sin2;

            float sumX;
            float sumY;
            float influence = sample.Influence;

            if (BlurMode == 1)
            {
                float alpha = math.saturate(BrushPowerCubed * influence * BrushPowerScale);
                float2 target;
                if (BlurUseFallback == 1)
                {
                    target = new float2(0f, 0f);
                }
                else
                {
                    target = BlurAverageLength * BlurDirNormalized;
                }
                sumX = math.lerp(currX, target.x, alpha);
                sumY = math.lerp(currY, target.y, alpha);
            }
            else if (PinchMode == 1)
            {
                float tmpInclined = math.sqrt((float)(sample.X * sample.X + sample.Y * sample.Y));
                tmpInclined = tmpInclined / math.max(1, (float)BrushSize);

                float sampleRad = math.atan2((float)sample.Y, (float)sample.X);
                float2 pinchDirVec = new float2(math.cos(sampleRad), math.sin(sampleRad));

                float alpha = math.saturate(BrushPowerCubed * influence * tmpInclined * BrushPowerScale);
                float sign = (PinchInverted == 1) ? 1f : -1f;
                float2 delta = sign * MaxInclination * pinchDirVec * alpha;

                sumX = currX + delta.x;
                sumY = currY + delta.y;
            }
            else
            {
                float2 dirVec = (InclinedOnly == 1) ? new float2(cos2, sin2) : new float2(Cos1, Sin1);
                float alpha = math.saturate(BrushPowerCubed * influence * BrushPowerScale);
                float2 target = MaxInclination * dirVec;
                sumX = math.lerp(currX, target.x, alpha);
                sumY = math.lerp(currY, target.y, alpha);
            }

            // Calculate new Dir and Inclined
            int outDir;
            if (InclinedOnly == 1)
            {
                outDir = data.Dir;
            }
            else
            {
                float len2 = sumX * sumX + sumY * sumY;
                if (len2 > 1e-12f)
                {
                    float newRad = math.atan2(sumY, sumX);
                    // Round to dir (deg * 10)
                    float v = newRad * 572.957795f; // Rad2Deg * 10
                    int idir = (int)math.floor(v + 0.5f);
                    if (v < 0f && (v - math.floor(v)) == 0.5f)
                        idir = (int)math.ceil(v - 0.5f);
                    
                    // Wrap
                    const int MinDir = -1800;
                    const int range = 3600;
                    int offset = idir - MinDir;
                    int wrapped = ((offset % range) + range) % range;
                    outDir = wrapped + MinDir;
                }
                else
                {
                    outDir = 0;
                }
            }

            float outLen;
            if (DirOnly == 1)
            {
                outLen = currentLen;
            }
            else
            {
                float len = math.sqrt(sumX * sumX + sumY * sumY);
                // MaxInclinationが0より大きい場合のみ、MaxInclinationを超えた分をブレンドする
                // MaxInclinationが0の場合（消しゴムモード）は、lerpで計算されたlenをそのまま使用
                if (MaxInclination > 0f && len > MaxInclination)
                {
                    float excess = len - MaxInclination;
                    float currentExcess = math.max(0f, currentLen - MaxInclination);
                    float totalExcess = math.max(excess, currentExcess);
                    
                    if (totalExcess > 0f)
                    {
                        float blendFactor = math.clamp(0.1f + 0.2f * (1f - excess / math.max(0.1f, totalExcess)), 0f, 1f);
                        len = math.lerp(currentLen, MaxInclination, blendFactor);
                    }
                    else
                    {
                        len = MaxInclination;
                    }
                }
                outLen = len;
            }

            data.Dir = outDir;
            data.Inclined = outLen;
        }
    }
}
