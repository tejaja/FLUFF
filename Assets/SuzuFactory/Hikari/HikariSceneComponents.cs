#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SuzuFactory.Hikari
{
    public static class HikariSceneComponents
    {
        public static List<T> FindAllInScene<T>(Scene scene) where T : Component
        {
            var result = new List<T>();
            if (!scene.IsValid())
            {
                return result;
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                result.AddRange(root.GetComponentsInChildren<T>(true));
            }
            return result;
        }

        public static IEnumerable<GameObject> EnumerateLoadedSceneRoots()
        {
            for (int scene_index = 0; scene_index < SceneManager.sceneCount; scene_index++)
            {
                Scene scene = SceneManager.GetSceneAt(scene_index);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    yield return root;
                }
            }
        }
    }
}
#endif
