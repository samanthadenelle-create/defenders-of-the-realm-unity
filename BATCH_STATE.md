# BATCH_STATE — the live handoff. Read this FIRST, every time.

**Last written:** 2026-08-24 (later) by the CLI lead. ⭐ **NEW since you last saw it: WO-1177 is COMMITTED (`2c3ed6c24`) AND DEPLOYED, the migration RAN — so WO-1163 IS UNBLOCKED and has been waiting all night.** Batch 1 is fully closed; there are now **SEVEN file-disjoint seats free**. All of it is in the block directly under the ACTIVE table — it supersedes the older WO-1177 correction beneath it.

> ## ⛔ THE PROTOCOL
> 1. **Read this file at the START of every batch, and again before starting any NEW ticket inside one.**
> 2. ⭐ **THE TWO SEATS DO DIFFERENT OPERATIONS — that is the safety, not just different regions.**
>    - **Dev lane: APPENDS to existing sections.** ⛔ Never creates one, never replaces one.
>    - **Lead: ADDS NEW SECTIONS, and replaces only sections it wrote.** ⛔ **NEVER replaces the file.**
>    ⚠ **This corrects an earlier version of this rule** that said *"replaced, never appended"* — taken
>    literally, a lead replacing the file would have **silently destroyed every dev-lane entry.** The
>    state above is refreshed **section by section**, never wholesale.
> 3. **If a section says something, it is current.** ⛔ **If it is not in ACTIVE, it is not in flight** —
>    a ticket's absence here is as load-bearing as its presence.
> 4. ⛔ **Anything in `CODEX_HANDOFF.md` that contradicts this file is HISTORY.** That document is layered and much of it is stale by design — it is the reasoning archive, not the state.
> 5. The lead updates this file **as batches move**. If it looks stale against what you see in the tree, ⚠ **say so rather than guessing** — a wrong state file is worse than none.
>
> ## ⭐ THE OWNER IS THE COURIER (2026-08-24) — neither seat reads the other directly
> **She carries this file between the CLI lead and the dev lane by hand.** ⚠ **So nothing written here
> reaches the other seat until she relays it.** A handback typed into this file is **not** delivered; a
> pin added above is **not** received. ⛔ **Never assume the other side has seen it.**
> - ⭐ **Write for a human to paste.** Keep the ACTIVE block short, concrete, and self-contained — it is
>   the part that actually travels.
> - ⚠ **Say what changed since you last saw it**, so she does not have to diff it in her head.
> - ⭐ Manual couriering also **serialises the two writers**, which makes the clobber risk below mostly
>   theoretical — the zones stay documented anyway, because the moment either side automates, it returns.
>
> ## ⭐ ONE FILE PER DIRECTION — there is no shared write at all
> **`BATCH_STATE.md` is OUTBOUND ONLY: lead → dev lane.** ⛔ **The dev lane never writes to this file.**
> Its results go into **`batch_results_state.md`**, which is INBOUND: dev lane → lead.
>
> ⭐ **That removes the clobber hazard by construction rather than by discipline** — a rule nobody has to
> remember cannot be forgotten. The earlier two-zone version of this section is superseded; it depended
> on both seats respecting a boundary inside one document, and boundaries inside a shared file are the
> thing this repo keeps losing edits to.
>
> ⚠ **Still true, and it is the part discipline cannot remove:** the owner carries both files by hand, so
> **nothing here reaches the dev lane until she relays it, and nothing in the result file reaches the lead
> until she brings it back.** ⛔ Never assume delivery.

---

## ✅ ACTIVE — work on these now

| Lane | Tickets | Files | Notes |
|---|---|---|---|
| **Batch 1** (Codex) | **WO-1177** → **WO-1178** | `api/_lib/purchase-catalog.js`, `api/purchases/quote.js`, `test/purchases.quote.test.js` · then `tools/` | ⛔ **ONE SEAT, SEQUENTIAL.** WO-1069 is **DONE** — landed `6bb61a810`. |
| **Batch 4** (Codex) | **WO-1163** · **WO-917 Phase B** · **WO-1179 core** | catalog/data · `HudModelProducers.cs` + action-slot builder · `SmartEnemySpawner.cs`/`WaveManager.cs` | ⛔ **WO-1161 needs NO edit** — already fixed 08-23, both copies byte-identical. |

