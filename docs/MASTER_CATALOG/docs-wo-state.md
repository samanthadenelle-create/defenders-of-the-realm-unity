# MASTER CATALOG — docs-wo-state (WO numbering + live WO map)

> **REWRITTEN 2026-08-02** from `CLI_LANES_WO_NUMBERS.md` (banner reconciled 2026-08-02),
> `WorkOrders/` (831 files), and `docs/qa/WORK_ORDER_77*.md`. Branch `wip/village2-and-f8-tickets`.
> The 2026-06-12 edition of this file (438-file legacy sweep, "next free = 412") is superseded;
> its legacy-cluster history (§2g/2h of that edition) remains true for numbers ≤430 but is not repeated here.

---

## 1. Numbering authority (the only rule that matters)

- **Authority = the TOP banner of `CLI_LANES_WO_NUMBERS.md`** — newest banner wins; mint from its
  next-free and bump it in the same edit. The per-lane detail below the banners is FROZEN pre-430 history.
- **Next free WO = 836** (banner "⚠ RECONCILED 2026-08-02"). **782–835 CONSUMED** (782 = reserved, see §3).
- Grok (AI PM) mints from 723+; two spec seats (UI seat + Claude) have repeatedly collided — the banner
  is also the collision-resolution ledger (§3).
- Live board mirror = Notion "Work Orders" DB (see `NOTION_SOURCE_OF_TRUTH.md`); full specs live in git.
- WO-770/771/772/773 specs live in **`docs/qa/`** (validation-signed-off), not `WorkOrders/`; they use
  **decimal sub-orders** (770.1–.11, 771.0–.14) — sub-tasks, never new numbers.

## 2. WO 770–835 state map (status header verbatim-gist + RESULT presence)

R = `*.RESULT.md` exists in `WorkOrders/` (only **808, 818, 819, 820, 826** have one).

