> ⚠ **SUPERSEDED — Defend-the-Tower / PatriciaLight was REMOVED 2026-06-09; not a live system.** Kept for history. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md` (single-Knight overworld BattleArena; ATB flat/separate).

# Manual QA Regression Checklist (WO-329)

The **non-headless** half of the check-in suite. The static gate and the Unity
EditMode/PlayMode tests catch compile/logic/null regressions automatically; the
items below need a **human watching the screen** and cannot be asserted in
batchmode. Run this pass before shipping a build to players (and after any change
that touches scenes, rigs, animators, HUD layout, or the world/march flow).

Extends `QA_CHECKLIST_FILLED.md` (which is the code-inspection baseline). This
file is the **runtime visual** companion — every item is a "play it and look"
check.

**How to use:** rebuild the scene if needed
(`Defenders > Week 3 > Build Village Scene`), enter Play (or load a fresh
Windows/WebGL build), and mark each row.

**Status:** PASS / FAIL / N-A. Put a one-line note on every FAIL (what you saw,
where).

| Date | Build (commit / exe) | Tester | Platform(s) |
|------|----------------------|--------|-------------|
|      |                      |        |             |

---

## 1. Hero rig & animation (no T-pose / no broken walk)
- [ ] Hero is fully skinned on spawn — **no T-pose**, no missing mesh, no grey capsule.  `____`
- [ ] Idle animation plays at rest (breathing/sway), not a frozen first frame.  `____`
- [ ] Walking forward plays the **walk/run** clip (not slide), feet roughly match speed.  `____`
- [ ] Hero faces the direction of travel — **not walking backwards**, not **90 degrees off** from move vector.  `____`
- [ ] Turning in place / sharp direction changes look correct (no spin-snapping or moon-walk).  `____`
- [ ] Attack, cast, and hit-react animations fire on the right actions.  `____`
- [ ] On death the actor **stays down** (the `Dead` bool latch) — no flicker back to idle.  `____`
- [ ] Same checks for enemies and pets (no T-pose, correct facing, death stays down).  `____`

## 2. HUD readability (web + mobile)
- [ ] HUD renders at desktop/WebGL aspect — HP/mana/resource counters legible, not clipped.  `____`
- [ ] HUD renders at a narrow **mobile** aspect (~9:16) — nothing overlaps or runs off-screen.  `____`
- [ ] Resource icons + numbers (Wood/Iron/Food/Gems) are readable and update live.  `____`
- [ ] Ability/skill bar cells are tappable-sized and cooldown overlays drain correctly.  `____`
- [ ] Text is sharp (no blur/pixelation) and contrasts against the parchment/panel backgrounds.  `____`
- [ ] No stray default-UI artifacts (e.g. the old blue Yarn Spinner button) visible.  `____`

## 3. Build / placement preview (isolated)
- [ ] Entering Build Mode shows the ghost preview tracking the cursor/finger.  `____`
- [ ] The build **preview modal renders in isolation** — only the previewed structure, neutral lighting, **no village geometry bleeding into the RT**.  `____`
- [ ] Rotation controls (+/-90, drag) work and the chosen yaw is what gets placed.  `____`
- [ ] Ghost turns red on invalid placement (overlap / off-grid) and green on valid.  `____`
- [ ] Placed structure persists with the correct rotation after confirm.  `____`

## 4. Defend-the-Tower (DTT / PatriciaLight) grounded + defeat flow
- [ ] Tower scene loads; the tower and defender are **grounded** (not floating / not sunk).  `____`
- [ ] Enemies path to the tower and attack it; damage registers visibly.  `____`
- [ ] Projectiles (arrows / spell VFX) are visible and hit targets.  `____`
- [ ] **Defeat flow** triggers correctly when the tower falls — defeat screen / return path, no soft-lock.  `____`
- [ ] Victory/clear flow works at the end of a successful defense.  `____`

## 5. Village world: gates, compass, trees, nodes
- [ ] All 4 cardinal **gates** are present, correctly oriented, and open/close (drawbridge) as intended.  `____`
- [ ] The **compass / wayfinding** points correctly (gates, objectives) and tracks as the hero moves.  `____`
- [ ] **Trees are textured** — foliage/bark materials present, **no magenta / no untextured grey** trunks or canopies.  `____`
- [ ] Tree of Life centerpiece is centered (0,0,0), dominant, and glowing.  `____`
- [ ] Ramparts are walkable and the hero stays grounded on top (no fall-through).  `____`

## 6. Node / interactable interaction
- [ ] Walking up to an interactable **node** (mine / harvest / NPC / portal) shows its prompt.  `____`
- [ ] **Node interaction works** — harvest grants resources (floating +X), NPC opens dialogue/upgrade, portal transitions.  `____`
- [ ] Pet harvesting assigns and ticks resources through the economy (counters rise).  `____`
- [ ] Building upgrade spends resources and applies the visual change.  `____`
- [ ] Dungeon portal loads the dungeon scene and back without error.  `____`

## 7. Performance & input (spot-check)
- [ ] Frame rate is acceptable on the target build (no obvious stutter spikes during a wave).  `____`
- [ ] Touch input works end-to-end on a mobile-sized build (move, tap, build, dialogue advance).  `____`
- [ ] Audio plays (music + at least one SFX on hit/cast) and is not stuck muted.  `____`

---

### FAIL log
| # | Item | What you saw | Scene / repro |
|---|------|--------------|---------------|
|   |      |              |               |