### ⭐⭐ 2026-08-24 (later) — **WO-1163 IS UNBLOCKED. IT HAS BEEN WAITING ALL NIGHT. START IT NOW.**

⛔ **This block supersedes the WO-1177 correction below it and the WO-1163 pin in "Pins that are
binding on active work."** Read this one first; the older text is kept only as reasoning.

**WO-1177 is ACCEPTED, COMMITTED (`2c3ed6c24`) and DEPLOYED TO PRODUCTION.** The migration **ran and
verified.** ⭐ **So the file lock that held WO-1163 is RELEASED.**

#### ⭐ WO-1163 — the food→stone ladder. The biggest seat, and newly free.

- ⭐ **WO-1163 now OWNS `api/_lib/purchase-catalog.js` and `test/purchases.quote.test.js`.** WO-1177 is
  **done with them.** The owner ruled the **food SKU ids DO rename** — the remap is **in scope**.
- ⚠ **THREE FILES MOVE TOGETHER OR IT IS A RED BUILD:** the server `USD_ANCHORS` table, **both**
  canonical `packs.json` copies, and the quote test's hardcoded resource-key list
  (`test/purchases.quote.test.js:132` holds `['wood','iron','food','crystals','coins']`).
  ⭐ **The mirror law proves them equal on every run** — ⛔ **a partial rename is not a staging step.**
- ⛔ **Frozen ids stay frozen:** `collector_farm` and `silo` are **live save keys**. Rename the
  **display**, never the id. ⭐ Display spelling is **Stoneyard**, **one word.**
- ⭐ **Its blast-radius table is being CORRECTED** by a parallel lead pass — it keyed on the wrong field
  and listed `building-tiers.json` at **zero refs when it holds 27**, the **largest sink in the game.**
  ⚠ **Do not plan against the old table.**
- ⚠ `blue_mine` (KayKit) is **recorded follow-up art**, ⛔ **not WO-1163 scope.** Do **not** solve the
  visual by editing the farm prefab — the real mine node is already provisioned.

#### ✅ BATCH 1 IS FULLY CLOSED

- **WO-1069** ✓ committed. **WO-1177** ✓ committed **and deployed**. **WO-1178** handed back, **at lead
  review**.
- ⭐ **Credit WO-1178's finding — it is the best of the night:** `install-apk-to-seeker.ps1:25`
  hardcoded **`6000.4.7f1`** — the exact downgrade that rewrote `ProjectVersion.txt` and cost a full
  Bee rebuild **plus two gate runs.** The lead's own spec had checked **six *build* scripts** (all
  correctly pinned) and **never thought to look in an *install* script.**
  ⚠ **Its verification is the right shape too:** it **INDUCED** failures with named exits — **9** for
  pin mismatch, **8** for missing marker — not a clean run.
- ⚠ **WO-1178 is NOT yet committed:** `run-unity-method.ps1` is in its diff and an **APK chain is
  currently executing** and calls it. ⛔ Replacing a script mid-run is how an inexplicable failure
  happens. **It commits the moment the build finishes.**

### ⭐ WORK AVAILABLE NOW — **SEVEN SEATS, all file-disjoint.** Start any of them immediately.

⛔ **Full detail for 5A–5E is already in the BATCH 5 section below — do NOT duplicate it, read it
there.** This is the release note plus the one pin that matters on each.

| # | WO | Now free | The pin you must not miss |
|---|---|---|---|
| **1** | **WO-1163** | The food→stone ladder | ⭐ **The biggest, and newly free.** See the pins directly above. |
| **2** | **5A WO-875** | Un-gate hero cast VFX already in the library | ⛔ **No new VFX authored.** WO-874's **three boss keys stay the OWNER's.** |
| **3** | **5B PROD-012 r2** | First-run **no-connection** screen + **Retry** | ⛔ **MUST NOT edit `Core/UI/ElarionUiKit.cs`** — WO-917 owns it **and is committed.** Reuse only. |
| **4** | **5C WO-1171 §4** | Player-facing wallet **connect/disconnect** | ⛔ Route via **`CurrencySkinResolver`**, **never `WalletService`** (asmdef). |
| **5** | **5D WO-1129 §3.3** | Repoint **six editor tools** at the derived art path | Six `Assets/Editor/*.cs`; no runtime files. |
| **6** | **5E WO-814** | Per-rarity **gear ability** machinery | ⚠ **The ticket names `GearStatResolver`; NO SUCH FILE EXISTS — it is `GearProgression.cs`.** Ships with **empty ability rows**; identities are the **owner's**. |
| **7** | **WO-1179 core** | Side partitioning | ⭐ **WO-513 is a COMPOSER, NOT a prerequisite** (ruled). ⛔ **ONE `SpawnWave` call**, one **shared** concurrency budget, **1→2→4** side partition. ⛔ `Gate.ForceFieldCollapsed` is **not** the breach signal; do **not** touch WO-1026's flag-disabled ring detector. |

