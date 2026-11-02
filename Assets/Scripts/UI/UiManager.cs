using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Demo.Gameplay
{
    /// <summary>
    /// Owns the Start button and the grid popup. The grid is only built and shown after the button is pressed.
    /// Assign 'Grid Popup' / 'Start Button' in the inspector, or leave them empty to create them at runtime.
    /// </summary>
    public class UiManager : MonoBehaviour
    {
        [SerializeField] private UiGridController gridPopup;
        [SerializeField] private Button startButton;
        [SerializeField] private string startLabel = "START";

        private void Awake()
        {
            if (GameManager.Instance == null) new GameObject("GameManager").AddComponent<GameManager>();

            if (gridPopup == null)
                gridPopup = FindFirstObjectByType<UiGridController>(FindObjectsInactive.Include);
            if (gridPopup == null)
            {
                var go = new GameObject("UiGridController");
                go.SetActive(false); // keep Awake/OnEnable from running before Init
                gridPopup = go.AddComponent<UiGridController>();
            }

            gridPopup.Init(this);
            gridPopup.Close();

            EnsureEventSystem();
            if (startButton == null) startButton = CreateStartButton();
            startButton.onClick.AddListener(OnStartClicked);
        }

        private void OnDestroy()
        {
            if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
            if (gridPopup != null) gridPopup.Dispose();
        }

        public void OnStartClicked()
        {
            startButton.gameObject.SetActive(false);
            gridPopup.Show();
        }

        public void OnGridClosed()
        {
            if (startButton != null) startButton.gameObject.SetActive(true);
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private Button CreateStartButton()
        {
            var canvasGo = new GameObject("StartCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var buttonGo = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = buttonGo.GetComponent<RectTransform>();
            rt.SetParent(canvasGo.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
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
