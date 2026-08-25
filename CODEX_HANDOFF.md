# ⭐⭐ NEXT UP — read this before starting anything (owner directive, 2026-08-24)

> *"hold six, put codex on batch 1"*

## ⛔ HOLD BATCH 6. Do not work `D:\eoa-codex-six`.

**Batch 6 has not been selected.** The feeding agent is still running its disjointness pass, and
⚠ **a worktree opened for an unselected batch is exactly the gap that agent exists to close** — it is
how WO-1069 and WO-1177 ended up shipped together in one batch, colliding on one file. Park it.

## ⭐ BATCH 1 IS THE CRITICAL PATH — and it is entirely unimplemented

Verified at HEAD on 2026-08-24: `USD_ANCHORS['hearth-spark']` is **still `1.99`**
(`api/_lib/purchase-catalog.js:84`), and `grep -ci discount` returns **0** across
`purchase-catalog.js` and `api/purchases/quote.js`. Only the intake pass ever ran.

⭐ **Four batch-5 tickets are queued behind these three.** Finishing batch 1 unblocks the longest
queue on the board; adding a fifth parallel lane unblocks nothing.

### The order, and it is not negotiable

| # | WO | Why this position |
|---|---|---|
| 1 | **WO-1069** | ⛔ **ONE SEAT with WO-1177, SEQUENTIALLY.** Both edit `api/_lib/purchase-catalog.js` and both disturb `test/purchases.quote.test.js`. ⚠ Split across two seats, the second merges onto a moved anchor table and a moved test. |
| 2 | **WO-1177** | Same seat, after 1069. Its `buildQuoteBody` change sits in the block 1069 reprices. |
| 3 | **WO-1178** | Disjoint (`tools/`) — may run in parallel with the above, or after. |

### All four intake blockers are ANSWERED — the tickets carry the rulings

- **WO-1069** — ⛔ **do NOT modify `ShortfallPackOffer`.** Codex was right to refuse: the resolver
  enforces WO-947 guardrail 1 and the ticket had asked for a violation. **The fix is DATA:
  `hearth-spark` 1.99 → 4.99.** ⚠ It is `DEVNET_CANARY_SKU` (`:29`) — **re-check the quote/verify
  test path against the new anchor**, or the canary asserts a price that no longer exists. Then add
  the regression: **no impulse rung strictly dominated at its own USD anchor.**
- **WO-1177** — ⭐ **stop trying to prove the shortfall.** A forged `reason` yields exactly what a
  genuine one yields: **one discount per window.** ⛔ Build **no** attestation rail. **Window = 7
  days**, server-recorded. The discount applies to `usd` **inside `buildQuoteBody`, before
  `quoteAmount`** — the client never sees a pre-discount number.
- **WO-1178** — the raw-invocation hole **cannot be closed**; the ticket no longer asks for it.
  ⭐ Its most valuable half has nothing to do with editors: **the runner must assert the MARKER
  instead of returning a bare exit code.**
- **WO-1173 / WO-1072** stay sequenced behind WO-1177 — both touch the same file.

⛔ **Cite `FOUNDATIONAL_RULINGS.md`; do not restate it** in code comments or ticket edits. A fact
written twice is this repo's dominant failure mode, and one fresh instance was created today by the
very process built to prevent it.

## ⚠ Batch 4 continues in parallel — it is file-disjoint from batch 1

`D:\eoa-codex-batch4` holds **WO-1163**, **WO-917 Phase B**, and **WO-1179's core**.
⭐ **WO-1161 needs no edit** — verified byte-identical in both canonical copies, corrected 2026-08-23
by `4f6dfc251`. Its banner was stale; the clearing agent is fixing it. ⚠ Its **§6 remains open**
(`EchoCardVM.FaucetBuildingIdFor` still routes iron to `collector_forge`) and is a **separate change
needing a captured run**.

---

# THE OPERATING LOOP (owner, 2026-08-24) - read this first

```
  LEAD  ---- selects a batch, writes it HERE ---->  OWNER tells Codex it is here
    ^                                                        |
    |                                              Codex works in its OWN worktree
    |                                                        |
    |                                              OWNER tells lead it is resolved
    |                                                        v
    +--- next batch <--- SELECTOR agent      LEAD verifies -> gates -> commits
                              ^                              |
                              |                              v
                              +----------------  BOARD agent reflects status
```

**Roles, and the reason each exists:**

