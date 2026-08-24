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

# BATCH 3 - board tooling (pure `tools/`, zero Unity contention)

| WO | What | Why now |
|---|---|---|
| **WO-1180** | The board parser accepts a malformed `**Status:**` and hides the rows its fallback rescues | ⭐ The valuable half is **counting and listing fallback-bucketed rows** - WO-932 was one edit from vanishing because it lived there |
| **WO-1181** | A status can lead with `FIXED` and say "not done" four words later - **seven rows were green today while admitting they were unfinished** | ⛔ The lint must distinguish **work remaining** from **verification remaining**, or it flags the entire healthy Fixed bucket and gets switched off in a day |

⭐ **Both are `tools/board_build.py` only** - no Unity, no gate, no lock, and they can run alongside
everything else. Both carry **induce-the-failure-and-watch-it-fire** acceptance.

⚠ **They touch the SAME FILE**, so they are ONE lane, not two - same seat, sequential, 1180 first.

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
