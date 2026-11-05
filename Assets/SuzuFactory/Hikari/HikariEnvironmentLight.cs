#if UNITY_EDITOR
using UnityEngine;

namespace SuzuFactory.Hikari
{
    public enum HikariEnvironmentLightSource : uint
    {
        Skybox = 0,
        Cubemap = 1
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hikari/Hikari Environment Light")]
    [HelpURL("https://doc.suzufactory.com/Hikari/component-environment-light")]
    public sealed class HikariEnvironmentLight : MonoBehaviour
    {
        [Tooltip("What to use as the environment light: the scene skybox settings or a cubemap texture.")]
        public HikariEnvironmentLightSource Source = HikariEnvironmentLightSource.Skybox;

        [Tooltip("The cubemap texture used when Source is Cubemap.")]
        public Cubemap Cubemap;

        [Min(0.0f)]
        [Tooltip("Multiplier applied to the environment light.")]
        public float Intensity = 1.0f;

        private void OnEnable()
        {
        }
    }
}
#endif
