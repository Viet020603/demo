using System;
using UnityEngine;
using Demo.Grid;

namespace Demo.SaveLoad
{
    [Serializable]
    public class GridSaveData
    {
        public int width;
        public int height;
        public CellData[] cells;
    }

    public static class GridSaveSystem
    {
        private const string SaveKey = "demo_grid_save";

        public static void Save(GridData data)
        {
            var payload = new GridSaveData
            {
                width = data.width,
                height = data.height,
                cells = data.cells
            };
            string json = JsonUtility.ToJson(payload);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
            Debug.Log("Grid saved.");
        }

        public static bool TryLoad(out GridSaveData data)
        {
            data = null;
            if (!PlayerPrefs.HasKey(SaveKey)) return false;

            string json = PlayerPrefs.GetString(SaveKey);
            data = JsonUtility.FromJson<GridSaveData>(json);
            return data != null && data.cells != null;
        }
    }
}