| WO | File (short) | Status (from the spec header) | R |
|---|---|---|---|
| 770 | qa/dungeon_functional | SPEC — PARTIALLY SHIPPED (.1/.2/.3/.3b/.4/.7/.9 DONE; .5/.6/.8/.10/.11 open as 775/776/777) | – |
| 771 | qa/raid_system (v2) | SPEC — PARTIALLY SHIPPED (771.9 barracks progression landed 4b5d2353; felt-slice open as 774) | – |
| 772 | qa/enemy_system | SPEC (firmed) — PHASE 1 SHIPPED (EnemyResolver + EnemyTaxonomy live, oracle green); Wildlands deferred | – |
| 773 | qa/obsidian_queue | SHIPPED (save v35, ObsidianQueueState + MigrateToV35); surface work → 778/816/817 | – |
| 774 | raid_loadout_deployring_naming | READY TO IMPLEMENT (CoC raid P0 felt-slice; sequenced in 824 Wave 2) | – |
| 775/777 | dungeon_debt_program (one file) | PARTIALLY SHIPPED — 776 DONE (70e0f85a Granary gate-off); 775 + 777 READY | – |
| 778 | queue_ux_completion | SHIPPED 2026-07-27 (0684f351 + f709a389); ⚠ P0-A SUPERSEDED — bar Work/Queues button RETIRED 2026-08-01 (eb5d0710) | – |
| 779 | ui_spacing_layout_conformance_sweep | READY — thin slice landed (1b3b9364); 55-screen rubric NOT run; overlaps 795 waves 1–2 | – |
| 780 | ftue_first_tower_affordability | SHIPPED 2026-07-27 (5dbe9574 prepaidTower grant) | – |
| 781 | troop_recovery_wiring | SHIPPED 2026-07-27 (cd5a059c TroopRecoveryService live + offline) | – |
| 782 | *(RESERVED — no file)* | Held for the NIGHT_WRAP 07-26 capsule NPC/boss standee re-bake claim; write the file under 782 when started | – |
| 783 | sme_findings_fix_wave | IMPLEMENTED 2026-07-29 (7f1f1e6a/10731c6c/997f063e); 3 owner rulings DEFERRED (D1 wave authority, D2 third raid exit, D3 veterancy pacing — D3 star rules shipped 07-30) | – |
| 784 | echo_lane_consumers | READY (Phase 1); Phase 2 gated on owner copy | – |
| 785 | vfx_runtime_art_survivability | READY (117/121 tagged VFX rows dangle on gitignored packs — promote to Resources/VFX) | – |
| 786 | raid_star_reveal | READY (owner-authored spec; DOTween ruled IN 2026-07-30, isolated change) | – |
| 787 | web_signin_surface_correctness | SHIPPED 2026-07-30 (c18ff812) | – |
| 788 | cathedral_electro_aura | SHIPPED 2026-07-30 (fe87a943 + f83d4c9f/fcf5a249 follow-ups) | – |
| 789 | wave5_boss_swap | SHIPPED 2026-07-30 (53525e8d — Cave Troll @1050 HP) | – |
| 790 | outpost_enemy_presentation | CODE LANDED · TEXTURES PARKED (owner: "i dont have the texture for now") | – |
| 791 | outpost_enemy_offnavmesh | SHIPPED 2026-07-30 (f4f31180) | – |
| 792 | enemy_zero_damage_to_hero | SHIPPED 2026-07-30 (f4f31180) | – |
| 793 | tree_quest_npc_and_marker | SPEC STUB — needs full spec pass (feature, not bug) | – |
| 794 | buildmode_upgrade_verb | SPEC STUB — a slice landed out-of-band (cd661967) | – |
| 795 | no_stacked_screens_scroll_standard | PARTIALLY SHIPPED (waves 1–2 + modal truce + capture coverage); remaining panels of the 16-panel audit READY | – |
| 796 | roomforge_hero_real_body | SHIPPED 2026-08-01 (fb358585) | – |
| 797 | dungeon_room_owned_enemies | READY (F8 seq 461 "all enemies at the entrance") | – |
| 798 | wc3_style_queue_visual | DESIGN INPUT — superseded for implementation by WO-817; pack `docs/UI/WO-798_wc3_queue/` | – |
| 799 | queue_cancel_refund_engine | READY (engine lane; UI waits on 798/801 chips) | – |
| 800 | building_focus_card_unify | READY (Claude designs first; CLI after owner sign-off) | – |
| 801 | queue_glance_icons_multichannel | READY — = WO-817 Phase 3 | – |
| 802 | raid_coc_stakes_casualties_loot | READY | – |
| 803 | raid_session_comfort | READY — sequence AFTER 774 | – |
| 804 | raid_structure_destruction_stars | READY — LATER; needs owner go (R3) | – |
| 805 | upgrade_construction_feedback_parity | READY | – |
| 806 | barracks_progression_spine_ux | READY (design-first, owner image-pair sign-off) | – |
| 807 | troop_upgrade_power_readability | READY | – |
| 808 | hero_gear_power_levels | SHIPPED 2026-07-30 (7d14a17a…55643448; Option A LOCKED; [gear-levels] oracle green) | **R** |
| 809 | war_readiness_power_score | READY | – |
| 810 | rumor_board_layout_rework | SHIPPED 2026-07-31 (74612a25 master-detail rebuild) | – |
| 811 | echo_gather_or_repair_tasks | READY | – |
| 812 | introduce_barracks | SHIPPED 2026-07-31 (c2665a9c) | – |
| 813 | barracks_discovery_offer | SHIPPED 2026-07-31 (fb2939f7); teach quality re-opened as WO-822 (813b) | – |
| 814 | gear_max_level_ability | SPEC DRAFT (owner floated "ability at lvl 5?" — needs ruling) | – |
| 815 | echo_harvest_choice_and_affinity | SUPERSEDED by WO-830; retained as origin ruling only | – |
| 816 | queue_timer_progress_bars | READY — absorbed as WO-817 Phase 2 | – |
| 817 | coc_wc3_queue_visual_system | READY (MASTER program, phases 0–6; folds 798/801/816) · ⚠ RE-SCOPE 08-01: bar Queues button retired — Builders chip is the sole entry | – |
| 818 | kaykit_structure_npc_models | SHIPPED 2026-08-01 (e8bd17b0 + 777dd9ff; NPC_MODELS oracle green) | **R** |
| 819 | structure_singleton_common_v2 | SHIPPED 2026-08-01 (c9a1bd73; SINGLETON_TWINS_OK); PO felt-verify OPEN | **R** |
| 820 | raid_full_army_gate | SHIPPED 2026-08-01 (db963472); rewired onto ArmyReadiness.Compute by 823-A (8560fced); PO felt-verify OPEN | **R** |
| 821 | timed_perk_research | READY (perk research timed + queued on Research channel) | – |
| 822 | barracks_teach_v2 | READY — UNBLOCKED (823 Phase A landed 8560fced) | – |
| 823 | post_review_hardening_pack | PHASES A–D SHIPPED 2026-08-01 (8560fced); Phase E NOT BUILT — awaiting owner ruling | – |
| 824 | coc_wc3_player_enjoyment_program | READY — PROGRAM / DISPATCH AUTHORITY (fun bar + ship Waves 0–6; does NOT re-implement children) | – |
| 825 | immersive_world_map_program | IN FLIGHT — PROGRAM (826 shipped; 827/828/829 READY) | – |
| 826 | realm_map_parchment_ui | SHIPPED 2026-08-01 (eb5d0710; REALM_MAP oracle green, capture verified) | **R** |
| 827 | realm_map_discovery_travel | READY — UNBLOCKED (826 shell shipped) | – |
| 828 | live_minimap_immersion | READY | – |
| 829 | map_atmosphere_content_pins | READY | – |
| 830 | echo_harvest_affinity_synergy | READY (owner-approved 2026-08-01; 3 `OWNER CONFIRM` spots inline; supersedes 815) | – |
| 831 | echo_emergence_sprite_beat | READY (owner-scoped 2026-08-01) | – |
| 832 | building_upgrade_one_true_button | IMPLEMENTED (pending gates — edit-only agent 2026-08-02) | – |
| 833 | kaykit_npc_idle_animation | IMPLEMENTED pending gates (compile + KayKitNpcAnimatorSetup.Build + NPC_MODELS + felt-verify) | – |
| 834 | blank_town_baked_standdown | IMPLEMENTED (pending gates; save v36 everBuiltStructureIds) — edit-only agent 2026-08-02 | – |
| 835 | hud_action_bar_applicability_repack | READY (2 `OWNER CONFIRM` defaults inside) | – |

