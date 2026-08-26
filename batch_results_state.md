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

## HANDOFF 2026-08-24 21:43 - WO-1163 implementation blocker found at source

### WO-1163 - ruled ladder cannot be represented by the live tier contract

1. **What landed:** **REFUSAL / NO CODE OR DATA EDIT YET.** The persistence-safe approach is confirmed: the player-facing Stone economy must continue using the existing `Resources.Food` wire slot because `GameState.Stone` already exists as a separate persisted legacy balance. Renaming or merging those fields would corrupt value rather than preserve it 1:1.
2. **Where:** investigation in clean isolated lane `D:\eoa-codex-1163`, branch `codex/wo1163`, based on shared commit `54a8b5a4f`.
3. **Not done:** no SKU rename, canonical mirror rewrite, generated fallback, tier repricing, troop repricing, schema bump, gate, commit or push. A partial catalog rename would make the mirror/pay path inconsistent and is therefore not a safe staging step.
4. **Mismatch / could not find:** the ruled tier basket is **L1 wood + gold / L2 stone + gold / L3 iron + gold**, but `BuildingTierDef` (`Assets/_Modules/Core/State/BuildingTierCatalog.cs`) exposes only `CostWood`, `CostFood`, and `CostCrystal`. `BuildingUpgradeService`, `BuildingUpgradeVM`, and `ManageScreenVM` likewise build upgrade costs from only those three fields. There is no `costIron`, `costGold`/`costCoins`, or conversion table in the ticket. The current 26 tier rows contain Wood/Food/Crystal values, so adding Iron/Gold also requires a data-contract and consumer expansion plus exact ruled amounts. Deriving Gold by summing or splitting old materials would invent an exchange rate and materially rebalance the live economy. The ticket's statement that §6 is fully answered therefore does not make §2 representable.
5. **Verification:** clean lane status confirmed; source searches found zero tier `costIron`, `costGold`, or `costCoins` fields/usages; live DTO and all three cost consumers inspected. No Unity gate was run.

**Required send-back ruling/spec pass:** provide either (a) exact Wood/Stone/Iron/Gold amounts for every reachable tier, together with authorization to add `costIron` + `costCoins` to the tier DTO/three consumers, or (b) a deterministic conversion rule from each existing row's Wood/Food/Crystal numbers into the new depth-resource + Gold basket. The Food-wire-slot alias, frozen building ids, `Stoneyard` spelling, and stone SKU-id rename are already clear and need no further ruling.

## HANDOFF 2026-08-25 - Batch 8 intake findings returned to CLI lead

### Fresh-addition triage - do not reassign until reconciled

1. **WO-1138 mismatch:** `BATCH_STATE.md` offers WO-1138 as new implementation work, but `WorkOrders/WORK_ORDER_1138_hollow_pass_ratchet.md` is already `FIXED — AWAITING OWNER FELT-TEST TO CLOSE` and says the control-flow walk, known-site control, 27-site triage, and green gate already landed. **CLI action:** reconcile the batch row against the canonical ticket and current source; return only a specifically named residual if one exists.
2. **WO-1137 mismatch:** `BATCH_STATE.md` offers WO-1137 as new implementation work, but `WorkOrders/WORK_ORDER_1137_fallback_catalog.md` is already `FIXED 2026-08-23 (84b9b987b) — AWAITING OWNER CLOSE` and says the generated 28-row fallback plus freshness gate landed. **CLI action:** remove/reconcile the stale assignment unless a current-tree regression is cited.
3. **`usdEffective` ownership collision:** the new row owns `api/purchases/quote.js`, which is already modified in the shared worktree; `PurchaseQuoteService.cs` and `PackStore.cs` are also dirty on the same price-display path. Starting from this seat would risk mixing an unattributed active diff into the new server-field change. **CLI action:** identify the current owner and either finish/return that work or provide a clean, explicit-path handoff.
4. **Spaced `X OK` ownership collision:** the new row owns `Assets/Editor/Regression/RegressionMarkerRegression.cs`, already modified in the shared worktree. **CLI action:** attribute and settle that diff before reassignment; include the exact intended spaced-marker grammar so the previously measured 24 prose collisions are not rediscovered by trial and error.
5. **Dungeon fleet proof blocked by stale player:** `tools/verify-dungeons.ps1` now parses and discovers eight player dungeons, but the only Windows player is `Builds/Windows/DefendersOfTheRealm.exe`, timestamped **2026-08-23 14:50**, predating the 2026-08-25 changes. The script's own contract says a stale player cannot prove today's composer output. The shared tree also contains active uncommitted WO-1191/purchase/regression work, so building it now would snapshot incomplete cross-seat changes. **CLI action:** settle the active dirty work, produce a fresh gated Windows build, then reassign the real eight-dungeon fleet run.

**Dev-lane disposition:** no Batch 8 implementation was started and no outbound `BATCH_STATE.md` text was changed. These findings are returned here so the CLI lead can correct ownership/state and resolve prerequisites before asking this seat again.

## FOLLOW-UP 2026-08-25 - 07:41 correction reviewed

The new ownership-attribution section resolves findings 3 and 4 procedurally: the dirty purchase and regression files now have named lead-side owners, and the collision rule correctly holds those lanes until commit. Two assignment defects remain unresolved:

1. **WO-1137 and WO-1138 are still listed under `AVAILABLE NOW`.** The correction explains file ownership but does not reconcile either row with its canonical ticket's existing `FIXED` status and landed implementation evidence. Calling WO-1137 “safe to assign immediately” does not answer the earlier stale-assignment finding. **CLI action still required:** remove both rows, or name a concrete residual absent from the current tree.
2. **The dungeon lane is file-disjoint but not evidence-ready.** Calling it safe to assign does not cure the stale-player condition: the only Windows executable remains dated 2026-08-23, while the script explicitly rejects stale builds as proof of current composer output. **CLI action still required:** after the active dirty changes are committed/gated, provide a fresh Windows build (or authorize that build from a clean settled tree), then assign the eight-dungeon run.

**Disposition remains refusal on these residuals.** No implementation or stale-build fleet run was started.

## HANDOFF 2026-08-25 - completed-worktree cleanup ledger

Owner authorized cleanup of old completed Codex worktrees. Each target was checked before removal: its branch had **zero commits not already contained by** `wip/village2-and-f8-tickets`; for dirty completed lanes, every status-path file was SHA-256 compared with the corresponding main-tree file.

### Removed - completed work already landed

- **WO-917 Phase B:** removed `D:\eoa-codex-batch4-clean` and deleted local branch `codex/batch4-clean`. Its `HudKitController.cs` handback was already landed; the worktree copy matched the main tree byte-for-byte.
- **WO-978 regression slice:** removed with the same `batch4-clean` tree. `EconomyCreditReportingRegression.cs` and `.meta` matched the landed main-tree copies byte-for-byte.
- **WO-1075:** removed `D:\eoa-codex-ui-panels` and deleted local branch `codex/ui-panels-1075-1078`. `RaidDeployScreen.cs` matched main byte-for-byte.
- **WO-1076:** the same UI-panel tree was removed. This ticket had correctly returned **already shipped / no implementation diff**, so no unique work was discarded.
- **WO-1077:** the same UI-panel tree was removed. `EndStateView.cs` matched main byte-for-byte.
- **WO-1078:** the same UI-panel tree was removed. `DialogueView.cs` matched main byte-for-byte.
- **UI cohesion lane:** removed clean `D:\eoa-ui-cohesion` and deleted local branch `codex/ui-cohesion`; the branch had zero commits ahead of main and no working-tree changes. **Ticket mapping could not be found in `BATCH_STATE.md`, `batch_results_state.md`, `READY_FOR_REVIEW.md`, `CODEX_HANDOFF.md`, or `WorkOrders/`; CLI should attach the ticket id if this lane needs a per-ticket historical note.**

