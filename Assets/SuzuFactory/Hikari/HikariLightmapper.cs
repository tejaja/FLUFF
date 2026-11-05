#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace SuzuFactory.Hikari
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Hikari/Hikari Lightmapper")]
    [HelpURL("https://doc.suzufactory.com/Hikari/component-lightmapper")]
    public sealed class HikariLightmapper : HikariStorageBehaviour
    {
        public HikariLightmapData Lightmaps = new HikariLightmapData();
        public HikariSettings SharedSettings;
        public HikariBakeSettings Settings = new HikariBakeSettings();

        protected override void OnEnable()
        {
            if (gameObject.hideFlags != HideFlags.None)
            {
                gameObject.hideFlags = HideFlags.None;
            }
            base.OnEnable();
        }

        public bool ForeignBakerOwnsScene()
        {
            return ForeignBakerOwnsScene(gameObject.scene);
        }

        public static bool ForeignBakerOwnsScene(UnityEngine.SceneManagement.Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "!ftraceLightmaps")
                {
                    continue;
                }
                foreach (Component component in root.GetComponents<Component>())
                {
                    if (component == null || component.GetType().Name != "ftLightmapsStorage")
                    {
                        continue;
                    }
                    System.Reflection.FieldInfo field = component.GetType().GetField(
                        "maps",
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    if (field != null && field.GetValue(component) is System.Collections.IList maps && maps.Count > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool IsSoleInstanceInScene()
        {
            return CountInScene(gameObject.scene) <= 1;
        }

        public static HikariLightmapper FindInScene(UnityEngine.SceneManagement.Scene scene)
        {
            if (!scene.IsValid())
            {
                return null;
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<HikariLightmapper>(true);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        public static List<HikariLightmapper> FindAllInScene(UnityEngine.SceneManagement.Scene scene)
        {
            return HikariSceneComponents.FindAllInScene<HikariLightmapper>(scene);
        }

        public static int CountInScene(UnityEngine.SceneManagement.Scene scene)
        {
            return FindAllInScene(scene).Count;
        }
    }
}
#endif
