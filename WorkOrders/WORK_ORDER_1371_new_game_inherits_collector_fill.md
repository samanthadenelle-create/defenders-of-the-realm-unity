# WORK ORDER 1371 - A NEW GAME inherits the previous save's collector fill: 14,089 free resources

**Status:** READY TO IMPLEMENT
**Silo / Lane:** Economy / harvest collectors + `GameStateService.ResetToNewGame`
**Type:** EXISTING system, economy defect
**Minted:** 2026-09-04 (CLI), from her live session
**Severity:** ⛔ **P1.** A fresh character is handed several hours of production instantly. It
destroys the early economy, the FTUE pacing, and any balance felt-test done on a new game.

## THE REPORT

Owner: ***"how did i manage to acquire 3000 stone when this game is about 25 minutes old"*** /
***"for some reason i got tons on harvest"*** / ***"is harvest reset to zero on new character"***.

## THE PROVING LINES

Source: `logs/device/freeze-20260904-095249.log`. A new game began at **09:44:43**
(`[Flow:Difficulty] DynamicDifficulty reset for a new game`). **Eleven seconds later:**

```
09:44:45.228  [Flow:Harvest] collector status -> full=2/3 maxFill=100% pending=14089 (near-full band)
09:44:54.082  [Flow:Harvest]   collect building=farm       +7500 Food wallet
09:44:54.092  [Flow:Harvest]   collect building=lumbermill +5760 Wood wallet
09:44:54.099  [Flow:Harvest]   collect building=forge      +829  Iron wallet
09:44:54.113  [Flow:Harvest]   collect-all total-banked=14089
```

⚠ **`Food` IS the visible Stone balance** - `GameState.cs:59-61` records that WO-1163 reused the
legacy Food slot for Stone. So she was granted **7500 stone** into a **3000** cap: capped, and the
overflow silently discarded (which is what produced the WO-1370 modal she could not read).

### ⭐ PROOF IT WAS NOT EARNED - the honest rate, measured minutes later

```
09:45:44  pending=29     09:47:30  pending=87     09:49:19  pending=145
09:46:34  pending=58     09:48:26  pending=116    09:50:10  pending=174
```

**~29 per 50s = ~0.58/s.** At that rate 14,089 is **~6.7 HOURS** of accrual, on a game 11 seconds
old. The fill is inherited, not produced.

## THE CAUSE - `ResetToNewGame` resets the wallet but not the faucet

`Assets/_Modules/Core/State/GameStateService.cs` DOES reset the adjacent state, which is what makes
the omission clear rather than deliberate:

| line | reset |
|---|---|
| `:1156` | `s.Resources = ResourceBalance.Starter;` |
| `:1223` | `s.LastHarvestClaimMs = 0;` *(New Game -> reseed the accrual clock on next load (no haul))* |
| `:1249` | `s.SiloResources = 0;` *(ECHO_WORKFORCE_SPEC - empty silo on New Game)* |
| `:1259` | `s.EchoLanes = "harvest:1";` |
| — | ⛔ **collector fill / pending: NEVER TOUCHED** |

⭐ **`:1223`'s own comment says the intent out loud - "no haul" on a new game.** The accrual CLOCK is
reseeded and the SILO is emptied, but the per-building collector fill survives, so the haul arrives
anyway through a different door. **This is an omission in an otherwise careful reset, not a design
decision** - and `:1117` already warns about exactly this class: *"so a 'new' game inherited the..."*.

## THE WORK

1. **Find where collector fill actually persists** and reset it in `ResetToNewGame`. ⚠ It is NOT an
   obvious `GameState` field - the grep for `collectorstate|pendingHarvest|collectorFill` finds only
   `CollectorStackView.cs`, so **establish the real storage before writing the reset**; it may be
   derived from a per-building timestamp, in which case the fix is to reseed that stamp (the same
   shape as `LastHarvestClaimMs`).
2. ⛔ **AUDIT `ResetToNewGame` FOR SIBLING OMISSIONS.** Two independent "new game inherits X" bugs are
   now on the record (this one, and the `:1117` note). **Enumerate every accrual/timer/fill source in
   the economy and prove each is either reset or deliberately carried.** A list, in the RESULT file.
3. **Decide the correct new-game state and say so**: zero fill, or fill seeded to the same starting
   point a founding town gets. ⚠ If that is a design question, it is the OWNER'S - ask, do not pick.

## ACCEPTANCE

- [ ] ⛔ **Proven from a capture**: `ResetToNewGame` -> the first `collector status` line reads
      `pending=0` (or the ruled starting value), NOT a carried figure. Quote before and after.
- [ ] ⛔ **Proven RED first** - reproduce the 14,089 inheritance, then show it gone (WO-1138).
- [ ] The full `ResetToNewGame` audit list from item 2 is in the RESULT file, each entry marked
      reset / deliberately carried, with a reason.
- [ ] An oracle fails if a fresh `ResetToNewGame` state carries any non-zero accrual. ⚠ This shipped
      to the production candidate with `REGRESSION_OK 358/358` green.

## WHAT NOT TO TOUCH

- ⛔ Not the collector RATE or the storage caps. 0.58/s and the 3000 cap are the WO-837 stockpile
      design working; the defect is the inherited starting fill.
- ⛔ Not the Food-slot-is-Stone mapping (WO-1163/WO-1212). It is deliberate and load-bearing.
- ⛔ Not the overflow-discard behaviour - that is WO-1370's copy problem, not a logic change.

## RELATED

- **WO-1370** - the unreadable HARVEST RESULT modal. **This ticket is why she saw it**: the inherited
  7500 stone overflowed a 3000 cap. Fixing 1371 removes the trigger; 1370 still needs doing.