### Preserved - not proven safe to delete

- **WO-1163:** kept `D:\eoa-codex-1163` / `codex/wo1163`; the lane is blocked on the tier-basket ruling, not completed.
- **WO-1177 and WO-1178:** kept `D:\eoa-codex-batch1-clean` / `codex/batch1-clean`; 11 status-path files no longer match the current main tree, so deletion would discard non-identical provenance even though the original handbacks landed.
- **WO-1069:** kept stale/misnamed `D:\eoa-codex-batch4` / `codex/batch1`; its four status-path files differ from current main. Prior handoff says the intended work landed as `6bb61a810`, but byte identity is no longer true, so cleanup was refused pending provenance disposition.
- **WO-1184 plus WO-1180/WO-1181 remnants:** kept `D:\eoa-codex-ready` / `codex/wo-1069-1177-1178`; `tools/board_build.py` and `CODEX_READY_FOR_REVIEW.md` differ from or are absent in main. Prior handoff says the Village/Siege work was harvested, but the residuals are not safe to destroy silently.
- **Batch 6 ticket set:** kept `D:\eoa-codex-six` / `codex/wo-six` under the existing quarantine. No rebase, cleanup, commit, or deletion was attempted.
- **Dungeon bake / WO-740 family:** kept `D:\eoa-dungeon-bake`. It shares the active main branch and carries extensive staged state; it was not treated as a completed disposable Codex lane.

**Recovery note:** removed worktree contents were not uniquely recoverable from those folders, but all ticketed files were first proven present identically in the main tree. The deleted local branch tips remain reachable through Git reflogs until normal expiry.

## HANDOFF 2026-08-25 - Batch 9 read-only intake review

Owner's standing ownership rule now supersedes implementation routing: **Claude owns all actions. Codex performs read-only inspection and writes only this results handback.** No Batch 9 code, generated asset, migration, test, build, branch, or ticket was changed by Codex.

### Issues returned for Claude to resolve before action

1. **Batch ownership is wrong:** the section title says `READY TO HAND TO CODEX` and presents six implementation lanes. Under the owner's explicit rule, these must be routed to Claude. Codex does not accept action ownership for any of the six.
2. **WO-1173 is underspecified against its own acceptance:** Batch 9 describes “wire into ONE ship-chain script + create `api/migrations/`” with `schema-parity.mjs` read-only. The ticket additionally requires (a) `SCHEMA_PARITY_OK` against production, (b) a deliberately narrowed CHECK/dropped column proven RED in a scratch DB, (c) blocking pre-ship wiring for anything reaching a device/store, and (d) execution after every production API deploy and schema edit. One unnamed chain is not enough to demonstrate those trigger surfaces, and the DB-dependent RED/production proofs require an authorized executor. Claude should name the exact chain and verification authority before treating this as complete scope.
3. **WO-1170 “three independent lanes” needs an ownership split for generated infrastructure:** sites 2 and 3 explicitly add a generator plus `.g.cs`; site 6 must choose delete-vs-codegen under WO-1170 §5 and may also require a generator/hash-parity suite. The statement that the three sites are “one file each” is false as written, and the ticket's standing-oracle acceptance can create a shared regression/registration surface. Claude should assign distinct generator/output paths and one owner for any shared parity registration before parallel edits.
4. **WO-1171 §4 file scope is broader than the work:** the ticket says the disconnect mechanism and Core seam are finished; only player-facing placement remains. Batch 9 nevertheless grants `Wallet/*`, which could authorize edits to the already-finished mechanism. The implementation scope should be the Settings host plus calls through `CurrencySkinResolver`; Wallet files should be read-only unless Claude finds and records a specific missing seam.
5. **PROD-014(b) acceptance requires headed evidence:** the slice is correctly limited to acknowledge/exit, but its ticket still requires complete rendering at 2670x1200 and the narrowest supported width, with captured PNGs opened. Source wiring and compile/regression alone cannot close it. Claude should retain the headed capture/felt-verification step in the lane acceptance.
6. **Older live-state text still contradicts Batch 9:** later sections in the same `BATCH_STATE.md` continue to list WO-1173 as held for a spec pass and older Batch 1/4/7 work as ACTIVE. The protocol says “if a section says something, it is current,” while Batch 9 does not explicitly supersede all older ACTIVE/HELD blocks. Claude should mark the older blocks historical or state an unambiguous precedence rule before using this as an action board.

**Disposition:** review returned; zero implementation started. Once Claude corrects routing and scope, Claude owns execution and Codex remains the verification/reporting seat.

## HANDOFF 2026-08-25 - Batch 9 implementation returns (wave 1)

Ownership correction received: Codex writes implementation; Claude specs, verifies, gates and commits. The following are isolated, uncommitted worktree returns.

### WO-1170 site 3 - stake reward fallback generated from JSON

1. **What landed:** removed the hand-maintained `DefaultTiers()` reward table. A read/parse failure now consumes a generated byte-exact copy of `stake-rewards.json`; a generated-copy parse failure emits `FlowTrace.Fail` and returns an empty safe ladder instead of inventing reward values. Added a deterministic editor generator that verifies the Resources and StreamingAssets copies are byte-identical, validates non-empty tiers, embeds exact bytes, records SHA-256/length, and emits `STAKE_REWARDS_FALLBACK_GEN_OK`.
2. **Where:** `D:\eoa-codex-b9-1170s3`, branch `codex/b9-1170s3`. Explicit paths: `Assets/_Modules/Core/Platform/StakeRewardsResolver.cs`; new `Assets/Editor/StakeRewardsFallbackGenerator.cs` + `.meta`; new `Assets/_Modules/Core/Platform/Generated.meta`; new `Assets/_Modules/Core/Platform/Generated/StakeRewardsFallbackData.g.cs` + `.meta`.
3. **Not done:** no `DataRegression.cs` or shared parity-oracle edit (Batch 9 assigns that shared surface to site 2); no Unity generator invocation, compile gate, regression, commit or ticket flip.
4. **Mismatch / could not find:** both canonical JSON copies still contain an authored `_note` saying to keep `DefaultTiers` in sync. They were left read-only because the lane grant did not include canonical JSON, but that note becomes stale when this lands and should be corrected by the committer in both mirror copies, followed by regeneration because changing the note changes the embedded hash/bytes.
5. **Verification:** canonical copies currently hash identically (`09e1f44287fa27a4d1e65acac7c40af42e34d6934e3ed858a2d744f04e12b105`, 2444 bytes); `git diff --check` clean; braces resolver **69/69**, generator **19/19**, generated output **2/2**; NUL **0**. Worktree has no generated `.csproj`, so Unity compile/generator proof remains Claude-owned.

