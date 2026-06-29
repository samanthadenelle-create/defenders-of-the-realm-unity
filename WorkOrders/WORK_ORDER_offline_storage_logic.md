# WORK ORDER — Offline Storage / Offline-Accrual Logic (DESIGN)

**Status:** DESIGN COMPLETE — ideas + data schema only. **NO `.cs` written.**
**Type:** Foundation design (the offline-storage-UPGRADE store packs sell against this).
**Author:** design pass, 2026-06-28
**Owner ask (verbatim framing):** *"how much can they hold and stuff like that"* — the offline
storage / offline-accrual model that the storage-upgrade packs monetize.
**Companion data file:** `Assets/Resources/Data/Economy/offline-storage.json` (the authored table).

---

## 0. WHAT ALREADY EXISTS (read before building — do NOT greenfield)

A working offline/idle system is already in the tree. This design **formalizes and
data-drives it**, it does not replace it.

| Piece | File | Today |
|---|---|---|
| Echo pooled **silo** (the "barn") | `Assets/_Modules/Village/Harvest/EchoService.cs` | Capacity measured in **HOURS** (`SiloCapHours`, base 4h; upgrade hook `SetSiloCapHours` → 6h/8h). Rate = `echoCount * BaseRatePerHour` (120/hr). Fills online (per-frame) + offline (reads the clock), **clamped to cap**. `DumpSilos()` = come-back-claim-reset → splits to wallet (wood/iron/food). |
| Offline **node** accrual | `Assets/_Modules/Village/Harvest/OfflineHarvestService.cs` | Mines + settlements + pets accrue over the away-gap, **capped to `OfflineCapHours`** (10h), banked to `GameState`, raises welcome-back popup. Clock = `GameState.LastHarvestClaimMs` (Unix-ms), advanced atomically (no double-grant). |
| Welcome-back reveal | `Assets/_Modules/Village/Harvest/UI/WelcomeBackPopup.cs` | "Your realm gathered for Xh (capped)." One-tap Collect. Grant already banked; popup is a reveal. |
| Wallet | `Assets/_Modules/Core/State/NestedTypes.cs` `ResourceBalance` | `{ wood, iron, food, crystals, coins }`. |
| Persistence | `GameState.cs` / `SaveSchema.cs` **v25** | `SiloResources`, `LastHarvestClaimMs`, `EchoCount`, `WavesCompleted` already round-trip + migrate. |
| Premium currency | `packs.json` | **SKR** (wallet token) priced per pack; `harvest-auto-collect` convenience kind = the time-skip precedent. |

**The gap this design fills:** those caps/rates are **hardcoded Inspector tunables** with a
single ad-hoc upgrade hook (4→6→8h). There is **no authored upgrade ladder, no costs, no
premium fast-track, no store hook**. This WO turns them into one **data-driven table** the
runtime interprets, and defines the **storage-capacity ladder the packs sell**.

---

## 1. THE MODEL (data-driven)

Records, not branches. All values live in `offline-storage.json`; the runtime reads them.

### 1a. Accrual (the faucet)
- `accrual.baseRatePerHourPerEcho` (120) — resources/hour per active Echo.
- **Total rate** = `activeEchoes * baseRatePerHourPerEcho`; `ratePerSecond = rate / 3600`.
- `accrual.maxEchoes` (4), `accrual.wavesPerEcho` (5) — Echoes earned via the wave pillar.
- `accrual.resourceSplit` — fractions for how a Dumped silo divides across **wood/iron/food**
  (one slot flagged `remainder:true` absorbs rounding so no unit is lost). **Crystals are NOT
  in the silo** — they remain a premium/reward currency banked only by mine/settlement/pet
  nodes through `OfflineHarvestService`.

### 1b. Storage cap (the barn — "how much can they hold")
Two dials, one ladder:
- **Silo cap (HOURS)** — `siloBaseCapHours` (4). Absolute capacity =
  `siloCapHours * baseRatePerHourPerEcho * activeEchoes`. This is the Echo buffer. Hours-based
  so it auto-scales when you add Echoes (more echoes → more rate → proportionally bigger barn).
- **Offline window cap (HOURS)** — `offlineWindowBaseCapHours` (10). The away-gap clamp for the
  mine/settlement/pet faucet in `OfflineHarvestService`.

### 1c. Offline accrual cap (the "barn full, come back" stop)
The silo/window **fills to cap then STOPS** — idle waste past the cap is fair, not punishing
(this is the classic idle-game retention loop, and matches the live `AddToSilo` clamp). The
`WasCapped` flag drives the welcome-back nudge ("your mines filled up — check in sooner").

