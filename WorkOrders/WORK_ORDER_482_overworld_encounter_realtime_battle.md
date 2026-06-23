# WORK_ORDER_482 — Overworld Encounter → Real-Time Battle Instance

**Status: DESIGN SETTLED (owner 2026-06-23) — slice plan below; confirm the ONE fork in §B, then implement.**
**Owner directive:** 2026-06-23. North star: `docs/COMBAT_PIVOT_NORTHSTAR.md`.
**Builds on:** WO-481 (Tripo Knight + Orc family). **Separate from ATB** — ATB is its own untouched battle system.
**Canon:** [[combat-pivot-single-hero-northstar]], [[atb-flat-vs-overworld-animated-combat]], [[tripo-roster-knight-orcs-first]].

---

## Goal
Stand up the V1 combat loop the owner settled on 2026-06-23:

> **Walk** the world → **engage** a wandering enemy representative → **pop into a dedicated real-time
> battle instance** where the full enemy family is staged → fight (animated, real-time) → **win returns**
> you to exactly where you were.

The **Tripo Knight** (hero class) fights the **Tripo Orc family** (`Orc_Warrior` leader + `Orc_Tank` + `Orc_Mage`)
in the battle instance, using the EXISTING real-time combat stack. This is the correct home for the animated
family staging that slice 2c wrongly put inside ATB ([[atb-flat-vs-overworld-animated-combat]]).

## Why this shape (owner rationale, captured)
- **Engagement is the hook** — the overworld is light; the fight is the event.
- **Isolation dodges WO-453.** The hard open problem is OuterWorld+castle baked stacked at one origin with a
  warp seam (dual-navmesh). A **separate battle instance has its own clean navmesh** — no seam, no warp. The
  thing that's blocked in-world combat for weeks simply isn't in the path.
- **Perf spine (northstar "full sim only when looked at").** Overworld = cheap wandering reps → **larger
  terrain is affordable** because "not as much happening." The full family + full combat cost is paid ONLY
  inside the contained battle, and (with a Single-load instance) the big terrain is **unloaded during the
  fight** — memory freed, so the explorable world can be bigger still.
- **Background matches source** — engaged outside ⇒ OuterWorld-style backdrop; engaged in castle ⇒ castle
  backdrop. Never a teleport to a void arena. (Source scene is already known — `SceneRouter.Return.Scene`.)
- **Fully isolated battle = fully isolated TESTING (owner 2026-06-23).** Because the fight is a self-contained
  instance with no overworld dependency, the headless fleet + `DataRegression` can hammer the battle
  deterministically on its own — combat is verifiable without the streamed world. (Reinforces the §B container choice.)

---

## A. Architecture (mirror the proven ATB round-trip, NEW destination)
The ATB breach already does: `SceneRouter.GoBattle(BattleParams)` → stash `PendingBattle` + `ReturnPoint`
(source scene + hero pose) → Single-load `ATBBattle` → on resolve, return-point restore warps the hero home.
We mirror this for a **real-time** route — NOT through ATB:

- **New handoff:** `EncounterParams { string[] FamilyIds; string BackdropContext; string ReturnScene; int Threat; }`
  + `SceneRouter.GoEncounterBattle(EncounterParams)` → stash + Single-load the battle instance + reuse the
  existing `ReturnPoint` stash/restore (already captures source scene + hero pose — that IS the backdrop signal).
- **`BackdropContext`** is derived from the source scene at engage time: `OuterWorld`/`Village2`/`MainCastle_Hall`
  → `"outerworld"` vs `"castle"`. The battle builder selects skybox/ground/ambient/silhouettes from it.

## A2. ARCHITECTURE REFINEMENT (owner 2026-06-23) — generalize the Arena, open kite arena, logic/presentation split
The repo already has a VERIFIED battle loop: `DeNelle.Village.Arena.ArenaMode` (async-PvP) does
enter→spawn→real-time-fight→win/lose→reward→return with FULL combat reuse (`EnemyOutpost` garrison are real
`Enemy`; hero auto-fights via `TargetManager`/`PlayerAttackController`/`HeroAbilities`; `BattleLock`, BGM,
`OnRaidEnded`). Three owner refinements shape how we reuse it:

