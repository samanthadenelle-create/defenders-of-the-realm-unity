# WORK ORDER 823 — Post-review hardening pack (army readiness + founding card + oracles + RESULT hygiene)

**Status:** READY - ⭐ **Phase E is now BUILDABLE (2026-08-24, UI seat): see "Phase E — THE IMPLEMENTABLE SPEC" at the end of this file.** The ruling alone was not enough — no "has ever completed a raid" signal existed anywhere; E1-E7 spec the field, the v38→v39 bump (FOUNDATIONAL_RULINGS.md §5, no owner ruling needed), the `ReconcileRaidEnd` seam, the derive-don't-default read-migration, and the `ArmyReadiness` routing. *(Prior line:)* ⭐ **Phase E's owner ruling LANDED 2026-08-24 (batch 2, ruling 8): the threshold is 3 OF 10**, first-ever raid only, and it MUST go through `ArmyReadiness`. Phases A-D shipped 2026-08-01 (`8560fced`). *(Prior line:)* BLOCKED - on an owner ruling (reconciled 2026-08-09 - Phases A through D SHIPPED in `8560fced`; the single outstanding item, Phase E (optional PO-tunable first-raid softness, P3), is explicitly awaiting the owner's ruling and cannot move without it)

**Status: READY — Phases A-D SHIPPED 2026-08-01 (8560fced); Phase E is now RULED (owner 2026-08-24: **3 of 10**, first raid only, via `ArmyReadiness`) and buildable.**  
**Minted:** 2026-08-01 (CLI / Grok — from PM code review of Claude Fable check-ins)  
**Silo:** Core/Troops + Village/Onboarding + Editor/Regression (file-disjoint phases; can fan-out)  
**Depends on:** 819/820 code already on origin (consume, do not re-litigate)  
**Sibling WOs (do NOT re-implement here — implement those files instead):**
| WO | Owns |
|----|------|
| **822** | Barracks teach v2 (marker + Train-3 + first-raid tip; intro key only on beat complete) |
| **818** | KayKit NPC bodies Phases 2–3 (`npcModel` + stage + injectors) |
| **817** | CoC/WC3 queue visual (engine frozen; presentation only) |
| **821** | Building perk timed Research jobs |

This WO is the **hardening layer** the review asked for so 819/820 stay correct and the next product WOs have a single readiness truth.

---

## Why

Review of checked-in work (808, 810, 812, 813 partial, 819, 820, founding flash, 795, dungeon trio):

1. **Army readiness math is forked** — same formula lives in `BuildTimerService.PublishArmyStatus` and `RaidSelectionScreen.Open` (and enqueue uses a related path). Drift will re-break the grey button vs the open gate.
2. **Founding Echo card** can softlock forever if `Onboarded` never flips or `PanelManager.AnyOpen` is sticky.
3. **No RESULT / status hygiene** — WO bodies still say READY while code is live; 819/820 have no RESULT.
4. **Over-queue / readiness** lack EditMode proof.
5. **Toast-only 813** is owned by **822** (not this WO) — but 822’s Do-NOT-touch assumes `ArmyReadiness.Compute` exists. **Land Phase A here first.**

---

## Phase A — `ArmyReadiness` single source (P0)

### Goal
One pure Village helper every army-fullness consumer calls.

### Implement
1. New file (suggested): `Assets/_Modules/Village/Troops/ArmyReadiness.cs`  
   Namespace `DeNelle.Village`. Pure static; no MonoBehaviour.
2. API (names may match style of nearby troops code):

```csharp
public readonly struct ArmyReadinessSnapshot
{
    public bool Ready;            // deployable + queued >= cap
    public int DeployableSlots;   // healthy only (GetDeployable)
    public int QueuedSlots;       // BarracksService.CommittedTrainingSlots()
    public int CapSlots;          // army.MaxArmySize
}

// state null / army null => Ready=true, zeros (headless / AutoPilot never false-blocks)
public static ArmyReadinessSnapshot Compute(GameState state);
```

3. **Wire all three consumers** to `Compute` (or its fields):
   - `BuildTimerService.PublishArmyStatus` → publish from snapshot to `RaidEntryGate.PublishArmyStatus`
   - `RaidSelectionScreen.Open` → authoritative gate uses snapshot (still never reads HUD mirror)
   - Keep `BarracksService.EnqueueTraining` cap check as-is **or** share slot math; do not change charge semantics
4. **Do not** put Village types on the Core `RaidEntryGate` — gate stays a DTO + publish/poll seam.

### Acceptance
- [ ] Grep shows **one** place that sums deployable + `CommittedTrainingSlots` + cap for Ready (the helper); Open + Publish call it.
- [ ] Empty army: Raids dim + tap still opens training (820 behavior preserved).
- [ ] Headless/no GameState: Ready true (no false block).
- [ ] COMPILE_GATE_OK.

---

## Phase B — Founding Echo card reliability (P1)

### File
`Assets/_Modules/Village/Harvest/EchoUnlockFeedback.cs` (existing quiet-screen holds).

### Rules (keep existing holds; add safety)
Current holds: gameplay scene, not build mode, `Onboarded`, `!PanelManager.AnyOpen`, 1 Hz poll while pending.

Add:
1. **Soft deadline:** if founding card is due (`EchoCount >= 1`, not yet `FoundingTaught`) for **> 120s unscaled** after first pending, OR if `Onboarded` is true and pending > 30s with `AnyOpen` still true every poll → `FlowTrace.Warn` once and **force-show** the card (prefer teaching over forever silence).
2. Confirm `PanelManager.AnyOpen` only means real modals (not persistent HUD chrome). If AnyOpen includes HUD-only widgets, narrow the check or document why — do not leave sticky-true forever without the deadline.
3. Do **not** mark founding taught until the card actually presents successfully (existing retry-on-fail stays).

### Acceptance
- [ ] Start New → complete tutorial → within a calm few seconds the founding card appears once.
- [ ] Artificial sticky modal / late Onboarded cannot block the card past the soft deadline (trace proves force path).
- [ ] Card still does not fire mid-fade on first castle frame (original flash fix preserved).

---

## Phase C — EditMode / regression proof (P1)

### Add
1. **EditMode** (or gate oracle) for over-queue: at cap 10, empty roster, try enqueue 20 Footmen → exactly 10 jobs / charge path refuses rest (mirror 820 AC). Prefer pure state + queue mock if easy; else document headless AutoPilot step.
2. **EditMode** for `ArmyReadiness.Compute`:
   - null state → Ready
   - 0 deployable, 0 queued, cap 10 → !Ready
   - 0 deployable, 10 queued slots → Ready
   - wounded-only roster does not count as deployable (if testable without full scene)
3. **DataRegression** (if not already covered by 822 presence oracle): assert first-of-type freebie still applies to non-tower buildings including `barracks` so first place cannot softlock on wood/iron. If freebie is code-path only, add a comment + unit test near freebie gate, not a false data check.

### Acceptance
- [ ] New tests green under existing EditMode / DataRegression runners.
- [ ] REGRESSION_OK after commit.

---

## Phase D — Canon / RESULT hygiene (P2, same session as A–C if cheap)

Write RESULT stubs (short, gate + what shipped + PO remaining):

| File | When |
|------|------|
| `WorkOrders/WORK_ORDER_819_structure_singleton_common_v2.RESULT.md` | After A optional; code already shipped |
| `WorkOrders/WORK_ORDER_820_raid_full_army_gate.RESULT.md` | After Phase A lands (note ArmyReadiness refactor) |
| Optional: flip **810 / 812** WO Status headers from READY → IMPLEMENTED awaiting PO if code is on origin | Do not invent felt-pass |

Update `CLI_LANES_WO_NUMBERS.md` only if statuses change materially.

### Acceptance
- [ ] 819 + 820 have RESULT files with commit SHAs and “PO felt still open” checkboxes.

---

## Phase E — Optional PO-tunable first-raid softness (P3 — only if owner asks)

**Default: do NOT implement** unless owner confirms.

Review risk: full army cap (10) may feel harsh for first raid. If owner wants soft gate:

- Config or constant `FirstRaidMinDeployableSlots` (e.g. 3) used **only** when save has never completed a raid; after first raid return, full 820 rule.
- Must still go through `ArmyReadiness` (extend snapshot, not fork Open).

Leave out of default implementation.

---

## Out of scope (separate WOs)

| Do not do in 823 | Why |
|------------------|-----|
| Full barracks coach / Train-3 / marker | **822** |
| KayKit stage + `npcModel` + injectors | **818** Phases 2–3 |
| Queue bars / CoC channel glance | **817** |
| Perk research timers | **821** |
| StructureSingleton FindObjects perf rewrite | Only if F8 proves hitch |
| Gear max-level ability | **814** |
| Echo gather/repair | **811** |
| Re-skin Rumor Board | **810** felt only |

---

## Implementation order for Claude / CLI

```
1. Phase A  ArmyReadiness + rewire Publish + Open
2. Phase C  tests for readiness + over-queue
3. Phase B  founding soft deadline
4. Phase D  RESULT files
5. STOP — hand 822 / 818 / 817 / 821 as separate pulls
```

Gate once at end of A–D: `COMPILE_GATE_OK` + `REGRESSION_OK`. Brace-check every touched `.cs`.

---

## Files likely touched

| File | Phase |
|------|--------|
| `Assets/_Modules/Village/Troops/ArmyReadiness.cs` | A (new) |
| `Assets/_Modules/Village/Buildings/BuildTimerService.cs` | A |
| `Assets/_Modules/Village/Hero/RaidSelectionScreen.cs` | A |
| `Assets/_Modules/Village/Troops/BarracksService.cs` | A only if shared helpers cleaned |
| `Assets/_Modules/Village/Harvest/EchoUnlockFeedback.cs` | B |
| `Assets/Tests/EditMode/*` or `Assets/Editor/Regression/*` | C |
| `WorkOrders/*819*.RESULT.md`, `*820*.RESULT.md` | D |

## Do NOT touch

- `FeatureFlags.RaidContinuousWalk` (OFF) short-circuit
- Queue engine / JobEffectRegistry semantics
- `StructureSingleton` enforcement rules (read-only consumers OK)
- Hand-edit of any `.unity` scene
- Mount/bash writes to `.cs` (Windows Write/Edit only)

---

## Paste blurb for Claude CLI

```
Implement WORK_ORDER_823_post_review_hardening_pack.md phases A→C→B→D in order.
Create ArmyReadiness.Compute and rewire BuildTimerService.PublishArmyStatus +
RaidSelectionScreen.Open to it (820 behavior unchanged). Add EditMode/oracle for
readiness + over-queue. Founding Echo: soft deadline so the card cannot stay
pending forever. Write 819/820 RESULT files. Do NOT implement 822/817/818/821 here.
COMPILE_GATE_OK + REGRESSION_OK; brace-check every .cs; sole committer commits by path.
```

---

## ⭐ OWNER RULING 2026-08-24 — batch 2, ruling 8 — Phase E: **soften the first raid. THE NUMBER IS 3 OF 10.**

**Recorded by the UI seat from `OWNER_RULINGS_OWED_2.md` §8. The number is hers; do not re-tune it
without her.**

⭐ **Her reasoning — keep it, it is why 3 and not 1 or 5:**
- **One unit feels like a scripted tutorial.**
- **Five starts feeling like waiting again.**
- **Three communicates troop selection, deployment, multiple actors, losses/survivors, and combat
  pacing** — the whole vocabulary of a raid, in the smallest army that can teach it.

### The shape, unchanged from the spec

- ⛔ **FIRST-EVER RAID ONLY.** The threshold applies only when the save has **never completed a raid**;
  once the first raid returns, the normal full-army rule resumes **permanently**.
- ⛔ **It MUST go through `ArmyReadiness`** — the single source Phase A built. **Never a second check
  inside the raid screen**, or the grey-button-versus-open-gate bug comes straight back.

**Status → READY.** Phase E is the last outstanding phase; Phases A–D shipped 2026-08-01 (`8560fced`).

---

# ⭐ Phase E — THE IMPLEMENTABLE SPEC (added 2026-08-24, UI seat)

**Why this section exists:** the ruling above is complete and the number is settled, but Phase E was
still **NOT BUILDABLE**. It says *"used only when the save has never completed a raid"* — and
**no such signal exists anywhere in the game.** Verified at source this session, not inferred:

| Searched | Result |
|---|---|
| `raidsCompleted` / `everCompletedRaid` / `firstRaid*` across every `.cs` under `Assets/` | **ZERO hits** |
| The full `[JsonProperty]` list in `Assets/_Modules/Core/State/SaveSchema.cs` | no raid-completion field of any kind |
| `Assets/_Modules/Core/State/GameState.cs:568` | only `RaidCooldowns` — **per-camp and expiring**, wrong shape |

So Phase E needs five pieces built before the 3-of-10 rule has anything to ask.

---

## E1 — The field

**`Assets/_Modules/Core/State/GameState.cs`** — append at the END of the class, beside `RaidCooldowns`
(`:568`). Model it on `StrategicPlacementMigrated` (`GameState.cs:435`):

```csharp
/// <summary>
/// WO-823 Phase E — has this save EVER completed a raid (victory, loss, retreat, timeout
/// or hero death)? Monotonic: once true it never returns to false. The ONLY input to the
/// first-raid soft gate (3 of 10). Distinct from RaidCooldowns, which is per-camp and EXPIRES.
/// </summary>
public bool EverCompletedRaid = false;
```

**Plain `bool` on GameState, nullable only on the wire** — the `StrategicPlacementMigrated`
precedent. **Monotonic, like `everBuiltStructureIds`** (`SaveSchema.cs:645`): nothing ever clears it.
⛔ Do not add a debug/reset path; `ResetToNewGame` is the only writer of `false`.

**The exact three-place pattern, verified at source on the `strategicPlacementMigrated` model:**

| Place | Model line | What to add |
|---|---|---|
| Wire field | `SaveSchema.cs:504` | `[JsonProperty("everCompletedRaid")] public bool? EverCompletedRaid;` — **append-only at the END** of `PersistedState`, after `raidCooldowns` (`:696`) |
| CAPTURE / save | `GameStateService.cs:543` | `EverCompletedRaid = s.EverCompletedRaid,` in the same object initializer |
| RESTORE / load | `GameStateService.cs:638` | `if (p.EverCompletedRaid.HasValue) s.EverCompletedRaid = p.EverCompletedRaid.Value;` |
| New game | `GameStateService.cs:1104` (the `s.RaidCooldowns = new List<...>();` line; bool precedent at `:1098`) | `s.EverCompletedRaid = false;` on an adjacent line |

---

## E2 — The schema bump: **v38 → v39**

⭐ **This bump does NOT need an owner ruling.** `FOUNDATIONAL_RULINGS.md` **§5** grants the lead
authority to bump the save schema when all four of its stated conditions hold together. They do:

| §5 condition | How E2 satisfies it |
|---|---|
| 1 | Nullable on the wire, append-only at the end of `PersistedState`; a v38 save deserializes unchanged. |
| 2 | Absent ⇒ the read-migration in **E4** supplies the value. |
| 3 | E6 adds the coverage. |
| 4 | Nothing existing is renamed, removed, reinterpreted or converted. `RaidCooldowns` keeps its exact current meaning. |

⛔ Do not restate §5's four conditions in a code comment — cite the section.

**The version number:** current is **`SaveSchema.CurrentVersion = 38`**
(`Assets/_Modules/Core/State/SaveSchema.cs:41`, read at source this session — ⛔ never take it from a
doc; that same file's own header comment sat two versions stale and says so at `:11-12`). So Phase E
ships **v39**.

**Where the step goes, verified:**
- Bump `SaveSchema.cs:41` and prepend the v39 note to that same comment — **that comment IS the changelog.**
- Register in the `Steps` dictionary, `SaveMigrator.cs:36-80`. Today's top entry is `SaveMigrator.cs:79`
  — `{ 38, MigrateToV38 },`. Add `{ 39, MigrateToV39 },` immediately after it.
- Method body precedent: `SaveMigrator.cs:678` (`MigrateToV38`). File ordering is not enforced.

⚠ **An additive default-on-read bool would not normally need a migrator step at all** (see the
precedents at `SaveMigrator.cs:49-61`, and v32/v33 which have none) — **but bumping `CurrentVersion`
does.** `Assets/Editor/Regression/CoreSaveContractRegression.cs:57-69` reflects the private `Steps`
table and **FAILS** unless the top step `==` `CurrentVersion`. Here the step is doing real work anyway
(E4), so this is not a formality.

---

## E3 — The seam that SETS it

**`Assets/_Modules/Village/Troops/RaidDeployController.cs:702` — `ReconcileRaidEnd(int starsEarned)`.**

There is **no single "raid ended" method**; the raid end is split across exits that converge here.
Verified call sites — **four, in three files**:

| Exit | Call site |
|---|---|
| Retreat / clock expiry | `RaidDeployController.cs:529` (`DoRetreat`, fed by `OnRaidTimeExpired` `:220`) |
| Hero death | `Assets/_Modules/Village/Hero/HeroHealth.cs:875` |
| Victory (camp raids) | `Assets/_Modules/Village/World/Camps/RaidVictoryController.cs:493`, via `ReconcileArmy` `:277` |

It is the right seam for three reasons:

1. **It is the only method reached by victory AND defeat AND retreat AND timeout AND hero-death.**
   The two obvious alternatives are **not**: `SettlePartialLoot` (`:556`) is non-victory only, and
   `RaidCooldownService.BeginAfterClear` is **victory only** (`RaidVictoryController.cs:249`,
   `Village2RaidController.cs:232` — the only two non-test call sites repo-wide).
2. **It is already latch-idempotent** — `_reconciled` (`:613`, checked `:704-710`), so the stamp fires
   exactly once per raid.
3. **It already has GameState in hand** (`Army()` `:712`) and every caller `Save()`s right after
   (`RaidDeployController.cs:532`, `HeroHealth.cs:879`; victory persists via `ClaimBase` /
   `RaidCooldownService.Persist`).

**Stamp it immediately after the `army == null` early-out (`:713-718`), BEFORE the reconcile work** —
so a raid ending in a reconcile anomaly still records that it happened. One assignment plus a
`FlowTrace.Step` **on the false→true transition only** (⛔ never per-raid; §12 says instrument, not flood).

⛔ **Do NOT stamp in `RaidVictoryController`** — victory-only, so a player whose first raid is a
retreat would never leave the soft gate.

### ⚠ KNOWN GAP — Village2 stronghold raids do not reach this seam

`Assets/_Modules/Village/World/Camps/Village2RaidController.cs:205` (`HandleCleared`) stamps its own
cooldown at `:232` but **never calls `ReconcileRaidEnd`** — Village2 has no `RaidScoring` and no
deploy-ledger settle. Compounding it, `RaidDeployController` self-installs **only in `RaidBase*`
scenes** (`:120-133`, noted again at `HeroHealth.cs:854-855`).

**Consequence to state plainly:** a player whose first-ever raid is a **Village2 stronghold** clears it
and the flag stays `false`, so the soft gate persists into their next raid. ⛔ **Do not "fix" this by
adding a second stamp in `Village2RaidController`** — that forks the one-owner seam and is the same
class of mistake as a second readiness check. Either Village2 is routed through `ReconcileRaidEnd` as
its own change, or this is accepted and documented. **Surface it; do not silently paper over it.**

---

## E4 — ⚠ The legacy-save default is a BEHAVIOUR CHOICE, and the obvious answer is WRONG

**The owner has completed several raids.** So `absent → false` — the naive migration — would
**re-gate her own save**, and every other veteran save, telling a player with a dozen raids behind
them that they may bring 3 of 10 troops "for their first raid." That is visibly wrong to precisely
the people most likely to notice. ⛔ Do not ship `absent → false`.

### The rule: `absent → DERIVE from named evidence; if no evidence exists → false`

**The evidence hunt, at source. There is exactly one durable signal, and it is imperfect:**

| Candidate | Verdict |
|---|---|
| ⭐ **`army.owned[].veterancyRank > 0`** | **USE THIS — primary clause.** Durable, never expires, never pruned. `PlayerTroop.cs:60-61` documents the rank as granted per survived raid; its **only** grant path repo-wide is `RaidDeployController.GrantVeterancy` (`:782`) — the raid seam itself — and `:755` records that before that call site `AddVeterancy` had **zero** callers. `PlayerTroop.cs:34` records troops are *"never deleted on a loss."* A non-zero rank is proof a raid completed. |
| `GameState.RaidCooldowns` non-empty | **Sufficient but NOT necessary — second clause, never alone.** `RaidCooldownService` **actively prunes** expired and inert records (`:212`, `:241`, `:354`), so an idle veteran save legitimately holds none. |
| Quest / objective completion | **NOT USABLE.** `QuestProgress` (`NestedTypes.cs:251-255`) has a `completed` dictionary, but no raid-completion quest id keyed to a camp raid was found in `Assets/Resources/Data/Canonical/quests.json`. ⛔ Do not invent one. |
| `defenseReports` (`SaveSchema.cs:664`) / `lastSiegeUnixMs` (`:678`) | **NOT USABLE — wrong direction.** Those record **incoming** sieges on the player's town, not outbound raids. |
| `settlements` / `tribes.clearCount` (`WorldContent.cs:93-113`, `:160`) | **NOT USABLE — would FALSE-POSITIVE.** Node claims and roaming-tribe wipes are different mechanics; clearing a tribe is not raiding a camp. |
| A lifetime raid counter | **DOES NOT EXIST.** No `Lifetime*` / `totalRaids` field on `ArmyStorage` or anywhere in `PersistedState`. |

**So `MigrateToV39` reads:** *absent ⇒ **true** if any owned troop has `veterancyRank > 0` **OR**
`raidCooldowns` is non-empty; otherwise **false**.* (Model the seed on `SaveMigrator.cs:456`, the
`MigrateToV30` line for `StrategicPlacementMigrated`.)

### ⚠ The gap this leaves — put it in the migrator comment, do not hide it

A veteran save is **still wrongly re-gated** when **all** of these hold at once: every raid was below
3 stars or lost, so veterancy was never granted (`GrantVeterancy` returns early under 3 stars,
`:760-765`), **and** every camp cooldown has since expired and been pruned. That player gets **one**
extra soft-gated raid, after which E3 stamps the flag and the gate is gone permanently.

⭐ **Two honest options for the residual gap. Both are the owner's call — this ticket does not pick:**

- **(a)** Accept it. One extra soft-gated raid for a narrow slice of veteran saves, self-healing after
  a single raid. **Say so in the ticket and in the migrator comment** — a derivation that quietly
  misses cases is worse than a documented one.
- **(b)** The owner rules a one-time manual set for her own save (and any save she names).

⛔ Do not implement either until she rules. **(a)** is the no-code default if she does not.

---

## E5 — The gate: through `ArmyReadiness`, and nowhere else

⛔ **The threshold is read INSIDE `ArmyReadiness.Compute`. There is no second check anywhere.**

### The real API (verified — it differs from Phase A's sketch above)

`Assets/_Modules/Village/Troops/ArmyReadiness.cs`, `public static class`, namespace `DeNelle.Village`:

- `:36` `public struct Snapshot` — fields `DeployableSlots` (`:39`), `QueuedSlots` (`:41`),
  `CapSlots` (`:43`), `RosterSlots` (`:45`), `Ready` (`:47`). **There is no `Ready`-only sketch struct;
  use these names.**
- `:55` `public static Snapshot Compute(GameState st)` — null `st`/`st.Army` ⇒
  `new Snapshot { Ready = true }` (`:58`), the deliberate headless never-false-block.
- `:73` `public static Snapshot Compute(ArmyStorage army, int deployableSlots, int queuedSlots)` —
  the EditMode test seam; `Ready = deployableSlots + queuedSlots >= cap` (`:82`).

### The change

Add two fields to `Snapshot` and route both overloads through one comparison:

```csharp
public int RequiredSlots;      // NEW: CapSlots normally; FirstRaidMinDeployableSlots when never raided
public bool FirstRaidSoftGate; // NEW: presentation-only — lets the screen say WHY, never re-decide
```

- `RequiredSlots` = `st.EverCompletedRaid ? CapSlots : FirstRaidMinDeployableSlots`.
- `Ready` becomes `DeployableSlots + QueuedSlots >= RequiredSlots` — **one comparison, one place.**
- `FirstRaidMinDeployableSlots = 3` — a **named constant** sited beside the helper. The owner's number.
  ⛔ Never re-tune without her; her reasoning (one = scripted tutorial, five = waiting again) is
  recorded above and is the retune argument.
- ⚠ **The test-seam overload (`:73`) takes no `GameState`, so it cannot see the flag.** Give it an
  explicit `bool everCompletedRaid` parameter (defaulted `true` = today's behaviour) rather than
  duplicating the comparison. ⛔ Two copies of the comparison is the bug this phase exists to avoid.
- **The null contract is unchanged:** `st == null` ⇒ `Ready = true`, `FirstRaidSoftGate = false` —
  headless / AutoPilot must never meet the soft gate.
- ⭐ **`FirstRaidSoftGate` is presentation-only.** The raid screen may use it to word the copy; it may
  **not** branch the decision on it.

### ⚠ THE UNIT TRAP — "3 of 10" is 3 **SLOTS**, not 3 troops

`ArmyReadiness` is **slot-weighted**: it sums `TroopDialogueCommands.SlotOf` (`:62-63`) against
`army.MaxArmySize`. `FirstRaidMinDeployableSlots` therefore lives in the **same unit as `CapSlots`**,
which is what makes "3 of 10" read correctly. ⛔ Do not compare it against a headcount.

### ⚠ AND THERE IS ALREADY A SECOND CHECK IN THE RAID SCREEN — this is the bug, live

`Assets/_Modules/Village/Hero/RaidDeployScreen.cs` bypasses `ArmyReadiness` in two places:

- `:477` — `bool troopsOk = !GateDeployAtZeroTroops || (_vm != null && _vm.DeployableCount > 0);`
  → drives `deployBtn.interactable` at `:478`
- `:526` — a **second copy** in the DEPLOY handler: `if (GateDeployAtZeroTroops && _vm.DeployableCount <= 0)`
  → toast + block (`:528-529`)

`_vm.DeployableCount` comes from `RaidDeployVM.cs:337` and is a **raw headcount** over
`_army.GetDeployable()` (`:309-322`) — **not slot-weighted**. `RaidDeployScreen.cs:474` even comments
that the upstream gate exists, and then does not consult it. `GateDeployAtZeroTroops` is currently
flagged OFF by default (`:438`, `:525`), which is the only reason this has not already produced the
grey-button-versus-open-gate divergence again.

⭐ **Phase E must route these two through the snapshot** rather than adding a third opinion. That is
the ruling's *"never a second check inside the raid screen"* applied to what is actually in the file
today. There is a **third** count path at `RaidDeployVM.cs:342-359` (`ComputeArmyCapText`, its own
`slotOf` lambda over `_army.SlotsUsed`) — copy text only; leave it, but ⛔ do not let the gate read it.

---

## E6 — Acceptance

- [ ] `grep -rn "EverCompletedRaid" Assets/` shows the field, the wire property, both
      `GameStateService` directions, the `ResetToNewGame` line, `MigrateToV39`, `ReconcileRaidEnd`
      and `ArmyReadiness` — **and nothing inside any raid screen, panel or VM.**
- [ ] `grep -rn "FirstRaidMinDeployableSlots" Assets/` returns **exactly one** definition and
      **exactly one** read, both inside `ArmyReadiness.cs`.
- [ ] `RaidDeployScreen.cs:477` and `:526` read the `ArmyReadiness` snapshot; `_vm.DeployableCount`
      no longer gates either one.
- [ ] `SaveSchema.CurrentVersion == 39` and the `SaveMigrator` `Steps` top entry is `MigrateToV39`
      (pinned by `CoreSaveContractRegression.cs:57-69`).
- [ ] A v38 fixture with **no** `everCompletedRaid` key deserializes cleanly.
- [ ] A v38 fixture carrying a troop at `veterancyRank >= 1` migrates to **true** (E4 primary clause,
      proved not assumed).
- [ ] A v38 fixture with a **non-empty** `raidCooldowns` migrates to **true** (E4 second clause).
- [ ] A genuinely fresh v38 fixture (no veterancy, no cooldowns) migrates to **false**, and the
      migrator's own comment names the E4 gap **and** the E3 Village2 gap.
- [ ] New game ⇒ `false` ⇒ first raid deploys at **3 slots**; after `ReconcileRaidEnd` runs once the
      flag is `true` and the requirement is the full cap **permanently**. **Cover all three exits** —
      retreat (`starsEarned 0`), hero death, and victory.
- [ ] Headless / no `GameState` ⇒ `Ready = true`, `FirstRaidSoftGate = false` (Phase A contract intact).
- [ ] COMPILE_GATE_OK + the regression marker.

### Suites this will trip — check them before claiming green

| Suite | Why it fires |
|---|---|
| `Assets/Editor/Regression/CoreSaveContractRegression.cs:57-69` | version-triple: top step must `==` `CurrentVersion` |
| `Assets/Editor/Regression/CoreSaveRegression.cs:230-244`, `:247` | migrates every start version `0..CurrentVersion-1` |
| `Assets/Editor/Regression/RaidCooldownRegression.cs:322-326` | **source-lints the `ReconcileRaidEnd(int starsEarned)` signature** — do not change it |
| `Assets/Editor/Regression/RaidExitParityRegression.cs:132-143` | source-lints the exit routing; a stamp inserted into `ReconcileRaidEnd` is checked here |
| `Assets/Editor/Regression/RaidsDiscoverabilityRegression.cs:233-234`, `:205-206` | pins that `RaidSelectionScreen.Open` calls `ArmyReadiness.Compute` and `RaidCapabilityHudBridge` does **not** |

**New coverage joins** `Assets/Editor/Regression/StrategicPlacementRegression.cs` as its model (the
closest precedent for an additive bool: migrator-seeds-the-right-default `:209-212`, round-trip
`:362-378`, absent-on-old-save `:393`), registered in `DataRegression.cs` the same way. EditMode:
`Assets/Tests/EditMode/ArmyReadinessTests.cs` and `Assets/_Modules/Core/Tests/SaveLoadRoundTripTest.cs`.

## E7 — What NOT to touch

- ⛔ `RaidCooldowns` semantics, its record shape, or its pruning. Untouched — that is §5 condition 4.
- ⛔ The `ReconcileRaidEnd(int starsEarned)` **signature** — source-linted (see table above).
- ⛔ `RaidVictoryController` / `Village2RaidController` — the stamp does not go in either (E3).
- ⛔ The 3-star rule inside `GrantVeterancy`. E4 **reads** veterancy; changing when it is granted
      would invalidate its own evidence.
- ⛔ `RaidCapabilityHudBridge` — it must keep NOT referencing `ArmyReadiness`.
- ⛔ Any third readiness opinion. Phase E **removes** one; it must not add one.

---

## ⭐ OWNER RULING 2026-08-24 - "3 of 10" MEANS **SLOTS**, not a troop headcount

⭐ **`ArmyReadiness`'s slot-weighted model is CORRECT and is the single source.** The threshold is
**3 deployable SLOTS of 10**, so a first raid may be three cheap units or fewer expensive ones - the
cost of what the player brings is part of the decision, which is the point.

⛔ **THIS MAKES THE TWO BYPASSES DEFECTS, not an alternative reading.** `RaidDeployScreen.cs:477` and
`:526` gate on a **raw headcount** (`_vm.DeployableCount`) while `ArmyReadiness` is slot-weighted, so
those two sites and the readiness service **disagree about what "enough army" means** - today, before
Phase E adds anything.

⚠ **That disagreement is the grey-button-versus-open-gate bug in its original form**: one surface says
you may raid, another says you may not, and neither is lying. ⭐ **Phase E REMOVES the two bypasses;
it does not add a third check.** ⛔ No new readiness predicate anywhere - route through `ArmyReadiness`
or do not gate.

⚠ Also still open and NOT fixed by this ruling: **`Village2RaidController` never calls
`ReconcileRaidEnd`**, so a first-ever Village2 stronghold raid would not clear
`EverCompletedRaid` and that player gets the soft gate a second time. Its own change, its own capture.
