# WO-783 — SME-fan-out fix wave: raid settlement, dungeon reachability, gate ratchet, wave-authoring rot

**Status:** IMPLEMENTED (this session) — with 3 items DEFERRED to the owner, listed at the bottom
**Minted:** 2026-07-30 (CLI, from the 12-agent read-only SME fan-out + the check-in sweep)
**Lanes:** Village/Troops+Camps · Village/World · Village/Waves · Village/Harvest · Editor/Regression · Dungeons (comments)
**Verification:** `COMPILE_GATE_OK` + `DataRegression.RunAll` + `UICaptureLaunch.RunCaptureHeadless` (pixels opened, not just the marker)

---

## Method note

Every item below was found by a read-only SME agent, then **re-verified by the CLI against the tree**
before any edit (owner's gatekeeper model, 2026-07-30). Two agent claims were **refuted** on
verification and are recorded as such — proposals are not truth.

---

## 1. Raid victory never settled the army — REAL BUG, FIXED

**Evidence:** `ArmyStorage.ReconcileAfterRaid` (`Core/State/ArmyStorage.cs:216`) had exactly ONE
production caller — `Village/Troops/RaidDeployController.cs:474`, inside `DoRetreat()`.
`ArmyStorage.AddVeterancy` (`:316`) had **ZERO callers repo-wide**. So winning a raid cost no troops
and paid no veterancy; only retreating or timing out ever did. The documented "+5% damage per survived
3-star raid" ladder (`PlayerTroop.cs:44,47,60-62`) was unreachable despite being fully consumed at
`TroopDeployer.cs:85` and displayed at `RaidDeployVM.cs:191`.

**Fix:** extracted the settlement into ONE latched `RaidDeployController.ReconcileRaidEnd(int starsEarned)`
that both exits call — `DoRetreat()` passes 0, `RaidVictoryController.ReconcileArmy(result)` passes
`RaidResult.Stars`. Added `GrantVeterancy`: on a 3-star clear each survivor gets one `AddVeterancy`.

**Why the deploy controller owns it:** `_deployed` is the only place the deployed set exists. A fallen
troop's body is destroyed `DeathHoldSeconds` after death (`TroopController.cs:127,460`), so a scene scan
finds survivors only and could never reconstruct `deployedIds`.

**Why it cannot double-settle** (4 independent layers, the first sufficient alone): the `_reconciled`
latch; `RaidVictoryController._handled` (`:64,156`); `RaidScoring._finalized` blocks `OnTimeExpired`
after `Finalize(true)` (`RaidScoring.cs:239,287`); and the Retreat button is physically behind the
victory modal's raycast-blocking scrim (`EndStateView.cs:121-123` over `RaidDeployHud` at 30000).

**Why the fire point is safe:** placed after `GrantLoot` and BEFORE `ShowVictoryScreen`, whose body is
inside a try/catch — a presentation throw must never skip the settlement. Verified nothing on the
victory path tears down troops before that point; the scene only unloads at `ReturnHome`.

**Tripwires built in:** `raid-end reconcile - deployed N, survivors N, wounded 0` means the survivor
computation broke open; `3-star clear but NO surviving deployed troops` means it broke closed.

## 2. Healer's Cottage was UNREACHABLE — REAL BUG, FIXED

**Evidence:** `Village/World/DungeonWorldPortalSpawner.cs` `AuthoredPortals` had two rows. The east
row's own comment records a deliberate reroute off `Dungeon_HealersCottage` to `dg_starter_loop`; the
Cottage's row was collateral. `LoadDefs()` still returns its def (`Resources/Dungeons/HealersCottage.asset`,
`DungeonId: HealersCottage`), so `TryGetAuthored` failed and the loop logged *"has NO authored world
position ... portal not placed"* every session. The richest dungeon in the game — lore stones,
mini-boss, chests, crafting, checkpoints, real exits, 3.5x the size of the other dungeon scene — was
reachable only from the dev overlay.

**Fix:** third `AuthoredPortal` row, SOUTH: `("HealersCottage", (20,0,-140), 352f)`.
- Id must be the **bare** `"HealersCottage"` — it has to equal `def.DungeonId` for `TryGetAuthored`;
  `DungeonPortal.EnterDungeon` then resolves the scene via its `"Dungeon_" + id` fallback (no scene is
  named `HealersCottage`). Using the full scene name would place nothing.
- Seat = the east row rigidly yaw-rotated +90 deg about origin, `(x,z) -> (z,-x)`. Same 141.4m radius.
  Yaw from the same formula the existing rows use: `Atan2(-x,-z) = Atan2(-20,140) = -8.13 -> 352`.
- **Ground is provably flat there** and it is the SAFEST of the three seats: it lands in the WO-468
  cave-road corridor, which `ExteriorTerrainBuilder` pins at exactly Y=0 for `|x| <= 20` over
  `z in [-700,-76]`. The other two rows sit past `ReliefBlendRadius=140` where +-1.8m of relief applies
  and rely on the `SeatOnGround` search. Prop scatter is rejected within 37m of that road line.
