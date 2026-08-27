# WORK ORDER 1026 — The base is never attacked: close the CoC consequence loop

**Status:** FIXED 2026-08-27 - gated `COMPILE_GATE_OK` + `REGRESSION_OK 307/307 suites` (Builds/w8-c, Builds/w8-r). Slices S2-S5 landed; the floor/cap KNOBS are provisional and OWNER-PENDING. AWAITING OWNER FELT-VERIFY to close.

> Owner ruling 2026-08-17 (*"open ones follow your recommendations"*): **model (a)** — scripted/generated
> attackers assault the base on a cadence, reusing `WaveManager`, no backend.
>
> ⚠ THE STRUCTURAL CONDITION IS THE RULING, not a nice-to-have. (a) was chosen **specifically because it
> can become (c)**, so the attack REPORT / REPLAY ARTIFACT must be designed as DATA from the first line —
> a serialisable record of "who attacked, with what, where they broke through, what was lost". Build it
> that way and ghost-PvP later is a SOURCE SWAP (generated attacker -> snapshotted real layout). Build it
> as immediate UI state instead and (c) is a rebuild, which is exactly the cost this ruling exists to avoid.
> Do not hardcode "the attacker is generated" anywhere the report can see.
>
> ### ⛔ STILL OPEN — the stakes. I made no recommendation here and am not inventing one.
> §3's second question — **what does a loss actually cost the player?** — remains unruled. The CoC answer
> is stockpiled resources, but that collides with the storage-cap progression (memory
> `stockpiles-cap-capacity`) and the WO-947 basket ruling, and this WO explicitly forbids inventing an
> economy rule. Implementation may proceed on everything EXCEPT the loss consequence; that needs the owner.
> A safe interim: attacks resolve and REPORT, but take nothing, until stakes are ruled.
**Minted:** 2026-08-15 (UI seat) — provenance stack bumped 1026 → 1027 in the same edit
**Lane:** Raid / village defense. Design-led.
**Provenance:** owner ask 2026-08-15 — *"a full review from the lens of what makes COC fun and warcraft 3
fun, and determine where we need to strengthen the game"*. Full analysis:
`docs/DESIGN_REVIEW_COC_WC3_LENS_2026-08-15.md` §3 ⓵ (ranked **highest leverage**).

---

## 1. The gap, measured

Grep across `Assets/_Modules`, 2026-08-15:

| symbol class | hits |
|---|---|
| `RaidDefen*` / `IncomingRaid` / `WasRaided` / `RaidReport` / `OfflineRaid` | **0** |
| `DefenseReport` | **0** |
| `Revenge` | **0** |
| `Trophy` | **0** |

**Strategic placement is ALWAYS ON** (canon §7/§8 — movable functional storefronts, player-built town).
The player authors a layout. **Nothing ever shows that layout being tested.**

In Clash of Clans the loop is *design → watch it fail → redesign*. The watching is not a feature bolted
on the side; it **is** the game. Without it, every wall, every tower position, every storefront
placement is a decision the player makes blind and receives no feedback on. All the placement machinery
we have built is, from the player's seat, decorative.

## 2. Why this is cheap — the halves already exist

This WO connects shipped systems; it does not add a pillar.

- **Waves already attack the town.** `WaveManager` runs live assaults against the player's real layout.
- **The raid spine already resolves attacker-vs-base.** Raid V1 is built end-to-end (memory
  `raid-v1-spine-already-built`) — Teleport/Deploy, troops, structure damage.
- **Structures already implement the damage interfaces.** WO-853 dual-implemented
  `IDamageable` + `IDamageableStructure` on `WallSegment` / `Gate` / `DefenseTower` / `RaidSpire`, and
  widened the troop mask on both `TroopController` entry points (anchor 2026-08-09 §9).

What is missing is the **mirror and the record**: the player seeing their own base attacked, and a
consequence they can act on.

## 3. ⛔ OWNER RULING REQUIRED FIRST — do not implement before this is answered

**Where do attacks on the player's base come from?** The three answers produce very different games:

| model | what it needs | risk |
|---|---|---|
| **(a) PvE siege** — scripted/generated attackers assault the base on a cadence; player watches or defends live | Nothing new server-side. Reuses `WaveManager`. | Lowest risk, lowest social pull |
| **(b) Asynchronous PvP** — other players' towns are raided and yours is raided back | Real backend: base snapshots, matchmaking, loot rules, shields | Highest pull, highest cost. `api/` is **PREVIEW-only** today (anchor) |
| **(c) Ghost PvP** — real player layouts are snapshotted and replayed by AI, no live opponent | Snapshot storage + a replay of the sim | CoC's actual model. Middle cost |

