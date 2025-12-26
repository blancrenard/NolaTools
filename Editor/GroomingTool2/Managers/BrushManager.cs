using System.Collections.Generic;
using GroomingTool2.Core;
using GroomingTool2.Services;
using UnityEngine;

namespace GroomingTool2.Managers
{
    internal sealed class BrushManager
    {
        private readonly Dictionary<string, Gradient> gradientCache = new();
        private readonly IStrokeService strokeService;

        public List<MyBrushData> Brush { get; private set; }
        public int BrushSize { get; private set; } = 16;
        public float BrushPower { get; private set; } = 0.3f;
		public float MaxInclination { get; private set; } = 0.75f;
        public Color FurColorPrimary { get; private set; } = Color.white;
        public Color FurColorSecondary { get; private set; } = new Color(1f, 1f, 1f, 0f);

        public BrushManager(IStrokeService strokeService)
        {
            this.strokeService = strokeService;
            Brush = Common.CreateBrush(BrushSize);
        }

        public void SetBrushSize(int size)
        {
            if (BrushSize == size)
                return;

            BrushSize = Mathf.Clamp(size, 1, 256);
            Brush = Common.CreateBrush(BrushSize);
        }

        public void SetBrushPower(float power)
        {
            if (Mathf.Approximately(BrushPower, power))
                return;

            BrushPower = Mathf.Clamp01(power);
        }

		public void SetMaxInclination(float max)
		{
			var clamped = Mathf.Clamp(max, 0f, 0.95f);
			if (Mathf.Approximately(MaxInclination, clamped))
				return;

			MaxInclination = clamped;
		}

        public void SetFurColors(Color primary, Color secondary)
        {
            if (FurColorPrimary == primary && FurColorSecondary == secondary)
                return;

            FurColorPrimary = primary;
            FurColorSecondary = secondary;
            gradientCache.Clear();
        }

        public Gradient GetGradient(Vector2 origin, Vector2 direction)
        {
            var key = $"{origin.x:F2}:{origin.y:F2}:{direction.x:F2}:{direction.y:F2}:{FurColorPrimary}:{FurColorSecondary}";
            if (gradientCache.TryGetValue(key, out var gradient))
                return gradient;

            gradient = new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(FurColorSecondary, 0f),
                    new GradientColorKey(FurColorPrimary, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(FurColorSecondary.a, 0f),
                    new GradientAlphaKey(FurColorPrimary.a, 1f)
                }
            };

            gradientCache[key] = gradient;
            return gradient;
        }

        public Vector2 GetAverageDirection(IReadOnlyList<Vector2> points)
        {
            return strokeService.CalculateAverageDirection(points);
        }
    }
}



