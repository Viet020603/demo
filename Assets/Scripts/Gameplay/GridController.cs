using System.Collections.Generic;
using UnityEngine;
using Demo.Grid;
using Demo.Rendering;
using Demo.InputSystem;
using Demo.Gameplay;
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

        [Header("Refs")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private GridLineRenderer gridRenderer;
        [SerializeField] private GameObject placePrefab;

        [Header("Visual")]
        [SerializeField] private Transform objectRoot;

        private GridData _data;
        private GridMath _math;
        private GridInputReader _input;
        private SimplePool _pool;

        private readonly Dictionary<int, GameObject> _spawnedByIndex = new();

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            _data = new GridData(width, height);
            _math = new GridMath(origin, cellSize);
            _input = new GridInputReader(mainCamera, _math, width, height);

            if (gridRenderer != null)
                gridRenderer.Configure(width, height, cellSize, origin);

            if (objectRoot == null)
            {
                var root = new GameObject("Objects");
                objectRoot = root.transform;
            }

            var poolObj = new GameObject("ObjectPool");
            poolObj.transform.SetParent(objectRoot);
            _pool = poolObj.AddComponent<SimplePool>();

            // assign prefab via reflection-safe serialized backing by helper
            var so = new UnityEditor.SerializedObject(_pool);
            var prefabProp = so.FindProperty("prefab");
            prefabProp.objectReferenceValue = placePrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private void Update()
        {
            HandleInput();
            HandleSaveLoadShortcuts();
        }

        private void HandleInput()
        {
            if (!_input.TryGetHoveredCell(out var cell)) return;

            if (UnityEngine.Input.GetMouseButtonDown(0))
                Place(cell);

            if (UnityEngine.Input.GetMouseButtonDown(1))
                Remove(cell);
        }

        private void Place(Vector2Int cell)
        {
            if (!_data.InBounds(cell.x, cell.y)) return;
            int idx = _data.IndexOf(cell.x, cell.y);
            ref var c = ref _data.Get(cell.x, cell.y);
            if (c.objectId != 0) return;

            c.objectId = 1;
            var pos = _math.CellToWorldCenter(cell);
            var go = _pool.Get(pos);
            _spawnedByIndex[idx] = go;
        }

        private void Remove(Vector2Int cell)
        {
            if (!_data.InBounds(cell.x, cell.y)) return;
            int idx = _data.IndexOf(cell.x, cell.y);
            ref var c = ref _data.Get(cell.x, cell.y);
            if (c.objectId == 0) return;

            c.objectId = 0;
            if (_spawnedByIndex.TryGetValue(idx, out var go))
            {
                _pool.Release(go);
                _spawnedByIndex.Remove(idx);
            }
        }

        private void HandleSaveLoadShortcuts()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.S))
                GridSaveSystem.Save(_data);

            if (UnityEngine.Input.GetKeyDown(KeyCode.L))
                LoadFromSave();

            if (UnityEngine.Input.GetKeyDown(KeyCode.C))
                ClearAll();
        }

        private void LoadFromSave()
        {
            if (!GridSaveSystem.TryLoad(out var save)) return;
            if (save.width != width || save.height != height) return;

            ClearAll();
            _data.cells = save.cells;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = _data.IndexOf(x, y);
                    if (_data.cells[idx].objectId != 0)
                    {
                        var go = _pool.Get(_math.CellToWorldCenter(new Vector2Int(x, y)));
                        _spawnedByIndex[idx] = go;
                    }
                }
            }
        }

        private void ClearAll()
        {
            foreach (var kv in _spawnedByIndex)
                _pool.Release(kv.Value);
            _spawnedByIndex.Clear();

            for (int i = 0; i < _data.cells.Length; i++)
                _data.cells[i].objectId = 0;
        }
    }
}
