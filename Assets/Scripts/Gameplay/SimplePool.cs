using UnityEngine;

namespace Demo.Gameplay
{
    public class SimplePool : MonoBehaviour
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private int prewarm = 32;

        private readonly System.Collections.Generic.Stack<GameObject> _pool = new();

        private void Awake()
        {
            for (int i = 0; i < prewarm; i++)
            {
                var go = Instantiate(prefab, transform);
                go.SetActive(false);
                _pool.Push(go);
            }
        }

        public GameObject Get(Vector3 position)
        {
            GameObject go = _pool.Count > 0 ? _pool.Pop() : Instantiate(prefab, transform);
            go.transform.position = position;
            go.SetActive(true);
            return go;
        }

        public void Release(GameObject go)
        {
            go.SetActive(false);
            _pool.Push(go);
        }
    }
}