| Who | Does | ⛔ Never |
|---|---|---|
| **Lead (this seat)** | Selects, specs, **verifies at source**, gates, commits by explicit path | Never trusts a handback's summary over the tree |
| **Owner** | Routes batches to Codex, says when they are resolved, **felt-verifies and closes** | - |
| **Codex** | Implements in its own worktree; **refuses wrong specs at intake** | Gate, commit, push, close, or invent policy |
| **BOARD agent** | Reflects `**Status:**` lines as work lands | ⛔ Never marks DONE - only the PO closes |
| **SELECTOR agent** | Picks the next batch that can safely go out | ⛔ Never assumes disjointness - **proves it** |

## The two standing agents, precisely

### BOARD agent - reflects, never closes

⛔ **BRIEF IT ON THE WHOLE BUCKET, NOT A DATE SLICE. This is a lesson paid for on 2026-08-24.**
The agent was told to reconcile *today's work against today's commits*. It did that faithfully and
found six drifts. Codex then reviewed **every Fixed row regardless of age** and found **seven more** -
WO-1157 and WO-1060 had been green and wrong since **08-23**, outside the window the brief gave.
⭐ **The sweep was not wrong, it was scoped too narrowly, and the scope was the lead's.** A
date-scoped sweep silently certifies everything outside the date. Standing instruction: **sweep the
whole Fixed bucket every time**, and read each row against its own body, not against the commit log.


`BOARD.html` is **generated** (`python tools/board_build.py`) and therefore **cannot drift**. What
drifts is the `**Status:**` line inside each work order. That is the only thing this agent edits, plus
a dated one-line note. ⛔ **Shipped code is `FIXED <date> (<sha>) - awaiting owner felt-verify`, never
DONE** - §13 reserves closing for the PO. A wrong DONE is worse than a stale line: it makes a live
ticket invisible.

### SELECTOR agent - proves disjointness
Its whole value is the check nobody makes by eye. ⭐ **Today that check caught WO-1173 and WO-1177
both editing `api/schema.sql`** - handed out together they would have had two seats in one file, with
the parity gate written against a schema moving underneath it.

A ticket is only handable when **all five** hold:
1. **Spec-complete** - a spec Codex must guess at returns as work the lead re-derives.
2. **File-disjoint** from every in-flight batch AND from whatever the lead is editing - proven by
   listing the files, not assumed from the silo name.
3. **No open owner ruling.** If a ruling is owed, it is a question, not a ticket.
4. ⚠ **Not already shipped.** Several tickets' own Status lines admit they do not know. **WO-822
   nearly went out today saying exactly that.** When in doubt it goes to the RCA lane, not the dev lane.
5. **Not scene/bake work** and not gate-dependent.

---

# Codex handoff - the standing rules, and what is ready right now

**Owner ruling 2026-08-24:** *"think of Codex as your dev team"* / *"and you are lead."*
Codex implements. The CLI lead specs, verifies, gates and commits. This file is what you hand Codex.

---

## The four rules. All four, every time.

1. ⛔ **Work in your OWN branch or worktree. NEVER the shared working tree.**
   Two writers in one tree is how `DungeonCompose.unity` came back NUL-corrupted, and it is why
   CLAUDE.md §0 exists at all. If you are unsure whether you are in the shared tree, you are.
2. ⛔ **Do NOT `git commit`. Do NOT `git push`. Do NOT run `vercel`.**
   There is exactly ONE committer. Two committers duel on `.git/index.lock` and produce a **false
   "pushed"** - the work looks shipped and is not. Write the code, say "ready", stop.
3. ⛔ **Do NOT run the compile gate, regressions, bakes, builds, or `adb`.**
   The single Unity instance is the coordination point and the lead holds it. A second batchmode run
   takes the project lock and both fail.
4. ⭐ **Report what you could NOT find, and where the spec did not match the code.**
   This is worth more than the code. Two handbacks today corrected the ticket that commissioned them -
   one found that the regression suite named in the spec had **zero** relevant cases, another that a
   `FlowTrace` helper I told it to use would have **silently discarded** the very signal it was
   added to capture. Both were right and both changed the work. **Push back in the report; do not
   silently force a spec that does not fit.**

## What makes a ticket safe to hand out

- **Spec-complete** - a spec you have to guess at comes back as work the lead re-derives from scratch.
- **File-disjoint** from every other in-flight ticket, and from whatever the lead is editing.
- **No open owner ruling.** If the ticket says a ruling is owed, it is not ready - it is a question.

### ⭐ WHY THE CADENCE PRODUCES QUALITY, NOT JUST SPEED (owner, 2026-08-24)