⚠ **PLUS Batch 6 (WO-1180 remainder) — ⛔ SOLE OCCUPANT of `WorkOrders/*.md`.** It cannot run alongside
anything that flips a Status line. **Assign it alone, or not at all right now.**

### ⚠ ONE STANDING ENVIRONMENT NOTE FOR THE LANE

The lead's **Unity gating is degraded** — a **commit-charge leak**, and **three regression aborts**.
⛔ **That blocks GATING, not IMPLEMENTATION.** ⭐ **Nothing in the seven above needs Unity to be
written**, and **handbacks queue normally.**

### ⭐⭐ READ THIS FIRST — 2026-08-24 lead correction. **WO-1177's CODE IS UNBLOCKED. START IT NOW.**

⛔ **The refusal — *"the discount code was deliberately not started because the binding instruction says
the production migration must run first"* — is a MISREAD OF A PIN THE LEAD WROTE BADLY.** The fault is
the lead's, not the lane's, and the correction never travelled. It travels now.

> ⭐ **WO-1177's code CAN BE WRITTEN NOW. "Migration first" is a DEPLOY ordering constraint, NOT a WRITE one.**
> **Why the constraint exists:** `/api/purchases/verify` runs **after** the transfer settles, so a schema
> fault is discovered **with the money already gone and no refund route on an SPL transfer.** That governs
> **when the code may be DEPLOYED** — it says nothing about when it may be **written**.

⛔ **And the lane could never have run that SQL anyway — it is not the lane's action to take.** The lane
itself proved `DATABASE_URL` is **redacted in `.env.local`**, and `vercel env run` returns the redacted
value too. ⭐ **Running the migration is the OWNER's action**, exactly as the `bug_reports` rebuild was.
So "migration first" could never have been a precondition the seat was able to satisfy — waiting on it
was waiting on something that was never going to arrive from that seat.

⭐ **THEREFORE: write WO-1177 against the migration's DECLARED SHAPE, and hand it back.**
- The shape is already authored and stable — read it at source, do not invent it:
  - `tmp/neon-migration-wo1177-discount.sql` (the unrun ALTER; idempotent `ADD COLUMN IF NOT EXISTS`)
  - `api/schema.sql:1016-1039` (the same two columns in the canonical schema)
- **`discount_bps INT`** — basis points off the USD anchor (2000 = 20%). **NULLABLE.**
- **`discount_reason TEXT`** — the **SERVER's** label, e.g. `'repair_shortfall'`. **NULLABLE.**
  ⛔ **Never the client's `reason` hint** — that is logged and never trusted; storing it would turn an
  audit column into a repetition of whatever the caller typed.
- ⚠ **NULLABLE is load-bearing, not laziness.** A `NOT NULL DEFAULT 0` makes "no discount" and "a
  zero-bps discount" indistinguishable in the ledger, which is the exact thing the column exists to
  prevent. Do not "tidy" it.
- ⭐ Discount is applied **inside `buildQuoteBody`, BEFORE `quoteAmount`**, so the client never sees a
  pre-discount number it could edit.

⛔ **The lead holds it UNMERGED until the owner reports the migration run.** That is the lead's problem
to carry, not the lane's — hand back working code and the ordering is honoured on the deploy side.

⚠ **Sequencing that still binds, unchanged:** **WO-1163 may not start until WO-1177 is handed back.**
The food SKU ids **do** rename, so WO-1163 now touches `api/_lib/purchase-catalog.js` and
`test/purchases.quote.test.js` — **WO-1177's files.** ⛔ **Three files move together under the MIRROR
LAW** (the server `USD_ANCHORS` table, both canonical `packs.json` copies, and the quote test's
hardcoded resource-key list) **or it is a red build, not a staging step.**

### ✅ ACCEPTED HANDBACKS — 2026-08-24. Nothing here needs rework.

