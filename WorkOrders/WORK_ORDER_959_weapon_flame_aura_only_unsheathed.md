# WORK ORDER 959 — Weapon flame aura shows ONLY while the sword is unsheathed

**Status:** DONE (implemented + gated 2026-08-10; RESULT filed; confirm the drawn/sheathed mapping named in the RESULT)
**Minted:** 2026-08-10 (CLI seat, main line — banner bumped 959 → 960 in the same edit)
**Silo:** Village/Hero gear aura — small gating change
**Origin:** owner RULING, F8 seq 2297, 2026-08-10 11:28, verbatim: *"can we agree to only show the
flames on the sword when unsheathed?"*

## 1. The ruling

The flameblade's flame aura (`Aura_Flame` — the `[Flow:GearAura]` system; e.g. `knight_flameblade`'s
element VFX) renders ONLY while the weapon is drawn/in combat stance. Sheathed/idle in town = no
flames. Applies to any element-carrying weapon aura, not just flame (one rule at the seam, not a
per-weapon patch).

## 2. Implementation notes (verify at source)

- Find the GearAura acquire/release seam (grep `GearAura` / `Aura_Flame` — the release-on-OnDisable
  path already exists per the live trace). Determine what "unsheathed" IS at HEAD: if there is a
  combat-stance/weapon-drawn state, gate on it; if the knight has no sheathe state and the sword is
  always in hand, the closest honest gate is the battle/combat state (`BattleLock.IsInBattle` or the
  hero attack-stance flag) — pick the one that matches what SHE means by unsheathed and name the
  choice in the RESULT (felt-verify will confirm).
- Acquire on stance-enter, release on stance-exit (reuse the existing release path); FlowTrace both.
  Composes with WO-929's deferred pool return (same VFX handle family, fix already in tree).
- Colorblind note: the flame is flavor, not information — no readability dependency to preserve.

## 3. What NOT to touch

Gear ownership/auto-upgrade (the 08-08 flameblade grant fix) · the aura's look (owner's) · VFX pool
internals (WO-929/955 landed this wave).
