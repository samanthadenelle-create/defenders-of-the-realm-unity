<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 115 — Offline Harvest Accrual: Come Back Richer (the idle half of the loop)

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-29
**Priority:** High — the idle retention hook; the OFFLINE rung of the core loop (`docs/NORTH_STAR.md`)
**Scope:** Medium — one new service in `DeNelle.Village`, two persisted save fields, a code-built welcome-back popup
**Depends on:** WO-111 (resource-node pillar — auto-harvest mines), WO-112 (ward-tether — only lit-ward node-claims accrue), WO-110/122 (crystal mine site)
**Implements:** WO-111 **Phase 5** ("Offline accrual"), the OFFLINE line of the NORTH_STAR core loop
**Canon source:** `docs/NORTH_STAR.md` (core loop: *"OFFLINE: mines + pets keep gathering up to a cap → come back richer"*), `docs/tower-empowerment-spec.md` §5.5 (off-chain local currency)

---

## Vision

The core loop has four rungs: **BUILD → HARVEST → DEFEND → OFFLINE**. This work order is the
fourth — the one that turns a session-based tower-defense into a game you *return* to.

> **NORTH_STAR:** *"OFFLINE: mines + pets keep gathering up to a cap → come back richer."*

While the player is away, every **claimed, lit-ward** resource node and every **harvesting pet**
keeps gathering at its rate. When the player reopens the app, the game computes how long they were
gone, grants the accrued haul (capped so it rewards returning without removing the reason to play),
and greets them with a **"Welcome back — your realm gathered while you slept"** summary.

The cap is the whole design tension: long enough that a player who checks in twice a day feels
rewarded, short enough that a player who checks in every few hours still has a reason to keep the
mines running and defended. **Suggested cap: 8–12 hours** (default **10h**) — tune in playtest.

This is the idle complement to the active defend loop: you don't *have* to play to progress, but the
node is **destructible** (WO-111) — leave it undefended and roaming enemies can stop the harvest. So
offline accrual is "claim it, fortify it, keep it lit, come back richer," never pure free money.

---

## Reconciliation — what already exists (build-up, not rebuild)

I inspected the save/economy layer before writing this. **Offline accrual does NOT exist yet** — it
is spec'd only (WO-111 Phase 5, NORTH_STAR). The pieces it hangs off, however, are built:

