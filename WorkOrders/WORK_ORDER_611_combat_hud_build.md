# WORK ORDER 611 — Build the Combat HUD (WO-609 Phase 3 implementation, owner-designed)

**Status:** READY TO IMPLEMENT — overnight build. Owner-designed live 2026-07-05 (interactive view iteration).
**WO number 611 PROVISIONAL** (authority = MASTER_PIPELINES_BACKLOG; confirm on mint).
**Lane:** HUD / Presentation (HudKit + Core UI factory + Village producers + posture).
**Supersedes/extends:** WO-609 (prefab-first battle HUD — the data/producers already landed). This WO builds the **visual + behavior** layer the owner designed on top of it.
**Visual spec (source of truth):** the frozen iteration view `v8-spec-freeze` (scratchpad `battle-hud-view.html`) mirroring `Assets/Resources/Data/Canonical/hud-areas.json` `hostile(prebattle|activebattle)`.
**Reuse law:** REUSE-FIRST. Build through the `ElarionUiKit` factory + `hud-areas.json` data rows. Code-built uGUI (NO UXML in builds). No Village↔HUD asmdef edge. Sprite-first with procedural fallback (null art never blanks).
**Gate:** `COMPILE_GATE_OK` + brace/NUL on every `.cs`. **Push HELD for owner felt-pass** (ten-year-old test).

---

## Owner behavior rules (BINDING — the spine of this HUD)
1. **Entering HOSTILE posture → every other screen CLOSES and HIDES.** On the posture arc flipping to `hostile(prebattle|activebattle)` (SceneOwnership hostile / pursuit / wave / BattleLock), `PanelManager.CloseAll()` — no modal, shop, panel, or town screen may linger over a fight.
2. **The combat HUD becomes THE active screen.** When hostile, only the combat HUD occupancy renders; friendly/town chrome is out.
3. **SIMPLE — nothing else.** Do not add widgets beyond the spec below. The empty centre stays the fight.

---

## Layout spec (per zone — the owner's design)

| Zone | Widget | Design decision (owner) |
|---|---|---|
| **vitals** (TL) | `playerNameplate` | HP + MP bars **recessed in an inset well INSIDE the plate** (no edge-bleed). Portrait + name + Lv. |
| vitals | `playerBuffRow` | small status icons, unchanged. |
| **status** (TC) | `targetFrame` | enemy portrait + name + **LEVEL beside the name** (gold, serif) + **animated LOCKED crosshair badge** (see below). Enemy HP bar. |
| status | `castBar` (activebattle) | enemy cast telegraph. |
| status | `enemyBuffRow` | small debuff/buff icons. |
| **system** (TR, activebattle) | `fleeButton` + `settingsButton` | unchanged. |
| **moveCluster** (BL) | **VIRTUAL D-PAD (cross)** | ⚠ **Canon reversal (owner 2026-07-05):** replace the 4-round-button `BuildControllerCluster` (A4 §1.11) with a proper **cross/plus virtual d-pad image** (steel body, gold directional chevrons, centre hub). Revives the `VirtualDPadLean` seam rather than the 4-button cluster. Update HUD_OBSIDIAN §1.11 canon to match. |
| **actionBar** (BC) | 4 hot-swap + HP/MP potions | **HOUSED in an obsidian panel** (gold-trim inner ring). Hot-swap = tree-assigned skills; potions = round. |
| **actionRail** (BR) | attack + Q/W/E/R | **ABILITY ARC:** the **attack = an oblong stadium PILL** (gold-trimmed, energy sword — Blink Obsidian Action-bar art `Action_Bar_Slot`/the pill art) anchored bottom-right corner (thumb rest); **Q/W/E/R = round gold medallions arcing up-left around the attack** (right-thumb reach), NOT a flat row. |
| **feedback** | stamps | pooled/capped combat text (already built). |

**Cooldowns:** **soft under-glow** treatment on the ability medallions (a soft gold radial glow that depletes) — owner pick over a hard clock-sweep. Apply via the kit's cooldown draw on `BuildActionSlot`/the arc discs.

**Lock crosshair badge (target frame):** small badge in the enemy nameplate showing lock state via the **`Cursors_Obsidian/Crosshair_1|2|3`** frames as a **lock-on animation**: `Crosshair_1` = unlocked (faint/wide) → `Crosshair_2` = acquiring (amber pulse) → `Crosshair_3` = locked (tight/gold). Bind to `TargetModel.Locked` / the acquire transition.

