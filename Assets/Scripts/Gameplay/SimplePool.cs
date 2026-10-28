using System.Collections.Generic;
using UnityEngine;

namespace Demo.Gameplay
{
    /// <summary>
    /// Pool of reusable objects. Objects may be pre-created in the scene (edit time);
    /// at runtime EnsureCapacity() creates only the missing amount, based on the camera size.
    /// </summary>
    public class SimplePool : MonoBehaviour
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private List<GameObject> items = new List<GameObject>();

        private readonly Stack<GameObject> _free = new Stack<GameObject>();
        private bool _init;

        public int TotalCount => items.Count;
        public int AvailableCount => _free.Count;
        public IReadOnlyList<GameObject> Items => items;

        public void SetPrefab(GameObject value) => prefab = value;

        public void SetItems(List<GameObject> list)
        {
            items = list ?? new List<GameObject>();
            _init = false;
        }

        /// <summary>Collects scene-placed items into the free stack (all hidden).</summary>
        public void Init()
        {
            if (_init) return;
            _init = true;
            _free.Clear();
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var go = items[i];
                if (go == null) continue;
                go.SetActive(false);
                _free.Push(go);
            }
        }

        /// <summary>
        /// Makes sure the pool owns at least 'count' objects. Only the missing ones are created.
        /// Returns number of newly created objects.
        /// </summary>
        public int EnsureCapacity(int count)
        {
            Init();

            GameObject template = prefab;
            if (template == null && items.Count > 0) template = items[0];
            if (template == null) return 0;

            int created = 0;
            while (items.Count < count)
            {
                var go = Instantiate(template, transform);
                go.name = "Cell_" + items.Count.ToString("000");
                go.SetActive(false);
                items.Add(go);
                _free.Push(go);
                created++;
            }
            return created;
        }

        public GameObject Get(Vector3 position)
        {
            Init();
            if (_free.Count == 0) return null; // capacity is sized from the camera, so this should not happen

            var go = _free.Pop();
            go.transform.position = position;
            go.SetActive(true);
            return go;
        }

        public void Release(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            _free.Push(go);
        }
    }
}
