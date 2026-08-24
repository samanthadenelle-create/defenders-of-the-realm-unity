# What Codex can take off the lead - ranked by actual leverage

Written 2026-08-24, after the first handback. Ranked by **time it actually saves**, not by how
delegable it sounds.

---

## 1. ⭐ EVERYTHING under `api/` and `tools/` - the single biggest win, and it is structural

**Unity is ONE LOCK.** One batchmode run at a time, project-locked, and every gate, bake, build and
regression queues behind it. Today a single editor-version thrash cost a full Bee rebuild and ate two
gate runs.

⭐ **`api/`, `tools/`, `site/` and `test/` contend for NOTHING.** No lock, no gate, no queue. Node
work runs at unlimited parallelism *while* Unity work is blocked. So routing backend work to Codex
does not just move hours - it moves them into a lane that was **idle by construction**.

Give it: endpoints, SQL, the admin console, migrations, `schema-parity`, the ship-chain scripts,
Vercel-side anything. ⚠ `api/` is git-tracked **in this repo** - it is not a separate project.

## 2. ⭐ Read-only RCA - infinite fan-out, zero contention

Diagnosis touches nothing, so **there is no limit on how many run at once** and no branch is needed.
This is where a dozen agents cost nothing but tokens.

The standing high-value sweep is **"is this actually already shipped?"** The board has a dozen
tickets whose own Status lines admit they do not know. ⛔ The live risk is a dev lane **rebuilding
working code** - WO-822 nearly went out today and its own status said it may already be done.

## 3. ⭐ PRE-FLIGHT MY SPECS BEFORE THEY ARE HANDED OUT - the surprise of the first handback

**Two of the three batch-1 tickets I wrote had wrong premises**, and Codex caught both at intake for
the cost of a read:
- **WO-1069** asked the resolver to serve `hearth-spark`. That pack is not an impulse SKU at all, and
  the guard that would reject it enforces a **binding WO-947 ruling**. The ticket asked for a
  violation. The resolver was right.
- **WO-1178** proposed a pre-run version check. A raw launch rewrites the file *afterwards*, so the
  check passes and the damage still happens. The fix did not close the hole it was written for.

⭐ **A wrong spec caught at intake costs a read. Caught after implementation it costs the
implementation, the review, the gate, and the trust.** Make intake-refusal a standing expectation,
not an exception: *does this ticket ask for something the code deliberately forbids?*

## 4. Regression authoring - the durable half of every fix

Most fixes have two halves: the change, and the test that stops it returning. **The test half is
safe to delegate** - it is additive, it touches no production path, and a wrong test fails loudly
instead of shipping quietly.

⚠ With one demand attached: **watch it FAIL first.** A test authored green proves nothing. This
project has shipped gates that never ran at all (`checkin_gate.ps1` did not parse under PowerShell 5.1
for an unknown length of time).

## 5. The "one fact written twice" purge (WO-1170)

The repo's dominant failure mode: a stale WO number block, a retired dependency table, a hardcoded
repo root, eight hardcoded level ceilings, a duplicated R2 push. Each was correct when written.

⭐ Codegen + a hash-parity gate is the **proven** answer here (WO-1137). It is mechanical, high
volume, and every site is independent - ideal dev-lane work.

## 6. Instrumentation passes (CLAUDE.md §12)

Adding `FlowTrace.Step/Warn/Fail` and `Guard.Try` is additive, low-risk, and pure future leverage.
⛔ Never removing any - instrumentation is permanent.

⚠ **Judgement required, so specify the sites:** today an agent was told to use `FlowTrace.Throttle`
and correctly refused - it logs via `Sink.Info` and discards suppressed calls, so it would have
demoted the Warn **and** thrown away the repeat that was the entire signal.

## 7. Canon staleness sweeps (CLAUDE.md §15)

Skim the load-bearing docs against `CANON_GROUND_TRUTH_<date>.md`; fix or flag. Read-mostly, and it
prevents the next 1090-file audit.

---

# ⛔ What Codex must NOT take

| Never | Why |
|---|---|
| **Gating** | One Unity lock, held by the lead. A second batchmode run makes both fail. |
| **Committing / pushing** | ONE committer. Two duel on `.git/index.lock` and produce a **false "pushed"**. |
| **Closing tickets** | The **PO felt-verifies and closes** (§13). Code landing is not done. |
| **Design / monetization rulings** | The owner's. ⛔ Never invent a policy constant to unblock yourself - **say it is missing.** Codex did exactly this today on the discount window and it was the right call. |
| **Scene files** | `.unity` corruption-on-resave history; bakes go through the builder. |
| **Judging feel** | Headless cannot. Neither can Codex. |

---

# The handback that makes this work

⭐ **The most valuable section of a handback is "what I could not find, and where the spec did not
match the code."** Today that section corrected two of my three tickets and one of my instructions.
Code I can re-derive; a wrong premise I would have shipped.
