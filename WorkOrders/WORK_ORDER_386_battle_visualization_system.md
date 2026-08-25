# WORK_ORDER_386 — Battle Visualization System

**Status:** BLOCKED on the §0 TARGET decision — UNDERSTAND PHASE COMPLETE (documented below) — design/build pending the TARGET decision (§0). Architecture recommended.
**Lane:** 2 Combat/AI + 4 UI/HUD (visualization layer over the ATB engine). Code-only.
**Source:** This session (2026-06-09). Owner-authored WO + verified investigation.

## Goal
Make the existing battle **simulation visible** to the player — from a simulated battle to *seeing* it: troops, combat, movement.

---

## §0. PREMISE CORRECTION + the one decision (read first)
The WO assumes "a battle sim runs in the background (earns kill-EXP) but nothing is visualized." Investigation (2026-06-09) found there is **no invisible headless sim** — there are **two** combat systems, and the target must be picked:

| System | Visualized today? | Spatial positions? | Grants kill-EXP? |
|---|---|---|---|
| **Arena raid** (`DeNelle.Village.Arena`) | YES — spawns real `Enemy` GameObjects + fort; hero/party auto-fight | YES (real Transforms/NavMesh) | YES (garrison deaths → ProgressionManager) |
| **ATB combat** (`DeNelle.BattleATB`) | Barely — scene `ATBBattle.unity` renders only **2 capsules** (hero + first enemy); everyone else is a HUD card | **NO — abstract, no Vector3 anywhere** | indirectly (outcome handed back) |

- **If "the simulated battle you want to SEE" = ATB** → this is the real Battle Visualization project: the engine is position-free, so the visualizer **synthesizes deterministic positions** and renders the full roster. (RECOMMENDED target — matches "simulated → seen".)
- **If = Arena raid** → it already visualizes; "shows nothing but grants EXP" is a **BUG, not a missing visualizer**: `EnemyOutpost.SpawnGarrison` (EnemyOutpost.cs:254–260) calls `Clear()` when `_aliveCount==0` (no NavMesh under the raid anchor / empty roster) → fires `OnCleared` → `ArenaMode` pays a silent win with zero combatants drawn. Fix = verify NavMesh/roster at the anchor, or treat spawn-failure as a loss. Track separately if this is the symptom.

**Architect recommendation:** target the **ATB** for the visualization system; file the Arena auto-clear as its own small bug-fix WO if that's the symptom you've been seeing.

---

## §1. The simulation code (Task 1)
### ATB (the abstract turn sim — the visualization target)
- Entry: `Turn.StartBattle(state)` → `BeginNextTurn` — `Assets/_Modules/BattleATB/Engine/Turn.cs:253, :102`.
- Headless run loop: **`Turn.AutoResolveBattle(state, maxTurns=5000)`** — `Turn.cs:268` — `while` until `BattlePhase.Ended`, calling `SubmitAction`/`ResolveAiTurn`/`BeginNextTurn`.
- Tick model: **event-step, not real-time.** `AdvanceToNextTurn` (`Turn.cs:33`) jumps to the smallest ATB fill that pushes a unit to `ATB_FULL=100` — no time-based bar. The bar animation in `BattleController.TickVisualAtb` (`BattleController.cs:165`) is cosmetic only.
- Scene host: `Assets/Scenes/ATBBattle.unity`, driven by `BattleController.cs`.

### Arena raid (already-visual real-time combat)
- Entry: `ArenaMode.TryStartRaid(ArenaOpponentDef)` — `Assets/_Modules/Village/Arena/ArenaMode.cs:102`. NO sim loop — it stakes a wager, spawns a real `EnemyOutpost`, and delegates the fight to real-time combat; watches win (`_outpost.OnCleared`, :131/:173) + loss (`WatchForLoss` poll, :195).
- Kill→XP (shared by waves, camps, AND arena): `Enemy.Die(killed)` → **`ProgressionManager.ReportKill`** (`Enemy.cs:1121`) → `ProgressionManager.Distribute` (`ProgressionManager.cs:99`): `baseXp=max(6, MaxHp*0.5)`, ×wave, split by damage share + 25% kill-credit.

