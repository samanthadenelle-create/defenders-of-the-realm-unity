# HANDOVER TO GROK - 2026-08-27

**From:** the CLI lead seat (Claude Code, sole committer)
**To:** Grok, in its standing role as **work-order author**
**Repo state at handover:** branch `wip/village2-and-f8-tickets`, HEAD `dea0c064e`, **tree clean,
everything pushed**. 59 commits today, all gated.

---

## 0. YOUR ROLE, AND ITS ONE HARD BOUNDARY

The three-seat flow is: **Grok drafts / suggests work orders → the UI seat refines → the CLI seat
implements, gates and commits.**

> ## ⛔ YOU AUTHOR TICKETS. YOU DO NOT WRITE, EDIT, GATE OR COMMIT CODE.
> There is exactly **one committer** (the CLI seat). Two committers duel over `.git/index.lock` and
> produce stale locks and false "pushed". If you believe code must change, say so **in a ticket**.

**Your drafts are refined, not accepted verbatim.** That is not a slight - it is the pipeline
working. Add a provenance line to anything you author so the next seat knows where it came from.

---

## 1. READ THESE BEFORE YOU WRITE A SINGLE TICKET

Non-negotiable, in order:

1. **`CLAUDE.md`** - the law. Every rule below is a compression of it, and where they disagree,
   `CLAUDE.md` wins.
2. **`docs/CLI_OPERATIONS_RUNBOOK.md`** - ⭐ NEW TODAY. How the machine is actually run: startup, the
   seat model, the board, every gate command and its marker, builds, R2, Firebase, Vercel, the
   database, F8 triage, commit discipline.
3. **`docs/ACCESS_AND_SECRETS.md`** - ⭐ NEW TODAY. **Read this before you ever say you cannot reach
   prod.** Most of what seats think is blocked is not secret: the live API base is
   `https://defenders-of-the-realm-v2.vercel.app`, compiled as a `const` into the public APK.
4. **`docs/MASTER_CATALOG.md`** + the `docs/MASTER_CATALOG/<area>.md` for whatever you are writing
   about. Built from CODE, because **comments lie**.
5. The newest **`CANON_GROUND_TRUTH_<date>.md`** at repo root.

**The board:** `python tools/board_build.py` regenerates `BOARD.html` from `WorkOrders/*.md`. The
repo is the source of truth; the board is a derived view and cannot drift. Linear, Notion and the
Task list are **all retired** - do not mirror to any of them.

**Numbering:** the `CLI_LANES_WO_NUMBERS.md` banner is the **sole** authority. **Next free = 1254**
(CLI line); the UI seat has its own disjoint block. ⛔ **Bump the banner in the SAME edit as a mint** -
a number written to disk without a banner bump *is* the collision, and that has happened five times
in one day before.

---

## 2. WHAT IS ALREADY TICKETED - DO NOT DUPLICATE ANY OF THESE

7 READY, every one minted today from the owner's live device felt-test, each carrying its evidence:

| WO | Subject | Note for you |
|---|---|---|
| **1246** | Store SKU visibility | Four DISTINCT causes; two are owner decisions, not engineering |
| **1248** | Hero select `Pr...` truncation | The **third** truncation defect in 7 days - asks whether the fix belongs one level up |
| **1249** | Boot still lands on validate wallet | ⛔ Owner ruled: **no tester bypass** (see §4) |
| **1250** | Weaponsmith + Armorer pre-built on new load | Likely the baked-standdown, not the buildings |
| **1251** | Crystal Mine colourless | **Cause already captured**: NULL material slot, F8 seq 3618/3619 |
| **1252** | "All builders busy" gives no next step | Amended mid-flight when 1253 superseded part of it |
| **1253** | Permanent builder = store SKU | Supersedes WO-911 Q6 as the Manage affordance |

**194 FIXED.** Before drafting anything, search `WorkOrders/` - this repo has ~1200 work orders and
duplicate coverage is a real and recurring cost.

---

## 3. HOW TO WRITE A TICKET THAT SURVIVES THIS REPO

Copy the shape of the ones minted today (1246-1253). What makes them handable:

- **State the evidence, not a theory.** If a device capture already names the cause, quote it verbatim
  and tell the implementer to go straight there. WO-1251 does this - the F8 line names a NULL material
  slot, so the ticket forbids re-diagnosing it.
- **Separate owner decisions from engineering.** If a fix needs a creative or pricing call, say so and
  give a **recommendation with the consequence**, never a guess. WO-1246 splits four causes into two
  of each.
- **Name what NOT to touch.** Every ticket here ends with that section. It is what stops a scoped fix
  becoming a refactor.
