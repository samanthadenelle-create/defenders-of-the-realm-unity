# Lanes — Work-Order Numbers Only (for CLI)  ·  reconciled 2026-06-12 (nightly refill)

> ## ⚠ RECONCILED 2026-08-08: main line next free = **928**. **782–859 + 900–927 CONSUMED.**
> - **927** = **PathPartial seam revalidation** — the design doc's §5.5.2 erosion justification is DEAD
>   (landing measured 1.30 m, path outcome unchanged). Capture M1–M7 on ONE failing seam (attachment-point
>   world coords, delta vector, connector scale/bounds/span, connector-disabled check, and a
>   `NavMesh.CalculateTriangulation` dump), then re-justify or retire the connector.
>   File `WORK_ORDER_927_pathpartial_seam_revalidation.md`. **READY TO IMPLEMENT** (owner-authored).
>
> *(banner bumped 927 → 928 in the SAME edit as the 927 mint — the rule that broke five times on 08-02.)*
> - **926** = **Combat anim: legs/hips, foot slide, recovery, shield clip** (Imagine review P2).
>   File `WORK_ORDER_926_combat_anim_root_motion_recovery.md`. **SPEC / owner priority.**
> - **925** = **Kill/condition permanent foot fire VFX** under hero (Imagine — always-on sparks).
>   Instrument HeroHpStateAura first. File `WORK_ORDER_925_kill_persistent_foot_fire_vfx.md`. **READY.**
> - **924** = **Kill neon-green exit/climb debug volumes** — DungeonExitInteractable Unlit beams +
>   EXIT labels; stop pairing Climb/Descend with debug pillars. File
>   `WORK_ORDER_924_dungeon_green_debug_exit_climb_volumes.md`. **READY.** Map:
>   `REVIEW_MAP_IMAGINE_DUNGEON_2026-08-07.md`.
>
> *(banner bumped 924 → 927 in the SAME edit as the 924–926 mint — the rule that broke five times on 08-02.)*
> - **923** = **Walkable multi-level stairs** — prefab kit (visual steps + invisible Cube ramp on
>   nose line, NOT Plane); rise=FloorSeparationY 6m; PathComplete on all multi-level bakes; retire
>   Descend ports when stair present. Source: `HANDOFF_GROK_DUNGEON_MULTILEVEL_NAV.md` + owner video.
>   File `WORK_ORDER_923_walkable_stair_prefab_kit.md`. **READY.**
>
> *(banner bumped 923 → 924 in the SAME edit as the 923 mint — the rule that broke five times on 08-02.)*
> - **922** = **RoomForge: all rooms much wider** — master `Cell` 6→**10** m (optional 12);
>   1×1 rooms 6×6→10×10; rebuild prefabs + recompose graphs + rebake. Combine bake with WO-919.
>   File `WORK_ORDER_922_roomforge_wider_rooms.md`. **READY.**
>
> *(banner bumped 922 → 923 in the SAME edit as the 922 mint — the rule that broke five times on 08-02.)*
> - **921** = **Dungeon fire cosmetic vs hazard** — torch_lit + intensity-2 lights make rooms look
>   “encased in fire” but do zero damage; real traps (spike/grate) damage but are invisible; no fire
>   kind. Dial cosmetic torches, telegraph traps, optional fire trap kind off spawn.
>   File `WORK_ORDER_921_dungeon_fire_cosmetic_vs_hazard.md`. **READY.**
>
> *(banner bumped 921 → 922 in the SAME edit as the 921 mint — the rule that broke five times on 08-02.)*
> - **920** = **Dungeon camera: stationary exploration** — default OFF free-look FPV; locked OTS;
>   kill AvoidObstacles bounce; calm combat framing (prefer no FPV↔OTS thrash). Owner: camera
>   bouncing + wants stationary dungeon view. File `WORK_ORDER_920_dungeon_stationary_camera.md`.
>   **READY** (prefer after 919 enclose). Updates `DungeonFpvRegression` deliberately.
> - **919** = **RoomForge enclose: taller walls + ceilings + kill blue sky.** Composed rooms are
>   2.8 m open-top boxes (`DefaultDungeonRoomsBuilder`); baker never fog/sky-kills. Owner shots
>   2026-08-07 show half-frame blue sky. Raise walls ≥4 m, ceiling pass, Healer’s ambient recipe,
>   re-bake composed layouts. File `WORK_ORDER_919_roomforge_enclose_taller_walls_ceilings.md`.
>   **READY.** WO-1000 remains the separate KayKit **outpost** builder.
>
> *(banner bumped 919 → 921 in the SAME edit as the 919–920 mint — the rule that broke five times on 08-02.)*
> - **918** = **Board hygiene: close shipped WOs + RESULT files** for the audit five-findings
>   (`f329c8d5`), WO-899 PARTIAL, WO-1001, without closing READY VFX (890/892/1002). Notion mirror.
>   File `WORK_ORDER_918_board_hygiene_close_shipped_wos.md`. **READY.**
> - **917** = **WO-899 §4 residual** — dodge icon + empty skill-slot “+” placeholder. Stick/compass/
>   attack landed in `a35163e1`; §4 deliberately not smuggled (no style-matched dodge art yet).
>   File `WORK_ORDER_917_hud_dodge_icon_empty_skill_slot.md`. **READY** (owner art pick if no icon).
> - **916** = **Marketing site vercel --prod** — repo tagline is canon (“Echoes of a Forgotten
>   Civilization”); production may still serve retired “last light” until verified deploy.
>   File `WORK_ORDER_916_site_canon_tagline_vercel_prod.md`. **READY.**
> - **915** = **RealmStorePurchase public-release re-gate + payment path.** Q9 turned Buy ON for the
>   sole tester; mainnet hard-block + empty SkrMintDevnet remain ship blockers. Owner rules A/B.
>   File `WORK_ORDER_915_realm_store_public_release_regate.md`. **READY FOR OWNER RULING.**
> - **914** = **Status mount: compass strip vs waveBlock layout.** WO-899 widened the strip; no UI
>   capture; calm posture co-occupies both widgets — measure rects first, fix only if collision.
>   File `WORK_ORDER_914_status_mount_compass_waveblock_layout.md`. **READY.**
> - **913** = **Arcane Element==visual regression.** `BoltVisualElement` is Aether in source but
>   `TowerProjectileMapRegression` never asserts Element/BoltVisualElement — Flame can ship green again.
>   File `WORK_ORDER_913_arcane_element_equals_visual_regression.md`. **READY.**
>
> *(banner bumped 912 → 919 in the SAME edit as the 913–918 mint — the rule that broke five times on 08-02.)*
> - **912** = **Ad revenue for the FREE PATH** (provider, rolling window, remote config, ad-boost packs).
>   File `WORK_ORDER_912_ad_revenue_free_path.md`. **READY FOR OWNER RULING.** ⚠ Was on disk while the
>   banner still read next-free 912 — reconciled 2026-08-07; do not re-mint 912.
> - **911** = **Timer speed-ups actually available** — Instant crystals + Ad skip on ALL channels
>   (Builder/Train/Research); root cause: Instant only resolved Builder + dead Ad hid all CTAs.
>   Crystal packs stay existing currency (no new type). File
>   `WORK_ORDER_911_timer_speedup_crystals_all_channels.md`. **READY.**
>   ⚠ Also on disk: `WORK_ORDER_911_unified_queue_screen.md` (second 911 title — historical collision;
>   do not mint another 911).
> - **910** = **Ranger + Mage talent trees: 31 player-reachable nodes have no consumer.** Surfaced the
>   moment `TalentStrategyRegression`'s `HiddenTrees` was emptied — it had hardcoded `{"ranger","mage"}`,
>   so guard G3 had NEVER audited 40 player-reachable nodes while reporting green. **Ranger collapses to
>   ONE usable talent out of 20, Mage to five; both lose their entire tier-4 capstone row.** Knight (32)
>   and shared (9) are fully green, so this is isolated to the two classes unlocked 2026-08-05.
>   ⚠ **Hiding was CONSIDERED AND REJECTED** — `HeroTalentNodeDef.Hidden` had ZERO runtime readers
>   (its own comment lied), so `"hidden": true` would have turned the gate green while leaving every node
>   clickable; and hiding strands three whole tiers + orphans three nodes. `Hidden` is now genuinely
>   wired, so an owner ruling to hide will actually work. The 31 are tracked as a dated, ratcheted
>   baseline: new debt fails, and a baseline id that stops being dead ALSO fails.
>   File `WORK_ORDER_910_ranger_mage_talent_consumers.md`. **READY FOR OWNER RULING.**
>
> *(banner bumped 910 → 911 in the SAME edit as the 910 mint — the rule that broke five times on 08-02.)*
> - **909** = **Activate Mage + Ranger in character selection (re-enable + verify).** Owner: create a WO
>   for CLI to make Mage/Ranger selectable. Gate `FeatureFlags.KnightOnly` already default-OFF
>   (`9a0ff548`); WO-861 landed kits/loadout/portraits/copy/rename — so this is a **re-enable + verify +
>   body-mesh finish**, not a build. Real open risk = Mage/Ranger body mesh (parked `.tripo-extracted`
>   FBX → Blink base vs KayKit body). Owner steer: *"Mage should obviously live heavily in that realm"* →
>   Mage is the magic/VFX showcase. File `WORK_ORDER_909_activate_mage_ranger_character_select.md`. **READY.**
>
> *(banner bumped 909 → 910 in the SAME edit as the 909 mint — the rule that broke five times on 08-02.)*
> - **908** = **Side menu: duplicate gear icon + wrong icon formatting.** Owner felt-test on the Seeker
>   (2670x1200): the left-side menu expands correctly, but TWO gear glyphs render in two different
>   styles — a gold/tan boxed gear seated on the **Music** row and overhanging the panel's left border,
>   and a grey outline gear drawn on top of the **"S" in "Settings"**. One icon, one style, seated in
>   its row. ⚠ Suspect the fraction-band / `ClampMinTouch` centre-grow class that broke WO-852/868/865
>   and both founding screens on 08-05 — check for a fraction-positioned band FIRST. Screenshot attached
>   in-repo at `docs/qa/screens/2026-08-05/gear-menu-double-icon.png`. Owner is routing this to the UI
>   team. File `WORK_ORDER_908_gear_menu_duplicate_icon.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 908 → 909 in the SAME edit as the 908 mint — the rule that broke five times on 08-02.)*
> - **907** = **Elemental affinity — towers, enemies, and a match bonus that is never a lock.** Owner:
>   *"each tower could land a different affinity"*, *"they could both apply"* (visual AND damage), and —
>   asked whether enemies carry an element — *"they don't yet but should."* ⚠ **Governing rule is the
>   EXISTING Echo grammar (CLAUDE.md §7 / WO-830): a MATCH BONUS, NEVER A LOCK.** No tower may become
>   useless against an enemy type. ⚠ Only `tower_arcane_spire` authors an element today (Aether); the
>   other four author NONE, and enemies author none at all — **tower affinity without enemy affinity is
>   half a system, both land together or neither ships.** `IDamageable.cs:61` already documents the
>   element param as *"used for resist / bonus math"* — §4.1 is to find out whether that math exists,
>   is unwired, or was never written. ⚠ **Gates part of WO-870: element FIRST, visual SECOND** — picking
>   VFX before elements reproduces the exact Arcane Spire defect (Aether damage, Fire visuals).
>   ⚠ Balance blast radius: this re-opens WO-855's tower cost/DPS band hours after it landed.
>   File `WORK_ORDER_907_elemental_affinity_system.md`. **SPEC.**
> - **906** = **Catapult becomes a DEPLOYED offensive siege unit** (owner: *"deploy offensively"*). Moves
>   it between SYSTEMS — StructureFactory/DefenseTower → TroopController/TroopDeployer — so it is NOT a
>   tag change. Currently authored as its opposite: `behaviorId: DefenseTower`, range 28, a placed
>   structure, and unreachable anyway (the build menu lists only the cheapest FOUR of five tower rows).
>   Named failure mode: half-of-each. WO-853's damageable walls/gates/towers is what makes a siege
>   weapon meaningful at all. File `WORK_ORDER_906_catapult_deployable_siege_unit.md`. **SPEC.**
>
> *(banner bumped 906 → 908 in the SAME edit as the 907 mint — and correcting a 906 mint that went to
> disk WITHOUT a bump earlier tonight, which is the exact rule this banner exists to enforce.)*
> - **905** = **"Manage" — one screen for every upgrade, sorted by what you can afford.** Owner: a Manage
>   section under Bag showing all three rails with drill-in, because *"not sure what they can afford"*.
>   ⚠ **The content tabs and the queue channels CROSS**: Defensive structures AND Building upgrades both
>   run on the **Builder** channel and share one rail; troop upgrades are Research. **V1 ships THREE tabs**
>   (defensive / buildings / troops); weapons + armor are FUTURE and have **no queue at all** —
>   `GearProgression.Improve` is instant ("instant V1 — no job/channel"), the only sink costing resources
>   but no time. **Deliverable, not a side effect: the always-on queue panel comes OFF the play HUD once
>   Manage is reachable — Manage first, removal second.** Rationale worth keeping: discoverability by
>   walking is not discoverability. Drill-in reuses the EXISTING `BuildingUpgradePanelMvvm` (83 KB,
>   already registered); do not build a second upgrade panel.
>   File `WORK_ORDER_905_manage_screen_upgrade_browser.md`. **SPEC — depends on WO-864's rail component.**
> - **904** = **Fortification: upgradeable walls AND gates.** Walls already upgrade (`wall_wood`/`wall_stone`
>   author `maxLevel:3` + a 2-rung `upgradeCost`) and WO-853 made them damageable — but **`gate_stone`
>   authors NO `maxLevel` and NO `upgradeCost`**, so the verb answers `1 >= 1` and toasts "Max tier
>   reached" on a fresh gate. A perimeter is only as strong as its weakest authored point; a raider walks
>   the door while the reinforced walls stand untouched. **Blocked on raid-steal by design** — fortification
>   before there is anything to lose is a cost with no reason to pay it.
>   File `WORK_ORDER_904_fortification_walls_and_gates.md`. **SPEC.**
>
> *(banner bumped 904 → 906 in the SAME edit as the 904 + 905 mint)*
>
> ### ⛔ THE MAIN LINE HAS COLLIDED WITH THE UI-SEAT BLOCK — READ BEFORE MINTING
> The main line consumed 859 and the next number, 860, is **inside the UI seat's reserved 860–899**
> (860/861/862/863 already consumed there). **The two blocks have MET.** The main line therefore
> **jumps to 900+**. Any main-line mint below 900 from here is a guaranteed collision.
>
> ⚠ **THIS PARAGRAPH WENT STALE AND IS CORRECTED 2026-08-07.** It read "the UI seat keeps 860–899
> (next free 864)". That is no longer true and was the SAME self-contradiction that seeded the
> earlier collision — a head that says one thing and a body row that says another. **Owner ruling:
> the UI seat moved to 1000–1099**; 860–899 is CLOSED (full at 899), and 1000 / 1001 / 1004 / 1005
> are already minted in the new block. The main line's next free is the HEAD BANNER (923), which is
> the sole authority — never this paragraph, never a number copied anywhere else.
>
> - **903** = **Storage pallet fill stacks (SMALL)** — lumberyard/foundry/silo show logs/ingots/sacks
>   as bank fill rises (~5% steps); reuse CollectorStackView/prop catalog. No economy rewrite.
>   File `WORK_ORDER_903_storage_pallet_fill_stacks.md`. **READY.**
> - **902** = **Archer Tower medieval castle visuals (Option A)** — retire Tribal T1–T3 for
>   `tower_ground_archer`; L1 `Tower_Castle_Round` → L2 `Tower_Castle_Square` → L3 `Tower_Medieval_Big`.
>   Catalog dual-copy + mirror Square into Resources if missing. No combat rewrite.
>   File `WORK_ORDER_902_archer_tower_medieval_castle_visuals.md`. **READY.**
> - **901** = **THE COLLECTOR LOOP (umbrella)** — owner directive "consolidate those into one idea and
>   implement". One idea: *your town keeps producing while you are away, into containers that visibly
>   fill to a cap and then stop, and storage raises what the town can hold.* Folds 857/858/859/900 into
>   one sequence (phases 0/A–G) and rules on their overlap. **⚠ Ruling: Grok's 858 icon half and CLI's
>   900 tell are the SAME FEATURE — `CollectorStackView` (437 lines) already implements it and `Attach`
>   has ZERO CALLERS. WIRE IT, do not build it.** Phase F (wallet clamp) deliberately WITHHELD from the
>   autonomous pass — it clamps `EconomyService.Grant`, which every income path flows through.
>   File `WORK_ORDER_901_the_collector_loop.md`. **IN PROGRESS.**
> - **900** = **Collector "I am full" tell** — appendix of 901, phases D/E. `CollectorStackView.Attach`
>   has zero callers (recorded WO-783:186 + `UiObsidianConformanceRegression.cs:168`, never fixed): a
>   WIRING fix, not a UI build. HUD chip via a Core status gate mirroring `ObsidianQueueGate` — NOT
>   `IVillageHud` (that is imperative push; this is a polled snapshot). No new reflection.
>   File `WORK_ORDER_900_collector_full_tell.md`. **READY.**
> - **859** = **Per-collector capacity in HOURS + offline accrual** — appendix of 901, phases 0/A/B/C.
>   Collectors have NO offline accrual (zero consumers of `LastHarvestClaimMs`), and the capacity curve
>   **runs backwards**: capacity grows x3 L1→L5 while throughput grows x5.6, so upgrading a collector
>   SHORTENS unattended runtime (6-echo L5 farm fills in **5.7 min**). ⚠ **Carries a P0 `35485f31` did
>   NOT close: `ResourceCollectorBootstrap.EnsureFallbackCollector` creates live collectors
>   UNCONDITIONALLY without consulting `everBuiltStructureIds`** — a blank town earns again, and full
>   town income accrues while the player is in a DUNGEON. Prove headless before editing (§12).
>   File `WORK_ORDER_859_collector_capacity_hours_and_offline_accrual.md`. **READY.**
>   ⚠ **Renumbered from a collided 858 mint** — Grok's 858 was first-on-disk-and-referenced and wins.
>
> *(banner bumped 859 → 902 in the SAME edit as the 859 + 900 + 901 mint)*
> - **858** = **Collector resource icons + high-value invasion targets** — billboard wood/iron/food/crystal
>   icons when pending (tap=Collect); catalog siegeValue/highValueTarget for premium collectors.
>   File `WORK_ORDER_858_collector_resource_icons_and_siege_value.md`, READY.
> - **857** = **CoC resource storage caps + HUD have/max** — bank max from lumberyard/foundry/silo
>   (`storageCapacity`) + baseCap; clamp grants; chips `current/max`. Collectors stay pending-only.
>   File `WORK_ORDER_857_coc_resource_storage_caps_hud.md`, READY.
> - **856** = **Crystal Mine actually pays out** — `mine_crystal` has never yielded a single crystal and
>   cannot: payout is gated at L3 (`CrystalMine.cs:188`), `_currentLevel` is a private field that persists
>   NOWHERE, and the catalog authors no `maxLevel`, so the upgrade verb answers `1 >= 1` and toasts
>   "Max tier reached." on a freshly-built mine. Root cause is the ORACLE:
>   `CrystalProductionRegression.cs:63-66` reflectively writes `_currentLevel` to max — a state no player
>   can reach — while claiming to prove yield "at a reachable level". Fix pulls the level from the
>   existing persisted `PlacedStructureData.level` (do NOT add a 4th level authority) and authors a
>   `[2,4,7]` per-wave curve. File `WORK_ORDER_856_crystal_mine_pays_out.md`. **READY.**
>   Spawns three separate WOs, NOT folded in: jeweler-as-crystal-upgrader (new feature, 5 ordered
>   steps — author the ladder LAST); `HealingFountain` (identical bug, and worse: it authors
>   `maxLevel:3` AND keeps the Coins F-key path, so two systems can each level one building); and a
>   generic `ApplyTierStats` level-receiver seam.
>
> *(banner bumped 856 -> 857 in the SAME edit as the WO-856 mint — the rule that broke 5x on 08-02)*
> - **855** = **Economy balance (mobile grind)** — data-first: tower/troop/gear costs, build+upgrade
>   times, gather yields, difficulty light pass, **generic tower spam softcap** (cost mult only).
>   NO system rewrites. File `WORK_ORDER_855_economy_balance_mobile_grind.md`, READY.
> - **854** = **Quest Completability Program** — owner ruled that a quest which can be ACCEPTED and TRACKED
>   but not completed is a BUG. Audit found **0 of 63 stages completable**: `QuestService.AdvanceQuest` has
>   exactly ONE caller and no shipped dialogue names any of the 24 quest ids. 7 phases behind a
>   `QUEST_REACH_OK <n>/63` oracle + ratchet. Adds `completeOn` to `QuestStage` (**no save bump** — catalog
>   content, not the persisted contract). File `WORK_ORDER_854_quest_completability_program.md`.
>   **READY (P0-P2, zero owner deps); P3-P7 gated on the §6 ruling set.**
>
> *(banner bumped 855 -> 856 with WO-855 economy mint)*
> - **853** = **Structures are targetable** — the disjoint-contract seam. `WallSegment.cs:28` + `Gate.cs:45`
>   implement `IDamageableStructure` while `TroopController.cs:449-469` sweeps for `IDamageable`; the two
>   are disjoint, so nothing can damage a wall, gate or enemy tower and "Razed %" counts bodies. Extends
>   the `RaidSpire` dual-interface precedent and gives `CombatFaction.Friendly` its first real producer.
>   ⚠ walls must STAY on layer `Structure` (it is the tower LoS mask). File
>   `WORK_ORDER_853_structures_are_targetable.md`. **READY** — one owner decision open (scoring weights).
>
> *(banner bumped 853 → 854 in the SAME edit as the mint — the rule that broke 5x on 08-02)*
> - **851** = every-4th-wave BOSS encounters + statistical adaptation (owner rulings: statistics not
>   AI, every 4th wave, boss enemies at boss scale, Syndrath's flair — JSON-driven HP bar + boss
>   music reusing the least-used clip). File `WORK_ORDER_851_every_fourth_wave_adaptation.md`.
>   **SHIPPED as spec in `0bb46258` — keeps 851 (first-on-disk-and-referenced).**
> - **852** = Echo card fixed-band layout (UI-seat RCA: the WO-830 resource picker's 1/n fraction
>   slices collapse below MinTouchPx and the buttons stack up into the info block; same class as
>   WO-832 §4 / WO-841 fraction-band culling). **Renumbered from a collided 851 mint — the CLI's
>   851 was already committed. THE COLLISION WAS THE CLI'S FAULT: it wrote 851 to disk without
>   bumping this banner, so the UI seat correctly read "next free = 851".** READY.
> - **⚠ FIVE collisions on 2026-08-02 alone.** The banner is only an authority if it is bumped in
>   the SAME edit as the mint — including by the CLI. Owner ratification of a reserved UI block
>   (860–899) is now overdue.
>
> ## ⚠ TWO-BLOCK ALLOCATION IN USE (2026-08-02 evening) — the collision fix, in practice
> | Block | Owner | Next free |
> |---|---|---|
> | **main line** | CLI | **928** (782–859 + 900–**927** consumed; 860–899 was the UI seat's old block) |
> | **1000–1099 reserved** | UI seat | **1006** (1000–1005 consumed) |
>
> *(UI-seat bumped 1005 → 1006 with the WO-1005 mint — dungeon UI cohesion: reskin the flat-purple "Descend"
> prompt to the Obsidian kit + fix the mirrored "EXIT" world label + one obsidian-gold theme for all dungeon overlays.)*
>
> *(UI-seat bumped 1004 → 1005 with the WO-1004 mint — composed-dungeon (Pipeline A) visual fixes: kill the
> rainbow-atlas floor, strip stray purple/green debug/socket/magenta markers from the build, and extend the
> WO-1000 enclose+relight (ceiling, dark ambient+fog, candle-VFX light) to the composer so every baked dungeon is clean.)*
>
> *(UI-seat bumped 1003 → 1004 with the WO-1003 mint — replace town NPCs (KayKit adventurers + CGTrader
> civilians) with the CraftPix Free Medieval People pack (14 dressed townsfolk, shared atlas, license
> commercial-green), staged tracked in Resources/NPCs/People, Humanoid-retargeted onto the shared animator.)*
>
> *(UI-seat bumped 1002 → 1003 with the WO-1002 mint — remove the yellow aura plume at the hub Heart of
> Elarion tree base (HeartAuraController tree-ambient loop; extend the hub withhold to cover it).)*
>
> *(UI-seat bumped 1001 → 1002 with the WO-1001 mint — Deep Dungeon Program: extend Pipeline A (JSON
> room-graph composer) into a full complex-dungeon engine (deep multi-level stairs, enemy families, boss
> wiring, loot/chests, oil/darkness risk-reward), then three large themed deep dungeons authored as graphs.)*
>
> *(UI-seat bumped 1000 → 1001 with the WO-1000 mint — Starter dungeon (KayKit Challenge Outpost) visual
> overhaul: enclose the top / kill daylight, KayKit textured shell + ceiling, candle-VFX lighting, fog/haze,
> real props — to the Healer's Cottage bar.)*
> | ~~860–899~~ | ~~UI seat~~ | ⛔ **CLOSED — 860–899 ALL CONSUMED.** Last mint 899 = HUD polish (analog joystick + wide compass + attack/dodge blend + empty-slot "add skill"). Do not mint here again. |
>
> ### ⚠ OWNER RULING 2026-08-07: the UI seat moves to the **1000s**.
> Her words: *"we can move to 1000's."* The old 860–899 block filled up, and the previously
> *recommended* "913+" was **WRONG and would have collided** — the CLI main line is at next-free
> **912** and climbing, so 913 is the CLI's very next number but one. The two blocks must stay
> **DISJOINT**, which is the entire point of the two-block scheme (five collisions in one day,
> 2026-08-02, all caused by two seats sharing a number space).
>
> **1000–1099 is the UI seat's. The main line stays below 1000 and must never cross it.**
> If the main line ever approaches 1000, allocate the CLI a fresh block rather than eating into
> this one. Each seat still bumps ITS OWN row in the SAME edit as its mint — that rule is unchanged
> and is what actually prevents collisions; a disjoint block only removes the chance of a tie.
>
> *(UI-seat bumped 898 → 899 with the WO-898 mint — queue progress bars + "Complete now" with crystals
> (any item/channel; 5-min-bracket cost, flat under 5 min). ⚠ **899 is the LAST number in the UI-seat
> 860–899 block — the block is now full after one more mint; a new UI-seat range must be allocated.**)*
>
> *(UI-seat bumped 895 → 898 in the same edit as three mints: WO-895 building-upgrade "next-only" redesign +
> stateful button, WO-896 skill-tree connected-progression-line redesign, WO-897 army composition auto-queue.
> 894 = Victory screen spinning stars.)*
>
> *(⚠ **Row corrected 2026-08-06:** the main-line cell read `910` while the RECONCILED banner at the top of
> this file — written in the same edit as the WO-910 mint — already said next free = **911** and
> `900–910 CONSUMED`. The file contradicted itself, which is precisely how a collision starts. The
> top banner wins; this row now agrees with it.)*
>
> *(UI-seat bumped 894 → 895 in the SAME edit as the WO-894 mint — Victory screen: real spinning 5-point
> stars + exact wireframe layout, replacing the diamond/no-spin BuildStarRow in EndStateView.)*
>
> *(UI-seat bumped 885 → 894 in the SAME edit as the WO-885–893 VFX mints — 885 umbrella index +
> 886 death · 887 on-hit · 888 heal/HP/item auras · 889 combat auras/nearest-N · 890 harvest ·
> 891 healer structure · 892 building damage · 893 portals/spawn/dissolve. Earlier: 884 VFX facade,
> 909 Mage/Ranger. Main-line mirror corrected 908 → 910 after the 909 mint.)*
>
> ⚠ **This table drifted AGAIN (corrected 2026-08-05).** The UI-seat row read `864` while
> `WorkOrders/` holds an unbroken 860→883 — twenty numbers stale, and 864 itself is not only
> consumed but is cited as a live dependency by the WO-905 spec ("depends on WO-864's rail
> component"). Three of the range (878/879/880/881/882/883) shipped in commits `31888576`,
> `d185f43c`, `572f1289`. Minting from this row would have collided on the first try — the exact
> failure that struck five times on 2026-08-02. `CANON_GROUND_TRUTH_2026-08-05.md` §8 already had
> 884 right; the SOLE AUTHORITY was the file that was wrong.
>
> ⚠ **Prior drift (2026-08-04).** It read `853` while the header row above it read `856` — two
> numbers in ONE file, the same two-authority failure the header warns about. The header is the
> authority; this table is a convenience mirror. **Bump BOTH rows in the same edit as a mint, or
> delete this table.**
>
> - **863** = Vercel one-pager + hosted privacy policy (the two dApp Store listing URLs). File
>   `WORK_ORDER_863_vercel_landing_and_privacy_page.md`, READY.
>   ⚠ **Banner reconciled 2026-08-04 by the CLI: 863 was minted to disk without the banner being bumped**,
>   so the banner was still offering it as next-free and the CLI nearly minted over it. Same failure that
>   struck 5x on 08-02. The rule is unchanged and it is the only one that matters here: **bump YOUR row in
>   the SAME edit as the mint.**
>
> - **860** = start loadout (sword+shield, not the stale axe) + weapon/armor shelf thinning. UI seat. IMPLEMENTED (lane agent), pending gate.
> - **861** = Sylas + Thrain playable (re-enable, not build-new; appendix carries the approved kits/trees/Cathedral map). UI seat. IN FLIGHT.
> - **862** = UI-seat fix WO (minted 2026-08-02 evening from the reserved block).
> - The blocks are DISJOINT, so both seats can mint in parallel without reading each other's state.
>   Each seat bumps ITS OWN row in the SAME edit as the mint. This is the rule that was broken 5x today.
> - **848** = RESTORE Android managed stripping Medium (lowered to Low 2026-08-02 because the
>   WO-766 Solana SDK's BouncyCastle.Cryptography fails the CIL-linker resolve at Medium;
>   captured in Builds/android-build.log + MobileSettings.cs comment). APK-size follow-up, OPEN.
> - **849** = dungeon PURSUIT bound (F8 seq 629 "not attacking me"): WO-797's flat wander slack
>   pinned engaged mobs on their room boundary while the hero stood 3.7m outside. Pursuit now
>   clamps to max(slack, wakeRadius) — "a mob may pursue as far as it can perceive"; the entrance
>   camp stays fixed (8.1m > wake 6). SHIPPED, oracle case 7 pins both halves.
> - **850** = deepest-room TREASURE cache (owner request 2026-08-02: "treasure at deepest, simple
>   crafting supply") — chest at the dungeon's deepest room granting basic crafting materials. OPEN.
> - **⚠ the proposed UI-seat reserved block moves to 860–899** (850–859 now consumed/reserved by the
>   main line; owner still to ratify).
> - **842** = dual-wallet unify (GameState = single Wood/Iron authority; the 985k-can't-afford-800 capture) ·
>   **843** = destroyed/sold singleton cards rebuildable (IsPlayerBuilt split from IsBuilt) ·
>   **844** = Bag potions apply their real effect (was TryRemove + lie) ·
>   **845** = login error mapping + password reset ("Internal error" F8) ·
>   **846** = bug-report attribution + notify (playerId = BoundWallet; api bugreports view; watcher trio) ·
>   **847** = wallet-first Android login ("connect wallet or play as guest"; desktop keeps email).
>   All SHIPPED in commits `a7e4acb2` / `731840e7` (2026-08-02).
> - **839** = raid deploy screen cleanup (UI seat; renumbered from a collided 834 mint) ·
>   **840** = armorer reachability + shop panel cleanup (UI seat; was 835) ·
>   **841** = upgrade panel countdown live-tick (UI seat; was 836). All READY, specs on disk.
> - **⚠ PROPOSED RULE (owner to ratify — the collision struck 3x on 2026-08-02 alone):**
>   **the UI seat mints ONLY from a reserved block 850–899**; the CLI mints the main line from this
>   banner. Two seats can then mint in parallel without collision; the CLI reconciles 850-block WOs
>   into the main sequence only if/when renumbering is ever needed. Until ratified, the CLI keeps
>   renumbering collisions by first-on-disk-and-referenced-wins.
> - **837** = Stockpiles cap resource capacity (owner ruling: lumberyard/foundry/silo(="Quarry"?) are
>   the stockpiles — OUT of the FoundingKit array; their storageCapacity becomes the live wallet-cap
>   mechanic; founding_stores tutorial re-spec). File `WORK_ORDER_837_stockpiles_cap_capacity.md`, READY.
> - **836** = MASTER_CATALOG full SME refresh (owner-ordered 14-agent fleet, docs-only). File
>   `WORK_ORDER_836_master_catalog_sme_refresh.md`, IN FLIGHT.
> - **835** = HUD action bar: show only APPLICABLE buttons, re-packed (UI-seat spec; renumbered from a
>   collided 833 mint — KayKit idle keeps 833). Two OWNER CONFIRM defaults inside (hide Raids until
>   discovery; constant-width vs stretch). File `WORK_ORDER_835_hud_action_bar_applicability_repack.md`, READY.
> - **834** = Blank-founding towns: baked default-town structures stand DOWN until first player build
>   (everBuiltStructureIds, save v36, blank-town gate on every surfacing path — 4 seams). File
>   `WORK_ORDER_834_blank_town_baked_standdown.md`, IMPLEMENTED pending gates. *(Renumbered from a
>   collided 832 mint — the UI seat's 832 below keeps the number.)*
> - **833** = KayKit NPC idle animation (T-pose F8 fix: shared KayKitNpcIdle.controller retargeting the
>   Knight mocap m-standby-idle onto the 12 Humanoid bodies; oracle-gated). File
>   `WORK_ORDER_833_kaykit_npc_idle_animation.md`, IMPLEMENTED pending gates.
> - **832** = Building-upgrade panel: ONE true gold Upgrade button (tab demoted to underline-tab,
>   in-card gold button removed; UI-seat spec). File `WORK_ORDER_832_building_upgrade_one_true_button.md`,
>   IMPLEMENTED pending gates.
> - **830** = Echo harvest affinity + synergy (all 6 echoes -> unique harvest affinities Wood/Iron/Food/Gold/Crystals/Repairs, 3 disclosed pair-synergies, 1 hidden tri-synergy; UI-seat minted). **831** = Echo emergence 2D sprite beat (new sprite + dialogue advance at unlock, no 3D). Files `WORK_ORDER_830/831_*.md`, READY.
> - **825–829** = **IMMERSIVE WORLD / REALM MAP** program. Master **825**; children:
>   **826** parchment Realm Map UI (`realm-map.json`), **827** discovery+travel+ZoneManager identity,
>   **828** cheap live minimap, **829** Withering/biome/content pins. Files `WORK_ORDER_825`…`829_*.md`, READY.
> - **824** = CoC+WC3 **PLAYER ENJOYMENT** master program: PO fun bar + binding ship Waves 0–6
>   (817 glance → 822 teach → 774 deploy → 809 readiness → 800/805/821/799 → 806/807 → stakes/spice).
>   Gap fills: soft first-raid ruling, Work empty-state teach, hub truth pass. Does NOT re-implement
>   children. File `WORK_ORDER_824_coc_wc3_player_enjoyment_program.md`, READY.
> - **823** = Post-review HARDENING pack: `ArmyReadiness.Compute` single source (rewire 820 Publish+Open),
>   founding Echo soft-deadline, over-queue/readiness EditMode oracles, 819/820 RESULT hygiene.
>   Does NOT own teach/KayKit/queue visual/perks (822/818/817/821). File
>   `WORK_ORDER_823_post_review_hardening_pack.md`, READY — **implement 823 Phase A before 822**.
> - **822** = Barracks teach v2 (813b): coach beat + world marker + Train-3 quest + first-raid tip +
>   presence oracle; intro key claimed only when the beat completes (review: "toasts are not teach").
>   File `WORK_ORDER_822_barracks_teach_v2.md`, READY (depends on 823 ArmyReadiness).
> - **821** = Building perk research TIMED + QUEUED on the Research channel + skills-tab timers
>   (owner F8 seq 545; naming half "Swift Recruitment -> Conditioning Drills" shipped same session).
>   File `WORK_ORDER_821_timed_perk_research.md`, READY.
> - **820** = Raids gated on FULL army (grey + drillmaster redirect) + over-queue exploit fix.
>   **IMPLEMENTED 2026-08-01**, awaiting gate + PO felt-verify. File `WORK_ORDER_820_raid_full_army_gate.md`.
> - **819** = StructureSingleton common v2 (catalog-driven `repo.bakedTwins`, zero-code enforcement,
>   sell-resurfaces-bake, CheckSingletons oracle). **IMPLEMENTED 2026-08-01**, awaiting gate + PO
>   felt-verify. File `WORK_ORDER_819_structure_singleton_common_v2.md`.
> - **818** = KayKit NPC body per structure (owner-approved 12-row mapping stored in
>   structures-catalog `repo.npcModel`; stager built, catalog+injector phases queued behind 819).
>   File `WORK_ORDER_818_kaykit_structure_npc_models.md`, IN PROGRESS.
> - **817** = **MASTER queue visual: CoC channels + WC3 production glance** (icon+bar+pending strip;
>   phases 0–6). Folds 798/801/816. Engine frozen. File
>   `WorkOrders/WORK_ORDER_817_coc_wc3_queue_visual_system.md`, READY.
> - **816** = Queue timer bars (= 817 Phase 2). File `WORK_ORDER_816_*`.
> - **813** = Barracks discovery/teach (B+C). **Depends on 812.** File `WORK_ORDER_813_*`.
> - **812** = **ADD Barracks placeable** (catalog + free first place + train entry). Authority for presence.
>   File `WORK_ORDER_812_introduce_barracks.md`. ⚠ **NUMBER COLLISION:** Claude also wrote
>   `WORK_ORDER_812_echo_harvest_choice_and_affinity.md` — **renumber that to 815** before implement;
>   barracks introduce keeps 812.
> - **814** = gear max-level ability (Claude). File `WORK_ORDER_814_gear_max_level_ability.md` — triage later.
> - **811** = Echo gather wood/iron/food OR repair. **810** = Rumor Board layout.
> - **Program hub:** `docs/WC3_COC_EXPERIENCE_ANALYSIS.md` — includes §2A army ladder (unlock→train→troop L→gear→readiness).
> - **806** = Barracks progression spine UX. **807** = troop power readability. **808** = hero gear levels
>   (**Option A LOCKED**). **809** = war readiness score. Files `WORK_ORDER_806`–`809_*`.
> - **800** = building focus card unify. **801** = queue glance implement (blocked on 798). **802–805** raid/build as prior banner.
> - **798/799/774** still in program hub (queue design, cancel engine, raid P0).
> - **799** = queue CANCEL verb + REFUND plumbing (engine). Panel-row cancel UI waits on 798/801 chips.
>   File `WorkOrders/WORK_ORDER_799_queue_cancel_refund_engine.md`, READY.
> - **798** = WC3 queue VISUAL design (Claude read-only; build on live Builders chip + 5-deep rows).
>   File `WorkOrders/WORK_ORDER_798_wc3_style_queue_visual.md`, READY FOR UI SEAT. Pack: `docs/UI/WO-798_wc3_queue/`.
> - **774** = raid loadout + deploy ring + Army/Deploy naming (CoC P0) — referenced by program hub, already READY.
> - **786–794** = specs on disk (star reveal, WOs 787–792 ticket batch, 793 tree-quest NPC, 794 upgrade verb).
> - **795** = no-stacked-screens scroll standard (owner F8 seq 466 + full 16-panel headed audit).
> - **796** = Room-Forge dungeons bake a REAL hero body (capsule/pill F8; NOT the 782 standee item).
> - **797** = dungeon rooms own their enemies (per-area seating + confinement; entrance-cluster F8).
> - **782** = RESERVED, no file yet — the night-wrap of 2026-07-26 (`docs/qa/NIGHT_WRAP_2026-07-26.md`) already
>   claimed 782 for the **capsule NPC/boss standee** item (re-source `DungeonSceneBuilder` from tracked
>   `Resources/Enemies` + `Resources/NPCs`, re-bake `Dungeon_HealersCottage.unity` editor-closed). Held rather
>   than collided with; write the file under 782 when that work starts.
> - **783** = SME-fan-out fix wave — **IMPLEMENTED this session.** Raid VICTORY now settles the army
>   (`ReconcileAfterRaid` had ONE caller = retreat; `AddVeterancy` had ZERO, so winning was free); Healer's
>   Cottage made REACHABLE (third `AuthoredPortal` row, south seat — the richest dungeon was dev-overlay-only);
>   `[ui-obsidian]` ratchet ARMED + a namespace-qualified regex blind spot closed (it was hiding `OutpostHub.cs`
>   as a false "resolved"); waves.json dead-authoring made LOUD; Echoes-button safe-area inset; FPV headers
>   corrected to match the owner's re-affirmed default-ON ruling. Carries 3 DEFERRED owner rulings (D1 wave
>   authority, D2 the third raid exit via hero death, D3 veterancy pacing).
>   File `WorkOrders/WORK_ORDER_783_sme_findings_fix_wave.md`.
> - **784** = Echo lanes — wire the CONSUMERS. Canon said "3 of 4 stub"; code says **all four** are write-only
>   (even Harvest's Core contract has zero readers — `EchoService.RatePerSecond` bypasses `EchoLaneBonuses`).
>   Phase 1 = make the Core contract the single seam + wire Defense (the owner's ruled "easy one": flat +x% to
>   the whole city defensive package) + open the picker to it + fix the founding-echo identity contradiction
>   (3 of 6 souls have an unreachable calling). File `WorkOrders/WORK_ORDER_784_echo_lane_consumers.md`, READY.
> - **785** = VFX runtime-art survivability. **117 of 121** owner-tagged VFX rows point into gitignored packs
>   (Hovl Studio 59, UnityTechnologies 54, Mirza Beig 3, Spells Pack 1); the catalog is tracked but binds them
>   by GUID, so on the laptop / a fresh clone / CI they all dangle and — unlike the character packs — there is
>   **no runtime fallback at all**. Promote the WIRED set into tracked `Resources/VFX/`, make a missing pack
>   loud, add a resolve oracle. Owner-only creative constraint restated: promote what she tagged, never
>   substitute. File `WorkOrders/WORK_ORDER_785_vfx_runtime_art_survivability.md`, READY.
> - **786** = Raid End: punchy star reveal + audio — **OWNER-AUTHORED spec**, transcribed verbatim.
>   0.45s dramatic hold -> stars slam in left-to-right (0 -> 1.3 -> 1.0, ease-out, screen shake heavier
>   on the 3rd) -> 3-star-only premium layer (gold flash + radial pulse + a **FLAWLESS** stamp) -> 0.4s
>   appreciation beat -> normal victory panel. Total added time <= 2.1s, 60fps on Seeker.
>   **Owner ruled 2026-07-30: ADD DOTween** (`com.demigiant.dotween` via the OpenUPM scoped registry
>   already in `Packages/manifest.json` — headless-doable, no Asset Store download; lands as its own
>   isolated change with an IL2CPP/stripping verification, never folded into another batch).
>   Depends on the star rules, which SHIPPED the same day (WO-783 D3) — note 3 stars is now genuinely
>   rare, so **FLAWLESS** actually means something (under the old formula every victory scored 3).
>   File `WorkOrders/WORK_ORDER_786_raid_star_reveal.md`, READY.
> - **787** = Web-build Sign In surface correctness (owner felt-report 2026-07-30, screenshot). THREE bundled
>   fixes on the one reported symptom: (a) LoginPanelController lays a full-height 0.04–0.97 fraction layout
>   inside `chrome.layout.body`, which WO-714 P6's close-band reservation compresses (body.y raised up to 0.45)
>   → the intro + 2 fields + 4 buttons overlap ("stacked"); the panel HIDES its Close so it should lay on the
>   full-rect `chrome.content` instead. (b) "Sign in with Google" is APK-only — hide `_google` off Android.
>   (c) "Sign in with Pi" must never show in a non-Pi build; when not Pi-facing the skin must resolve to
>   SKR/Solana (CurrencySkinResolver currently defaults to Pi with no Pi-Browser-environment auto-detect).
>   File `WorkOrders/WORK_ORDER_787_web_signin_surface_correctness.md`, READY. Lane 4 UI/HUD + Platform.
> - **788** = Cathedral of Magic aura swap (owner felt-report 2026-07-30, screenshot + owner choice).
>   Replace the `Aegis_Shield` holy-shield-DOME default with the flat **electro magic-circle** ground
>   loop (`Assets/Hovl Studio/Magic circles/Prefabs/Loop version/Magic circle electro loop.prefab`,
>   currently un-keyed in VfxCasterLibraryIndex.json). Tag a new key (e.g. `Cathedral_Aura`) via
>   VfxManualPicks.json overlay + regen the catalog; retag BOTH cathedral surfaces
>   (`StructureFactory.cs:804` + `HubStructureVisualInjector.cs:425`) and update
>   `VfxAuraDifferentiationRegression` expected key. Must stay DISTINCT from node + spire auras
>   (gate enforces 3 distinct keys). File `WorkOrders/WORK_ORDER_788_cathedral_electro_aura.md`, READY.
>   Lane 9 VFX/Audio.
> - **789** = Wave 5 boss swap — replace the TEST-ONLY apex dragon Syndrath (HP 4200) with a lower
>   ground boss **Cave Troll (`troll`)** pinned to **1050 HP** (1/4 of the dragon). Owner felt-report
>   2026-07-30 + owner boss choice. waves.json `waveId:5` carries a self-labelled "TEST ONLY … REVERT
>   before ship" `apexBoss` block; delete it, add `boss` + a wave-level HP override to 1050 (ground
>   `boss` field has no hp override today — add one mirroring `apexBoss.hp`, OR a `troll`@1050 boss
>   variant). Wave 20 keeps the real Syndrath — DO NOT TOUCH. Edit BOTH Resources + StreamingAssets
>   copies. File `WorkOrders/WORK_ORDER_789_wave5_boss_swap.md`, READY. Lane 2 Combat/AI (data).
> - **790** = Outpost/garrison enemy PRESENTATION broken (owner felt-report 2026-07-30, screenshots):
>   flat GREEN/ORANGE textureless enemies + weapon not seated. Green = EnemyFactory designed tint
>   fallback (`EnemyFactory.cs:216-253`) firing because Orc-family meshes ship no albedo; orange =
>   capsule fallback (`:669-677`) on a failed mesh load. Weapon-not-seated = `AttachmentOffsetRegistry`
>   grip untuned + `ff.enemyweapons` gate (`EnemyFactory.cs:185-198,557-576`). Lane 9 VFX/Art.
>   File `WorkOrders/WORK_ORDER_790_outpost_enemy_presentation.md`, READY.
> - **791** = Outpost/garrison enemy AI/placement broken: spawns OFF NavMesh → never moves/chases AND
>   floats above ground (no snap). `EnemyFactory.cs:45-54,318-324` (off-mesh spawn only reported),
>   `Enemy.cs:1000-1008` (DriveNav gated on isOnNavMesh), `EnemyOutpost.cs:565-595` (SnapToNav no-op),
>   OuterWorld navmesh coverage under the outpost anchor (`RaidOutpostSystem.cs:185-217`). Lane 2/5
>   Combat/World. File `WorkOrders/WORK_ORDER_791_outpost_enemy_offnavmesh.md`, READY.
> - **792** = Enemy attacks deal ZERO damage to the hero (owner felt-report 2026-07-30). Enemy→
>   HeroHealth melee path; candidate-level RCA (needs a headless proving read: is HeroHealth.TakeDamage
>   never called, or called with 0?). Lane 2 Combat. File `WorkOrders/WORK_ORDER_792_enemy_zero_damage_to_hero.md`, READY.
> - Granary dungeon "does not work → pops to outpost/arena" = **already ticketed WO-776** (gate the
>   contentless Folk's Granary stub) — NOT YET APPLIED; the stub's invisible `DungeonStubEncounter`
>   insta-warps to `BattleArena`. See `WorkOrders/WORK_ORDER_775_777_dungeon_debt_program.md` + gaps
>   P1-D/P1-H (`docs/qa/GAMEPLAY_GAPS_2026-07-26.md:48,52`).
> Mint the next new WO from THIS line's next-free = **793**; bump it in the same edit.

> ## ⚠ RECONCILED 2026-07-26 (Sunday housekeeping, on `wip`): next free WO = **782**. **761–781 CONSUMED.** (**779** = UI spacing/layout conformance sweep (OWNER-requested) — kill the overlap/clip/truncation class (Echo-flavor-flood, pet-roster-stack, queue clip): layout.body discipline + touch/contrast + ratchet oracle; run AFTER 778; **780** = FTUE first-tower affordability — prepaidTower/crystal grant so the taught build doesn't stall; **781** = wire ArmyStorage.TickRecovery — wounded troops never heal, borderline P0 (renumbered from 779). Files `WorkOrders/WORK_ORDER_779/780/781_*.md`, READY.) (**778** = Queue UX completion — kind-labels+target identity, HUD reachability (P0-A), layout.body/scroll, Barracks Train strip, Train→EnqueueTraining flip, sell-time buttons (P0-B); file `WorkOrders/WORK_ORDER_778_queue_ux_completion.md`, READY.) (**775/776/777** = dungeon-debt program — 775 hero-vitals-from-HeroHealth/HeroAbilities (770.10a), 776 Folk's-Granary gate-off-reachable (770.6), 777 door-consolidation + kill walk-by auto-teleport (770.5); one file `WorkOrders/WORK_ORDER_775_777_dungeon_debt_program.md`, READY.) 761 (structure fire) + 762 (builder queue) + 763 (Wisdom earned) + 764 (hub Y-height) + 765 (capture Default Town) + 766 (Seeker wallet) + 767 (texture caps) + 768 (thin-client migration) + 769 (Firebase auth) all have `WorkOrders/WORK_ORDER_76*.md` files. **770 (dungeon functional loop), 771 (COC Teleport/Deploy raid system, v2), 772 (shared enemy system — classes/families/armor/weapons), 773 (common Obsidian job queue)** are firmed specs in `docs/qa/WORK_ORDER_77*.md` (validation-signed-off, `docs/qa/dungeon-raid-validation-2026-07-26.md`). These four use **decimal sub-orders** (770.1–770.11, 771.0–771.14) — sub-tasks, not new WO numbers. **Status (refreshed 2026-07-26):** 770.1/.2/.3/.3b/.4/.7/.9 DONE; **773 SHIPPED** (multi-channel queue, save v35); **771.9 DONE** (barracks progression, in bank); **772 Phase 1 UNBLOCKED** (Hollow Ones APPROVED per PAIN_POINTS §1.1; Wildlands DEFERRED) — EnemyResolver DONE/wired; 770.5/.6/.8/.10/.11 (=775/776/777 + others) BACKLOG; 774 raid felt-slice SPEC. See `docs/qa/SUNDAY_STATUS_2026-07-26.md`. **774** (raid V1 felt-slice: loadout handoff + deploy ring + Army/Deploy naming) minted 2026-07-26 from the Grok CoC systems review — SPEC READY, sequenced AFTER WO-771.9 integration + barracks-catalog-structure; file `WorkOrders/WORK_ORDER_774_raid_loadout_deployring_naming.md`. Mint the next new WO from THIS line's next-free = **775**; bump it in the same edit.
> ## ⚠ RECONCILED 2026-07-24 (CLI, on `wip`): next free WO = **761** (SUPERSEDED — 761–773 consumed, see 07-26 banner above). **755–759 CONSUMED** (the 07-19 banner's "755" was STALE — 755/756 + 757 dragon-breath + 758 particle-VFX-mental-model + 759 wire-manual-picks were all minted since without a banner bump). New this session: **760** = Syndrath dragon — complete the licensed-asset swap (Assets/Dragon 71047; delete RedDragon 1.2; git-rm old CC-BY-NC) + fly-in→land→burn-towers→retarget-Tree behavior; file `WorkOrders/WORK_ORDER_760_dragon_syndrath_fly_land_burn_tree.md`, READY (owner-requested 2026-07-24). Mint from THIS line's next-free = **761**.
> ## ⚠ RECONCILED 2026-07-19 EVENING (CLI, on `wip`): next free WO = **755**. (**754** = VFX Caster Particle Pack multi-layer preview fix — IMPLEMENTED). **739–753 all CONSUMED.** (felt-test fix wave). New this evening:
> - **750** = Right ActionBar naming + Warden's Grace redesign — **SPEC, READY** (blocked on 2 clip IDs); Attack + Q/W/E/R named skills (Sword Wielding/Sword Heroic/Shield Charge/Warden's Grace/Radiant Strike), mobile HUD shows NO key-letters. File `WORK_ORDER_750_right_actionbar_naming_and_warden_grace_redesign.md`.
> - **751** = Y-height normalization — **IMPLEMENTED** this wave: default 4m + tower 7m override + siege 3m override + Y-height audit tool. File `WORK_ORDER_751_y_height_normalization.md`.
> - **752** = Echo founding-card overhaul + post-tutorial interjection — **SPEC + creative sign-off** (awaiting owner copy). Echo = essence of a person the tree guards; 6 named people (Aldwin/Elowen/Corvin/Bran/Doran/Maren). File `WORK_ORDER_752_echo_founding_card_and_post_tutorial_interjection.md`.
> - **753** = Destructible lifecycle — **IN PROGRESS** (CLI committing): destroyed items = no rebuild + full-cost + VFX cleanup via a new `Destructible` component. Spec file pending on disk.
> (The 07-18d banner below said next-free = 752; it is SUPERSEDED — 752 + 753 were minted this evening.)
>
> ## ⚠ RECONCILED 2026-07-18d (CLI, on `wip`): next free WO = **752** (2026-07-19; 748/749 DONE + RESULT-filed; 750 = Right ActionBar naming/Warden's Grace redesign SPEC; 751 = Y-height normalization IMPLEMENTED — default 4m + tower 7m override + audit tool). **739–748 all CONSUMED.** (**748** = Founding choice "Default Town" vs "Build Your Own" — resurrect the pre-WO-695 `ff.strategicplacement`-OFF prebuilt city as an onboarding choice; apply the prebuilt town as movable `BaseLayout` records (StrategicPlacementMigration.BakedRows + CastleHubBuilder ring pos), NOT the old locked bakes; new `FoundingChoiceController` after PetSelect; FTUE auto-satisfies via TryAutoCompleteAlreadyBuilt; granted (no cost). Movability CONFIRMED for the 8 catalogued buildings. Risks: merged-world coord mismatch, lumbermill-vs-lumberyard id, uncatalogued stations. SPEC file `WorkOrders/WORK_ORDER_748_default_town_choice.md`, READY. Owner-requested 2026-07-18.)
>
> ## ⚠ RECONCILED 2026-07-18c (CLI, on `wip`): next free WO = **748**. **739–747 all CONSUMED.** (**747** = Gear curation -> runtime, "Option A" (architect-ruled 07-18): the Gear Caster curates the FULL StreamingAssets gear library (owner's 65 blink weapons `included` in `GearCurationPicks.json`) but runtime loads the Resources copy FIRST (34 wpn / 20 armor, blink-free) -> the curated set + blink-armor class-defaults NEVER load in-game. Fix: NEW `GearCurationExporter` writes Resources = the curated subset (picks.included ∪ code-referenced default ids); `DataWebRegression` made curation-aware for weapons/armor (assert Resources == curated projection, not byte-identity) w/ marker `GEAR_CURATION_OK`; runtime load order unchanged (Resources-first, WebGL-safe); keeps blink per owner "consistency" call. Owner action: curate armor in the Caster. In implementation. See `GAP_AUDIT_2026-07-18.md`.)
>
> ## ⚠ RECONCILED 2026-07-18b (CLI, on `wip` — the branch that wins; UI seat flagged the working-tree copy flip-flopping to "739" via branch merges, so this is the authoritative record): next free WO = **747**. **739–746 all CONSUMED.** (**746** = Build-Mode/FTUE placement tickets BM-1/2/3 — BM-1 PLACE-return-to-shop (wiring: success path never calls `BuildPaletteUI.Expand()`), BM-2 Echo-Hollow singleton palette gate (hoist `SingletonAlreadyBuilt`, render "Built" state), BM-3 wrong-spotlight-glow (§12 capture-first: UiSpotlight highlightId + resolved target + card-registration ids to split the 2 suspects); file `WORK_ORDER_746_buildmode_placement_tutorial_tickets.md`, READY.) (**744** = strict-MVVM whole-game UI migration — spec `docs/UI_MVVM_MIGRATION_PLAN.md`; conformance-oracle ratchet `UiMvvmConformanceRegression` + **6 of 7 silos landed** on `wip` (B/C/D/E/F/G-safe), ~33 views on VMs, oracle debt 28→16, all §2c-tested; BattleHud+Dialogue landmines pending; CLI-minted 07-18 — a concurrent UI banner edit had dropped this line, re-recorded here.) (**745** = Room Forge regression oracle + FlowTrace instrumentation — UI-seat mint, file `WORK_ORDER_745_room_forge_regression_flowtrace.md`, READY; folds into the 740-743 Room Forge program close.) (**740–743** = Room Forge into mainline PROGRAM — `WorkOrders/WORK_ORDER_PROGRAM_740_743_room_forge_into_mainline.md`, source branch `feat/room-forge-dungeon-baker`: **740** Room Forge + DungeonBaker socketed room pipeline (scaffold LANDED on branch; sockets Door/Arch/StairUp/StairDown, JSON compose layouts, door-touch-door bake gate, seal-unmated, NavMesh bake; file `WORK_ORDER_740_room_forge_dungeon_baker.md`) · **741** default rooms + materials smoke · **742** bake demo layout smoke · **743** canon/README/RESULT close [741–743 spec files still to be written — program table names them but they are not on disk yet].) (**739** = Generic Obsidian building-upgrade tier panel (Enhancement Path) — ONE data-driven panel for all 6 building ids, tier trees from `docs/design/BUILDING_UPGRADE_TREES.md` rev 2, owner-pinned VM binding map, adds `costIron`, mobile-first NO hotkeys; mockup `docs/UI_Mockups/building_upgrade_obsidian_template.html`; file `WORK_ORDER_739_generic_obsidian_upgrade_tier_panel.md` — READY TO IMPLEMENT. NOTE: a 2026-07-17b banner bump recording this mint was overwritten by a later edit — re-recorded here.)
> ## ⚠ REFRESHED 2026-07-17: (superseded — was next free WO = **739**). (**738** = Echo per-echo agency + specialization — the post-pivot Path-B model: 6 collectible spirits with element/level/assigned-lane, passive lane bonuses (Harvest/Crafting live; Defense = offline city-raid only, never a real fight; Exploration = dungeons only), reconciled onto EchoRosterCatalog/EchoAssignments/EchoService + echoes-balance.json, save v32→33; file `WORK_ORDER_738_echo_per_echo_agency_specialization.md` — SPEC, awaiting owner pins.)
> ## ⚠ REFRESHED 2026-07-16c: (superseded — was next free WO = **738**) (**737** = Barracks Train panel proper Obsidian layout — zone map, lock/select/CTA states, SME refs `UI_BLINK_TEMPLATE_CANON` + Grok-02 + inventory locked cells; file `WORK_ORDER_737_barracks_train_obsidian_layout.md`) (**732–736** = Barracks troop roster + tier unlocks — program `WorkOrders/WORK_ORDER_PROGRAM_732_736_barracks_troop_roster.md`: **732** data · **733** unlock gate · **734** tier copy · **735** visuals · **736** regression) (**723–731** = CoC Arena + Barracks → AI camps → async PvP — `WORK_ORDER_PROGRAM_723_731_coc_arena_barracks.md`) (prior 2026-07-16: next free was **732**; **722** = Obsidian expansion tail � **721** = HUD vitals fill contract � **720** = founding-critical Obsidian FIX � **719** = dedicated Build HUD CoC � **718** = kit-law regression � **717** = unstyled-class kill � **716** = capture/pair-walk gate � Grok-03 program � **715** = Hovl towers/melee/spell combat VFX proper wire � READY) (**714** = Obsidian conformance PROGRAM — pack styling across ALL screens, 3 phases (kit primitives → per-screen lanes → image-pair sweep gate), UI-seat mint 07-13 READY · **713** = inventory panel Obsidian conformance + hero render window + consumable hot-swap belt, UI-seat mint 07-13 READY [the UI-seat file briefly minted as 710 is renumbered — its 710 file is now a pointer stub] · **712** = courtyard navmesh island diag, fleet-captured · **711** = HealersCottage content dressing, torch-teacher NPC (owner live walk) · **710** = phased founding, staged palette reveal / chunk-selectable · **709** = echo workforce global multiplier + workforce HUD panel — UI-seat mint 07-13, spec awaiting owner pins 1–4 · **708** = wall builder drag-lines, base-creation completer · **707** = town catalog grooming, one-building-per-trade + pallet stores · **706** = build-palette portraits complete set, UI-seat art · **705** = onboarding duplicate-UIDocument RCA, fleet-captured) (**704** = trailer fullscreen/aspect, ticket VID-1) (**703** = blank-start residual standdown, ticket BLANK-1) (**699** = hero-select ability chips SEL-1 · **700** = Android APK/Seeker test [UI-seat mint, correctly took next-free] · **701** = Mending Echoes offline repair [renumbered from colliding fresh 686] · **702** = Founding of Elarion FTUE [renumbered from colliding fresh 699]) (688–695 consumed by the collision renumber below; **696** = repair-before-upgrade context, renumbered from a colliding fresh 684 mint on 07-13; **697** = currency compact-format + icon chips, ticket RES-1; **698** = encounter budget + scouting, renumbered from a colliding fresh 685 mint). Prior refresh 07-12: 685/686/687 = web-trace lifecycle trio (685 retention/TTL cron, 686 ingestion hardening, 687 read/triage surface); 684 = outstanding-items board. Disk max (program) = **737** (CoC 723–731 + roster 732–736 + Obsidian train layout 737).
> ⚠ **UI-SEAT SYNC NOTE (2026-07-13):** the spec-writer seat is minting in the pre-renumber 674–685 space — every fresh mint there collides. UI-seat numbers translate: its 682=695 (strategic placement) · its 683=693 (jeweler) · its 684=696 (repair context) · its 685=698 (encounter budget). Point the UI seat at THIS banner before its next mint.
> The per-lane detail below is **FROZEN HISTORY** (pre-430 era — ~270 numbers stale); do **NOT** mint from it.
> **Collisions RESOLVED 2026-07-13** (677–681 each had two specs on disk; the 07-12 evening-arc/ticket-board side kept its number, the other spec was renumbered — files renamed + headers updated):
> - 677 kept `mobile_buildmode_move_unreachable` (spec + RESULT) · `asset_caster_toolkit_family` → **688**
> - 678 kept `pi_sdk_timeout_clean_wrap` (spec + RESULT) · `hovl_vfx_fidelity` (spec + RESULT) → **689**
> - 679 kept `crystal_economy_faucets` (owner ask 07-12) · `swordshield_full_wiring` (Grok package) → **690**
> - 680 kept `enhancement_tier_gate_legibility` (ticket UPG-1) · `blink_orcs_activation` (Grok package) → **691**
> - 681 kept `echo_select_intro_and_assign` (ticket ECHO-1) · `blink_icons_import` (Grok package) → **692**
> - 683 kept `build_screen_dpad` (spec + RESULT, ticket closed by PO 07-13) · `jeweler_crafting_mobile_readability` → **693** (untracked — stage when claimed)
> - 685 kept `webtrace_retention_ttl_cron` (banner-canonical, board row Done 07-13) · `webtrace_lifecycle` → **694** (untracked — stage when claimed)
> - 682 kept `web_quiet_error_surface` (spec + RESULT, board row Done 07-13) · `strategic_placement_lock_on` → **695** (untracked — stage when claimed; implementation agent pre-briefed on the rename)
> Rule stands: **mint from THIS banner's next-free, bump it in the same edit.**

Branch **feat/tower-core-loop**. Numbers only, run order. `→` = serial (same lane, in order);
commas = parallel-safe. Detail in `MASTER_PIPELINES_BACKLOG_2026-06-06.md`. New WOs ≥290 are spec'd by
this session's design docs (see "Newly minted" below) — full WO files on request.
**Numbering authority = the master doc + this file, NOT the filesystem max. Next free WO = 430**
(287/288/306–343 used, 289 free, 290–305 minted, 339–343 refill; **352–390 minted out-of-band by the
2026-06-08/09 sessions** and slotted on 2026-06-10; **344–351 skipped — treat as used/reserved,
do NOT mint** per CLAUDE.md; **391–411 minted on-board (Notion) by the 2026-06-10/11 owner/CLI
sessions** — specs live in the Notion rows, only WO-405 has a repo spec file — slotted on 2026-06-11;
**412–428 minted on-board (Notion) by the 2026-06-11/12 owner sessions** — playtest bug sweep, slotted
below on 2026-06-12; **429** = the repo "store stock from DB" spec renumbered from a colliding WO-414).
⚠ **Number collisions (clean up via Lane 0 dedupe item):** repo has duplicate WO files for 329/330/331/333/334
(and legacy 43/46/106–111/129/136–138/152/159/179/181/253–257/279/280/282/301); Notion board also carries a
*different* 328–339 P0-bug block (06-08 session) vs this file's 328–339. Do not reuse any of these numbers.
**Live board (Notion mirror):** https://app.notion.com/p/f3115f05ecf940cf8968bd82bbbdff9f — see `NOTION_SOURCE_OF_TRUTH.md`.

---

## Newly minted this session (≥290 — keeps lanes full)

- **290** QuestService + quest tracker UI (backbone for all questlines) — *foundational, do early*
- **291** Vendor Yarn pack (9 NPCs) + NPCCommandBridge quest verbs (StartQuest/Advance/Complete/GiveKeystone)
- **292** Keystone → Spire finale wiring (≥6 Keystones → Spire Defense → Necromancer)
- **293** Crafting tiers (Common/Fine/Master/Legendary) + legendary recipe system
- **294** Forgemasters' Saga: 4 deep crafter Yarn files + 3 reconciliation scenes
- **295** Legendary set "Aegis of Elarion" items + Oathweld ward effect
- **296** Reforge choice (Heart vs cleansed regions) → finale/ending wiring
- **297** Pet acquisition + slots (tame / egg-hatch / rescue)
- **298** Pet skill catalog content + balance (4 branches + signatures)
- **299** Pet bond questlines (Fenn "Wild Hearts" + per-species)
- **300** Elarion weaponsmithing lore integration (item flavor, maker's marks, appraisal)
- **301** Party persistence — wallet-keyed roster in GameState (+ migrate pet PlayerPrefs blob)
- **302** Floating health-bar oversize fix (green-pill host-scale)
- **303** Combat party HUD wire-to-live-data (HUDManager)
- **304** Brom's rumor board (quest-board UI; can fold into 290)
- **305** Relic-recovery quests (Dawnedge / garrison blades / pattern-blade)
- **339** SaveSchema: add quest state versioning + migration stub (anchor for all quest WOs)
- **340** PlayerPrefs migration: legacy pet/party data → GameState on load
- **341** Backend: auth token refresh + expiry handling
- **342** WebGL: memory optimization + GC pressure reduction
- **343** Analytics: event batching + periodic backend flush

## Out-of-band block 352–390 (minted 2026-06-08/09 sessions; slotted 2026-06-10)

- **352–353, 355–357** build-mode/UI panels (preview, palette filters, portrait layout, placement validation, touch) — L4
- **354** upgrade tier display + synergy — L11
- **358** ✓DONE Yarn welcome · **359** combat feedback — L9 · **360** companion echo outpost — L12
- **361** wave rewards/passive XP — L6 · **362** enemy wave composition — L2 · **363** orientation validation gate — L0
- **364** companion gear — L12 · **365–367** idle poses/routines + town camera — L10 · **368** ✓DONE camera regression
- **369** arena monument — L1 · **370–372** monument VFX, combat SFX, battle music — L9
- **373** critical regression gates — L0 · **374–378** UI fixes (char select, Yarn threading=375, hero pose, dialogue block, town HUD) — L4
- **379** echo auto-summon — L12 · **380** ✓DONE gear icon · **381** ATB arena cleanup — L2 · **382** ✓DONE hero HP
- **383** castle↔outerworld seam — L5 · **384** castle stairs — L1 (CastleHubBuilder single-writer) · **385** castle camera (fade landed, pending playtest) — L4
- **386** battle visualization (understand-phase done) — L2/L4 · **387** ✓DONE camera-relative movement
- **388** player castle as arena defender (SPEC) — L2 · **389** arena mode attack/defend (partial built) — L2/L6/L4 · **390** battle potion loadout (SPEC, after 389) — L6/L2

## Out-of-band block 391–411 (minted on-board 2026-06-10/11 owner/CLI sessions; slotted 2026-06-11)

Newly minted: specs live in the Notion rows (only **405** has a repo `WORK_ORDER_*.md` file — backfill the rest when claimed).
⚠ **HUD/UI gate:** 400/403/404/411 are **Blocked on WO-405** (UGUI design-system owner-approval gate) — do not pick up until 405 is Done.

- **392** Warcraft-style tiered building upgrades (Lumbermill/Forge/Armorer) — L11 · **407** Arcane Tower tiers (extends 392) — L11
- **393** low-contrast yellow text on building-upgrade UI — L4 · **394** build click gives no feedback (surface block reason) — L11
- **395** resource node / mine interaction visual replacement (asset-library audit first) — L5
- **398** Knight still dealing ranged damage (melee-only) — L2 · **399** Knight melee weapon skill set — L3
- **400** Inventory rework to mockup (after 403) — L4 · **401** Blacksmith vendor presentation (signboard + Yarn-only) — L12
- **403** UNIFIED context HUD shell + TOWN mode (RESPEC; rebuild, don't patch; needs 405✓kit) — L4
- **404** combat HUD group (same canvas as 403, waits on 403) — L4
- **405** ✓kit-approved — UGUI design system `ElarionUiKit` (P0, blocks all HUD work; repo file exists) — L4
- **406** shops empty — vendor inventories not populated — L6 · **408** WebGL texture optimization 223→<60 MB (scripts committed, NOT run) — L10
- **409** magenta tower materials (Standard→URP swap, NOT broad fixer) + UI sprite `*`/`#` glyphs — L0
- **411** Town HUD ≠ `hud_mobile_town.png` mockup (10 deviations; blocked on 405, folds into 403 path) — L4
- **391 / 396 / 397 / 402** — used on-board (rows not mirrored here yet — see the Notion board for titles) → do NOT mint
- **410** ★P0 — 0.1 fps in MainCastle_Hall: main-thread GC storm + combat-object leak — L10 (title mirrored 2026-06-12)

