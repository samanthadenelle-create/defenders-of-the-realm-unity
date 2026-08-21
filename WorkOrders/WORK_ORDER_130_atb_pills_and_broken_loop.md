<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 130 — ATB: Enemies Still Render as Pills + Battle Loop Feels Broken

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Date:** 2026-05-30
**Priority:** OWNER DECISION REQUIRED — see "Keep / Park / Cut" flag below. Spec'd because the owner reported it live; do not start CLI work until the owner confirms ATB is worth fixing now.
**Scope:** Medium — one swapper rewrite (enemy model), two binding/UX fixes, no engine changes
**Reported (owner):** "Bug in ATB — enemies are still pills, and the whole ATB still feels broken."
**Module:** `DeNelle.BattleATB` (assembly) / `DeNelle.BattleATB` namespace
**Investigated:** READ-ONLY. No `.cs` edited, no bakes fired, no scenes touched.

---

## ⚠️ KEEP / PARK / CUT FLAG (owner reads first)

`docs/NORTH_STAR.md` line 87-90 puts the **ATB battle** in the **"off to the side
(pulled focus — owner decides: keep / park / cut)"** bucket, alongside Defend-the-Tower,
dungeons, and the monetization stack. The note asks of each: *"does it feed the
CREATE → HARVEST → DEFEND loop, or sit beside it?"*

ATB does **not** feed the core loop today — it is a side combat mode reached only via a
village tree-breach "Last Stand" or a dungeon encounter. Before CLI spends a cycle on
this, **the owner should decide:**
- **KEEP** → implement the fixes below (it is genuinely close — the engine is sound and
  fully unit-tested; the visible defects are presentation/wiring, not combat logic).
- **PARK** → leave as-is, hide the entry points, revisit post-launch.
- **CUT** → remove the breach→ATB handoff and the scene from Build Settings.

**Recommendation: KEEP-but-defer.** The pill bug and the loop polish are small, well-isolated
fixes (no engine risk — see §"Engine is healthy" below), so the mode can be made
presentable cheaply. But it should not jump the SUNDAY core-loop queue. Fix it in a quiet
lane after the CREATE→HARVEST→DEFEND priorities, OR park it. Owner's call.

---

## 🚫 Do NOT touch

- **Do NOT edit the ATB engine** (`Engine/*.cs`: `Turn`, `Combat`, `Ai`, `Targeting`,
  `BattleState`, `Defs`). It is a pure, byte-for-byte TS port with a full passing test
  suite (`Tests/*Test.cs`). The reported "feels broken" is **not** an engine defect — see below.
- **Do NOT hand-edit `Assets/Scenes/ATBBattle.unity`** (CLAUDE.md §3 corruption history).
  If a scene-ref re-bake is needed it is run by CLI via
  `-executeMethod DeNelle.Editor.BattleSceneBuilder.BuildBattleScene` — **not** by the UI,
  and **not** while the editor is open. The fixes below are deliberately designed to need
  **no scene re-bake** (the swapper self-installs at runtime; binding is code-side).
- **Do NOT regress** `BattleController.OnEnable`/`Start` split — the comment at
  `BattleController.cs:124-131` documents a hard-won fix (UIDocument root not built in
  OnEnable). Leave the Start()-binds-HUD ordering intact.
- Keep `?.` null-conditional on all cross-module / optional-singleton calls
  (`ATBCombatManager.Instance?.…`, `CoreServices.*`).
- Run the brace-balance gate (CLAUDE.md §1) on every `.cs` file touched.

---

## Engine is healthy (why "feels broken" is NOT the combat math)

Confirmed by reading `ATBRuntimeState.cs` + `Engine/Turn.cs`:
- The turn pipeline (`AdvanceToNextTurn` → `BeginNextTurn` → `SubmitAction` →
  `ResolveAiTurn`) is intact with loop guards. `StartBattle` runs the intro and
  auto-advances to the first hero turn; `ChooseAction` drains all AI turns to the next
  hero turn; `DeriveResult` settles Victory/Defeat at `BattlePhase.Ended`;
  `OnOutcome` fires once. Damage, targeting, win/lose, and the scene hand-back
  (`ReturnAfterResult` → `ResolveReturnScene` → `LoadSceneWithFade`) are all wired and
  were static-verified intact in WORK_ORDER_11_atb_battle_check.RESULT.md.
- WO-21 restored the dropped `_runtimeState` ref; WO-68/93/94 already wired the turn
  timer, attack animation trigger, item/skill paths, source-tagging, and the
  return-scene guard. Those are landed in the current `BattleController.cs` /
  `ATBCombatManager.cs`.

So the residual "feels broken" is **presentation + a few binding gaps**, addressed in
Issues 2-4 below. Issue 1 (pills) is the headline.

---

