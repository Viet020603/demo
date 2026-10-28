using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Demo.CameraControl;
using Demo.Grid;
using Demo.Rendering;
using Demo.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Demo.Gameplay
{
    /// <summary>
    /// Builds the map from o_ban_do.json. Every cell has an id; each visible cell is a prefab
    /// instance (CellView) bound to that id. Clicking a cell logs its id.
    /// Only cells inside the camera view get an instance (pooled and reused while panning).
    /// </summary>
    public class GridController : MonoBehaviour
    {
        [Header("Map data (o_ban_do.json)")]
        [Tooltip("Drag the JSON here, or leave empty and put o_ban_do.json in Assets/Resources/.")]
        [SerializeField] private TextAsset mapFile;
        [SerializeField] private string resourceName = "o_ban_do";

        [Header("Grid (auto-detected from the JSON when loaded)")]
        [SerializeField] private int width = 31;
        [SerializeField] private int height = 31;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector2 origin = Vector2.zero;

        [Header("Scene References (optional)")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private GridLineRenderer gridRenderer;
        [SerializeField] private Transform objectRoot;
        [SerializeField] private SimplePool pool;
        [SerializeField] private GameObject selectionMarker;
        [Tooltip("Cell prefab. If it has no CellView one is added. Empty = auto-created quad.")]
        [SerializeField] private GameObject placePrefab;

        [Header("Visual")]
        [SerializeField] private Color selectionColor = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField, Range(0.1f, 1f)] private float objectScale = 0.95f;
        [Tooltip("Color per maLoai (index = maLoai).")]
        [SerializeField] private Color[] typeColors = new Color[]
        {
            new Color(0.5f, 0.5f, 0.5f),   // 0
            new Color(1.0f, 0.2f, 0.2f),   // 1
            new Color(1.0f, 0.6f, 0.2f),   // 2
            new Color(1.0f, 0.9f, 0.3f),   // 3
            new Color(0.4f, 0.8f, 0.4f),   // 4
            new Color(0.3f, 0.6f, 1.0f),   // 5
            new Color(0.6f, 0.4f, 0.9f),   // 6
            new Color(0.9f, 0.4f, 0.7f),   // 7
            new Color(0.2f, 0.9f, 0.9f),   // 8
        };

        [Header("Culling / Pool")]
        [SerializeField] private int cullPadding = 1;
        [SerializeField] private float startOrthoSize = 6f;
        [SerializeField] private float designAspect = 16f / 9f;

        [Header("Debug")]
        [SerializeField] private bool logClicks = true;
        [SerializeField] private bool drawCameraGizmos = true;
        [SerializeField] private Color cameraViewColor = new Color(0f, 1f, 1f, 0.9f);
        [SerializeField] private Color cullViewColor = new Color(1f, 0.85f, 0f, 0.9f);
        [SerializeField] private Color mapBoundsColor = new Color(1f, 0f, 0f, 0.9f);

        /// <summary>Raised with the clicked cell's id.</summary>
        public event Action<int> CellIdClicked;

        private GridData _data;
        private GridMath _math;
        private GridInputReader _input;
        private CameraController2D _camController;

        private int[] _idByIndex;   // grid index -> id (0 = no cell in json)
        private readonly Dictionary<int, GameObject> _active = new Dictionary<int, GameObject>();
        private readonly List<int> _tmpRelease = new List<int>();
        private RectInt _lastVisible = new RectInt(-1, -1, 0, 0);
        private float _lastOrtho = -1f;
        private float _lastAspect = -1f;

        // ------------------------------------------------------------------ runtime

        private void Awake()
        {
            LoadMap();
            ResolveSceneReferences();

            _math = new GridMath(origin, cellSize);
            _input = new GridInputReader(mainCamera, _math, width, height);
            gridRenderer.Configure(width, height, cellSize, origin);

            if (selectionMarker != null) selectionMarker.SetActive(false);

            var bounds = GetMapWorldBounds();
            _camController.SetBounds(bounds);
            mainCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
            _camController.SetZoom(startOrthoSize);

            pool.Init();
            EnsurePoolForCamera();
        }

        private void Start()
        {
            RefreshVisible(true);
        }

        private void LoadMap()
        {
            TextAsset asset = mapFile != null ? mapFile : Resources.Load<TextAsset>(resourceName);
            if (asset == null)
            {
                Debug.LogError($"[GridController] Map json not found. Put '{resourceName}.json' in Assets/Resources/ or assign it to 'Map File'.");
                _data = new GridData(width, height);
                _idByIndex = new int[width * height];
                return;
            }

            var cells = MapJsonLoader.Parse(asset.text);

            int maxX = 1, maxY = 1;
            foreach (var c in cells)
            {
                if (c.cx > maxX) maxX = c.cx;
                if (c.cy > maxY) maxY = c.cy;
            }
            width = maxX;
            height = maxY;

            _data = new GridData(width, height);
            _idByIndex = new int[width * height];

            foreach (var c in cells)
            {
                int x = c.cx - 1;   // json is 1-based
                int y = c.cy - 1;
                if (!_data.InBounds(x, y))
                {
                    Debug.LogWarning($"[GridController] Cell id {c.id} out of range ({c.cx},{c.cy}).");
                    continue;
                }
                int idx = _data.IndexOf(x, y);
                _idByIndex[idx] = c.id;
                _data.cells[idx].objectId = c.id;
                _data.cells[idx].terrainType = (byte)Mathf.Clamp(c.maLoai, 0, 255);
            }

            Debug.Log($"[GridController] Loaded {cells.Length} cells from json ({width}x{height}).");
        }

        private void ResolveSceneReferences()
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
                gridRenderer = new GameObject("GridRenderer").AddComponent<GridLineRenderer>();

            if (objectRoot == null)
                objectRoot = new GameObject("Objects").transform;

            if (pool == null)
            {
                var poolObj = new GameObject("ObjectPool");
                poolObj.transform.SetParent(objectRoot);
                pool = poolObj.AddComponent<SimplePool>();
            }

            if (selectionMarker == null)
                selectionMarker = CreateSelectionMarkerRuntime();
        }

        private GameObject CreateCellTemplate()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "CellTemplate";
            go.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            go.transform.localScale = new Vector3(objectScale * cellSize, objectScale * cellSize, 1f);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.AddComponent<CellView>();
            go.transform.SetParent(pool.transform, false);
            go.SetActive(false);
            return go;
        }

        private GameObject CreateSelectionMarkerRuntime()
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.name = "SelectionMarker";
            marker.transform.SetParent(objectRoot);
            marker.transform.localScale = new Vector3(cellSize, cellSize, 1f);
            marker.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = selectionColor };
            var col = marker.GetComponent<Collider>();
            if (col != null) Destroy(col);
            marker.SetActive(false);
            return marker;
        }

        private void Update()
        {
            HandleInput();

            if (!Mathf.Approximately(_lastOrtho, mainCamera.orthographicSize) ||
                !Mathf.Approximately(_lastAspect, mainCamera.aspect))
            {
                EnsurePoolForCamera();
                _lastVisible = new RectInt(-1, -1, 0, 0);
            }

            RefreshVisible(false);
        }

        // ------------------------------------------------------------------ pool size from camera

        private int CapacityFor(float ortho, float aspect)
        {
            float viewW = ortho * aspect * 2f;
            float viewH = ortho * 2f;
            int cols = Mathf.Clamp(Mathf.CeilToInt(viewW / cellSize) + cullPadding * 2 + 2, 1, width);
            int rows = Mathf.Clamp(Mathf.CeilToInt(viewH / cellSize) + cullPadding * 2 + 2, 1, height);
            return cols * rows;
        }

        private void EnsurePoolForCamera()
        {
            _lastOrtho = mainCamera.orthographicSize;
            _lastAspect = mainCamera.aspect;

            int needed = CapacityFor(_lastOrtho, _lastAspect);
            if (pool.TotalCount >= needed) return;

            if (pool.TotalCount == 0)
                pool.SetPrefab(placePrefab != null ? placePrefab : CreateCellTemplate());

            int created = pool.EnsureCapacity(needed);
            Debug.Log($"[GridController] Camera size={_lastOrtho:0.##}, aspect={_lastAspect:0.##} -> {needed} cells needed. Created {created}, pool total {pool.TotalCount}.");
        }

        // ------------------------------------------------------------------ culling

        private RectInt GetVisibleRect()
        {
            float halfH = mainCamera.orthographicSize;
            float halfW = halfH * mainCamera.aspect;
            Vector3 c = mainCamera.transform.position;

            int minX = Mathf.Clamp(Mathf.FloorToInt((c.x - halfW - origin.x) / cellSize) - cullPadding, 0, width - 1);
            int maxX = Mathf.Clamp(Mathf.FloorToInt((c.x + halfW - origin.x) / cellSize) + cullPadding, 0, width - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt((c.y - halfH - origin.y) / cellSize) - cullPadding, 0, height - 1);
            int maxY = Mathf.Clamp(Mathf.FloorToInt((c.y + halfH - origin.y) / cellSize) + cullPadding, 0, height - 1);

            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private void RefreshVisible(bool force)
        {
            RectInt r = GetVisibleRect();
            if (!force && r.Equals(_lastVisible)) return;
            _lastVisible = r;

            // release cells that left the view
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
                pool.Release(_active[idx]);
                _active.Remove(idx);
            }

            // reuse pooled prefabs for cells that entered the view and bind their id
            for (int y = r.yMin; y < r.yMax; y++)
            {
                for (int x = r.xMin; x < r.xMax; x++)
                {
                    int idx = _data.IndexOf(x, y);
                    if (_idByIndex[idx] != 0 && !_active.ContainsKey(idx))
                        ShowCell(idx, x, y);
                }
            }
        }

        private void ShowCell(int idx, int x, int y)
        {
            var go = pool.Get(_math.CellToWorldCenter(new Vector2Int(x, y)));
            if (go == null) return;

            var view = go.GetComponent<CellView>();
            if (view == null) view = go.AddComponent<CellView>();

            int maLoai = _data.cells[idx].terrainType;
            view.Bind(_idByIndex[idx], maLoai, x + 1, y + 1, ColorFor(maLoai));
            _active[idx] = go;
        }

        private Color ColorFor(int maLoai)
        {
            if (typeColors == null || typeColors.Length == 0) return Color.white;
            return typeColors[Mathf.Clamp(maLoai, 0, typeColors.Length - 1)];
        }

        // ------------------------------------------------------------------ input

        private void HandleInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            // click = release without having dragged the camera
            if (mouse.leftButton.wasReleasedThisFrame && !_camController.DraggedThisPress)
            {
                if (_input.TryGetHoveredCell(out var cell))
                    OnCellClicked(cell);
            }
        }

        private void OnCellClicked(Vector2Int cell)
        {
            int idx = _data.IndexOf(cell.x, cell.y);
            int id = _idByIndex[idx];

            if (selectionMarker != null)
            {
                selectionMarker.SetActive(true);
                selectionMarker.transform.position = _math.CellToWorldCenter(cell) + new Vector3(0f, 0f, -0.01f);
            }

            if (id == 0) return;
            if (logClicks)
                Debug.Log($"Clicked cell id = {id}  (cx={cell.x + 1}, cy={cell.y + 1}, maLoai={_data.cells[idx].terrainType})");
            CellIdClicked?.Invoke(id);
        }

        private Rect GetMapWorldBounds()
        {
            return new Rect(origin.x, origin.y, width * cellSize, height * cellSize);
        }

        // ------------------------------------------------------------------ gizmos

        private void OnDrawGizmos()
        {
            if (!drawCameraGizmos) return;

            Gizmos.color = mapBoundsColor;
            DrawRect(GetMapWorldBounds());

            var cam = mainCamera != null ? mainCamera : Camera.main;
            if (cam == null) return;

            float halfH = cam.orthographicSize;
            float halfW = halfH * Mathf.Max(cam.aspect, 0.01f);
            Vector3 c = cam.transform.position;
            Rect camRect = Rect.MinMaxRect(c.x - halfW, c.y - halfH, c.x + halfW, c.y + halfH);
            Gizmos.color = cameraViewColor;
            DrawRect(camRect);

            float pad = cullPadding * cellSize;
            Gizmos.color = cullViewColor;
            DrawRect(Rect.MinMaxRect(camRect.xMin - pad, camRect.yMin - pad, camRect.xMax + pad, camRect.yMax + pad));
        }

        private static void DrawRect(Rect r)
        {
            var bl = new Vector3(r.xMin, r.yMin, 0f);
            var br = new Vector3(r.xMax, r.yMin, 0f);
            var tr = new Vector3(r.xMax, r.yMax, 0f);
            var tl = new Vector3(r.xMin, r.yMax, 0f);
            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl);
            Gizmos.DrawLine(tl, bl);
        }

