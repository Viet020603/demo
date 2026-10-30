using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Demo.CameraControl;
using Demo.Grid;

namespace Demo.Gameplay
{
    /// <summary>
    /// uGUI version of the grid. Everything lives inside one Canvas:
    /// Canvas > Viewport (RectMask2D) > Content (map). Drag moves Content, wheel zooms it.
    /// Only the cells inside the viewport are active (pooled UI Images, reused while panning).
    /// Cells never receive raycasts: clicks are resolved with math, so any of the cells is clickable.
    /// </summary>
    public class UiGridController : MonoBehaviour
    {
        [Header("Map data (o_ban_do.json)")]
        [SerializeField] private TextAsset mapFile;
        [SerializeField] private string resourceName = "o_ban_do";

        [Header("Canvas")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private float cellSizePx = 64f;
        [Tooltip("Cell prefab (needs a Graphic such as Image). Empty = auto-created Image.")]
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private Color backgroundColor = new Color(0.12f, 0.2f, 0.12f, 1f);
        [SerializeField, Range(0.5f, 1f)] private float cellFill = 0.95f;

        [Header("Camera (drag / zoom)")]
        [SerializeField] private float dragThresholdPixels = 6f;
        [SerializeField] private float minZoom = 0.5f;
        [SerializeField] private float maxZoom = 2f;
        [SerializeField] private float startZoom = 1f;
        [SerializeField] private float zoomStep = 0.1f;
        [SerializeField] private int cullPadding = 1;

        [Header("Selection")]
        [SerializeField] private Color selectionColor = new Color(1f, 1f, 1f, 0.45f);

        [Header("Debug")]
        [SerializeField] private bool logClicks = true;

        [SerializeField] private Color[] typeColors = new Color[]
        {
            new Color(0.5f, 0.5f, 0.5f),
            new Color(1.0f, 0.2f, 0.2f),
            new Color(1.0f, 0.6f, 0.2f),
            new Color(1.0f, 0.9f, 0.3f),
            new Color(0.4f, 0.8f, 0.4f),
            new Color(0.3f, 0.6f, 1.0f),
            new Color(0.6f, 0.4f, 0.9f),
            new Color(0.9f, 0.4f, 0.7f),
            new Color(0.2f, 0.9f, 0.9f),
        };

        public event Action<int> CellIdClicked;

        private int _width = 31, _height = 31;
        private int[] _idByIndex;
        private byte[] _typeByIndex;

        private Canvas _canvas;
        private RectTransform _viewport;
        private RectTransform _content;
        private RectTransform _marker;

        private readonly Dictionary<int, CellView> _active = new Dictionary<int, CellView>();
        private readonly Stack<CellView> _free = new Stack<CellView>();
        private readonly List<int> _tmp = new List<int>();
        private RectInt _lastVisible = new RectInt(-1, -1, 0, 0);

        private float _zoom = 1f;
        private bool _pressed, _dragged;
        private Vector2 _pressScreen, _lastScreen;

        private void Awake()
        {
            DisableWorldSpaceVersion();
            LoadMap();
            BuildCanvas();
            _zoom = Mathf.Clamp(startZoom, minZoom, maxZoom);
            _content.localScale = Vector3.one * _zoom;
            CenterContent();
        }

        private void Start()
        {
            RefreshVisible(true);
        }

        // -------------------------------------------------------------- setup

        private static void DisableWorldSpaceVersion()
        {
            var old = FindFirstObjectByType<GridController>();
            if (old != null) old.enabled = false;
            var cam = Camera.main;
            if (cam != null)
            {
                var c = cam.GetComponent<CameraController2D>();
                if (c != null) c.enabled = false;
            }
        }

        private void LoadMap()
        {
            TextAsset asset = mapFile != null ? mapFile : Resources.Load<TextAsset>(resourceName);
            if (asset == null)
            {
                Debug.LogError($"[UiGridController] Map json not found. Put '{resourceName}.json' in Assets/Resources/ or assign it to 'Map File'.");
                _idByIndex = new int[_width * _height];
                _typeByIndex = new byte[_width * _height];
                return;
            }

            var cells = MapJsonLoader.Parse(asset.text);
            int maxX = 1, maxY = 1;
            foreach (var c in cells)
            {
                if (c.cx > maxX) maxX = c.cx;
                if (c.cy > maxY) maxY = c.cy;
            }
            _width = maxX;
            _height = maxY;
            _idByIndex = new int[_width * _height];
            _typeByIndex = new byte[_width * _height];

            foreach (var c in cells)
            {
                int x = c.cx - 1, y = c.cy - 1;   // json is 1-based
                if (x < 0 || y < 0 || x >= _width || y >= _height) continue;
                int idx = y * _width + x;
                _idByIndex[idx] = c.id;
                _typeByIndex[idx] = (byte)Mathf.Clamp(c.maLoai, 0, 255);
            }
            Debug.Log($"[UiGridController] Loaded {cells.Length} cells ({_width}x{_height}).");
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("GridCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            var vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            _viewport = vp.GetComponent<RectTransform>();
            _viewport.SetParent(canvasGo.transform, false);
            Stretch(_viewport);
            var bg = vp.GetComponent<Image>();
            bg.color = backgroundColor;
            bg.raycastTarget = false;

            var ct = new GameObject("Content", typeof(RectTransform));
            _content = ct.GetComponent<RectTransform>();
            _content.SetParent(_viewport, false);
            _content.anchorMin = _content.anchorMax = Vector2.zero;
            _content.pivot = Vector2.zero;
            _content.sizeDelta = new Vector2(_width * cellSizePx, _height * cellSizePx);

            var mk = new GameObject("SelectionMarker", typeof(RectTransform), typeof(Image));
            _marker = mk.GetComponent<RectTransform>();
            _marker.SetParent(_content, false);
            _marker.anchorMin = _marker.anchorMax = Vector2.zero;
            _marker.pivot = new Vector2(0.5f, 0.5f);
            _marker.sizeDelta = new Vector2(cellSizePx, cellSizePx);
            var mi = mk.GetComponent<Image>();
            mi.color = selectionColor;
            mi.raycastTarget = false;
            mk.SetActive(false);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // -------------------------------------------------------------- update

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                HandleDrag(mouse);
                HandleZoom(mouse);
            }

            ClampContent();
            RefreshVisible(false);
        }

        private void HandleDrag(Mouse mouse)
        {
            Vector2 screen = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
            {
                _pressed = true;
                _dragged = false;
                _pressScreen = _lastScreen = screen;
            }

            bool held = mouse.leftButton.isPressed || mouse.middleButton.isPressed;
            if (_pressed && held)
            {
                if (!_dragged && (screen - _pressScreen).sqrMagnitude > dragThresholdPixels * dragThresholdPixels)
                {
                    _dragged = true;
                    _lastScreen = screen;
                }
                if (_dragged)
                {
                    _content.anchoredPosition += (screen - _lastScreen) / _canvas.scaleFactor;
                    _lastScreen = screen;
                }
            }

            if (_pressed && !held)
            {
                if (!_dragged && mouse.leftButton.wasReleasedThisFrame)
                    TryClick(screen);
                _pressed = false;
            }
        }

        private void HandleZoom(Mouse mouse)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.001f) return;

