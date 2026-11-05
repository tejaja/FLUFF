#if UNITY_EDITOR
using UnityEngine;

namespace SuzuFactory.Hikari
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hikari/Hikari Settings")]
    [HelpURL("https://doc.suzufactory.com/Hikari/component-settings")]
    public sealed class HikariSettings : MonoBehaviour
    {
        public HikariBakeSettings Settings = new HikariBakeSettings();
    }
}
#endif
