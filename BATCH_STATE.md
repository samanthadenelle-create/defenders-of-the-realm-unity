# BATCH_STATE — the live handoff. Read this FIRST, every time.

**Last written:** 2026-08-24 by the CLI lead.

> ## ⛔ THE PROTOCOL
> 1. **Read this file at the START of every batch, and again before starting any NEW ticket inside one.**
> 2. This file is **REPLACED, never appended.** If it says something, it is current. If it does not say it, it is not in flight.
> 3. ⛔ **Anything in `CODEX_HANDOFF.md` that contradicts this file is HISTORY.** That document is layered and much of it is stale by design — it is the reasoning archive, not the state.
> 4. The lead updates this file **as batches move**. If it looks stale against what you see in the tree, ⚠ **say so rather than guessing** — a wrong state file is worse than none.
>
> ## ⛔⛔ TWO WRITERS, TWO ZONES — the rule that stops us clobbering each other
> **The dev lane writes ONLY inside `## 📥 HANDBACKS`, at the bottom. The lead writes ONLY above it.**
>
> ⚠ **Why:** if both seats rewrite the whole file, whoever saves second wins and the other's update
> vanishes **silently** — the same failure as two committers on one `.git/index.lock`, and this repo has
> already paid for that once.
> - ⭐ **Dev lane: APPEND a dated entry to HANDBACKS. Never edit anything above it, never reorder it.**
> - ⭐ **Lead: when replacing the state above, PRESERVE the HANDBACKS section verbatim.** ⛔ Replacing the
>   file without carrying it forward destroys the other seat's report.
> - ⚠ **If you find your own last entry missing, say so immediately** — that means a replace dropped it,
>   and the protocol failed rather than the work.

---

## ✅ ACTIVE — work on these now

| Lane | Tickets | Files | Notes |
|---|---|---|---|
| **Batch 1** (Codex) | **WO-1177** → **WO-1178** | `api/_lib/purchase-catalog.js`, `api/purchases/quote.js`, `test/purchases.quote.test.js` · then `tools/` | ⛔ **ONE SEAT, SEQUENTIAL.** WO-1069 is **DONE** — landed `6bb61a810`. |
| **Batch 4** (Codex) | **WO-1163** · **WO-917 Phase B** · **WO-1179 core** | catalog/data · `HudModelProducers.cs` + action-slot builder · `SmartEnemySpawner.cs`/`WaveManager.cs` | ⛔ **WO-1161 needs NO edit** — already fixed 08-23, both copies byte-identical. |

### ⛔ Pins that are binding on active work

- **WO-1163** — ⛔ do **NOT** rename any SKU id, and do **NOT** touch `api/_lib/purchase-catalog.js` or `test/purchases.quote.test.js`. The three food SKUs live in the `USD_ANCHORS` block **batch 1 owns**, and that file carries a **MIRROR LAW** proven by the quote test. The food→stone SKU remap is a **follow-up after batch 1 returns.**
- **WO-1179** — ⛔ **ONE `SpawnWave` call.** Partition one wave's composition across active sides under **one shared concurrency budget**. Calling it per-side hands each call the full budget and doubles the field, defeating a cap that exists because of a phone frame-rate cliff. ⛔ `Gate.ForceFieldCollapsed` is **NOT** the breach signal (it also fires when the hero walks out of town), and ⛔ do not touch WO-1026's ring detector (behind a flag OFF since WO-579 — it records nothing, silently).
- **WO-1177** — ⚠ its migration is **written and unrun**: `tmp/neon-migration-wo1177-discount.sql`. ⛔ **It must run BEFORE the code deploys** — `/verify` runs after the transfer settles, so a schema fault there is found with the money already gone.

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

## 📥 HANDBACKS — ⛔ THE DEV LANE OWNS THIS SECTION. The lead never rewrites it.

**Append a dated entry as each ticket completes.** ⛔ Do not edit anything above this heading.

**One entry per ticket, and say these five things:**
1. **WO + what landed** — one line.
2. **Where it is** — worktree/branch, or "in the shared tree by explicit path".
3. ⛔ **What you did NOT do**, and why — a blocked slice, a dependency, a refused spec. ⭐ **This is the
   most valuable line**; today it corrected more tickets than the code in them.
4. ⚠ **What you could NOT find, or where the spec did not match the code.**
5. **Verification you ran** — tests, counts, `node --check`. ⛔ **Never a gate** — the gate is the lead's
   (one Unity lock, one committer), and ⛔ **never a commit or push.**

⚠ **A refusal is a completion.** If a ticket turned out already-shipped, unimplementable as written, or
gated behind a ruling — **that is the handback**, and it is worth more than an implementation. Three
tickets today were "already shipped" and were wrong in **both** directions.

<!-- entries below this line -->
