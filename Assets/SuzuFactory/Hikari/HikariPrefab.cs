#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace SuzuFactory.Hikari
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Hikari/Hikari Prefab")]
    [HelpURL("https://doc.suzufactory.com/Hikari/component-prefab")]
    public sealed class HikariPrefab : HikariStorageBehaviour
    {
        public bool ApplyAutomatically = true;
        public HikariLightmapData Lightmaps = new HikariLightmapData();

        public static List<HikariPrefab> FindAllInScene(UnityEngine.SceneManagement.Scene scene)
        {
            return HikariSceneComponents.FindAllInScene<HikariPrefab>(scene);
        }
    }
}
#endif