### WO-1171 section 4 - player-facing Settings wallet home

1. **What landed:** Settings now has a dedicated Wallet section with explicit `Connect Wallet` and `Disconnect Wallet` controls. Both remain visible so both capabilities are discoverable; enabled state and disconnect label carry the connected state in words. The row subscribes to `WalletConnectionChanged` and refreshes on open and on state changes. Required ladder height increased for the authored row.
2. **Where:** `D:\eoa-codex-b9-1171`, branch `codex/b9-1171`; one-file diff `Assets/_Modules/Settings/SettingsController.cs`.
3. **Not done:** no Wallet file was edited. No mechanism, provider, session store, confirm policy, compile, capture, regression, commit or ticket status was changed.
4. **Mismatch / could not find:** none. The existing Core seam was sufficient; both callbacks route only through `CurrencySkinResolver.RequestWalletConnect/Disconnect` as required.
5. **Verification:** `git diff --check` clean; one modified file only. Claude still owes Unity compile/regression and owner flow proof: disconnect -> relaunch -> Connect -> reconnect -> store prices.

### PROD-014 slice (b) - refused repair acknowledge / exit

1. **What landed:** the unaffordable Hub Repair state now shows the shared labeled `ObsidianCloseButton`. Acknowledge routes through the existing `WallRepairController.CancelRepair()`, clearing selection and marker, then hides the surface. The exact shortfall is latched so the 0.75-second refresh cannot immediately reopen the refusal; a changed wallet/shortfall makes it eligible to surface again. Affordable repair behaviour is unchanged.
2. **Where:** `D:\eoa-codex-b9-prod014b`, branch `codex/b9-prod014b`; one-file diff `Assets/_Modules/Village/Walls/HubRepairAffordance.cs`.
3. **Not done:** slices (c)/(d), pack offers, crystal pricing, economy code, `WallRepairHudBridge`, and `WallRepairController` were untouched. No compile, headed capture, commit, or ticket flip.
4. **Mismatch / could not find:** the three-file batch grant was broader than necessary; the existing public `CancelRepair()` seam made this a one-file presentation change.
5. **Verification:** `git diff --check` clean; braces **78/78**, NUL **0**. Acceptance still requires Claude's headed capture at **2670x1200 and the narrowest supported width**, with PNGs opened; compile/regression alone cannot close it.

## HANDOFF 2026-08-25 - Batch 9 implementation returns (wave 2 / scope stops)

### WO-1170 site 6 - delete-vs-codegen decision cannot be made from the offered contract

1. **What landed:** **NO CODE EDIT.** Source inspection completed the required pre-write decision pass, but neither sanctioned outcome is currently specified safely.
2. **Where:** inspected `Assets/_Modules/Village/Enemies/Enemy.cs`, `Assets/_Modules/Village/Enemies/EnemyTypeVfxLibrary.cs`, `Assets/_Modules/Village/Enemies/EnemyTypeVfxSet.cs`, and `WorkOrders/WORK_ORDER_1170_json_is_the_only_source.md` in the main tree.
3. **Not done:** no synthesized fallback deletion, generator, generated output, parity registration, compile, regression, commit, or ticket flip.
4. **Mismatch / could not find:** WO-1170 site 6 names only "VFX sets" as the duplicated source; it does not name a canonical JSON/input from which a `.g.cs` can be generated. The current synthesized rung supplies a non-null `EnemyTypeVfxSet` and preserves telegraph timing if the Resources asset is missing. Deleting it without a named flow-refusal boundary can change combat from a visible warning into missing/unsafe cues; generating it without a canonical input would merely rename a hand-authored fallback. **Required CLI action:** name the canonical source and choose CODEGEN, or explicitly authorize DELETE and define which enemy-spawn/combat flow must refuse visibly when the asset is absent.
5. **Verification:** confirmed the library resolution order is family Resources asset -> default Resources asset -> synthesized instance; confirmed the ticket's acceptance requires either generated hash parity or a loud refusal. No mutation was made.

### WO-1173 - corrected Batch 9 row is explicitly not executable yet

1. **What landed:** **NO CODE OR MIGRATION EDIT.** The current tree was inspected for candidate ship/deploy chains and the existing schema-parity tool.
2. **Where:** `tools/schema-parity.mjs` (read-only), `morning-ship-chain.ps1`, `overnight-webgl-deploy.ps1`, `overnight-apk-build.ps1`, `distribute-android.ps1`, and the existing repair SQL under `tmp/`.
3. **Not done:** no chain wiring, migration synthesis, production DB query, destructive scratch-DB RED proof, gate, commit, or ticket flip.
4. **Mismatch / could not find:** the newest Batch 9 pin itself says to name the exact chain and verification authority before treating the lane as scoped. The trigger surfaces are split: device/store work appears in `morning-ship-chain.ps1` and related Android scripts, while production API/WebGL deployment appears in `overnight-webgl-deploy.ps1`; the grant permits only **ONE** ship-chain script. `DATABASE_URL` is redacted, so this seat also cannot provide the required production `SCHEMA_PARITY_OK` or deliberately broken scratch-DB RED proof. **Required CLI action:** name the one writable chain (or widen the file grant to every required trigger surface), provide an authorized production/scratch DB executor, and identify which repair SQL is authoritative for the tracked migration.
5. **Verification:** `tools/schema-parity.mjs` exists and is not presently integrated into the inspected chains. Existing repair SQL is untracked operational material and was not promoted speculatively.

### WO-1170 site 2 - reserved shared-owner lane not partially returned

1. **What landed:** **NO IMPLEMENTATION EDIT.** A clean isolated worktree was reserved and the three-table `BuildFallback()` surface was confirmed.
2. **Where:** `D:\eoa-codex-b9-1170s2`, branch `codex/b9-1170s2`; intended source `Assets/_Modules/Village/Catalog/BuildCategoryRegistry.cs` plus distinct generator/output paths.
3. **Not done:** generator, `.g.cs`, removal of all three hand mirrors, shared parity/oracle registration for sites 2/3/6, deliberate RED proof, Unity gate, commit, and ticket flip.
4. **Mismatch / could not find:** none beyond the already-recorded Batch 9 correction that this lane owns the shared parity surface. Because that shared owner must be complete and collision-free, no partial source-only diff is being offered as finished work.
5. **Verification:** worktree was created from the corrected Batch 9 base and left clean. The existing `BuildFallback()` still contains the ticketed types, `lockedIds`, and `visibleLockedIds` mirrors.

## HANDOFF 2026-08-25 - Batch 9 Revision 2 implementation returns

### WO-1170 site 2 - generated build-category fallback and shared parity owner

