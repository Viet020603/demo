using System.Collections.Generic;
using UnityEngine;

namespace Demo.Gameplay
{
    /// <summary>
    /// Pool of pre-created objects. Items can be created in the scene at edit time
    /// (see GridController > Build Scene Objects) so they can be tweaked before pressing Play.
    /// </summary>
    public class SimplePool : MonoBehaviour
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private List<GameObject> items = new List<GameObject>();
        [SerializeField] private int prewarm = 16;
        [SerializeField] private bool allowRuntimeInstantiate = false;

        private readonly Stack<GameObject> _free = new Stack<GameObject>();
        private bool _init;

        public int TotalCount => items.Count;
        public int AvailableCount => _free.Count;
        public IReadOnlyList<GameObject> Items => items;

        public void SetPrefab(GameObject value) => prefab = value;
        public void ConfigurePrewarm(int count) => prewarm = Mathf.Max(0, count);

        public void SetItems(List<GameObject> list)
        {
            items = list ?? new List<GameObject>();
            _init = false;
        }

        /// <summary>Collects scene-placed items into the free stack (all hidden). Safe to call many times.</summary>
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

        /// <summary>Runtime fallback: create items only if the scene did not provide enough.</summary>
        public void PrewarmNow()
        {
            Init();
            if (prefab == null) return;
            while (items.Count < prewarm)
            {
                var go = Instantiate(prefab, transform);
                go.SetActive(false);
                items.Add(go);
                _free.Push(go);
            }
        }

        private void Awake()
        {
            Init();
        }

        public GameObject Get(Vector3 position)
        {
            Init();
            GameObject go;
            if (_free.Count > 0)
            {
                go = _free.Pop();
            }
            else if (allowRuntimeInstantiate && prefab != null)
            {
                go = Instantiate(prefab, transform);
                items.Add(go);
            }
            else
            {
                return null; // pool exhausted: never instantiate at runtime
            }

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