## Issue 1 — Enemies render as a tinted CAPSULE PILL instead of a real enemy model  ★ headline

### Symptom
The hero capsule is swapped for the player's class FBX, but the **enemy** stays a plain
capsule primitive (just recolored violet). Owner: "enemies are still pills."

### Root cause
`Assets/_Modules/BattleATB/AtbCombatantSwapper.cs`:
- The swapper **deliberately does not load an enemy model.** `TintEnemy()`
  (lines 121-134) only paints the existing `EnemyCapsule` renderer "Hollow-One violet."
- The reason given in the header comment (lines 14-15) and `TintEnemy`'s caller is now
  **STALE / FALSE:**
  > "there is NO runtime enemy model in Resources — the KayKit skeleton lives in the
  > gitignored Assets/Models and is edit-time only."
- That is no longer true. **`Assets/Resources/Enemies/` now exists and contains loadable
  runtime FBX models:** `Skeleton_Minion.fbx`, `Skeleton_Warrior.fbx`, `Skeleton_Rogue.fbx`,
  `Skeleton_Mage.fbx`, `Skeleton_Golem.fbx`, `Necromancer.fbx`, `Dragon.fbx`, plus
  `HumanoidEnemy.controller`, `LargeEnemy.controller`, and skeleton materials under
  `Assets/Resources/Enemies/Materials/`.
- The hero path already proves the pattern works:
  `SwapHero()` (lines 57-106) does `Resources.Load<GameObject>("Heroes/" + slug)`, sizes
  it into the capsule slot, recenters by world bounds, URP-fixes materials, and hides the
  pill. The enemy just never got the same treatment.

### Fix (precise) — `AtbCombatantSwapper.cs`
Replace the tint-only `TintEnemy(Transform capsule)` with a `SwapEnemy(Transform capsule)`
that mirrors `SwapHero`, loading from `Resources/Enemies/`:

1. **Guard re-entry** like the hero: `if (capsule.Find("AtbEnemyModel") != null) return;`
2. **Resolve the enemy model slug** from the live battle's first enemy def id. The engine
   def id is the source of truth — read it from the active battle. Two viable sources
   (use whichever is cleanest without adding an asmdef dependency):
   - Preferred: read the first enemy `BattleUnit` from `ATBRuntimeState.Battle` (the
     `BattleController` already has `_runtimeState`; the swapper currently uses reflection
     for GameState, so reflecting `ATBRuntimeState.Battle` / `Enemies()` to get the first
     enemy `Name`/def is consistent with the file's existing reflection style), OR
   - Acceptable fallback: have `BattleController` expose the active enemy def id (e.g. a
     `public string ActiveEnemyModelSlug { get; }`) and let the swapper read it by
     reflection. Keep the swapper's no-extra-asmdef contract (it is a `static` util that
     reflects into Core/State today).
3. **Map the engine def id → a Resources/Enemies FBX name.** The engine keys are in
   `Defs.ENEMY_DEFS`: `goblin, skeleton, bruiser, necromancer, hollow-captain, hollow-king,
   hollow-apprentice`. Suggested mapping (closest available runtime FBX):
   | engine def id        | Resources/Enemies FBX   |
   |----------------------|-------------------------|
   | `skeleton`           | `Skeleton_Minion`       |
   | `goblin`             | `Skeleton_Rogue`        |
   | `bruiser`            | `Skeleton_Warrior` (or `Skeleton_Golem`) |
   | `necromancer`        | `Necromancer`           |
   | `hollow-captain`     | `Skeleton_Warrior`      |
   | `hollow-king`        | `Dragon` (boss) or `Skeleton_Golem` |
   | `hollow-apprentice`  | `Necromancer`           |
   | (unknown / fallback) | `Skeleton_Minion`       |
   Owner may retune which FBX maps to which archetype — this is a creative call, flag it.
4. **Instantiate + place** exactly like `SwapHero`: capture the capsule's renderer bounds
   as the "slot," `Instantiate(prefab, capsule)`, name it `AtbEnemyModel`,
   `localPosition = Vector3.zero`. Enemy stands on the RIGHT facing the hero on the LEFT
   (-X), so set yaw so the model's visual forward points toward the hero (likely `0°` for
   the KayKit skeletons, which face -Z by default — tune if it reads backward; the hero
   uses `180°` because Tripo heroes face local -X).
5. `StripCamerasAndColliders(model)` (reuse the existing helper).
6. URP material fix: the KayKit enemies may import fine, but apply the same
   `DeNelle.Core.TripoMaterialFixer` add-component guard the hero uses, OR assign the
   skeleton material from `Assets/Resources/Enemies/Materials/skeleton_texture_A.mat`
   (loadable via `Resources.Load<Material>("Enemies/Materials/skeleton_texture_A")`) if the
   model imports magenta.
