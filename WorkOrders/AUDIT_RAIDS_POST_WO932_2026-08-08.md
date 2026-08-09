# Raids audit — post WO-932 implementation (2026-08-08)

**Scope:** Path A teleport/deploy loop (V1 LOCKED). Walk-to (`ff.raidwalk`) noted only as secondary.  
**Source:** code + data + build settings *after* WO-932 phases 1–4 + next-set (retreat loot, army N/cap toast, eliteCount live, props honesty).  
**Gates last verified:** `COMPILE_GATE_OK`; `REGRESSION_OK 130/130` (incl. `RAID_SCORING_OK`).  
**Not verified:** Phase 0 device/playtest matrix (PO).

---

## 1. Executive scorecard

| Layer | Status | Notes |
|-------|--------|--------|
| Feature flags | **GREEN** | `Raid` ON; `RaidContinuousWalk` OFF (teleport path) |
| HUD entry / capability | **GREEN** | Hide until barracks+troop; one-shot unlock toast |
| Full-army gate | **GREEN / harsh** | Works; toast now `Army N/Cap` |
| Select → pre-deploy → assault | **GREEN** | BEGIN ASSAULT + build-settings check |
| Flagship scenes in build | **GREEN** | 3× `RaidBase_*` registered |
| Garrison + elites | **GREEN** | Spawn path + eliteCount consumer |
| In-raid deploy/rally/retreat | **GREEN** | Tray self-installs |
| Clock / stars / live HUD | **GREEN** | Scoring + RaidHudController |
| Victory → claim → return | **GREEN** | Spire or legacy garrison wipe |
| Retreat/timeout loot | **GREEN** (code) | Finalize(false) + grant — **needs felt confirm** |
| Content depth / props | **YELLOW** | Empty props; only 3 camps |
| Docs / schema honesty | **YELLOW** | Some stale comments fixed; more remain in headers |
| Device feel (PO) | **UNKNOWN** | No play matrix closed |

**Bottom line:** Code spine for a Regular raid is **architecturally complete**. Remaining gaps are **content**, **product policy**, **stale copy/comments**, and **unproven feel** — not a missing victory controller.

---

## 2. What is solid (do not rebuild)

```
HUD Raids (capable)
  → RaidEntryGate / RaidEntryBridge  (raidwalk OFF)
  → RaidSelectionScreen (full army OR redirect)
  → RaidDeployScreen (BEGIN ASSAULT)
  → SceneRouter.GoRaid (IsSceneInBuild)
  → RaidBase_* :
       RaidGarrisonSpawner + RaidSpire
       RaidDeployController (drop / rally / retreat)
       RaidScoring + RaidHudController
       RaidVictoryController (claim, loot, companion, return)
```

| # | Capability | Evidence |
|---|------------|----------|
| S1 | Capability + teach | `RaidCapabilityHudBridge` toast on NOT CAPABLE |
| S2 | Full-army gate with numbers | `RaidSelectionScreen.Open` → Army N/Cap toast |
| S3 | Scene honesty | `CanDeploy` + `IsSceneInBuild` + under-construction toast |
| S4 | Assault CTA clarity | **BEGIN ASSAULT** vs in-raid drop |
| S5 | Auto Recommend not silent | Toast with n + power |
| S6 | Three flagships + build list | configs + EditorBuildSettings |
| S7 | eliteCount live | `ExpandComposition` |
| S8 | Props count honest | empty set → count 0 |
| S9 | Partial loot on retreat/timeout | `DoRetreat` → Finalize(false) |
| S10 | Win = spire (fallback garrison) | `RaidVictoryController` |
| S11 | Headless math | `RAID_SCORING_OK` / full suite 130/130 |

---

## 3. Gap register (post-932)

### P0 — Blockers only if playtest fails

| ID | Gap | Severity | Why still open | Suggested next |
|----|-----|----------|----------------|----------------|
| **P0-feel** | No closed PO play matrix | **P0 process** | Code green ≠ felt green | Run WO-932 Phase 0 checklist on device |
| **P0-softlock** | Soft-lock if spawner missing + no retreat UX noticed | Low residual | Victory warns if no spawner; retreat tray should still exist | Felt: force-fail spawn path |
| **P0-nav** | Troops stuck off-mesh on a base | Unknown | Nav bake not re-verified this session | Deploy troop path to wall/spire |

### P1 — Product / content (real remaining work)

