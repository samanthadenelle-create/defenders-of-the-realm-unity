# WORK ORDER 813 — Barracks discovery / teach (dialogue + raid safety net)

**Status:** DONE (reconciled 2026-08-09 from the tree - commit `fb2939f7` landed the barracks teach plus the raids empty-army safety net, with the Raids button in `2598f2f7`. NOT felt-verified; no `.RESULT.md`)

**Status:** SHIPPED 2026-07-31 (fb2939f7 — barracks teach + raids empty-army safety net). Teach quality re-opened as WO-822 (813b).  
**Minted:** 2026-07-30 (Claude UI seat) · **CLI guidance pass:** 2026-07-30  
**Lane:** Onboarding / Progression (dialogue + raid entry UX)  
**Origin:** owner — *“there is a gap. We never show the barracks… some dialogue and raid tutorial?”*  
**Related:** **WO-812** (ADD placeable Barracks — **must land first or in parallel** so teach has a building) · WO-806 (Barracks UX) · WO-774 (raid loadout) · WO-810 (Rumor Board)  

---

## ★ CLI guidance (read before implementing)

### How this relates to WO-812
| WO | Owns |
|----|------|
| **812** | **Presence:** catalog placeable `barracks`, free first place, structure exists in world, train entry on placed instance |
| **813** | **Discovery:** dialogue + marker + “Train N troops” + Raids empty-army redirect + light first-raid coach |

**If you only ship 813:** coach points at nothing (CastleBarracks often missing).  
**If you only ship 812:** building exists, player may never find train → raid.  
**Ship order:** **812 presence first (or same sprint) → 813 teach.** Soft-depend 813 acceptance on “Barracks exists in world.”

### Numbering note
Claude also dropped `WORK_ORDER_812_echo_harvest_choice_and_affinity.md` — that **collides** with CLI’s `WORK_ORDER_812_introduce_barracks.md`. Echo affinity should be renumbered to **next free after banner** (do not use 812). Treat **812 = Barracks placeable** as authority.

### Owner ruled direction (keep)
**B + C** — proactive teach + need-time safety net.  
**Not** “only placeable” and **not** “raid tutorial epic” as the whole fix.  
Option **A** (catalog build) is owned by **812**, not this WO.

### Code corrections for Claude’s “Today’s reality”
Mostly right; tighten:
- Unlock is `FeatureFlags.Barracks` (**default ON** in code) **AND** `Onboarded` — comments saying default OFF are **stale**.
- Drillmaster only spawns if `CastleBarracks` (or future placed barracks) exists — injectors **no-op** if missing.
- Raid HUD icon path may be dead (RaidRequested unused); **Herald / `RaidSelectionScreen.Open`** still open raids. Safety net **C** must hook **every** raids entry (Herald + bridge + Dev if needed), not only a deleted glyph.

### Scope trim (do not gold-plate)
**In for 813:**
1. **Post-Onboarded once:** coach/Sylas dialogue + map marker to Barracks / drillmaster.  
2. Yarn (or existing structure dialogue) beat: train → soldiers for raids; ends with **Train 3 troops** (or 1 Footman if softer).  
3. **Raids entry with 0 deployable troops:** never empty deploy — toast/modal “You need soldiers. Visit the Barracks.” + marker.  
4. **Optional light first-raid coach** (one-time): only **after** player has ≥1 troop and opens Raids — deploy-edge hint + “wounded recover” on return. Keep short; deep loadout/ring is **WO-774**.

**Out of 813 (defer):**
- Full CoC first-raid multi-step cinematic  
- Placeable catalog (812)  
- Barracks panel layout polish (806)  
- Troop power deltas (807)  

### Implementation notes for CLI
| Beat | Suggested hooks |
|------|-----------------|
| Once dialogue | `SeenTutorials` / MarkTutorialSeen key e.g. `barracks_intro` after `Onboarded` |
| Marker | existing quest/highlight registry if any; else temporary world ping / compass objective |
| Train N | quest or simple counter on `ArmyStorage` train complete / EnqueueTraining grant |
| Empty army | `RaidEntryBridge` + `ArenaHeraldSpawner` + `RaidSelectionScreen` / pre-deploy open |
| Copy | Sylas or drillmaster voice; ASCII-only; Elarion not Avalon |

### Acceptance (implementable)
- [ ] Fresh player after onboard gets **one** Barracks intro without external help  
- [ ] Marker/dialogue only fires once  
- [ ] Raids with **zero** deployable troops **never** opens empty field deploy without redirect  
- [ ] With troops, Raids still opens selection/deploy normally  
- [ ] Works even if player never opened Rumor Board  
- [ ] If Barracks still missing in world: FlowTrace.Fail + copy “Barracks not found” (do not softlock); 812 fixes presence  

### Do NOT
- Assume bake always exists without 812  
- Duplicate a second full raid tutorial system next to WO-774  
- Gate `ff.raid` off as a substitute for teaching  

---

## Ruled direction — the teaching flow (owner sign-off shape)

1. **Dialogue beat (B):** after tutorial closes (`Onboarded`), once: coach — soldiers needed; drillmaster at Barracks; marker; Yarn teaches train→raid; task **Train 3 troops** (or agreed N).  
2. **Raid safety (C):** Raids with zero troops → Barracks redirect + marker, never empty deploy.  
3. **Light first-raid coach (optional thin):** first open of Raids **with** troops — short deploy-edge + wounded-recover lines only.

## Original options (A/B/C) — status

| Option | Status |
|--------|--------|
| **A** Buildable Barracks | **WO-812** owns |
| **B** Teach quest/dialogue | **This WO** |
| **C** Raids empty-army redirect | **This WO** |

## Files (expected)
- Dialogue / Yarn barracks or Sylas nodes  
- `RaidEntryBridge`, Herald, maybe `RaidSelectionScreen` / `RaidDeployScreen` open guards  
- Quest or SeenTutorials keys  
- Marker / highlight registry  

## Claude follow-up (if needed)
Write final Yarn lines + one mock of empty-army redirect modal only; do not re-argue A vs B.
