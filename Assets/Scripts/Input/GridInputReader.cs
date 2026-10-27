using UnityEngine;
using UnityEngine.InputSystem;
using Demo.Grid;

namespace Demo.InputSystem
{
    public class GridInputReader
    {
        private readonly Camera _camera;
        private readonly GridMath _gridMath;
        private readonly int _width;
        private readonly int _height;

        public GridInputReader(Camera camera, GridMath gridMath, int width, int height)
        {
            _camera = camera;
            _gridMath = gridMath;
            _width = width;
            _height = height;
        }

        public bool TryGetHoveredCell(out Vector2Int cell)
        {
            cell = default;
            var mouse = Mouse.current;
            if (mouse == null) return false;

            Vector2 screen = mouse.position.ReadValue();
            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
            world.z = 0f;
            cell = _gridMath.WorldToCell(world);
            return cell.x >= 0 && cell.y >= 0 && cell.x < _width && cell.y < _height;
        }
    }
}
