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

## HANDOFF 2026-08-24 20:53 - clean-lane rework

### WO-1177 - blocked at mandatory migration-first step

1. **What landed:** **REFUSAL / NO CODE WRITTEN.** A clean current-head Batch 1 lane was created successfully.
2. **Where:** `D:\eoa-codex-batch1-clean`, branch `codex/batch1-clean`, created from shared head `9bda8e61c`.
3. **Not done:** the discount code was deliberately not started because the binding instruction says the production migration must run first. WO-1178 was not started because the lane is sequential.
4. **Mismatch / could not find:** this environment exposes neither `DATABASE_URL` nor a `psql` executable. No sanctioned alternative migration executor was found in the repo; `tools/schema-parity.mjs` also requires `DATABASE_URL` for live verification.
5. **Verification:** clean worktree status confirmed before work; migration and live parity **not run**. Required unblock: run `tmp/neon-migration-wo1177-discount.sql` against the intended production database and return its column/index/data-preservation evidence, or provide this lane an approved connection/executor.

### WO-917 Phase B - empty ability-slot affordance

1. **What landed:** empty ability slots now remain visible, render a dimmed `+`, clear stale icon/caption/count state, and show `Add a skill to activate` when tapped. Equipped slots retain the original `AbilityRequested(slot)` dispatch and cooldown/affordability behavior.
2. **Where:** `D:\eoa-codex-batch4-clean`, branch `codex/batch4-clean`, one-file diff: `Assets/_Modules/HUD/Kit/HudKitController.cs`.
3. **Not done:** Phase A dodge art was not touched; no second loadout UI or skill-tree routing was invented. No Unity gate, capture, commit, or push was run because those belong to the lead.
4. **Mismatch / could not find:** the ticket says empty slots are absent because `BuildActionSlot` disables its icon. Current code had evolved: combat empty medallions already remained visible, but `SetEmptyMedallion` explicitly blanked the face and disabled taps; non-medallion empty slots were hidden. The implementation corrects the current binding seam rather than the stale stated cause.
5. **Verification:** `git diff --check` clean; brace count **235/235**; NUL count **0**; exact diff inspected. Unity compile/regression/UI capture remain for the lead.

### New sequencing acknowledged

- Owner ruling received: Food SKU IDs **do rename**.
- WO-1177 -> WO-1163 is now **one sequential seat**. WO-1163 has not been started.
- WO-1163 must eventually move the server `USD_ANCHORS`, both canonical `packs.json` copies, and the quote test together under the mirror law.
- `blue_mine` is recorded follow-up art, not WO-1163 scope; no farm-prefab visual edit is authorized.

### Batch 5F / WO-978 regression slice - honest credit reporting oracle

1. **What landed:** one new source-structural regression checks all four reward callers: each anchored reporting block contains the credit token, request token, and `FlowTrace.Warn`, while the caller source contains a before/credited measurement.
2. **Where:** `D:\eoa-codex-batch4-clean`, branch `codex/batch4-clean`; new files `Assets/Editor/Regression/EconomyCreditReportingRegression.cs` and `.meta` only for this slice.
3. **Not done:** no production economy behavior, player-facing presentation, crystal cap, or `DataRegression.cs` edit. The ticket's behavior/doc reconciliation remains blocked; only the explicitly unaffected regression slice was implemented. No gate, commit, or push was run.
4. **Mismatch / could not find:** the ticket says assert literal `requested`, but the existing Population reporter says `request`. The oracle pins the stable `request` stem rather than forcing cosmetic production-copy churn. Reporting helpers receive measured deltas from their callers, so before/after is asserted across the caller file, not incorrectly required inside each helper body.
5. **Verification:** `git diff --check` clean; all four anchors found and all six structural predicates true; regression source braces **12/12**, NUL count **0**. Lead registration owed in fenced `DataRegression.cs`: invoke `EconomyCreditReportingRegression.Run(out reason)` using the existing suite-registration pattern.

## HANDOFF 2026-08-24 21:07 - WO-1177 complete

### WO-1177 - server-issued seven-day shortfall discount

