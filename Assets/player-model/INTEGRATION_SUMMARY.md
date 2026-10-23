# Dark Necromancer GLB Integration Summary

- **Authoritative Asset**: `Assets/player-model/dark_necromancer_-_corrupted_staff__skulls.glb`
- **Active Visual Mesh**: `Assets/player-model/DarkNecromancer.obj`
- **Active Material**: `Assets/player-model/M_DarkNecromancer.mat` (Universal Render Pipeline/Lit, Double-Sided)
- **Vertex Count**: 13,37,416
- **Triangle Count**: 18,93,733
- **Calibrated Scale**: 1.8361
- **Target AR Height**: 1.80m
- **Ground Offset**: Local Y = 0.90m (Feet grounded exactly at Y = 0.00m)
- **Rotation**: Euler(-90, 0, 0) for upright Z-up to Y-up orientation facing +Z forward
- **Hitbox**: Radius 0.50m, Height 1.80m, Center Y = 0.90m (Functional & Visually Hidden)
- **Target Reticle**: Ground level (Local Y = 0.02m, Diameter = 1.20m)
- **UI Anchor**: Local Y = 2.00m (0.20m above character apex)
- **Authoritative Runtime Prefabs**:
  - `Assets/Prefabs/PlayerCharacter_Prefab.prefab`
  - `Assets/Resources/PlayerCharacter_Prefab.prefab`
  - `Assets/Prefabs/OpponentCharacter_Prefab.prefab`
- **Prefab Hierarchy**:
  ```
  PlayerCharacter
  ├── GameplayRoot
  ├── CharacterView
  │   └── DarkNecromancer_GLB
  ├── Hitbox
  ├── TargetRing
  ├── Effects
  └── UIAnchor
  ```
- **Active Scene**: `Assets/Scenes/Battle_DebugSimulation.unity`
- **Integration Status**: 100% COMPLETE & VERIFIED
