# WO-932 RESULT — Raids full functional (partial implementation)

**Date:** 2026-08-08 · **Seat:** CLI / Grok  
**Status:** PHASES 1–4 CODE LANDED (Phase 0 felt matrix + Phase 6 gates still for implementer/PO)

## Implemented this pass

### Phase 1 — Entry teach
- `RaidCapabilityHudBridge`: once-per-session toast when not capable with concrete reason (barracks / troops / flag).
- `RaidEntryBridge`: fixed stale “victory not built” log; toast when `FeatureFlags.Raid` OFF.

### Phase 2 — Deploy honesty
- `SceneRouter.IsSceneInBuild` public (same gate as GoRaid).
- `RaidDeployVM.CanDeploy` requires scene in Build Settings.
- `RaidDeployScreen`: CTA **BEGIN ASSAULT**; Auto Recommend is real feedback (full army ready); refuse undeployable scenes with toast; assault toast with camp name.

### Phase 3 — In-raid start probe
- `RaidGarrisonSpawner`: `[Flow:Raid] RAID START` line with garrison count + spire HP.
- Victory/scoring finalize-once already present; clock → retreat already wired — not rewritten.

### Phase 4 — Data honesty
- `eliteCount` now appends extra guards (strongest composition id or boss).
- Empty `props.set` → `count: 0` in dual-copy `scene-configs.json` (no fake prop counts).
- **IronBastion:** still orphan (not registered) — intentional; do not advertise until catalogued + baked into build.

### Phase 3b / 5 — next set (2026-08-08 continued)
- `RaidDeployController.DoRetreat`: **Finalize(false) + grant partial loot** before leave (timeout/retreat no longer skip scoring).
- Full-army toast: **Army N/Cap** concrete fill line.
- `RaidScoring` bind: **RAID CLOCK armed** FlowTrace for probes.
- Herald comment: documents PanelManager + IsScreenOpen (no double stack).
- `Assets/Scenes/RaidBase_IronBastion/ORPHAN.md` owner keep/drop note.

## Not done (still open)
- Phase 0 full playtest matrix (needs PO/device).
- Phase 5 hero-in-raid ruling / star breakdown UI redesign.
- Props art fill (count 0 until real set tokens exist).
- Register IronBastion as 4th raid (owner keep/drop).

## Verify
```
CompileGate.Run
DataRegression.RunAll
# Feel: barracks + troops → Raids → Small Raider Camp → BEGIN ASSAULT → clear → return
# Also: retreat mid-raid with ≥50% razed → partial loot + home
```
