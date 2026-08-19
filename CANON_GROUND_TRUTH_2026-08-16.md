> ## ⚠ SUPERSEDED 2026-08-18 — the live anchor is `CANON_GROUND_TRUTH_2026-08-18.md`
>
> This file is FROZEN as a dated point-in-time ledger (CLAUDE.md §15) — its body is kept verbatim and
> is NOT rewritten. Read the 08-18 anchor first; it wins on any conflict. Everything here that the
> 08-18 anchor does not contradict still stands as history.
>
> ⚠ Specifically stale below: the header's `HEAD 210d4f2bb` snapshot, and any structure-orientation
> reading — the 08-18 anchor records that the `f995c4706` axis-bake pass corrected `offsets.json`,
> which is INERT for structures, while the live channels (`structures-catalog.json` orientation +
> `HubStructureVisualInjector` `pitchDeg`) still carried the legacy `-90`.

# CANON GROUND TRUTH — 2026-08-16

**Supersedes `CANON_GROUND_TRUTH_2026-08-09.md`** (now bannered/frozen, along with the 08-07 anchor,
which was still claiming to be live a week after it was superseded). Per CLAUDE.md §15 this is the
single live anchor: every other doc loses to it on conflict.

**Branch:** `wip/village2-and-f8-tickets` · **HEAD `210d4f2bb`** (2026-08-16, the Sunday punch-list
wave) · **read `git status` / `git rev-list origin/..HEAD` for push state — never trust a hash or an
"N ahead" count copied into a doc.** The 08-09 anchor's header was 57 commits stale within a day;
this one will rot the same way if it is read as a live counter instead of a dated snapshot.

> **⚠ RECENCY DOES NOT CERTIFY A DOC.** Newest-wins is a tiebreaker between sources, never evidence
> that any one of them is right. Every fact below was verified at source on 2026-08-16 — the file and
> line are named so the next reader can re-verify instead of inheriting.

---

## 1. Save schema — **v38**

`SaveSchema.CurrentVersion = 38` at `Assets/_Modules/Core/State/SaveSchema.cs:41` (the const is the
authority; the file's own header says so, because it once read "36" twenty-five lines above a const
that read 38). `SaveMigrator` top step = `{ 38, MigrateToV38 }` at
`Assets/_Modules/Core/State/SaveMigrator.cs:79`.

| Version | What it added |
|---|---|
| **v38** | **WO-934 army loadout bank** — `ArmyStorage.loadouts` (3 named composition presets) + `activeLoadout` index. Additive on nested Army JSON; `MigrateToV38` runs `EnsureLoadouts` for empty slots. |
| v37 | WO-911 the per-job **PAID BASKET** — `paidWood/paidFood/paidIron/paidCrystals/paidMagic` on `BuildJobData`; the precondition for cancel refunding 100% of what was paid, flat. A pre-v37 job refunds ZERO and says so. |
| v36 | WO-834 `everBuiltStructureIds` — the blank-town baked standdown ledger. |
| v35 | WO-773 `obsidianQueue` — the common multi-channel work queue. |

**Any doc still saying v37, v36, v34, v30 or v20 is stale.** The version triple
(`SaveSchema.CurrentVersion` == `GameState.SchemaVersion` default == `SaveMigrator` top step) is
pinned by `Assets/Editor/Regression/CoreSaveContractRegression.cs` — read it there, not here.

---

## 2. What changed on 2026-08-16 — twelve systems

### 2.1 Raid heroes carry across — **`RaidHeroSpawner` NEVER EXISTED**
(WO-1109, commit `256fa9ee3`.) The raid scenes never had a hero spawner class. The **emergency
pill-hero was the normal path**, not a fallback — and it had **no abilities at all**. The real hero
now carries into the raid. **Do not go looking for `RaidHeroSpawner`**: `git log -S` over the whole
history returns only the commits that say it never existed. A session that hunts for it burns the
morning on a class that has never been in the repo.

### 2.2 The Echo has ONE appearance owner — `EchoWorldPresence`
(WO-1108 Lane B, commit `7fcb49a1b`.) The Echo escorts you to the gate, vanishes, and returns once
after the battle. `PetDeployer.DespawnEcho` (`Assets/_Modules/Pets/PetDeployer.cs:442`) is the
**FIRST despawn path in the game** — nothing had ever removed a deployed pet before. Pinned by
`Assets/Editor/Regression/EchoWorldPresenceRegression.cs`.

### 2.3 Echo repair is passive and count-driven
(WO-1108 Lane A, commit `c72d276db`.) Repair rides the **roster count**, not a per-pet assignment:
every owned Echo contributes through `EchoRepairService`, the single scanner/spender against
`WallRepairController`. There is no longer a per-pet repair task.

### 2.4 WO-993 — the physical pet stack is retired
(Commit `b63bc7190`.) **DELETED:** `AuraController`, `PetProgression`, `EchoSpiritPresentation`.
⚠ **`PetTaskController` is NOT deleted.** It is **retired in place** as a task-state holder — kept
because `EchoEngageDialogueRegression` pins its shape by reflection — and `SetTask(Repair)` now
**refuses loudly** (`Assets/_Modules/Village/Pets/PetTaskController.cs:97`). What is gone is its
update loop, `TickRepair`, and `PetTaskInstaller`. **`PetHeroLeash` STAYS** — it is what makes the
wolf guide move. (The spirit layer was already dead before the lane started: zero live callers since
WO-961 on 08-10.)

