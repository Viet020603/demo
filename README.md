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
- In the Unity Editor, run **Tools > Grid Demo > Create Popup Prefabs** (optional). It creates `Assets/Resources/UI/Popup/Grid/PopupGrid.prefab` and `Assets/Prefabs/UICell.prefab` (skipped if they exist).
- The Canvas prefab is a standalone scene root with editable `Viewport`, `Content`, and `SelectionMarker` children, plus the `UiGridController` settings. Adjust the prefab or scene instance before Play.
- Canvas render mode is **Screen Space - Camera**. The controller uses its assigned `Canvas Camera`, otherwise `MainCamera` (and creates one if needed).
- The popup hierarchy is `PopupGrid > Viewport > Content > SelectionMarker` (+ `BtnClose`). Only visible cell views are active; views leaving the viewport are reused while panning.
- `Content > MapBackground` shows `Assets/Resources/Sprite/dat.png` and pans/zooms with the grid. Replace it via `Background Sprite` on `UiGridController` (or the `MapBackground` Image in the prefab); when empty, `Resources/Sprite/dat` is loaded.
- Layout matches the reference: `Content` = grid + `Border Cells` (default 4) on every side. The ground covers it all, the 31x31 grid is drawn in the middle as `GridLines` (one mesh, one draw call; color/thickness are editable), and cells are transparent (`Cell Tint Alpha` = 0) so only icons and the orange selection show.
- `Content > MapFrame` is an optional decorative frame (trees, clouds, water) with a transparent center. Assign your sprite to its Image (it enables itself when a sprite is set); it is drawn above the ground and below the grid.
- The camera starts zoomed out to fit the whole map (`Fit Map On Start`); drag and wheel zoom stay clamped to the map edges.
- To show icons on cells, put them in a child Image of the cell prefab; the root Image receives the per-type tint.
- `UICell.prefab` is the pooled cell template. Edit its `Image` or add UI components, then assign it in `UiGridController`'s `Cell Prefab` field if needed.
- If the prefab builder has not been run, the controller retains a runtime fallback that creates the Canvas hierarchy and simple Image cells.
- Cells are clickable via coordinate conversion without a collider or raycast. Clicking logs the cell `id`, coordinates, and type.
- The `CellIdClicked` event exposes the clicked id to other scripts.

## Controls
- Left or middle mouse drag: pan the grid.
- Left click without dragging: select a cell.
- Mouse wheel: zoom around the cursor.

## Start button / popup flow

- `PopupBase` (abstract: `Init`, `Show`, `Close`, `Dispose`) is the base for popups; it reports open/close to `GameManager`.
- `UiGridController` now derives from `PopupBase`. It is built in `Init` but stays hidden until `Show`.
- `UiManager` creates a **START** button (or use your own: assign `Start Button` / `Grid Popup`). Pressing it hides the button and opens the grid. `Esc` closes the grid and shows the button again.

## ScrollRect + clickable cells

- The viewport has a `ScrollRect` (drag with inertia / elastic edges; toggle `Inertia`). The mouse wheel zooms `Content` instead of scrolling.
- Every visible cell is a pooled `Button` (`UICell.prefab` has `Button` + `Image`). Clicking a cell moves the orange marker and logs/raises `CellIdClicked(id)`. Hook your own logic there or edit the cell prefab.
- Needs an `EventSystem` (created by `UiManager`) and a `GraphicRaycaster` on the canvas (added automatically).
- Regenerate `GridCanvas.prefab` / `UICell.prefab` (delete them, run the menu) to pick up the new components; the runtime fallback adds missing ones anyway.

## Popup pattern (no Canvas / Camera changes)

- `UiGridController : PopupBase` no longer creates or edits a Canvas, CanvasScaler or Camera. `UiManager.GetPopupGrid()` instantiates the prefab (`Resources/UI/Popup/Grid/PopupGrid`, or `Popup Grid Prefab`) under `Panel Ui In Game` (empty = the first Canvas in the scene), then `Init(this)` once; `Show()` / `Close()` toggle it, `BtnClose` and `Esc` close.
- Click/zoom coordinates use the camera of the parent Canvas (none for Overlay), so any render mode works.
- The Start button is also created under that panel (or assign your own `Start Button`). A Canvas is created only if the scene has none.
- Delete the old `GridCanvas.prefab` if you still have it; it is no longer used.
