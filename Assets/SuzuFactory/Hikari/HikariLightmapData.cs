#if UNITY_EDITOR
using System;
using UnityEngine;

namespace SuzuFactory.Hikari
{
    [Serializable]
    public sealed class HikariLightmapData
    {
        [Serializable]
        public struct Atlas
        {
            public Texture2D Color;
            public Texture2D Direction;
            public Texture2D ShadowMask;
        }

        [Serializable]
        public struct RendererBinding
        {
            public Renderer Renderer;
            public int LightmapIndex;
            public Vector4 ScaleOffset;
        }

        [Serializable]
        public struct TerrainBinding
        {
            public Terrain Terrain;
            public int LightmapIndex;
            public Vector4 ScaleOffset;
        }

        [Serializable]
        public struct ReflectionProbeBinding
        {
            public ReflectionProbe Probe;
            public Texture Texture;
        }

        [Serializable]
        public struct LightBinding
        {
            public Light Light;
            public bool IsBaked;
            public LightmapBakeType BakeType;
            public MixedLightingMode MixedMode;
            public int OcclusionMaskChannel;
            public bool HasProbeOcclusionLightIndex;
            public int ProbeOcclusionLightIndex;

            public int ResolvedProbeOcclusionLightIndex => HasProbeOcclusionLightIndex
                ? ProbeOcclusionLightIndex
                : MixedMode == MixedLightingMode.Shadowmask && OcclusionMaskChannel >= 0 && OcclusionMaskChannel < 4
                    ? OcclusionMaskChannel
                    : -1;
        }

        public LightmapsMode Mode = LightmapsMode.NonDirectional;
        public Atlas[] Atlases = Array.Empty<Atlas>();
        public RendererBinding[] Renderers = Array.Empty<RendererBinding>();
        public TerrainBinding[] Terrains = Array.Empty<TerrainBinding>();
        public ReflectionProbeBinding[] ReflectionProbes = Array.Empty<ReflectionProbeBinding>();
        public LightBinding[] Lights = Array.Empty<LightBinding>();
        public LightProbes LightProbes;
        public float[] LightProbeOcclusion = Array.Empty<float>();
        public int LightProbeOcclusionLightCount;
    }
}
#endif
