using UnityEngine;
using UnityEngine.UI;

namespace Demo.Gameplay
{
    /// <summary>
    /// Draws all grid lines as one UI mesh (single draw call). Size the RectTransform to the grid area.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class GridLinesGraphic : MaskableGraphic
    {
        [SerializeField] private int columns = 31;
        [SerializeField] private int rows = 31;
        [SerializeField] private float thickness = 1.5f;

        public void Configure(int cols, int rowCount, float lineThickness)
        {
            columns = cols;
            rows = rowCount;
            thickness = lineThickness;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (columns <= 0 || rows <= 0) return;

            Rect r = GetPixelAdjustedRect();
            float half = thickness * 0.5f;
            Color32 c = color;

            for (int i = 0; i <= columns; i++)
            {
                float x = r.xMin + r.width * i / columns;
                AddQuad(vh, x - half, r.yMin - half, x + half, r.yMax + half, c);
            }
            for (int j = 0; j <= rows; j++)
            {
                float y = r.yMin + r.height * j / rows;
                AddQuad(vh, r.xMin - half, y - half, r.xMax + half, y + half, c);
            }
        }

        private static void AddQuad(VertexHelper vh, float x0, float y0, float x1, float y1, Color32 c)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(x0, y0), c, Vector2.zero);
            vh.AddVert(new Vector3(x0, y1), c, Vector2.zero);
            vh.AddVert(new Vector3(x1, y1), c, Vector2.zero);
            vh.AddVert(new Vector3(x1, y0), c, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
