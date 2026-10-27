using UnityEngine;

namespace Demo.CameraControl
{
    [RequireComponent(typeof(Camera))]
    public class CameraController2D : MonoBehaviour
    {
        [SerializeField] private float dragSpeed = 1f;
        [SerializeField] private float zoomSpeed = 8f;
        [SerializeField] private float minZoom = 4f;
        [SerializeField] private float maxZoom = 25f;

        private Camera _cam;
        private Vector3 _lastMouse;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
        }

        private void Update()
        {
            if (UnityEngine.Input.GetMouseButtonDown(2))
                _lastMouse = UnityEngine.Input.mousePosition;

            if (UnityEngine.Input.GetMouseButton(2))
            {
                Vector3 delta = UnityEngine.Input.mousePosition - _lastMouse;
                transform.position -= new Vector3(delta.x, delta.y, 0f) * (dragSpeed * Time.deltaTime);
                _lastMouse = UnityEngine.Input.mousePosition;
            }

            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize - scroll * zoomSpeed, minZoom, maxZoom);
            }
        }
    }
}
