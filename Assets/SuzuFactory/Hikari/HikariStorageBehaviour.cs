#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SuzuFactory.Hikari
{
    public abstract class HikariStorageBehaviour : MonoBehaviour
    {
        protected virtual void OnEnable()
        {
            Apply();
            EditorApplication.delayCall -= ReapplyIfEnabled;
            EditorApplication.delayCall += ReapplyIfEnabled;
        }

        private void ReapplyIfEnabled()
        {
            if (this != null && isActiveAndEnabled)
            {
                Apply();
            }
        }

        protected virtual void Start()
        {
            Apply();
        }

        public virtual void Apply()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }
            HikariLightmapComposition.Compose(gameObject.scene);
        }
    }
}
#endif
