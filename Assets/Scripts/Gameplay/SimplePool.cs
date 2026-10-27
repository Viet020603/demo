using UnityEngine;

namespace Demo.Gameplay
{
    public class SimplePool : MonoBehaviour
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private int prewarm = 16;

        private readonly System.Collections.Generic.Stack<GameObject> _pool = new();

        public void SetPrefab(GameObject value) => prefab = value;

        private void Awake()
        {
            if (prefab == null) return;
            for (int i = 0; i < prewarm; i++)
            {
                var go = Instantiate(prefab, transform);
                go.SetActive(false);
                _pool.Push(go);
            }
        }

        public GameObject Get(Vector3 position)
        {
            if (prefab == null)
            {
                Debug.LogError("SimplePool prefab is null.");
                return null;
            }

            GameObject go = _pool.Count > 0 ? _pool.Pop() : Instantiate(prefab, transform);
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
