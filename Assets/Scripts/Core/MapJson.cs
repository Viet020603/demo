using System;
using UnityEngine;

namespace Demo.Grid
{
    /// <summary>One entry of o_ban_do.json.  cx = column (1-based), cy = row (1-based).</summary>
    [Serializable]
    public class MapCellJson
    {
        public int id;
        public int maLoai;
        public int cx;
        public int cy;
    }

    [Serializable]
    public class MapCellList
    {
        public MapCellJson[] items;
    }

    public static class MapJsonLoader
    {
        /// <summary>JsonUtility can't read a top-level array, so wrap it first.</summary>
        public static MapCellJson[] Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<MapCellJson>();
            var wrapped = "{\"items\":" + json + "}";
            var list = JsonUtility.FromJson<MapCellList>(wrapped);
            return list != null && list.items != null ? list.items : Array.Empty<MapCellJson>();
        }
    }
}
