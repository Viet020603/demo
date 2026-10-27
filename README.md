# Unity Grid Strategy Demo (31x31)

Unity version: **6000.3.20f1**

Repository contains a complete starter project focused on performance for a 31x31 interactable grid (no 961 cell GameObjects).

## Features
- Data-driven grid: `CellData[]` (31x31 by default)
- Mouse-to-cell math interaction (no per-cell colliders)
- Place/remove objects on cells
- Object pooling for spawned visuals
- Camera drag + zoom
- Grid line rendering in a single component
- Save/load grid state as JSON

## Project structure
- `Assets/Scenes/GameScene.unity` *(create from instructions below if scene not serialized by your setup)*
- `Assets/Scripts/Core`
- `Assets/Scripts/Rendering`
- `Assets/Scripts/Input`
- `Assets/Scripts/Gameplay`
- `Assets/Scripts/Camera`
- `Assets/Scripts/SaveLoad`

## Quick setup in Unity
1. Open with Unity **6000.3.20f1**.
2. Create a new Scene named `GameScene` in `Assets/Scenes/`.
3. Create empty GameObjects:
   - `GameBootstrap` (attach `GridController`)
   - `GridRenderer` (attach `GridLineRenderer`)
4. Main Camera:
   - Add `CameraController2D`
   - Set projection to Orthographic
5. Create a simple sprite prefab (e.g. square/circle) named `CellObject.prefab` in `Assets/Prefabs/`.
6. In `GameBootstrap/GridController`:
   - Assign `Main Camera`
   - Assign `Grid Renderer`
   - Assign `Place Prefab` = `CellObject.prefab`
   - Keep width/height = 31, cellSize = 1
7. Enter Play mode:
   - Left click: place object
   - Right click: remove object
   - Middle mouse drag: pan
   - Scroll: zoom
   - `S`: save, `L`: load, `C`: clear

## Notes
- This starter is optimized for your case: all cells are interactable, but only occupied cells have visuals.
- You can later swap rendering to Tilemap/URP shader without changing grid data logic.
