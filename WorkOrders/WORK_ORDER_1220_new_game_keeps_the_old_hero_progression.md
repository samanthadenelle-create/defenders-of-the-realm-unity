# WORK ORDER 1220 - A NEW GAME keeps the old hero's level, XP and talents

**Status:** IMPLEMENTED + gate-green - OWNER DEVICE/FELT-VERIFY OWED (not FIXED/DONE)
**Silo:** Save / state reset
**Severity:** P0 — a "new game" is not new. Silent, persisted, cross-class power carryover on a LIVE
build that takes real money.
**Origin:** Owner felt-test, Seeker build `2026.08.26.341419`, 2026-08-26. Owner verbatim:
*"the town loads for a new player but i still have old skill points"*.

---

## PROOF — captured from the owner's device, not inferred

**1. The new game happened.** `10:49:17.190`:
```
[Flow:Save] ResetToNewGame: cleared 4 stale equip/loadout PlayerPrefs key(s)
            (dotr-equip-* + dotr-loadout-* + dotr-skillbar-*) - a new game starts on the
            class STARTER loadout, never an old equip and never another hero's hot-swap bar.
```

**2. The town DID reset — correctly.** `10:54:31.327`:
```
[Flow:Barracks] baked-barracks adoption SETTLED: blank-town surface gate CLOSED
                (Build Your Own founding)
[Flow:Perf] fps=41 ... towers=0 enemies=0
[Flow:HUD]  echoes 1/6
[Flow:Tutorial] walk-probe :: 'hero.reached:guide_gate' ... guideBody=ALIVE played=10s
```
Blank town, one Echo, FTUE running. ⭐ **The town is NOT the bug — it is the control group that
proves the reset ran.**

**3. The hero did NOT reset.** `10:54:04.660`, five minutes INTO the new game:
```
[Flow:HeroXp] attached HeroProgression to 'Hero (Blaise)' scene 'Main_Castle_Overworld'
              -> restored level=4 xp=3531.9 (fromSave=True)
[Flow:HeroHealth] max resolved: base 100 + gear 25 + talent 35 + cathedral 0 = 160
[Flow:HeroTalents] Aether Bond applied: +20 % mana regen (shared.n5)
```

**4. Visible on screen** — `tmp/resources-tap-105648.png`: HUD reads **`Sylas Lv 4 · Focus`** and
**`SK... 170`** on a game started seven minutes earlier.

**5. It is a CROSS-CLASS carryover.** The prior session was Thrain the **Mage** (`avatar=MageAvatar
| controller=Mage`, `10:33`). The new game is Sylas the **Ranger** (`avatar=RangerAvatar |
controller=Ranger`, `10:54:20`). A brand-new Ranger inherited a level-4 Mage's level, XP, talent
points and at least one unlocked talent node.

## THE DEFECT

`ResetToNewGame` clears equip / loadout / skillbar **PlayerPrefs**, and does **not** clear the hero
progression fields that save schema **v29** made PERSISTED — `heroLevel`, `heroXp`,
`heroLifetimeXp` — nor the talent state those unlocks hang off.

The reset was extended for every other subsystem (`EchoCount = 1`, `PopulationXp = 0`,
`BaseLayout` cleared, `BuildingTiers` cleared, `HeroLevel = 1`, `HeroXp = 0f` **are all present in
the same method**) — so read that block carefully before concluding a field is missing rather than
being overwritten later. ⚠ **The log says `fromSave=True` at `HeroProgression` ATTACH time, which is
AFTER `ResetToNewGame` ran.** That points at a restore that re-reads stale state, or a save written
between the two, not necessarily an absent assignment. **Instrument the order before editing.**

⛔ **DO NOT "fix" this by zeroing the fields blind.** WO-981 records that `HeroProgression`'s starter
latch **is not persisted — it is INFERRED from hero level** at `RestoreFromSave:202`, and that the
per-level grant at `:259` **silently drops a point on a null `SkillSystem`, every level**. A blind
zero can therefore change starter-latch behaviour and mask a second defect. Trace the ordering:
`ResetToNewGame` -> save write -> scene load -> `HeroProgression.Awake` -> `RestoreFromSave`, and
find which step reintroduces level 4.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. ⭐ A regression that **FAILS on today's tree**: run `ResetToNewGame` from a save carrying
   `heroLevel = 4` + unlocked talents, then assert level 1 / xp 0 / lifetimeXp 0 / zero unlocked
   talent nodes / zero unspent points **after the restore path has run**, not merely after the
   reset call. Prove it RED first — a test that passes before the fix is decoration (WO-1138).
3. A case asserting the CROSS-CLASS shape specifically: reset from a Mage save, start a Ranger,
   assert no Mage talent is applied.
4. ⭐ A `[Flow:HeroXp]` line on a real new game reading `level=1 xp=0 (fromSave=False)`.
5. Owner felt-verifies on device and CLOSES.

## What NOT to touch

- ⛔ `s.Stone = 20` in the same method — WO-1212 owns retiring the phantom balance.
- ⛔ The Wood/Iron zero-seed (owner ruling 2026-07-13) and the new Gold 200 seed (WO-1217).
- ⛔ The blank-town / `everBuiltStructureIds` / `strategicPlacementMigrated` path — it is WORKING
  and is the control group in §PROOF item 2.
- ⛔ The starter-latch inference at `RestoreFromSave:202` without reading WO-981 first.
## LANDED-WORK AUDIT (2026-08-26)

The full reset implementation and behavioural fixture landed in `b303c4fbf`. Fresh evidence:
`Builds/batch0-compile-2.log:1966` `COMPILE_GATE_OK`;
`Builds/batch0-regression-2.log:83606` `RESET FULL CLEAR OK` sweeps 85 persisted fields, preserves
only 13 named carve-outs, force-reseeds the zone graph, clears settlements, and passes the EditMode
fixture; `:83814` is `REGRESSION_OK 291/291`. Remaining acceptance: owner device felt-verification
that New Game visibly starts with fresh hero progression, then owner close.