#if UNITY_EDITOR
        // ------------------------------------------------------------------ edit-time preview (optional)

        [ContextMenu("Build Scene Objects")]
        public void BuildSceneObjects()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                mainCamera = camObj.AddComponent<Camera>();
                Undo.RegisterCreatedObjectUndo(camObj, "Create Camera");
            }
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = startOrthoSize;
            Rect map = GetMapWorldBounds();
            mainCamera.transform.position = new Vector3(map.center.x, map.center.y, -10f);
            if (mainCamera.GetComponent<CameraController2D>() == null)
                mainCamera.gameObject.AddComponent<CameraController2D>();

            if (gridRenderer == null)
            {
                var g = new GameObject("GridRenderer");
                Undo.RegisterCreatedObjectUndo(g, "Create GridRenderer");
                gridRenderer = g.AddComponent<GridLineRenderer>();
            }
            gridRenderer.Configure(width, height, cellSize, origin);
            EditorUtility.SetDirty(gridRenderer);

            if (objectRoot == null)
            {
                var root = new GameObject("Objects");
                Undo.RegisterCreatedObjectUndo(root, "Create Objects root");
                objectRoot = root.transform;
            }
            if (pool == null)
            {
                var p = new GameObject("ObjectPool");
                Undo.RegisterCreatedObjectUndo(p, "Create ObjectPool");
                p.transform.SetParent(objectRoot, false);
                pool = p.AddComponent<SimplePool>();
            }

            for (int i = pool.transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(pool.transform.GetChild(i).gameObject);

            float aspect = Mathf.Max(designAspect, mainCamera.aspect);
            int count = CapacityFor(startOrthoSize, aspect);
            var list = new List<GameObject>(count);
            for (int i = 0; i < count; i++)
            {
                GameObject go;
                if (placePrefab != null)
                    go = (GameObject)PrefabUtility.InstantiatePrefab(placePrefab, pool.transform);
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    go.transform.SetParent(pool.transform, false);
                    go.transform.localScale = new Vector3(objectScale * cellSize, objectScale * cellSize, 1f);
                    go.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                    var col = go.GetComponent<Collider>();
                    if (col != null) DestroyImmediate(col);
                }
                if (go.GetComponent<CellView>() == null) go.AddComponent<CellView>();
                go.name = "Cell_" + i.ToString("000");
                go.transform.position = new Vector3(map.center.x + (i % 20) * cellSize, map.center.y + (i / 20) * cellSize, 0f);
                list.Add(go);
            }
            pool.SetItems(list);
            EditorUtility.SetDirty(pool);
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log($"[GridController] Built {count} preview cell objects. They are hidden/bound to ids automatically on Play.");
        }

        [ContextMenu("Clear Scene Objects")]
        public void ClearSceneObjects()
        {
            if (pool != null)
            {
                for (int i = pool.transform.childCount - 1; i >= 0; i--)
                    DestroyImmediate(pool.transform.GetChild(i).gameObject);
                pool.SetItems(new List<GameObject>());
                EditorUtility.SetDirty(pool);
            }
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }
}
