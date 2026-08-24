# PROD-015 — Arcane Spire costs 500 crystals and nothing in town produces crystals

**Status:** READY — needs an owner balance ruling. **Silo:** Economy/balance.
**Reported:** owner, 2026-08-24 — *"are arcane towers really 500 crystals?"* · *"it takes a long time to get 500 crystals"*.

## The numbers, read from `structures-catalog.json`

| id | display | crystals | iron |
|---|---|---|---|
| `tower_arcane_spire` | **Arcane Spire** | **500** | 160 |
| `arcane-tower` | Cathedral of Magic | 240 | 240 |
| `healing_caravan` | Healing Caravan | 760 | 400 |

⚠ Note the owner said *"arcane towers"* and the 500 belongs to the **Arcane Spire** (the defensive tower), not the **Cathedral of Magic** (Thrain's leveling building, 240). Those two are routinely confused — `building-tiers.json` carries a standing warning about exactly that pair — so name the id, never the word "arcane", when discussing this.

## ⛔ The real problem is not the price — crystals have NO TOWN FAUCET

- `mine_crystal` (**Crystal Mine**, `behaviorId: CrystalMine`, `maxLevel 3` — a real, working producer) is **LOCKED** out of the Town palette.
- The Cathedral has not yet absorbed the crystal-producer role — that is the WO-1168 ruling, **not yet implemented**.
- So crystals arrive only from raids and one-time sources.

⚠ **This is the same shape as the iron dead end the owner hit the same morning**, one tier up: the game prices something in a resource the town cannot produce. There, the producer existed and was locked out of the palette. Here, likewise.

⭐ And crystals are the resource that most matters: WO-1165 §3 found they are **the only currency that holds value** — uncapped, and gating rare+ gear — while wood/iron/food are capped and their sinks clear in ~4 hours.

## The ruling needed

**500 may well be correct once crystals have a source.** Judge the price *after* the faucet exists, not before — otherwise the "fix" is a discount for a problem that was never about price. Sequenced behind WO-1168 §4 (Cathedral becomes the crystal producer).

## Acceptance

- [ ] Crystals have a reachable town producer
- [ ] Owner re-judges 500 against real acquisition time with that faucet live
