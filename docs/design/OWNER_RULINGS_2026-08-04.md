# Owner rulings — 2026-08-04

**Status:** DESIGN CANON. Rulings, not proposals. Recorded the same day they were given (§15).
**Why this file:** these decisions were arriving faster than they were being written down, and several
lived only in commit messages. A ruling that exists only in a commit message is one `git log` away from
being lost and re-litigated.

---

## 0. THE PILLAR — the satisfaction loop

> **Owner, 2026-08-04:** *"The idea I want to go towards is having so many resources (saving for some
> future upgrade) and having a raid attack you and lose massive quantities, or the invader stealing a
> portion as a reward."* — **"that's the satisfaction loop."**

**This is the frame every economy decision below serves.** The satisfaction is not in accumulating; it is
in a **real choice with a real cost on both sides**:

> *Hoard toward the big upgrade and carry the risk — or spend now and bank the progress safely.*

**Consequence for tuning, and it is the important one:** the choice only exists if the bank cap is
meaningful.

- **Cap too small** → nothing worth stealing → no tension, and hoarding is impossible anyway.
- **Cap too large** → one bad raid erases days → hoarding feels punishing rather than daring.

**The bank cap IS the tension dial for the whole loop.** Tune the loop by tuning the cap, not by tuning
the raid percentage first.

**Supporting fact:** the seam was authored for this and never got a consumer. `RepoProps.storageCapacity`
(`:153`) refers to a *"damage-to-stores loop"*, and `IsStorageContainer` (`:174`) is documented as
flagging a **"raidable stock CONTAINER."** WO-857 / WO-901 Phase F is wiring it now.

**Architectural constraint this must respect:** loot must be a **percentage of the TOTAL** (the CoC model),
never tied to a specific container's stored balance. Per-container balances would reverse **WO-842
(dual-wallet unify)**, which exists because two authorities disagreed and produced the captured symptom
*"985k can't afford 800."* `GameState.Resources` stays the single authority.

---

### 0a. The defensive half of the loop — walls protect the hoard

> **Owner, 2026-08-04:** *"Eventually when they build their own bases I want them to be able to upgrade
> walls around those higher or stronger for that reason."*

This is the **defensive answer** to §0's risk. The stockpile is what a raider wants; walls are how the
player chooses to protect it. It completes the choice: hoard, spend, **or fortify**.

**State of the scaffolding — verified at source 2026-08-04, and it is further along than expected:**

| Piece | Status |
|---|---|
| Player builds their own base | **LIVE** — strategic placement is always on; the flag was removed |
| Walls **upgrade** | **LIVE** — `wall_wood` + `wall_stone` both author `maxLevel: 3` and a 2-rung `repo.upgradeCost`. They were among the ONLY catalog rows authoring an upgrade ladder before the towers got one today |
| Walls **take damage** | **LIVE** — WO-853 closed the disjoint `IDamageable` / `IDamageableStructure` contract |
| **Gates upgrade** | ⚠ **MISSING** — `gate_stone` authors **no `maxLevel` and no `upgradeCost`** |
| Raid steals from the hoard | **NOT BUILT** — §0 |

⚠ **The gate is the hole, and it is the specific hole this design cannot tolerate.** A player can upgrade
the wall but not the door in it, so a raider simply walks the gate while the reinforced walls stand
untouched. **Any wall-fortification work must give `gate_stone` a ladder in the same pass**, or the
feature is defeated by its own weakest authored point.

**Sequencing note:** wall/gate upgrades are only *meaningful* once §0's raid-steal exists. Building the
fortification ladder first would ship a cost with no reason to pay it — the same shape as the four
authored-but-unreachable systems found on 2026-08-04. **Raid-steal first, fortification second.**

---

## 1. Storage caps — CLAMP AND WARN

Overflow is **lost**, and the player is **warned**. Uniformly — including where a holder exists.

An analysis argued this is not literally the CoC model (CoC storages *refuse*, and the collector keeps
holding) and recommended hold-back-where-a-holder-exists. **The owner considered that and ruled
clamp-and-warn everywhere.** One rule is simpler to reason about and simpler to signal, and it cannot
produce the confusing half-state where some overflow survives and some does not.

⚠ **The warn is load-bearing.** It is the only thing between the player and silently vaporised resources.
It must fire on every clamped grant, name the resource and the amount lost, and never be swallowed (§12).

---

## 2. Crystals are UNCAPPED

Premium / bottleneck currency. CoC precedent: gems uncapped, gold and elixir capped by storages.

- When the cap system is wired, crystals are **explicitly exempt by design** — a named constant plus a
  regression that fails if a crystal cap is ever introduced. **Not a comment.**
- With no cap, **the production RATE is the only brake**, so crystal faucets must be sized conservatively.
- ⚠ **Open, surfaced not decided:** if raid-steal ever touches crystals, uncapped means *unbounded
  at-risk*. That may argue for crystals being unstealable, which would be coherent with them being the
  premium currency. **Owner's call, still open.**

