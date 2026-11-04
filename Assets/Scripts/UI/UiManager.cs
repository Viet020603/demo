using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Demo.Gameplay
{
    /// <summary>
    /// Owns the Start button and the grid popup. The popup is created lazily (GetPopupGrid), the same way the
    /// other popups are, under an EXISTING UI panel: the Canvas and Camera of the scene are never touched.
    /// </summary>
    public class UiManager : MonoBehaviour
    {
        public const string PopupGridResourcePath = "UI/Popup/Grid/PopupGrid";

        [Tooltip("Parent of every popup (like panelUiInGame). Empty = the first Canvas found in the scene.")]
        [SerializeField] private Transform panelUiInGame;
        [Tooltip("Empty = Resources/" + PopupGridResourcePath)]
        [SerializeField] private UiGridController popupGridPrefab;
        [SerializeField] private Button startButton;
        [SerializeField] private string startLabel = "START";

        private UiGridController popupGrid;

        private void Awake()
        {
            if (GameManager.Instance == null) new GameObject("GameManager").AddComponent<GameManager>();

            EnsureEventSystem();
            if (panelUiInGame == null) panelUiInGame = FindUiParent();
            if (startButton == null) startButton = CreateStartButton();
            startButton.onClick.AddListener(ShowPopupGrid);
        }

        private void OnDestroy()
        {
            if (startButton != null) startButton.onClick.RemoveListener(ShowPopupGrid);
            if (popupGrid != null) popupGrid.Dispose();
        }

        private UiGridController GetPopupGrid()
        {
            if (popupGrid == null)
            {
                var prefab = popupGridPrefab != null
                    ? popupGridPrefab
                    : Resources.Load<UiGridController>(PopupGridResourcePath);

                if (prefab != null)
                {
                    popupGrid = Instantiate(prefab, panelUiInGame, false);
                }
                else
                {
                    var go = new GameObject("PopupGrid", typeof(RectTransform));
                    go.transform.SetParent(panelUiInGame, false);
                    popupGrid = go.AddComponent<UiGridController>();
                }
                popupGrid.Init(this);
            }
            return popupGrid;
        }

        public void ShowPopupGrid()
        {
            if (startButton != null) startButton.gameObject.SetActive(false);
            GetPopupGrid().Show();
        }

        public void ClosePopupGrid() => GetPopupGrid().Close();

        public void OnGridClosed()
        {
            if (startButton != null) startButton.gameObject.SetActive(true);
        }

        private static Transform FindUiParent()
        {
            var existing = FindFirstObjectByType<Canvas>();
            if (existing != null) return existing.rootCanvas.transform;

            // Only when the scene has no Canvas at all.
            var go = new GameObject("UiCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return go.transform;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private Button CreateStartButton()
        {
            var buttonGo = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = buttonGo.GetComponent<RectTransform>();
            rt.SetParent(panelUiInGame, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(420f, 140f);
            buttonGo.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.25f, 1f);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(rt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            text.text = startLabel;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 56;
            text.color = Color.white;
            text.raycastTarget = false;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return buttonGo.GetComponent<Button>();
        }
    }
}