7. **Normalize height + recenter by world bounds** (reuse `NormalizeHeight` + `ModelBounds`
   + the slot-delta reposition block) so the off-center FBX pivots don't fling the mesh away
   (the same trap documented in `SwapHero`).
8. **Hide the original capsule renderers** (`r.enabled = false`) so the pill disappears —
   but **keep the `EnemyCapsule` transform**, because `BattleController.ApplyCapsuleState`
   tilts it on death and the model is its child (death-tilt still works, same as hero).
9. Update `TrySwap()` (line 52-53) to call `SwapEnemy(enemy.transform)` instead of
   `TintEnemy(enemy.transform)`. Keep the `TintEnemy` as a fallback only if
   `Resources.Load` returns null (e.g. model missing on a fresh clone) so the enemy is at
   least a clearly-colored pill rather than magenta — `Debug.LogWarning` (not error) on the
   miss, per CLAUDE.md §4.
10. Delete or update the stale header comment (lines 14-15) — it no longer reflects reality.

### Acceptance criteria
- [ ] Entering ATBBattle (via breach or dev direct-play) shows a **real skeleton/enemy mesh**
      on the right, not a capsule pill, not magenta.
- [ ] The enemy model faces the hero and is sized to roughly the hero's height.
- [ ] On enemy death, the model tilts over (death-tilt via `ApplyCapsuleState` still fires).
- [ ] A missing FBX (fresh clone, no Resources/Enemies) logs a `LogWarning` and falls back
      to a tinted pill — never magenta, never a null-ref.
- [ ] No new asmdef reference added to `DeNelle.BattleATB` for this (Resources.Load +
      existing reflection only).

---

## Issue 2 — In-editor (UXML) battle exposes only the Attack button; Skills / Item / Flee are dead

### Symptom
When the battle is opened in the editor, only **Attack** works. Skills, Item, and Flee
appear to do nothing (or are absent). Contributes to "feels broken."

### Root cause
`Assets/_Modules/BattleATB/UI/BattleHUD.uxml` only declares a single
`<ui:Button name="attack-button" .../>` in the command deck (line 71). There are **no**
`skills-button`, `item-button`, or `flee-button` elements. But `BattleController.BindUi()`
(lines 735-738) queries all four by name, so in the UXML path `_skillsButton`,
`_itemButton`, and `_fleeButton` bind to **null** and their handlers never wire.

In **player builds** this is masked: CLAUDE.md §8 says "UXML in builds does NOT work," and
`BindUi()` detects the empty `attack-button` and calls `BuildFallbackHud()` (lines 760-829),
which **does** build all four buttons. So the bug is **editor-only** but it is exactly where
the owner playtests.

### Fix (precise) — two options, pick the cheaper:
**Option A (recommended, no scene/asset bake):** Add the three missing buttons to
`BattleHUD.uxml`'s `command-deck` (lines 70-72), matching the names `BindUi` queries:
```xml
<ui:Button name="attack-button" text="Attack"  class="action-button action-button--attack" />
<ui:Button name="skills-button" text="Skills"  class="action-button" />
<ui:Button name="item-button"   text="Item"    class="action-button" />
<ui:Button name="flee-button"   text="Flee"    class="action-button" />
```
(`.uxml` is not a `.cs` file — the brace gate / Windows-mount `.cs` rule does not apply, but
still use the Write/Edit tools, not bash redirects, to be safe.)

**Option B:** Make `BattleController.BindUi()` always run `BuildFallbackHud()` (force the
code-built deck even when `attack-button` binds), aligning with CLAUDE.md §8 "always use
code-built UI." This guarantees parity between editor and build and removes the UXML
dependency entirely. Requires touching `BattleController.cs:742-752`.

Owner/CLI choose A (fast, keeps UXML) vs B (kills the UXML divergence for good). B is more
in line with the documented "code-built UI wins" lesson.

### Acceptance criteria
- [ ] In the **editor**, all four commands (Attack / Skills / Item / Flee) are present and
      respond — Skills casts, Item heals (potions seeded), Flee exits a Dungeon-source fight.
- [ ] Editor and player-build HUDs expose the same four commands.
- [ ] Flee remains hidden when `_source == BattleSource.Village` (Last Stand is do-or-die).

---

## Issue 3 — Only the FIRST enemy is shown, even when a breach sends a roster of up to 6

### Symptom
A village breach can hand off up to 6 enemies (`BuildEnemyRoster`, cap `MaxEnemies = 6`),
but the scene shows one enemy capsule and the HUD shows one enemy card. A multi-enemy
breach reads as "broken" — you kill one and the fight ends, or extra enemies are invisible.

