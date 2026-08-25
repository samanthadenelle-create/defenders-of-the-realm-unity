# BATCH_STATE — the live handoff. Read this FIRST, every time.

**Last written:** 2026-08-24 by the CLI lead.

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
> Its results go into a **separate result file the owner creates**, which is INBOUND: dev lane → lead.
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

## 📥 RESULTS COME BACK IN A SEPARATE FILE — ⛔ do not write them here

The owner creates and carries a **separate result file** for the dev lane's handbacks. ⛔ **Nothing is
appended to `BATCH_STATE.md` by the dev lane.**

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