using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRCLightVolumes;
using VRCLightVolumes.Editor;

namespace SuzuFactory.Hikari.Editor
{
    [InitializeOnLoad]
    internal static class HikariLightVolumeBaker
    {
        static HikariLightVolumeBaker()
        {
            HikariLightVolumeHooks.CollectVolumeProbes = CollectVolumeProbes;
            HikariLightVolumeHooks.Apply = Apply;
        }

        internal static void CollectVolumeProbes(HikariSceneExportResult result)
        {
            foreach (LightVolumeManager manager in FindManagers())
            {
                int volume_count = manager.Editor.GetCustomProbesCount();
                List<LightVolumeInstance> volumes = FindCustomProbeVolumes(manager);
                for (int id = 0; id < volume_count; id++)
                {
                    Vector3[] positions = manager.Editor.GetCustomProbes(id);
                    if (positions == null || positions.Length == 0)
                    {
                        result.Warnings.Add(
                            $"Hikari: light volume {id} of '{manager.gameObject.name}' has no voxels and was skipped.");
                        continue;
                    }

                    var range = new HikariLightVolumeProbeRange
                    {
                        Volume = manager,
                        CustomProbeId = id,
                        ProbeOffset = result.Input.LightProbePositions.Count / 3,
                        ProbeCount = positions.Length,
                    };
                    if (manager.DilateInvalidProbes)
                    {
                        Vector3Int? resolution = id < volumes.Count ? volumes[id].Resolution : (Vector3Int?)null;
                        if (resolution.HasValue &&
                            resolution.Value.x * resolution.Value.y * resolution.Value.z == positions.Length)
                        {
                            result.Input.LightProbeGrids.Add(new HikariLightProbeGridInput
                            {
                                FirstProbe = (uint)range.ProbeOffset,
                                SizeX = (uint)resolution.Value.x,
                                SizeY = (uint)resolution.Value.y,
                                SizeZ = (uint)resolution.Value.z,
                                DilationIterations = (uint)Mathf.Max(0, manager.DilationIterations),
                                DilationBackfaceBias = manager.DilationBackfaceBias,
                            });
                        }
                        else
                        {
                            result.Warnings.Add(
                                $"Hikari: could not resolve the voxel grid of light volume {id} of " +
                                $"'{manager.gameObject.name}'; its invalid probes will not be dilated.");
                        }
                    }
                    foreach (Vector3 position in positions)
                    {
                        result.Input.LightProbePositions.Add(position.x);
                        result.Input.LightProbePositions.Add(position.y);
                        result.Input.LightProbePositions.Add(position.z);
                        result.Input.LightProbeAtlasGroups.Add(HikariBakeInput.NoAtlasGroup);
                    }
                    result.LightVolumeProbeRanges.Add(range);
                }
            }
            if (result.LightVolumeProbeRanges.Count > 0)
            {
                HikariBakeDebugLog.Write(
                    $"Collected {result.LightVolumeProbeRanges.Count} VRC Light Volume(s) for probe baking " +
                    $"({result.Input.LightProbeGrids.Count} dilation grid(s))");
            }
        }

        internal static void Apply(HikariBakeOutput output, HikariSceneExportResult exportResult, Action<double, string> report)
        {
            HikariLightVolumeProbeApplication.Apply<LightVolumeManager>(output, exportResult,
                (manager, id, l0, l1r, l1g, l1b) => manager.Editor.SetCustomProbesBaked(id, l0, l1r, l1g, l1b),
                report);
        }

        private static List<LightVolumeInstance> FindCustomProbeVolumes(LightVolumeManager manager)
        {
            var volumes = new List<LightVolumeInstance>();
            foreach (LightVolumeInstance volume in manager.LightVolumeInstances)
            {
                if (volume != null && volume.Bake && volume.gameObject.activeInHierarchy &&
                    !volume.CompareTag("EditorOnly"))
                {
                    volumes.Add(volume);
                }
            }
            return volumes;
        }

        private static List<LightVolumeManager> FindManagers()
        {
            return UnityEngine.Object
                .FindObjectsByType<LightVolumeManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .OrderBy(
                    manager => HikariEditorPaths.SceneHierarchyPath(manager.transform, ':'),
                    StringComparer.Ordinal)
                .ToList();
        }
    }
}
