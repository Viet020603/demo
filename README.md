# Unity Grid Strategy Demo (31x31, JSON driven)

Unity version: **6000.3.20f1** · new Input System.

## Map from JSON
1. Copy your **`o_ban_do.json`** into **`Assets/Resources/o_ban_do.json`**
   (or drag it into the `Map File` field of `GridController`).
2. Press Play.

JSON format (array):
```json
[ { "id": 101, "maLoai": 4, "cx": 1, "cy": 1 }, ... ]
```
- `cx` = column, `cy` = row (1-based), `id` = unique cell id, `maLoai` = cell type.
- Grid size is detected automatically from the max `cx` / `cy`.

## How it works
- Every cell in the JSON is a **prefab instance (`CellView`) bound to its `id`**.
- To stay fast, only cells inside the camera view are instantiated; they are pooled and reused while panning, and re-bound to the new cell's id.
- Pool size is computed from the real camera size at runtime.
- **Click a cell -> Console prints** `Clicked cell id = 101 (cx=1, cy=1, maLoai=4)`.
- Subscribe in code: `gridController.CellIdClicked += id => { ... }`.

## Custom prefab / colors
- Assign `placePrefab` on `GridController` to use your own cell prefab (a `CellView` is added if missing).
- `typeColors` sets the color per `maLoai` (index = maLoai).

## Controls
- Left drag / middle drag: pan camera
- Left click (no drag): select + log id
- Mouse wheel: zoom

Gizmos: red = map, cyan = camera view, yellow = culling area.