**Recommendation: (a) first, structured so (c) drops in later.** It closes the feedback loop
immediately with zero backend, and if the *report/replay artifact* is designed as data from day one, (c)
becomes a source swap rather than a rebuild.

⚠ **Do NOT begin implementation until the owner rules.** The data model differs per branch, and picking
wrong means rebuilding it.

## 4. Scope once ruled (assuming (a))

**The deliverable is the FEEDBACK, not the combat.** The combat exists.

1. **A defense outcome record** — after any assault on the player's town: what attacked, where it
   entered, what it destroyed, what held, what was lost. Persisted as **data**, not just a toast, so
   (c) can later populate the same record from a snapshot.
2. **A surfaced report the player reads** — reachable from the town, showing that record legibly. This
   is the *"watch your base fail"* moment. Without it the loop stays open.
3. **A reason to redesign** — the report must make the failure point obvious (where the breach was), so
   the player forms an intent: *move that tower*.
4. **Stakes, sized by the owner** — what is actually lost. ⚠ Losing stockpiled resources is the CoC
   answer but it interacts with `stockpiles-cap-capacity` (memory) and the WO-947 cost-basket ruling.
   **Do not invent an economy rule here** — bring a proposal to the owner.

## 5. Explicitly OUT of scope

- Live PvP, matchmaking, clan wars
- Shields / revenge / trophies — these are *balancing* mechanics for model (b)/(c); they mean nothing
  under (a) and should not be built speculatively