> *"By doing this in a cadence, we can keep things moving fast with high quality because two
> different AI units are verifying the solution."*

That is the load-bearing idea, and **2026-08-24 proved it runs in BOTH directions** - which is the
only thing that makes it verification rather than ceremony:

- **Codex -> lead.** It refused batch 1 at intake and was right twice. **WO-1069** asked the resolver
  to serve `hearth-spark`; that pack is not an impulse SKU and the guard that rejects it enforces a
  **binding WO-947 ruling** - the ticket asked for a violation. **WO-1178** proposed a pre-run version
  check; a raw launch rewrites the file *afterwards*, so the check passes and the damage still lands.
  Both were **my** specs.
- **Lead -> agents.** A `FlowTrace.Throttle` instruction I gave would have demoted a Warn to Info and
  discarded the repeat that was the entire signal. An agent caught it. Separately, a `MaterialsZero`
  premise I wrote named the wrong regression suite - the file had **zero** relevant cases.

⛔ **THE CONDITION, and it is fragile:** the two units must be **genuinely free to disagree**. Codex
must be able to refuse a spec, and the lead must **verify at source instead of trusting the
handback's summary**. The moment either defers to the other, the double-check silently becomes a
single point of failure that *looks* like two - which is worse than one honest check, because
everyone stops looking.

⚠ Which is why the handback's **"what I could not find, and where the spec did not match the code"**
section is mandatory, and why every claim in this repo is judged by **marker on a fresh log, never an
exit code**. Three runners on 2026-08-24 reported a verdict unrelated to reality inside ten minutes -
two exited **0 having done nothing**, one said **`NO LOG`** while the gate had **passed**.

---

# ⭐ BATCH 1 IS NOW UNBLOCKED - all four intake blockers are answered (2026-08-24)

Codex refused batch 1 at intake, correctly, on four points. **All four are now settled and written
into the tickets.** It can proceed:

- **WO-1069** - ⛔ **do NOT modify `ShortfallPackOffer`.** The resolver was right; the ticket pointed
  at the wrong layer. `hearth-spark` is not an impulse SKU and the guard rejecting it enforces a
  binding WO-947 ruling. **Owner ruling: `hearth-spark` moves $1.99 -> $4.99** in
  `api/_lib/purchase-catalog.js`. ⚠ It is `DEVNET_CANARY_SKU` - re-check the quote/verify test path
  with the new anchor. Add the regression: **no impulse rung strictly dominated at its own USD anchor.**
- **WO-1177** - ⭐ **stop trying to prove the shortfall; it does not need proving.** A forged `reason`
  yields exactly what a genuine one yields: **one discount per window.** So build **no**
  attestation rail. **Owner ruling: the window is SEVEN DAYS**, server-recorded.
- **WO-1178** - the raw-invocation hole **cannot be closed** and the ticket no longer asks for it.
  ⭐ Its most valuable half has nothing to do with editors: **the runner must assert the MARKER
  instead of returning a bare exit code.**
- **WO-1173** - still sequenced behind WO-1177 (shared `api/schema.sql`).

---

---

# ⭐ THE CADENCE — when the two standing agents run (owner directive, 2026-08-24)

> *"i told you two agents one clearing one feeding"* · *"not you, you are CLI — you delegate and own
> the gate"* · *"agents do the work"* · *"you verify and orchestrate"*

⛔ **THE LEAD DOES NOT DO THIS WORK BY HAND.** On 2026-08-24 the lead wrote both roles into this
document and then ran neither, doing status lines and batch composition manually instead. ⚠ **The
cost was measurable: `BOARD.html` was STALE at HEAD** despite being regenerated inside **34 commits**
that day. Doing it by hand meant doing it inconsistently — which is the whole reason the roles exist.

⭐ **This is the same failure the repo keeps producing in other forms: a mechanism built and never
called.** Seven were found in one day (`WalletService.Disconnect`, `SetWalletService` ×2, `founderOnly`,
the repair HUD seam, `MonthlyCardService.ActivateCard`, the schema-parity gate). These two agents were
number eight, and the lead built them.

## The lead's lane — all four, nothing else

**VERIFY** at source · **GATE** (marker on a fresh log, never an exit code) · **COMMIT** by explicit
path (sole committer) · **HOLD THE THROUGH-LINE** with the owner.

⛔ Not: writing tickets, editing status lines, composing batches, or doing the deep read. **Those are
agent work.** If the lead is typing a ticket body, an agent should be.

