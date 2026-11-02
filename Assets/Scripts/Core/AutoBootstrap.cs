using UnityEngine;
using Demo.Gameplay;

namespace Demo.Bootstrap
{
    public class AutoBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (Object.FindFirstObjectByType<UiManager>() != null) return;

            new GameObject("UiManager").AddComponent<UiManager>();
        }
    }
}
