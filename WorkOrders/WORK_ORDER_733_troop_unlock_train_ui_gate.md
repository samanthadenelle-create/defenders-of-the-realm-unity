# WORK ORDER 733 — Troop Unlock UX + Train Refuse Gate

**Status:** READY TO IMPLEMENT  
**Priority:** P0  
**Silo:** UI / State / Barracks  
**Depends on:** WO-732  
**Blocks:** WO-736; unblocks meaningful WO-724 felt-pass  
**Program:** `WORK_ORDER_PROGRAM_732_736_barracks_troop_roster.md`  
**Effort:** M–L  
**Audience:** Claude + CLI  

---

## Goal

Training UI shows **the full roster progression**: default types trainable now; higher types **visible but locked** until Barracks tier is high enough. Train actions **hard-refuse** locked types (panel, Yarn, any other entry).

---

## Current state (verified)

- `TroopTrainingPanel.Rebuild` iterates **all** `TroopCatalog.All` with no tier check.  
- `TroopDialogueCommands.Train` spends + `ArmyStorage.TrainNow` — **no unlock check**.  
- Barracks tier source of truth: `ModifierService.TierOf("barracks")` (see upgrade VM / building progression).  
- Feature gate for feature existence: `FeatureFlags.Barracks` / `BarracksUnlock` (if present on branch) — **orthogonal** to per-troop tier unlock.

---

## Unlock resolution rule (BINDING)

```
int barracksTier = ModifierService.TierOf("barracks");
// If barracks structure exists / is the training context but tier never written, treat as 1.
if (barracksTier < 1) barracksTier = 1;

bool canTrain = troop.UnlockBarracksTier <= barracksTier;
```

**Notes:**

- Tier **1** = day-one defaults (Footman, Archer) after barracks is usable.  
- Do **not** require Village Tier separately for unit unlocks (Barracks tier already gates on village tier in `building-tiers.json`).  
- Cap / wounded / afford still apply **after** unlock passes.

---

## Deliverables

### 1. Shared query API (single place)

Add a small static helper (suggested location: `TroopCatalog` or new `TroopUnlock.cs` under `Assets/_Modules/Village/Troops/`):

```csharp
// Pseudocode — implement cleanly
static int EffectiveBarracksTier(); // TierOf, floor 1
static bool IsTrainable(TroopDef def); // unlock <= effective tier
static string LockedReason(TroopDef def); // "Unlocks at Barracks Tier 3 — War College"
```

All train entry points **must** call `IsTrainable` — no copy-pasted magic numbers.

### 2. `TroopTrainingPanel` UX

**Left list (master):**

- Show **all** catalog troops sorted by `UnlockBarracksTier` then catalog order.  
- **Unlocked:** normal Obsidian button (selectable).  
- **Locked:** dimmed button (or locked style); still selectable so detail explains unlock.  
- Never show raw ids — use `DisplayName` / `SpacedDisplayName`.

**Right detail (when selected):**

- Stats + costs (existing).  
- If locked: prominent line e.g. `Locked — upgrade Barracks to Tier {n} ({tierName})`.  
- Pull tier **name** from `BuildingTierCatalog.TierOf("barracks", n)` when available.  
- If unlocked: show Train ×1 / ×5 as today.  
- If locked: **disable** Train buttons (or hide); tap shows toast/kit feedback, does not spend.

**Optional polish:** lock icon / gold trim dim — only if kit already has a pattern; do not invent a new UI framework.

### 3. Hard gate on `TroopDialogueCommands.Train`

Before spend:

1. Resolve def; if null → 0.  
2. If `!IsTrainable(def)` → log FlowTrace Warn, return 0, no spend.  
3. Else existing cap/economy path.

Also guard `StartTraining` Yarn path (same `Train` method).

### 4. Deploy tray safety (light)

If `RaidDeployController` / army tray only shows owned `PlayerTroop` instances, locked types cannot appear unless already trained (impossible if gate works). **No change required** unless a cheat path trains locked types — then fix the cheat too.

### 5. Instrumentation

FlowTrace system `"TroopTrain"` or `"Barracks"`:

- `refuse-locked id=… needTier=… haveTier=…`  
- `train-ok id=… qty=…`  

---

## Tasks (ordered)

1. SME-read panel + dialogue train + ModifierService tier.  
2. Implement unlock helper.  
3. Panel list/detail lock UX.  
4. Train refuse gate.  
5. Manual/dev: set barracks tier via DevPanel if available; verify T1 only trains 2 types; force tier 3 → Shieldguard trainable.  
6. Brace/NUL + CompileGate.  
7. RESULT with screenshots optional; cite FlowTrace lines if fleet not run.

---

## Acceptance

- [ ] Fresh barracks tier 1: only Footman + Archer trainable; other 5 visible + locked copy.  
- [ ] At tier N, all troops with `unlockBarracksTier <= N` trainable.  
- [ ] Locked train (panel or Yarn) spends **0** resources and adds **0** army members.  
- [ ] Unlock helper is the **only** tier compare used for troops.  
- [ ] `ff.barracks` OFF still blocks whole panel open (existing guard) if present.  
- [ ] CompileGate green; no UXML introduced.

---

## Not in scope

- Authoring JSON roster (WO-732).  
- Changing barracks tier **costs** or perk mults (WO-734 only copy + unlock announce).  
- Custom models (WO-735).  
- CoC deploy loop feel (WO-726).

---

## Key files

| Action | Path |
|--------|------|
| EDIT | `Assets/_Modules/Village/Hero/TroopTrainingPanel.cs` |
| EDIT | `Assets/_Modules/Village/Troops/TroopDialogueCommands.cs` |
| ADD or EDIT | `TroopUnlock.cs` or methods on `TroopCatalog.cs` |
| READ | `Assets/_Modules/Core/State/ModifierService.cs` (or wherever `TierOf` lives) |
| READ | `Assets/_Modules/Core/State/BuildingTierCatalog.cs` |
| READ | `Assets/_Modules/Core/State/ArmyStorage.cs` |

---

## Claude implementation notes

- MVVM if you touch VM — today panel is a “dumb” service caller; keep logic in helper/services, not raw button lambdas beyond selection.  
- Use kit toasts for refuse feedback if the project pattern exists; else `Debug.Log` + FlowTrace is OK for day-one.  
- Mobile: locked rows still tappable for education.  
- Do not delete locked troops from the list (players must **see** the ladder).

---

## RESULT

`WorkOrders/WORK_ORDER_733_troop_unlock_train_ui_gate.RESULT.md`
