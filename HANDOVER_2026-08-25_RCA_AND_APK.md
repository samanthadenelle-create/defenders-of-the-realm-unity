# Handover — Ready-board RCA and APK checkpoint

**Date:** 2026-08-25

**Branch:** `wip/village2-and-f8-tickets`

**Handover HEAD:** `1bf657684` (`chore(board): regenerate ready work-order view`)

## 1. Current release posture

The current integrated code is committed and pushed. The last full Unity data gate after the
WO-970 repair reported:

- `COMPILE_GATE_OK`
- `ATTACHMENT_OFFSET_OK`
- `REGRESSION_OK 283/283`

WO-970 landed at `8e546a8c7`: the stale staff `+90Y` compensation was reset to neutral after the
underlying bounds solver had been repaired. The registered attachment regression pins the neutral
staff default and preserves the independent wand calibration. Owner headed capture/veto remains.

The live board was rebuilt by the sanctioned process:

```powershell
python tools/board_build.py --check
```

Result: 22 Ready rows, 0 unlabeled, 0 status contradictions. The generated board was committed at
`1bf657684`.

## 2. APK / Firebase distribution

The approved distribution command was attempted:

```powershell
cd D:\eoa
.\distribute-android.ps1 -Build -Groups testers -Notes "2026-08-25 stable candidate - WO-970 staff grip and WO-1204 ad-return verification"
```

It stopped before the build and upload because this Codex process has no `DATABASE_URL`:

```text
SCHEMA_PARITY_FAIL no DATABASE_URL in env
```

This was a correct release-gate refusal. Nothing was uploaded. Run the same command from the
owner's credentialed PowerShell; do not bypass schema parity.

Device checks requested for that APK:

1. WO-970: inspect `staff_A` drawn and sheathed; accept or veto the neutral grip.
2. WO-1204: invoke a rewarded ad from Daily Chest and a second non-Settings caller; test earned,
   dismissed and unavailable outcomes; confirm each returns to its caller.
3. Confirm ordinary app backgrounding outside an ad still opens Pause.

## 3. Complete Ready-board RCA

Three read-only RCA seats audited disjoint shards of the regenerated Ready board. Root reconciled
their findings against current HEAD.

### Sev0

1. **WO-1159 — treasury verification is not wired into the actual ship command.**
   `tools/treasury-verify.mjs` supports the required multisig verification, but
   `tools/command-centre.ps1` does not invoke it. Wire the two-state policy: unreachable RPC warns
   and requires explicit acknowledgement; a successful query proving wrong configuration blocks.
   Always invoke with `--multisig`.
2. **WO-1128 — offline-accrual reconciliation has a documented bypass.** An unchanged
   `lastHarvestClaimMs` can yield `no_forward_window`, allowing a fabricated resource delta to avoid
   reconciliation. The safe follow-on needs a canonical server-side maximum gain/rate. This is an
   owner/data-authority hold until that rule is approved.

WO-1199's remaining production/rollback exercises are also release-critical, but they are
credentialed operations rather than repository implementation.

### Sev1 executable queue

1. **WO-823 Phase E.** First-ever raid readiness still requires the full army cap instead of the
   approved three slot-weighted slots. Implement as schema **v40**; v39 already belongs to paidCoins.
   Persist a monotonic `EverCompletedRaid`, derive legacy truth from veterancy or raid cooldowns,
   stamp at the shared `ReconcileRaidEnd` seam, and keep `ArmyReadiness` the sole threshold authority.
2. **PROD-014(c).** Its WO-1069 dependency is now fixed. Revalidate and implement repair-shortfall
   routing through the existing `PackStore.FocusShortfall`; do not create another purchase or price
   authority.
3. **WO-1129.** Enemy-art consolidation remains player-visible and real. It requires an exclusive
   Unity/import lane, GUID-preserving `AssetDatabase.MoveAsset`, collision refusal, zero expected
   coverage misses, and opened EnemyProvingHarness renders.
4. **WO-935.** This is a program, not a single lane. Take one numbered VFX phase only after the
   Phase-0 key/call-site inventory.

### Verification or operations only

- **WO-1199:** credentialed production success, refusal and rollback proofs.
- **WO-1152:** run the existing R2 ship script; then verify the watchtower on device.
- **WO-1192:** fresh portrait/landscape captures opened and judged.
- **PROD-008:** zero-file known-bad historical oracle proof; no developer edits authorized.
- **WO-970:** owner headed staff capture/veto.
- **WO-1204:** Seeker ad-return felt test.
- **WO-1195:** Magic/Wisdom art plus normal, narrow and greyscale captures.

### Sev2 / specification debt

- **WO-1100:** all five alleged material offenders are authored
  `ParticleSystemRenderMode.None`; they intentionally render nothing. Do not assign materials.
  Correct the runtime/oracle semantics and stale comments so None-mode systems are counted as
  intentional non-renderers, then remove the false debt baseline.
- **R3 structure toughness:** production currently duplicates but numerically matches the canonical
  authority. Delegate WallSegment to `HeroTalentModifiers` and strengthen the behavioral oracle.
- **R2/R2b loader observability:** an unmerged isolated commit needs tri-state registration results
  and `FlowTrace.Fail` for caught exceptions before it may land.
- **WO-1170:** sites 4–6 are withdrawn; only a carefully specified standing detector remains.
- **WO-1073:** architecture slice landed, but the entitlement/endpoint/schema work must be split and
  specified before assignment.
- **WO-1200:** blocked until the cloud UI seat's transport capabilities are evidenced.

### Stale, duplicate or not assignable as code

- WO-1026 implementation plan
- WO-1121
- WO-503
- PROD-016 (duplicate of landed WO-1163)
- WO-970 code portion
- WO-1060's four previously reported panel fixes

## 4. Owner actions and recommendations

1. **WO-1082:** choose palette group 4a or 4b. Recommendation: **4a, storage containers first**;
   it matches the evidence and does not reverse WO-963.
2. **WO-1128:** approve a canonical server-side maximum accrual rule/rate.
3. **WO-917:** select an existing style-matched dodge glyph.
4. **WO-1195:** provide or approve Magic and Wisdom icons.
5. **WO-1175:** create/configure Discord outside the repo and split that ops task from the SKR
   cosmetic-reward feature.
6. Run the credentialed APK/Firebase command in section 2.

## 5. Worktree preservation

Do not sweep the remaining dirty tree into a commit. It contains pre-existing or other-seat work,
including `batch_results_state.md`, `FELT_TEST_2026-08-25.md`, `READY_FOR_REVIEW.md`, logs, screenshots,
SQL repair files, `dev/`, `tmp/`, and `Assets/_Modules/Village/Buildings/Generated.meta`.

Stage future lanes by explicit path only.