| ID | Gap | Evidence | Recommendation |
|----|-----|----------|----------------|
| **G-content-1** | Only **3** raid targets | Flagship ids only | Author more `scene-configs` + bake + register |
| **G-content-2** | **Props empty** | `props.set: []`, count 0 | Prop dresser OR accept barren camps |
| **G-content-3** | **IronBastion orphan** | Scene + `ORPHAN.md`, not in build | Owner: register as tier-4 **or** delete |
| **G-content-4** | Elite = **duplicate unit**, not true elite kit | Appends last composition id | Optional elite enemyId table later |
| **G-ui-1** | **Auto Recommend** still not composition AI | Toast only; no loadout subset | Hide button **or** real scout-driven picker |
| **G-ui-2** | Scout sketch / preview placeholder | “Scout sketch not yet available” | Thumbnail bake or remove band |
| **G-ui-3** | Est clear time static | Comment FIRST PASS static | Live ETA from power vs garrison optional |
| **G-data-1** | **rewardMultiplier / shardDropChance** UI-only | Shown on cards; loot math ignores them | Wire into `ComputeLoot` **or** stop showing as loot |
| **G-data-2** | Schema comment was stale | Fixed dual-copy elite note this audit | Keep comments in sync with code |
| **G-policy-1** | Full army required to open list | WO-820 | Owner: allow ≥1 troop with dimmed “fill army” |
| **G-policy-2** | Scout with 0 troops allowed on pre-deploy | `GateDeployAtZeroTroops = false` | Confirm still intentional vs capability ≥1 |

### P2 — Design debt / polish

| ID | Gap | Evidence | Recommendation |
|----|-----|----------|----------------|
| **G-hero-1** | Hero + companions on pre-deploy; battlefield role unclear | RAID_BATTLEFIELD comment spectator pending | Owner ruling: hero in raid vs spectator |
| **G-copy-1** | Header comments still say “Auto Recommend stub” | `RaidDeployScreen` header vs body | Doc-only cleanup |
| **G-copy-2** | `RaidOutpostSystem` comment “soft-lock / raid OFF” | Stale vs victory built | Comment scrub |
| **G-path-1** | Walk-to path still exists, easy to re-enable by mistake | `ff.raidwalk` | Leave OFF; don’t dual-ship |
| **G-path-2** | Village2 raid controller parallel | Separate soft-lock history | Keep silo; don’t mix into flagship UI |
| **G-claim-1** | Re-raid claimed base behavior | Claim service persists | Document: new claim companion once; re-assault rules? |
| **G-audio-1** | Victory music swap only | No fail sting on timeout | Optional SFX pass |
| **G-test-1** | No EditMode test for eliteCount expansion | Regression keys only | Small unit test ExpandComposition |

### Explicitly NOT gaps (closed by 932)

| Former gap | Status |
|------------|--------|
| Silent unlock | Toast |
| Fake Auto Recommend no feedback | Toast with n/power |
| Silent bad scene load | Build check + toast |
| eliteCount dead | Consumer + ledger updated |
| Fake prop counts | Zeroed |
| Retreat skips score/loot | Finalize + grant |
| Victory missing | Long-fixed; comments scrubbed on entry path |

---

## 4. Priority ladder (what to do next)

1. **PO Phase 0 feel** — Regular camp: unlock → assault → drop troops → raze spire → return; then retreat mid-fight with ≥50% damage for partial loot.  
2. **Owner policy (30 min)** — full-army gate keep/soften; 0-troop scout keep/kill; IronBastion keep/drop; hero-in-raid.  
3. **G-data-1** — wire or strip rewardMultiplier/shardDropChance (stops card lying).  
4. **G-ui-1** — hide Auto Recommend until real, or implement loadout.  
5. **G-content-2/3** — props + IronBastion.  
6. **Comment scrub** — headers still calling stubs/soft-locks.

---

## 5. Minimal “done enough for store demo” checklist

- [ ] Barracks + full army → Raids opens with 3 cards  
- [ ] BEGIN ASSAULT loads Small Raider Camp  
- [ ] Troops deploy and fight  
- [ ] Spire fall or clear → victory + return  
- [ ] Retreat home without freeze  
- [ ] No toast/claim double-grant on re-open  

If those six pass on device, **gaps above are polish/content, not spine failure.**

---

## 6. Related docs

| Doc | Role |
|-----|------|
| `WORK_ORDER_932_raids_full_functional_audit_and_fix.md` | Original ladder |
| `WORK_ORDER_932_…RESULT.md` | What code landed |
| This file | Post-implement gap audit |

**Next free WO for follow-ups:** take from `CLI_LANES_WO_NUMBERS.md` banner (do not invent numbers here).