1. **What landed:** `BuildCategoryRegistry` no longer contains the hand-maintained `BuildType`/`CatalogType`, `lockedIds`, or `visibleLockedIds` fallback tables. Runtime-file and fallback paths now parse the same DTO shape. Added a deterministic editor generator and generated `.g.cs` snapshot sourced from `build-categories.json`. Added and registered one shared generated-fallback parity suite; it checks both canonical copies and the generated hashes for build categories and the separately returned stake-reward artifact.
2. **Where:** `D:\eoa-codex-b9-1170s2`, branch `codex/b9-1170s2`. Modified `Assets/_Modules/Village/Catalog/BuildCategoryRegistry.cs` and `Assets/Editor/Regression/DataRegression.cs`; added `Assets/Editor/BuildCategoryFallbackGenerator.cs`, `Assets/_Modules/Village/Catalog/Generated/BuildCategoryFallbackData.g.cs`, `Assets/Editor/Regression/GeneratedFallbackParityRegression.cs`, and their `.meta` files.
3. **Not done:** no canonical JSON edit, Unity generator invocation, deliberate JSON-drift RED run, compile/regression gate, commit, or ticket flip. Site 3 must be harvested before the shared suite runs, because absence of its generated artifact is deliberately RED. Site 6 remains withdrawn and has no parity registration.
4. **Mismatch / could not find:** none after Revision 2. The distinct output is `Village/Catalog/Generated/BuildCategoryFallbackData.g.cs`; the pre-existing `Village/Buildings/Generated/` lane was untouched.
5. **Verification:** both canonical copies are byte-identical at SHA-256 `5acd1ba494770cba3cab3cdadf1d0004517a8921d69c3f8b2ce47dcb690ad83c`; the checked-in generated payload parses to schema version 3 and five categories; `git diff --check` clean; braces balanced and NUL count zero in all new/modified implementation files. Claude owns the Unity generator, deliberate RED, compile, and regression markers.

### WO-1173 - device/store schema gate wiring plus tracked repair migration

1. **What landed:** schema parity now blocks all three actual Android/device distribution entry points: the complete morning chain, detached APK build, and Firebase distribution of an existing APK. Each writes a fresh `Builds/schema-parity.log`, invokes the read-only `tools/schema-parity.mjs`, and requires both successful execution and a line-start `SCHEMA_PARITY_OK` marker. The pre-push hook now detects outgoing commits that edit `api/schema.sql` and applies the same blocking marker rule. Added a fresh ordered, idempotent migration covering the absent `dungeon_status`, `auth_sessions`, and `purchase_quotes` tables plus the four entitlement audit columns and widened network CHECK.
2. **Where:** `D:\eoa-codex-b9-1173`, branch `codex/b9-1173`. Modified `morning-ship-chain.ps1`, `overnight-apk-build.ps1`, `distribute-android.ps1`, and `.githooks/pre-push`; added `api/migrations/20260824_0001_repair_schema_parity.sql`. `tools/schema-parity.mjs` remained read-only.
3. **Not done:** no production DB execution, scratch-DB destructive RED proof, migration application, production deploy, commit, or ticket flip. `overnight-webgl-deploy.ps1` was inspected and intentionally left untouched.
4. **Mismatch / could not find:** the granted WebGL script explicitly performs a Vercel **preview** and says `never --prod`; no tracked script in the tree invokes `vercel --prod`. Therefore it cannot honestly satisfy "after every production API deploy." Revision 2 moved production proof out of lane, but a real production-deploy trigger still needs an ops-owned script or CI surface. Wiring the preview script would label the wrong event as covered.
5. **Verification:** PowerShell parser clean for all three modified `.ps1` files; pre-push shell syntax clean after the repository's CRLF normalization; `git diff --check` clean; `node tools/schema-parity.mjs --expected-only` emitted `SCHEMA_PARSE_OK` for 17 declared tables. Live `SCHEMA_PARITY_OK` remains owner/ops-owned because `DATABASE_URL` is redacted.

### WO-1171 section 4 - Revision 2 wallet-choice scope conflict

1. **What landed:** no additional edit beyond the earlier Settings-only connect/disconnect return.
2. **Where:** existing isolated return `D:\eoa-codex-b9-1171`, branch `codex/b9-1171`.
3. **Not done:** persisted preferred wallet package, installed-handler enumeration, sealed-session clearing, kingdom-identity confirmation, or chooser UI.
4. **Mismatch / could not find:** Revision 2 requires a stored package preference consulted by `TargetedLocalAssociationScenario` and clearing `MwaSessionStore` when it changes, but its final binding scope simultaneously says `Settings/` is the only writable area and `Wallet/` remains read-only. Those requirements cannot be implemented through `SettingsController.cs` alone or through the existing `CurrencySkinResolver` seam. **Required CLI action:** grant the exact Wallet files/seams needed after the UI design arrives, or split the new preference-chain mechanism into a separately scoped ticket. The existing Settings placement remains a valid partial return and was not widened silently.
5. **Verification:** current narrow worktree still modifies only `Assets/_Modules/Settings/SettingsController.cs`; Wallet files remain untouched.

## OWNER/OPS EVIDENCE 2026-08-25 - WO-1173 production schema parity

- **Command:** `node tools/schema-parity.mjs` from `D:\eoa`.
- **Fresh production marker supplied by owner:** `SCHEMA_PARITY_OK 17 table(s) verified against api/schema.sql`.
- **Disposition:** WO-1173 acceptance item "SCHEMA_PARITY_OK against production" is now proven. This evidence does not by itself prove the separate deliberately narrowed CHECK/dropped-column RED run in a scratch database; keep that item open unless separately evidenced.

## HANDOFF 2026-08-25 - WO-1196 wallet preference-chain mechanism

1. **What landed:** added a device-local PlayerPrefs wallet-package preference with Seeker as the unchanged default. The existing scenario now exposes the actually installed MWA package ids, resolves an installed stored choice before the unchanged Seeker-first chain, and records whether the winner came from the stored choice, chain rank, or implicit fallback. Its public switch entry point requires an explicit confirmation boolean, refuses uninstalled packages, persists the choice, and calls `MwaSessionStore.Clear(...)` on every effective wallet change so boot-time auto-resume cannot reconnect the old wallet. A stored choice whose app was removed warns and falls through to the original chain. The targeted association clone, `setPackage`, identity URI, association token, websocket lifecycle, and implicit fallback remain intact.
2. **Where:** isolated worktree `D:\eoa-codex-1196`, branch `codex/wo1196`. Modified `Assets/_Modules/Wallet/TargetedLocalAssociationScenario.cs`; added `Assets/_Modules/Wallet/WalletPreferenceStore.cs` and `.meta`. `MwaSessionStore.cs` already exposed the necessary public `Clear` seam and was not edited.
3. **Not done:** no picker/confirm UI, Settings edit, Core edit, provider protocol edit, save migration/re-key, Unity compile, device proof, gate, commit, or ticket flip. Presentation remains with WO-1171/the UI seat.
4. **Mismatch / could not find:** no implementation blocker. The mechanism exposes `MwaSessionStore.StoredAddress` as `CurrentSessionWalletAddress` for picker identification; it deliberately does not read `GameState.BoundWallet`, because this lane may not read/write/move save data. **Proposed owner copy:** title `Switch wallet and kingdom?`; body `A different wallet opens a different saved kingdom. Your current kingdom is not deleted; it returns when you reconnect this wallet.`; actions `Stay Here` / `Switch Wallet`. These strings are proposed only, not settled or rendered by this lane.
5. **Verification:** `git diff --check` clean; new store braces **7/7**, NUL **0**, non-ASCII **0**; every added scenario line is ASCII-only. Structural checks confirm the only preference persistence is `PlayerPrefs`, the effective-change path calls `MwaSessionStore.Clear`, the old `LocalAssociationIntentCreator` + targeted `setPackage` path remains, and neither changed file references any save service or re-key operation. Claude still owes compile/regression and Android proofs for default Seeker, stored installed choice, sealed-session clearing, and stored-uninstalled fallback.

