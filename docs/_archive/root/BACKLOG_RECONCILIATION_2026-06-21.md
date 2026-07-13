# Backlog Reconciliation — 2026-06-21 (overnight closing task)

> ⚠ **SUPERSEDED 2026-06-27** by `BACKLOG_RECONCILIATION_2026-06-27.md` (re-verified at a later
> HEAD; close-list re-confirmed + 8 new frontier closes added). This doc remains valid as the
> 2026-06-21 point-in-time snapshot — read the 06-27 doc for current state.

**Owner directive (2026-06-21):** reconcile all open tickets + sync as much as possible;
**Defend-the-Tower = deprecated → CLOSE**; **Dungeon = FUTURE WORK**; cross-check Notion ↔
the pipeline backlog and **confirm against the actual code (code wins on truth)**.

**Method:** 3 read-only agents swept all 445 `WORK_ORDER_*.md` files + cross-checked each
classification against `Assets/_Modules` code at HEAD (`52c8b677`) and against
`MASTER_PIPELINES_BACKLOG_2026-06-06.md` / `CLI_LANES_WO_NUMBERS.md`. **The file "Status:
READY TO IMPLEMENT" line is an unreliable template default** (~290 files carry it though HEAD
is at WO-468 with only ~45 RESULT files) — so done-ness is judged by CODE PRESENCE, not status text.

**Notion sync caveat:** the Notion "Work Orders" DB SQL query is Enterprise-gated for this
session's tools (I can fetch/update individual pages, not bulk-query/bulk-update). **This doc IS
the reconciled source of truth**; the Notion board must be updated FROM it (owner-side bulk edit,
or I can patch specific pages on request).

---

## A. DEFEND-THE-TOWER / PatriciaLight → CLOSE (DEPRECATED)

Code-confirmed dead: no `Assets/_Modules/**/PatriciaLight*` module, no `PatriciaLightMode.unity`
scene; `PIPELINE_STATE.md` records the 2026-06-09 removal (only `Resources/PatriciaLight/tower2`
art remnant kept). **CLOSE these as deprecated:**

**WO-46, 47, 48, 96, 98, 99, 100, 209, 221, 317, 318, 319, 320, 330, 331, 332, 333(dtt-sensitivity variant)**

Notes:
- WO-46 = two files (`_tower_combat` + `_defend_the_tower_refinement`), both DTT.
- WO-209 = misfiled duplicate of WO-46 Tower Combat.
- WO-333 is ambiguous: close `333_dtt_sensitivity_consolidation` ONLY. **Do NOT close**
  `333_village_death_no_dtt_atb_trigger` (that's a Village ATB ticket; "no_dtt" = the bug is
  no-DTT-involved).
- Mentions-only (leave open/as-is, NOT DTT WOs): 92, 97, 111, 130, 142, 143, 168, 197, 279,
  301, 322, 327, 328, 329, 334, 430, 433 + the WO-106/108/175/282/283/284/285 RESULT files.
- **Code cleanup follow-up (not a WO close):** `Core/SceneRouter.cs` still declares dead
  `PatriciaLightParams` / `GoPatriciaLight()` / `const PatriciaLight = "PatriciaLightMode"` —
  pointing at a scene that no longer exists. Strip in a future cleanup pass.

## B. DUNGEON → FUTURE WORK (deferred)

Dungeon code is real (`DeNelle.Dungeons` module; `Dungeon_HealersCottage` built/data-driven,
`Dungeon_FolksGranary` stub; `Lantern.cs`). **Set these OPEN dungeon WOs to FUTURE WORK:**

**WO-23, 29, 30, 65, 165, 250, 324**

- Already DONE (do not re-queue): **WO-19** (dungeon entrances), **WO-59** (dungeon VFX) — both shipped.
- Mention-only (leave as-is, NOT reclassified): 97, 116, 130, 142, 161 (player-home reuses dungeon
  tech), 197, 240, 467 + incidental low-hit files.
- WO-65 / WO-250 are portal-VFX-flavored; included as future-dungeon. If you treat portal VFX as a
  separate visual lane, they can drop to mention-only.

## C. OPEN BACKLOG RECONCILED vs CODE (code merge confirms)

### C1. DONE-IN-CODE → CLOSE (deliverable exists at HEAD despite no RESULT file)
Foundational systems proven present in `Assets/_Modules` — these transitively close the large
100s–340s "READY" block that depended on them:

| Cluster | Code evidence (HEAD 52c8b677) |
|---|---|
| WO-290/304 Quests + board | `Core/Quests/QuestService.cs`, `HUD/QuestTrackerHud.cs`, `Village/Hero/RumorBoardPanel.cs` |
| WO-339 quest save versioning | `Core/State/SaveSchema.cs` (QuestProgress, schema v24, round-trip) |
| WO-293/295 crafting + Aegis | `Village/Crafting/GearCraftingRecipeCatalog.cs` |
| WO-297/298/299 pets | `HUD/PetUnlockTracker.cs`, `Pets/PetSkillTreeCatalog.cs`, `Pets/PetDeployer.cs` |
| WO-108/215/282/314 build mode | `Village/BuildMode/BuildModeController.cs`, `BuildPreviewModal.cs` |
| WO-131/228/424 economy/harvest | `Village/EconomyService.cs`, `World/Camps/ClaimableCamp.cs` |
| WO-258/276 ATB | `BattleATB/ATBCombatManager.cs`, `BattleController.cs` |
| WO-430(mvvm)/431/434 UI MVVM + shop/inventory | `Core/UI/Mvvm/*`, `*.RESULT.md`, `ShopVM/InventoryVM/EquipVM` |
| WO-450 injector fixes | `Village/World/OuterWorldBoundaryInjector.cs` |
| WO-358/368/380/382/387/465 | pipeline doc marks ✓DONE |

