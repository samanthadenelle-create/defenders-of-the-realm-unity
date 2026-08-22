# Session handoff — 2026-08-22, CLI seat

⚠ **Point-in-time record. Do not rewrite the body; supersede it by date.**
Branch `wip/village2-and-f8-tickets`. Everything below was verified at source or from captured
logs this session — nothing is asserted from a comment.

---

## 1. STATE OF THE TREE — read this before you touch anything

**My work is committed.** Last commit `38c963bb3`. Seven commits this session, listed in §6.

**The working tree is NOT clean, and that is expected.** ~23 modified files belong to the
**Codex / MON lane**, which was actively working when this session ended. They are that seat's to
finish and signal on. ⛔ **Do not commit them as a batch, and do not `git add -A`.** There is
exactly ONE committer; reconcile by explicit path after the other seat signals ready.

Safety check already done: **`WalletService.cs:235` still reads
`public const WalletNetwork DefaultNetwork = WalletNetwork.Devnet`.** The MON lane's edit to that
file is an additive `Indeterminate` payment-result factory, not a network flip. Re-verify this
before any build that could reach a store — flipping it is owner-gated and an agent never does it.

**Gates as of the last full run:** `COMPILE_GATE_OK` + `REGRESSION_OK 264/264 — 264 green, 0 red,
0 skipped`, both on fresh logs (`Builds/gate-f7.log`, `Builds/reg-f7.log`).
⚠ Those predate the MON lane's uncommitted edits. **Re-gate at HEAD before trusting them.**

**APK:** built and installed-ready at `Builds/Android/DefendersOfTheRealm.apk`, with
`R2_PUSH_OK` + `R2_PARITY_OK 42 object(s) verified`. Built **with** owner-test defines
(`STORE_RAIL_LOCAL_TEST;MONETIZATION_LOCAL_TEST`) for the Devnet purchase canary. It was **never
installed** — the owner had not run `install-apk-to-seeker.ps1` when the session ended.

---

## 2. ⛔ UNRESOLVED AND TIME-SENSITIVE

**~~An owner F8 capture is buried.~~ CORRECTED 2026-08-22 — it was not.** The four newest captures
were triaged read-only at the end of session and **all four are closed by fixes that postdate them**:

| seq | capture | closed by |
|---|---|---|
| 3584 · 11:34 | `Sfx/TowerFire` not found (hard error) | `f03ee0dda` 12:03 — `AudioService.cs:768` now `optional: true` |
| 3585 · 12:00 | preview RT is uniform clear colour | `9f26ad71e` 16:07 — `HeroPreviewViewer` |
| 3586 · 12:01 | same, second occurrence | same |
| 3587 · 12:28 | `[dg_hollow_roads]` portal travel | `59ccd9843` 12:57 |

⚠ **The earlier claim in this doc that seq 3587 "was never read by any seat" was WRONG**, and is
left visible rather than deleted because the error is instructive: `59ccd9843`'s commit message
names *"F8 seq 3587"* explicitly. The ack was legitimate. **Check `git log` for the sequence number
before concluding a capture was buried** — this repo's fix commits cite them.

**The mechanism risk is still real, though it did not bite here.** The ack tool remains a high-water
pointer, and **seq 2329 is a genuine orphan**: two different captures ~2.5h apart sharing one
sequence, neither queued, so one owner flag is unrecoverable by sequence. That is WO-1018's
consumer half, still open.

Related, and the reason it happened: `WO-1018`'s producer half is fixed and proven (1,272 queued
captures) but the **consumer half is untouched** — the scan never looks below the watermark. Also
found: **seq 2329 has two different captures sharing one sequence**, neither queued, so one owner
flag is unrecoverable by sequence. ⚠ The backfill sweep must run **before** any authority flip, or
1,273 captures re-open at once and look like a catastrophic regression.

**The F8 inbox check timed out** (2 min) late in the session — the daemon may be hung. Worth a look.

---

## 3. WHAT SHIPPED THIS SESSION

- **WO-1149** — a purchase now freezes the world through `WorldHold`, the single writer of
  `Time.timeScale`, acquired as a `using` declaration and the first statement of
  `PackStore.Purchase`, so every exit releases by construction.
- **MON backend** (Codex lane, verified here) — `/verify`, `/reconcile`, `/fulfill` live with an
  exactly-once entitlement seam; Devnet canary bounded to one SKU (`hearth-spark`), non-SKR rails
  refused on Devnet. Both monetization flags remain `defaultOn: false`.
- **Realm Store door** — a "Realm Store" tab on the left gear slide-dock. The owner reported the
  vendor NPC was the only entrance. This is a **second caller** of the existing
  `PanelRouter.Open(PanelId.RealmStore)`, not a second opener.
- **Ship-chain passthrough** — `overnight-apk-build.ps1 -Defines '...'` now forwards scripting
  defines. Before this, owner-test symbols were reachable only by a raw `run-unity-method` call
  that **bypasses the §16 R2 push+verify** — the same hole that shipped capsule enemies on 08-20.
- **dApp Store packet** — four submission blockers named with their source lines (§5).
- **`board_build.py`** — was dying on its own console output *after* writing `BOARD.html`, which
  swallowed `BOARD_CHECK_OK` / `DUPLICATE_WO_NUMBERS` / `BANNER_OK`.

**Next WO mint: CLI 1150 / UI seat 1063** — read it off the `CLI_LANES_WO_NUMBERS.md` banner, and
bump your own row in the SAME edit as the mint.

---

## 4. TRIAGE RESULT — the board substantially overstates open work

30 work orders triaged read-only by 19 agents. Full board:
**https://claude.ai/code/artifact/efb7d9f6-d88f-4070-87a0-ebbbdc900c05**