## HANDOFF 2026-08-25 - WO-1073 architecture slice only

1. **What landed:** added a pure CommonJS patronage library with the server-owned lifetime-USD aggregate over `purchase_entitlements`, keyed by wallet. Every durable entitlement participates: there is deliberately no SKU, date-window, or fulfillment-status filter, and NULL canary anchors contribute zero through SQL `SUM`. Added one frozen, data-authored three-tier table at the ruled tentative thresholds ($50 Patron, $150 High Patron, $500 Founder / Benefactor), with only the approved cosmetic/status capability descriptors and no tier above $500. Threshold comparison uses exact integer cents parsed from Postgres NUMERIC text rather than floating point. Added a seven-case oracle covering exact boundaries, all-entitlement aggregation, empty history, immutable cosmetic-only schema, forbidden power/spendable vocabulary, and absence of any entitlement mutation export/SQL.
2. **Where:** isolated worktree `D:\eoa-codex-1073`, branch `codex/wo1073-architecture`; two greenfield files only: `api/_lib/patronage.js` and `test/patronage.test.js`.
3. **Not done:** **THIS IS A NAMED ARCHITECTURE SLICE AND DOES NOT CLOSE WO-1073.** No patronage entitlement flip, migration, endpoint, authentication surface, client/UI surface, cosmetic renderer, public profile output, Unity file, deployment, commit, or ticket status was touched. The excluded entitlement flip remains migration-owned; client tiers remain unrendered and therefore are not wired to any player surface.
4. **Mismatch / could not find:** none. The existing backend suite baseline was 49 tests, not the post-slice count; the seven new architecture/oracle tests bring the full fleet to 56.
5. **Verification:** `NODE_PATH=D:\eoa\node_modules node --test test/*.test.js` completed **56/56 green** with no `DATABASE_URL`; `git diff --check` clean; worktree contains exactly the two granted untracked files. The oracle explicitly proves the module exports status/aggregation only and contains no `INSERT`, entitlement `UPDATE`, or `DELETE` path.

## HANDOFF 2026-08-25 - WO-1198 real quoted price and visible saving

1. **What landed:** the server now transports `usdEffective`, the exact `quotedUsd` input used to calculate `amountBaseUnits`, plus server-computed `usdSaving`. `usdAnchor` remains the auditable authored price. Ordinary undiscounted quotes carry effective=anchor and no saving; pinned canaries carry null for both. The client deserializes these as nullable display facts, shows effective USD as the price, and announces `was $X - save $Y` in words and digits beside the exact SKR. A discounted response missing `usdEffective` fails closed to no dollar label instead of falling back to the wrong full-price anchor.
2. **Where:** isolated worktree `D:\eoa-codex-1198`, branch `codex/wo1198`. Modified `api/_lib/purchase-catalog.js`, `api/purchases/quote.js`, `test/purchases.quote.test.js`, `Assets/_Modules/Wallet/PurchaseQuoteService.cs`, and `Assets/_Modules/Wallet/PackStore.cs`.
3. **Not done:** no quote amount/rate/rounding/settlement change, verify-path change, SKU allowlist, migration, deployment, Unity compile, headed greyscale capture, commit, or ticket flip.
4. **Mismatch / could not find:** none. Dollar saving also had to travel from the server: calculating `usdAnchor - usdEffective` on the client would violate the ticket's no-client-price-arithmetic rule just as surely as deriving the effective price there.
5. **Verification:** the old `discountedUsd` absence pin was re-pointed, not deleted. It now asserts discounted `usdEffective`, server-computed `usdSaving`, endpoint wire preservation, client deserialization, `amountBaseUnits` as the binding client origin, and absence of client-side anchor/rate derivation. Full backend suite **56/56 green** without `DATABASE_URL`; `git diff --check` clean. Claude still owes Unity compile and the ruled greyscale approval-screen capture.

## HANDOFF 2026-08-25 - WO-1199 deploy chain steps 1-8 only

1. **What landed:** added one ASCII PowerShell command centre that runs fresh compile and registered regression markers in order, calls the existing R2 authority and explicitly decodes its UTF-16 parity log, runs production schema parity, captures the current production deployment id before any promote, builds and preview-deploys, byte-compares the served preview `index.html` to the local artifact, promotes that exact preview URL, proves the production database with the row-writing nonce endpoint, and automatically promotes the captured deployment id if that final proof fails. Every refusal records step, wanted marker, log, and reason. PowerShell was chosen because the Unity/R2 authorities are already PowerShell and the R2 log needs native UTF-16 handling.
2. **Where:** isolated worktree `D:\eoa-codex-1199`, branch `codex/wo1199`; added `tools/command-centre.ps1` only.
3. **Not done:** no sale schedule, store table/JSON, `-Status`, analytics view, schema/R2 authority edit, live deploy, production promote, rollback, Unity gate, R2 upload, commit, or ticket flip. This is explicitly WO-1199 steps 1-8 only, as ordered.
4. **Mismatch / could not find:** none. The script always invokes R2 because every full command-centre run ships repo content. Vercel and database credentials are required from `VERCEL_TOKEN` and `DATABASE_URL` environment variables and are never placed in CLI arguments or logs. The operator is explicitly told that `.vercelignore` re-includes `api/`, so promotion is not WebGL-only.
5. **Verification:** PowerShell parser clean; script bytes are pure ASCII; grep proves there is no token CLI argument; `git diff --check` clean. A deliberate missing-secret run was seen RED with exit 20 and the complete refusal `step=5 wanted=VERCEL_TOKEN_SET log=environment reason=VERCEL_TOKEN_MISSING`; its command-centre log was verified ASCII with zero NUL bytes. Claude/ops still owe full live GREEN plus deliberate failed-gate/no-promote and failed-post-deploy/automatic-rollback RED proofs.

## HANDOFF 2026-08-25 - WO-1197 partial-landed board badge

1. **What landed:** chose shape **(a), the PARTIAL sub-badge**. Ready rows whose status says `PARTIAL`, or explicitly says a `SLICE ... LANDED`, remain assignable in the Ready bucket and now render a separate word-bearing PARTIAL badge. This is the smallest truthful fix: it makes landed work visible without imposing a new mandatory field across the repo's legacy partial statuses or making `--check` noisy. The documented status contract explains that this is presentation, not a fourth bucket.
2. **Where:** isolated worktree `D:\eoa-codex-1197`, branch `codex/wo1197`; modified `tools/board_build.py`, `docs/BOARD.md`, and regenerated `BOARD.html` through the generator (not by hand).
3. **Not done:** no new bucket, no bucket priority change, no work-order status edit, no `RESIDUAL:` grammar, no legacy near-miss cleanup, no commit, and no ticket flip.
4. **Mismatch / could not find:** none. A badge was chosen over required `RESIDUAL:` because enforcing the latter honestly would immediately broaden this small lane into migration of numerous legacy partial rows; a non-enforced field would only look stronger while proving nothing.
5. **Verification:** `python tools/board_build.py --check` emitted `BOARD_CHECK_OK 0 unlabeled, 0 status contradictions, mint numbers readable`. The generated real rows for WO-1170, PROD-014, and WO-1073 each contain both `Ready` and `PARTIAL`; `git diff --check` clean.