- Any change to `WaveManager` composition or the smart-roster rules
- Any change to the raid attack flow (that is WO-774's lane)

## 6. Acceptance criteria (for model (a))

- [ ] An assault on the player's town produces a **persisted outcome record** — survives a session
- [ ] The player can **read that record in-game** and identify where their base failed
- [ ] The record is **data-shaped**, with the source (PvE vs snapshot) as a field, so model (c) is a
      source swap and not a rewrite
- [ ] A player who moves a structure and is attacked again sees a **different** outcome — the loop
      closes and the redesign has visible effect
- [ ] Zero changes to raid-attack behaviour (lane isolation from WO-774)
- [ ] `FlowTrace` instrumentation on record creation + surfacing, per §12 — this is a new subsystem and
      the trace is what makes its first bug cheap

## 7. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. Headless: assault a saved town, assert a record is written and reloads
3. **Screenshot the report screen** — memory `screenshots-are-primary-evidence-for-visual-defects`
4. Owner felt-verifies. ⚠ This one is *especially* a felt judgement: the question is not "does it
   work" but **"does losing feel like it was my fault, and do I know what to change?"** If the player
   cannot answer that, the loop is still open regardless of green gates.

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `no RaidDefen*/IncomingRaid symbols` — nothing built; stakes unruled. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

---

## 8. ⛔ STAKES — ONE PROPOSAL, FOR THE OWNER. NOTHING HERE IS BUILT.

**Status: AWAITING OWNER RULING.** This section is a decision document, not a spec. No option below
is implemented, and none should be started before the ruling. The shipped build resolves and reports
an attack and **takes nothing**: `StakesLedger` is all zero, stamped `none.interim.wo1026`, and
`DefenseReportContractRegression` **fails the gate** on a non-zero stake that has no new rule id.
That guard stays exactly as strict after this ruling — the ruling changes the expected rule id, it
does not relax the guard.

**The seam is one method:** `DefenseReportBuilder.BuildStakes`. Whichever option is chosen, the
change is (1) the arithmetic in that method + a new `StakesRuleId`, (2) one guarded, traced debit
through the **existing** wallet path at the `SiegeSession.Close` call site, (3) the oracle's stakes
case updated to the new rule id. The wire already carries the basket, so **no schema bump** either way.

### The question the stake has to answer

Not "how do we punish a loss" but: **what makes the player want to go and move a tower?** The report
now tells them *what to change* (first breach, attack path, front/second/core grouping, hold time per
structure). The stake is what makes them *bother*. That framing rules out anything that mostly
generates resentment, and it is the axis every option below is judged on.

### Option (a) — lose X% of the softest resource, up to a soft cap

| | |
|---|---|
| **What it is** | A failed defence takes a percentage of the single largest stockpile, capped. |
| **For** | Real, legible, immediately felt. It is what Clash does, so it needs no teaching (memory `design-tiebreaker-what-would-coc-do`). |
| **Against** | It is the option most likely to **collide with existing rulings**, and that is not a small caveat. Storage containers are a *capacity-cap progression* (memory `stockpiles-cap-capacity`, WO-1108b: six levels, 2000 → 34000), so a percentage steal is worth ~17x more to a late player than an early one — the same rule, wildly different sting. It also has to respect the WO-947 basket split (regular = wood+iron, magical = crystals; never one basket of all three), so "the softest resource" needs a definition that does not quietly cross that line. |
| **Risk** | Highest. It is a live economy change on a published game, and it interacts with two prior rulings that were expensive to settle. |

### Option (b) — lose a temporary shield duration

| | |
|---|---|
| **What it is** | Holding grants a no-attack window; losing forfeits or shortens it. |
| **For** | Costs the player nothing they can see disappear, so it reads as fair. |
| **Against** | **A shield is a PvP mechanism wearing a PvE costume.** Its entire purpose is to stop *other players* farming you while you are offline. Under model (a) the attacker is generated by our own scheduler — so a shield is the game protecting the player from a tap it controls, which it could simply not make. It also directly contradicts the shipped cadence design (away time becomes *pressure*, so you come home to a fight); a shield would mean away time sometimes produces nothing, and "nothing happened" is the failure state this whole WO exists to end. WO-1026 §5 already lists shields as out of scope for exactly this reason. |
| **Risk** | Medium build cost, low payoff, and it pre-commits a mechanic whose meaning only appears under (b)/(c). |

### Option (c) — lose NOTHING, but carry a visible "base was hit" SCAR until repaired ⭐ RECOMMENDED

| | |
|---|---|
| **What it is** | No resource is taken. Broken structures stay visibly broken, and the town wears the damage until the player repairs it. The stake is the **state of your town**, not a number. |
| **For** | It is the only option that is **already true** — `WaveDamageReport` prices repairs today, broken structures already persist as scannable shells, and `GameState.BuildingDamage` already round-trips. So the cost is real (materials + builder time, through the existing repair path) without inventing a second economy writer, without touching the storage-cap ruling, and without crossing the WO-947 basket split. It converts the report from a screen you read into **a chore list you act on**, which is precisely the "I know what to move" thought we are trying to produce. It is honest about scale: a small raid leaves a small scar. And it holds up under model (c) unchanged. |
| **Against** | It is the *gentlest* option. A player who does not care about a scruffy town feels no stake at all, and we will not know which kind of player she is until it is felt-tested. |
| **Risk** | Lowest by a wide margin — mostly presentation plus the repair loop that already exists. |

### Recommendation

**Ship (c) first, and treat (a) as a later, separately-ruled escalation on top of it.**

Three reasons. **(1)** It is the only option that needs no new economy rule, so it cannot collide with
the stockpile-cap or basket rulings — the two places this could go wrong expensively. **(2)** It makes
the consequence *visible in the town itself*, which is where the player is standing when they decide
what to move; a number deducted on a screen they have closed is not. **(3)** It is reversible. If it
felt-tests as too soft, (a) layers on top with the report and the seam unchanged. The reverse is not
true: shipping (a) first and walking it back means taking resources and then giving them back, on a
live published game.

⚠ **Explicitly NOT recommended: shipping any stake before the report has been felt-tested.** If
losing does not yet feel like the player's own fault, a stake does not add consequence — it adds
grievance, and it will read as the game being unfair rather than the base being badly laid out.
The felt bar (§7.4) comes first; the stake is what we add once it is met.

### What must NOT be pre-built while this is open
Shields / immunity timers, revenge targets, trophies or rating numbers (all (b)/(c) balancing that
means nothing under (a)), and any interaction with storage caps or the WO-947 basket split.

---

## 9. FOR THE OWNER'S FELT-CHECK — the town door, and the score

### The door: Settings → "Defence Reports"

**Implemented, and it needs no seventh bar face.** CLAUDE.md §7 caps the calm(town) action bar at SIX
visible faces; adding a face to reach this screen would silently undo that ruling. So the entry point
follows the precedent WO-588 set for the Game Guide — a secondary screen that must be reachable
without eating a bar face lives behind **Settings**. The row is built only when reports exist (no dead
button on a fresh save) and the unread count rides in the **label text** — "Defence Reports (2 new)" —
never a coloured dot.

**⚠ This is REACHABLE, not yet DISCOVERABLE, and that distinction is the felt-check.** Settings is two
taps from town, which clears the acceptance bar. But a player who never opens Settings will not learn
the report exists, and "a report she cannot reach is a report that does not exist" applies just as well
to one she never thinks to look for.

The candidates for a *discoverable* surface, deliberately NOT minted here because each is a felt call:
- an unread badge on the Heart (thematically exact — it is the thing that was attacked), which needs a
  world-interaction affordance the Heart does not currently have;
- a tab inside the Manage screen, which means editing `ManageScreenPanel` + `ManageTab` + `ChannelOf`
  — every existing tab maps to a queue channel and a report tab has none, so that ripples;
- a one-shot toast after a siege resolves, which is the cheapest and the most likely to be seen.

*(Rejected on inspection: `HudBuildingFocus`, the proximity-context seam towers and buildings use.
`HudActionBarModel` relabels the bar's Quests face to **Upgrade** whenever that focus is held, and the
Upgrade face is now the Manage door — so routing the Heart through it would hijack Manage and label a
defence report "Upgrade".)*

### The score: what it is, and when it refuses to answer

`DefenseScore` is **0-100, frozen at close, and presentation-only** — no reward, no matchmaking, no
stake, nothing gameplay-facing reads it. Keeping it inert is what stops a display weighting quietly
becoming an economy rule.

Derivation: start from the outcome (Held 100 / Breached 75 / Overrun 40); subtract 5 per breach capped
at 20; subtract up to 35 scaled by the fraction of the base destroyed; clamp 0-100. It prints as a
number **and a word** ("63/100 - Shaky") so it survives greyscale.

**It DECLINES — prints nothing at all — when the defender snapshot has no structure census.** Without
that count the destroyed-fraction term is undefined, and what is left would be the outcome enum wearing
three inputs' clothes: a confident-looking 75 that actually means "it was breached, and we know nothing
else". Same rule as an unmeasured hold time printing nothing rather than "fell in 0s". The oracle pins
the decline, the 0-100 bound, and that the score is monotone in the outcome (a clean hold can never
score at or below a breach).

---

# ★ OWNER RULING 2026-08-21 — LOSS STAKES, THE CONSERVATIVE BOUNDARY (unparks §3)

This closes the ruling that has kept `StakesLedger` at all-zeros (`none.interim.wo1026`) and
`FeatureFlags.Siege` OFF. Plug point is `DefenseReportBuilder.BuildStakes` - the SINGLE home.

## WHAT THE PLAYER LOSES (the complete list - nothing may be added to it)
1. **Troops used in the failed raid enter their NORMAL recovery/attrition path.** Not a new
   penalty - the existing `ArmyStorage.MarkWounded` / `AdvanceRecovery` spine, now on the ruled
   difficulty-scaled timers (Regular 5min / Hard 20min / Extreme 45min).
2. **Actually-damaged structures create a BOUNDED repair bill.** Existing single authority:
   `WallRepairController`, `ceil(buildCost x damageFraction)`, **crystals never charged**. Only
   what actually took damage - never a flat tax.

## ⛔ WHAT THE PLAYER NEVER LOSES (each line is a HARD NO, not a default to revisit)
- ~~**NO RESOURCE THEFT.** Nothing is taken from the bank, ever.~~
  **⚠ REVERSED THE SAME DAY — see "THEFT IS ALLOWED" below, which is the LIVE ruling.**
- **NO BUILDING DOWNGRADE.** A structure never loses a level.
- **NO DESTROYED PERMANENT PROGRESS.**
- **NO LOST STARS OR CLEARED-CAMP PROGRESS.** A cleared camp stays cleared.

## ⚠ RECORD-KEEPING NOTE FOR ANY FUTURE SEAT - THE RULING MOVED TWICE IN ONE EXCHANGE

The final position is **THEFT IS ALLOWED**, bounded as specified below. The two earlier positions
are recorded only so nobody mistakes a mid-exchange snapshot for the ruling:
1. An options prompt offered "repair bill + some stored resources stolen" and it was clicked.
2. The owner's written reply then said *"No resource theft."*
3. The owner then reversed that in the next breath: **"Allow theft, i think it causes real risk."**

**(3) IS THE LIVE RULING.** Do not re-litigate (1) or (2).

**Why this boundary:** it makes a siege COST something real (time + resources) without ever taking
away something the player EARNED. Losing banked resources while offline is the most-resented
mechanic in the genre, and this game's covenant is convenience and beauty, never punishment.

---

# ★★ THEFT IS ALLOWED — THE LIVE STAKES RULING (owner, 2026-08-21)

Owner verbatim: **"Allow theft, i think it causes real risk."** This SUPERSEDES the no-theft block
above. Risk is the point: a siege you cannot lose anything to is not a threat, and the whole
consequence loop depends on the loss being real.

## THE BOUNDS (all four are part of the ruling - none is a tuning default)

| Rule | Value |
|---|---|
| **Steal fraction** | **15% of CURRENTLY BANKED** wood / food / iron |
| **Protected floor** | anything below **~20% of that resource's capacity is UNTOUCHABLE** |
| **Crystals** | ⛔ **NEVER STEALABLE - HARD EXEMPTION, see below** |
| **Offline sieges** | **YES - they can steal too**, not only sieges the player fought |

Worked example: a full L6 container (34,000) loses ~5,100 of that resource - a real sting that
funds rebuilding. A nearly-empty player sits under the floor and loses **nothing**, so the mechanic
never kicks a player who is already down.

## ⛔ THE CRYSTAL EXEMPTION IS NOT A BALANCE KNOB
Crystals are **purchasable with real money** through the impulse packs. Taking a currency the player
PAID FOR converts a gameplay loss into a **refund request and a store dispute** on a LIVE published
title. Wood/food/iron are EARNED, so they are fair game; crystals are BOUGHT, so they are not.
Never "temporarily" enable crystal theft for a test build - the payment path is live.

## WHAT IS STILL NEVER LOST (unchanged by this reversal)
- **NO BUILDING DOWNGRADE** - a structure never loses a level.
- **NO DESTROYED PERMANENT PROGRESS.**
- **NO LOST STARS OR CLEARED-CAMP PROGRESS** - a cleared camp stays cleared.
- Troops follow their NORMAL recovery path (5/20/45 min by difficulty), never permadeath.
- The repair bill stays bounded: `ceil(buildCost x damageFraction)`, only what actually took damage,
  **crystals never charged** for repairs either.

## IMPLEMENTATION NOTES
- Plug point remains the SINGLE home: `DefenseReportBuilder.BuildStakes`. Replace rule id
  `none.interim.wo1026` with a real id; `StakesLedger` stops being all-zeros.
- ⛔ **The theft must be computed from the SAME persisted record the report reads**, so what the
  player is TOLD they lost and what the wallet ACTUALLY lost can never diverge. Two computations of
  one number is the defect class this ticket exists to avoid.
- An offline siege that steals MUST be legible on next launch - the player learns it from the report,
  never by noticing a number is smaller. An unexplained loss is the resented version of this mechanic.
- Respect the storage caps (`stockpiles-cap-capacity`) and the WO-947 basket separation; theft reads
  the cap to compute the floor, it does not invent a second capacity notion.


---

# DONE 2026-08-21 - WHAT SHIPPED, AND WHY THE STAKES ARE A SEPARATE TICKET

Owner, 2026-08-21: *"1026 was the defensive report didnt you do that?"* - yes. Closing it.

## SHIPPED (this ticket's section 4 deliverable)
- **Siege cadence** - `SiegeScheduler` / `SiegeSession` / `SiegeSchedulerBootstrap`, driving
  `WaveManager.ForceBeginNextWave()`. `WaveManager` stays the SINGLE attacker authority.
- **The persisted Defense Report** - `DefenseReportBuilder` + `StructureVitalsWatch` +
  `DefenseMapPlate`: what attacked, where it broke through, what held, how long each structure
  survived. **Shaped as DATA from the first line**, with the attacker source as a FIELD, which was
  the structural condition of the 2026-08-17 ruling - so ghost-PvP later is a SOURCE SWAP, not a
  rebuild.
- **The combat firewall**, fixed properly: the cadence and ledger wall-clock stamps moved OUT of
  the swept combat directories into `SiegeClock.cs`, so queue-time stays skippable and battle-time
  never is. Three oracles pin it (`DefenseReportContract`, `SiegeCadence`, `SiegeSpawnAuthority`).

## NOT SHIPPED, AND DELIBERATELY SPLIT OUT -> **WO-1139**
The loss stakes were ruled TODAY (theft 15%, floor-protected, crystals exempt, offline included -
see the ruling block above). They are a distinct piece of work touching the wallet and the repair
bill, and holding this ticket open for them would misreport a shipped subsystem as unbuilt.

⛔ **`FeatureFlags.Siege` STAYS OFF until WO-1139 lands.** The cadence would otherwise open sieges
that resolve and report but take nothing - the "safe interim" this ticket named, which is fine to
sit in the tree and wrong to ship as the finished loop.

---

## OWNER RULING 2026-08-27 - THE COLLISION IS RESOLVED. BANK THEFT REPLACES COLLECTOR LOOTING.

The 2026-08-26 siege ruling reinstated a system the 2026-08-22 ruling (WO-1139) had deleted.
Put to the owner as a direct collision; she ruled:

### 1. BANK THEFT **REPLACES** COLLECTOR LOOTING
A siege bills **ONCE** per attack, not twice. Collector looting is REMOVED.

⛔ **WO-1139 is SUPERSEDED, not ignored.** `SiegeLossStakesRegression` currently FAILS THE GATE IF THE
BANK MOVES AT ALL - it is the oracle for "COLLECTOR LOOTING ONLY. NO BANK THEFT." That oracle must be
**RE-POINTED to the new rule, never deleted**, and WO-1139 gets a `SUPERSEDED 2026-08-27` banner
rather than a rewrite (CLAUDE.md section 15). A green oracle turning red here is the ORACLE DOING ITS
JOB; do not route around it.

### 2. THE LOOTABLE SET
```
LOOTABLE      Wood, Iron, Stone, Coins
UNTOUCHABLE   Crystals, SKR, purchased goods, equipped gear
```

### ⚠ 3. "STONE" IS THE BALANCE INTERNALLY NAMED `Food`. THIS IS THE TRAP.
Owner verbatim: *"food was depreicated and is stone."*

`BankResource` has **no Stone member** - it is `Wood, Iron, Food, Crystals, Coins`. The HUD labels
`GameState.Resources.Food` as **"Stone"**, and WO-1212 confirmed that slot is the LIVE authority
(the field literally *named* `Stone` was dead code and has been retired).

So: **`BankResource.Food` IS Stone.** The enum member keeps the old name because it is a live
SAVE AND WIRE KEY and renaming it would break existing saves. Do NOT rename it. Do NOT add a Stone
member. Do NOT conclude from the name that Stone is unimplemented or that Food is a separate
lootable resource - that misreading is exactly how a siege would either take the wrong balance or
take one balance twice.

"Gold" in the ruling is `Resources.Coins`.

### 4. The floor and the cap are STILL UNRULED
A protected floor and a per-attack cap are both REQUIRED by the 08-26 ruling, but no numbers were
given. ⛔ Do NOT reuse the retired ruling's 15%/20% pair as defaults - they belong to the deleted
system. Implement the mechanism with the numbers authored in data, surface what the seam needs, and
ask. The acceptance test stays the owner's own sentence:

> "Damn, I should improve my defenses" instead of "The game erased something I paid for. Delete."

---

## OWNER RULING 2026-08-27 (b) - LOOTABILITY IS NOT ABOUT ORIGIN

Raised because the implementing lane spotted it and would not decide it alone: **every real-money
pack in `packs.json` sells `stone`, `coins`, `wood` and `iron`** - the exact four resources the
08-27 ruling made lootable. Crystals are protected; those four are not. And `BankGrantKind` is a
GRANT-TIME TAG ONLY (it exempts paid grants from the capacity clamp) - it is **not persisted**, so
once purchased wood lands in the wallet it is indistinguishable from mined wood.

**RULED: accept it. The PROTECTED FLOOR and the PER-ATTACK CAP are the protection - not origin.**

"Purchased goods are untouchable" means the DURABLE things: **crystals, SKR, equipped gear, and the
packs themselves.** It does NOT mean spendable materials. A siege may take a bounded slice of
wood / iron / stone / coins no matter how the player came by them.

### Why this is written this hard
A future seat WILL find a player losing wood they paid for and read it as a violation of the
untouchable list. It is not. Origin is deliberately NOT tracked, and adding tracking was considered
and REJECTED: it would need new persisted balances (`purchasedWood` etc.), a migration on every live
save, a correct tag on every grant path, and a spend-order rule - real migration risk on a live game
for a distinction the floor and cap already bound.

⛔ Do NOT add purchased-resource tracking to "fix" this.
⛔ Do NOT quietly move wood/iron/stone/coins to the untouchable list.
⛔ Do NOT weaken the floor or the cap - THEY are the protection this ruling rests on. If a loss ever
feels unfair, the lever is those two numbers, not the lootable set.

The acceptance test is unchanged and it is the test for the KNOBS:
> "Damn, I should improve my defenses" instead of "The game erased something I paid for. Delete."