---

## 3. Container fill/drain order — BY CAPACITY, SMALLEST FIRST

> *"By capacity. Fill smallest first, so pallets drain last."*

- Order containers by **capacity, ascending** — not by current contents.
- **Fill** smallest-capacity first; **drain** in the same order.
- Net effect: the largest containers (the pallets) are the **last to fill and last to drain**. They sit
  visibly stocked while small buffers churn — the diegetic read is "big storage is a reserve."

**This supersedes** an earlier same-day line ("pull from biggest stacks first, so remove from pallets
first"). The owner revised it; the later ruling stands.

**Two properties worth protecting:**
- Capacity-ordering is **stable** (capacity changes only on place/upgrade), so the pallet props cannot
  flicker frame to frame. Contents-ordering would have reshuffled constantly.
- Fill and drain are **the same pure function** evaluated at different totals — there is no separate drain
  path and no drain state. Two code paths is how the pallets end up showing a state the wallet disagrees
  with.

---

## 4. Tower upgrade ladder — 1.0–1.2x then 2.0–2.5x

L1→L2 costs **1.0–1.2x** the place basket; L2→L3 costs **2.0–2.5x**.

**This supersedes the earlier same-day 4x/8x ruling**, which had already shipped into
`repo.upgradeCost[]` on all five towers and requires a retune. The ladder lives in the **catalog**
`repo.upgradeCost` array (owner ruling earlier the same day) — not in `Resources/Towers/*.asset`, which
no runtime path reads.

---

## 5. The kill-combo bonus pays GOLD, not crystals

> *"Make it gold."*

`KillComboTracker` granted 25 Aether Crystals at a 5-kill streak and 60 more at 8. Measured
(`docs/ECONOMY_REWARD_MEASUREMENT_2026-08-04.md` §5): **~1,435 crystals per 20-wave run against the
designed boss-drop rate of 3.6 — roughly 400x** — making an undocumented combo bonus **~70% of everything
a player banked**, and breaking the WO-830 guard in `echoes-balance.json` that crystals remain the
slowest faucet, which the entire WO-855 rebalance was calibrated against.

**The payoff is KEPT and the values are unchanged; only the currency moved.** Gold is the currency the
same measurement found correctly tuned (it tracks enemy HP at ~0.084 coin/HP across the whole roster).

Pinned by `[combo-pays-gold]` in `CrystalProductionRegression`, which fails in **both** directions —
if crystals come back, and if the gold payment disappears.

---

## 6. Collector capacity is measured in HOURS

Capacity scales on the same basis as rate, so **hours-to-full is constant** across level and echo count.
Target: **8 hours** (farm 7500 / lumbermill 5760 / forge 3456) — a twice-a-day check-in rhythm, sitting
just above the Echo silo's 4h so collectors read as the primary faucet and the silo as the bonus.

Before this, the curve **ran backwards**: capacity grew x3 from L1→L5 while throughput grew x5.6, so
**upgrading a collector shortened how long it could run unattended** (a 6-echo L5 farm filled in 5.7
minutes). Precedent for the fix is in-repo — `EchoService.cs:142-149` had already solved exactly this for
the Echo silo.

**Per-collector capacity IS the offline cap.** No second time-based cap; one mechanism, one dial.

---

## 7. Echo scaling is NOT to be nerfed (reaffirmed)

The quadratic is **intended** — WO-709, reaffirmed by `docs/design/ECONOMY_PROGRESSION_THESIS_2026-08-02.md`
(owner design pillar): *"12-15x at full roster is NOT a bug — it is the milestone… do NOT nerf the Echo
multiplier to compensate for a missing sink. Fix the sink."*

Measurement confirmed echoes were never the runaway: late-game the echo silo pays **8,856/hr** against the
collectors' **~2.1M/hr**. The collectors were cut; echoes were left alone.

---

## 8. Landscape is locked

Portrait autorotate disabled. Design landscape-only.

---

## Open owner questions carried forward

- **Raid-steal and crystals** — unbounded at-risk if steal touches an uncapped currency (§2).
- **The endless-mode reward runaway** — `_rewardScalingStepCap = 0` inflates payout +20% every 5 waves
  forever against difficulty that clamps at wave 60 (x41 at wave 1000). Needs a cap.
- **The apex dragon pays nothing** — 4,200 HP, the longest fight in the game, zero gold/XP/crystals.
- **Active play pays less than idling** — 0.24x per wall-clock, 0.83x per combat-hour, never reaching 1.0
  across 20 waves. The defend loop does not pay for itself.
- **WO-837 step 1 never shipped** — `lumberyard` is still in `BuildModeController.FoundingKit`,
  contradicting the ruling that storage buildings are never founding freebies.
- **The main-line WO range** — it collided with the UI seat's reserved 860–899 and now jumps to 900+;
  a permanent range needs ratifying.

*(Detail for each: `docs/ECONOMY_REWARD_MEASUREMENT_2026-08-04.md`.)*
