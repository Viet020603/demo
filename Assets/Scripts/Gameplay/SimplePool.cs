using UnityEngine;

namespace Demo.Gameplay
{
    public class SimplePool : MonoBehaviour
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private int prewarm = 16;
        [SerializeField] private bool allowRuntimeInstantiate = false;

        private readonly System.Collections.Generic.Stack<GameObject> _pool = new();

        public int AvailableCount => _pool.Count;
        public int SpawnedCount { get; private set; }

        public void SetPrefab(GameObject value) => prefab = value;

        public void ConfigurePrewarm(int count)
        {
            prewarm = Mathf.Max(0, count);
        }

        public void PrewarmNow()
        {
            if (prefab == null) return;
            while (SpawnedCount < prewarm)
            {
                var go = Instantiate(prefab, transform);
                go.SetActive(false);
                _pool.Push(go);
                SpawnedCount++;
            }
        }

        private void Awake()
        {
            PrewarmNow();
        }

        public GameObject Get(Vector3 position)
        {
            if (prefab == null)
            {
                Debug.LogError("SimplePool prefab is null.");
                return null;
            }

            GameObject go = null;
            if (_pool.Count > 0)
            {
                go = _pool.Pop();
            }
            else if (allowRuntimeInstantiate)
            {
                go = Instantiate(prefab, transform);
                SpawnedCount++;
            }
            else
            {
                return null;
            }

            go.transform.position = position;
            go.SetActive(true);
            return go;
        }

        public void Release(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            _pool.Push(go);
        }
    }
}
