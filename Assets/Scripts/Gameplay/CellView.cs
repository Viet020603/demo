using UnityEngine;
using UnityEngine.UI;

namespace Demo.Gameplay
{
    /// <summary>
    /// Attached to every cell prefab instance. Holds the id coming from the JSON file.
    /// Works with both world-space Renderers and uGUI Graphics (Image...).
    /// </summary>
    public class CellView : MonoBehaviour
    {
        public int Id;
        public int MaLoai;
        public int Cx;
        public int Cy;

        private Renderer _renderer;
        private Graphic _graphic;
        private bool _resolved;
        private MaterialPropertyBlock _block;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public void Bind(int id, int maLoai, int cx, int cy, Color color)
        {
            Id = id;
            MaLoai = maLoai;
            Cx = cx;
            Cy = cy;
            gameObject.name = "Cell_" + id;

            if (!_resolved)
            {
                _graphic = GetComponentInChildren<Graphic>();
                if (_graphic == null) _renderer = GetComponentInChildren<Renderer>();
                _resolved = true;
            }

            if (_graphic != null)
            {
                _graphic.color = color;
                return;
            }

            if (_renderer == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(ColorId, color);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