- `Dungeon_HealersCottage` confirmed enabled in Build Settings (`EditorBuildSettings.asset:20-22`).

**Still UNVERIFIED (needs a run, not a read):** that the baked navmesh actually has a walkable polygon
at `(20,0,-140)`. The spawner self-reports — look for
`[Flow:DungeonPortal] placed 'HealersCottage' ... navmesh-seated=True` and `Placed 3/3`. If it reports
`False`, retune to `(16f, 0f, -140f)` (one token, 4m more corridor margin).

## 3. `[ui-obsidian]` ratchet ARMED — and a regex blind spot closed

**Evidence:** `HardFailOnNew = false` meant the suite reported 5 NEW hand-rolled-uGUI files and still
**returned true**, green-passing the gate.

**Triage (each verified, none is compile-stripped — the two "dev" ones are runtime-gated OFF instead):**
- **Allowlisted:** `FlagCaptureButton.cs`, `ResourceDevTool.cs` — dev/tester overlays whose flags
  resolve `defaultOn: IsDevBuild`, so they never spawn in a store build. Same class as the two
  overlays already allowlisted.
- **Frozen into KnownBaseline (genuine shipping debt, NOT dev tools):** `GhostPreview.cs` (the
  owner-requested "why it's red" placement label, instantiated unconditionally by
  `BuildModeController.cs:517/1889/2362`), `CollectorStackView.cs` (diegetic collector fill bar),
  `PauseHudBootstrap.cs` (dead-but-shipping `PauseHudButton` chip — resolve by DELETING the class).

**The trap that was nearly baked in:** the run reported 2 baseline entries "resolved". Only ONE really
was (`ArenaPanel.cs`, now routes through the kit). `OutpostHub.cs` was a **false resolve** — the
`StrongSmells` regexes required the bare type name right after `typeof(`/`AddComponent<`, so the
**namespace-qualified** form (`typeof(UnityEngine.UI.Image)`, `AddComponent<TMPro.TextMeshProUGUI>`)
slipped through while the file still hand-rolls raw uGUI at `OutpostHub.cs:161/183/195/208/222`.
Dropping it would have baked a false-clean into the baseline. **Patterns now tolerate an optional
namespace prefix; OutpostHub stays in the baseline.** A sweep confirmed it was the only file hidden.

**Consequence:** a new `new GameObject(..., typeof(Image))` in a shipping module now HARD-FAILS
`DataRegression.RunAll`, naming path + line + source text. It cannot be silenced without editing the
gate file itself.

**Known limits (documented, accepted):** routing is file-level — one `ElarionUiKit.` reference anywhere
exempts the whole file. And `Assets/_Modules/DevTools/**` is not excluded from the scan, so a future
tool in that compile-stripped assembly would hard-fail despite never shipping (latent, not live —
zero strong-smell hits there today).

## 4. waves.json authored schedule is DEAD — made LOUD (half fixed, ruling deferred)

**Evidence:** `WaveManager.StartWave` runs the smart path first and only falls through when it spawned
nothing (`WaveManager.cs:1254-1273`). `_smartComposition` is serialized **1** in both live hubs
(`Main_Castle_Overworld.unity:2853`, `MainCastle_Hall.unity:1619`) and both carry 4 `WaveSpawnPoint`
markers, so the smart path always succeeds and **both fallbacks are unreachable**.

**This is NOT a code regression** — `WORK_ORDER_362` says verbatim *"use new composer instead of flat
spawning"*, and the field tooltip says *"ON = ignore the flat waves.json batches"*. **The regression is
in the DATA, and it happened after the fact:** WO-362 landed mid-June; the 20-wave schedule was authored
**2026-07-11**, ~4 weeks after the batches went inert, against a port that no longer runs. waves.json's
own comments still describe the dead consumption path. **19 waves / 55 batch entries / 148 authored
enemies are discarded every session** and nothing said so.

