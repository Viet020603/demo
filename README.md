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
- In the Unity Editor, run **Tools > Grid Demo > Create Canvas Prefabs and Scene Instance** before Play. This creates `Assets/Prefabs/GridCanvas.prefab` and `Assets/Prefabs/UICell.prefab`, then adds a `GridCanvas` prefab instance to the open scene.
- The Canvas prefab is a standalone scene root with editable `Viewport`, `Content`, and `SelectionMarker` children, plus the `UiGridController` settings. Adjust the prefab or scene instance before Play.
- Canvas render mode is **Screen Space - Camera**. The controller uses its assigned `Canvas Camera`, otherwise `MainCamera` (and creates one if needed).
- The Canvas hierarchy is `GridCanvas > Viewport > Content > SelectionMarker`. Only visible cell views are active; views leaving the viewport are reused while panning.
- `Content > MapBackground` shows `Assets/Resources/Sprite/dat.png` and pans/zooms with the grid. Replace it via `Background Sprite` on `UiGridController` (or the `MapBackground` Image in the prefab); when empty, `Resources/Sprite/dat` is loaded.
- `UICell.prefab` is the pooled cell template. Edit its `Image` or add UI components, then assign it in `UiGridController`'s `Cell Prefab` field if needed.
- If the prefab builder has not been run, the controller retains a runtime fallback that creates the Canvas hierarchy and simple Image cells.
- Cells are clickable via coordinate conversion without a collider or raycast. Clicking logs the cell `id`, coordinates, and type.
- The `CellIdClicked` event exposes the clicked id to other scripts.

## Controls
- Left or middle mouse drag: pan the grid.
- Left click without dragging: select a cell.
- Mouse wheel: zoom around the cursor.
