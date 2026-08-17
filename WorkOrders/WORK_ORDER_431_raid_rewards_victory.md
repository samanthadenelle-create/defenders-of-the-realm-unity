<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 431 — Raid Rewards, Victory Screen & Offline-Lite

**Status: READY TO IMPLEMENT**
Owner design 2026-06-14 (`Desktop/rewards and lose focus option.txt`) + architect assessment (this session).
Closes the raid loop: a raid CLEAR currently doesn't score or pay out — this is that piece.

## Goal
On raid clear: compute a **star rating**, compute **rewards** (`Base × Star × Level × Staking` + minor bonuses),
show a **victory screen** with a transparent breakdown, and **grant** the rewards. Plus **offline-lite**
resilience (pause/resume + offline result queue). Reuse-first — most hooks already exist.

## Reuse map (verified this session)
- **Clear detection EXISTS:** `RaidGarrisonSpawner.OnCleared` event + `Cleared` bool + `MarkCleared()` (fires when
  the last defender dies, line ~335). Subscribe to it — do NOT re-detect.
- **Star thresholds EXIST:** `SceneConfigDef.recommendedClearTime` (3★) / `twoStarTime` (2★) / `oneStarTime`
  (any clear) + `rewardMultiplier` + `shardDropChance` + `eliteCount` (added WO-430 session).
- **Payout EXISTS:** `EconomyService.Grant(ResourceCost)` / `GrantSpendable(wood,food,iron,crystals)`.
- **UI pattern EXISTS:** code-built uGUI via `ElarionUiKit` + `RaidSelectionScreen`/`RaidDeployScreen`
  (NEVER UXML — doesn't render in builds). Dark wood + gold trim, matches the doc.
- **Loss/exit EXISTS:** `RaidDeployController` retreat → `ArmyStorage.ReconcileAfterRaid` → `SceneRouter.GoCastle`.
- **Loss model:** wounded-recovery, no permadeath ([[raid-troop-slice-resume]]). Death in raid = port out.

## 1. Star scoring (new — small)
A `RaidScorer` (Village) subscribes to `OnCleared`, reads the config's timers + tracks:
- **elapsed** (start a stopwatch at raid load / first deploy) → vs `recommendedClearTime`/`twoStarTime`/`oneStarTime` → 1★/2★/3★.
- **troopsLost / troopsDeployed** (from `ArmyStorage` reconcile) → survival rate (minor bonus + "Perfect Survival").
- **underdog**: player level vs the config's `baseEnemyLevel` → small bonus for clearing below recommended level.

## 2. Reward formula
`Total = Base × StarMult × LevelMod × StakingMult`, then add minor flat bonuses, per resource.
- Base = config base reward (add a `baseRewardWood/Food/Crystal` to `SceneConfigDef`, or derive from
  `rewardMultiplier`). StarMult: 1★=0.5, 2★=0.8, 3★=1.0.
- LevelMod (underdog): small (+x% if cleared under recommended level).
- Minor: Lightning Clear (fast time), High Survival.
- **StakingMult — STUBBED NOW, chain-wired later.** Add `GameState.StakedSkr` (long, default 0) → save (v24,
  one field, append-only). `StakingMult = 1 + min(0.20, floor(StakedSkr / 50000) × 0.01)` (1%/50k, cap 20%).
  A real Solana stake-account query sets `StakedSkr` with the wallet integration; until then it's 0 (no bonus).
  It is a **blessing (adds %), never a gate** ([[owner-skr-personal-stake]] — on-brand for the SKR thesis).

## 3. Victory screen (new — code-built uGUI)
Per the doc layout. `RaidVictoryScreen` (ElarionUiKit, dark-wood/gold):
- **Top:** big glowing ★/★★/★★★ + particle, raid name, fanfare + shake on 3★.
- **Middle:** Left = Stars / Clear Time (3:47 / 5:30) / Troops Lost (2/12 or "Perfect Survival").
  Right = the multiplier breakdown rows (Base, ★ Rating, Underdog, Lightning Clear, Staking Blessing) — TRANSPARENT (player sees why).
- **Bottom:** TOTAL REWARDS (gold) + per-resource breakdown (+delta) + Echo Shards + "Claim Rewards" button →
  `EconomyService.GrantSpendable` + shard grant, then `SceneRouter.GoCastle`.

## 4. Offline-lite (SCOPE-BOUNDED — owner-approved staging)
**Do NOT build full per-entity mid-raid serialization (deferred).** Stage:
- **Brief backgrounding (95% case): in-memory pause.** `OnApplicationPause(true)` → `Time.timeScale = 0`
  (+ "paused" overlay); on focus → small 10-15s grace, then resume. Zero serialization. NO auto-forfeit.
- **OS kills the app (rare): a COARSE checkpoint** — raid id + objectives-done + elapsed (NOT entity HP/positions).
  On relaunch mid-raid: re-enter the raid at start with progress credited, or let them re-run (raids are short).
- **Internet loss: continue offline (already free — raid runs locally).** Show a non-intrusive "Connection Lost"
  banner, finish offline, **queue the result** and sync to the backend on reconnect ([[backend-persistence-pivot]]
  offline-first). Full rewards offline.

## Acceptance criteria
- [ ] Clearing a raid (garrison wiped) shows the victory screen with the correct star rating vs the config timers.
- [ ] Reward breakdown is transparent + math checks out (Base × Star × Level × Staking + minors).
- [ ] Claim grants the resources (EconomyService) + Echo Shards and returns to the castle.
- [ ] StakedSkr=0 → no staking bonus; setting it (dev) → correct capped % shows + applies.
- [ ] Backgrounding mid-raid pauses (no forfeit); refocus resumes after the grace.
- [ ] Internet loss → banner + finishable offline + result queued; full rewards.
- [ ] Save v24 migrates clean (StakedSkr defaults 0; no field reorder; one field).
- [ ] WebGL-safe (CanonicalJson for any reward data; code-built UI).

## What NOT to touch / scope boundaries
- Do NOT build full per-entity mid-raid resume (coarse checkpoint only).
- Do NOT block rewards on the Solana chain query — stub `StakedSkr`, wire the real query with the wallet later.
- Do NOT reorder SaveSchema fields (append v24, one field).
- Narrative gate ("The Stolen Hammer" first-raid unlock — reuses Borin's forgotten-hammer seed) is a SEPARATE
  follow-up when that questline is built; not in this WO.
- Code-built UI only (UXML doesn't render in builds).
