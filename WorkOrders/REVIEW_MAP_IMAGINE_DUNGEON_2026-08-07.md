> ## RECONCILED 2026-08-08 - true status is ROUTING MAP VALID - ALL TARGETS NOT STARTED
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. The priority-to-WO routing below is sound, but
> every WO it routes to (920, 921, 923, 924, 925, 926) is NOT STARTED at HEAD.
> WARNING: commit 6e0cde93's subject says "land WO 923-926" - it did NOT land that code. Its diff is
> `.gitignore` plus `TimelineSettings.asset` plus 5 `.md` files. It landed the DOCUMENTS, not the code.
> Do not read that commit subject as completion. A Status line has been added - the file previously had none.

# Visual review map — Grok Imagine (Development Build 52s) → WOs

**Status:** ROUTING MAP VALID - ALL TARGETS NOT STARTED (reconciled 2026-08-08)  
**Date:** 2026-08-07 · **Recording:** dungeon Grom Lv5 + exit to Heart of Elarion  
**Contrast noted:** outdoor Heart looks polished; dungeon reads unfinished.

| Imagine priority | Grounded cause | WO | Pass? |
|------------------|----------------|-----|-------|
| 1. Neon-green climb/exit beams + Climb/Descend placeholder | `DungeonExitInteractable` Unlit pillars/sheet/Beacon_Beam + `"EXIT"`; multi-level = **`DressVerticalStairPorts` only** (no stairs) | **924** (green debug) + **923** (real stairs) | **Yes — P0 dungeon** |
| 2. Combat stiff / foot slide / recovery / shield clip | Animator / root motion / layers (needs measure) | **926** | After dungeon P0 or parallel anim seat |
| 3. Permanent foot fire particles | Likely `HeroHpStateAura` NearDeath TinyFlames stuck or always Drive — **instrument first** | **925** | **Yes — P0 VFX** |
| 4. Floating EXIT / TIX3 + camera clip | EXIT labels § handoff 3.2; camera OTS/clip → **920**; TIX3 = find tutorial/debug string | **924** + **920** | Yes |
| Dark enemies hard to read | Ambient + no candle play (handoff 3.5) | **921** + candle seam later | After enclose |
| Real multi-level walk | Portals only in baker | **923** | **Yes — product P0** |

## Pass order (recommended)

1. **923** walkable stairs (kills portal-as-design)  
2. **924** remove green debug exit volumes  
3. **925** foot fire  
4. **920** dungeon camera stability  
5. **926** combat anim (larger; can parallel if separate owner)  

Already open and still valid: **919–922** enclose/wider/fire cosmetic.
