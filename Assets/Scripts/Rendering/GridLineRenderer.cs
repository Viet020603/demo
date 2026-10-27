using UnityEngine;

namespace Demo.Rendering
{
    [ExecuteAlways]
    public class GridLineRenderer : MonoBehaviour
    {
        [SerializeField] private int columns = 31;
        [SerializeField] private int rows = 31;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector2 origin = Vector2.zero;
        [SerializeField] private Color color = new(0.1f, 0.45f, 0.1f, 0.55f);

        public int Columns => columns;
        public int Rows => rows;
        public float CellSize => cellSize;
        public Vector2 Origin => origin;

        public void Configure(int w, int h, float size, Vector2 org)
        {
            columns = w;
            rows = h;
            cellSize = size;
            origin = org;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = color;
            float width = columns * cellSize;
            float height = rows * cellSize;

            for (int x = 0; x <= columns; x++)
            {
                var from = new Vector3(origin.x + x * cellSize, origin.y, 0f);
                var to = new Vector3(origin.x + x * cellSize, origin.y + height, 0f);
                Gizmos.DrawLine(from, to);
            }

            for (int y = 0; y <= rows; y++)
            {
                var from = new Vector3(origin.x, origin.y + y * cellSize, 0f);
                var to = new Vector3(origin.x + width, origin.y + y * cellSize, 0f);
                Gizmos.DrawLine(from, to);
            }
        }
    }
}