### Root cause
`BattleController.Render()` (lines 492-505) renders exactly one enemy via
`FirstUnit(state, Side.Enemy)` into the single `_enemyCard` / `_enemyCapsule`. The scene
(`BattleSceneBuilder.CreateCombatantCapsule`) builds exactly one `EnemyCapsule`. The engine
fully supports up to `MAX_ENEMIES = 8`; the **presentation** is single-enemy only.

### Fix (precise) — scope per owner:
This is the largest of the four and is **optional polish**. Minimum viable:
- **MVP (recommended for now):** cap the ATB breach roster to **1 enemy** so the visuals
  match the engine. In `BattleController.BuildEnemyRoster` change `MaxEnemies` handling to
  take only the first breaching id (or set `const int MaxEnemies = 1;`). The fight then
  honestly shows the one enemy it renders. Cheapest path to "not broken."
- **Full (defer):** render N enemy cards + N enemy models (loop the roster, instantiate one
  model per enemy offset along +X, add a HUD enemy card per foe, retarget Attack via a
  picker). This is a real feature, not a bug fix — spin a separate WO if the owner wants it.

### Acceptance criteria (MVP)
- [ ] The number of enemy models/cards shown equals the number of enemies the engine is
      actually fighting (1 in MVP).
- [ ] Killing the visible enemy ends the fight with a Victory (no invisible survivors).

---

## Issue 4 — Idle "ATB pressure" bar may read as static; confirm it drives the HUD fill

### Symptom
Owner historically reported "ATB bars don't move" (WO-93). The turn timer
(`ATBCombatManager.TurnProgress`) exists and is reset on every action, but the **enemy ATB
fill** in the HUD is driven only by the engine snapshot (`unit.Atb / ATB_FULL`) which is
event-stepped, not real-time — so between actions the bars can look frozen.

### Root cause
`BattleController.RenderCombatant` sets the ATB fill from the engine's discrete `unit.Atb`
(lines 527-537), which only changes when a turn resolves. `ATBCombatManager.TurnProgress`
(the real-time idle-pressure value) is **not** bound to any HUD bar — there is no per-frame
`Update()` in `BattleController` pushing `TurnProgress` into a fill. So the "pressure"
visual the WO-93 design intended never animates.

### Fix (precise) — `BattleController.cs`
Add a lightweight `Update()` that, while `ATBCombatManager.Instance?.IsActive == true` and
the hero is awaiting input, drives the **hero ATB fill** from
`ATBCombatManager.Instance.TurnProgress` (0→1 as the idle timer counts up), then snaps back
on action. Keep it null-guarded (`?.`) and a no-op when the manager is absent (tests /
direct-play). Do **not** touch the engine `unit.Atb` — this is a HUD-only overlay on the
hero bar to make the idle clock visible.

(Alternatively, if the owner prefers the bars to reflect only engine state, document that
the ATB bar is event-stepped and remove the WO-93 "idle pressure" expectation. Owner's
call on whether the real-time pressure bar is wanted.)

### Acceptance criteria
- [ ] The hero ATB bar visibly fills toward the auto-attack threshold while the player idles,
      and resets when they act (or the design is explicitly changed to event-stepped and
      documented).
- [ ] No engine state is mutated by the HUD; manager-absent path is a clean no-op.

---

## Files to edit (CLI)

| File | Action |
|---|---|
| `Assets/_Modules/BattleATB/AtbCombatantSwapper.cs` | **Edit** — replace `TintEnemy` with `SwapEnemy` loading from `Resources/Enemies/`; update stale comment (Issue 1) |
| `Assets/_Modules/BattleATB/UI/BattleHUD.uxml` (Option A) | **Edit** — add `skills-button` / `item-button` / `flee-button` (Issue 2) |
| `Assets/_Modules/BattleATB/BattleController.cs` (Option B / Issues 3 MVP & 4) | **Edit** — force code HUD (B), cap roster to 1 (3-MVP), add `Update()` ATB-pressure bind (4) |

**No scene re-bake required** for Issues 1, 2A, 4. If Option B or a roster change touches
serialized fields, no new fields are added, so still no bake. If a bake is ever deemed
necessary it is CLI-only via `BattleSceneBuilder.BuildBattleScene` with the editor closed.

---

## Before reporting done (CLI checklist)
- [ ] Brace balance check passed on every `.cs` file edited
- [ ] No `.unity` scene hand-edited; no bake fired with the editor open
- [ ] `?.` used on all `ATBCombatManager.Instance` / `_runtimeState` / Resources-miss paths
- [ ] No new asmdef dependency added to `DeNelle.BattleATB`
- [ ] Missing-FBX path logs `LogWarning` (not error) and falls back gracefully
- [ ] Engine (`Engine/*.cs`, `Tests/*`) left untouched

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `AtbCombatantSwapper.cs:70,294; BattleController.cs:790` — pills->models, UXML HUD retired. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
