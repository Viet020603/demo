using UnityEngine;
using Demo.Gameplay;

namespace Demo.Bootstrap
{
    public class AutoBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (Object.FindFirstObjectByType<UiGridController>() != null) return;

            var oldGrid = Object.FindFirstObjectByType<GridController>();
            if (oldGrid != null) oldGrid.enabled = false;

            var go = new GameObject("GameBootstrap");
            go.AddComponent<UiGridController>();
        }
    }
}