## HANDOFF 2026-08-25 - WO-1196 wallet preference oracle gap

1. **What landed:** extended the already-registered wallet-session regression suite with the three missing runtime cases: no explicit choice resolves Seeker at chain rank 1 even when another handler is listed first; an installed stored package wins over the default chain; and changing from implicit Seeker to a confirmed installed wallet persists the choice and clears both halves of the sealed MWA session. The probe snapshots and restores the real preference/token/address PlayerPrefs keys in `finally`.
2. **Where:** isolated worktree `D:\eoa-codex-1196-oracle`, branch `codex/wo1196-oracle`; modified only `Assets/Editor/Regression/WalletSessionPersistenceRegression.cs`. It is already invoked by `DataRegression` as `[wallet-session]`, so no shared registration edit is needed.
3. **Not done:** no production Wallet code change, Settings/UI change, save-data read/write/re-key, Android picker launch, Unity compile/regression execution, commit, or ticket flip.
4. **Mismatch / could not find:** none. The suite invokes the internal preference-store seam by reflection because the public switch entry point correctly enumerates Android handlers and therefore returns none in the Editor; this still exercises the exact store method the public path calls after enumeration.
5. **Verification:** structural oracle check confirms references to `WalletPreferenceStore`, both resolution expectations, the preference-change seam, and the sealed-session assertion; braces **31/31**, NUL **0**, `git diff --check` clean. Claude owns the fresh Unity `COMPILE_GATE_OK` and registered `REGRESSION_OK` markers.

## HANDOFF 2026-08-25 - WO-1199 revision B1-B4

1. **What landed:** fixed `Invoke-Captured` so native stderr is non-terminating inside the capture boundary and all stderr/stdout reaches the log. Added a credential-free synthetic regression that emits two stderr lines followed by stdout and proves all three plus exit 0. Replaced preview promotion with an explicit production-target candidate flow: `vercel deploy --target production --skip-domain` creates a non-live candidate using production environment; authenticated `vercel curl` byte-proves that exact deployment despite preview protection; `vercel promote` receives its immutable id, for which the CLI preview-rebuild branch is structurally unreachable. Promotion and rollback no longer trust prose or exit code: both poll the production alias until its inspected `.id` equals the expected id, with a bounded timeout/refusal.
2. **Where:** revised isolated worktree `D:\eoa-codex-1199`, branch `codex/wo1199`; `tools/command-centre.ps1` plus new `test/command-centre.capture.test.ps1`.
3. **Not done:** no live Unity gate, R2 push, candidate deployment, production promotion, induced gate failure, induced post-deploy failure, rollback, commit, or ticket flip. **OPS-OWNED SLICE:** WO-1199 acceptance items 1, 2, 3, and 6 require the single Unity executor, real credentials/deployment, and two induced live failure paths; the dev lane cannot close them.
4. **Mismatch / could not find:** the original ticket's literal preview-then-promote design is incompatible with installed Vercel CLI 56.4.0 because promoting a preview rebuilds it with production environment. The revision therefore uses the ticket-authorized explicit design change: an unaliased production-target candidate is the only artifact that can both carry production environment and later be promoted without rebuilding. No unresolved local blocker remains.
5. **Verification:** `COMMAND_CENTRE_CAPTURE_OK stderr=2 stdout=1 exit=0`; PowerShell parser clean for script and regression; both files pure ASCII; static check proves the production candidate, authenticated candidate fetch, exact-id promote, promotion alias poll, and rollback alias poll are present and the obsolete prose-success regex/preview promotion are absent; `git diff --check` clean.

## HANDOFF 2026-08-25 - WO-1163 money-path correction

1. **What landed:** completed the missing Unity side of the food-to-stone impulse conversion without changing amounts, rates, settlement, or schema. `PackEconomy`, `ShortfallPackOffer`, and `ImpulsePackRegression` now bind/read `stone`; each renamed impulse SKU carries its former `impulse-food-*` id in `legacySkus`. The Node dominance oracle derives its resource list from canonical authored economy keys, and the Unity oracle compares raw authored keys to `PackEconomy`'s `JsonProperty` bindings so Newtonsoft cannot silently discard a future resource.
2. **Where:** isolated worktree `D:\eoa-codex-1163-r2`, branch `codex/wo1163`. Modified exactly seven files: both canonical `packs.json` copies, `api/_lib/purchase-catalog.js`, `test/purchases.quote.test.js`, `PackCatalog.cs`, `ShortfallPackOffer.cs`, and `ImpulsePackRegression.cs`.
3. **Not done:** no food-themed player copy, tier amount, exchange-rate, schema, settlement, deployment, commit, or ticket flip. Owner retains copy.
4. **Mismatch / could not find:** none after correction. The historic internal `PackEconomy.Food` field name remains to avoid an unrelated serialized/C# rename, but its authoritative JSON binding is now `stone` and every public economy path reports stone.
5. **Verification:** quote suite **31/31 green**; canonical pack copies byte-identical at SHA-256 `6F6F7BE3722599980CFBFF6F2A457109654ED7BD1E69C897C8771939763A65F5`; `git diff --check` clean. Claude owns Unity compile/regression execution and commit.

## HANDOFF 2026-08-25 - WO-1199 revision B5-B6

1. **What landed:** removed `--no-color` from authenticated `vercel curl`, because that subcommand forwards unknown flags to the real curl binary. The candidate fetch now deletes any prior remote-index artifact first, captures and requires curl exit zero, and independently requires a newly created output file before hashing it.
2. **Where:** existing isolated worktree `D:\eoa-codex-1199`, branch `codex/wo1199`; revised `tools/command-centre.ps1` only. The synthetic capture regression remains unchanged.
3. **Not done:** no live deploy, promote, rollback, credentials, Unity gate, commit, or ticket flip.
4. **Mismatch / could not find:** none after correction. A stale `index.html` can no longer survive a failed fetch and satisfy STEP 5.
5. **Verification:** PowerShell parser clean; `COMMAND_CENTRE_CAPTURE_OK stderr=2 stdout=1 exit=0`; static B5/B6 assertions green; `git diff --check` clean. Claude/ops own the live proofs.

## HANDOFF 2026-08-25 - UtcDay save-contract oracle

