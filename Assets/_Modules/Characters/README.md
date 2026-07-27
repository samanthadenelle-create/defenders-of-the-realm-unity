# Characters — EMPTY / RESERVED

No code lives here (no `.cs`, no `.asmdef`). This folder is a reserved slot for a
future character-systems module; nothing references it today.

Character/rig/equipment code currently lives elsewhere:
- Hero locomotion, health, equipment, seating/offsets → `Assets/_Modules/Village/`
- Behavior-tree AI → `Assets/_Modules/Core/` (`DeNelle.AI`)
- Skeleton/humanoid import + animator setup → `Assets/Editor/`
- Enemy classes / families / raid troops → `Assets/_Modules/Village/Troops/`

Do not treat this folder as a code home until it is given an assembly definition
and a purpose. Update this README (and the module table in `../README.md`) if that changes.
