using UnityEngine;

namespace Demo.CameraControl
{
    [RequireComponent(typeof(Camera))]
    public class CameraController2D : MonoBehaviour
    {
        [Header("Drag")]
        [SerializeField] private float dragThresholdPixels = 6f;

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 0.01f;
        [SerializeField] private float minZoom = 3f;
        [SerializeField] private float maxZoom = 20f;

        private Camera _cam;
        private bool _pressed;
        private Vector2 _pressScreen;
        private Vector3 _dragOriginWorld;
        private bool _hasBounds;
        private Rect _bounds;

        /// <summary>True if the last press turned into a drag (so it must NOT count as a click).</summary>
        public bool DraggedThisPress { get; private set; }

        /// <summary>Current camera visible rect in world-space.</summary>
        public Rect CurrentViewRect
        {
            get
            {
                float halfH = _cam.orthographicSize;
                float halfW = halfH * _cam.aspect;
                Vector3 p = transform.position;
                return Rect.MinMaxRect(p.x - halfW, p.y - halfH, p.x + halfW, p.y + halfH);
            }
        }

        /// <summary>Current world bounds clamp rect (if configured).</summary>
        public bool TryGetBounds(out Rect bounds)
        {
            bounds = _bounds;
            return _hasBounds;
        }

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
        }

        public void SetBounds(Rect worldBounds)
        {
            _bounds = worldBounds;
            _hasBounds = true;
            ClampToBounds();
        }

        public void SetZoom(float orthoSize)
        {
            _cam.orthographicSize = Mathf.Clamp(orthoSize, minZoom, maxZoom);
            ClampToBounds();
        }

        private void Update()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;

            HandleDrag(mouse);
            HandleZoom(mouse);
        }

        private void HandleDrag(UnityEngine.InputSystem.Mouse mouse)
        {
            bool downNow = mouse.leftButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame;
            bool held = mouse.leftButton.isPressed || mouse.middleButton.isPressed;

            Vector2 screen = mouse.position.ReadValue();

            if (downNow)
            {
                _pressed = true;
                DraggedThisPress = false;
                _pressScreen = screen;
                _dragOriginWorld = ScreenToWorld(screen);
            }

            if (_pressed && held)
            {
                if (!DraggedThisPress && (screen - _pressScreen).sqrMagnitude > dragThresholdPixels * dragThresholdPixels)
                {
                    DraggedThisPress = true;
                    _dragOriginWorld = ScreenToWorld(screen);
                }

                if (DraggedThisPress)
                {
                    Vector3 delta = _dragOriginWorld - ScreenToWorld(screen);
                    transform.position += new Vector3(delta.x, delta.y, 0f);
                    ClampToBounds();
                }
            }

            if (!held) _pressed = false;
        }

        private void HandleZoom(UnityEngine.InputSystem.Mouse mouse)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.001f) return;

            _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize - scroll * zoomSpeed * 10f, minZoom, maxZoom);
            ClampToBounds();
        }

        private Vector3 ScreenToWorld(Vector2 screen)
        {
            Vector3 w = _cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_cam.transform.position.z));
            w.z = 0f;
            return w;
        }

        private void ClampToBounds()
        {
            if (!_hasBounds) return;

            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;
            Vector3 p = transform.position;

            p.x = _bounds.width <= halfW * 2f
                ? _bounds.center.x
                : Mathf.Clamp(p.x, _bounds.xMin + halfW, _bounds.xMax - halfW);
            p.y = _bounds.height <= halfH * 2f
                ? _bounds.center.y
                : Mathf.Clamp(p.y, _bounds.yMin + halfH, _bounds.yMax - halfH);

            transform.position = p;
        }
    }
}
