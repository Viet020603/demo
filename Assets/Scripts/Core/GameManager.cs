using UnityEngine;

namespace Demo.Gameplay
{
    /// <summary>Tracks how many popups are open.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public int OpenPopupCount { get; private set; }
        public bool IsPopupOpen => OpenPopupCount > 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Call_Plus_OpenPopupUI() => OpenPopupCount++;

        public void Call_Sub_IsOpenPopupUI() => OpenPopupCount = Mathf.Max(0, OpenPopupCount - 1);
    }
}
