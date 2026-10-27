using System;
using UnityEngine;

namespace Demo.Grid
{
    [Serializable]
    public struct CellData
    {
        public byte terrainType;
        public int objectId; // 0 = empty
        public bool walkable;
    }

    [Serializable]
    public class GridData
    {
        public int width;
        public int height;
        public CellData[] cells;

        public GridData(int width, int height)
        {
            this.width = width;
            this.height = height;
            cells = new CellData[width * height];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].walkable = true;
                cells[i].objectId = 0;
                cells[i].terrainType = 0;
            }
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

        public int IndexOf(int x, int y) => y * width + x;

        public ref CellData Get(int x, int y) => ref cells[IndexOf(x, y)];
    }
}
