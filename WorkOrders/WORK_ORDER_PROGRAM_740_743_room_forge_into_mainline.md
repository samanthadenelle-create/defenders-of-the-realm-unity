<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-18
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-18) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# PROGRAM — WO-740 → WO-743 · Room Forge into regular build

**Status:** READY FOR CLAUDE / CLI  
**Date:** 2026-07-18  
**Source branch (has the code):** `feat/room-forge-dungeon-baker`  
**Target (regular build):** `wip/village2-and-f8-tickets` (or current mainline WIP)  
**Next free after this block:** **744**  

**Claude handoff packet:** this file + the four WO specs below.

---

## Goal

Bring the **Room Forge + DungeonBaker + default room kit + KayKit shared wall/floor mats** work from the feature branch into the **regular daily build branch**, verify it compiles and runs from menus, and leave canon/WOs closed so the team does not re-invent it.

---

## What’s already on `feat/room-forge-dungeon-baker` (do not re-greenfield)

| Area | Paths / menus |
|------|----------------|
| Runtime sockets | `Assets/_Modules/Dungeons/RoomForge/` (`RoomSocket`, `RoomSocketType`, `RoomPrefabMeta`, `DungeonComposeLayout`) |
| Editor | `Assets/Editor/RoomForge/` (`RoomForgeWindow`, `DungeonBaker`, `DefaultDungeonRoomsBuilder`, `RoomForgeMaterials`) |
| Editor asmdef | `Assets/Editor/DeNelle.Editor.asmdef` — references `DeNelle.Dungeons` |
| Layouts | `StreamingAssets` + `Resources` `Data/Canonical/dungeon-layouts/` (`d4_sunken_crypt_spine.json`, `demo_branching_kit.json`) |
| Prefab folder | `Assets/Dungeon/Rooms/` (may only have `.gitkeep` until WO-741) |
| Commits (approx) | `ecb55e53` scaffold · `ef6f6920` KayKit mats + defaults + carousel |

**Menus (after merge + recompile):**

1. `Defenders/Dungeon/Ensure Room Forge Materials (KayKit atlas)`  
2. `Defenders/Dungeon/Build Default Room Prefabs`  
3. `Defenders/Dungeon/Room Forge`  
4. `Defenders/Dungeon/Bake Compose Layout (default spine)`  
5. `Defenders/Dungeon/Bake Compose Layout From Selected JSON`  

---

## Work order order

| Order | WO | Title | File |
|------:|----|-------|------|
| 1 | **740** | Merge Room Forge into mainline + CompileGate | `WORK_ORDER_740_room_forge_merge_mainline.md` |
| 2 | **741** | Generate default rooms + materials smoke | `WORK_ORDER_741_room_forge_default_prefabs_smoke.md` |
| 3 | **742** | Bake demo layout + soft-lock free scene | `WORK_ORDER_742_room_forge_bake_demo_smoke.md` |
| 4 | **743** | Canon, README, RESULT close | `WORK_ORDER_743_room_forge_mainline_canon_close.md` |

**Serial:** 740 → 741 → 742 → 743.  
**Do not** start 741 until 740 CompileGate is green on the **target** branch.

---

## Paste starter for Claude

```
You are integrating Room Forge into the regular Defenders build.

BOOT:
1. Read WorkOrders/WORK_ORDER_PROGRAM_740_743_room_forge_into_mainline.md
2. Read WORK_ORDER_740, then 741–743 in order
3. Source branch = feat/room-forge-dungeon-baker (code already exists — merge/cherry-pick, do not rewrite)
4. Target = current mainline WIP (wip/village2-and-f8-tickets unless owner says otherwise)

RULES:
- Sole committer discipline; stage by explicit path (no git add -A)
- No UXML; no hand-edit shipping .unity scenes
- Dual-copy dungeon-layouts JSON when touched
- Brace/NUL gate on every .cs
- CompileGate after merge before claiming done
- Write RESULT.md per WO

START at WO-740 only.
```

---

## Out of program

- Endless seed composer  
- Full KayKit art-dressed rooms (placeholder shells + prop carousel is enough for mainline)  
- Replacing healers-cottage `DungeonLayout` wall-run format  
- Google Play / barracks CoC WOs (separate programs)  