- **Require the proof.** `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, plus a
  **screenshot for anything visual**, plus **RED-first** proof for any new regression.
- ⛔ **Never restate a number that lives in code.** Say "read it off the marker / off the config".
  Every copied number goes stale - that is the single most expensive recurring failure in this repo.

### The rules a ticket must respect or it will bounce

- **Markers, never exit codes.** The runners exit 0 on refusals and FAILs. Marker absence on a fresh
  log is a FAILURE, not an unknown.
- **RED-first (WO-1138).** A test that has never failed proves nothing. Also prove the SUCCESS path -
  failure-only acceptance shipped a guard here that aborted every good run while exiting 0.
- **No hollow passes.** A guard returning early on a missing dependency without asserting or emitting
  `RegressionOutcome.Skip`/`PartialSkip` is a **P1 defect**.
- **Instrument, do not guess (§12).** Static reading LOCATES; it never CONCLUDES.
- **ASCII-only in player-facing strings.** A tofu oracle fails the regression on characters the UI
  font cannot render - CJK brackets cost a gate run today.
- **The owner is red/green colourblind.** State goes in WORDS. Never ask her to pick hues.
- **Money is REAL** (mainnet + SKR live). Never delete or renumber a SKU id that has ever been sold.
- **⛔ R2 (§16):** enemy and structure art is served from a CDN with **no local fallback**, and bundle
  names are **content-hashed**, so **every content build needs its own push**. A missing push renders
  as placeholder art with **no error on screen**.

---

## 4. OWNER RULINGS MADE TODAY - these are binding and some overturn prior canon

1. **⛔ THE TESTER BUILD MUST BEHAVE LIKE PRODUCTION.** *"i need it to act as it would in prod as its
   the only way to really test it."* `TESTER_BUILD` is for **tooling she deliberately invokes** (the
   AdminOverlay, the resource grant, the F8 chip). It is **NOT** a licence to change what the game
   does on its own. This overturned a recommendation to skip wallet validation for testers. **Never
   propose a tester-only behaviour bypass.**
2. **The `store` kill switch fails CLOSED; the other five fail OPEN.** A wrongly-open store can charge
   real people during the incident it was sealed for - irreversible. A wrongly-closed store defers a
   sale. ⛔ Do not "unify" these into one rule; the inconsistency is deliberate.
3. **A permanent builder is a store SKU** (WO-1253), superseding WO-911 Q6's crystal sink as the
   Manage-screen affordance. ⚠ **Open ruling:** does the crystal queue-slot sink survive alongside it?
4. **WO-1175 CLOSED AS MOOT.** Its premise cannot be true - `purchase_quotes` CHECKs currency to
   `'SKR'` alone, so there is nothing to "choose". ⛔ It carries **two false claims** that are
   labelled in place; do not rebuild on them. Survivors were re-minted as 1246 and 1247.
5. **Board validation sections start collapsed**, expanding as she is ready to test.

---

## 5. OPEN QUESTIONS THE OWNER STILL OWES

- **WO-1253:** does the crystal queue-slot sink survive alongside the new SKU? (Recommendation: keep
  both - crystals buy DEPTH, money buys a BUILDER.)
- **WO-1247:** who is thanked by the patron covenant, and with what? ⭐ Check first whether shipped
  WO-1073 already IS the answer.
- **WO-1246:** the clone-merge and dominated-pricing calls.

## 6. TWO OWNER ACTIONS BLOCKING WORK

1. **`ADMIN_OPS_KEY` is not set on Vercel.** The command center is read-only until it is, and it must
   be a **different value** from `ADMIN_DASH_KEY`.
2. **`vercel deploy --prod` has not been run.** `vercel.json` sets `git.deploymentEnabled: false`, so
   **pushing does not deploy**. `/api/maintenance` currently **404s in production** - which is what
   sealed the store on her device this morning.

---

## 7. WHERE THINGS ACTUALLY STAND

- **Live tester APK on the Seeker:** built 2026-08-27 12:04, `TESTER_BUILD`, bundle
  `2026.08.27.343739`. `R2_PUSH_OK` + `R2_PARITY_OK 45 object(s)`, 0 compile errors.
- **Gates green at HEAD:** `COMPILE_GATE_OK`, `REGRESSION_OK 309/309 suites`, `UI_CAPTURE_OK`,
  `SCHEMA_PARITY_OK 19 tables`, JS suite 136/136.
- **Proof page:** `PROOF.html`, 10 items with images.
- **`FeatureFlags.FoundersMonument` is OFF** until the owner's custom monument FBX exists. Flip it
  back the moment that Addressables address resolves; nothing else changes.

---

## 8. THE ONE LESSON WORTH CARRYING

Today, WO-1243 passed `COMPILE_GATE_OK`, `REGRESSION_OK 308/308` and its own dedicated regression -
and the **first screenshot** showed the operator's message truncated to `- Rai...`, plus a second
producer that every test was aimed at and no player ever reads.

**FlowTrace shows what the code believes. The screenshot shows what the player gets.** When you author
a ticket for anything visual, make the screenshot an acceptance criterion, not a nicety.

And its mirror, learned the same day: **verify your tooling before blaming the code.** A "responsive
bug" at 390px was an artifact of headless Edge's ~500px minimum window - the tell was an identical
element offset at 360 and 390. Filing it would have cost someone a morning chasing correct CSS.
