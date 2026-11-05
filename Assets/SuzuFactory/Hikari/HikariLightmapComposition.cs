#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SuzuFactory.Hikari
{
    public static class HikariLightmapComposition
    {
        public static System.Action<IReadOnlyList<HikariLightmapData>> ComposeLightProbes;
        private const string DirectionalModeConflictMessage =
            "Hikari lightmap composition stopped because baked lightmap data uses inconsistent Directional Modes. " +
            "Bake Lightmaps to rebuild the whole scene with one Directional Mode.";

        private struct BlockSource
        {
            public Object Owner;
            public HikariLightmapData Data;
        }

        public static void Compose(Scene scene)
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            if (HikariLightmapper.ForeignBakerOwnsScene(scene))
            {
                return;
            }

            List<BlockSource> sources = CollectLoadedBlockSources();
            List<Object> conflicts = DirectionalModeConflicts(sources, null);
            if (conflicts.Count > 0)
            {
                foreach (Object conflict in conflicts)
                {
                    Debug.LogError(DirectionalModeConflictMessage, conflict);
                }
                return;
            }

            List<HikariLightmapData> blocks = CollectBlocks(sources);

            var combined = new List<LightmapData>();
            bool directional = false;
            var offsets = new int[blocks.Count];
            for (int i = 0; i < blocks.Count; i++)
            {
                offsets[i] = combined.Count;
                HikariLightmapData.Atlas[] atlases = blocks[i].Atlases;
                if (atlases == null)
                {
                    continue;
                }
                foreach (HikariLightmapData.Atlas atlas in atlases)
                {
                    if (atlas.Color == null)
                    {
                        combined.Add(new LightmapData());
                        continue;
                    }
                    combined.Add(new LightmapData
                    {
                        lightmapColor = atlas.Color,
                        lightmapDir = atlas.Direction,
                        shadowMask = atlas.ShadowMask
                    });
                    if (atlas.Direction != null)
                    {
                        directional = true;
                    }
                }
            }

            LightmapSettings.lightmaps = combined.ToArray();
            LightmapSettings.lightmapsMode = directional ? LightmapsMode.CombinedDirectional : LightmapsMode.NonDirectional;

            for (int i = 0; i < blocks.Count; i++)
            {
                ApplyBlock(blocks[i], offsets[i]);
            }
            if (ComposeLightProbes != null)
            {
                ComposeLightProbes(blocks);
            }
            else
            {
                ApplyLightProbes(CollectComposedBlocks(scene));
            }
        }

        public static List<HikariLightmapData> CollectComposedBlocks(Scene scene)
        {
            return CollectBlocks(CollectComposedBlockSources(scene));
        }

        public static List<Object> DirectionalModeConflicts(Scene scene)
        {
            return DirectionalModeConflicts(CollectLoadedBlockSources(), null);
        }

        public static bool CanReplaceDirectionalMode(
            Scene scene,
            HikariLightmapData replaced,
            LightmapsMode replacementMode,
            IEnumerable<Object> replacementTargets = null,
            bool replacementHasAtlases = true)
        {
            if (replacementMode != LightmapsMode.NonDirectional &&
                replacementMode != LightmapsMode.CombinedDirectional)
            {
                return false;
            }

            List<BlockSource> sources = CollectLoadedBlockSources(scene, replaced, replacementTargets);
            if (DirectionalModeConflicts(sources, replaced).Count > 0)
            {
                return false;
            }

            if (!replacementHasAtlases)
            {
                return true;
            }
            bool replacement_directional = replacementMode == LightmapsMode.CombinedDirectional;
            foreach (BlockSource source in sources)
            {
                if (ReferenceEquals(source.Data, replaced) || !TryGetBlockDirectionality(source.Data, out bool directional))
                {
                    continue;
                }
                if (directional != replacement_directional)
                {
                    return false;
                }
            }
            return true;
        }

        private static List<BlockSource> CollectLoadedBlockSources(
            Scene replacementScene = default,
            HikariLightmapData replaced = null,
            IEnumerable<Object> replacementTargets = null)
        {
            var sources = new List<BlockSource>();
            var managed_targets = replacementTargets != null
                ? new HashSet<Object>(replacementTargets)
                : new HashSet<Object>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }
                if (HikariLightmapper.ForeignBakerOwnsScene(scene) &&
                    !(scene == replacementScene && replaced != null))
                {
                    continue;
                }
                sources.AddRange(CollectComposedBlockSources(scene));
            }
            foreach (BlockSource source in sources)
            {
                AddBindingTargets(managed_targets, source.Data);
            }
            AddBindingTargets(managed_targets, replaced);

            LightmapData[] lightmaps = LightmapSettings.lightmaps;
            if (lightmaps == null || lightmaps.Length == 0)
            {
                return sources;
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }
                BlockSource current = CollectCurrentAssignments(scene, managed_targets, lightmaps);
                if (current.Owner != null)
                {
                    sources.Add(current);
                }
            }
            return sources;
        }

        private static void AddBindingTargets(HashSet<Object> targets, HikariLightmapData data)
        {
            if (data?.Renderers != null)
            {
                foreach (HikariLightmapData.RendererBinding binding in data.Renderers)
                {
                    if (binding.Renderer != null && HasUsableAtlas(data, binding.LightmapIndex))
                    {
                        targets.Add(binding.Renderer);
                    }
                }
            }
            if (data?.Terrains != null)
            {
                foreach (HikariLightmapData.TerrainBinding binding in data.Terrains)
                {
                    if (binding.Terrain != null && HasUsableAtlas(data, binding.LightmapIndex))
                    {
                        targets.Add(binding.Terrain);
                    }
                }
            }
        }

        private static BlockSource CollectCurrentAssignments(
            Scene scene, HashSet<Object> managedTargets, LightmapData[] lightmaps)
        {
            var atlases = new List<HikariLightmapData.Atlas>();
            var renderers = new List<HikariLightmapData.RendererBinding>();
            var terrains = new List<HikariLightmapData.TerrainBinding>();
            var indices = new Dictionary<int, int>();
            Object owner = null;

            int LocalIndex(Object target, int index)
            {
                if (managedTargets.Contains(target) || index < 0 || index >= lightmaps.Length ||
                    lightmaps[index]?.lightmapColor == null)
                {
                    return -1;
                }
                if (!indices.TryGetValue(index, out int local))
                {
                    local = atlases.Count;
                    indices[index] = local;
                    LightmapData atlas = lightmaps[index];
                    atlases.Add(new HikariLightmapData.Atlas
                    {
                        Color = atlas?.lightmapColor,
                        Direction = atlas?.lightmapDir,
                        ShadowMask = atlas?.shadowMask
                    });
                }
                owner = owner != null ? owner : target;
                return local;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    int local = LocalIndex(renderer, renderer.lightmapIndex);
                    if (local >= 0)
                    {
                        renderers.Add(new HikariLightmapData.RendererBinding
                        {
                            Renderer = renderer,
                            LightmapIndex = local,
                            ScaleOffset = renderer.lightmapScaleOffset
                        });
                    }
                }
                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    int local = LocalIndex(terrain, terrain.lightmapIndex);
                    if (local >= 0)
                    {
                        terrains.Add(new HikariLightmapData.TerrainBinding
                        {
                            Terrain = terrain,
                            LightmapIndex = local,
                            ScaleOffset = terrain.lightmapScaleOffset
                        });
                    }
                }
            }
            return new BlockSource
            {
                Owner = owner,
                Data = new HikariLightmapData
                {
                    Mode = LightmapSettings.lightmapsMode,
                    Atlases = atlases.ToArray(),
                    Renderers = renderers.ToArray(),
                    Terrains = terrains.ToArray()
                }
            };
        }

        private static List<BlockSource> CollectComposedBlockSources(Scene scene)
        {
            var blocks = new List<BlockSource>();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return blocks;
            }

            HikariLightmapper lightmapper = SoleEnabledLightmapper(scene);
            if (lightmapper != null)
            {
                blocks.Add(new BlockSource { Owner = lightmapper, Data = lightmapper.Lightmaps });
            }
            foreach (HikariPrefab prefab in HikariPrefab.FindAllInScene(scene))
            {
                if (prefab.enabled)
                {
                    blocks.Add(new BlockSource { Owner = prefab, Data = prefab.Lightmaps });
                }
            }
            foreach (HikariAtlasGroup atlas_group in HikariAtlasGroup.FindAllInScene(scene))
            {
                if (atlas_group.enabled)
                {
                    blocks.Add(new BlockSource { Owner = atlas_group, Data = atlas_group.Lightmaps });
                }
            }
            return blocks;
        }

        private static List<HikariLightmapData> CollectBlocks(List<BlockSource> sources)
        {
            var blocks = new List<HikariLightmapData>(sources.Count);
            foreach (BlockSource source in sources)
            {
                blocks.Add(source.Data);
            }
            return blocks;
        }

        private static List<Object> DirectionalModeConflicts(
            List<BlockSource> sources, HikariLightmapData ignored)
        {
            var conflicts = new List<Object>();
            var contributors = new List<BlockSource>();
            bool has_directional = false;
            bool has_non_directional = false;

            foreach (BlockSource source in sources)
            {
                if (ReferenceEquals(source.Data, ignored) || !HasUsableAtlas(source.Data))
                {
                    continue;
                }

                contributors.Add(source);
                if (!TryGetBlockDirectionality(source.Data, out bool directional) ||
                    source.Data.Mode != (directional
                        ? LightmapsMode.CombinedDirectional
                        : LightmapsMode.NonDirectional))
                {
                    conflicts.Add(source.Owner);
                    continue;
                }

                has_directional |= directional;
                has_non_directional |= !directional;
            }

            if (has_directional && has_non_directional)
            {
                foreach (BlockSource source in contributors)
                {
                    if (!conflicts.Contains(source.Owner))
                    {
                        conflicts.Add(source.Owner);
                    }
                }
            }
            return conflicts;
        }

        private static bool TryGetBlockDirectionality(HikariLightmapData data, out bool directional)
        {
            directional = false;
            if (data == null || data.Atlases == null || data.Atlases.Length == 0)
            {
                return false;
            }

            bool found = false;
            for (int i = 0; i < data.Atlases.Length; i++)
            {
                if (!HasUsableAtlas(data, i))
                {
                    continue;
                }
                bool current = data.Atlases[i].Direction != null;
                if (found && current != directional)
                {
                    return false;
                }
                directional = current;
                found = true;
            }
            return found;
        }

        private static bool HasUsableAtlas(HikariLightmapData data, int index)
        {
            return data?.Atlases != null && index >= 0 && index < data.Atlases.Length &&
                data.Atlases[index].Color != null;
        }

        private static bool HasUsableAtlas(HikariLightmapData data)
        {
            if (data?.Atlases != null)
            {
                for (int i = 0; i < data.Atlases.Length; i++)
                {
                    if (HasUsableAtlas(data, i))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void ApplyLightProbes(List<HikariLightmapData> blocks)
        {
            foreach (HikariLightmapData block in blocks)
            {
                if (block != null && block.LightProbes != null)
                {
                    LightmapSettings.lightProbes = block.LightProbes;
                    return;
                }
            }
        }

        private static void ApplyBlock(HikariLightmapData data, int offset)
        {
            if (data.Renderers != null)
            {
                foreach (HikariLightmapData.RendererBinding binding in data.Renderers)
                {
                    if (binding.Renderer != null && HasUsableAtlas(data, binding.LightmapIndex))
                    {
                        binding.Renderer.lightmapIndex = offset + binding.LightmapIndex;
                        binding.Renderer.lightmapScaleOffset = binding.ScaleOffset;
                    }
                }
            }
            if (data.Terrains != null)
            {
                foreach (HikariLightmapData.TerrainBinding binding in data.Terrains)
                {
                    if (binding.Terrain != null && HasUsableAtlas(data, binding.LightmapIndex))
                    {
                        binding.Terrain.lightmapIndex = offset + binding.LightmapIndex;
                        binding.Terrain.lightmapScaleOffset = binding.ScaleOffset;
                    }
                }
            }
            if (data.ReflectionProbes != null)
            {
                foreach (HikariLightmapData.ReflectionProbeBinding binding in data.ReflectionProbes)
                {
                    if (binding.Probe != null && binding.Texture != null)
                    {
                        binding.Probe.bakedTexture = binding.Texture;
                    }
                }
            }
            if (data.Lights != null)
            {
                foreach (HikariLightmapData.LightBinding binding in data.Lights)
                {
                    if (binding.Light != null)
                    {
                        binding.Light.bakingOutput = new LightBakingOutput
                        {
                            isBaked = binding.IsBaked,
                            lightmapBakeType = binding.BakeType,
                            mixedLightingMode = binding.MixedMode,
                            occlusionMaskChannel = binding.OcclusionMaskChannel,
                            probeOcclusionLightIndex = binding.ResolvedProbeOcclusionLightIndex
                        };
                    }
                }
            }
        }

        private static HikariLightmapper SoleEnabledLightmapper(Scene scene)
        {
            HikariLightmapper found = null;
            int count = 0;
            foreach (HikariLightmapper lightmapper in HikariLightmapper.FindAllInScene(scene))
            {
                if (lightmapper.enabled)
                {
                    found = lightmapper;
                    count++;
                }
            }
            return count == 1 ? found : null;
        }
    }
}
#endif
