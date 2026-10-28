# Unity Grid Strategy Demo (31x31)

Unity version: **6000.3.20f1** · Uses the **new Input System** package.

Repo: https://github.com/Viet020603/demo

## Quick start (objects are created in the scene, not at runtime)
1. Open the project, open `Assets/Scenes/GameScene.unity`.
2. Menu **Tools > Grid Demo > Build Scene Objects**
   (or select `GameBootstrap` and click **Build Scene Objects** in the inspector).
3. The Hierarchy now contains `Main Camera`, `GridRenderer`, `Objects/ObjectPool/Cell_000...` and `SelectionMarker`.
   Everything is visible in the Scene view, so you can adjust size, color, position, zoom, padding, etc. before Play.
4. Save the scene (Ctrl+S) and press Play.

On Play the pool objects are hidden and reused: only cells inside the camera view are shown, and they are recycled when the camera pans. Nothing is instantiated at runtime.

Re-press **Build Scene Objects** after changing grid size, cell size, zoom or padding so the pool capacity matches the camera view.

## Features
- 31x31 data-driven grid (no 961 per-cell GameObjects)
- Drag to pan the camera (left or middle mouse), clamped to the map
- Viewport culling with a fixed, pre-built pool
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
