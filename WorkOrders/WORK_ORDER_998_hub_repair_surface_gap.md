# WORK ORDER 998 — No repair surface exists in the hub while structures burn

**Status:** CLOSED — SUPERSEDED by `WorkOrders/WORK_ORDER_1024_no_repair_surface_in_hub.md`
(2026-08-15, same defect minted by both seats within minutes; the UI seat's 1024 carries the PROVEN
root cause — `HubRepairAffordance.cs:88-109` one-shot `TrySpawn` bails when `SceneHasRepairables()`
is false at scene-load, before the town exists, and nothing retries — verified at source by the CLI
seat. 1024 wins on RCA completeness. ⚠ Two items from THIS file must ride with 1024's fix: (1)
register the ORPHAN `RepairProbeRegression` (or a consumption-asserting successor) — an unregistered
oracle never runs; (2) check whether the Obsidian queue's Repair kind was meant to supersede the old
affordance before resurrecting UI — WO-911 arc.)
**Minted:** 2026-08-15 (CLI seat, main-line block) — banner bumped 997 -> 999 in the same edit as this
mint + WO-997.
**Lane:** Village walls/repair. File-disjoint from talents/caravan/mana lanes.
**Provenance:** owner F8 captures **seq 2397 + 2398** (2026-08-15 21:48, `Main_Castle_Overworld`),
identical `FlowTrace.Fail` from `RepairAvailabilityProbe.ReportSurfaces`
(`RepairAvailabilityProbe.cs:209`), fired in BOTH `WaveManager=Countdown` and `WaveManager=Active`:

> `[Flow:RepairProbe] SURFACES scene='Main_Castle_Overworld' WallRepairController=ABSENT
> HubRepairAffordance=ABSENT WaveManager=Active -> NO repair surface exists in this scene at all
> while a structure burns. The player has no way to repair anything here.`

## 1. What the captured data proves

The probe is a deliberate watchdog (§12 pattern: a silent gap converted into a captured line) and it
did its job: during the owner's 2026-08-15 editor session a structure was burning in the merged hub
and **neither repair surface was present in the scene** — `WallRepairController` ABSENT and
`HubRepairAffordance` ABSENT. This is the player-felt "my stuff burns and I can do nothing" state.

## 2. Known adjacent facts (read before theorising)

- `RepairProbeRegression` is a **known ORPHAN oracle** (editor-tools catalog: suite registration is a
  manual follow-up step and this one was missed) — whatever fix lands, registering that oracle (or a
  sharper one) is part of DONE.
- The hub is the MERGED `Main_Castle_Overworld`; repair surfaces may have been wired against a
  pre-merge scene name or injected by a bootstrap that no longer matches (`HubScenes` gating class —
  see the exact-scene vs contains-matching inconsistency ledger in `docs/MASTER_CATALOG/village-npcs.md`).
- WO-911's Manage/Queues arc touched repair routing; check whether the Obsidian queue's Repair kind
  superseded the old wall-repair affordance ON PURPOSE (if so, the FIX is to teach the probe the new
  surface, not to resurrect the old one — check for a ruling before re-adding UI).

## 3. Plan

1. **Triage (read-only):** find who is supposed to spawn `WallRepairController` / `HubRepairAffordance`
   in the merged hub (injector? scene bake? flag?) and whether the Obsidian Repair channel replaced
   them. Cite lines.
2. **Fix at the owning layer** — either re-seat the repair surface in the merged hub, or (if the queue
   superseded it) point the probe + the player affordance at the queue path.
3. **Register the orphan `RepairProbeRegression`** (or replace it with a consumption-asserting case:
   "a damaged structure in the hub always has a reachable repair affordance").
4. Headless verify + the probe's own line going quiet across a burned-structure session.

## 4. What NOT to touch

- The probe itself (`RepairAvailabilityProbe`) — it is the net that caught this; never strip (§12).
- `waves.json` / wave composition; the burn source is irrelevant to the gap.
