# BATCH_STATE — handover to Codex, 2026-08-26

**Lead:** Claude Code (CLI seat, sole committer). **Courier:** the owner. Append-only by section.

---

## PART 1 — WHERE THE PROJECT ACTUALLY IS

**The game is LIVE on the Solana dApp Store.** This is not pre-release work. A regression that
reaches the store reaches real players.

**Today the owner ran a long felt-test on a Seeker device** (build `2026.08.26.342290`). It produced
~25 new tickets and 11 approved UI specs. The board reads **58 READY** — that number is misleading,
see PART 2.

### Eight finished lanes are in the tree, gated or not — check `git status` first

If the lead gated before handing over, these are committed and the tree is clean. If not, they are
uncommitted and **you must not touch any file they contain** (PART 3.4 lists them).

| Lane | What was actually wrong |
|---|---|
| Staff pose (WO-1226) | The drawn staff lay across the body. Six prior fixes failed because **a regression PINNED the wrong value**. Owner ruled it stands vertical; the pin moved WITH the ruling. |
| Battle-lock (WO-1233) | **P0.** Winning an arena left the town unresponsive 8 times in 9. Root: `PursuitBattleProbe` held the lock; `PostureSignals.ClearPursuits` had ONE caller, on scene load — and the arena stages in-place with no scene load. |
| Raid payout | Troops killing defenders banked per-kill materials mid-raid. Owner ruled raids pay ONCE at the end. Gold and XP still pay per kill. |
| Fail-closed gating (WO-1223) | **FOUR OF FIVE** failure modes resolved to OPEN. `ParseState` literally ended `default: return Open`. The gate was no gate in every degraded condition. |
| Enemy level (WO-1232) | Two call sites still ran a retired `maxHp/25` heuristic; one drove the danger skull, so every enemy read LETHAL. |
| VFX pool (WO-1229) | **Not a leak.** 44 candle anchors against a global 24-slot pool with no bound. Also found: the dungeon 48-tier had **never engaged in a shipped build**. |
| Hollow passes | Four regression guards returned without asserting and landed in the GREEN column. |
| Hero select (WO-1083/1234) | The portrait resource path was written out in **eleven literals across seven files**. Now 2, both inside one constant. |

**Also live as of today: the F8 DEVICE BRIDGE (WO-1227).** Until this morning device captures never
reached any seat — 736 entries had accumulated unread since 2026-07-20. The chain is whole and
delivered its first two captures within the hour.

---

## PART 2 — THE GOAL

**The goal is a PROD BUILD, and it does NOT require closing 58 tickets.**

Ship gates are FOUR MARKERS (CLAUDE.md §8 + §16), not an empty board:

```
COMPILE_GATE_OK  +  REGRESSION_OK <n>/<n>  +  UI_CAPTURE_OK  +  R2_PARITY_OK
```

**What actually blocks a prod push:**

1. **WO-1233** battle-lock softlock — FIXED, awaiting gate
2. **WO-1223** fail-closed gating — FIXED, awaiting gate
3. **The R2 content push** — `tools\r2-ship.ps1`. Bundle names are **content-hashed**, so every
   content build needs **its own** push. A previous push can never cover this one. This has already
   burned the project three times.

Everything else on the board is polish, presentation, or new feature. Shipping with an untidy
Treasure panel is legal. Shipping with a town the player cannot interact with is not.

**Your job in this window is NOT to burn down the board.** It is to advance work genuinely disjoint
from the gate, so that when the lead returns the gate runs once and cleanly.

---

## PART 3 — THE PROCESS

### 3.1 You are in a linked worktree
Your `git diff` will report the lead's committed work as if it were uncommitted or duplicated.
**It is not.** Hash before believing duplication. Never merge your worktree as a branch.

### 3.2 NO git commit, add, or push. Ever.
There is exactly ONE committer and it is not you. Two committers duel on `.git/index.lock` and
produce stale locks plus false "pushed" reports. Leave work in the tree; describe it in the handback.

### 3.3 NO Unity. You cannot gate.
Unity is single-instance and the lead owns the gate. Do **not** run `run-unity-method.ps1`,
`CompileGate`, or `DataRegression`. A collision can corrupt a gate log the lead depends on.

**Therefore prefer work verifiable WITHOUT Unity.** `api/` has node tests. PowerShell has
`[System.Management.Automation.PSParser]::Tokenize` for parse checking. Use them and **paste the
output** in your handback.

For any C# you write: brace-balance check (`{` vs `}`) plus a NUL scan on every file, counts
reported. That is this repo's minimum.

### 3.4 Do not touch files the lead has uncommitted
**Run `git status --short -- Assets/` FIRST.** Every file it lists is locked. If the tree is clean,
this section is moot and you have more room.

If an assignment appears to require a locked file: **STOP and say so in the handback.** Do not work
around it, do not copy the file, do not "just add one small thing".

### 3.5 Where you can safely work
- **`api/`** — the Vercel serverless backend. It is **in this repo**, not a separate project. Fully
  disjoint from the gameplay lanes, and node-testable.