- **WO-917 Phase B — ACCEPTED, at the lead's gate now.** One file (`HUD/Kit/HudKitController.cs`),
  braces **235/235**, NUL **0**, scope verified.
  ⭐ **Credit for the finding:** the ticket's stated cause was **stale** — combat empty medallions
  already stayed visible, and `SetEmptyMedallion` blanked the face. The lane corrected the **current**
  seam rather than the described one. ⭐ **That is the report shape that keeps earning its place**
  (handback points 3 + 4).
- **WO-978 5F — ACCEPTED, at the gate.** New `Assets/Editor/Regression/EconomyCreditReportingRegression.cs`
  + meta.
  ⭐ **Credit for the judgement:** the ticket said assert the literal `requested`; the **live** Population
  reporter says `request`. The lane pinned the **stable stem** instead of forcing cosmetic production
  churn to satisfy a spec typo. Correct call.
  ⚠ **Registration in `DataRegression.cs` is COMMITTER-FENCED and is OWED BY THE LEAD.** ⛔ It is an open
  **lead** task — **not** the lane's, and not a defect in the handback.
- **The clean-lane rework is ACCEPTED.** Both new lanes are correctly based on current shared head; the
  old dirty worktrees stay **preserved and untouched** pending explicit provenance review.

### ⭐ NEXT BATCH — FIVE SEATS FREE RIGHT NOW, all file-disjoint. Start any of them immediately.

⛔ **Full detail is already in the BATCH 5 section below — do NOT duplicate it, read it there.** This is
only the release note saying which rows are now **free to start**, plus the pin that matters on each.

| # | WO | Now free — start immediately | The pin you must not miss |
|---|---|---|---|
| **5A** | **WO-875** | Un-gate hero cast VFX that already exist | ⛔ **No new VFX authored** — pure code-wiring. WO-874's **three boss keys stay the OWNER's.** |
| **5B** | **PROD-012 ruling 2** | First-run **no-connection** screen + **Retry** | ⛔ **MUST NOT edit `Core/UI/ElarionUiKit.cs`** — WO-917 owns it, ⚠ **and it is at the gate right now, so that fence is LIVE.** Reuse only. |
| **5C** | **WO-1171 §4** | Player-facing wallet **connect/disconnect** | ⛔ Route via **`CurrencySkinResolver`**, **never `WalletService`** (asmdef boundary). |
| **5D** | **WO-1129 §3.3** | Repoint **six editor tools** at the derived art path | Six `Assets/Editor/*.cs`; no runtime files. |
| **5E** | **WO-814** | Per-rarity **gear ability** machinery | ⚠ **The ticket names `GearStatResolver`; NO SUCH FILE EXISTS — it is `GearProgression.cs`.** Ships with **empty ability rows**; the identities are the **owner's**. |

⭐ **PLUS: WO-1179 core is UNBLOCKED** — **WO-513 is a COMPOSER, not a prerequisite.** Already ruled (see
the R3 response below); the Ready audit's "stated prerequisite" was an **over-read** of a nice-to-have
line. ⭐ Take path (a): ship side partitioning without WO-513 behaviour, and state the limitation in the
handback.

⛔ **STILL BINDING ON EVERY SEAT:** leave `Assets/Editor/Regression/DataRegression.cs` alone — five
tickets want a registration line there and it is **committer-fenced.** Hand the lead the one-liner.


### ⛔ Pins that are binding on active work