### C2. STALE / DEAD → CLOSE (premised on removed/abandoned/superseded systems)
- **Village.unity-premised** (scene abandoned; Village2 + CastleHubBuilder canonical): WO-101, 102,
  103, 105, 126, 158, 167, 168, 177, 179, 183, 188, 247, 253, 256, 263, 278.
- **Superseded by a later WO:** WO-110(yarn)→455; WO-307/378/411→403/405+437/438; WO-414→429
  (file already `_SUPERSEDED`); WO-333(dtt)→332; WO-301 backbone→SaveSchema.
- **Yarn-specific narrative specs** (mid-migration off Yarn per WO-455): WO-291, 294 (the quests
  exist; the Yarn-authoring specs are stale — author in `dialogues.json`).
- **Explicitly CANCELLED:** WO-265 (castle doors, owner-cancelled), WO-328 (closed no-repro).

### C3. GENUINELY OPEN — EXISTING (actionable fixes; system built, change pending) — **13**
WO-431(raid rewards), 437(input/battle-lock gate), 437/438(combat HUD tech-skin), 438(base-loop RCA),
439(quest board collection), 440(ATB wiring gaps), 446(front-door cold open), 447(hero-select polish),
448(hub↔OuterWorld seam fix), 451(intro shorten), 454(unified quest system), 468(seam redesign).

### C4. NEW FEATURE → route to PO as spec (not a bug-fix) — **7**
WO-435(Blink merchant view), 442(build-mode wall pay/validation), 452(autopilot hardening oracles),
453(dev capture toolkit), 454(faction base generator), 467(RegionGate recipe primitive),
455(custom dialogue — PARTIAL/in-flight: `Core/Dialogue/*` + `dialogues.json` + `FeatureFlags.CustomDialogue`
default OFF; phased migration is the active mandate).

## D. WO-NUMBER COLLISIONS (metadata cleanup — most urgent: WO-430)
- **WO-430 = FIVE colliding files** (canonical = `ui_mvvm_seam`; others: city_upgrades, instrumentation_TGVRU,
  gear_catalog_from_db, Handover_Triage). Pipeline authority = ui_mvvm_seam.
- **WO-431/432/433/434/437/438/454** each have TWO divergent specs (shop-MVVM arc vs raid/building/quest/tech-skin) — both tracks live, numbers duplicated on disk.
- Legacy dup pairs (doc-listed): 43, 106, 107, 108, 109, 110(×3), 111, 129, 136(×3), 137, 138, 152,
  159, 179, 181, 253–257, 279, 280, 282, 301, 329–334.
- Filesystem max = WO-468, but `MASTER_PIPELINES_BACKLOG` still says "next free 430" — **doc is behind
  reality**; 430–468 were minted since. Do NOT reuse 328–339 / 344–351 (Notion-reserved).

---

## E. RECONCILED COUNTS (headline)

| Bucket | Count | Action |
|---|---|---|
| DTT/PatriciaLight | 17 WO# | **CLOSE — deprecated** |
| Dungeon (open) | 7 WO# | **FUTURE WORK** (+2 already done: 19, 59) |
| Done-in-code | ~26 clusters | **CLOSE** (code confirms) |
| Stale/dead | ~40+ | **CLOSE** |
| **Genuinely open — EXISTING** | **13** | actionable fixes (430–468 frontier) |
| **New feature** | **7** | route to PO as specs |

**The true actionable frontier is small: ~13 existing-system fixes + ~7 new-feature specs, all in
the 430–468 block.** Everything below ~390 is overwhelmingly done-in-code or stale despite the
"READY" status text. The ~445 file / ~397 distinct-number raw count collapses to a **~20-item live
backlog** once dead/done/duplicate are removed.

## F. NOTION SYNC — what's needed
This doc is the reconciled truth (code-confirmed). To sync Notion (DB SQL bulk-query is
Enterprise-gated here):
1. Mark §A WO#s **Closed/Deprecated** (DTT).
2. Mark §B WO#s **Future work** (dungeon).
3. Mark §C1+§C2 **Done/Closed**.
4. Keep §C3 (13) **Open**, §C4 (7) as **PO/spec**.
5. Resolve the WO-430 five-way collision (keep `ui_mvvm_seam`).
I can patch specific Notion pages on request (`notion-fetch`/`notion-update-page`), or this drives
an owner-side bulk edit. **Nothing pushed; all reconciliation is local.**