1. **What landed:** the quote endpoint accepts an untrusted `repair_shortfall` hint; the server checks prior discounted issuance, applies **2000 bps** before `quoteAmount`, persists nullable `discount_bps` plus the server-authored `discount_reason`, and returns nullable `discountBps` plus server-authored display copy. Discount issuance uses a `Serializable` conditional-insert transaction so simultaneous empty-window reads cannot both commit. A losing or in-window request receives no second discount. `/verify` reads the persisted discount audit fields while continuing to compare the finalized chain transfer against the persisted discounted base-unit amount. The client sends only context and displays server copy; it performs no price/percentage arithmetic and still reaches exactly one wallet prompt.
2. **Where:** `D:\eoa-codex-batch1-clean`, branch `codex/batch1-clean`; six-file explicit-path diff: `api/_lib/purchase-catalog.js`, `api/purchases/quote.js`, `api/purchases/verify.js`, `test/purchases.quote.test.js`, `Assets/_Modules/Wallet/PurchaseQuoteService.cs`, and `Assets/_Modules/Wallet/PackStore.cs`.
3. **Not done:** no migration, deployment, gate, commit, or push. The lead must hold this unmerged/undeployed until the owner runs `tmp/neon-migration-wo1177-discount.sql` and confirms the declared nullable columns/index. No WorkOrder status was flipped because Batch 6 is the sole current owner of `WorkOrders/*.md`.
4. **Mismatch / could not find:** `PackStore.FocusShortfall` is generic across repair/build/upgrade resource gaps; there is no repair-specific purchase caller or durable shortfall-origin enum. The lane therefore sends the ruled `repair_shortfall` hint only when buying the impulse pack matching the live focused shortfall. This is safe because the hint is never authorization, but the audit label is broader than its name. No approved database executor is available in this lane, as previously reported.
5. **Verification:** `node --check` clean for all three changed JS runtime files; `node --test test/purchases.quote.test.js test/purchases.verify.test.js` with shared `NODE_PATH` **43/43 passing**; `git diff --check` clean; C# braces `PurchaseQuoteService.cs` **83/83**, `PackStore.cs` **286/286**; both NUL **0**; client discount-arithmetic scan found no discount multiplier/percentage computation.

**Sequencing release:** WO-1177 is handed back. Per the owner ruling, WO-1163 may begin only after the lead accepts this handback; its SKU rename must move `USD_ANCHORS`, both canonical `packs.json` copies, and the quote-test resource-key list together.

## HANDOFF 2026-08-24 21:20 - WO-1178 complete

### WO-1178 - editor downgrade detection and marker-proof gate runners

1. **What landed:** one reusable pre-launch assertion now refuses a mismatched `ProjectVersion.txt` or a silently selected fallback editor before Unity starts; all seven general Unity launch paths call it. The pre-push hook refuses any project editor version other than the 6000.4.8f1 branch pin and names the rebuild/marker risk. The check-in gate now passes explicit `COMPILE_GATE_OK`, `REGRESSION_OK`, and `CHECKIN_SUITE_OK` expectations into the existing fresh-log evidence gate. `install-apk-to-seeker.ps1` now uses the pinned editor/SDK and routes its build through `run-unity-method.ps1` with the `[AndroidBuild] SUCCEEDED` marker.
2. **Where:** `D:\eoa-codex-batch1-clean`, branch `codex/batch1-clean`; WO-1178 explicit paths are `.githooks/pre-push`, new `tools/assert-unity-editor-pin.ps1`, `run-unity-method.ps1`, `run-tests.ps1`, `build-windows.ps1`, `build-webgl.ps1`, `build-webgl-isolated.ps1`, `install-apk-to-seeker.ps1`, `tools/run-unity-playmode.ps1`, and `tools/regression/checkin_gate.ps1`.
3. **Not done:** no Unity process, gate, build, push, commit, or WorkOrder status edit was performed. The repo cannot prevent an arbitrary external raw editor launch; it now detects the resulting metadata mismatch before sanctioned launches and at push, matching the revised ruling.
4. **Mismatch / could not find:** the ticket says all six repo scripts pin 6000.4.8f1. That was false: a seventh direct launcher, `install-apk-to-seeker.ps1`, hard-coded both Unity and Android SDK to **6000.4.7f1** and invoked Unity directly. It exactly matches the unexplained 4.8 -> 4.7 rewrite and is the strongest source-level cause found. Also, the nominally pinned general launchers silently fell back to another installed editor when 4.8 was absent; the assertion makes that fallback fail closed.
5. **Verification:** PowerShell parser clean for all nine changed/new `.ps1` files; hook shell syntax clean after stripping the repository's CRLF for Bash parsing; `git diff --check` clean. Deliberate no-Unity proof: pin mismatch printed `UNITY_EDITOR_PIN_MISMATCH` and exited **9**; judge-only marker test printed `VERDICT=FAIL reason=LOG_MISSING` for missing `COMPILE_GATE_OK` and exited **8**. Script scan found no remaining `6000.4.7f1` pin in any `.ps1`. Unity/runtime gate remains the lead's.

