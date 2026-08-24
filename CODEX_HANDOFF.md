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

## Ready RIGHT NOW

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