## When each agent fires

| Agent | Fires |
|---|---|
| **CLEARING** (status reflection) | ⭐ **After every gate-green commit wave** — the moment work lands is the moment its Status line is wrong. Also at session start, and before any board read the owner will act on. |
| **FEEDING** (batch selection) | ⭐ **The moment a batch is handed out** — not when it returns. The next batch must be ready *before* the lane goes idle, and the disjointness check must run **BEFORE** a batch ships, never after. ⚠ On 2026-08-24 it ran after, and found a collision **inside a batch already handed to Codex**. |

⚠ **Neither is one-shot.** Agents do not persist, so "standing" means **the lead re-spawns them at
each cadence point.** A cadence that depends on the lead remembering is a cadence that lapses —
which is exactly what happened. **If in doubt, re-spawn: a redundant sweep is cheap, a stale board is
not.**

## The two briefs that must not be softened

- ⛔ **CLEARING sweeps the WHOLE BUCKET, never a date slice.** A date-scoped sweep found 6 drifts; a
  whole-bucket review found **7 more**, two wrong since the previous day. ⚠ **A date-scoped sweep does
  not merely miss old rows — it implicitly CERTIFIES them.**
- ⛔ **FEEDING proves disjointness by listing FILE PATHS.** Never infer it from a silo name. That check
  caught WO-1173/WO-1177 sharing `api/schema.sql`, and WO-1069/WO-1177 sharing
  `api/_lib/purchase-catalog.js` **inside a live batch**.

## The one thing the lead must never delegate

⛔ **The gate.** One Unity lock, one committer. ⚠ And judge by **MARKER on a FRESH log** — on
2026-08-24 three runners returned a verdict unrelated to reality inside ten minutes: two exited **0
having done nothing**, one reported **`NO LOG`** while the gate had **passed**. Trusting the third
would have discarded correct work.

---

# BATCH 4 - REBUILT after the owner's ten rulings (2026-08-24)

⚠ **The one-ticket batch 4 below the fold was composed BEFORE the rulings landed and is superseded.**
Eight tickets moved SPEC -> READY in one sitting; the board went **Ready 25 -> 33**. That is the
bottleneck clearing exactly as predicted.

| WO | What | Silo | Files |
|---|---|---|---|
| **WO-1161** | Tutorial names the wrong buildings - fix **TWO IDS ONLY**: trigger `workshop`->`forge`, nudge `forge`->`armorer` | Onboarding/data | `tutorial-steps.json` (both copies) |
| **WO-1163** | Stone replaces food: `collector_farm`->**Quarry**, `silo`->**Stoneyard**, balances convert **1:1** | Economy/catalog | 431-line spec, 15 files named |
| **WO-917 §2 Phase B** | Unequipped ability slots render a **blank plate** - add the `+` affordance | HUD/UI | `HudModelProducers.cs`, `HudKitController.cs`, the action-slot builder |

⛔ **WO-1161: the TWO IDS AND NOTHING ELSE.** That file carries the owner's creative pin on the rest
of the sequence. ⚠ And `EchoCardVM.FaucetBuildingIdFor` still routing iron to `collector_forge` is a
**separate change needing a captured run** - repointing the cue without moving the faucet binding
swaps one lie for another.

