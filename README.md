# Unity Grid Strategy Demo (31x31)

Unity version: **6000.3.20f1** · Uses the **new Input System** package.

Repo: https://github.com/Viet020603/demo

## Features
- 31x31 data-driven grid (no 961 per-cell GameObjects)
- Drag to pan the camera (left or middle mouse), clamped to the map
- Viewport culling: only objects inside camera view are active
- Objects are shown automatically from grid data; initial content is seeded
- Every cell is clickable via mouse -> cell math (no colliders)
- Selection marker, save/load/clear
- Debug Gizmos:
  - Red = map bounds
  - Cyan = camera view rect
  - Yellow = culling influence rect (camera + padding)

## Pool behavior (updated)
- Fixed pool capacity can be matched to camera-visible cell capacity.
- When camera moves, cells leaving view are released to pool; new visible cells reuse those pooled objects.
- Runtime instantiate can be disabled for strict fixed-capacity behavior.

## Controls
- Left drag / middle drag: move camera
- Left click (no drag): select cell and toggle object
- Right click: remove object
- Mouse wheel: zoom
- `S` save, `L` load, `C` clear

## Setup
Open with Unity 6000.3.20f1, open `Assets/Scenes/GameScene.unity`, press Play.

If you see *"You are trying to read Input using the UnityEngine.Input class"*, this version already fixed it by using `UnityEngine.InputSystem` (`Mouse.current`, `Keyboard.current`). Make sure **Project Settings > Player > Active Input Handling** is `Input System Package (New)` or `Both`.