**Already shipped (verify, don't build):** WO-972, 968, 967, 1035, 969, 1031, 1059 (flip to DONE),
1114 (built; needs a Neon `schema.sql` run + `vercel --prod`, plus seed rows for
`dg_healers_cottage` and `dg_folks_granary`).

**Highest-value open items, cause already proven:**

- **WO-1142 is NOT the CDN.** Under §16 it looks like a missing push; the log disproves it — misses
  at lines 17616–23222, asset **resident at 27743**, warm pass `resident=35/35`. It is a
  **cold-start residency race**: every other caller subscribes to `WhenSettled` and self-heals,
  `BaseLayoutLoader` has zero warmer subscriptions so its miss is permanent. The clincher is a
  negative — the `knownAbsent=True` escalation never appears. One subscription fixes it.
- **WO-1137 is worse than filed.** Bootstrap decides success on `loaded > 0` and the row loader
  silently `continue`s past rejects, so **1 good row out of 28 passes and prints "data-driven path
  is live."** The fallback announces via `Debug.LogWarning`, which the capture harness drops
  (`BreakCaptureHarness.cs:250` keeps only Error/Exception plus `[Flow:` lines) — so on a device it
  reaches **neither** captured artifact. Needs owner ruling (a) fail loud vs (b) codegen.
- **WO-1055 — its oracle has never run.** `StructureOrientationOracle` is marked
  `regression-registry: standalone`, was never registered, and its own comment warns exactly that.
  **264/264 green says nothing about that tower.** RCA proven, no fix landed.
- **WO-1061 — green ≠ fixed.** The suite is behavioural and honest, but its fixture already passes,
  so `EQUIP_DRAWER_OK` reports the drawer working while the defect is live in the owner's build.
- **Gate-adjacency gap, unfiled:** WO-972's carve-out keys on `type == Wall`; `gate_stone` is
  `type: Gate`, so it still claims its measured mesh. Gate-beside-wall may still fail.

**Stale / canon-contradicting — all still marked READY, so a seat can pull them:**
`WO-467` designs *the exact perimeter carve removed as a P0 hero-confinement bug*;
`skr_store_design` and `skr_staking_and_seeker` spec an in-game SKR ledger that `PackStore.cs:320`
forbids **by name in its own header**; `WO-501` is the **gear shop**, not the Night Market;
`WO-905`, `WO-829`, `WO-513`, `ad_generator` all have expired premises.

> **The pattern under nearly all of it: the ticket body gets updated and the status line does not.**

---

## 5. OPEN OWNER DECISIONS

1. **dApp Store blocker — the purchase rail.** A compliant production build (no local-test defines)
   ships purchases **off**, while the listing declares "contains in-app purchases: Yes" and tells
   reviewers to press Buy. Flip the default for store builds, or strip every purchase claim.
2. **dApp Store blocker — the short description** is authored in two files and has drifted into two
   different strings, both over the 30-char ceiling. The ruling must **delete the losing copy**.
3. **dApp Store blockers 1 and 3:** Mainnet flip (owner-gated, written approval required) and the
   six missing listing images. Detail in `publishing/SUBMISSION_READY_2026-08-22.md`.
4. **Battle pass conflict.** The owner ruled KEEP on `BattlePassManager` because it was never used —
   but a **second, data-driven battle pass has since shipped and is live**, and
   `BattlePassService.cs:51-84` carries a self-declared conflict block saying this must not sit.
5. **WO-1137** (a) vs (b), above.
6. **WO-1134's remaining four questions** — what escalation moves, the sub-linear curve as a
   concrete pair, whether the ladder resets, whether 3 camps is the intended rotation.

**Settled this session:** raid crystals reset **daily** (owner: *"repeat raids same day would not
pay crystals"*). Ruling + implementation spec recorded in `WORK_ORDER_1134`. Code deliberately not
written — the spec names the trap (day-scoping `IsClaimed` would re-grant one-time progression
unlocks every day, because that flag carries two meanings at two call sites).

---

## 6. COMMITS THIS SESSION

```
38c963bb3  Record the owner ruling that raid crystals reset daily
a99969c45  Add a Realm Store door that does not require walking to the vendor
ea6e3c0ca  Stop board_build dying on its own console output
5f4188dc1  Make the dApp Store packet applyable: name the four blockers, rewrite What's New
7678cde62  Let the sanctioned ship chain forward owner-test scripting defines
9f26ad71e  Pause the world during transactions, wire Devnet purchase verification,
           fix two stale gate oracles
```

**Nothing was pushed.** Per protocol, push only after the owner felt-verifies or a regression
passes. Note `.githooks/pre-push` refuses a push whenever anything under `ServerData/` is newer than
`Builds/r2-parity.log` — run `tools\r2-ship.ps1` to clear a real block; there is deliberately no
override flag.

---

## 7. TWO GATE ORACLES WERE STALE — both failed correct code

Recorded because the failure shape recurs:

1. **`[suite-count]` fired "SUITE VANISHED" at a SURPLUS.** The hazard it guards — a suite throwing
   inside `Guard.Try`, emitting neither tag line nor failure — is *exclusively* a shortfall. A
   by-name reconciliation of all 262 registration call-sites confirmed every registered suite
   reported. Now shortfall-only. Residual, unticketed: a vanish **masked** by a co-occurring surplus
   still slips — the old `!=` did not catch that either, it just printed the wrong message.
2. **The MON oracle required the literal `contract.lamports`.** The canary rail is SKR, an SPL token
   at 9 decimals — there are no lamports on it. Repointed to pin the **comparison**
   (`=== String(contract.amountBaseUnits)`), which is stronger than the field-name check.

Both were oracles asserting a stale *address* rather than a real property. When a suite fails on
code you believe is correct, check what the oracle is actually pointed at before changing the code.
