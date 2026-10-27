# Unity Grid Strategy Demo (31x31) - Complete Auto-Bootstrap

Unity version: **6000.3.20f1**

Repo: https://github.com/Viet020603/demo

## What is completed
- Full Unity project scaffold
- Auto-bootstrap on scene load (no manual object setup required)
- 31x31 data-driven grid (no 961 per-cell GameObjects)
- Place/remove objects on cells
- Camera pan/zoom
- Save/Load/Clear shortcuts

## Controls
- Left click: place object
- Right click: remove object
- Middle mouse drag: pan camera
- Mouse wheel: zoom
- `S`: save
- `L`: load
- `C`: clear all

## Clone and run
```bash
git clone https://github.com/Viet020603/demo.git
```
1. Open Unity Hub
2. Add project folder `demo`
3. Open with Unity `6000.3.20f1`
4. Open `Assets/Scenes/GameScene.unity`
5. Press Play

## Notes
- Runtime automatically creates required bootstrap objects if missing.
- If `placePrefab` is not assigned, system uses a fallback Quad visual so demo always works.
