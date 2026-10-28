# Unity Grid Strategy Demo (31x31)

Unity version: **6000.3.20f1** · Uses the **new Input System** package.

Repo: https://github.com/Viet020603/demo

## How objects are spawned
On Play, `GridController` **checks the real camera size** (`orthographicSize` and `aspect`), computes how many cells the camera can see (+ `cullPadding`) and creates **only that many objects** (just the missing ones if some already exist in the scene). Nothing else is instantiated.

- While you pan the camera, objects leaving the view are returned to the pool and reused for cells entering the view.
- If you zoom out / the window aspect changes, the capacity is re-checked and the pool grows only when needed (it never shrinks, so it is reused).
- The console logs e.g. `Camera size=6, aspect=1.78 -> needs 168 objects. Created 168, pool total 168.`

## Optional: preview objects in the editor
Menu **Tools > Grid Demo > Build Scene Objects** (or the button on `GameBootstrap`) pre-creates camera, grid, pool objects and selection marker in the scene so you can tweak them before Play. At runtime the camera is still checked and any missing objects are created automatically.

## Features
- 31x31 data-driven grid (no 961 per-cell GameObjects)
- Drag to pan the camera (left or middle mouse), clamped to the map
- Camera-size based pool + viewport culling
- Every cell is clickable via mouse -> cell math (no colliders)
- Selection marker, save/load/clear
- Gizmos: red = map, cyan = camera view, yellow = culling area

## Controls
- Left drag / middle drag: move camera
- Left click (no drag): select cell and toggle object
- Right click: remove object
- Mouse wheel: zoom
- `S` save, `L` load, `C` clear

## Notes
- Set **Project Settings > Player > Active Input Handling** to `Input System Package (New)` or `Both`.
- Optional: assign `placePrefab` on `GridController` to use your own sprite/prefab for pool items.
