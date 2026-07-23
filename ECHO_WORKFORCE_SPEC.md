# Echo Workforce — V1 Spec (owner-resolved 2026-06-26)
### The farm pillar's player-facing loop. Implement the MODEL against the REAL systems (not the placeholder pseudocode).

## The model (owner)
- **Progression:** start 1 Echo (auto-assigned to a node). +1 Echo every 5 waves completed (4 normal + 1 boss). Wave 5→2, 10→3, 15→4, 20→5, 25→6 (**MAX 6** — corrected 2026-07-22; code ships `EchoService.MaxEchoes = 6` with the full 6-soul `EchoRosterCatalog`: Aldwin/Elowen/Corvin/Bran/Doran/Maren. The old "MAX 4" was never the shipped cap). Unlocks feel earned via the defense/wave pillar.
- **Harvest:** each Echo auto-harvests a node at a fixed rate (resources/hour, configurable). Pooled silo for V1 (shared, simpler).
- **Silos = buffer + engagement hook:** capacity in HOURS (base 4h → upgrades 6h/8h). Fills while online + offline, CAPPED. **"Dump"** = one-tap transfer silo → main resource bins, resets the timer. Come-back-claim-reset is the loop. Idle waste if ignored past cap (fair, not punishing — optional partial credit past cap).
- **Balance:** Echoes = the free soft-currency faucet; waves are how you EARN more Echoes; Pi premium = overfill silo once / extend cap / instant-dump+bonus (NOT progression gates).
- **Feel tweaks (later):** cute animated workers (reuse family assets), mild diminishing returns/upkeep on later Echoes, partial offline credit past cap.

## Integration to the REAL code (CLI mapping — do NOT use the placeholder APIs)
- **Offline accrual ENGINE = existing `OfflineHarvestService`** (`Assets/_Modules/Village/Harvest/OfflineHarvestService.cs`) — it already computes elapsed time from persisted `GameState.LastHarvestClaimMs` (Unix-ms, 10h cap), banks atomically, no double-grant. DO NOT use `Time.time` (resets per session = wrong for offline). The Echo silo accrual should be an accrual SOURCE the offline service integrates (rate = echoCount × ratePerHour, clamped to the silo HOUR cap), OR EchoManager reads the same persisted timestamp delta. Reuse, don't reinvent.
- **Save = real `GameState` + `SaveSchema`** (`Assets/_Modules/Core/State/`). Add `echoCount` + `siloResources` (+ wavesCompleted if not already tracked) to `PersistedState`/`SaveSchema` with a schema version bump + migrator default. Reuse `LastHarvestClaimMs` as the silo clock. NOT a fictional `SaveSystem.Instance`.
- **Dump = `EconomyService.GrantSpendable`** (NOT `AddResources`/`Grant`) so Wood/Iron persist into GameState + reach the building-upgrade ledger (the Wood/Iron routing fix). Split the pooled silo across the configured resource types.
- **Wave unlock = the real `WaveManager`** — hook its wave-complete / boss-wave event (there's a `WaveXpBridge` pattern to mirror). On `wavesCompleted % 5 == 0` → unlock next Echo (≤4) + "New Echo joined!" feedback.
- **Reconcile with the existing `WorkerManager`** (the 1-capsule click-stub): the Echo model REPLACES it as the V1 workforce abstraction (or the capsule becomes the Echo's visual). Don't run two competing harvest systems.
- **HUD:** Echo count + silo fill % (`silo / (capHours × ratePerHour)`) + a **"Dump All"** button calling the dump. Code-built uGUI (no UXML).
- **Anti-tamper:** the light server-handshake (PI_INTEGRATION_SPEC §4) is a fast-follow; V1 = device clock via the existing OfflineHarvestService clamp.

## V1 acceptance
Start 1 Echo auto-farming → silo fills (online + offline, capped) → Dump banks to bins (persisted, visible to upgrades) → beating 5 waves unlocks Echo 2 (…→6, corrected 2026-07-22) → more Echoes = faster fill. Persists across sessions. Ties: waves → echoes → farm → build/upgrade.