| Need | Exists? | Where / note |
|---|---|---|
| Persisted player state (SO) | **BUILT** | `GameState.cs` — 41 flat persisted fields |
| Save/load service | **BUILT** | `GameStateService.cs` — `Save()` Newtonsoft → PlayerPrefs key `dotr-save` |
| Save envelope timestamp | **PARTIAL** | `SaveSchema.SaveFile.ExportedAt` = `DateTime.UtcNow` ISO-8601 string — **envelope metadata only, not a `GameState` field, not read on load for accrual** |
| Unix-ms timestamp precedent | **BUILT** | `GameState.LastInboxSyncAt` (`double`, unix ms) — **mirror this exact pattern for `LastHarvestClaimMs`** |
| Resource wallet | **BUILT** | `GameState` — `AetherCrystals`, `Stone`, `Iron`, `Wood`, `Resources.{Food,Coins,Crystals}` |
| Off-chain award path | **BUILT** | write `GameState` resource fields directly / `CrystalEconomy.AddCrystals` (Core can't ref Village) |
| Per-node mine + yield | **BUILT** | `CrystalMine.cs` (passive per-wave); generalizing to timed auto-harvest = **WO-111 Phase 3** |
| Pet roster | **BUILT** | `GameState.Pets` / `Pet.cs` — **but pet auto-harvest behaviour does NOT exist yet** (WO-58 pet aura is a combat buff, not harvest). Pet accrual depends on WO-111 Phase 4. |
| Node claimed/lit gate | **spec'd** | WO-112 `LitWardIds` — only lit-ward node-claims accrue |
| Welcome-back popup | **NONE** | new, must be **code-built** (PIPELINE_STATE.md §8 — UXML does not work in builds) |

**So the new work is: a timestamp + an accrual service + a code-built summary popup — NOT a new
economy, save system, mine, or currency.** Reuse all of the above.

---

## 1. The mechanic — accrue-on-resume

On app resume (and on cold load), compute elapsed real time since the last harvest claim, accrue
each active source's yield over that window, clamp to the cap, grant it, and show the summary.

```
ON RESUME / LOAD:
  nowMs        = unix-ms now (see §3 for the time source)
  lastClaimMs  = GameState.LastHarvestClaimMs   (0 on a fresh save → no accrual yet)
  elapsedSec   = max(0, (nowMs - lastClaimMs) / 1000)        // clamp >= 0 — never negative
  cappedSec    = min(elapsedSec, OfflineCapSeconds)          // the 8–12h cap

  FOR EACH claimed+lit-ward node (and each harvesting pet):
     accrued[resourceType] += ratePerSecond * cappedSec      // floor to int per type

  grant accrued into GameState (per resource type) and Save()
  GameState.LastHarvestClaimMs = nowMs
  IF total accrued > 0: show WelcomeBackPopup(accrued, cappedSec, wasCapped: elapsedSec > cap)
```

### Illustrative DESIGN code only (CLI writes the real implementation)

```csharp
// DeNelle.Village — OfflineHarvestService.cs  (DESIGN SKETCH — not final)
namespace DeNelle.Village
{
    /// <summary>
    /// Computes resources accrued by claimed nodes + harvesting pets while the
    /// app was backgrounded/closed, grants them (capped), and raises a summary.
    /// Runs on cold load and on app-resume (OnApplicationPause(false)).
    /// </summary>
    public sealed class OfflineHarvestService : MonoBehaviour
    {
        // Tunable — owner to confirm in playtest. 8–12h window; default 10h.
        [SerializeField] private float _offlineCapHours = 10f;

        public OfflineHarvestResult ClaimAccrual()
        {
            var svc = GameStateService.Instance;
            if (svc?.State == null) return OfflineHarvestResult.None;

            double nowMs       = TimeSource.NowUnixMs();              // §3
            double lastClaimMs = svc.State.LastHarvestClaimMs;
            if (lastClaimMs <= 0) { svc.State.LastHarvestClaimMs = nowMs; svc.Save(); return OfflineHarvestResult.None; }

            double elapsedSec = System.Math.Max(0, (nowMs - lastClaimMs) / 1000.0); // clamp >= 0
            double capSec     = _offlineCapHours * 3600.0;
            double cappedSec  = System.Math.Min(elapsedSec, capSec);

            var result = new OfflineHarvestResult { WasCapped = elapsedSec > capSec, AwaySeconds = elapsedSec };

            // Only CLAIMED + lit-ward nodes accrue (WO-112). Pets only if assigned to harvest (WO-111 P4).
            foreach (var node in ResourceNodeRegistry.ActiveClaimedNodes())
                result.Add(node.ResourceType, (int)(node.RatePerSecond * cappedSec));
            foreach (var pet in PetHarvestRegistry.HarvestingPets())            // WO-111 Phase 4 — null-safe no-op until built
                result.Add(pet.ResourceType, (int)(pet.RatePerSecond * cappedSec));

            if (result.Total > 0) GrantToGameState(result, svc);                // write GameState resource fields directly
            svc.State.LastHarvestClaimMs = nowMs;                               // always advance the clock
            svc.Save();
            return result;
        }
    }
}
```

> **Design note:** the clock advances to `now` **even when nothing accrued** (e.g. no node claimed
> yet, or away < a node's first tick). Otherwise a player with no nodes would bank a giant haul the
> instant they claim their first node. The cap is applied to *elapsed*, not to each source's bank.

---

## 2. The "Welcome Back" summary popup — CODE-BUILT (not UXML)

When `ClaimAccrual()` returns a non-zero haul, show a one-tap summary on the first frame the player
is back. **It must be built in C# UI Toolkit / UGUI in code — NOT UXML.**

> **PIPELINE_STATE.md §8 — hard rule:** *"UXML in builds: does NOT work — always use code-built UI
> (learned the hard way)."* Build the panel the same way `CrystalMine.InjectUpgradePanel()` builds
> its `VisualElement` tree in code (see `CrystalMine.cs`). No `.uxml`, no `UIDocument` source asset.

Contents:
- Title: **"Welcome back, Keeper."**
- Sub-line: how long they were away (e.g. *"Your realm gathered for 10h (capped)"* — show the
  cap note only when `WasCapped`).
- One row per resource accrued: icon + `+N <Resource>` (Crystals / Stone / Iron / Wood / Coins).
- A single **"Collect"** button that dismisses (the grant already happened on claim — the popup is
  a *reveal*, not the transaction; never gate the grant behind the button, or a player who closes
  the app loses the haul).
- If `WasCapped`, a gentle nudge: *"Your mines filled up — keep them defended and check in sooner to
  catch every shard."* (the retention hook, never a scold).

**Trigger discipline:** show at most once per resume, only when `Total > 0`. Never show during an
active wave or ATB battle — defer to the next safe moment (village idle) if a wave is mid-flight.

---

## 3. Elapsed-time source — device clock v1, server-authoritative later

The accrual window is only as trustworthy as the clock it reads.

**v1 (this WO): device clock.** Use `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` via a thin
`TimeSource.NowUnixMs()` seam. Simple, offline-friendly, no network dependency.

- **Clock-tampering flag (known, accepted for v1):** a player can advance their device clock to fake
  elapsed time and over-accrue. Mitigations to apply *now* even on device-clock v1:
  - **Clamp negative deltas to 0** (clock set backwards → no accrual, no error).
  - **Hard cap** at `OfflineCapSeconds` means the max exploit per claim is one full cap window —
    bounded, not unbounded.
  - Optionally store the last-seen max timestamp and refuse to accrue if `now < lastSeenMax` (a
    crude monotonic guard) — owner's call, low cost.
- **Server-authoritative time (the hardening path — NOT this WO):** route the resume timestamp
  through the backend so the server, not the device, defines "now." This is the **WO-107-backend /
  WO-120 backend-reconciliation** lane (`docs/v2-unity-port-backend-spec.md`, `docs/anti-cheat-spec.md`).
  The `TimeSource` seam is built so v2 swaps the device read for a server read **without touching the
  accrual math**. Note it as a follow-up; do not block v1 on it.

---

## 4. Ward-tether gate — only claimed, lit nodes accrue

Per WO-112 §6 and §9, offline accrual must respect the ward-tether:

- A node accrues offline **only if its node-ward is lit** (its `CollectionPoint` is *claimed/active*).
  Unclaimed / out-of-reach nodes contribute **zero** — there is no "phantom" offline income from a
  region you haven't pushed into.
- WO-112 guarantees lit wards persist (`LitWardIds`) and restore on load **before** the first tether
  evaluation — so by the time `OfflineHarvestService` runs on load, the claimed set is already
  correct. Read the claimed/active node set; do **not** duplicate or re-derive ward state here.
- A node whose mine was **destroyed** while away (WO-111 destructible tension) stops accruing at the
  moment of destruction — but offline we don't simulate the attack timeline; **v1 simplification:**
  if the node is currently claimed+lit on resume, accrue the full capped window; if the mine is gone
  on resume, accrue nothing for it. (A future pass could persist a "mine destroyed at" timestamp for
  partial accrual — note only, not in scope.)

---

## 5. Save / persistence fields

Two new persisted fields, following the **exact** existing pattern (`LastInboxSyncAt` is the model —
a `double` unix-ms field that already round-trips through the save layer).

| Field | Type | Default | Purpose |
|---|---|---|---|
| `LastHarvestClaimMs` | `double` (unix ms) | `0` | Timestamp of the last accrual claim. `0` = never claimed (fresh save → seed to now, no accrual). |
| `LastSeenMaxMs` | `double` (unix ms) | `0` | *(optional, anti-tamper)* highest `now` ever seen; refuse accrual if `now < this`. Owner's call. |

Adding a persisted field touches the **full save round-trip** (CLI's job, all `.cs`):

1. `GameState.cs` — add the field(s) with default `0` (alongside `LastInboxSyncAt`).
2. `SaveSchema.cs` `PersistedState` — add `[JsonProperty("lastHarvestClaimMs")] public double? LastHarvestClaimMs;`
   (nullable, matching the `.partial()` convention of every other field).
3. `GameStateService.Snapshot()` — write the field into the payload.
4. `GameStateService.Restore()` — read it back (`if (p.LastHarvestClaimMs.HasValue) ...`).
5. `SaveMigrator.cs` — add a migration step defaulting older saves to `0` (= no retroactive haul),
   and **bump `SaveSchema.CurrentVersion`** per the existing migration discipline.

> Do **not** repurpose `SaveFile.ExportedAt` for this — it's envelope metadata (a formatted string,
> rewritten on every `Save()`), not a load-read `GameState` field. The accrual clock must be a
> first-class persisted `GameState` field so it survives and is read deterministically on load.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Village/OfflineHarvestService.cs` | **Create** — accrual compute + grant + raise summary; `OnApplicationPause(false)` + cold-load hook |
| `Assets/_Modules/Village/OfflineHarvestResult.cs` | **Create** (or nest) — per-resource haul struct + `WasCapped` / `AwaySeconds` |
| `Assets/_Modules/Village/TimeSource.cs` | **Create** — `NowUnixMs()` seam (device clock v1; swappable for server time later) |
| `Assets/_Modules/Village/UI/WelcomeBackPopup.cs` | **Create** — **code-built** summary panel (no UXML; mirror `CrystalMine.InjectUpgradePanel`) |
| `Assets/_Modules/Core/State/GameState.cs` | **Edit** — add `LastHarvestClaimMs` (`double`, default 0); optional `LastSeenMaxMs` |
| `Assets/_Modules/Core/State/SaveSchema.cs` | **Edit** — add `lastHarvestClaimMs` to `PersistedState`; bump `CurrentVersion` |
| `Assets/_Modules/Core/State/GameStateService.cs` | **Edit** — `Snapshot()` + `Restore()` round-trip the new field |
| `Assets/_Modules/Core/State/SaveMigrator.cs` | **Edit** — migration step defaulting old saves to `0` |
| `docs/NORTH_STAR.md` | **(optional, owner)** — tick the OFFLINE rung once shipped |

**Assembly discipline (CLAUDE.md §5):** the service + popup live in `DeNelle.Village`; save fields in
`DeNelle.Core.State`. **Village → Core only.** Granting writes `GameState` resource fields directly /
`CrystalEconomy` (Core can't reference Village — the established award path). Any HUD/Audio surfacing
(e.g. a chime on collect) goes through `CoreServices.Hud?` / `CoreServices.Audio?` with `?.` — never a
direct `DeNelle.HUD` reference. **No new `System.Reflection`.** Off-chain only — the accrued haul is
local currency, never a token/wallet mint (mirror `docs/tower-empowerment-spec.md` §5.5: *"No
blockchain — this is a local-only off-chain currency."*).

---

## Acceptance Criteria

- [ ] `GameState.LastHarvestClaimMs` persisted field added (`double`, default 0); round-trips through Snapshot/Restore/Migrator; `CurrentVersion` bumped; old saves migrate to `0` (no retroactive haul)
- [ ] On resume/cold-load, elapsed time = `now - lastHarvestClaimMs`; **negative deltas clamp to 0**
- [ ] Accrual = `rate × min(elapsed, cap)` per source; cap is 8–12h (default 10h), owner-tunable in inspector
- [ ] Only **claimed + lit-ward** nodes (WO-112) accrue; unclaimed/out-of-reach nodes contribute zero
- [ ] Pet harvest accrual is wired null-safe (no-op until WO-111 Phase 4 ships pet auto-harvest) — no crash when no pet harvesters exist
- [ ] The grant happens on claim and persists immediately; the popup is a **reveal**, never the transaction (closing the app after claim never loses the haul)
- [ ] `LastHarvestClaimMs` always advances to `now` on claim — even when nothing accrued (prevents a giant first-claim haul)
- [ ] Welcome-back popup is **code-built** (no `.uxml`, no `UIDocument` source) — confirmed against PIPELINE_STATE.md §8
- [ ] Popup shows per-resource `+N` rows, away-time, a cap note when `WasCapped`, and a single Collect dismiss
- [ ] Popup is suppressed during an active wave / ATB battle and during `Total == 0`; shows at most once per resume
- [ ] `TimeSource.NowUnixMs()` seam isolates the clock read (device clock v1) so server-time can swap in later without touching accrual math
- [ ] Accrual is off-chain local currency only — no token/wallet mint
- [ ] Brace balance check passes on every `.cs` touched; cross-module calls use `?.`; Village → Core only

---

## Do NOT touch

- **Do NOT build the popup in UXML** — code-built only (PIPELINE_STATE.md §8). UXML does not work in builds.
- **Do NOT invent a new save file or PlayerPrefs key** — extend the existing `GameState` + `SaveSchema` round-trip (key `dotr-save`); follow the `LastInboxSyncAt` pattern exactly.
- **Do NOT repurpose `SaveFile.ExportedAt`** as the accrual clock — it is envelope metadata, not a load-read `GameState` field.
- **Do NOT duplicate ward / CollectionPoint / mine state** (WO-110/111/112) — read the existing claimed/active node set; the ward only flips the gate.
- **Do NOT mint/credit any on-chain token or wallet** — accrual is local off-chain currency only (`docs/tower-empowerment-spec.md` §5.5).
- **Do NOT gate the resource grant behind the Collect button** — grant on claim, reveal in the popup.
- **Do NOT block v1 on server-authoritative time** — device clock with negative-clamp + hard cap is v1; server time is the WO-107/WO-120 backend lane (note only).
- **Do NOT generalize `CrystalMine` or build pet auto-harvest here** — those are WO-111 Phases 3/4. This WO consumes their output via a registry seam and is null-safe until they land.
- **Do NOT hand-edit `Village.unity`** — the service is a scene component placed via the architect lane / builder, never a hand-edit.
- Do not touch ATB, WalletService, monetization, or clan code.

---

🤖 Spec'd by the design lane (UI); reconciled against `GameState` / `GameStateService` / `SaveSchema`
(no offline accrual exists today; no last-harvest timestamp persisted — `LastInboxSyncAt` is the
pattern to mirror), `CrystalMine`, `Pet.cs` (no harvest behaviour yet), WO-111 Phase 5, WO-112
ward-tether, and the NORTH_STAR core loop. Markdown work order only — no `.cs` touched, no bake fired.