---

## 2. UPGRADE TIERS — the storage ladder (what packs sell)

`storageTiers[]` — a 5-tier shared-warehouse ladder. Each tier raises the **silo cap (hours)**
AND the **offline window (hours)**, with a **soft-currency cost** and a **premium SKR fast-track**.

| Tier | Name | Silo cap | Offline window | Soft cost (wood/iron/crystal/coin) | SKR fast-track |
|---|---|---|---|---|---|
| 1 | Woven Silo (start) | 4h | 10h | free | — |
| 2 | Timbered Granary | 6h | 12h | 400 / 150 / 0 / 250 | 30 |
| 3 | Stone Reliquary Vault | 8h | 16h | 1200 / 500 / 200 / 800 | 60 |
| 4 | Warded Hold | 12h | 24h | 3000 / 1400 / 700 / 2000 | 120 |
| 5 | Eternal Heartstore | 18h | 36h | 7000 / 3200 / 1800 / 5000 | 240 |

**Premium paths (`premium` block):**
- `skrFastTrack` per tier — pay SKR to **own that tier now** instead of grinding soft currency.
- `fastForwardSilo` (Spirit Surge, 15 SKR) — one-shot: instantly fill the silo to its **current**
  cap (time-skip only; cap unchanged). Maps to the `harvest-auto-collect` convenience kind.
- `packLinkedTiers` — an **offline-storage store pack grants a tier outright** (or advances the
  owned tier). E.g. Lanternlight → ≥ Stone Vault, Patron → ≥ Warded Hold, Founder's Vow →
  Eternal. This is the monetization hook: a pack = permanently owning a bigger barn.

**V2 option — per-resource warehouses** (`perResourceWarehouses`, `model:"per-resource"`, flag
`ff.perresourcestorage`): absolute per-resource holding caps with their own tier ladders, so a
pack can sell "Iron Vault +5000". Inert in V1 (shared-silo). Runtime picks the interpreter off
the top-level `model` string — no recompile to switch.

---

## 3. OFFLINE CALC — exact formula + worked examples

```
ratePerSecond   = activeEchoes * baseRatePerHourPerEcho / 3600
secondsOffline  = max(0, nowUnixMs - lastHarvestClaimMs) / 1000      // monotonic guard: clock-back → 0
capSeconds      = ownedTier.siloCapHours * 3600                       // (silo)  OR offlineWindowCapHours*3600 (nodes)
cappedSeconds   = min(secondsOffline, capSeconds)
gained          = ratePerSecond * cappedSeconds
siloNext        = min(siloCapacity, siloBefore + gained)             // siloCapacity = siloCapHours * rate
wasCapped       = secondsOffline > capSeconds
```
`siloCapacity` (absolute) = `siloCapHours * baseRatePerHourPerEcho * activeEchoes`.
**Collected on return** = the clamped delta above; the welcome-back popup reveals it; `Dump`
moves it to the spendable wallet and advances `lastHarvestClaimMs` to now (reset).

### Worked examples (base rate 120/hr/echo)

**A. 2 Echoes, Tier 1 (4h cap), away 3h**
rate = 2·120 = 240/hr. capacity = 4·120·2 = 960. gained = 240·3 = 720 ≤ 960 → **collect 720**, not capped.

**B. 2 Echoes, Tier 1 (4h), away 9h**
gained-uncapped = 240·9 = 2160, but capped at 4h → 240·4 = **960 (full barn)**, `wasCapped=true`.
→ 5h of potential harvest **wasted** → the upgrade nudge fires.

**C. Same player upgrades to Tier 4 (12h), away 9h**
cappedSeconds = 9h < 12h → gained = 240·9 = **2160 collected**, not capped. The upgrade
*directly* converted lost time into resources — the felt value of the purchase.

**D. 4 Echoes (max), Tier 5 (18h), away 24h**
rate = 480/hr. capacity = 18·120·4 = 8640. away 24h > 18h cap → 480·18 = **8640 (full)**, capped.
A weekend-away player on the top tier still wants the 36h *offline-window* nodes (mines) which
keep filling to 36h in parallel.

**Node example (OfflineHarvestService, Tier 4 = 24h window), 3 mines @ 5/s each, away 30h**
per-mine cappedSeconds = min(30h, 24h) = 86400s → 5·86400 = 432000, clamped to each mine's
finite reserve. Sum across mines = the banked haul, `wasCapped=true`.

