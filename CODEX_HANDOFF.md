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

## BATCH 1 - handed out 2026-08-24

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