**Deployment evidence received while this ticket was in progress:** the WO-1177 migration is now verified live with all four columns present and the existing 391-SKR quote intact, so its schema-first deployment hold is cleared. Production still has only one historical quote row; the next real quote is the first repetition of that rail and should be treated as evidence, not assumed from deployment success.

## HANDOFF 2026-08-24 21:34 - UI panels WO-1075 through WO-1078

### WO-1075 - Raid deploy actions keep canonical touch height

1. **What landed:** `Army Ready?` and `BEGIN ASSAULT` are vertically centred at the fixed `CanonCtaHeight` instead of taking 90% of an aspect-dependent footer. Both therefore author at 132 ref px, above the 112 px floor.
2. **Where:** `D:\eoa-codex-ui-panels`, branch `codex/ui-panels-1075-1078`; one-file diff `Assets/_Modules/Village/Hero/RaidDeployScreen.cs`.
3. **Not done:** readiness/deployability logic, glow geometry, the allow-list, oracle, gate, capture, commit and push were untouched.
4. **Mismatch / could not find:** none in the current seam; the two fractional button bands were still present exactly as ticketed.
5. **Verification:** `git diff --check` clean; fixed-height structural check true; braces **41/41**, NUL **0**. Fresh resolved capture numbers remain for the lead's Unity gate.

### WO-1076 - Rumor Board Close reserve

1. **What landed:** **REFUSAL / ALREADY SHIPPED.** No new diff. Current source already computes the detail pane floor from the shared Close's measured anchor plus canonical pixel height, then grows upward rather than back into that reserved band.
2. **Where:** shared source commit `a2162f17d` (`fix(ui): RumorBoard + RealmMap stop overlapping the Close...`); inspected in `D:\eoa-codex-ui-panels` at `Assets/_Modules/Village/Hero/RumorBoardPanel.cs`.
3. **Not done:** no duplicate geometry change, allow-list, oracle, gate, capture, commit or push.
4. **Mismatch / could not find:** the ticket's proposed fix already exists as `CloseReserveTopFraction`; the ticket was minted from the older `Builds/wo1060-capture.log`, not the current source state.
5. **Verification:** source reserve call and implementation found; braces **78/78**, NUL **0**. The lead should rerun the capture to reconcile the stale 18 findings before changing this file again.

### WO-1077 - EndState Repair-All excluded from dismiss catcher

1. **What landed:** banners with a Repair-All CTA now parent `TapDismiss` to the report well instead of the full chrome root. Tap-dismiss remains over the report, while the separately owned CTA band is outside its geometry; banners without a CTA retain the original whole-panel catcher.
2. **Where:** `D:\eoa-codex-ui-panels`, branch `codex/ui-panels-1075-1078`; one-file diff `Assets/_Modules/Village/UI/EndState/EndStateView.cs`.
3. **Not done:** no `LayoutOracle` exclusion, allow-list change, CTA action/price logic, gate, capture, commit or push. This takes ticket path (a), as explicitly ruled.
4. **Mismatch / could not find:** source proved the prior safety claim was z-order only (`SetAsFirstSibling`), which cannot satisfy the geometric oracle. The catcher is now separated geometrically instead.
5. **Verification:** `git diff --check` clean; Repair-All parent structural check true; braces **173/173**, NUL **0**. Raycast winner and resolved rectangles remain for the lead's capture.

### WO-1078 - dialogue choices no longer covered by TapAdvance

1. **What landed:** the redundant body-sized `TapAdvance` overlay is removed. Tap-to-advance remains on the prose scroll viewport through its existing `vpBtn.onClick -> OnBoxTapped`; `ResizeToContent` already ends that viewport above the separately owned options band.
2. **Where:** `D:\eoa-codex-ui-panels`, branch `codex/ui-panels-1075-1078`; one-file diff `Assets/_Modules/HUD/DialogueView.cs`.
3. **Not done:** no `LayoutOracle` exclusion, allow-list change, option-row geometry, Close ordering, gate, capture, commit or push. This takes ticket path (a), as explicitly ruled.
4. **Mismatch / could not find:** the ticket described shrinking `TapAdvance`, but current source already had a second, correctly scoped prose-viewport button invoking the same handler. Keeping both created two controls for one action; removing the redundant overlay preserves behavior with the smaller blast radius.
5. **Verification:** `git diff --check` clean; no `new GameObject("TapAdvance"` remains; viewport handler check true; braces **109/109**, NUL **0**. Option raycast winner and resolved rectangles remain for the lead's capture.

**Batch note:** WO-1163 remains isolated in `D:\eoa-codex-1163`; none of its files were touched. The four UI tickets do not modify `LayoutOracle.cs`, `UICaptureLaunch.cs`, `ElarionUiKit.cs`, or any WorkOrder status line.
