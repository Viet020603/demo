using UnityEngine;
using UnityEngine.UI;

namespace Demo.Gameplay
{
    /// <summary>Attached to every cell prefab instance. Holds the data coming from the JSON file.</summary>
    public class CellView : MonoBehaviour
    {
        public int Id;
        public int MaLoai;
        public int Cx;
        public int Cy;

        private Graphic _graphic;

        public void Bind(int id, int maLoai, int cx, int cy, Color color)
        {
            Id = id;
            MaLoai = maLoai;
            Cx = cx;
            Cy = cy;
            gameObject.name = "Cell_" + id;

            if (_graphic == null) _graphic = GetComponentInChildren<Graphic>();
            if (_graphic != null) _graphic.color = color;
        }
    }
}