## §2. Data model (Task 2)
- **ATB unit = `BattleUnit`** (`Types.cs:266`): `Id, Side(Party/Enemy), Name, Kind, Hp/MaxHp, Resource, Atb(0..100), Speed, Defense, Attack, Element, Statuses, Cooldowns, Alive` + kind-specifics. **NO spatial fields — no Vector3, no facing.** `Atb` is a turn-order scalar.
- Container = **`BattleState`** (`Types.cs:370`): `List<BattleUnit> Units` ("party first, then enemies"), `ActiveUnitId`, `TurnCounter`, `Rng`, append-only **`List<BattleLogEntry> Log`**. Sides via the `Side` enum + `BattleStateOps.LivingUnits`.
- **Arena/waves unit = `Enemy`** (`Enemy.cs`, a MonoBehaviour): positions ARE real (Transform/NavMeshAgent); stat block `EnemyDef`; defender side = `EnemyOutpost._garrison` (`List<Enemy>`).

## §3. Spawning (Task 3)
- **ATB = pure data.** `BattleStateOps.BuildHeroUnit/BuildPetUnit/BuildEnemyUnit` + `CreateBattle` (`BattleState.cs:62/95/145/191`) just `new BattleUnit{}`. No `Instantiate`/prefabs/transforms in the engine. The only GameObjects are `BattleController.Start`'s **2 placeholder capsules** + `AtbCombatantSwapper`; floating damage in `BattleController.SpawnFloatingDamage` (:768).
- **Arena = real factory spawn (reusable!).** `EnemyOutpost.SpawnBoss/SpawnGuard` → **`EnemyFactory.Build(def,pos,rot,parent)`** ("the ONE enemy creation path"); fort via `OutpostFoundationGenerator.Realize` → `StructureFactory.Create`.

---

## §4. Recommended architecture (sim → seen, decoupled replay)
The sim stays the source of truth; the visualizer **replays its deterministic log** — confirmed viable by the code:
1. **Keep the ATB engine authoritative + position-free** (don't move logic into the view; keeps it headless/fast/offline + deterministic/replayable — good for the Arena "watch a match" + CoC raid-replay pillar).
2. **Synthesize deterministic positions:** lay `BattleState.Units` into formation slots (party left / enemies right — list is already party-first). Deterministic so any replay matches.
3. **Visualizer = log/event replayer:** subscribe to `ATBRuntimeState` UnityEvents (`OnBattleChanged/OnActionSubmitted/OnTurnResolved/OnOutcome`, hooked in `BattleController.Subscribe` :173) AND/OR diff `BattleState.Log` by cursor — **generalize the EXISTING `_lastProcessedLogIndex` replay (`BattleController.TryDriveHitAndDeathAnims` :723) from 2 capsules to one spawned model per `BattleUnit`, keyed by `Id`.**
4. **Reuse, don't reinvent:** spawn models via `EnemyFactory.Build` / `VisualFactory`+`HeroAnimatorFactory`; drive `PlayAttack/PlayHit/Die` via `ActorAnimator` (already used by `BattleController`); expand `AtbCombatantSwapper` from hero+enemy to the full roster. NO new combat or animation code.

## §5. Acceptance criteria (ATB target)
- [ ] All party + enemy units render as models (not 2 capsules) in deterministic formation slots, keyed by `BattleUnit.Id`.
- [ ] Attacks/abilities/deaths play (move/swing/hit/die + floating damage) driven by the log/events — once per event (reuse the cursor; no replay-spam).
- [ ] Outcome (win/lose) + return flow unchanged; engine remains deterministic/headless-runnable (`AutoResolveBattle` still works for away/tests).
- [ ] Mobile/WebGL-safe (code-built, pooled spawns, skippable).

## §6. What NOT to touch
- Do NOT move sim/resolution logic into the view — engine stays authoritative + deterministic.
- Do NOT add per-frame real-time ticking to the engine (it's event-step by design).
- Reuse `EnemyFactory`/`ActorAnimator`/`AtbCombatantSwapper` — no parallel spawn/anim paths.
- If the symptom is actually the Arena silent-win, that's a separate bug WO (EnemyOutpost.cs:254–260), not this visualization.
