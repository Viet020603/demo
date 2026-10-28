using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Demo.CameraControl;
using Demo.Grid;
using Demo.Rendering;
using Demo.InputSystem;
using Demo.SaveLoad;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

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

        [Header("Scene References (optional, created by 'Build Scene Objects')")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private GridLineRenderer gridRenderer;
        [SerializeField] private Transform objectRoot;
        [SerializeField] private SimplePool pool;
        [SerializeField] private GameObject selectionMarker;
        [Tooltip("Optional. If empty, a quad with an auto-created material is used.")]
        [SerializeField] private GameObject placePrefab;

        [Header("Visual")]
        [SerializeField] private Color selectionColor = new Color(0.2f, 0.7f, 1f, 0.35f);
        [SerializeField] private Color objectColor = new Color(1f, 0.4f, 0.1f, 0.95f);
        [SerializeField, Range(0.1f, 1f)] private float objectScale = 0.8f;

        [Header("Culling / Pool")]
        [Tooltip("Extra cells around the camera view that are kept alive to avoid pop-in.")]
        [SerializeField] private int cullPadding = 1;
        [SerializeField] private float startOrthoSize = 6f;
        [Tooltip("Only used by 'Build Scene Objects' (edit time preview).")]
        [SerializeField] private float designAspect = 16f / 9f;

        [Header("Debug Gizmos")]
        [SerializeField] private bool drawCameraGizmos = true;
        [SerializeField] private Color cameraViewColor = new Color(0f, 1f, 1f, 0.9f);
        [SerializeField] private Color cullViewColor = new Color(1f, 0.85f, 0f, 0.9f);
        [SerializeField] private Color mapBoundsColor = new Color(1f, 0f, 0f, 0.9f);

        public event Action<Vector2Int> CellClicked;

        private GridData _data;
        private GridMath _math;
        private GridInputReader _input;
        private CameraController2D _camController;

        private readonly Dictionary<int, GameObject> _active = new Dictionary<int, GameObject>();
        private readonly List<int> _tmpRelease = new List<int>();
        private RectInt _lastVisible = new RectInt(-1, -1, 0, 0);

        // camera signature used to detect zoom / resolution changes at runtime
        private float _lastOrtho = -1f;
        private float _lastAspect = -1f;

        // ------------------------------------------------------------------ runtime

        private void Awake()
        {
            ResolveSceneReferences();

            _data = new GridData(width, height);
            _math = new GridMath(origin, cellSize);
            _input = new GridInputReader(mainCamera, _math, width, height);

            SeedInitialContent();
            gridRenderer.Configure(width, height, cellSize, origin);

            if (selectionMarker != null) selectionMarker.SetActive(false);

            _camController.SetBounds(GetMapWorldBounds());
            _camController.SetZoom(startOrthoSize);

            pool.Init();
            EnsurePoolForCamera(); // <-- check the camera size, then create only the needed amount
        }

        private void Start()
        {
            RefreshVisible(true);
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
                    var c = GetMapWorldBounds().center;
                    camObj.transform.position = new Vector3(c.x, c.y, -10f);
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

        private void SeedInitialContent()
        {
            var rng = new System.Random(randomSeed);
            for (int i = 0; i < _data.cells.Length; i++)
            {
                if (rng.NextDouble() < initialFillRate)
                    _data.cells[i].objectId = 1;
            }
        }

        private GameObject CreateRuntimeFallbackPrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "CellObject_Template";
            go.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = objectColor };
            go.transform.localScale = new Vector3(objectScale * cellSize, objectScale * cellSize, 1f);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(pool.transform, false);
            go.SetActive(false);
            return go;
        }

        private GameObject CreateSelectionMarkerRuntime()
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.name = "SelectionMarker";
            marker.transform.SetParent(objectRoot);
            marker.transform.localScale = new Vector3(cellSize * 0.95f, cellSize * 0.95f, 1f);
            marker.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = selectionColor };
            var col = marker.GetComponent<Collider>();
            if (col != null) Destroy(col);
            marker.SetActive(false);
            return marker;
        }

        private void Update()
        {
            HandleInput();
            HandleSaveLoadShortcuts();

            // If the camera zoom / aspect changed, re-check how many objects are needed.
            if (!Mathf.Approximately(_lastOrtho, mainCamera.orthographicSize) ||
                !Mathf.Approximately(_lastAspect, mainCamera.aspect))
            {
                EnsurePoolForCamera();
                _lastVisible = new RectInt(-1, -1, 0, 0); // force a refresh
            }

            RefreshVisible(false);
        }

        // ------------------------------------------------------------------ capacity from camera

        /// <summary>Number of cells the camera can see right now (+ padding), based on the real camera size.</summary>
        private int ComputeCapacityFromCamera()
        {
            float ortho = mainCamera.orthographicSize;
            float aspect = mainCamera.aspect;
            return CapacityFor(ortho, aspect);
        }

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

            int needed = ComputeCapacityFromCamera();
            if (pool.TotalCount >= needed) return; // enough objects already (scene-built or created earlier)

            // Need a template if the scene has no items and no prefab was assigned.
            if (pool.TotalCount == 0)
                pool.SetPrefab(placePrefab != null ? placePrefab : CreateRuntimeFallbackPrefab());

            int created = pool.EnsureCapacity(needed);
            Debug.Log($"[GridController] Camera size={_lastOrtho:0.##}, aspect={_lastAspect:0.##} -> needs {needed} objects. Created {created}, pool total {pool.TotalCount}.");
        }

        // ------------------------------------------------------------------ culling

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

            // return objects that left the view to the pool
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

            // reuse pooled objects for cells that entered the view
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
            var go = pool.Get(_math.CellToWorldCenter(new Vector2Int(x, y)));
            if (go != null) _active[idx] = go;
        }

        private void DespawnVisual(int idx)
        {
            if (_active.TryGetValue(idx, out var go))
            {
                pool.Release(go);
                _active.Remove(idx);
            }
        }

        // ------------------------------------------------------------------ input

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
            if (selectionMarker == null) return;
            selectionMarker.SetActive(true);
            selectionMarker.transform.position = _math.CellToWorldCenter(cell) + new Vector3(0f, 0f, -0.01f);
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
            foreach (var kv in _active) pool.Release(kv.Value);
            _active.Clear();
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

