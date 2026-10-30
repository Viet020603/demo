# Unity Grid Strategy Demo (31x31, JSON driven)

Unity version: **6000.3.20f1** · new Input System · uGUI Canvas.

## Run
1. Copy `o_ban_do.json` into `Assets/Resources/o_ban_do.json`.
2. Open the project in Unity and press Play.

JSON format:
```json
[ { "id": 101, "maLoai": 4, "cx": 1, "cy": 1 }, ... ]
```
- `cx` is the column, `cy` is the row (both 1-based).
- Grid size is detected automatically from the largest `cx` and `cy`.

## Canvas and grid
- The grid UI is placed under a standalone `GridCanvas` root, separate from the bootstrap/controller object.
- Canvas render mode is **Screen Space - Camera**. The controller uses its assigned `Canvas Camera`, otherwise `MainCamera` (and creates one if needed).
- You can assign an existing Canvas and Camera to `UiGridController`; if no Canvas is assigned, it creates the standalone Canvas at runtime.
- The Canvas hierarchy is `GridCanvas > Viewport > Content`. Only visible cell views are active; views leaving the viewport are reused while panning.
- Set `Cell Prefab` to a uGUI prefab containing an `Image` or another `Graphic`. If left empty, simple Image cells are created automatically.
- Cells are clickable via coordinate conversion without a collider or raycast. Clicking logs the cell `id`, coordinates, and type.
- The `CellIdClicked` event exposes the clicked id to other scripts.

## Controls
- Left or middle mouse drag: pan the grid.
- Left click without dragging: select a cell.
- Mouse wheel: zoom around the cursor.

The old world-space `GridController` is disabled when the Canvas grid bootstraps.