---

## Fixes to FOLD IN (data-proven this session)

### F1 — Blank ability icons (ABILITY_ICON_AUDIT_2026-07-05.md — "always an image")
All 11 HUD-reachable abilities render **blank** today (producers pass the glyph `"✦"` as the icon key; `ConceptIconResolver` only maps abilityId/effect → null → icon disabled, no placeholder). Art exists (`RpgUi/icons/*`).
- **A.** `HudModelProducers.cs:401` (`AbilityLoadoutProducer`) + `:653` (`AssignableLoadoutProducer`): set the record icon key from `def.Id`/`def.Effect` via `ConceptIconResolver.ResolveAny(id, effect)`, NOT `def.Icon`.
- **B.** `ElarionUiKitObsidian.cs:767` `ActionSlotHandle.SetIcon`: when `s == null`, substitute `ConceptIconResolver.DefaultSprite()` (`icon_combat`) instead of disabling the Image → a blank ability slot becomes structurally impossible.

### F2 — Import the crosshair frames
`Cursors_Obsidian/Crosshair_1|2|3.png` are NOT imported to `Resources/RpgUi` (HUD_OBSIDIAN §3.5 parked cursors). Add the 3 to `BlinkUiImporter.BuildTable` (role `hud` → `hud/crosshair_1|2|3`, Simple) so the lock badge can resolve them. Fallback: a vector crosshair if art absent.

### F3 — Enemy level source (for "Lv beside name")
`TargetProducer` currently derives level from an **HP/25 heuristic** (Enemy exposes no `Level`). DECISION: add a real `Level` to the enemy def/`Enemy` and feed `TargetModel.Level`, OR keep the heuristic for V1. Recommend a real field (small) so the shown level is truthful. **Owner call.**

### F4 — Missing currency art (peripheral)
`RpgUi/currency/*` doesn't exist → resource chips resolve null. Out of scope for combat HUD (town chrome), but note for the icon-guarantee sweep.

---

## Silos (file-disjoint — for parallel edit agents, §11)
1. **Importer:** `Assets/Editor/BlinkUiImporter.cs` (F2 crosshair frames).
2. **Icon fix + kit fallback:** `Assets/_Modules/Village/HUD/HudModelProducers.cs` + `Assets/_Modules/Core/UI/ElarionUiKitObsidian.cs` (F1) + `ConceptIconResolver.cs` if a `ResolveAny` helper is needed.
3. **Layout widgets:** `Assets/_Modules/HUD/Kit/HudKitController.cs` + `Assets/_Modules/Core/UI/ElarionUiKit.cs` (d-pad, oblong attack pill, ability arc, action-bar panel, HP/MP inset, lock crosshair badge, soft-glow cooldown).
4. **Posture close-behavior:** `PostureEvaluator.cs` / `HudContextEvaluator.cs` + `PanelManager` (rule 1: hostile → CloseAll; rule 2: combat HUD is the active screen).
5. **Target level:** `HudModelProducers.cs` `TargetProducer` + `Enemy` def (F3, if real level chosen).
6. **Data (already matches):** `hud-areas.json` (both mirrors) — verify rows == spec.

## Acceptance
- [ ] Hostile posture closes all panels; only the combat HUD renders (rule 1+2), traced via `[Flow:HudKit] posture`.
- [ ] D-pad (cross), oblong attack pill, Q/W/E/R medallion arc, housed action bar, HP/MP inset, enemy Lv beside name, animated lock crosshair badge — all present, prefab/sprite-first with fallback.
- [ ] **No blank ability icon** (F1) — every equipped slot shows art; grep proves no `SetIcon(null)`-disables-without-fallback path.
- [ ] Crosshair_1/2/3 import green (F2).
- [ ] `COMPILE_GATE_OK`; brace/NUL clean; headless autopilot verifies posture + a UI capture. **Push held for owner felt-pass.**

## Do NOT
- Add anything beyond the spec (owner: "simple, nothing else").
- Break the friendly/town HUD, the fill-binding contract (§1.1), or add a Village↔HUD edge.
- Hand-edit `.unity`. Regenerate via builders.