⛔ **WO-1163: IDS STAY FROZEN.** `collector_farm` and `silo` are **live save keys**; only
`displayName` changes. ⚠ Spelling is **"Stoneyard"** (the 08-24 ruling supersedes §4b's "Stone Yard").

⛔ **WO-917: PHASE B ONLY** - Phase A is an owner art pick. ⚠ `ElarionUiKit.cs` is shared with WO-1182
in batch 3: **917 confines edits to the action-slot builder; 1182 adds no new kit primitives.**

## ⭐ WO-1179 IS NOW IN BATCH 4 - ruled and specced 2026-08-24

| WO | What | Silo | Files |
|---|---|---|---|
| **WO-1179** | Roaming troops: escalating **side count 1 -> 2 -> 4**, away time **banks pressure** | Combat/AI | `Village/Waves/SmartEnemySpawner.cs`, `WaveManager.cs`, `WaveCompositionBuilder.cs` |

**Bounded by the owner to: one encounter, one global cap, sides 1 -> 2 -> 4, away time banks pressure
rather than simulating an unseen loss.**

⛔ **SIX BINDING CONSTRAINTS - in the ticket's ruling section. Read them before touching the spawner.**
The two that will otherwise cost a day:
- ⭐ **ONE `SpawnWave` CALL.** Partition one wave's composition across active sides under **one shared
  concurrency budget**. ⚠ Calling it per-side hands **each** call the full budget and doubles the
  field, defeating a cap that exists because of a **phone frame-rate cliff**.
- ⛔ **`Gate.ForceFieldCollapsed` is NOT the breach signal** - it also fires whenever the hero walks
  out of town. And ⛔ **do not touch WO-1026's ring detector**: behind a flag OFF since WO-579, it
  records **nothing, forever, silently**.

⚠ **`SmartEnemySpawner.cs` and `WaveManager.cs` are disjoint from every other in-flight ticket** -
but WO-513 (batch 2) is the other Combat/AI lane. It owns `Village/Arena/BattleArena.cs`. **No overlap;
keep it that way.**

⚠ **Instrument BEFORE tuning:** `[Flow:Wave] wave N: concurrency cap ... HOLDING M`. ⭐ **If the cap
already binds, two sides means HALF A WAVE EACH** - the escalation would read as weaker, not harder.

## ⚠ (Historical) NOT in batch 4: WO-1179, and the gap was MINE

Roaming troops is **ruled and still not handable** - 122 lines naming exactly **one** concrete file.
I wrote it as a design spec, not an implementation one, so it fails test 1. **The lead owes it a spec
pass** naming the `WaveManager` seam, the `SpawnPoint` resolution, and where the two-gate escalation
step lives. ⛔ Handing it out as-is returns work I would have to re-derive.

---

# BATCH 5 - the monetization cluster. ONE LANE, ONE SEAT, SEQUENTIAL.

All four are **READY** on the owner's rulings. ⛔ **They are NOT four parallel tickets.**

| Order | WO | Ruling that unblocked it |
|---|---|---|
| 1 | **WO-1164** | Store HUD entry = a **tab in Bag**; town building stays. ⛔ Two doors, **ONE** `PackStore` - never forked |
| 2 | **WO-1071** | Storehouse Deeds = **percentage multiplier** (+25% / +25-50% / more), plus cosmetic evolution of buildings already placed, ⛔ **no new container object** |
| 3 | **WO-1070** | Founder's Vow grants **crystals TOWARD** the gated slot, never the slot; "Named on the Heart" **removed** |
| 4 | **WO-1073** | Patronage **architecture** now, thresholds **tentative** at $50/$150/$500; ⛔ nothing above $500 until real $500 patrons exist |

⛔ **WHY ONE SEAT: they share `packs.json`, `api/_lib/purchase-catalog.js` and the `PackStore`
surface** - and **batch 1 (WO-1069 + WO-1177) is IN `purchase-catalog.js` right now.**
⚠ **Batch 5 must not start until batch 1 returns.** Two seats in the anchor table, one of them
repricing `hearth-spark`, is the collision that was already caught once today.

⭐ **All four cite `FOUNDATIONAL_RULINGS.md`** - §1 progression gates cannot be purchased, §2 paid
permanence should be visible, and the Heart is not a sponsor surface. ⛔ **Do not restate those rules
in code comments or ticket edits; cite the file.** A fact written twice is this repo's dominant
failure mode, and one fresh instance was created today by the very process built to prevent it.

---

# BATCH 4 (SUPERSEDED - composed before the rulings) - thin on purpose

⚠ **One clean ticket. It was not padded, and that is the finding.** The selector swept every
`WorkOrders/*.md` by status line: everything else fails a handability test.

| WO | What | Silo | Files |
|---|---|---|---|
| **WO-917 §2 Phase B ONLY** | An unequipped ability slot renders a **blank plate** - add a "+ / add skill" affordance | HUD/UI | `Village/HUD/HudModelProducers.cs`, `HUD/Kit/HudKitController.cs`, the action-slot builder in `Core/UI/ElarionUiKit*.cs` |

Verified live at source: `HudModelProducers.cs:595` gives an unequipped slot a **null** icon, and no
`+` affordance exists anywhere in the kit.

⛔ **PHASE B ONLY. Phase A (the dodge glyph) is an OWNER ART PICK** and is not ruled. ⭐ Phase B needs
no colour decision at all - the spec already names "faint gold `+` on a dimmed frame" plus a tap hint,
which matters because **the owner is red/green colourblind and visual picks are always delegated.**

⚠ **`ElarionUiKit.cs` is a SHARED kit and WO-1182 (batch 3) also uses it.** Tell both seats: **917
confines edits to the action-slot builder; 1182 adds NO new kit primitives** (its own spec already
says it uses the same kit as every other surface).

## ⏸ Held - WO-1026 (PvE siege + Defense Report), and it is the best-specced ticket in the repo

618 lines: every file named with its assembly, three regression oracles, headlessly-checkable
acceptance, and an honest list of what it could not verify. ⭐ Its unruled stakes are **designed
around** - §8 pins the loss consequence to one six-line method returning an all-zero `StakesLedger`,
so it ships without a ruling.

⛔ **But it collides with WO-827, already out in batch 2.** Both write `Core/State/GameState.cs`,
`GameStateService.cs`, `SaveSchema.cs` (1026 bumps `CurrentVersion` 38 -> 39) and `SaveMigrator.cs`.
⚠ **Two seats in the save seam with one of them bumping the schema version** is the WO-1177/WO-1173
shape again. **It goes out the moment 827 returns.**

## ⛔ Do NOT hand these out - their status is WRONG, they are not work

- **WO-1157** - its remaining slice is **already in the tree and committed**. Corrected to FIXED.
- **WO-911-B** - its banner says *"there is no `TryAdSkip` in `BuildTimerService`"*. At HEAD there is a
  channel-aware pair (`BuildTimerService.cs:880`, `ObsidianQueueHud.cs:299/:408`,
  `ManageScreenVM.cs:452/:1118`) plus `AdGateService` and a covenant regression over it. **16 days
  stale.**
- **WO-501** - all four owner points shipped per its own banner; every `file:line` in its §1 is
  invalidated (a 583-line survey against a 1727-line file).

⚠ These go to the **lead/RCA lane**, never a dev lane - a seat handed one would rebuild working code.

## ⭐ THE REAL CONSTRAINT, stated plainly

**It is not tickets. It is owner answers and lead-run captures.**
**Eighteen** tickets are one owner sitting from ready. **Four more** are one lead measurement from
ready (WO-914 Phase A, WO-925, WO-926, WO-917 Phase A).

That is roughly **five batches of dev work** sitting behind about **two hours of the owner's time and
three headless runs.**

---

# BATCH 3

| WO | What | Silo | Files |
|---|---|---|---|
| **WO-1180** | Board parser accepts a malformed `**Status: ...**` and silently rescues rows via substring fallback | Tooling (Python) | `tools/board_build.py` |
| **WO-1181** | Lint rejecting a `FIXED/DONE/CLOSED` status whose own text asserts work remaining - **7 rows today** | Tooling (Python) | `tools/board_build.py` |
| **WO-876** | Troop combat VFX: melee on-hit impact, take-hit, death burst, a real Archer projectile instead of instant damage | Combat/AI VFX | `Assets/_Modules/Village/Troops/TroopController.cs` |
| **WO-1182** | The **last UXML player-facing surface** - rebuild the dungeon crafting modal as code-built Obsidian-kit uGUI | Dungeons/UI | `Dungeons/UI/CraftingPanelController.cs`; retire `CraftingPanel.uxml/.uss` |

⛔ **WO-1180 + WO-1181 are ONE lane, ONE seat, sequential, 1180 first.** They read as independent
tickets and **rewrite the same file**; 1181's spec explicitly builds on 1180's fallback path.

⚠ **WO-876's cited line numbers are STALE** - the file is 799 lines and was restructured by WO-935's
`CombatCast`. Tell Codex the citations are historical and to **report the mismatch** rather than
hunt for them. Every hook it reuses does exist verbatim (`VfxPool.SpawnHitImpact`,
`VFXManager.Play(Impact_*)`, `ProjectileVFXCatalog.SpawnFlying`), so it is a **mapping job with no
creative VFX pick** - which matters, because VFX key selection is the owner's.

⛔ **HAND OUT `WO-1182`, NOT "WO-1005 tail" - they are THE SAME FILE and two rows would race it.**
WO-1182 was split out of WO-1005 on 2026-08-24 and **already carries** the two things this section told
the lead to add by hand. WO-1005's own status now records the split. *(Caught by the batch-4 selector;
I created the duplicate by minting 1182 without updating this table.)*

