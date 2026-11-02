using UnityEngine;

namespace Demo.Gameplay
{
    public abstract class PopupBase : MonoBehaviour
    {
        protected UiManager uiManager;

        protected virtual void OnEnable()
        {
            transform.SetAsLastSibling();

            GameManager.Instance?.Call_Plus_OpenPopupUI();
        }

        protected virtual void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.Call_Sub_IsOpenPopupUI();
            }
        }

        public abstract void Init(UiManager _uiManager);

        public abstract void Dispose();
        public abstract void Show();

        public abstract void Close();
    }
}
