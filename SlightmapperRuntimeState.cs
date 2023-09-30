using System;
using System.Linq;
using System.Runtime.CompilerServices;
using SubsurfaceStudios.Slightmapper.Global;
using UnityEngine;

namespace SubsurfaceStudios.Slightmapper.Assets {
	[CreateAssetMenu(fileName = "NewSlightmapperState", menuName = "Debug/Slightmapper Runtime State")]
    public class SlightmapperRuntimeState : ScriptableObject {
        public LightmapReference[] lightmaps;
        public LightmapsMode lightmapsMode;
        public LightProbes lightProbes;
        public RendererInfo[] staticRendererData;
        public ReflectionProbeInfo[] reflectionProbeData;

        public void Load() {
            LightmapSettings.lightmaps      = lightmaps.Select(x => x.ToLightmapData()).ToArray();
            LightmapSettings.lightmapsMode  = lightmapsMode;
            LightmapSettings.lightProbes    = lightProbes;

            int renderer_iterations = staticRendererData.Length;
            for (int i = 0; i < renderer_iterations; i++) {
                staticRendererData[i].Apply();
            }

            int reflection_probe_iterations = reflectionProbeData.Length;
            for (int i = 0; i < reflection_probe_iterations; i++) {
                reflectionProbeData[i].Apply();
            }
        }
    }

    [Serializable]
    public struct LightmapReference {
        public Texture2D lightmapColor;
        public Texture2D lightmapDir;
        public Texture2D shadowMask;

        public readonly LightmapData ToLightmapData() => new() {
            lightmapColor   = lightmapColor,
            lightmapDir     = lightmapDir,
            shadowMask      = shadowMask
        };

        public LightmapReference(LightmapData original) {
            lightmapColor   = original.lightmapColor;
            lightmapDir     = original.lightmapDir;
            shadowMask      = original.shadowMask;
        }
    }

    [Serializable]
    public struct RendererInfo {
        public uint rendererId;
        public Vector4 lightmapScaleOffset;
        public int lightmapIndex;

        public RendererInfo(MeshRenderer renderer) {
            Debug.Assert(renderer.TryGetComponent(out RendererId reference));

			rendererId          = reference.Id;
            lightmapScaleOffset = renderer.lightmapScaleOffset;
            lightmapIndex       = renderer.lightmapIndex;
        }

        public void Apply() {
            RendererId reference = RendererIdAllocator.Index[rendererId];

            reference.RendererIfAvailable.lightmapScaleOffset    = lightmapScaleOffset;
            reference.RendererIfAvailable.lightmapIndex          = lightmapIndex;
        }
    }

    [Serializable]
    public struct ReflectionProbeInfo {
        public uint rendererId;
        public Texture bakedTexture;

        public ReflectionProbeInfo(ReflectionProbe probe) {
            Debug.Assert(probe.TryGetComponent(out RendererId reference));

            rendererId = reference.Id;
            bakedTexture = probe.bakedTexture;
        }

        public void Apply() {
            RendererId reference = RendererIdAllocator.Index[rendererId];

            reference.ReflectionProbeIfAvailable.bakedTexture   = bakedTexture;
        }
    }
}