## Out-of-band block 412–429 (minted on-board 2026-06-11/12 owner sessions; slotted 2026-06-12)

Owner playtest bug sweep + vendor/storefront chain. Specs live in the Notion rows (repo spec files exist
only for **413** and **429**; backfill others when claimed).
⚠ **Collision resolved 2026-06-12:** the repo file `WORK_ORDER_414_store_stock_from_db.md` collided with
Notion's WO-414 (TALK-glow black disc). Repo spec **renumbered → WO-429** (`WORK_ORDER_429_store_stock_from_db.md`);
the old 414 file is marked SUPERSEDED. Notion's 414 stands.

- **412** ◐ Vendor Wares BUY tab empty (layout fix `ca89d9b` landed; build-test + catalog-load open) — L6
- **413** upgradable vs shoppable building menus (data-driven; repo spec exists) — L6
- **414** black circle under TALK button (AttentionGlowUi first-frame) — L4
- **415** vendor storefront UI from Tech hud elements pack (after 412) — L4 · **416** hide "Talk:" world prompt (supersedes 411 #9) — L4
- **417** ★DO FIRST: Settings/Dev Tools rows blank (owner's test harness) — L4 · **421** battle HUD skill bar empty — L4 · **428** hero damage not shown on HUD — L4
- **418** castle→OuterWorld hard-pop blend — L5 · **426** enable node/outpost claim loop — L5
- **419** enemies don't attack after castle→OuterWorld — L2 · **423** hero attacks without facing target — L2
- **422** Echo Warden pet-selection gate + unlock quest — L12
- **424** harvested resources not in HUD count — L6 · **425** hero spawns unarmed (default weapon) — L6
- **420 / 427** — used on-board (titles not mirrored — see the Notion board) → do NOT mint
- **429** store stock served from Neon DB (StoreService + offline-first fallback; repo spec exists) — L7

---

## Lanes (topped up)

**Lane 0 — Verify/build now:** 283✓, 284✓, 285✓, 286✓, 107, 108✓, 109, 110, 111, 329(regression suite), 302, 303, 363(orientation gate), 373(regression gates), 409(magenta towers + UI glyphs)  ~~328 CLOSED (ambiguous/no repro)~~
**Lane 1 — World/Env (VillageSceneBuilder = SOLE WRITER, serial):** 253 → 166✓ → 167 → 168 → 157 → 137, then 173, 245, 246, 247, 263, 311, 312, 313, 321, 323, 369(arena monument), 384(castle stairs — CastleHubBuilder)
**Lane 2 — Combat/AI (parallel):** 254, 255, 135, 145, 146, 147, 155, 128, 287(SPEC), 310, 315, 316, 317, 318, 320, 326, 327, 330(DTT cyan hero), 331(DTT hotkeys), 332(DTT aim sensitivity), 333(village death→DTT/ATB HIGH), 335(ATB purple capsule bug HIGH) → 336(ATB village wall environment), 362(wave composition), 381(ATB arena cleanup), 386(battle viz, w/ L4), 388(arena defender base SPEC), 389(arena mode, partial), 398(Knight melee-only), 419(enemies passive after transition), 423(rotate-to-target)
**Lane 3 — Combat Feel (serial):** 288(in-progress) → 213 → 217 → 218 → 219 → 220, then 295 (legendary set feel), 319 (DTT parity/anim), 399 (Knight melee skill set)
**Lane 4 — UI/HUD (parallel):** 307 → 308, 309; 303, 302, 110, 124, 156, 178✓, 237, 257, 304, 322, 337(Echo Hollow dialogue overlap HIGH), 338(Echo Hollow rebrand — UI strings), 352, 353, 355, 356, 357, 374, 375, 376, 377, 378, 385(castle camera, pending playtest), 380✓, 382✓; **405✓kit → 403 → 404, 400, 411** (unified HUD path — P0 chain, 400/403/404/411 blocked on 405 Done), 393, 414(TALK-glow disc), 416(hide Talk prompt), 417(★DO FIRST: Settings/DevTools rows), 421(battle HUD skill bar), 428(hero damage HUD); 415(vendor storefront skin, after 412)
**Lane 5 — World/Exploration:** 164 → 153✓, 159, 160, 165, 142, 143, 144, 154, 305, 324, 383(castle↔outerworld seam), 395(node/mine visual replacement), 418(castle→OW blend), 426(claim loop)
**Lane 6 — Economy/Progression:** 228 → 229, 151, 115, 117, 119, 194, 293, 297, 298, 325, 361(wave rewards), 390(potion loadout, after 389), 406(empty vendor shops), 412◐(Vendor Wares BUY empty), 413(upgradable vs shoppable menus), 424(harvest→HUD count), 425(default weapon)
**Lane 7 — Persistence/Backend:** 301 → 339 → 340, 341; 120, 80, 129, 121, 118, 429(store stock from DB — needs React-repo GET endpoint)
**Lane 8 — Monetization/Store:** 72, 73✓, 74, 75, 76, 77, 78, 79, 80, 236
**Lane 9 — VFX/Audio (parallel):** 256, 264, 272, 195, 170, 171, 66✓, 111, 243, 359(combat feedback), 370(monument VFX), 371(combat SFX), 372(battle music)
**Lane 10 — Build/Deploy/Perf:** 196 → 211 → 342, 343; 191, 51, 53, 54, 57, 282(HELD), 365, 366, 367, 368✓, 408(texture opt — scripts committed, not run), 410(★P0 GC storm 0.1fps MainCastle_Hall)
**Lane 11 — Build Mode / Player Base:** 108✓ → 215, 282, 113, 114, 181, 104, 239, 292, 314, 334(tower placement rotate menu), 354(upgrade synergy), 392(building upgrade tiers) → 407(arcane tower tiers), 394(build-block feedback)
**Lane 12 — Narrative/Onboarding/Quests:** 290 → 291 → 304, 230, 222 → 227, 238, 277, 116, 235, 133, 294, 296, 299, 300, 338(Echo Hollow rebrand — Yarn + DESIGN-DECISIONS), 358✓, 360(echo outpost), 364(companion gear), 379(echo auto-summon), 401(blacksmith presentation), 422(Echo Warden pet gate + unlock quest)

**Hard rules:** ONE agent in Lane 1. `GameState.cs`/`SaveSchema` field-adds (Lanes 5/6/7/11/301/339) additive,
one-at-a-time. **Do early:** 164 (zone), wallet/economy merge, 290 (QuestService), 339 (SaveSchema anchor) — many lanes depend on them.
Overlaps: 108 (5/11), 282 (10/11), 80 (7/8), 111 (0/9), 295 (3/6), 340 (7/301).
