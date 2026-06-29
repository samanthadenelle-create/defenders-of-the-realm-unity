# GROK_SYNC_PACK — lightweight project context for Grok

**Updated:** 2026-06-26 · **Branch:** `wip/village2-and-f8-tickets` (HEAD `8aa24c32`) · **Full anchor:** `CANON_GROUND_TRUTH_2026-06-26.md`

> You're a **second set of eyes** for a deep dive — not the primary. This is the lightweight current
> picture so your insights land on *today's* reality, not stale assumptions. If you need more depth on
> one system, ask for that file/log rather than the whole repo.

## Current reality (read first)
- **Hero:** ONE controllable Tripo self-rigged Knight ("Grom"). Everything else autonomous. *(Old "Blink full-body rig" is junked — ignore it.)*
- **World:** `MainCastle_Hall` (hub) ↔ `OuterWorld` (additive stream), joined by four-side warp gates (working). `Village2` = raid target; `Village.unity` abandoned.
- **Combat:** overworld real-time **BattleArena** (lock-on, 9-zone HUD). **ATB is flat/static** — not the animated combat.
- **Core loop:** Echo workforce (offline harvest) → build/upgrade → defend waves → raid. Base-defense/tower-defense is **V2-gated** (`ff.basebuilding`).
- **Creative canon:** living **world-Tree** (NOT a Cathedral Spire). Dialogue moving off Yarn to custom MVVM. Web build live on itch; Vercel parked.

## Open thread / next
1. **Targeting sweep** (`ff.enemystructureaware`) — UNVERIFIED: verify-capture showed 0 sweep acquires (hero-in-aggro gate suppressed it). Needs a skip-reason FlowTrace + a headless capture of the no-hero-near-structure case before it's claimed fixed.
2. Offline troop garrison (zero-cost defense + login rewards + repair) — queued (WO-430).
3. Triage 2 untracked `.cs` (`CastlePlaceCrossing.cs`, `RumorBoardPanelBootstrap.cs`).

## Key files (verified paths)
- `Assets/_Modules/Village/Buildings/TowerCombat.cs` — tower/targeting logic
- `Assets/_Modules/Village/Waves/WaveManager.cs` — wave loop / live enemies
- `Assets/_Modules/Village/Harvest/OfflineHarvestService.cs` — offline sim
- `Assets/_Modules/Core/State/GameStateService.cs` — troops, Echoes, save (v25)
- `Assets/_Modules/Village/World/RuntimeRegionGate.cs` — region seams / warp gates

## How we work (so your advice fits)
- **Instrument, don't guess** — no fix lands until captured data proves the cause (FlowTrace / F8 break-log).
- Single Unity gate, one committer (CLI). Doc-only changes are safe; code is verified headless before push.

## Handoff protocol
- Paste this file at the start of a Grok session for context.
- Deep dive on a specific bug → `logs/debug/<issue>.md` + the relevant `.cs` + `Player.log`.
- If this pack looks out of date, say so — it's kept current under the canon-maintenance rule (CLAUDE.md §15).

---
**Notes from Samantha:** _(add anything you want Grok to always remember)_
