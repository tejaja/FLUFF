#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace SuzuFactory.Hikari
{
    public enum HikariAtlasGroupScope : uint
    {
        Children = 0,
        Objects = 1
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Hikari/Hikari Atlas Group")]
    [HelpURL("https://doc.suzufactory.com/Hikari/component-atlas-group")]
    public sealed class HikariAtlasGroup : HikariStorageBehaviour
    {
        [Tooltip("When off, this Atlas Group is never baked: its Bake button is disabled and the Hikari Lightmapper / Hikari Window bakes skip its objects.")]
        public bool BakeEnabled = true;

        [Tooltip("Children: this object's hierarchy is packed into its own set of lightmaps. Objects: the listed objects (and their children) are packed instead.")]
        public HikariAtlasGroupScope Scope = HikariAtlasGroupScope.Children;

        [Tooltip("Objects packed together into their own set of lightmaps, separate from other objects. Children of a listed object are included.")]
        public List<GameObject> Objects = new List<GameObject>();

        public HikariSettings OverrideSettings;
        public HikariLightmapData Lightmaps = new HikariLightmapData();

        public IEnumerable<GameObject> TargetObjects()
        {
            if (Scope == HikariAtlasGroupScope.Children)
            {
                yield return gameObject;
                yield break;
            }
            foreach (GameObject listed in Objects)
            {
                yield return listed;
            }
        }

        public static List<HikariAtlasGroup> FindAllInScene(UnityEngine.SceneManagement.Scene scene)
        {
            return HikariSceneComponents.FindAllInScene<HikariAtlasGroup>(scene);
        }
    }
}
#endif