1. **GENERALIZE the Arena into ONE class for both modes.** Extract the shared battle spine out of `ArenaMode`
   (lifecycle + win/lose watcher + reward + return + `BattleLock` + BGM + `OnEnded`) into a generic
   **`BattleArena`** controller. Both the **PvP raid** (fort garrison) and the new **PvE encounter** (open-arena
   orc family) drive it. **GENERALIZE BY EXTRACTION, NOT REWRITE** — the verified PvP path must keep working
   (regression-guard it); PvE reuses the same spine with a different combatant-set.
2. **NO structures in the PvE battle — an OPEN KITE ARENA.** The PvE encounter does NOT spawn an `EnemyOutpost`
   fort / `OutpostFoundationGenerator` geometry. It is just a **large bounded open space** (~28–35 × 18–22, big
   enough to kite), hero south + orc family north. So PvE is SIMPLER than PvP: combatant-set = orc family via
   `EnemyFactory` + `EnemyBrain` roles only.
3. **Logic/presentation separation (the HP-B2B law) holds in battle.** LOGIC (combat resolution, win/lose, and
   **which hero abilities are ALLOWED = read from the skill tree**) is presentation-agnostic; PRESENTATION
   (model load, animation, VFX, HUD) is a separate skin. The generic `BattleArena` core is logic; it reuses the
   existing presentation (EnemyFactory models, `HeroAbilities` VFX, the HUD bridge).

## B. CONTAINER — DECIDED by the owner design doc (§G): additive `BattleArena` + `CombatManager`
**The owner's authoritative design doc (§G below) settles the container: an ADDITIVELY-loaded `BattleArena`,
with a `CombatManager` singleton that persists across scenes and keeps the open world in memory.** This
SUPERSEDES the earlier "Single-load to unload terrain" recommendation — additive keeps the overworld resident
so "return exactly where you were" + state-restore are trivial, and the open world is not torn down.
- `CombatManager` (DDOL singleton) captures `{hero, enemy pack, area theme}` on engage, loads `BattleArena`
  additively, runs the arena round-trip, and on end returns to the open world (unload/fade) + leashes survivors.
- `BattleArena` is **code-built** (runtime-created additive scene + code geometry + runtime NavMesh bake +
  theme), so there is **no hand-edited `.unity` and no bake dependency** (§3). Themed modular prefabs can swap
  in for the first-pass code geometry later.

---

## C. Slice plan (each slice delivered complete + headless-verified before the next)
**Slice 1 — Orc family in the OuterWorld/battle spawn path (reusable, design-independent).**
- `EnemyAnimatorFactory`: add `EnemyRig.OrcHumanoid`; `RigFor` `Orc_Warrior/Orc_Tank/Orc_Mage` → `OrcHumanoid`;
  `Controller(OrcHumanoid)` → `"OrcHumanoid"` (controller already in `Resources/Enemies`).
- `EnemyFactory.ModelForEnemy`: `orc-warrior→Orc_Warrior`, `orc-tank→Orc_Tank`, `orc-mage→Orc_Mage`.
- `EnemyFactory` yaw/material block: treat `OrcHumanoid` like `OrcWarband` (-90° yaw + `FixTripoMaterials`),
  AND bind the per-orc basecolor fallback (`Enemies/OrcTex/Orc_*_basecolor`) on the `TripoMaterialFixer` — the
  new Tripo orcs ship external textures and render WHITE without it (the exact issue slice 2c fixed in ATB).
  → needs a `FallbackTexture` path on `SkinOptions` (passed to the fixer) OR a post-Skin `SetFallbackTexture`.
- Code `EnemyDef`s for the 3 orc ids (mirror the ATB engine stats in `Defs.cs`; orc ids are not in enemies.json).
- **Verify:** headless spawn → orc family renders TEXTURED (not white/magenta/T-pose), plays `OrcHumanoid` clips.

**Slice 2 — Overworld representative + controlled engagement + the chase.**
- A cheap wandering **rep mob** (one per family) in OuterWorld with **owner-controlled aggro range + patrol**
  (reuse `EnemyBrain` tactical/patrol + leash; reps do NOT fight in-world — they are the hook only).