- `.claude/skills/run-defenders/*.ps1` — committed as of `89977006a`.
- `Assets/_Modules/Core/Diagnostics/BreakCaptureHarness.cs`.
- `docs/`, `WorkOrders/`.
- Canonical JSON **except `waves.json`** (mid-change).

### 3.6 Engineering rules that are not negotiable
- **Instrument before you fix** (CLAUDE.md §12). Static reading LOCATES a candidate; it never
  CONCLUDES a cause. If you cannot cite captured data proving the cause, you have not earned the
  edit. Every wrong theory this project shipped came from skipping this.
- **No hollow passes.** A guard that returns without asserting lands in the GREEN column and is a P1
  defect here. Missing dependency → FAIL naming it, or return an explicit skip token. Never a silent
  bare return.
- **Prove RED first.** A regression that has never failed proves nothing.
- **Never raise a cap or threshold to make a symptom go away.** The VFX pool went 20 → 40 → 24 that
  way and the real cause sat unfound for months.
- **A failure-only oracle is not acceptance.** Assert the good path too — this repo shipped a guard
  that aborted every good run while exiting 0.
- The owner is **red/green colourblind**. Nothing may carry meaning by hue alone.
- **ASCII-only** TMP strings and PowerShell.

---

## PART 4 — ASSIGNMENTS, in priority order

### 1. WO-1237 — the softlock detector fires on AFK  *(safe, self-contained)*
`WorkOrders/WORK_ORDER_1237_softlock_detector_fires_on_afk.md` — read it in full.

The detector labels 180s of no movement as `possible_softlock`. Capture seq 3609 proves a false
positive: the screenshot shows full HP, a five-face bar, and a wave clock counting down normally. The
owner was idle, not stuck.

**Why it matters:** `possible_softlock` is one of four kinds that PAGE a seat, and the device backfill
holds 8 of them. Noise trains the seat to discount the kind — and the one real softlock then arrives
already discredited.

Build an IDLE-vs-STUCK discriminator. Candidates (instrument, do not assume): input presence,
`Application.isFocused`, world liveness (the wave clock was ticking).

- **Do NOT just raise the 180s threshold** — that trades a false positive for a slower true positive
  and leaves the classifier equally blind.
- **Do NOT silence the kind.** An idle capture is still RECORDED, just not paged.
- Re-run your classifier over `logs/f8-inbox/DEVICE_BACKFILL_2026-08-26.md` (736 entries) and
  **report how many of the 8 reclassify**. That number is the ticket's value.
- Any `.ps1` must be **pure ASCII**. A BOM-less UTF-8 `.ps1` is read as ANSI by PS 5.1, and CP1252
  turns smart-quote bytes into string delimiters — silently mis-parsing while every gate stays green.
  A regression FAILS on this and it caught the lead's own hook today.

### 2. `api/` hardening  *(safe, node-testable)*
- `api/schema.sql` uses `ON CONFLICT DO NOTHING` on its seeds. **That is exactly why two
  `dungeon_status` rows never reached production and the owner's dungeons were shut all day.** Audit
  every seed in that file for the same trap and report which others would silently fail to back-fill
  an already-provisioned database.
  **Do NOT change production data** — the lead already wrote the two rows and verified them by shape
  query (`DUNGEON_ROWS_OK 6/6 covered`).
- `test/dungeon-status.manifest.test.js` — confirm it still REDS when a portal-gated id has no seed
  row. Run it; paste the output.

### 3. WO-1121 — live money rails and buy gate  *(oldest READY — TRIAGE, do not build)*
Read it and **report whether it is actually actionable** before writing a line. It may be owner-gated
or need rulings. A truthful *"this is blocked on X, here is the evidence"* is a valid and valuable
handback — more valuable than code built on a wrong assumption.

---

## PART 5 — HOW TO HAND BACK

Write `batch_results_state.md`, append-only by section. For each assignment:

- files changed, with line numbers
- the **command output** proving your claim — not a description of it
- brace counts + NUL scan for any C#
- anything you could not do, and why

**Your handback is a CLAIM until the lead proves it.** That is not distrust; it is the protocol this
repo runs on. Every assertion will be re-verified against the tree.

---

## PART 6 — CONTEXT YOU WILL OTHERWISE GET WRONG

- **`Enemy.Level` IS the `maxHp/25` heuristic** (`Enemy.cs:623`). There is **no authored level field**
  on `EnemyDef`. Its own doc comment claims otherwise and is WRONG — it misled a ticket today.
  Comments lie; read the code.
- **Five action-bar faces in open town is CORRECT.** Talk is proximity-gated on
  `TalkPromptRegistry.Count > 0`. Do not "fix" it — doing so cost an RCA this morning.
- **Offline first-run = every dungeon SEALED.** Owner ruling, not a bug.
- **Passive Echo repair SPENDS wood and iron.** The owner ruled the spend stays.
- **The repo root is machine-dependent** (`C:\eoa` on one machine, `D:\eoa` here). Never hardcode it.
- **58 READY is not 58 ship blockers.** See PART 2.