### 2.5 Ranger — the BOW is an action-bar ability, the DAGGER is the primary
(WO-1105 R5, commit `682c6f595`.) `ranger.q` (Quick Shot) has **always** been slot Q, and Q is the
class's LOCKED basic (only W/E/R are loadout-swappable). The morning's commit had additionally wired
the shot to the PRIMARY input; that path is deleted (`FirePrimary` / `FireRangedPrimary` /
`ResolveRangedTarget` / `ResolvePrimaryFace`, ~145 lines), so **the phone's one attack button never
spends an arrow**. Primary is the melee/dagger sweep for every class.

### 2.6 The bow's attach seat was 90 degrees off
(Commit `998ca0751`.) The bow lay HORIZONTAL in Sylas's hand. This is a **seat orientation** failure,
distinct from the grip POSITION, which measured correct. See §4 — it is the counterexample to
"derivation is self-proving".

### 2.7 Talent trees re-authored — 3 bases branching wider
(Commits `28d70b98b` + `82b1b10d4`.) **WO-910 is RESOLVED, not open.** Every tree now starts with
**THREE base nodes and branches**; verified in `Assets/StreamingAssets/Data/Canonical/hero-talents.json`:
knight **3/7/8/7/7** (32 nodes) · ranger **3/5/6/6** (20) · mage **3/6/6/5** (20).
⚠ **Ranger and mage previously had NO authored x/y at all** — the "31 dead nodes" framing described a
layout that did not exist, not a design deficit. Also fixed: **one focus plate per BOARD** (the view
had been consuming `HeroSkillTreeVM`'s per-TRACK `nextTaken` signal as board-level, drawing a plate on
every track).

### 2.8 Storage containers climb to SIX levels
(WO-1108b, commit `38ed0d881`.) Capacity at level = **1k / 2k / 4k / 8k / 16k / 32k**; a maxed
container takes that resource's store from **2000 base → 34000**. Data, not a new system:
`storageCapacity` 500 → 1000, multipliers `[1,2,3]` → `[1,2,4,8,16,32]`, `maxLevel` 3 → 6. Costs
double per step, wood+iron only (WO-947 — containers are regular structures). Time is deliberately
NOT authored: `StartUpgrade` derives tier as `targetLevel-2` and the existing curve yields
40s/2m/6m/18m/55m by itself.
⛔ **`RepoProps.MaxStructureLevel = 6` (`Assets/_Modules/Core/Catalog/RepoProps.cs:69`) is the SINGLE
ceiling.** It replaced **eight hardcoded 3s** (BuildModeController, StructureCardVM, three suites, an
EditMode test, StorageCapsCatalog's fallback array). **Never re-hardcode a level ceiling.**

### 2.9 One resolver decides the upgrade family
(Commit `adfbec3cb`.) `UpgradeFamilyResolver` is the **ONE** decider of upgrade family;
`PlacedStructureUpgradeService` is the **SINGLE** start path for placed structures. Both live in
`Assets/_Modules/Village/Buildings/Progression/`. Every `maxLevel > 1` structure now reaches a
truthful upgrade page with a 3D preview, from either doorway (Manage tab or the modeled page).
**The live defect this closed was a LIE, not an absence:** Manage passed a bare catalog id, it
resolved to `UpgradeFamily.None`, and a LEVEL-1 TOWER rendered "Fully enhanced — has reached tier 0
of 0, there is nothing left to upgrade here."

### 2.10 The Arcane Spire plans seat INSIDE the wall ring
(Commit `ec247d4f1`, from owner F8 seq 2505 "I'm on wave five and still cannot build arcane towers".)
The plans dropped on schedule at wave 3 — but ~4m **beyond** the 40.8m wall line, because the drop
pulled 8m inward from a spawn point 12m OUTSIDE the gate while its comment claimed "pulled well
inside". There was no `plans-collected` in 647k log lines. Fixed **with no new magic number**:
`WaveSpawnPoint` already carries its gate's world position (baked by `CastleHubBuilder`). The plans
now also carry a **landmark beacon** so they announce themselves.

### 2.11 `CollectorStackPropCatalog` exists
(Commit `617628a8c`.) `Assets/_Modules/Village/Buildings/Progression/CollectorStackPropCatalog.cs` —
log / flour sack / iron bar. Collectors no longer fall back to an abstract bar.

### 2.12 Three oracles were matching their own PROSE, not code
(Commit `8aef8f25b`.) Worth carrying forward as a pattern, not just a fix: a regression that asserts
against a comment it also owns proves nothing. Cross-check any suite that "passes" a claim it states.

---

## 3. Standing rules re-confirmed today

- **Never restate a suite count, a HEAD hash, an "N commits ahead", or a next-free WO number in a
  doc.** Read them off the marker file / `git` / the `CLI_LANES_WO_NUMBERS.md` banner. Every one of
  those copied numbers was found stale in today's sweep.
- Gate markers are DISTINCT per entry point: `REGRESSION_OK <n>/<n> suites` (DataRegression) ·
  `CHECKIN_SUITE_OK` (RegressionSuite) · `SESSION_GUARDS_OK` (SessionRegression).
- FlowTrace is **never stripped** — flag it off, leave the calls (CLAUDE.md §12).

---

## 4. The derivation caveat — recorded because it cost us today

`docs/ARCHITECTURE.md` states that orientation/grip/seat is **DERIVED from bounds + name, never
guessed**. That is still the principle. It is **not self-proving**: derivation did NOT save the bow
(§2.6). Its held rotation was 90 degrees wrong at the **attach seat**, a different failure from the
grip POSITION, which measured correct — and **headless gates cannot see orientation**. A derived
value can be derived correctly and still land wrong one transform up the chain. For anything the
player sees pointed a direction, the screenshot is the evidence, not the gate.
