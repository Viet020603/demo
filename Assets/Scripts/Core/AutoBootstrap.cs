using UnityEngine;
using Demo.Gameplay;

namespace Demo.Bootstrap
{
    public class AutoBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (Object.FindFirstObjectByType<GridController>() != null) return;

            var go = new GameObject("GameBootstrap");
            go.AddComponent<GridController>();
        }
    }
}