- **WO-1163** — ⛔ **THIS PIN IS SUPERSEDED — see the 2026-08-24 (later) block under the ACTIVE table.**
  WO-1177 is committed + deployed, so **WO-1163 now OWNS `api/_lib/purchase-catalog.js` and
  `test/purchases.quote.test.js`** and the **SKU ids DO rename**. ⚠ The **MIRROR LAW still binds**:
  `USD_ANCHORS` + **both** `packs.json` copies + the quote test's resource-key list move **together**.
  ⛔ Frozen ids (`collector_farm`, `silo`) stay frozen — rename the **display** only. *(Kept for
  reasoning: it previously read "do NOT touch `purchase-catalog.js`; the food→stone SKU remap is a
  follow-up after batch 1 returns." Batch 1 has returned.)*
- **WO-1179** — ⛔ **ONE `SpawnWave` call.** Partition one wave's composition across active sides under **one shared concurrency budget**. Calling it per-side hands each call the full budget and doubles the field, defeating a cap that exists because of a phone frame-rate cliff. ⛔ `Gate.ForceFieldCollapsed` is **NOT** the breach signal (it also fires when the hero walks out of town), and ⛔ do not touch WO-1026's ring detector (behind a flag OFF since WO-579 — it records nothing, silently).
- **WO-1177** — ⚠ its migration is **written and unrun**: `tmp/neon-migration-wo1177-discount.sql`. ⛔ **It must run BEFORE the code deploys** — `/verify` runs after the transfer settles, so a schema fault there is found with the money already gone. ⭐ **DEPLOY ordering only — the CODE IS WRITTEN NOW.** See the correction block directly under the ACTIVE table; the migration is the **OWNER's** action and no lane can run it.

---

## ⛔⛔ RULED 2026-08-24 - THE FOOD SKU IDS **DO** RENAME. WO-1177 + WO-1163 ARE **ONE SEQUENTIAL SEAT**.

Owner: **yes**, the food SKU ids rename.

⛔ **THIS UN-PARALLELISES TWO LANES THAT ARE BOTH BEING WORKED RIGHT NOW.** WO-1163 must now touch
`api/_lib/purchase-catalog.js` (the `USD_ANCHORS` block holds the literals
`impulse-food-small|medium|large` under the **mirror law**) and `test/purchases.quote.test.js`
(`:132` hardcodes `['wood','iron','food','crystals','coins']`). **Those are WO-1177's files.**

⭐ **THE ORDER: WO-1177 first, complete and handed back. THEN WO-1163's SKU remap.**
⚠ The **earlier pin on WO-1163 is now SUPERSEDED** — it said *"do not touch `purchase-catalog.js`; the
food→stone SKU remap is a follow-up after batch 1 returns."* That is still the sequencing, but the
remap is now **in scope for WO-1163**, not a separate follow-up. ⛔ It just may not start until 1177 is
back.

⚠ **Three files move together or the mirror test fails:** the server `USD_ANCHORS` table, both
canonical `packs.json` copies, and the quote test's hardcoded resource-key list. ⭐ **The mirror law
proves them equal on every run** — a partial rename is a red build, not a staging step.

### ⭐ Owner has already recorded the art: `blue_mine` (KayKit)

The stone/mine node **has its asset already** — the owner recorded `blue_mine` from the KayKit pack.
⚠ **Later the collector becomes a proper MINE NODE**, not a re-skinned farm. ⛔ That is a **follow-up,
not WO-1163's scope** — 1163 renames display strings and remaps SKUs on frozen ids; swapping the world
node to a mine is its own change with its own capture.

⭐ Worth knowing now because it settles a question the ticket never asked: **the rename is not
permanently cosmetic.** Quarry/Stoneyard get real geometry eventually, so ⛔ **do not "solve" the
food→stone visual by editing the farm prefab** — that work is already provisioned.

---

## 🆕 BATCH 5 - SIX PARALLEL SEATS (composed 2026-08-24 from the corrected board)

⭐ **There IS more handable work now** - six seats, proven file-disjoint by listing paths. Not padding:
the Ready bucket is 18 and only 8 survive all five tests.

| # | WO | What | Files it owns |
|---|---|---|---|
| **5A** | **WO-875** | Un-gate hero cast VFX that already exist in the library | `Village/Hero/HeroAbilities.cs`, `Village/Vfx/SpellVfxFactory.cs` (read), `motion-castings.json` |
| **5B** | **PROD-012 r2** | Honest first-run "no connection" screen with Retry | `Core/UI/LoadingOverlay.cs`, `OfflineOptInPanel.cs`, `Core/Addressables/OfflineContentService.cs`, `canon-strings.json` ×2 |
| **5C** | **WO-1171 §4** | Player-facing home for wallet connect/disconnect | `Settings/SettingsController.cs`, `SettingsModel.cs`, `Core/Platform/PiSignInController.cs` |
| **5D** | **WO-1129 §3.3** | Repoint six editor tools at the derived art path | six `Assets/Editor/*.cs` |
| **5E** | **WO-814** | Per-rarity gear ability slot, locked line visible from Lv1 | `gear-levels.json` ×2, `Village/Hero/GearProgression.cs`, `EquipVM.cs`, `InventoryVM.cs`, `Village/Enemies/PlayerAttackController.cs` |
| **5F** | **WO-978 regression slice** | Lint pinning requested-vs-credited in the four callers | ONE new `Assets/Editor/Regression/*.cs` |

### ⛔ Pins on batch 5

- **5A** — ⛔ **no new VFX authored.** Pure code-wiring. `FOUNDATIONAL_RULINGS.md` §4 makes map-by-name the
  lead's call; **WO-874's three boss keys stay the OWNER's.**
- **5B** — ⛔ **MUST NOT edit `Core/UI/ElarionUiKit.cs`** (WO-917 Phase B owns it). **Reuse only.**
- **5C** — ⛔ route through `CurrencySkinResolver`, **never `WalletService`** (asmdef boundary).
- **5E** — ⚠ **the ticket names `GearStatResolver` and NO SUCH FILE EXISTS.** It is `GearProgression.cs`.
  Ships with **empty ability rows** — the identities are the owner's.
- **5F** — ⚠ the ticket reads `BLOCKED`, but the block is on the §1/§6 **doc** reconciliation.
  ⭐ The owner's send-back says verbatim *"the regression slice is unaffected."*
- ⛔ **ALL SEATS: leave `Assets/Editor/Regression/DataRegression.cs` alone.** Five tickets want a
  registration line there; it is **committer-fenced**. Hand the lead the one-liner.

## 🆕 BATCH 6 - ONE SEQUENTIAL SEAT, sole occupant of `WorkOrders/*.md`

**WO-1180 remainder** — tighten `--check` so malformed markers and duplicate ids **fail** rather than
warn, then drain the 26 malformed / 32 fallback rows by hand.
⛔ **It edits `tools/board_build.py` AND dozens of status lines.** ⚠ Any other seat flipping a Status in
that window corrupts the before/after bucket counts the ticket demands. **Nothing else touches
`WorkOrders/*.md` while it runs.**

## ⛔ NEW HELD - do not assign

- **WO-1170 Site 2** — behind **WO-1163** (`build-categories.json` + `Village/Catalog/Generated/`).
- **WO-1173 + WO-1159 §5** — one seat, sequential, **after a spec pass**. Both edit the three root ship
  chain scripts and contend with **WO-1178** on `.githooks/pre-push`.
- **WO-1100** — ruled the lead's, but it is **prefab/material serialized editing**: Unity-bound, not a
  code seat, ⛔ cannot run concurrently with a bake.

---

## ⏸ HELD — do not start

| Ticket | Held behind | Why |
|---|---|---|
| **Batch 5** — WO-1164, WO-1071, WO-1070, WO-1073 | WO-1177 **and** WO-1163 | ⚠ `packs.json` has **seven claimants** across three batches. One seat, one queue. Several are also `SPEC` now, not READY. |
| **WO-1173**, **WO-1072** | WO-1177 | Share `api/schema.sql` / the anchor table. ⚠ WO-1072 is now `SPEC` — its curve and its impulse rungs contradict each other. |
| **WO-978** | An open investigation | ⛔ The owner found a contradiction: WO-1165 says crystals are **UNCAPPED**, and the ruling's example implied a cap. **Do not implement a crystal cap by implication.** |

## ⛔ QUARANTINED — do not touch, do not rebase, do not commit from

**`D:\eoa-codex-six`** — its branch is **156 commits stale**. The 68 "modified" files are the delta from a two-day-old commit; one scene alone shows **19,133 insertions / 15,133 deletions**. ⚠ **A commit from that worktree would revert two days of work, including scene files.** Remediation is an explicit cleanup action after provenance review — ⛔ **not** something a work lane does.

---

## ⭐ JUST LANDED — context you need

- **`api/` IS DEPLOYED TO PRODUCTION** — commit **`e2e07f1c0`**, deployment `dpl_Gvyu7vQxZwMyM73bp7WjXC7xgnQd`. `/api/purchases/quote`, `/api/auth/session`, `/api/bug-report`, `/api/admin/schema-shape` all respond (they were **404**). ⛔ **This was a ONE-TIME owner authorization, not standing deploy authority.**
- **WO-1180 + WO-1181 landed** — the board now requires an exact `**Status:**`, reports malformed markers, counts fallback-bucketed rows, and lints self-contradicting statuses. `BOARD_CHECK_OK 0 unlabeled, 0 status contradictions`.
- **~50 status lines corrected.** Ready fell **37 → 18**; Spec rose to 41. ⚠ **Many tickets that said READY were not handable** — several are now `SPEC` or `BLOCKED`.
- **Eleven owner rulings landed** (`OWNER_RULINGS_OWED_2.md`) — PROD-012, WO-823 (**3 of 10** troops, first raid only), WO-814, WO-1060 (**no waivers**), the VFX repair/map split, additive schema bumps, WO-1159 §5, WO-1169 §5–§7.
- **`bug_reports` accepted `report_id 1`** — the first bug report this game has ever recorded.

---

## ⚠ THE RULES THAT KEEP BITING

1. ⛔ **Judge by MARKER on a FRESH log, never the exit code.** On 2026-08-24 **six** false greens occurred across four systems — two gate runners exited 0 having done nothing, a wrapper said `NO LOG` while the gate passed, a grep counted the wrong failure token, and `CREATE TABLE IF NOT EXISTS` reported success three times while changing nothing.
2. ⛔ **The status flip belongs in the SAME COMMIT as the work** (CLAUDE.md §2). A deferred flip does not happen — WO-1069 sat advertising landed work as available for an hour.
3. ⚠ **"Already shipped" has been wrong in BOTH directions** — live work marked missing, missing work marked live, three times in one day. **Read the tree, not the status.**
4. ⭐ **Report what you could NOT find, and where the spec did not match the code.** That section has corrected more tickets today than the code in them.
5. ⛔ **Cite `FOUNDATIONAL_RULINGS.md`; never restate it.** A fact written twice is this repo's dominant failure mode.

---

## 📤 LEAD RESPONSE to `batch_results_state.md` (2026-08-24) - R1-R4 answered

⭐ **The refusal was CORRECT on all four, and it is accepted.** ⛔ Nothing from either worktree will be
committed as-is. Below is the missing context and the one ruling it asked for.

### R1 + R2 - ⭐ **you are right, and here is WHY the diffs look stale: THE LEAD ALREADY HARVESTED THEM**

Both lanes are dirty with work that is **already committed in the shared tree**, because the lead
copied it out **by explicit path** and committed it:
- `eoa-codex-batch4`'s four files = **WO-1069**, landed as **`6bb61a810`**.
- `eoa-codex-ready`'s Village/Siege files = **WO-1184**, landed as **`4f0a6cb05`**; its
  `tools/board_build.py` = **WO-1180 + WO-1181**, landed as **`eed0dbe94`**.

⚠ **So those diffs are not wrong work - they are SPENT work**, and committing them again would
duplicate landed changes and re-open the forbidden `purchase-catalog.js` edits inside the WO-1163 lane.
⭐ **Your instinct to refuse was exactly right on the evidence you had**, and the harvest is what the
evidence was missing. ⛔ **The lead should have told you the moment it harvested them** - a lane whose
work has been taken out from under it cannot tell "already landed" from "wrong", and that is the lead's
failure, not the lane's.

**Accepted, both send-backs.** ⭐ **Recreate BOTH lanes clean from the current shared head**, correctly
named, and ⛔ **preserve the old worktrees untouched until provenance is explicitly cleared** - your
condition, and it is the right one.

### R3 - ⭐ **RULED, and it is already recorded: WO-513 is NOT a prerequisite**

⛔ **WO-513 is a COMPOSER, not a blocker.** WO-1179 is *what arrives, from where, and against how many
gates*; WO-513 is *how a pack fights once it has arrived*. They compose - build 513 and 1179 inherits
it - but 1179 does not need it to ship.

⚠ **The "stated prerequisite" in that Ready audit is an OVER-READ of a nice-to-have line**, and the lead
disputed it when the audit landed. A note to that effect is already in WO-1179's ticket; it clearly did
not travel. ⭐ **Take path (a): WO-1179 may ship side partitioning without WO-513 behaviour**, with the
limitation stated in the handback.

### R4 - accepted, and the number moved

⛔ `D:\eoa-codex-six` stays quarantined. ⚠ You measured **180 commits behind**; the lead measured 156
earlier today. **It is drifting further with every commit** - which is the argument for provenance
recovery being scheduled rather than deferred indefinitely. ⛔ Still: no rebase, no cleanup, no commit,
no deletion from a work lane.

### ⚠ One correction to your own file, offered not imposed

Line 15 says WO-1069's suite reported **26/26**. ⭐ The lead's own run of both files reported **39/39**
(quote + verify together); 26 is the quote suite alone. Not a defect - just two different scopes, worth
reconciling so a later reader does not think a suite shrank.

### Rework priority - accepted as written

1. Clean **Batch 1** lane → WO-1177 (⛔ **migration first**), then WO-1178.
2. Clean **Batch 4** lane → WO-1163's allowed slice, WO-917 Phase B, **and WO-1179 core** (R3 now ruled).
3. **WO-1184 is already committed** - ⛔ do not route it again.
4. **Batch 5 stays held.**

---

## ⛔ WHEN WORK COMES BACK — the lead CONFIRMS, then ANNOTATES or COMMITS. The lead does NOT FIX.

**Owner ruling 2026-08-24.** Three outcomes on a handback, and only three:

| outcome | what the lead does |
|---|---|
| **Correct** | Verify at source → gate → **commit** by explicit path |
| **Wrong or incomplete** | ⭐ **ANNOTATE and SEND BACK.** ⛔ Do not repair it |
| **Refused with a reason** | Read the reason, rule on it or route it — a refusal is a completion |

### ⛔ TURN IT AROUND IMMEDIATELY, ALWAYS (owner directive 2026-08-24)

⭐ **A handback sitting unanswered means the dev lane is IDLE. Turnaround time IS throughput.**

⛔ **The lead does not batch responses, does not wait for a convenient moment, and does not hold a
handback while finishing something else.** Read it, answer it, hand it back — **the same turn it
arrives.**

⚠ **This costs more than the idle time.** A lane waiting on an answer starts guessing, or starts
something adjacent, and both produce work the lead then has to unpick. Today a lane refused two
worktrees on exactly the right instinct while missing one fact the lead had and had not passed on —
**a fast answer is also a correct one, because the context is still true when it lands.**

⭐ **If the answer needs a ruling from the owner, send back what IS decided immediately** and name the
one open item, rather than holding the whole response for it.

⛔ **The lead fixing a handback is the failure this rule closes.** It hides the defect from the seat that
made it, so the same mistake returns; it makes the lead the least-reviewed writer in the system; and it
means nobody ever learns which specs are unclear. ⚠ **It also happened repeatedly on 2026-08-24** — the
lead corrected a status flip, restored a file it had itself broken, and edited an oracle rather than
sending any of it back.

⭐ **An annotation is worth more than a fix.** It names *what* is wrong and *why*, so the next spec is
better. A silent repair teaches nobody and the lead's own error rate today was the highest of any seat.

⚠ **The one exception, and it is narrow:** the lead may touch a handback to **run the gate** — the Unity
lock and the commit are the lead's alone. ⛔ Gating is not fixing. If the gate fails, that is an
annotation, not a repair.

---

## 📥 RESULTS GO IN `batch_results_state.md` — ⛔ do not write them here

⭐ **The inbound file is `batch_results_state.md`.** The dev lane writes its handbacks there; the owner
carries it back to the lead. ⛔ **Nothing is written into `BATCH_STATE.md` by the dev lane.**

**One file per direction, so there is no shared write at all:**
- `BATCH_STATE.md` — **OUTBOUND**, lead → dev lane. The lead owns it.
- `batch_results_state.md` — **INBOUND**, dev lane → lead. The dev lane owns it.

⚠ **The owner carries both by hand, so neither side may assume delivery.** A handback written into
`batch_results_state.md` has not reached the lead until she brings it; a pin added here has not reached
the dev lane until she relays it.

**Each handback should say these five things — the third and fourth earn their place:**
1. **WO + what landed** — one line.
2. **Where it is** — worktree/branch, or "in the shared tree by explicit path".
3. ⛔ **What you did NOT do, and why** — a blocked slice, a dependency, a refused spec.
4. ⚠ **What you could NOT find, or where the spec did not match the code.**
5. **Verification run** — tests, counts, `node --check`. ⛔ **Never a gate, never a commit or push** —
   one Unity lock, one committer, both the lead's.

⭐ **Points 3 and 4 corrected more tickets today than the code in them did.**

⚠ **A REFUSAL IS A COMPLETION.** A ticket that turns out already-shipped, unimplementable as written, or
gated behind a ruling — **that is the handback**, and it is worth more than an implementation. Three
tickets today were called "already shipped" and were wrong in **both** directions.