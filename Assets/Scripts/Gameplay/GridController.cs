using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Demo.CameraControl;
using Demo.Grid;
using Demo.Rendering;
using Demo.InputSystem;
using Demo.SaveLoad;

namespace Demo.Gameplay
{
    public class GridController : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] private int width = 31;
        [SerializeField] private int height = 31;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector2 origin = Vector2.zero;

        [Header("Initial content")]
        [SerializeField, Range(0f, 1f)] private float initialFillRate = 0.3f;
        [SerializeField] private int randomSeed = 12345;

        [Header("Refs")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private GridLineRenderer gridRenderer;
        [SerializeField] private GameObject placePrefab;

        [Header("Visual")]
        [SerializeField] private Transform objectRoot;
        [SerializeField] private Color selectionColor = new Color(0.2f, 0.7f, 1f, 0.35f);

        [Header("Culling")]
        [Tooltip("Extra cells around the camera view that are kept alive to avoid pop-in.")]
        [SerializeField] private int cullPadding = 1;
        [SerializeField] private float startOrthoSize = 6f;

        [Header("Pool Strategy")]
        [Tooltip("If true, pool size is fixed to camera-visible cell capacity and runtime instantiate is disabled.")]
        [SerializeField] private bool fixedPoolByCameraView = true;

        [Header("Debug Gizmos")]
        [SerializeField] private bool drawCameraGizmos = true;
        [SerializeField] private Color cameraViewColor = new Color(0f, 1f, 1f, 0.9f);
        [SerializeField] private Color cullViewColor = new Color(1f, 0.85f, 0f, 0.9f);
        [SerializeField] private Color mapBoundsColor = new Color(1f, 0f, 0f, 0.9f);

        public event Action<Vector2Int> CellClicked;

        private GridData _data;
        private GridMath _math;
        private GridInputReader _input;
        private SimplePool _pool;
        private CameraController2D _camController;
        private GameObject _selectionMarker;

        private readonly Dictionary<int, GameObject> _active = new();
        private readonly List<int> _tmpRelease = new();
        private RectInt _lastVisible = new RectInt(-1, -1, 0, 0);

        private void Awake()
        {
            BootstrapIfNeeded();

            _data = new GridData(width, height);
            _math = new GridMath(origin, cellSize);
            _input = new GridInputReader(mainCamera, _math, width, height);

            SeedInitialContent();

            if (gridRenderer != null)
                gridRenderer.Configure(width, height, cellSize, origin);

            if (objectRoot == null)
                objectRoot = new GameObject("Objects").transform;

            var poolObj = new GameObject("ObjectPool");
            poolObj.transform.SetParent(objectRoot);
            _pool = poolObj.AddComponent<SimplePool>();
            _pool.SetPrefab(placePrefab != null ? placePrefab : CreateFallbackPrefab());

            _selectionMarker = CreateSelectionMarker();

            var worldBounds = GetMapWorldBounds();
            _camController.SetBounds(worldBounds);
            mainCamera.transform.position = new Vector3(worldBounds.center.x, worldBounds.center.y, -10f);
            _camController.SetZoom(startOrthoSize);

            ConfigurePoolCapacityToCamera();
        }

        private void Start()
        {
            RefreshVisible(true);
        }

        private void ConfigurePoolCapacityToCamera()
        {
            if (!fixedPoolByCameraView) return;

            int capacity = ComputeVisibleCapacity();
            // Set internal fields via config methods
            _pool.ConfigurePrewarm(capacity);
            _pool.PrewarmNow();
        }

        private int ComputeVisibleCapacity()
        {
            float halfH = mainCamera.orthographicSize;
            float halfW = halfH * mainCamera.aspect;

            float viewW = halfW * 2f;
            float viewH = halfH * 2f;

            int visibleCols = Mathf.CeilToInt(viewW / cellSize) + cullPadding * 2 + 2;
            int visibleRows = Mathf.CeilToInt(viewH / cellSize) + cullPadding * 2 + 2;

            visibleCols = Mathf.Clamp(visibleCols, 1, width);
            visibleRows = Mathf.Clamp(visibleRows, 1, height);

            return visibleCols * visibleRows;
        }

        private void BootstrapIfNeeded()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    var camObj = new GameObject("Main Camera");
                    mainCamera = camObj.AddComponent<Camera>();
                    camObj.tag = "MainCamera";
                }
            }

            mainCamera.orthographic = true;
            _camController = mainCamera.GetComponent<CameraController2D>();
            if (_camController == null)
                _camController = mainCamera.gameObject.AddComponent<CameraController2D>();

            if (gridRenderer == null)
            {
                var gridObj = new GameObject("GridRenderer");
                gridRenderer = gridObj.AddComponent<GridLineRenderer>();
            }
        }

        private void SeedInitialContent()
        {
            var rng = new System.Random(randomSeed);
            for (int i = 0; i < _data.cells.Length; i++)
            {
                if (rng.NextDouble() < initialFillRate)
                    _data.cells[i].objectId = 1;
            }
        }

        private GameObject CreateFallbackPrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "CellObject_Fallback";
            var mat = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.4f, 0.1f, 0.95f) };
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.transform.localScale = new Vector3(0.8f * cellSize, 0.8f * cellSize, 1f);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.SetActive(false);
            return go;
        }

        private GameObject CreateSelectionMarker()
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.name = "SelectionMarker";
            marker.transform.SetParent(objectRoot);
            marker.transform.localScale = new Vector3(cellSize * 0.95f, cellSize * 0.95f, 1f);
            var mat = new Material(Shader.Find("Sprites/Default")) { color = selectionColor };
            marker.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var col = marker.GetComponent<Collider>();
            if (col != null) Destroy(col);
            marker.SetActive(false);
            return marker;
        }

        private void Update()
        {
            HandleInput();
            HandleSaveLoadShortcuts();
            RefreshVisible(false);
        }

        private RectInt GetVisibleRect()
        {
            float halfH = mainCamera.orthographicSize;
            float halfW = halfH * mainCamera.aspect;
            Vector3 c = mainCamera.transform.position;

            int minX = Mathf.FloorToInt((c.x - halfW - origin.x) / cellSize) - cullPadding;
            int maxX = Mathf.FloorToInt((c.x + halfW - origin.x) / cellSize) + cullPadding;
            int minY = Mathf.FloorToInt((c.y - halfH - origin.y) / cellSize) - cullPadding;
            int maxY = Mathf.FloorToInt((c.y + halfH - origin.y) / cellSize) + cullPadding;

            minX = Mathf.Clamp(minX, 0, width - 1);
            maxX = Mathf.Clamp(maxX, 0, width - 1);
            minY = Mathf.Clamp(minY, 0, height - 1);
            maxY = Mathf.Clamp(maxY, 0, height - 1);

            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private void RefreshVisible(bool force)
        {
            RectInt r = GetVisibleRect();
            if (!force && r.Equals(_lastVisible)) return;
            _lastVisible = r;

            _tmpRelease.Clear();
            foreach (var kv in _active)
            {
                int x = kv.Key % width;
                int y = kv.Key / width;
                if (x < r.xMin || x >= r.xMax || y < r.yMin || y >= r.yMax)
                    _tmpRelease.Add(kv.Key);
            }
            for (int i = 0; i < _tmpRelease.Count; i++)
            {
                int idx = _tmpRelease[i];
                _pool.Release(_active[idx]);
                _active.Remove(idx);
            }

            for (int y = r.yMin; y < r.yMax; y++)
            {
                for (int x = r.xMin; x < r.xMax; x++)
                {
                    int idx = _data.IndexOf(x, y);
                    if (_data.cells[idx].objectId != 0 && !_active.ContainsKey(idx))
                        SpawnVisual(idx, x, y);
                }
            }
        }

        private void SpawnVisual(int idx, int x, int y)
        {
            var go = _pool.Get(_math.CellToWorldCenter(new Vector2Int(x, y)));
            if (go != null) _active[idx] = go;
        }

        private void DespawnVisual(int idx)
        {
            if (_active.TryGetValue(idx, out var go))
            {
                _pool.Release(go);
                _active.Remove(idx);
            }
        }

        private void HandleInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasReleasedThisFrame && !_camController.DraggedThisPress)
            {
                if (_input.TryGetHoveredCell(out var cell))
                {
                    CellClicked?.Invoke(cell);
                    MoveSelection(cell);
                    TogglePlace(cell);
                }
            }

            if (mouse.rightButton.wasPressedThisFrame && _input.TryGetHoveredCell(out var rc))
                Remove(rc);
        }

        private void MoveSelection(Vector2Int cell)
        {
            if (_selectionMarker == null) return;
            _selectionMarker.SetActive(true);
            _selectionMarker.transform.position = _math.CellToWorldCenter(cell) + new Vector3(0f, 0f, -0.01f);
        }

        private void TogglePlace(Vector2Int cell)
        {
            int idx = _data.IndexOf(cell.x, cell.y);
            ref var c = ref _data.Get(cell.x, cell.y);
            if (c.objectId == 0)
            {
                c.objectId = 1;
                SpawnVisual(idx, cell.x, cell.y);
            }
            else
            {
                c.objectId = 0;
                DespawnVisual(idx);
            }
        }

        private void Remove(Vector2Int cell)
        {
            int idx = _data.IndexOf(cell.x, cell.y);
            ref var c = ref _data.Get(cell.x, cell.y);
            if (c.objectId == 0) return;
            c.objectId = 0;
            DespawnVisual(idx);
        }

        private void HandleSaveLoadShortcuts()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.sKey.wasPressedThisFrame) GridSaveSystem.Save(_data);
            if (kb.lKey.wasPressedThisFrame) LoadFromSave();
            if (kb.cKey.wasPressedThisFrame) ClearAll();
        }

        private void LoadFromSave()
        {
            if (!GridSaveSystem.TryLoad(out var save)) return;
            if (save.width != width || save.height != height) return;

            ReleaseAllVisuals();
            _data.cells = save.cells;
            RefreshVisible(true);
        }

        private void ClearAll()
        {
            ReleaseAllVisuals();
            for (int i = 0; i < _data.cells.Length; i++)
                _data.cells[i].objectId = 0;
        }

        private void ReleaseAllVisuals()
        {
            foreach (var kv in _active) _pool.Release(kv.Value);
            _active.Clear();
        }

        private Rect GetMapWorldBounds()
        {
            return new Rect(origin.x, origin.y, width * cellSize, height * cellSize);
        }

        private void OnDrawGizmos()
        {
            if (!drawCameraGizmos) return;

            Gizmos.color = mapBoundsColor;
            var map = GetMapWorldBounds();
            DrawRect(map);

            if (mainCamera == null)
                mainCamera = Camera.main;
            if (mainCamera == null) return;

            float halfH = mainCamera.orthographicSize;
            float halfW = halfH * mainCamera.aspect;
            Vector3 c = mainCamera.transform.position;
            Rect camRect = Rect.MinMaxRect(c.x - halfW, c.y - halfH, c.x + halfW, c.y + halfH);
            Gizmos.color = cameraViewColor;
            DrawRect(camRect);

            float pad = cullPadding * cellSize;
            Rect cullRect = Rect.MinMaxRect(camRect.xMin - pad, camRect.yMin - pad, camRect.xMax + pad, camRect.yMax + pad);
            Gizmos.color = cullViewColor;
            DrawRect(cullRect);
        }

        private static void DrawRect(Rect r)
        {
            Vector3 bl = new Vector3(r.xMin, r.yMin, 0f);
            Vector3 br = new Vector3(r.xMax, r.yMin, 0f);
            Vector3 tr = new Vector3(r.xMax, r.yMax, 0f);
            Vector3 tl = new Vector3(r.xMin, r.yMax, 0f);

            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl);
            Gizmos.DrawLine(tl, bl);
        }
    }
}
