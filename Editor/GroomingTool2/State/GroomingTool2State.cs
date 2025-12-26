using System;
using System.ComponentModel;
using UnityEngine;

namespace GroomingTool2.State
{
    internal sealed class GroomingTool2State : ScriptableObject, INotifyPropertyChanged
    {
        private const string AssetName = "GroomingTool2State";

        [SerializeField] private float scale = 1f;
        [SerializeField] private float inclined = 0.75f;
        [SerializeField] private int brushSize = 16;
        [SerializeField] private float brushPower = 0.3f;
        [SerializeField] private int displayInterval = 16;
        [SerializeField] private int sceneViewDisplayInterval = 1;
        [SerializeField] private Color wireframeColor = new Color(1f, 1f, 1f, 0.3f);
        [SerializeField] private int uvPadding = 4;
        [SerializeField] private Color sceneViewHairColor = Color.white;
        [SerializeField] private float autoSetupSurfaceLift = 0.75f;
        [SerializeField] private float autoSetupRandomness = 0f;
        [SerializeField] private bool sceneEditingEnabled = false;
        [SerializeField] private bool useGpuRendering = true;
        [NonSerialized] private GameObject avatar;

        public event PropertyChangedEventHandler PropertyChanged;

        public float Scale
        {
            get => scale;
            set
            {
                if (Mathf.Approximately(scale, value)) return;
                scale = Mathf.Clamp(value, 0.25f, 8f);
                OnPropertyChanged(nameof(Scale));
            }
        }

        public float Inclined
        {
            get => inclined;
            set
            {
                if (Mathf.Approximately(inclined, value)) return;
                inclined = Mathf.Clamp(value, 0f, 0.95f);
                OnPropertyChanged(nameof(Inclined));
            }
        }

        public int BrushSize
        {
            get => brushSize;
            set
            {
                if (brushSize == value) return;
                brushSize = Mathf.Clamp(value, 1, 256);
                OnPropertyChanged(nameof(BrushSize));
            }
        }

        public float BrushPower
        {
            get => brushPower;
            set
            {
                if (Mathf.Approximately(brushPower, value)) return;
                brushPower = Mathf.Max(0f, value);
                OnPropertyChanged(nameof(BrushPower));
            }
        }

        public int DisplayInterval
        {
            get => displayInterval;
            set
            {
                var clamped = Mathf.Clamp(value, 1, 128);
                if (displayInterval == clamped) return;
                displayInterval = clamped;
                OnPropertyChanged(nameof(DisplayInterval));
            }
        }

        public int SceneViewDisplayInterval
        {
            get => sceneViewDisplayInterval;
            set
            {
                var clamped = Mathf.Clamp(value, 1, 128);
                if (sceneViewDisplayInterval == clamped) return;
                sceneViewDisplayInterval = clamped;
                OnPropertyChanged(nameof(SceneViewDisplayInterval));
            }
        }

        public Color WireframeColor
        {
            get => wireframeColor;
            set
            {
                if (wireframeColor == value) return;
                wireframeColor = value;
                OnPropertyChanged(nameof(WireframeColor));
            }
        }

        public int UvPadding
        {
            get => uvPadding;
            set
            {
                var clamped = Mathf.Clamp(value, 0, 32);
                if (uvPadding == clamped) return;
                uvPadding = clamped;
                OnPropertyChanged(nameof(UvPadding));
            }
        }

        public Color SceneViewHairColor
        {
            get => sceneViewHairColor;
            set
            {
                if (sceneViewHairColor == value) return;
                sceneViewHairColor = value;
                OnPropertyChanged(nameof(SceneViewHairColor));
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public GameObject Avatar
        {
            get => avatar;
            set => avatar = value;
        }

        public float AutoSetupSurfaceLift
        {
            get => autoSetupSurfaceLift;
            set
            {
                if (Mathf.Approximately(autoSetupSurfaceLift, value)) return;
                autoSetupSurfaceLift = Mathf.Clamp(value, 0f, 1f);
                OnPropertyChanged(nameof(AutoSetupSurfaceLift));
            }
        }

        public float AutoSetupRandomness
        {
            get => autoSetupRandomness;
            set
            {
                if (Mathf.Approximately(autoSetupRandomness, value)) return;
                autoSetupRandomness = Mathf.Clamp(value, 0f, 0.5f);
                OnPropertyChanged(nameof(AutoSetupRandomness));
            }
        }

        public bool SceneEditingEnabled
        {
            get => sceneEditingEnabled;
            set
            {
                if (sceneEditingEnabled == value) return;
                sceneEditingEnabled = value;
                OnPropertyChanged(nameof(SceneEditingEnabled));
            }
        }

        public bool UseGpuRendering
        {
            get => useGpuRendering;
            set
            {
                if (useGpuRendering == value) return;
                useGpuRendering = value;
                OnPropertyChanged(nameof(UseGpuRendering));
            }
        }
    }

    [Serializable]
    internal sealed class FurDataSerialized
    {
        [SerializeField] private int[] dir;
        [SerializeField] private float[] inclined;

        public int[] Dir
        {
            get => dir;
            set => dir = value;
        }

        public float[] Inclined
        {
            get => inclined;
            set => inclined = value;
        }
    }

}
