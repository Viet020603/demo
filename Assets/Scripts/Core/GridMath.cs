using UnityEngine;

namespace Demo.Grid
{
    public class GridMath
    {
        private readonly Vector2 _origin;
        private readonly float _cellSize;

        public GridMath(Vector2 origin, float cellSize)
        {
            _origin = origin;
            _cellSize = cellSize;
        }

        public Vector2Int WorldToCell(Vector3 world)
        {
            int x = Mathf.FloorToInt((world.x - _origin.x) / _cellSize);
            int y = Mathf.FloorToInt((world.y - _origin.y) / _cellSize);
            return new Vector2Int(x, y);
        }

        public Vector3 CellToWorldCenter(Vector2Int cell)
        {
            float x = _origin.x + (cell.x + 0.5f) * _cellSize;
            float y = _origin.y + (cell.y + 0.5f) * _cellSize;
            return new Vector3(x, y, 0f);
        }
    }
}
