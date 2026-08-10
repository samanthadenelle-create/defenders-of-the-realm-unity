# WORK ORDER 948 — Walls: build at level 1 ONLY; higher tiers come from the upgrade verb (CoC model)

**Status:** DONE — implemented + `[wall-build-l1]` green 2026-08-10; owner felt-verify pending. See
the RESULT file.
**Minted:** 2026-08-10 (CLI seat, main line — banner bumped 948 → 949 in the same edit)
**Silo:** Village/BuildMode palette + Walls — coordinate with WO-1010's Castle Structures tab (shipped);
composes with WO-904 (which keeps the deeper ladder + gates behind raid-steal)
**Type:** owner PROGRESSION RULING

---

## 1. The ruling (owner, 2026-08-10, on first seeing the Castle Structures tab — which she loves)

> "I don't like the idea that we can allow them to start with a level two wall... we should enforce
> them to start with a level one wall, and then upgrade to level two, then upgrade to level three —
> like CoC does it."

CoC-confirmed: in Clash of Clans you can only ever BUILD a structure at level 1; higher levels exist
only by upgrading the placed piece. (Design tie-breaker memory: WWCD.)

## 2. Verified state at HEAD (2026-08-10)

- `BuildPaletteUI.cs:1105-1106` offers **BOTH `wall_wood` and `wall_stone` as separate placeable
  cards** — the player can place a stone wall directly on day one. This is the defect.
- `walls.json` (dual-copy, v1) already authors the real ladder: **L0 Wooden Fence (mult 1.0) → L1
  Stone (0.85) → L2 Steel (0.7) → L3 Spiked Steel** with per-tier meshes/heights.
- `heartDamageMultiplier` is LIVE — consumed by the wave loop, `WALL_MITIGATION` regression green —
  so the wood→stone rung pays off TODAY, independent of raid-steal.
- WO-904 (SPEC, blocked): owns the FULL fortification arc — gate ladder, deeper tiers, raid-steal
  framing. Its §2 sequencing ruling stands for that scope. WO-114 is CLOSED/superseded into it.

## 3. Scope (the enforcement slice ONLY)

1. **Palette:** remove `wall_stone` (and any other >L1 wall variant) from the placeable build palette.
   Exactly ONE wall card remains: the L1 wood wall. Keep the catalog ENTRY (existing saves replay
   placed stone walls via BaseLayout — they must keep working, selling, and rendering; only new
   PLACEMENT is closed).
2. **Upgrade rung (recommended in-scope, the one that already pays):** wire the wood→stone upgrade on
   a placed wall run through the EXISTING upgrade verb (WO-794 lane) reading the `walls.json` tier
   ladder — tier swap = mesh + heartDamageMultiplier + height from the data. If discovery shows the
   WallSegment tier-read genuinely is not built and the rung is disproportionate, land the palette
   enforcement alone and report the rung's true size — do NOT strand the ruling on the rung.
3. **Deeper tiers (Steel/Spiked) + gates stay in WO-904** behind raid-steal per its §2. This WO must
   not pre-build them.
4. Regression: palette case — the buildable wall set contains exactly one entry at L1; catalog case —
   any wall variant above L1 is not placement-reachable. Follow the existing BuildMenu/BuildEconomy
   suite patterns.

## 4. What NOT to touch

- Existing saves' placed stone walls (replay/sell/repair unchanged). Wall height cadence exclusion
  (canon: walls deliberately excluded from the height fit — pathable-gap hazard). `gate_stone`
  placement (a gate is a distinct piece, not a wall tier — its ladder is WO-904's). WO-947's cost
  ruling applies to any touched basket (walls are regular: wood/iron).
