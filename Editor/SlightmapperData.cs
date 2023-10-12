#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SubsurfaceStudios.Slightmapper.Assets {
	[CreateAssetMenu(fileName = "NewSlightmapperData", menuName = "Debug/Slightmapper Runtime Data")]
    public class SlightmapperData : ScriptableObject {
        [Header("Bake Data")]
        public List<SlightmapperBakeState> BaketimeStates = new();
	}

    [Serializable]
    public class SlightmapperBakeState {
        public string Name;
        public SlightmapperRuntimeState RuntimeState;
        public LightingDataAsset DataAsset;
        public ComptimeLightInfo[] LightSettings;
    }

    [Serializable]
    public struct ComptimeLightInfo {
        public string ObjectId;
        public bool Enabled;

        public Color Color;

        public float Range;
        public float Intensity;
        public float IndirectMultiplier;
    }
}
#endif
