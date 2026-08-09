# MASTER CATALOG — docs-design (the load-bearing design/canon doc set)

> **REWRITTEN 2026-08-02** from the actual tree (glob + header/status reads), branch
> `wip/village2-and-f8-tickets`. Scope: the CURRENT load-bearing design/canon docs — the set a
> session must trust — with one-line role + freshness verdict each. The exhaustive 2026-06-12
> per-file sweep of all `docs/**` this file used to carry is superseded; historical/port-era docs
> are compressed into §H. Legend: **LIVING** = updated in place, trust it · **ANCHOR** = dated,
> newest wins, older ones frozen · **BINDING** = law, read before touching its area ·
> **FROZEN** = dated snapshot, true-as-of-date only · **STALE** = superseded, banner present or needed.

---

## A. The canon spine (read-first, every session)

| Doc | Role | Verdict |
|---|---|---|
| `CANON_GROUND_TRUTH_2026-08-01.md` (root) | THE live reality anchor — delta over 07-26 → 07-22 (deep module digests/§6 drift ledger/§7 comment-lies/§8 landmines). HEAD `ac0a52e3`, pushed, save v35. | **ANCHOR — newest.** Chain: 07-12/13/18/19/22/26 all bannered-frozen; only 08-01 is live. |
| `KEY_FACTS.md` (root) | The living fact sheet — code-verified facts updated IN PLACE, never snapshotted; wins over any contradicting doc. | **LIVING** |
| `SESSION_CANON_LOADER.md` (root) | Session primer the owner pastes; LIVE THREAD block re-stamped 2026-08-01. | **LIVING** (its "next-free = 832" line lags the 08-02 banner's **836** — banner is authority) |
| `docs/HANDOVER.md` | The one-sheet operator's manual; ★★ SESSION HANDOVER block currently 2026-08-01. | **LIVING** |
| `docs/MASTER_CATALOG.md` + `docs/MASTER_CATALOG/*.md` | The mandatory file-by-file SME catalog (this folder). | **LIVING** (per-area files carry their own dates — check each header) |
| `PREFLIGHT_GATE.md` (root) | The binding yes/no checklist before any edit/debug. | **BINDING** |

## B. Architecture / method law (BINDING set)

| Doc | Role | Verdict |
|---|---|---|
| `docs/ARCHITECTURE.md` | The single authoritative architecture hub; indexes the per-area `*_ARCHITECTURE*` deep-dives. | **BINDING hub — CURRENT** |
| `docs/ARCHITECTURE_PRINCIPLES.md` | Project law: HP-B2B bounded contexts, presentation-never-touches-objects, right-not-easy. | **BINDING — CURRENT** |
| `docs/ARCHITECTURE_NORTH_STAR.md` | The 6 load-bearing technical principles keeping the vision reachable. | CURRENT |
| `docs/INSTRUMENTATION_STANDARD.md` | The *method* for CLAUDE.md §12 (instrument, don't guess) — FlowTrace/Guard authoring from line one. | **BINDING — CURRENT** |
| `docs/TICKET_PIPELINE.md` | QA → CLI → PO role-separated ticket pipeline (owner 2026-06-20). | **BINDING — CURRENT** |
| `docs/UI_BLINK_TEMPLATE_CANON.md` | The UI law: one Obsidian master-frame template, all screens fill it (owner-ratified 2026-06-28). See `BLINK.md` in this folder. | **BINDING — CURRENT** |
| `docs/ZONE_STREAMING_ARCHITECTURE.md`, `docs/HUD_OBSIDIAN_ARCHITECTURE_2026-07-03.md`, `docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md` | Per-area deep-dives under the hub. | CURRENT (dated but not superseded) |

## C. Pillar north-stars (each the ONE doc for its pillar)

| Doc | Role | Verdict |
|---|---|---|
| `docs/COMBAT_PIVOT_NORTHSTAR.md` | ACTIVE north star (owner 2026-06-22): single controllable Knight "Grom", Blink armor junked, isolated real-time BattleArena. | **CURRENT** |
| `docs/RAID_NORTHSTAR.md` | The one canonical raid doc (2026-07-26): loop LOCKED to CoC Teleport/Deploy; build plan = WO-771 v1. Wins over all other raid docs. | **CURRENT ANCHOR** |
| `docs/WC3_COC_EXPERIENCE_ANALYSIS.md` | LIVING program hub (2026-07-30): WC3 production glance + CoC invasion; §2A army ladder; spawns the 798–835 WO arc. | **LIVING — CURRENT** |
| `docs/PAIN_POINTS_2026-07-26.md` | BINDING PM rulings for pipeline prioritization (Grok/PM); unblocked Tier-0 gates (e.g. Hollow Ones approved §1.1). Chain of one — newest and only. | **BINDING** (dated; re-anchor on next PM pass) |
| `docs/ECHO_SOCIAL_VISION.md`, `docs/RAID_PILLAR_VISION.md`, `docs/RAID_TROOP_NARRATIVE.md`, `docs/RAID_TROOP_UI.md` | Pillar vision/support docs under their north-stars. | CURRENT as intent; RAID_NORTHSTAR wins on conflict |
| `docs/NORTH_STAR.md` / `NORTH_STAR_PROGRESS.md` | Original one-picture vision / dated ladder tracker. | MIXED (Pi-economy line superseded by Solana/$SKR) / FROZEN |
| `docs/PATH_TO_V1.md` (2026-06-23) · `docs/ROADMAP_V1.md` (2026-06-28) | V1 through-line + launch roadmap. | Intent CURRENT, embedded state ~5 weeks old — check against the 08-01 anchor |

## D. Reference dictionaries (`docs/reference/` — durable, source-cited, Sunday-refreshed)

| Doc | Role | Verdict |
|---|---|---|
| `HERO_ANIMATION_DICTIONARY.md` | Every Grom (KnightV3 + KnightMocap.controller) animation → action; ActionBar + Hot-Swap map, file:line cited. | CURRENT (2026-07-19) |
| `REGRESSION_COVERAGE_MATRIX.md` | Coverage verdict over the 73 audit findings: 0 hard-covered, 12 soft, 61 uncovered; 7 root oracles proposed. | CURRENT (2026-07-19) — partially overtaken by the late-July oracle wave (REGRESSION_OK now 103 checks); refresh due |
| `MASTER_BACKLOG_2026-07-19.md` | The find-all-work audit (60 agents / 11 systems): "built but disconnected" is the master verdict. | CURRENT (2026-07-19); several named gaps since closed (dungeon body, queue, realm map) |
| `DOTWEEN_SME.md` | DOTween canonical reference (owner ruled DOTween IN 2026-07-30 for WO-786). | CURRENT (2026-07-30) |

## E. SME dossiers (`docs/SME/`) + asset-pack notes

- `docs/SME/`: `BLINK_SME.md` (07-11; pack root is now cited repo-root-relative — 2026-08-09),
  `KAYKIT_SME.md`, `CHARACTER_PACKS_SME.md`, `VFX_PACKS_SME.md`, `AUDIO_SME.md`,
  `POLYPERFECT_QUATERNIUS_SME.md`, `SWORD_SHIELD_MOCAP_SME.md`, `ASSET_STORE_LEDGER_2026-07-12.md`, etc.
  — durable pack dossiers, CURRENT except path-of-record drift.
- Pack notes set (`docs/KAYKIT_NOTES.md`, `POLYPERFECT_NOTES.md`, `QUATERNIUS_NOTES.md`,
  `MIRZABEIG_VFX_NOTES.md`, `HOVL_STUDIO_SME.md`, `SPELLS_PACK_NOTES.md`, `LANA_RPG_VFX_NOTES.md`,
  `LEANTOUCH_NOTES.md`, `UNITASK_NOTES.md`, `YARNSPINNER_DIALOGUE_NOTES.md`, `INSTALLED_PACKS_INDEX.md`) — CURRENT.
- `docs/BLINK_NOTES.md` — **STALE-bannered** (pre single-Knight pivot); `docs/BLINK_UI.md` (06-17) —
  still the accurate re-skin architecture note. Both superseded in detail by `MASTER_CATALOG/BLINK.md`.

## F. Design-lane specs (`docs/design/` + root DESIGN_*)

- `docs/design/`: `BUILDING_UPGRADE_TREES.md` (rev 2 — drives WO-739/821 perk trees),
  `BUILDING_PERKS_DESIGN.md`, `BUILDING_TIER_MODEL_REQUIREMENTS.md`,
  `WAVE_AUTHORING_REFERENCE_2026-07-30.md`, `WC3_BUILDING_REFERENCE_2026-07-30.md`,
  `AUDIO_DESIGN_V1.md`, `BALANCE_AUDIT.md`, `ART_PIPELINE_AUDIT.md`, `LIVEOPS_RETENTION.md`,
  `LOCALIZATION_READINESS.md`, `UX_ONBOARDING_ACCESSIBILITY_AUDIT.md`, `DUNGEON_ART_FINISH_AUDIT.md`
  + concept images. CURRENT working specs for the active WO arc.
- `docs/UI/`: `Grok-02-Obsidian-UI-guidance.md`, `Grok-03-here-to-there-WO-program.md`,
  `OBSIDIAN_UI_DESIGN_skilltree_inventory.md`, `WO-798_wc3_queue/`, `WO-810_rumor_board/` — design
  packs feeding the UI seat. CURRENT.
- Root `DESIGN_*.md` (CORE_LOOP, ELARION_CITY, PET_SYSTEM, VENDOR_STORYLINES, VILLAGE_DISTRICTS,
  FORGEMASTERS_SAGA, ATB_UI…) + `ECHO_WORKFORCE_SPEC.md` — SPEC-era design set; intent mostly
  current, embedded state predates the July pivots. Check the anchor before acting on any.

## G. QA / audit set (`docs/qa/` — recent first)

| Doc | Role | Verdict |
|---|---|---|
| `UI_REVIEW_2026-08-01.md` | Mobile-first UI readability review from real-pixel captures (16-panel sweep + worst-case set). | CURRENT — newest QA read |
| `FULL_AUDIT_2026-07-26.md` | Sunday-scale ground-truth sweep — master truth doc for the build decision. | FROZEN (07-26) — the base the 783/795/796 fix waves worked from |
| `GAMEPLAY_GAPS_2026-07-26.md` | Full player-journey QA walk (FTUE→town→dungeon→raid→meta); P1-A…H gap ids referenced by WOs. | FROZEN (07-26); several gaps since shipped |
| `SUNDAY_STATUS_2026-07-26.md` / `NIGHT_WRAP_2026-07-26.md` | Sunday housekeeping status + night wrap (claimed WO-782). | FROZEN |
| `dungeon-raid-validation-2026-07-26.md` / `dungeon-regression-2026-07-26.md` | Validation sign-off for WO-770/771 firmed specs + dungeon regression record. | FROZEN |
| `TEST_PLAN_V1.md` | The V1 QA test plan (Unity 6000.4.8f1). | LIVING |
| `UI_CAPTURE_TEST_SCENARIOS.md` | Graphics-enabled headless capture scenarios (Store/Inventory vs Obsidian refs). | CURRENT |
| `WORK_ORDER_770–773*.md` | The four firmed pillar specs (dungeon/raid/enemy/queue) — live in `docs/qa/`, NOT `WorkOrders/`. | See `docs-wo-state.md` |
| `bug-log.md`, `qa-test-plan.md`, `regression-suite.md`, `uat-script.md`, `owner-acceptance-checklist.md`, `po-validation-village-maps.md` | Week-1→8 port-era QA set. | FROZEN/HISTORICAL |

## H. Everything else (compressed)

- **Canon/brand (current):** `docs/DESIGN-DECISIONS.md` (BINDING changelog), `docs/BRAND_BIBLE.md`,
  `docs/BRAND_AND_PLATFORM_CANON.md`, `docs/GAME_DESCRIPTION.md`, `docs/STORYLINE.md` (v2 — Cathedral
  Spire), `docs/LORE_FALL_AND_FOUNDING_OF_ELARION.md`, `docs/PITCH_DECK.md`, `docs/MARKET_RESEARCH.md`,
  `docs/SOLANA_MOBILE_GRANT_APPLICATION_2026-07.md`, `docs/SOLANA_STORE_LISTING_PLAN_2026-07-22.md`,
  `docs/PRIVACY_POLICY.md`, `docs/PUBLISHING_STEPS.md`.
- **Engine/feature SPEC docs (designed, mostly not built):** CHARACTER_*/WORLD_ENGINE/ENGINE_MASTER_PLAN,
  CATALOG_SYSTEM, PLAYER_BASE/SCROLL_BLUEPRINT/WORLD_COLLECTION, BATTLE_2D_PARTY, MONSTER_FAMILY,
  ENCOUNTER/ALERT_INTEL, RESOURCE_ECONOMY, TALENT_TREE_V2, ITEM_DROPS, LEGENDARY_GEAR, BARD, DUNGEON_DESIGNS.
  Treat as intent, never as current-state.
- **Dated audits/analyses (FROZEN):** `docs/audit/*`, `PM_AUDIT_2026-07-12.md`,
  `SECURITY_AUDIT_2026-07-12.md`, `SECURITY_COMPLIANCE_HARDENING_AUDIT.md`, `UNITY_BEST_PRACTICES_AUDIT`,
  `REUSABILITY_AUDIT`, `VISION_GAP_ANALYSIS`, `WO673_*_REVIEW.md`, `CLAUDE_GROK_DISCUSSION_2026-07-26.md`,
  `MORNING_HANDOFF_2026-07-17.md`, `SEAM_BRIDGE_OFFSETS_LOCKED_2026-07-04.md` (locked values — still authoritative data).
- **Port-era / STALE (banner or ignore):** the whole `docs/port-notes/*` set, `v2-unity-port-spec.md` +
  backend spec (Avalon/Blaise canon stale), `avalon-village-layout-spec.md`, `narrative-bible.md`
  (tree premise superseded), `whitepaper.md`, `PI_PITCH.md`, `refactor-feature-modules-spec.md`,
  `recovery-work-orders.md`, `claude-code-work-order.md`, `build-mode-architecture.md` (lowercase),
  `webgl-hosting-notes.md`. The stale-canon traps (Avalon/Blaise/Heart-Tree/lantern/Village.unity/Pi)
  catalogued in the 06-12 edition of this file all still apply to these — do not act on them.

---

## FLAGS (2026-08-02)

1. **Anchor chain discipline holds** — 7 CANON_GROUND_TRUTH files exist; only 08-01 is live, and it
   explicitly supersedes 07-26. No unbannered stale anchor found.
2. **`SESSION_CANON_LOADER.md` numbering line lags** — says "WO next-free = 832"; the
   `CLI_LANES_WO_NUMBERS.md` 2026-08-02 banner says **836**. Banner is the authority (per its own rule).
3. **`docs/reference/REGRESSION_COVERAGE_MATRIX.md` (07-19) predates the oracle wave** — REGRESSION_OK
   is now 103 checks (loader 08-01); the "0 of 73 covered" verdict needs a Sunday refresh.
4. ~~**`docs/SME/BLINK_SME.md` cites an absolute pack root**~~ — **RESOLVED 2026-08-09.** The repo root
   is **machine-dependent** (`C:\eoa` on one box, `D:\eoa` on another), so *no* doc may name it; the
   dossier now cites `Assets/Blink/` repo-root-relative. Same rule applies to every doc/script.
5. **`docs/qa/` is the live QA ground** — the 07-26 audit trio + 08-01 UI review drove the 783→835 WO
   arc; anyone triaging must read UI_REVIEW_2026-08-01 before re-auditing UI.
