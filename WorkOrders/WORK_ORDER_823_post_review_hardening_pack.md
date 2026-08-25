# WORK ORDER 823 — Post-review hardening pack (army readiness + founding card + oracles + RESULT hygiene)

**Status:** READY - ⭐ **Phase E's owner ruling LANDED 2026-08-24 (batch 2, ruling 8): the threshold is 3 OF 10**, first-ever raid only, and it MUST go through `ArmyReadiness`. Phases A-D shipped 2026-08-01 (`8560fced`). *(Prior line:)* BLOCKED - on an owner ruling (reconciled 2026-08-09 - Phases A through D SHIPPED in `8560fced`; the single outstanding item, Phase E (optional PO-tunable first-raid softness, P3), is explicitly awaiting the owner's ruling and cannot move without it)

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