1. **What landed:** corrected `UtcDay.cs`'s drifted migration ledger from three copies to five and fixed the two Wallet paths. Added an editor oracle pinning the persisted `yyyy-MM-dd` constant, invariant culture, Local-to-UTC conversion, and the UTC-midnight boundary. Its source lint requires the exact 2+2+1 outstanding formatter set and fails on any sixth private formatter, while explicitly allowing the two owner-ruled local-day variants.
2. **Where:** isolated worktree `D:\eoa-codex-utcday`, branch `codex/utcday-oracle`; modified `Assets/_Modules/Core/UtcDay.cs`; added `Assets/Editor/Regression/UtcDayContractRegression.cs` and `.meta`.
3. **Not done:** none of the five live monetization call sites was migrated; the two local-day implementations were not changed; `DataRegression.cs` registration is lead-owned and untouched. No Unity run, commit, or ticket mint/status occurred.
4. **Mismatch / could not find:** none. Source inspection confirms BattlePass has two copies, MonthlyCard two, and AdGate one.
5. **Verification:** independent source scan returns exactly those three files with counts **2/2/1**; `git diff --check` clean. Claude owns registration and the Unity marker run.

## HANDOFF 2026-08-25 - username-policy oracle

1. **What landed:** added four DB-free tests for exact length boundaries/trimming, the ASCII character contract, embedded reserved/abuse terms, and the exported leetspeak/punctuation/repeat normalizer.
2. **Where:** `D:\eoa-codex-username-policy`, branch `codex/username-policy-oracle`; new `test/username-policy.test.js` only.
3. **Not done:** no policy/runtime edit, registry, deployment, commit, or ticket mint.
4. **Mismatch / could not find:** none.
5. **Verification:** full Node fleet **60/60 green**; `git diff --check` clean.

## HANDOFF 2026-08-25 - audit privacy oracle

1. **What landed:** added four DB-free behavioral tests pinning stable short salted IP correlation, absence of raw IP/query/body data from logs and stored properties, bounded diagnostic shape, console-only operation, rejecting-DB degradation, and circular-detail non-throw behavior.
2. **Where:** `D:\eoa-codex-audit-privacy`, branch `codex/audit-privacy-oracle`; new `test/audit.privacy.test.js` only.
3. **Not done:** no audit/runtime edit, DB call, deployment, commit, or ticket mint.
4. **Mismatch / could not find:** `safeJson` is private rather than exported as the pipeline note implied; its invariant is exercised through `logAuthReject` instead.
5. **Verification:** full Node fleet **60/60 green**; `git diff --check` clean.

## HANDOFF 2026-08-25 - WO-939 backend-auth define oracle

1. **What landed:** added an editor settings oracle requiring `BACKEND_AUTH_ENFORCED` on both shipping target groups, Android and WebGL, using Unity's PlayerSettings API.
2. **Where:** `D:\eoa-codex-wo939-oracle`, branch `codex/wo939-oracle`; new `BackendAuthEnforcedRegression.cs` and `.meta` only.
3. **Not done:** no ProjectSettings edit, registration, Unity execution, commit, or DONE-status change.
4. **Mismatch / could not find:** **EXPECTED RED DISCOVERY:** current Android and WebGL `scriptingDefineSymbols` rows both lack `BACKEND_AUTH_ENFORCED`, despite WO-939 being DONE. Claude must decide whether to restore both defines before registering or reopen the ticket; the oracle was not weakened to current state.
5. **Verification:** source/settings inspection confirms both missing rows; `git diff --check` clean.

## HANDOFF 2026-08-25 - WO-774 raid-copy oracle

1. **What landed:** added the named source oracle for the Defenders readout, absence of razed/base-percent player copy in HUD and victory, and the rule that modal and tray cannot both label an action `Deploy`.
2. **Where:** `D:\eoa-codex-wo774-oracle`, branch `codex/wo774-oracle`; new `RaidCopyRegression.cs` and `.meta` only.
3. **Not done:** no player-copy correction, registration, Unity execution, commit, or DONE-status change.
4. **Mismatch / could not find:** **EXPECTED RED DISCOVERY:** HEAD still authors `Razed 0%`, runtime `Razed N%`, and victory `% razed`, and does not author the required `Defenders` label. The pre-raid CTA is already `BEGIN ASSAULT`, so the deploy-collision half is structurally green. Claude must reopen/correct the copy before registering; the oracle preserves the written acceptance.
5. **Verification:** offending source literals confirmed at HEAD; `git diff --check` clean.

## HANDOFF 2026-08-25 - WO-929 aura-reparent oracle

1. **What landed:** added a scene-free EditMode-shaped runtime oracle that creates a host and real manager aura, proves the spawned aura is parented to its host, forces the inactive-host deferred-return path, invokes the next-frame sweep, and proves the aura ends inactive outside the host under the pool root.
2. **Where:** `D:\eoa-codex-wo929-oracle`, branch `codex/wo929-oracle`; new `AuraReparentRegression.cs` and `.meta` only.
3. **Not done:** no VFX runtime edit, scene load, registration, Unity execution, commit, or status change.
4. **Mismatch / could not find:** none at source. This is intentionally runtime-shaped and still needs Claude's EditMode/registered execution.
5. **Verification:** reflection seams and cleanup are explicit; `git diff --check` clean.

## HANDOFF 2026-08-25 - guest-identity second-client-copy oracle

1. **What landed:** extended WalletIdentityRegression case 2 so it reads both GameStateService and BackendRequestSigner, requires both guest prefixes, and compares normalized `IsGuestIdentity` bodies before comparing the client contract to the server regex.
2. **Where:** `D:\eoa-codex-guest-identity-oracle`, branch `codex/guest-identity-oracle`; modified only `Assets/Editor/Regression/WalletIdentityRegression.cs`.
3. **Not done:** no production identity edit, server edit, shared registration edit, Unity run, commit, or ticket mint.
4. **Mismatch / could not find:** none; the two client bodies match at current HEAD.
5. **Verification:** structural checks confirm the second source and body comparison are live; `git diff --check` clean. Claude owns the already-registered suite run.

## HANDOFF 2026-08-25 - Batch 12 R4 phantom harvest yield

1. **What landed:** `HarvestSite.AssignedCount` now self-heals by pruning Unity-null worker transforms before yield is calculated, so a destroyed Echo cannot continue paying `YieldPerAssignedPet`. The one owner of Echo teardown, `PetDeployer`, now emits a pre-destroy lifecycle notice; `PetHarvestBootstrap` subscribes once at runtime and explicitly unassigns that worker from every live/inactive harvest site before destruction. No second recall/despawn path was added.
2. **Where:** isolated worktree `D:\eoa-codex-b12-r4`, branch `codex/b12-r4-harvest`; modified `Assets/_Modules/Pets/PetDeployer.cs`, `Assets/_Modules/Village/World/HarvestSite.cs`, and `Assets/_Modules/Village/World/PetHarvestBootstrap.cs`.
3. **Not done:** no quest file, save schema, economy amount, deployment, commit, or ticket status was touched. WO-1195 was deliberately not half-landed; its temporary scaffold was removed cleanly when the owner prioritized today's stable APK/production build.
4. **Mismatch / could not find:** the bootstrap component is normally gated off with placeholder nodes, so the lifecycle subscription is installed in its static runtime initializer before that component gate. This keeps real HarvestSites covered without resurrecting placeholder content.
5. **Verification:** fresh pinned-Unity gate emitted `COMPILE_GATE_OK` in `Builds/b12-r4-compile.log`; wrapper verdict PASS; `git diff --check` clean. The full EditMode baseline ran **956/973**, with 17 failures across ability/pack/wave catalogs, barracks/dev grants, roster/raid/shop, null-coalesce lint, and wallet defaults; none references HarvestSite, PetHarvestBootstrap, PetDeployer, assignment, yield, or this lane's three paths. Therefore compile acceptance is green but the repository-wide test fleet is honestly RED and must not be represented as APK-stable without baseline disposition. Unity's import-only FBX/meta mutations were restored and are not part of the lane.

