using Demo.Grid;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Demo.Gameplay
{
    /// <summary>
    /// uGUI version of the grid. The Canvas is a standalone scene root:
    /// Canvas > Viewport (RectMask2D) > Content (map). Drag moves Content, wheel zooms it.
    /// Only the cells inside the viewport are active (pooled UI Images, reused while panning).
    /// Cells never receive raycasts: clicks are resolved with math, so any of the cells is clickable.
    /// </summary>
    public class UiGridController : PopupBase
    {
        [Header("Map data (o_ban_do.json)")]
        [SerializeField] private TextAsset mapFile;
        [SerializeField] private string resourceName = "o_ban_do";

        [Header("Canvas")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Camera canvasCamera;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform selectionMarker;
        [SerializeField] private float canvasPlaneDistance = 1f;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private float cellSizePx = 64f;
        [Tooltip("Cell prefab (needs a Graphic such as Image). Empty = auto-created Image.")]
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private Color backgroundColor = new Color(0.12f, 0.2f, 0.12f, 1f);
        [SerializeField, Range(0.5f, 1f)] private float cellFill = 0.95f;

        [Header("Map background (pans and zooms with the grid)")]
        [Tooltip("Empty = loads Resources/Sprite/dat.")]
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Image mapBackground;
        [SerializeField] private string backgroundResource = "Sprite/dat";

        [Header("Map layout (background is larger than the grid, like the reference)")]
        [Tooltip("Empty border around the grid, in cells. The background and frame cover grid + border.")]
        [SerializeField, Min(0)] private float borderCells = 4f;
        [Tooltip("Optional decorative frame (trees, clouds, water) with a transparent center. Drawn over the ground, under the grid.")]
        [SerializeField] private Image mapFrame;
        [SerializeField] private GridLinesGraphic gridLines;
        [SerializeField] private Color gridLineColor = new Color(0.1f, 0.3f, 0.05f, 0.75f);
        [SerializeField] private float gridLineThickness = 1.5f;
        [Tooltip("Opacity of the per-type cell tint. 0 = cells are transparent (only icons / selection show).")]
        [SerializeField, Range(0f, 1f)] private float cellTintAlpha = 0f;
        [SerializeField] private bool fitMapOnStart = true;

        [Header("Camera (drag / zoom)")]
        [SerializeField] private float dragThresholdPixels = 6f;
        [SerializeField] private float minZoom = 0.2f;
        [SerializeField] private float maxZoom = 2f;
        [SerializeField] private float startZoom = 1f;
        [SerializeField] private float zoomStep = 0.1f;
        [SerializeField] private int cullPadding = 1;

        [Header("Selection")]
        [SerializeField] private Color selectionColor = new Color(0.85f, 0.42f, 0.12f, 0.9f);

        [Header("Debug")]
        [SerializeField] private bool logClicks = true;

        [Header("Auto zoom on start")]
        [SerializeField] private bool autoZoomToCenterOnStart = true;
        [SerializeField, Min(0f)] private float autoZoomDelay = 2f;
        [SerializeField] private float autoZoomScale = 1.3f;
        [SerializeField, Min(0.01f)] private float autoZoomDuration = 0.6f;

        private Coroutine _autoZoomRoutine;
        [SerializeField]
        private Color[] typeColors = new Color[]
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

        private readonly Dictionary<int, CellView> _active = new Dictionary<int, CellView>();
        private readonly Stack<CellView> _free = new Stack<CellView>();
        private readonly List<int> _tmp = new List<int>();
        private RectInt _lastVisible = new RectInt(-1, -1, 0, 0);

        private float _zoom = 1f;
        private bool _pressed, _dragged;
        private Vector2 _pressScreen, _lastScreen;

        private bool _initialized, _needFit;

        public override void Init(UiManager _uiManager)
        {
            uiManager = _uiManager;
            if (_initialized) return;
            _initialized = true;

            LoadMap();
            BuildCanvas();
            _needFit = true;
        }

        public override void Show()
        {
            gameObject.SetActive(true);
            if (canvas != null) canvas.gameObject.SetActive(true);
            if (!_initialized) return;

            if (_needFit)
            {
                _needFit = false;
                Canvas.ForceUpdateCanvases();
                _zoom = Mathf.Clamp(fitMapOnStart ? FitZoom() : startZoom, minZoom, maxZoom);
                content.localScale = Vector3.one * _zoom;
                CenterContent();
            }
            RefreshVisible(true);

            if (autoZoomToCenterOnStart)
                _autoZoomRoutine = StartCoroutine(AutoZoomToCenterRoutine());
        }

        public override void Close()
        {
            _pressed = false;
            _dragged = false;
            gameObject.SetActive(false);
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        public override void Dispose()
        {
            foreach (var cell in _active.Values) if (cell != null) Destroy(cell.gameObject);
            _active.Clear();
            while (_free.Count > 0)
            {
                var cell = _free.Pop();
                if (cell != null) Destroy(cell.gameObject);
            }
            if (canvas != null && canvas.gameObject != gameObject) Destroy(canvas.gameObject);
            _initialized = false;
        }

        // -------------------------------------------------------------- setup
        private void OnDisable()
        {
            if (_autoZoomRoutine != null)
            {
                StopCoroutine(_autoZoomRoutine);
                _autoZoomRoutine = null;
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
            if (canvas == null)
            {
                var canvasGo = new GameObject("GridCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                canvas = canvasGo.GetComponent<Canvas>();
            }

            canvas.transform.SetParent(null, true);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            if (canvasCamera == null)
            {
                canvasCamera = Camera.main;
                if (canvasCamera == null)
                {
                    var cameraObject = new GameObject("Main Camera");
                    cameraObject.tag = "MainCamera";
                    canvasCamera = cameraObject.AddComponent<Camera>();
                    canvasCamera.orthographic = true;
                }
            }
            canvas.worldCamera = canvasCamera;
            canvas.planeDistance = Mathf.Max(canvasPlaneDistance, canvasCamera.nearClipPlane + 0.01f);

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            if (viewport == null)
            {
                var vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
                viewport = vp.GetComponent<RectTransform>();
            }
            viewport.SetParent(canvas.transform, false);
            Stretch(viewport);
            var bg = viewport.GetComponent<Image>();
            if (bg == null) bg = viewport.gameObject.AddComponent<Image>();
            bg.color = backgroundColor;
            bg.raycastTarget = false;

            if (content == null)
            {
                var ct = new GameObject("Content", typeof(RectTransform));
                content = ct.GetComponent<RectTransform>();
            }
            content.SetParent(viewport, false);
            content.anchorMin = content.anchorMax = Vector2.zero;
            content.pivot = Vector2.zero;
            content.sizeDelta = new Vector2(
                (_width + borderCells * 2f) * cellSizePx,
                (_height + borderCells * 2f) * cellSizePx);

            SetupBackground();
            SetupFrame();
            SetupGridLines();

            if (selectionMarker == null)
            {
                var mk = new GameObject("SelectionMarker", typeof(RectTransform), typeof(Image));
                selectionMarker = mk.GetComponent<RectTransform>();
            }
            selectionMarker.SetParent(content, false);
            selectionMarker.anchorMin = selectionMarker.anchorMax = Vector2.zero;
            selectionMarker.pivot = new Vector2(0.5f, 0.5f);
            selectionMarker.sizeDelta = new Vector2(cellSizePx, cellSizePx);
            var mi = selectionMarker.GetComponent<Image>();
            if (mi == null) mi = selectionMarker.gameObject.AddComponent<Image>();
            mi.color = selectionColor;
            mi.raycastTarget = false;
            selectionMarker.gameObject.SetActive(false);
        }

        private void SetupBackground()
        {
            if (backgroundSprite == null && !string.IsNullOrEmpty(backgroundResource))
                backgroundSprite = Resources.Load<Sprite>(backgroundResource);

            if (mapBackground == null)
            {
                if (backgroundSprite == null) return;
                var go = new GameObject("MapBackground", typeof(RectTransform), typeof(Image));
                mapBackground = go.GetComponent<Image>();
            }

            var rt = mapBackground.rectTransform;
            rt.SetParent(content, false);
            Stretch(rt);
            rt.SetAsFirstSibling();
            if (backgroundSprite != null) mapBackground.sprite = backgroundSprite;
            mapBackground.type = Image.Type.Simple;
            mapBackground.preserveAspect = false;
            mapBackground.raycastTarget = false;
        }

        private void SetupFrame()
        {
            if (mapFrame == null) return;
            var rt = mapFrame.rectTransform;
            rt.SetParent(content, false);
            Stretch(rt);
            rt.SetSiblingIndex(1);
            mapFrame.raycastTarget = false;
            mapFrame.enabled = mapFrame.sprite != null;
        }

        private void SetupGridLines()
        {
            if (gridLines == null)
            {
                var go = new GameObject("GridLines", typeof(RectTransform), typeof(CanvasRenderer), typeof(GridLinesGraphic));
                gridLines = go.GetComponent<GridLinesGraphic>();
            }

            var rt = gridLines.rectTransform;
            rt.SetParent(content, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = GridOrigin;
            rt.sizeDelta = new Vector2(_width * cellSizePx, _height * cellSizePx);
            rt.SetSiblingIndex(mapFrame != null ? 2 : 1);

            gridLines.color = gridLineColor;
            gridLines.raycastTarget = false;
            gridLines.Configure(_width, _height, gridLineThickness);
        }

        private Vector2 GridOrigin => new Vector2(borderCells * cellSizePx, borderCells * cellSizePx);

        private float FitZoom()
        {
            Vector2 view = viewport.rect.size;
            Vector2 size = content.sizeDelta;
            if (size.x <= 0f || size.y <= 0f) return startZoom;
            return Mathf.Min(view.x / size.x, view.y / size.y);
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
            if (!_initialized) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                uiManager?.OnGridClosed();
                return;
            }

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
                    content.anchoredPosition += (screen - _lastScreen) / canvas.scaleFactor;
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
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screen, canvasCamera, out var before)) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screen, canvasCamera, out var inViewport);

            _zoom = Mathf.Clamp(_zoom * (1f + Mathf.Sign(scroll) * zoomStep), minZoom, maxZoom);
            content.localScale = Vector3.one * _zoom;

            // keep the point under the cursor fixed; viewport local space has its pivot at the center
            Vector2 vpFromBottomLeft = inViewport + viewport.rect.size * 0.5f;
            content.anchoredPosition = vpFromBottomLeft - before * _zoom;
        }

        private void ClampContent()
        {
            Vector2 view = viewport.rect.size;
            Vector2 size = content.sizeDelta * _zoom;
            Vector2 p = content.anchoredPosition;
            p.x = size.x <= view.x ? (view.x - size.x) * 0.5f : Mathf.Clamp(p.x, view.x - size.x, 0f);
            p.y = size.y <= view.y ? (view.y - size.y) * 0.5f : Mathf.Clamp(p.y, view.y - size.y, 0f);
            content.anchoredPosition = p;
        }

        private void CenterContent()
        {
            //content.anchoredPosition = Vector2.zero;
            //Canvas.ForceUpdateCanvases();
            //Vector2 view = viewport.rect.size;
            //content.anchoredPosition = (view - content.sizeDelta * _zoom) * 0.5f;

            content.anchoredPosition = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            content.anchoredPosition = CenteredPositionForZoom(_zoom);
        }

        // -------------------------------------------------------------- click

        private void TryClick(Vector2 screen)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screen, canvasCamera, out var local)) return;
            local -= GridOrigin;
            int x = Mathf.FloorToInt(local.x / cellSizePx);
            int y = Mathf.FloorToInt(local.y / cellSizePx);
            if (x < 0 || y < 0 || x >= _width || y >= _height) return;

            selectionMarker.gameObject.SetActive(true);
            selectionMarker.anchoredPosition = CellCenter(x, y);
            selectionMarker.SetAsLastSibling();

            int idx = y * _width + x;
            int id = _idByIndex[idx];
            if (id == 0) return;
            if (logClicks)
                Debug.Log($"Clicked cell id = {id}  (cx={x + 1}, cy={y + 1}, maLoai={_typeByIndex[idx]})");
            CellIdClicked?.Invoke(id);
        }

        private Vector2 CellCenter(int x, int y) =>
            GridOrigin + new Vector2((x + 0.5f) * cellSizePx, (y + 0.5f) * cellSizePx);

        // -------------------------------------------------------------- culling + pool

        private RectInt GetVisibleRect()
        {
            Vector2 view = viewport.rect.size;
            Vector2 p = content.anchoredPosition;
            Vector2 o = GridOrigin;
            float minX = -p.x / _zoom - o.x, minY = -p.y / _zoom - o.y;
            float maxX = (view.x - p.x) / _zoom - o.x, maxY = (view.y - p.y) / _zoom - o.y;

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
            Color tint = ColorFor(maLoai);
            tint.a = cellTintAlpha;
            view.Bind(_idByIndex[idx], maLoai, x + 1, y + 1, tint);
            _active[idx] = view;
            selectionMarker.SetAsLastSibling();
        }

        private CellView CreateCell()
        {
            GameObject go;
            if (cellPrefab != null)
                go = Instantiate(cellPrefab, content, false);
            else
                go = new GameObject("Cell", typeof(RectTransform), typeof(Image));

            go.transform.SetParent(content, false);
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

        private IEnumerator AutoZoomToCenterRoutine()
        {
            if (autoZoomDelay > 0f)
                yield return new WaitForSeconds(autoZoomDelay);

            float startScale = _zoom;
            float targetScale = Mathf.Clamp(autoZoomScale, minZoom, maxZoom);

            Vector2 startPos = content.anchoredPosition;
            Vector2 targetPos = CenteredPositionForZoom(targetScale);

            float elapsed = 0f;
            while (elapsed < autoZoomDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / autoZoomDuration);
                float k = Mathf.SmoothStep(0f, 1f, t);

                _zoom = Mathf.Lerp(startScale, targetScale, k);
                content.localScale = Vector3.one * _zoom;
                content.anchoredPosition = Vector2.Lerp(startPos, targetPos, k);

                RefreshVisible(false);
                yield return null;
            }

            _zoom = targetScale;
            content.localScale = Vector3.one * _zoom;
            content.anchoredPosition = targetPos;
            ClampContent();
            RefreshVisible(false);
        }

        private Vector2 CenteredPositionForZoom(float zoom)
        {
            Vector2 view = viewport.rect.size;
            return (view - content.sizeDelta * zoom) * 0.5f;
        }

    }
}

   

