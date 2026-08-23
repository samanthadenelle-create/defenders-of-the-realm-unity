> ## RECONCILED 2026-08-08 - true status is NOT STARTED
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: commit `6e0cde93` added this `.md` file ONLY - no climb/exit volume code landed with it. CONFIRMED VISUALLY in the owner's 2026-08-08 F8 screenshot of `dg_bonecrypt`: a flat Unlit green pillar still rises out of the Descend socket.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 924 — Kill neon-green climb/exit debug volumes (EXIT beams + portal placeholders)

**Status:** DONE 2026-08-23 (owner-confirmed: "WO-924 / WO-918 Both Done"). The neon-green climb/exit debug volumes and portal placeholders are gone from the dungeon floors.
**Minted:** 2026-08-07 (Grok Imagine visual review + owner 52s Development Build recording)  
**Silo:** Dungeons / presentation  
**Roles:** CLI implement  
**Related:** **WO-923** (real walkable stairs — removes need for Descend/Climb portal as design); handoff §3.1–3.2; `DungeonExitInteractable`, `DungeonPortLink`  
**Owner / Imagine review:** bright solid neon-green full-height beams scream debug; purple Climb/Descend over them; breaks immersion vs polished Heart of Elarion outdoor.

---

## 0. One-line truth

Dungeon vertical / exit affordances are **primitive Unlit green cubes and sheets**, always opaque, plus world-space **"EXIT"** text. Climb/Descend is a **portal** (`DungeonPortLink`), not architecture. The player never sees stairs — only debug volumes.

---

## 1. Grounded sources (do not re-guess)

| What the recording shows | Code |
|--------------------------|------|
| Tall neon-green vertical beams | `DungeonExitInteractable.BuildBeacon` → `Beacon_Beam` cube scale **(0.28, 6.4, 0.28)** URP/Unlit green-gold |
| Flat green door sheet / pillars | Same file: `Pillar_L/R`, `Sheet` (Unlit quad, ignores dungeon ambient — full-bright) |
| World **"EXIT"** text | `Beacon_Label` TextMesh `"EXIT"` — no depth test / billboards through walls (handoff §3.2) |
| Purple **Climb** / **Descend** | `DungeonPortLink` + `MobileInteractButton` — **no mesh on the port itself**; green nearby is exit/arch, but player reads them as “the climb thing” |
| Multi-level design | `DungeonBaker.DressVerticalStairPorts` — **only** ports; **no stair mesh** (WO-1001 1b interim) |

F8 history already named green pillars: `DungeonController` comments on “two big flat green bars fill the view”.

---

## 2. Product rules

1. **No solid neon Unlit debug geometry** in player-facing dungeon.  
2. Exit must read as **architecture** (arch, stone door, torch-lit frame) or a **subtle** interactable — not a laser pillar.  
3. **Multi-level = walkable stairs (WO-923)**; Climb/Descend portal is interim only.  
4. World labels: no multi-EXIT billboards through walls; prefer compass / proximity prompt only.  
5. Colourblind: do not rely on green alone for “safe exit.”

---

## 3. Scope

### Phase A — Exit beacon demote (required, fast)

`DungeonExitInteractable.cs`:

1. **Remove or hide** `Beacon_Beam` (tall green cube).  
2. **Remove** always-on ASCII `"EXIT"` **or** gate it: only when hero within ~8 m, with depth-tested TMP, not Legacy TextMesh through walls.  
3. Replace `Sheet` Unlit full-bright with **Lit** emissive soft seal (or KayKit doorway mesh if available) — must respond to ambient so it does not dominate the frame after WO-919 dark dungeons.  
4. Soften pillar scale/material to stone (Lit), not neon Unlit.

### Phase B — Port markers (until WO-923 lands)

`DungeonPortLink` / baker:

1. **Do not** add green cubes to mark ports.  
2. Optional: small floor **glyph** or torch-lit niche; prompt already says Climb/Descend.  
3. When **WO-923** stair prefab is present for a pair → **disable** that pair’s ports entirely (no Climb UI on empty air).

### Phase C — Acceptance vs recording

- Capture same dungeon exit / stair room: **no** full-height solid green beams.  
- EXIT text not visible through solid walls from mid-dungeon.  
- Outdoor Heart scene unchanged.

### Phase D — Out of scope

- Combat animation (WO-926).  
- Foot fire (WO-925).  
- Building full stair mesh (WO-923 — separate, higher product value for multi-level).

---

## 4. Files

| File | Action |
|------|--------|
| `Assets/_Modules/Dungeons/DungeonExitInteractable.cs` | Beacon/sheet/pillars/label |
| `Assets/_Modules/Dungeons/DungeonExitBeacon.cs` (if split) | Billboard depth / hide |
| `Assets/Editor/RoomForge/DungeonBaker.cs` | Only if port markers added by bake |
| WO-923 | Retire ports when stairs exist |

---

## 5. Acceptance

- [ ] No neon-green full-height debug beams in play.  
- [ ] Exit readable without dominating frame under dark ambient.  
- [ ] No multi-EXIT labels through walls.  
- [ ] Climb/Descend not paired with green debug pillars (prompt-only or real stairs).  
- [ ] `COMPILE_GATE_OK` + capture PNG open.  

## RESULT

`WorkOrders/WORK_ORDER_924_dungeon_green_debug_exit_climb_volumes.RESULT.md`