---

## 4. THE FELT LOOP (why caps + upgrades engage, without being predatory)

1. **Return to collect** — the cap creates a *reason to come back*: leave, the barn fills, you
   come home to a satisfying haul + welcome-back reveal. A bottomless barn kills the check-in.
2. **Upgrade to hold more** — hitting the cap ("filled up, came back to waste") is the felt pain
   that *motivates* the storage upgrade. The ladder is the answer to your own frustration. Example C
   makes the value legible: the same 9h away pays 720 → 2160 after the upgrade.
3. **Optional premium skip** — SKR fast-track (own the tier now) or Spirit Surge (fill now) are
   **time-savers, never power**. Same covenant as the pack convenience items: a non-payer reaches
   every tier by playing; a payer just gets there sooner. No paywalled caps, no pay-to-win.
4. **Not predatory** — caps are generous (overnight at Tier 4, weekend at Tier 5), idle waste is
   transparent (the popup *tells* you it capped), and there are **no timers you must pay to avoid**,
   no FOMO loss of already-banked resources (the grant persists the instant it's claimed —
   closing the app keeps the haul; `WelcomeBackPopup` is a reveal, not a transaction).

---

## 5. SCHEMA + SAVE-MODEL NOTE

**Config** = `Assets/Resources/Data/Economy/offline-storage.json` (authored, read at runtime;
hot-tunable without a code change — the owner's data-first lever). Structure:
`accrual` (rate/echoes/split) · `siloBaseCapHours` / `offlineWindowBaseCapHours` ·
`storageTiers[]` (cap-hours + cost + skrFastTrack per tier) · `premium` (SKR fast-track,
Spirit Surge, packLinkedTiers) · `perResourceWarehouses` (V2) · `saveModel` (the persist note).

**Save model** (fits the staged local→cloud→Solana path; canon `data-architecture-hybrid-db-direction`
— config binaries stay OUT of the DB, only these scalars sync):
- **Already persisted (v25, no change):** `SiloResources` (current stored amount),
  `LastHarvestClaimMs` (the last-collected timestamp / accrual clock), `EchoCount`,
  `WavesCompleted`, `Resources{wood,iron,food,crystals,coins}`.
- **New (bump to v26 only if adopted):** `ownedStorageTier` (int, default 1 — index into
  `storageTiers`; replaces the hardcoded `EchoService.SiloCapHours` / `OfflineHarvestService.OfflineCapHours`),
  and `warehouseTiers` (int[] per resource, default `[1,1,1]`, only when `model="per-resource"`).
  Both are small scalar indices — migrator seeds defaults when absent (mirror the existing
  `EchoCount`/`SiloResources` migration in `SaveMigrator.cs`).

---

## 6. IMPLEMENTATION NOTES (for the future CLI work order — NOT done here)

- Add a small `OfflineStorageConfig` loader (`Resources.Load` of the JSON via the existing
  catalog source pattern) and have `EchoService` / `OfflineHarvestService` read
  `ownedStorageTier`'s cap-hours **instead of** their hardcoded fields. Keep the Inspector
  fields as fallbacks when config is absent (graceful degrade, per Guard §12).
- Wire `SetSiloCapHours` to be driven by `ownedStorageTier` rather than called ad-hoc.
- Storage-upgrade UI = a small Obsidian panel (one master-frame, per UI canon) listing the
  ladder, current tier, next-tier cost, Upgrade (soft) + Fast-Track (SKR) buttons. Spend via
  `EconomyService.TrySpend(ResourceCost)` for soft, `WalletService` for SKR.
- Add a `storage` contents slot to the relevant `packs.json` entries (`grantsTierAtLeast`)
  so a purchase advances `ownedStorageTier`.
- Regression-guard the offline calc (the clamp + no-double-grant + clock-advance) with a
  headless `DataRegression` oracle before felt-verify (§12/§13).

## 7. ACCEPTANCE (of this DESIGN deliverable)
- [x] Data-driven model: per-resource/shared caps, accrual rate, offline cap — as records.
- [x] Upgrade ladder (Tier 1..5) raising caps, soft cost + SKR fast-track, pack hook.
- [x] Exact offline formula + 5 worked examples (incl. a capped one + the upgrade payoff).
- [x] Felt-loop rationale (return / upgrade / optional skip; non-predatory argument).
- [x] Schema authored as runtime-interpreted data + save-model note on the staged save path.
- [x] No `.cs` written; grounded in the existing EchoService/OfflineHarvestService system.