#if UNITY_EDITOR
        // ------------------------------------------------------------------ edit-time preview (optional)

        /// <summary>
        /// OPTIONAL: pre-creates Camera, GridRenderer, pool objects and selection marker in the scene
        /// so they can be tweaked before Play. At runtime the camera size is still checked and
        /// any missing objects are created automatically.
        /// </summary>
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

            Material objMat = placePrefab == null
                ? GetOrCreateMaterial("Assets/Materials/CellObject.mat", objectColor)
                : null;

            float aspect = Mathf.Max(designAspect, mainCamera.aspect);
            int count = CapacityFor(startOrthoSize, aspect);
            float viewW = startOrthoSize * aspect * 2f;
            float viewH = startOrthoSize * 2f;
            int cols = Mathf.Clamp(Mathf.CeilToInt(viewW / cellSize) + cullPadding * 2 + 2, 1, width);
            int rows = Mathf.Clamp(Mathf.CeilToInt(viewH / cellSize) + cullPadding * 2 + 2, 1, height);
            int minX = Mathf.Clamp(Mathf.FloorToInt((map.center.x - viewW * 0.5f - origin.x) / cellSize) - cullPadding, 0, Mathf.Max(0, width - cols));
            int minY = Mathf.Clamp(Mathf.FloorToInt((map.center.y - viewH * 0.5f - origin.y) / cellSize) - cullPadding, 0, Mathf.Max(0, height - rows));

            var math = new GridMath(origin, cellSize);
            var list = new List<GameObject>(count);
            for (int i = 0; i < count; i++)
            {
                GameObject go;
                if (placePrefab != null)
                {
                    go = (GameObject)PrefabUtility.InstantiatePrefab(placePrefab, pool.transform);
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    go.transform.SetParent(pool.transform, false);
                    go.transform.localScale = new Vector3(objectScale * cellSize, objectScale * cellSize, 1f);
                    go.GetComponent<MeshRenderer>().sharedMaterial = objMat;
                    var col = go.GetComponent<Collider>();
                    if (col != null) DestroyImmediate(col);
                }

                go.name = "Cell_" + i.ToString("000");
                go.transform.position = math.CellToWorldCenter(new Vector2Int(minX + i % cols, minY + i / cols));
                go.SetActive(true);
                list.Add(go);
            }
            pool.SetItems(list);
            EditorUtility.SetDirty(pool);

            if (selectionMarker == null)
            {
                var m = GameObject.CreatePrimitive(PrimitiveType.Quad);
                m.name = "SelectionMarker";
                m.transform.SetParent(objectRoot, false);
                m.transform.localScale = new Vector3(cellSize * 0.95f, cellSize * 0.95f, 1f);
                m.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial("Assets/Materials/SelectionMarker.mat", selectionColor);
                var col = m.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col);
                m.SetActive(false);
                selectionMarker = m;
            }

            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(mainCamera);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log($"[GridController] Built {count} preview pool objects in the scene. Save the scene (Ctrl+S).");
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

        private static Material GetOrCreateMaterial(string path, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                    AssetDatabase.CreateFolder("Assets", "Materials");
                mat = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }
#endif
    }
}