Two proofs of real divergence: wave 3 authors `troll x2 + ogre x1` but `BrutePool(3)` cannot produce
troll or ogre before wave 6 — the set-piece is unreachable. Wave 20 authors `"enemies": []` ("the
dragon IS the wave") but generation still fields ~21 ground enemies — the intent is **inverted**.

**Fixed now:** a once-per-session `FlowTrace.Warn` at the moment of discard, naming the wave, the batch
count and the enemy total, so any capture self-reports it.

**DEFERRED — needs your ruling (see below).**

## 5. Echoes button safe-area — FIXED (and my "clipped" claim REFUTED)

I reported the button as clipped at the right edge. **That was wrong** — a pixel scan of the capture
shows it occupies x 2190..2321 of 2340 with the full bevel intact: a real 16 ref px inset, nothing cut.
The *substance* stood: 16 ref px is ~18 device px ~= **7 dp ~= 1.15mm** on the Seeker — visually flush
and inside the rounded-corner / cutout / gesture band.

**Fix:** inset raised to `3 * ElarionUi.PadPanel` = 54 ref px ~= 24 dp (~60 device px), 1.5x the
Material 16 dp screen margin, using the dp scale in `docs/SME/VISUAL_TOUCH_CONTRAST_AUDIT_2026-07-14.md`.
Authored as a deliberate multiple of `PadPanel`, not a raw literal (WO-779 spacing rule). Size, anchors,
pivot, style, copy and callback untouched. Screenshot-verifiable: `CaptureEchoRoster` reflectively
invokes the very method patched.

**Note:** there is **no safe-area helper anywhere in the project** (grep for `safeArea|SafeArea|EdgeInset`
= zero hits). This static inset is the correct interim; WO-779 §5.6 already carries the shared helper.
The pip has the mirror problem on the left edge (`anchoredPosition = (20,-150)`).

## 6. FPV camera — owner-CLOSED, comments corrected

Canon (07-26 anchor) recorded FPV default-ON as an owner choice; `PAIN_POINTS` §4 said "keep only if
felt-tested, else default third-person". **Owner re-affirmed 2026-07-30: FPV stays default-ON.**
`ff.dungeonfpv` unchanged at `defaultOn:true`. Corrected the two `DungeonCameraRig` headers that
contradicted it (one called FPV *"a STUB: no independent look yet"* when the yaw+pitch layer is fully
implemented at `LateUpdate`; the other named over-the-shoulder the default). `PAIN_POINTS` §4 gate
closed in the same change (§15). Comment + doc only — zero behaviour change.

---

## DEFERRED — owner rulings required

**D1 — Which wave-authoring layer wins?** Two defensible answers, both destructive to someone's work:
set `_smartComposition = 0` in both hubs (the 2026-07-11 authored schedule becomes live again, but
WO-362's rotating gates / anti-repeat / elite cadence / family variety — which you asked for on
2026-06-14 — are lost), OR strip `enemies[]` from waves.json keeping `countdownSeconds`/`boss`/
`apexBoss` (generation wins, and 148 enemies of dead authoring stop lying to the next designer).
A fail-by-design `[wave-authoring]` oracle is **specced and ready** (it decides from the two real
inputs — the serialized flag by script GUID, and waves.json via the runtime read path — and cannot go
green by drift, rename, moved file, or finding no scenes). **Deliberately NOT registered yet:** it
would turn `DataRegression` RED immediately, and the ruling is yours. Register it with the ruling.
*The real architectural answer, out of scope for a guard: make the two authorities COMPOSE — let
`WaveCompositionBuilder.Build` SEED from the authored batches, tactical positioning applied on top.*

**D2 — A THIRD raid exit still bypasses settlement.** Hero death in an enemy-owned scene routes
straight home at `HeroHealth.cs:719-728` (`SceneOwnership.IsEnemyOwned` -> `GoCastle()`) with no
reconcile anywhere on that path — **dying in a raid still costs no troops.** The new public
`ReconcileRaidEnd(0)` closes it in one guarded line, but it is a distinct behaviour change on a
distinct path and I did not fold it in unasked. Say the word.
(`Village2RaidController` needs nothing — `RaidDeployController` only self-installs in `RaidBase*`
scenes and `TroopDeployer.SpawnFromArmy` has exactly one caller, so Village2 raids deploy no army.)

**D3 — Veterancy pacing.** `ComputeStars` makes **every victory 3 stars** (`cleared` forces 2, then
`cleared && elapsed <= clock` forces 3; a raid that overruns the clock never reaches the victory path
at all). So under the documented rule every win promotes every survivor — rank 6 / +30% in six wins.
That is what the data model literally says. If you want a slower ladder the lever is the 3rd-star
condition (e.g. `elapsed <= clock * 0.5f`), NOT the veterancy rule.

## Also surfaced, not actioned

- `CollectorStackView.Attach()` has **no caller anywhere** — the diegetic collector fill bar renders
  nothing at runtime despite its own comment claiming the bootstrap calls it. Separate ticket.
- `WaveManager._soWaves` (the WO-86 ScriptableObject authoring layer) is declared and **never read** —
  a third dead authoring surface.
- `RaidBase_IronBastion` is on disk, **not** in Build Settings, and has no `RaidGarrisonSpawner`, so it
  is both unloadable and unclearable. Inert today (nothing routes to it). Fixing needs a scene bake =
  owner-gated.
- `RaidGarrisonSpawner` is authored-in-scene, NOT self-installing (unlike the other four raid
  components). Any new `RaidBase_*` scene silently ships un-clearable, and `RaidScoring` degrades to 0%
  destruction with only a Warn.
- Dungeon return drops the hero at the castle spawn, ~140m from the south portal — a re-walk after
  every run. Correct by canon, but a felt-loop item.

## References

12-agent SME fan-out, 2026-07-30 · `docs/PAIN_POINTS_2026-07-26.md` · `WORK_ORDER_362` (the accepted
supersession) · `WORK_ORDER_779` (spacing rule + safe-area helper) · `WORK_ORDER_781` (recovery, the
sibling half of the wounded model) · `docs/SME/VISUAL_TOUCH_CONTRAST_AUDIT_2026-07-14.md`