- **ON AGGRO (owner 2026-06-23):**
  - **Chase-music sting** — swap to an early "they see us" chase cue to denote detection (reuse `CoreServices.Audio`
    music A/B crossfade / `AbilityAudioBridge.PlayDangerSting`-style hook).
  - **Wide-leash chase** — the rep pursues far (not a tight tether) once it detects you.
  - **Speed ≈ player +5% (or very close)** — a too-high-level mob you wandered into **can't be trivially outrun**,
    so getting caught (→ forced engage) MEANS something. This IS the danger-gradient soft-gate
    ([[world-architecture-gated-regions-playable-connectors]]): toughness telegraphed by "you can't escape this one."
- Proximity/contact engage → `SceneRouter.GoEncounterBattle(EncounterParams{ family, backdrop=sourceScene, return })`.
- Auto-spawn (NOT a hotkey — [[never-dragdrop-or-manual-playtest]] bans playtest hotkey detection).

**Slice 3 — Generic `BattleArena` + PvE open-arena orc fight + battle HUD.**
- **Extract** the generic `BattleArena` spine from `ArenaMode` (lifecycle/win-lose/reward/return/`BattleLock`/BGM/
  `OnEnded`) — PvP delegates to it unchanged (regression-guarded); add a **PvE entry** `BattleArena.BeginEncounter(
  EncounterParams)`.
- **Open kite arena** (NO fort): a large bounded flat space (~28–35 × 18–22) themed to the source context;
  runtime NavMesh via `ArenaNavMeshBaker` only if the spawn space lacks a baked mesh (else reuse existing mesh).
  Hero south, orc family north (1–6, staggered/loose) via `EnemyFactory` + Slice-1 wiring + `EnemyBrain` roles
  (Warrior=leader/DPS, Tank=Tank, Mage=Ranged/Healer).
- **Real-time combat is 100% reused** (`TargetManager`/`PlayerAttackController`/`HeroAbilities`/`HeroHealth`,
  hero-aggro DEF-224). Hero abilities **gated by the skill tree** (logic). **Battle HUD** via `ArenaHudBridge` +
  the existing combat HUD (hero HP/mana/abilities) + enemy bars + Flee.
- Victory → reward + return via the existing `ReturnPoint`/`OnEnded` round-trip (consume the rep mob).
- **Verify:** headless — Knight + family spawn in the open arena, engage, exchange damage, victory returns to source.

**Slice 4 — Knight art promotion (parallel/after; needs Unity editor import).**
- Promote the armored Tripo Knight (`Assets/Art/Incoming_Tripo/Heroes/Knight/`) → import Humanoid → retarget the
  donor `Knight.controller` → into `Resources/Heroes` (WO-481 Phase 1/4). Until then the existing animated Knight
  body fights fine.

## D. Flags (all reversible — canon law)
- New `ff.overworldencounter` (default OFF until the vertical is felt-verified) gates the rep-spawn + the route.
- `ff.knightonly` ON, `ff.singlehero` ON, `ff.blinkarmor` OFF, `ff.basebuilding` OFF (unchanged).

## E. What NOT to touch
- **ATB stays untouched** — its own separate system (engine, `BattleController`, `ATBBattle` scene, `AtbCombatantSwapper`).
- No hand-edited `.unity` (§3) — the battle scene is editor-built + code-populated.
- No companion/party/Blink-armor revival; no base-building/waves/troops (V2).
- Do NOT regen `MainCastle_Hall`.

## F. Acceptance (the vertical, felt-verified by PO)
Walk OuterWorld → meet a wandering orc rep (controlled aggro/patrol) → engage → **pop** into a real-time battle
instance with an **OuterWorld-matching backdrop** + battle HUD → the Knight and the full orc family fight with
**visible animation** (orc wind-up telegraphs, Knight swings/abilities) → win → **return to where you were**.
Headless-verified at each slice; **rebuild the exe before the felt-verify** (stale-exe lesson, [[atb-flat-vs-overworld-animated-combat]]).

---

## Notes
- §0: all `.cs` via Write/Edit on the Windows path; brace + NUL gate via `CompileGate.Run` before any commit.
- Slot into the master backlog numbering (`CLI_LANES_WO_NUMBERS.md`) — 482 picked as filesystem-max+1; confirm.
