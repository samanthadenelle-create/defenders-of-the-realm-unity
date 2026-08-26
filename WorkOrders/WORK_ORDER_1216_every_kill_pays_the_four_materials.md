# WORK ORDER 1216 - Every kill pays Wood / Iron / Gold / Stone, tuned so grinding funds repair

**Status:** READY TO IMPLEMENT
**Silo:** Economy / rewards
**Origin:** Owner ruling, 2026-08-26, from a Seeker felt-test on build `2026.08.26.341419`.

Owner verbatim, in the order the scope grew - read all four, the last one is the ruling:
1. *"bring iron cost down its very hard"*
2. *"or increase iron drop rate"* -> *"so more iron from kills"*
3. ***"the drop is any kill, not just waves but in the world the encounters"*** (scope correction)
4. ***"wood iron gold stone, balance it so i can afford to repair by grinding some kills"***

**The design target is a FELT one, and it is the acceptance criterion:** a player who has just been
hit by a wave should be able to afford **Repair All** by grinding a reasonable number of kills.
Measured reference from the owner's own device: **Repair All = 155 Wood / 78 Iron**.

---

## 1. THE TRAP - do NOT use `WaveManager._ironPerKill`

`WaveManager.cs:294` holds `_ironPerKill = 0` beside `_woodPerKill = 1`, and it looks exactly like
the answer. It is not:

- Its grant (`:3315`) is gated on `_phase == WavePhase.Active`, so it pays for **wave kills only**
  and silently misses every world encounter, outpost and arena kill - **precisely the scope the
  owner corrected in quote (3)**.
- It is a `[SerializeField]`, so changing the code default does not move an already-serialized
  scene value.

⛔ **Leave both fields exactly as they are.** They are not this ticket's lever.

## 2. THE SEAM - one owner, already built right

`Assets/_Modules/Village/Enemies/Enemy.cs:~3007-3025` - the WO-432/433 gold-on-kill grant. The owner
pointed at it directly: *"that's what needs to pay."* Every material rides this one path and
inherits what already makes it trustworthy:

- **Data-driven with a fallback so EVERY enemy pays** - `_def.CoinReward` when authored, else
  `Max(4, XpReward * 0.4f)`. No enemy can silently pay nothing.
- **Variance-rolled** through `EnemyDef.RollReward(base, variance)`.
- **MEASURED, never assumed** - the wallet is read either side of the mover, and ROLLED (asked) vs
  CREDITED (banked) are traced separately with a `FlowTrace.Warn` on any shortfall. The existing
  comment states the reason in as many words: *"Printing rolled as if it were final would be a
  hollow assertion."*

⭐ **Mirror that discipline per material.** Roll AND credit, both traced, per resource. A single
combined number is a hollow assertion - it cannot show WHICH material failed to bank.

## 3. ⛔⛔ THE STONE TRAP - THIS IS THE ONE THAT LOSES REAL MONEY

**There are TWO Stone balances and only one of them is the player's. This is WO-1212, a filed P0.**

- `Resources.Food` / `EconomyService.Food` is the slot the HUD renders as **Stone** and that every
  cost actually spends. **This is the player's balance. Grant here.**
- `GameState.Stone` (`GameState.cs:60`, seeded `= 20` at new game) is a **second persisted,
  server-guarded balance, displayed nowhere and spent by nothing.**

⛔ **Routing the Stone grant into `GameState.Stone` means the player kills an enemy, is told they
earned Stone, and receives NOTHING - silently, on a build that takes real money.** The obvious-
looking line (`state.Stone += n`) is the wrong one. Grant through `EconomyService` like the other
three, and prove it by reading the balance the HUD reads.

**Do not attempt to reconcile the two balances in this ticket.** That is WO-1212's job and it
carries its own owner ruling (retire `GameState.Stone`, DISCARD its value).

## 4. THE MATH - and the honest problem with the 25% the owner first ruled

The owner ruled iron at **~25% of the gold base**, derived rather than authored per enemy, so it
scales with difficulty for free and a newly added enemy pays automatically. That derivation shape is
**correct and stays**. What needs a second look is the constant, because 25% does not reach the felt
target in quote (4):

Average authored `coinReward` across the 19 enemies is roughly **10**, so at 25% a kill pays ~2-3 of
each material. Against a **155 Wood** Repair All that is **~60 kills - about four waves.** That is a
chore, not "grinding some kills."

**To land a Repair All at 20-30 kills (one to two waves), the constant needs to be nearer 0.60.**

| enemy | gold | at 0.25 | at 0.60 |
|---|---|---|---|
| hollow-walker | 4 | 1 | 2 |
| hollow-rogue | 6 | 2 | 4 |
| hollow-warrior | 10 | 3 | 6 |
| hollow-reaper | 28 | 7 | 17 |
| hollow-brute | 60 | 15 | 36 |
| necromancer | 120 | 30 | 72 |

**⚠ THIS IS A NUMBER THE OWNER MUST CONFIRM before it ships.** Implement the mechanism with the
constant in DATA so she can move it in one edit, start it at **0.60**, and put the measured
kills-to-Repair-All figure in the RESULT so she is ruling on evidence rather than a guess.

### Per-material rules

- **Wood, Iron, Stone**: derived from the gold base by the shared constant. Each rounds so the
  weakest enemy still pays **at least 1** - a kill that pays zero of a material reads as broken.
- **Gold**: the existing grant, **unchanged**. It is already the reference the others derive from;
  do not double-pay it.
- The constant (and any per-material override) is authored in a **balance JSON, dual-copy and
  versioned** - ⛔ never a code literal. Remember `Resources/Data/Canonical` WINS at runtime; both
  copies must be written and byte-identical.
- Apply the **same variance roll** gold uses so the payout feels like one drop, not four systems.

## 5. Context - why iron feels starved today

The entire current iron faucet is the wave-clear roll: `_ironRewardBase = 15`,
`_ironRewardSpread = 10`, `_ironRewardInterval = 4` - **15-25 iron on every 4th wave and nothing
else.** This ticket does not touch those fields; leave them so one felt-test attributes one lever.

## 6. Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. ⭐ A regression that **FAILS on today's tree**: kill an enemy on a **non-wave** path (a world
   encounter) and assert all four materials were **CREDITED** to the wallet the HUD reads. Prove it
   RED first - a test that passes before the change is decoration (WO-1138).
3. ⭐ A case asserting the **Stone** grant lands in `EconomyService.Food` and that `GameState.Stone`
   is **UNCHANGED** by a kill. This is the §3 trap, pinned so it cannot regress.
4. A case asserting the derived amounts track the gold base, so the balance constant is genuinely
   the value being read.
5. A `[Flow:Reward]` line on a real kill showing rolled and credited **per material**.
6. ⭐ **The RESULT file states the MEASURED kills-to-afford-Repair-All figure** at the shipped
   constant, against the 155 Wood / 78 Iron reference. The owner is ruling on that number.
7. Owner felt-verifies on device and CLOSES. Not the CLI.

## 7. What NOT to touch

- ⛔ `WaveManager._ironPerKill` / `_woodPerKill` / the wave-clear reward fields (§1, §5).
- ⛔ The gold grant, `AddCoins`, or the XP path.
- ⛔ `GameState.Stone` in any direction - that is WO-1212 (§3).
- ⛔ Repair PRICING (`WallRepairController.CostForFraction`). The owner chose to fix the faucet
  rather than the cost; changing both at once makes the felt-test unattributable.
