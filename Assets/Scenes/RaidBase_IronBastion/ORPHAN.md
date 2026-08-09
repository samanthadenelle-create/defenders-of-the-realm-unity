# RaidBase_IronBastion — ORPHAN (WO-932)

This scene exists on disk but is **not** in:

- `EditorBuildSettings` (cannot load via `SceneRouter.GoRaid`)
- `RaidSelectionVM` flagship ids
- `scene-configs.json` as a live enemy raid row (if missing)

**Do not** surface it in the Raids UI until:

1. A full `scene-configs.json` entry exists  
2. `RaidBaseGenerator` / nav bake has been run  
3. The scene is registered in Build Settings  

Owner choice: **keep** as future tier-4 camp, or **delete** the folder to reduce confusion.
