# Batch Results State - dev lane to lead

**Recorded:** 2026-08-24  
**Source read first:** `BATCH_STATE.md` at `cab4d8a12`  
**Delivery:** owner-carried inbound handback; do not infer that the lead has received it until the owner relays it.

## Batch 1

### WO-1069 - dominated shortfall pack removal

1. **What landed:** `hearth-spark` moved to the ruled **$4.99** anchor; catalog mirrors agree; the domination regression was added.
2. **Where:** shared tree, commit `6bb61a810`.
3. **Not done:** this result does **not** complete WO-1177 or WO-1178.
4. **Mismatch / could not find:** no additional mismatch is asserted here; this is the only Batch 1 implementation presently proven landed in the shared tree.
5. **Verification:** focused purchase-quote suite reported **26/26** in the reviewed handback. The current worktree was not re-gated for this state-file write.

### WO-1177 - server-issued shortfall discount

1. **What landed:** **NO COMPLETION CLAIM.** Migration preparation exists, but the active implementation is not proven landed.
2. **Where:** migration staged at `tmp/neon-migration-wo1177-discount.sql`; intended implementation lane is `D:\eoa-codex-ready`.
3. **Not done:** do not deploy the code before the migration; do not mark FIXED; do not release Batch 5 from this dependency on this handback.
4. **Mismatch / could not find:** the inspected `D:\eoa-codex-ready` worktree contains unrelated uncommitted siege/notification and board-parser changes, not a clean WO-1177 handback. No landed WO-1177 commit was found in the shared-tree recent history.
5. **Verification:** no qualifying WO-1177 verification result is recorded. Required evidence remains migration execution plus focused quote tests and syntax checks.

### WO-1178 - Unity editor pin

1. **What landed:** **NO COMPLETION CLAIM.** The ticket remains available after WO-1177 in the active Batch 1 sequence.
2. **Where:** intended lane `D:\eoa-codex-ready`; no landed shared-tree commit identified.
3. **Not done:** raw `Unity.exe` invocation cannot be made impossible; implement only the bounded wrapper/editor-pin contract in the ticket.
4. **Mismatch / could not find:** no isolated WO-1178 diff or verification handback was found in the inspected lane.
5. **Verification:** none recorded.

## Batch 4

### WO-1161 - structure role table

1. **What landed:** **REFUSAL / ALREADY SHIPPED.** Both canonical copies were previously verified byte-identical.
2. **Where:** shared tree; existing implementation dated 2026-08-23.
3. **Not done:** no edit was made, because the active state explicitly says this ticket needs none.
4. **Mismatch / could not find:** the earlier batch description treated it as implementation work although the current tree already satisfies it.
5. **Verification:** prior byte-identity check is the recorded evidence; no new gate was run for this handback.

### WO-1163 - Food to Stone resource ladder

1. **What landed:** **NO COMPLETION CLAIM.** The ticket remains open.
2. **Where:** intended Batch 4 lane `D:\eoa-codex-batch4`.
3. **Not done:** the paid food-SKU remap is correctly deferred until Batch 1 returns. Frozen IDs `collector_farm` and `silo` must not be renamed; display spelling is **Stoneyard**.
4. **Mismatch / could not find:** the inspected Batch 4 worktree currently has uncommitted edits in `api/_lib/purchase-catalog.js` and `test/purchases.quote.test.js`, which the binding WO-1163 pin forbids Batch 4 from touching. Its branch is named `codex/batch1`, so provenance and ownership must be reconciled before any commit.
5. **Verification:** no qualifying WO-1163 verification result is recorded.

### WO-917 Phase B - empty ability-slot affordance

1. **What landed:** **NO COMPLETION CLAIM.** Phase B remains open.
2. **Where:** intended Batch 4 lane `D:\eoa-codex-batch4`.
3. **Not done:** Phase A is outside this batch; no owner-art choice is implied.
4. **Mismatch / could not find:** no isolated Phase B diff or verification evidence was found in the inspected worktree.
5. **Verification:** none recorded.

### WO-1179 - roaming horde core

1. **What landed:** **NO CORE COMPLETION CLAIM.** The design is ruled: one encounter, one global cap, composition partitioned across active sides, side escalation 1 -> 2 -> 4, and away time banks pressure.
2. **Where:** ticket/spec is in the shared history; no proven core implementation commit was identified. Notification presentation exists as separate WO-1184 work and must not be counted here.
3. **Not done:** do not call `SpawnWave` once per side; do not use `Gate.ForceFieldCollapsed`; do not use WO-1026's disabled ring detector. The detailed Ready audit also found WO-513 is a stated prerequisite if roaming packs must inherit coordinated-family behavior, so that dependency needs lead reconciliation before implementation is called handable.
4. **Mismatch / could not find:** `D:\eoa-codex-ready` contains uncommitted roaming-horde notification files, but those are WO-1184 presentation, not WO-1179 core. No core partitioning handback or verification was found.
5. **Verification:** none recorded for the core.

## Cross-batch result