**Snapshot totals (770–835):** 22 SHIPPED/IMPLEMENTED-gated · 4 partially shipped · ~24 READY ·
3 spec-stub/draft · 2 superseded-for-implementation (798, 815) · 1 reserved (782) · 2 programs in
flight (824, 825). RESULT hygiene: only 5 RESULT files in the range — most late-July ships record
their commit in the spec's Status line instead (823 §"819/820 RESULT hygiene" addressed the newest two).

## 3. Collision-resolution ledger (two-seat mints — banner is the record)

| Number | Kept by | Renumbered spec → new number |
|---|---|---|
| **812** | `introduce_barracks` (barracks placeable — presence authority) | Claude's `echo_harvest_choice_and_affinity` → **815** (since superseded by 830) |
| **832** | UI seat's `building_upgrade_one_true_button` | CLI's blank-town baked stand-down mint → **834** |
| **833** | `kaykit_npc_idle_animation` (T-pose F8 fix) | UI seat's HUD action-bar repack mint → **835** |
| **782** | *(reserved, no file)* | Held rather than collided — NIGHT_WRAP 07-26 pre-claimed it for the capsule-standee re-bake |

Earlier ledgers (still binding — never reuse): 677–685 dual-spec renumbers → 688–698 (07-13 banner),
699/701/702 renumbers, 710→713 pointer stub, the ≤430-era duplicate set (43/46/106–111/129/136–138/152/
159/179/181/253–257/279/280/282/301/329–334), and 344–351 skipped-treat-as-used. The UI/spec seats must
mint from the TOP banner only — the 07-13 "pre-renumber space" failure mode has now recurred twice (812, 832/833).

## 4. Cross-cutting rulings that re-scope shipped WOs

- **Bar Work/Queues button RETIRED (owner 2026-08-01, eb5d0710):** the right-column **Builders chip is
  the ONLY Queues entry** (6-face bar, ObsidianQueueRegression 7c enforces). Stale-marked inside 778,
  817, 821 — any queue-surface WO must NOT re-add a bar button.
- **ArmyReadiness.Compute is the single readiness source** (823 Phase A) — 820's gate + panel rewired onto it.
- **Save schema:** v35 live (773 queue); v36 staged by 834 (everBuiltStructureIds), pending gates.

## 5. FLAGS

1. **RESULT-file discipline has drifted** — CLAUDE.md §2 says every done WO gets a RESULT.md; 17 of the
   22 shipped WOs in this range have none (status-line-only). WO-823 restored it for 819/820; backfill or
   accept status-line as the norm and amend §2.
2. **Three banners deep is live** — the 08-02, 07-26, and 07-24 banners each carry unique state; readers
   must read banners top-down and stop at the first (the 06-12-era "Next free WO = 430" line below the
   banners is frozen and wrong).
3. **`SESSION_CANON_LOADER.md` says next-free 832** — lags the 836 banner (see docs-design.md FLAGS).
4. **832/833/834 are gate-pending as of this writing** — CompileGate + capture/oracles not yet run on the
   2026-08-02 edit-only agent output; do not mark shipped until the orchestrator batch-gates.