## VERIFY 2026-08-25 - WO-1163 R2 money-path correction

1. **Verdict:** source-ready handoff in `D:\eoa-codex-1163-r2`, branch `codex/wo1163-r2`; no further edit was required during verification. The seven-file diff implements all six bounce instructions: authored `stone` binds to the reused internal `PackEconomy.Food` slot, impulse amount and shortfall paths use `stone`, the Unity resource family and raw-key-vs-DTO oracle use `stone`, all three retired food SKU ids survive through `legacySkus`, and Node derives its dominance-key union from canonical grants instead of a fail-open literal.
2. **Scope held:** exactly the two canonical `packs.json` copies, `api/_lib/purchase-catalog.js`, `test/purchases.quote.test.js`, `PackCatalog.cs`, `ShortfallPackOffer.cs`, and `ImpulsePackRegression.cs` are modified. No quest path is present; `SaveSchema.CurrentVersion` remains 38; no amount, price, rate, rounding, settlement, or player-facing food-themed pack-name/tagline edit was added.
3. **Verification:** quote suite **31/31 green**; complete backend fleet **57/57 green** using `NODE_PATH=D:\eoa\node_modules`; canonical mirrors byte-identical at MD5 `A711238D20A51A29E294236AB25B3D3D`; the only remaining JSON `food` key tokens are the six intentional `legacySkus` aliases across the two mirrors; `git diff --check` clean.
4. **Lead-owned:** run the registered Unity compile/regression gate before harvesting, then commit/push by explicit path. The dev lane did not commit, push, stage, deploy, or change ticket status.

## HANDOFF 2026-08-25 - Batch 13 WO-1211 cold-launch signing

1. **What changed:** boot cloud reads now attach only an already-usable cached backend session (or the non-interactive guest header) and otherwise keep the local save without invoking `sign_messages`. Connect/auto-resume session warm-up no longer mints a session; the first authenticated action mints lazily. Cloud save writes route through `BackendRequestSigner.TryAttachAsync` and retain the existing fail-closed return/requeue behavior. The duplicated private nonce/sign rail and its response DTO were removed from `GameStateService`; the bearer token remains deliberately memory-only.
2. **Where:** isolated worktree `D:\eoa-codex-b13-1211`, branch `codex/wo1211`; modified `Assets/_Modules/Core/State/GameStateService.cs` and `Assets/_Modules/Core/Web3/BackendRequestSigner.cs`; added unregistered `Assets/Editor/Regression/BackendSaveAuthRegression.cs` plus `.meta`.
3. **Oracle:** source/method-bounded checks pin cached-only boot load, guest routing at both load and save call sites even when enforcement is off, no connect-time mint/sign, shared-signer write routing with the auth-failure branch structurally bound to `return false`, and zero remaining auth-message/nonce/sign authority in `GameStateService`. `DataRegression.cs` was not touched; committer must register `BackendSaveAuthRegression.Run` and judge `BACKEND_SAVE_AUTH_OK`.
4. **Review corrections:** first pass was rejected because `WarmUpSessionAsync` still signed during auto-resume and guest headers were lost when enforcement was off. Both were corrected. A second review required removal of the dead private authority and stronger guest/refusal oracle bindings; those corrections are included.
5. **Verification/ownership:** `git diff --check` clean and exact runtime fence held. Per Batch 13, Codex did not run Unity gates, commit, push, or flip status. CLI lead owns compile, focused marker, full regression, commit/push, and the two-cold-launch device proof. OPS-OWNED acceptance: fresh device boot logs must contain no `sign_messages`; cloud-row success after an authenticated session and write refusal/success both require the live backend/wallet.
6. **Binding precedence noted:** Batch 13 forbids persisting the bearer token and explicitly chooses local-first boot when no in-memory session exists. This supersedes the older ticket paragraph asking to persist the token. A manual-connect versus auto-resume warm-up split would require `WalletSkinBootstrap.cs`, outside this lane's file fence; this implementation follows the newer batch rule and defers warm-up for both paths.

## BOUNCE 2026-08-25 - Batch 13 PROD-016 fence is not closed under the required token migration

1. **Verdict:** no code written in `D:\eoa-codex-b13-prod016`; the authorized two-file fence cannot safely implement the required `food:N -> stone:N` read-migration.
2. **Why:** `EchoAssignments.ResourceTokenOf` is consumed as a public token outside the fence. `EchoBonusCalculator.cs:240,465` compares it directly with `EchoRosterCatalog.TargetToken(entry.Affinity)`, which still returns `food` for Aldwin. Returning migrated `stone` would silently remove the affinity/match bonus. `EchoCardVM.cs:342` maps only `EchoAssignments.ResFood` to `ResourceBuildingProgression.FarmId`; a `stone` picker token would no longer follow the intended prerequisite. `EchoRosterCatalog.TryTargetFromToken` also recognizes `food` but not `stone`, so `TryTargetOf` and labels fail unless adapted.
3. **World-node half:** `Assets/Resources/Harvest/stone.fbx` exists and `HarvestSite` can safely keep the frozen `MineResource.Food` enum while routing its model/display behavior to Stone. That does not make a partial landing safe because the persisted Echo token and consumers must move atomically.
4. **Required fence correction:** add at least `EchoRosterCatalog.cs`, `EchoBonusCalculator.cs`, and `EchoCardVM.cs` (plus their focused oracle) or explicitly rule that `food` remains the frozen internal/persisted token and only display/model output changes. The latter contradicts Batch 13's explicit read-migration requirement.
5. **Preserved:** no edit to live-agent-owned `EchoService.cs`, no blind rename, no `idle` migration, no commit/gate/status change.

## HANDOFF 2026-08-25 - Batch 13 WO-1209 Phase A weapon-scale instrumentation

1. **What changed:** the existing event-driven parent-scale solve now records the complete scale chain required by the ticket: grip-root name, instantiated body-child name, parent bone, parent lossy scale, authored multiplier, and resulting grip-root local scale. Existing renderer counts and resulting world bounds remain in the same line.
2. **Why this seam:** `CompensateParentScale` is reached at initial attach and every meaningful re-solve/re-parent, but its four-input change gate prevents frame-rate log spam. This captures both required moments without changing weapon seating or scale behavior.
3. **Where:** isolated worktree `D:\eoa-codex-b13-1209`, branch `codex/b13-1209`; modified only `Assets/_Modules/Village/Hero/EquipmentController.cs`.
4. **Not done:** no scale correction, offset edit, prefab/model edit, Unity gate, device capture, commit, push, or ticket status change. Phase A is measurement only; the lead/device seat owns the oversized-weapon capture and Phase B ruling from those numbers.
5. **Verification:** exact one-file fence held and `git diff --check` is clean (Git reports only the repository's LF-to-CRLF working-copy notice).