            Vector2 screen = mouse.position.ReadValue();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, screen, null, out var before)) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screen, null, out var inViewport);

            _zoom = Mathf.Clamp(_zoom * (1f + Mathf.Sign(scroll) * zoomStep), minZoom, maxZoom);
            _content.localScale = Vector3.one * _zoom;

            // keep the point under the cursor fixed; viewport local space has its pivot at the center
            Vector2 vpFromBottomLeft = inViewport + _viewport.rect.size * 0.5f;
            _content.anchoredPosition = vpFromBottomLeft - before * _zoom;
        }

        private void ClampContent()
        {
            Vector2 view = _viewport.rect.size;
            Vector2 size = _content.sizeDelta * _zoom;
            Vector2 p = _content.anchoredPosition;
            p.x = size.x <= view.x ? (view.x - size.x) * 0.5f : Mathf.Clamp(p.x, view.x - size.x, 0f);
            p.y = size.y <= view.y ? (view.y - size.y) * 0.5f : Mathf.Clamp(p.y, view.y - size.y, 0f);
            _content.anchoredPosition = p;
        }

        private void CenterContent()
        {
            _content.anchoredPosition = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            Vector2 view = _viewport.rect.size;
            _content.anchoredPosition = (view - _content.sizeDelta * _zoom) * 0.5f;
        }

        // -------------------------------------------------------------- click

        private void TryClick(Vector2 screen)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, screen, null, out var local)) return;
            int x = Mathf.FloorToInt(local.x / cellSizePx);
            int y = Mathf.FloorToInt(local.y / cellSizePx);
            if (x < 0 || y < 0 || x >= _width || y >= _height) return;

            _marker.gameObject.SetActive(true);
            _marker.anchoredPosition = CellCenter(x, y);
            _marker.SetAsLastSibling();

            int idx = y * _width + x;
            int id = _idByIndex[idx];
            if (id == 0) return;
            if (logClicks)
                Debug.Log($"Clicked cell id = {id}  (cx={x + 1}, cy={y + 1}, maLoai={_typeByIndex[idx]})");
            CellIdClicked?.Invoke(id);
        }

        private Vector2 CellCenter(int x, int y) =>
            new Vector2((x + 0.5f) * cellSizePx, (y + 0.5f) * cellSizePx);

        // -------------------------------------------------------------- culling + pool

        private RectInt GetVisibleRect()
        {
            Vector2 view = _viewport.rect.size;
            Vector2 p = _content.anchoredPosition;
            float minX = -p.x / _zoom, minY = -p.y / _zoom;
            float maxX = (view.x - p.x) / _zoom, maxY = (view.y - p.y) / _zoom;

            int x0 = Mathf.Clamp(Mathf.FloorToInt(minX / cellSizePx) - cullPadding, 0, _width - 1);
            int x1 = Mathf.Clamp(Mathf.FloorToInt(maxX / cellSizePx) + cullPadding, 0, _width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(minY / cellSizePx) - cullPadding, 0, _height - 1);
            int y1 = Mathf.Clamp(Mathf.FloorToInt(maxY / cellSizePx) + cullPadding, 0, _height - 1);
            return new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        private void RefreshVisible(bool force)
        {
            RectInt r = GetVisibleRect();
            if (!force && r.Equals(_lastVisible)) return;
            _lastVisible = r;

            _tmp.Clear();
            foreach (var kv in _active)
            {
                int x = kv.Key % _width, y = kv.Key / _width;
                if (x < r.xMin || x >= r.xMax || y < r.yMin || y >= r.yMax) _tmp.Add(kv.Key);
            }
            for (int i = 0; i < _tmp.Count; i++)
            {
                var view = _active[_tmp[i]];
                view.gameObject.SetActive(false);
                _free.Push(view);
                _active.Remove(_tmp[i]);
            }

            for (int y = r.yMin; y < r.yMax; y++)
            {
                for (int x = r.xMin; x < r.xMax; x++)
                {
                    int idx = y * _width + x;
                    if (_idByIndex[idx] != 0 && !_active.ContainsKey(idx)) ShowCell(idx, x, y);
                }
            }
        }

        private void ShowCell(int idx, int x, int y)
        {
            CellView view = _free.Count > 0 ? _free.Pop() : CreateCell();
            var rt = (RectTransform)view.transform;
            rt.anchoredPosition = CellCenter(x, y);
            rt.sizeDelta = new Vector2(cellSizePx * cellFill, cellSizePx * cellFill);
            view.gameObject.SetActive(true);

            int maLoai = _typeByIndex[idx];
            view.Bind(_idByIndex[idx], maLoai, x + 1, y + 1, ColorFor(maLoai));
            _active[idx] = view;
            _marker.SetAsLastSibling();
        }

        private CellView CreateCell()
        {
            GameObject go;
            if (cellPrefab != null)
                go = Instantiate(cellPrefab, _content, false);
            else
                go = new GameObject("Cell", typeof(RectTransform), typeof(Image));

            go.transform.SetParent(_content, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            foreach (var g in go.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

            var view = go.GetComponent<CellView>();
            if (view == null) view = go.AddComponent<CellView>();
            return view;
        }

        private Color ColorFor(int maLoai)
        {
            if (typeColors == null || typeColors.Length == 0) return Color.white;
            return typeColors[Mathf.Clamp(maLoai, 0, typeColors.Length - 1)];
        }
    }
}