- **Batch 5 remains held.** WO-1177 and WO-1163 are not proven complete by this handback.
- **Do not commit either inspected Codex worktree as-is.** Their names, active assignment, and modified files do not agree with `BATCH_STATE.md`; reconcile provenance by explicit path first.
- **`D:\eoa-codex-six` remains quarantined.** This handback does not authorize rebasing, committing, or deleting it.
- The separate Ready-queue audit is recorded in `READY_FOR_REVIEW.md`; it is classification evidence, not implementation completion.

## REWORK REQUEST - return to dev lane before any commit

**Disposition:** **WRONG / INCOMPLETE - ANNOTATED AND SENT BACK.** Do not repair these diffs in the lead lane. Rework from the current shared-tree head by explicit path, leaving the existing dirty worktrees intact until provenance is settled.

### R1 - `D:\eoa-codex-batch4` is not Batch 4 work

- **Risk:** **CRITICAL.** The worktree is 28 commits behind the inspected shared head, its branch is named `codex/batch1`, and all four dirty files are Batch 1 / WO-1069 files:
  - `Assets/Resources/Data/Canonical/packs.json`
  - `Assets/StreamingAssets/Data/Canonical/packs.json`
  - `api/_lib/purchase-catalog.js`
  - `test/purchases.quote.test.js`
- **Why unsafe:** the diff is the already-landed WO-1069 `$4.99 hearth-spark` change and domination test. Committing it from this stale branch would duplicate landed work and places forbidden `purchase-catalog.js` / quote-test edits inside the WO-1163 lane.
- **Send-back instruction:** do not commit, merge, rebase, or reinterpret this diff as WO-1163. Recreate Batch 4 from the current shared head in a correctly named clean worktree, then implement only WO-1163's non-purchase-catalog slice, WO-917 Phase B, and the ruled WO-1179 core. Preserve this old worktree for provenance until the lead explicitly clears it.
- **Acceptance to return:** clean base at current shared head; no diff in `api/_lib/purchase-catalog.js` or `test/purchases.quote.test.js`; each ticket returned as an isolated explicit-path diff with its own five-field handback.

### R2 - `D:\eoa-codex-ready` mixes unrelated and already-landed scopes

- **Risk:** **CRITICAL.** The worktree is 29 commits behind and is assigned to WO-1177/1178, but its dirty files implement WO-1184 lookout presentation and WO-1180/1181 board parsing instead:
  - Village `.asmdef`, `SiegeClock.cs`, `AlertIntelSystem.cs`, `SiegeScheduler.cs`
  - new `RoamingHordeNotifications.cs` plus `.meta`
  - `tools/board_build.py`
- **Why unsafe:** WO-1180/1181 are already landed in the shared tree, while WO-1184 is orthogonal to WO-1179 and unrelated to Batch 1. The `.asmdef` edit changes the Unity dependency graph. A single commit from this lane would combine three ownership domains and still contain no demonstrated WO-1177 or WO-1178 implementation.
- **Send-back instruction:** do not commit this mixed tree. Recreate WO-1177/1178 from the current shared head in a clean Batch 1 worktree. Keep WO-1177 first and apply/run its migration before any code deployment; return WO-1178 separately after WO-1177. Route the WO-1184 files as their own explicit-path handback for source review and Unity gating. Drop no files and perform no destructive cleanup until the lead confirms provenance.
- **Acceptance to return:** WO-1177 diff limited to its ticketed backend/schema/test paths with migration and focused test evidence; WO-1178 diff limited to the bounded tools/editor-pin paths; WO-1184 isolated from both; no stale `board_build.py` copy.

### R3 - WO-1179 dependency contradiction must be resolved before code

- **Risk:** **HIGH.** `BATCH_STATE.md` marks WO-1179 core active, while the audited ticket states WO-513 is a prerequisite if roaming packs must inherit coordinated-family behavior.
- **Why unsafe:** implementing the side partition now may either bypass the required family behavior or accidentally absorb WO-513 into WO-1179, expanding scope without authorization.
- **Send-back instruction:** return a refusal until the lead rules one of two bounded paths: (a) WO-1179 may ship side partitioning without WO-513 behavior, with that limitation explicit; or (b) WO-513 lands first and WO-1179 consumes its seam. Do not guess.
- **Acceptance to return:** the lead's dependency ruling cited in the handback, then one `SpawnWave` call, one shared concurrency budget, deterministic 1 -> 2 -> 4 side partition, no `Gate.ForceFieldCollapsed`, and no disabled ring-detector dependency.

### R4 - quarantined lane remains non-work

- **Risk:** **CRITICAL / DESTRUCTIVE.** `D:\eoa-codex-six` is 180 commits behind the inspected shared head and carries large dirty scene/content deltas.
- **Send-back instruction:** no rebase, cleanup, commit, or deletion. It is not a source for rework. Only an explicit provenance-recovery task from the lead may touch it.

## Rework priority

1. Recreate a clean Batch 1 lane and return WO-1177, then WO-1178.
2. Recreate a clean Batch 4 lane and return WO-1163's allowed slice and WO-917 Phase B independently.
3. Hold WO-1179 core for the WO-513 dependency ruling.
4. Route WO-1184 as a standalone review; do not count it toward Batch 1 or WO-1179.

**Batch 5 remains held** until clean, verified WO-1177 and WO-1163 handbacks are accepted by the lead.