⚠ **(Historical - already satisfied by WO-1182.)** WO-1005's remaining slice was one sentence, not a spec section. The
pattern is commit `7c103775a` (the lantern-oil rebuild - self-builds its own Canvas at runtime and
tolerates the legacy `UIDocument` seat), and the constraint is **keep the `DungeonCraftVM` MVVM seam;
the View reads no game state**. Without that it fails test 1. ⛔ **No scene or prefab work.**

## ⛔ COLLISION FOUND INSIDE BATCH 1 - already handed out

**WO-1069 and WO-1177 both edit `api/_lib/purchase-catalog.js`**, and both disturb
`test/purchases.quote.test.js`. 1069 changes `USD_ANCHORS['hearth-spark']` (`:84`) 1.99 -> 4.99;
1177 rewrites the `USD_ANCHORS` block (`:83-114`) and adds the eligibility check at `:414`.

⭐ **Safe IF and ONLY IF one seat takes both, sequentially.** Split across two seats, the second
merges onto a moved anchor table and a moved test. ⚠ I handed these out together without catching
it - **confirm the seat before the handbacks land.**

## Other collisions worth knowing

- **WO-827 is in batch 2 (implementation) AND the RCA lane** (*"is realm travel wired, or is the shell
  still a stub?"*). Read-only, so no file conflict - but ⚠ **a lane may be building what the RCA lane
  is about to declare already shipped.** Run the RCA first.
- **Latent:** WO-914 edits `hud-areas.json`; WO-1171 §4 must give connect/disconnect a home on a HUD
  surface. If §4's answer is a new widget row, that is the same file. ⚠ **§4 is also the thinnest
  spec in batch 2** - it names no screen, only *"a real settings screen"*.

---

# BATCH 1 - handed out 2026-08-24

| WO | What | Silo | Notes |
|---|---|---|---|
| **WO-1177** | The shortfall discount, server-issued | Backend/monetization | Spec names the exact file, function and line for every change. ⛔ Greenfield - `grep -rni discount api/` returns **zero**. |
| **WO-1069** | Shortfall resolver must never serve a dominated pack | Monetization/data | ⚠ **PROD-014 is sequenced behind this**, so it unblocks player-facing work. |
| **WO-1178** | A raw `Unity.exe` call bypasses the editor pin | Tooling/gates | Small. Cost a full Bee rebuild today and produced a gate that **exited 0 with no marker**. |

⛔ **NOT ready to hand out:** the rest of **PROD-014** (refusal card, crystals option, pack offer) -
the lead is editing those exact files right now, and item 3 carries a live balance constraint.

## What the lead does with the handback

Reviews it **against the whole tree, not its stated scope** - a scoped seat cannot see blast radius.
Then gates (`COMPILE_GATE_OK` / `REGRESSION_OK` on a **FRESH** log, judged by **MARKER, never exit
code**) and commits by explicit path.

⚠ **Why "never exit code" is written this hard:** on 2026-08-24 three separate runners returned a
verdict unrelated to reality within ten minutes - two exited **0 having done nothing**, and one
reported **`NO LOG`** while the gate had in fact **passed**. Had the lead trusted the third, correct
work would have been thrown away.

---

# BATCH 2 - ready to hand out

| WO | What | Silo | Why it is safe to run in parallel |
|---|---|---|---|
| **WO-513** | Coordinated family combat - the orc family GANGS the hero instead of three identical solo rushes | Combat/AI | `Assets/_Modules/Village/Arena/BattleArena.cs`. Touches nothing any other lane touches. Fully specced: diagnosed state, approach, slices, guardrails, acceptance. |
| **WO-1170** | JSON is the only source - retire the remaining in-code fallbacks | Data/codegen | `Tower.cs` + `TowerPerkFallbackGenerator.cs`. Site 1 already landed, so the pattern to follow is in the tree. |
| **WO-827** | Realm map discovery + travel | World/progression | The WO-826 gate is lifted and the shell shipped. 141 lines of spec. |
| **WO-1171 §4** | Wallet disconnect - the PLAYER-FACING placement | HUD | The dev-panel surface is DONE; only §4 remains. |

## ⛔ Deliberately NOT in batch 2, and why

- **WO-1173 (schema-parity gate)** - ⚠ **it collides with WO-1177, which is already out in batch 1.**
  Both edit **`api/schema.sql`**: 1177 adds the discount columns, 1173 is the gate that reads the
  schema. Running them concurrently means two seats editing the same file, and the gate would be
  written against a schema that is changing underneath it. **Sequence it: 1173 goes out the moment
  1177 comes back**, and then it validates 1177's own new columns - which is the right order anyway.
- **WO-822 (barracks teach v2)** - ⚠ its own status line says `barracks_intro`,
  `BarracksNpcInjector` and `BarracksBlankTownRegression` **already exist at HEAD** and *"this ticket
  may be partly or wholly shipped; a CLI acceptance pass is owed before anyone re-implements it."*
  ⛔ That is a **verification** task for the lead, not implementation for a dev lane. Handing it out
  risks re-building something that already works.
- **WO-1179 (roaming troops)** - newly written, **SPEC not READY**. It carries three open design
  questions the owner has to answer, and one of them decides whether the 48-hour shield product has
  anything to protect.
- **The rest of PROD-014** - the lead is in those files.

⚠ **On WO-513 specifically:** it is *not* the roaming-troops feature the owner asked for on 08-24.
513 is how a pack fights **once it has arrived**; roaming troops is **what arrives, from where, and
against how many gates**. They compose - build 513 first and WO-1179 inherits it - but a lane handed
513 expecting roaming troops will deliver the wrong feature, correctly.

---

# BATCH 2-RCA - diagnosis only, no code

⭐ **This lane is READ-ONLY, so it does not contend for the Unity gate and every item runs in
parallel with every other item AND with the implementation batches.** Nothing here needs a branch,
because nothing here writes.

## The rule for this lane

⛔ **Produce a VERDICT WITH CITED EVIDENCE, not a fix.** Every claim carries `file.cs:line` or a
commit sha. ⚠ **"Not found" is a first-class result** - say it plainly rather than inferring that
something exists because a ticket says it should. A ticket asserting a class that has never been in
the repo has cost this project a morning before (`RaidHeroSpawner`, WO-1109).

⛔ **Static code-reading LOCATES candidates; it never CONCLUDES a runtime cause** (CLAUDE.md §12).
For anything behavioural, say what capture would settle it - do not guess and do not "fix".

## Group A - the board is lying and only reading the tree settles it

These tickets' OWN status lines admit uncertainty. Each verdict is one of:
**ALREADY SHIPPED** (cite the commit + the code) / **PARTIALLY SHIPPED** (name exactly what remains) /
**NOT SHIPPED** (name what is missing). ⚠ The point is to stop a dev lane **re-implementing something
that already works** - which is a real risk right now, not a hypothetical.

| WO | The question | Where to look |
|---|---|---|
| **WO-822** | Its own status says `barracks_intro`, `BarracksNpcInjector`, `BarracksBlankTownRegression` **exist at HEAD** and *"may be partly or wholly shipped."* Which of the ticket's scope items are live? | Those three symbols; the ticket's §Scope, item by item |
| **WO-827** | Status says `RegionProgress` **exists in GameState/Save**. Is realm travel wired, or is the shell still a stub? | `RegionProgress`, the WO-826 shell (`eb5d0710`) |
| **WO-1010** | Claims *"all code is in HEAD; remaining = external-tester re-test only."* Verify that literally. | The carousel/minimize path |
| **WO-1001 / WO-1004 / WO-1008** | Three dungeon tickets marked PARTIAL on different dates. What is genuinely left across all three, deduplicated? | Dungeon compose pipeline; `COMPOSE_ALL_OK` history |
| **WO-557** | Phase 2 "faithful subset migrated + enabler-gaps specced." What Yarn remains at HEAD? | `grep` for the Yarn package + call sites |
| **WO-932** | Phases 1-4 landed 2026-08-08. What do phases 5+ still require? | The RESULT file beside it |

## Group B - real defects that need a cause before anyone writes a line

| WO | The question |
|---|---|
| **WO-884** | ⛔ **It carries TWO CONTRADICTING Status lines.** And `VfxFacade` / `VfxSocket` / `VfxEmitter` / `ParticlePackVfxBuilder` appear **absent**, with `Vfx.On(` returning **0 hits**. Settle it: does any of that exist at HEAD? If not, the ticket describes a system that was never built, and the two status lines are both wrong. |
| **PROD-013 / PROD-014 tail** | The mechanism is already found and written up: `WallRepairController.HandleTap:398` discards a world tap **before any raycast** whenever a selection is open, and `ClearSelection` is reachable only via the HUD prompt's Cancel. ⚠ **Do NOT change it** - making a world tap clear a selection alters a deliberately modal interaction documented at `:391-397`. The RCA question is narrower: **is there any OTHER path that leaves a structure selected with no visible prompt?** |

## What a good handback looks like

One short verdict per ticket, each with its evidence, plus - explicitly - **anything you looked for
and could not find.** ⭐ That last list is the most valuable part: two handbacks today corrected the
ticket that commissioned them, and both changed what got built.
