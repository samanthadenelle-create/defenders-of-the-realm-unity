# Lanes â€” Work-Order Numbers Only (for CLI)  Â·  reconciled 2026-06-12 (nightly refill)

> ## PROD SERIES (post-launch): next free = **PROD-013**.
> *(Docs seat minted **PROD-012** and bumped 012 -> 013 in this SAME edit. **PROD-012** = IS INTERNET
> REQUIRED on first run — an OWNER DECISION, not a defect, deliberately left BLOCKED with no answer
> proposed. The CDN migration DELETED `Assets/Resources/Structures` and `Assets/Resources/Enemies`
> (verified absent), so the Addressables-first / Resources-fallback chain has NO second tier: a
> disconnected first run = no buildings, no enemy models. The owner's nuance is CORRECT — bundles
> cache (`m_UseAssetBundleCache: 1`), so it is a FIRST-RUN-per-build requirement, not per-launch. The
> miss is LOUD to us (`StructureAssetLoader.cs:139` FlowTrace.Fail) and SILENT to the player. Three
> rulings owed: store-listing declaration; an honest no-connection screen with retry; whether a
> minimal offline FLOOR is wanted — that third one is the ONLY thing that would justify duplication,
> and duplication carries the PROD-010 §1 already-installed-APK hazard.)*
> *(Earlier: Docs seat minted **PROD-011** and bumped 011 -> 012 in this SAME edit. **PROD-011** = NOTHING gates
> an APK-vs-bucket content mismatch — tonight the APK's `structure_art_..._7608a3cb` was absent from
> R2 and the morning build's enemy bundle had never been uploaded, caught ONLY BY HAND; `16e22dba3`
> already conceded *"NO GATE COULD HAVE CAUGHT THIS"*. The gate: parse the built catalog's remote
> `m_InternalId`s, `list_objects_v2` the bucket, diff — every input already exists in
> `tools/r2_sync.py`. Three sharp edges to fix in the same change: `--push ServerData/Android`
> FLATTENS to the bucket root and the docstring at `:21` STILL teaches that wrong form; `--push` skips
> by SIZE and `catalog_*.hash` is always 32 bytes so a reused bundleVersion silently skips; `--check`
> proves credentials ONLY. Bundles the `m_RetryCount: 0` / `m_Timeout: 0` fix (schema `:36`/`:33`),
> which PROD-009 makes mandatory.)*
> *(Earlier: Docs seat minted **PROD-010** and bumped 010 -> 011 in this SAME edit. **PROD-010** = the first-run
> content signal + an opt-in OFFLINE download, owner-designed (*"lets keep the cdn"*, *"registering
> build to user and creating profile"*, *"watching the left hand while the right hand does the
> work"*). WHY keeping the CDN was RIGHT: `m_DisableCatalogUpdateOnStart: 0` means already-installed
> APKs adopt the new remote catalog, so re-pointing an asset local = a path that does not exist inside
> shipped builds = INVISIBLE BUILDINGS for every existing player. Caching is ON
> (`m_UseAssetBundleCache: 1`), so the cost is one-time PER BUILD. ⛔ THE COPY MUST BE TRUE PER
> LAUNCH — beats selected from measured inputs, and when nothing is true SHOW NOTHING; a fixed string
> claiming profile creation on the fifth launch is the `[[missing:market]]` defect class. No timer-driven
> beats, no hardcoded MB.)*
> *(Earlier: Docs seat minted **PROD-009** and bumped 009 -> 010 in this SAME edit. **PROD-009** = enemy content
> is ALL-OR-NOTHING and loads on the MAIN THREAD (`EnemyAssetLoader.cs:115` `WaitForCompletion`;
> PackTogether + ZERO labels on all 78 `Enemy_Art` entries; NO prewarm exists anywhere). Owner ruling
> *"one family not every family"* / *"streaming all seems wasteful"*: on-demand PER FAMILY, labels
> DERIVED from `enemies.json`'s `family` field (19 rows: hollow/troll/orc) via an editor tool, never
> hand-assigned. ⛔ THE ASYNC LOADER IS A HARD PREREQUISITE — on-demand on a BLOCKING loader is
> strictly WORSE than today, because it moves the freeze from the loading screen INTO a fight. Roster
> lookahead is possible because `WaveCompositionBuilder.Build` is a pure deterministic static
> (`:169`), but it re-seeds the GLOBAL RNG (`:179`) so `Random.state` must be saved/restored.
> STRUCTURES stay WHOLE (19.71 MiB): they are player-chosen, so there is no roster to read ahead.
> PROD-011 is a PREREQUISITE (more bundles + `m_RetryCount: 0` = permanent misses).)*
> *(Earlier: Docs seat minted **PROD-008** and bumped 008 -> 009 in this SAME edit. **PROD-008** = NO ORACLE
> CAN SEE ORIENTATION — every orientation defect this project shipped went out compile-green and
> regression-green, because the only oracle is the owner's eyes. The instrument already exists
> (`WoodenWatchtowerBuilder.cs:270-278`, `UprightAspectMin = 1.2f`, measured 1.70-1.92 upright vs
> 0.52-0.59 down). ⛔ A GLOBAL aspect threshold FALSE-POSITIVES on wide buildings
> (`House_Medieval_Medium` = 0.72 upright), so the PRIMARY assert must be HEIGHT FIDELITY
> (`bounds.size.y` vs `YHeightVariable * heightMul`, threshold-free) with the 1.2 band scoped to
> tower-class rows. `RealmStore` is not a catalog row and is a stated coverage GAP, not a special
> case. Must be proven to FAIL pre-PROD-007-fix and PASS after.)*
> *(Earlier: Docs seat minted **PROD-007** and bumped 007 -> 008 in this SAME edit. **PROD-007** = commit
> `f995c4706`'s axis-conversion pass corrected the WRONG FILE — for STRUCTURES the
> `Assets/OffsetForge/offsets.json` rows it retired are INERT (`AttachmentOffsetRegistry` is keyed by
> hero/enemy attachment mesh ids). The LIVE channel is `entry.orientation` in
> `structures-catalog.json`, applied at `StructureFactory.cs:151-158`, which still carried
> `[-90,0,0]` — baked mesh + legacy -90 = the building lies down. Five rows zeroed in tree
> (forge/workshop/jeweler/barracks/tower_ballista), catalog v22 -> v23, dual copies md5-identical.
> ⛔ EIGHT OTHER ROWS KEEP A LIVE -90 AND ARE CORRECT (pet-house, market, arcane-tower,
> collector_farm, collector_lumbermill, lumberyard, foundry, silo) — a "tidy up the -90s" pass would
> lay all eight down, including the FTUE's first building. OPEN: `Tower_Wooden_Watchtower_L3` is
> double-corrected via `preservePrefabRotation` (WO-928 regressing) and was deliberately left.)*
> *(Earlier: Docs seat minted **PROD-006** and bumped 006 -> 007 in this SAME edit, per the sec.2 rule that a
> mint and its banner bump are ONE edit. **PROD-006** = the SIGN IN gate presents to a player whose
> wallet is ALREADY connected (owner, LIVE build: *"im already signed in should not show this
> screen"*). Failure class (b) WRONG SOURCE, NOT a race: `LoginPanelController.cs` pre-fix line 106
> read `FirebaseAuthService.IsSignedIn` as the gate's ONLY input, and that file's own identity law
> (`:556-557`) says an email/Google success binds NOTHING — so a wallet-only player would have seen
> this wall on EVERY launch forever. Fixed in tree tonight (pure `ShouldContinueWithoutLogin` seam +
> `GameStateService.HasAttestedWalletIdentity`), COMPILE_GATE_OK, pinned by
> `Assets/Editor/Regression/LoginGateRegression.cs` [login-gate]. ⛔ NO delay/timeout constant was
> added and none may be added — a timing knob would encode the WRONG diagnosis permanently.)*
> *(Earlier: Docs seat minted **PROD-005** and bumped 005 -> 006 in this SAME edit, per the sec.2 rule that a
> mint and its banner bump are ONE edit. **PROD-005** = the default shield renders THROUGH the hero's
> body and the break survives a dungeon->town port (owner, LIVE build). Diagnostic ancestor = **WO-994**,
> left INTACT and bannered — it holds the trace-proven RCA. Approach: REPLACE the asset
> (`gear/weapon/ShieldWithItemLogic`, addressable) rather than re-dial `shield_A`'s stranded
> `rot=(-160,-180,-84)`, so it seats from DERIVED geometry with no offsets.json row at all.
> ⚠ Acceptance requires a SCREENSHOT after a dungeon->town port — headless gates cannot see
> orientation; the bow proved that.)*
> *(Earlier: CLI seat minted **PROD-004** and bumped 004 -> 005 in this SAME edit.
> PROD-004 = baked-twin STANDDOWN LEAVES THE FOOTPRINT.)*
>
> **A THIRD, DISJOINT NAMESPACE — deliberately not a slice of the main line.** Owner ruling
> 2026-08-17: *"can we change numbering to PROD - restarting numbering as now we are live"* /
> *"thios creates a firm boundary"* / *"there are all issues from dev, but not for prod"* /
> *"exactly they are all front of the line"* / *"as we close from backlog that other list will
> shrink not grow"*. PROD numbers are for defects found AFTER the Solana dApp Store launch; they
> jump the queue. The dev-era backlog keeps its legacy numbers and only ever SHRINKS.
> `tools/board_build.py` parses `WORK_ORDER_PROD-NNN_*` and sorts PROD first within each bucket.
> ⛔ Never renumber a PROD ticket into the main line, or the boundary the owner asked for stops
> existing. Consumed so far: PROD-001 (guide wolf facing), PROD-002 (NPC roles + purchased bodies),
> PROD-003 (Realm Store permanent storefront), PROD-004 (baked-twin footprint),
> PROD-005 (default shield renders through the hero body; survives a dungeon->town port).

> ## AUDITED 2026-08-21 (docs seat) - BOTH ROWS VERIFIED CONSISTENT WITH DISK.
> `python tools/board_build.py` prints **`BANNER_OK next mint - CLI: 1140, UI seat: 1054`**, and both
> rows below match what is on disk: the CLI main line is consumed through **WO-1139**
> (`WORK_ORDER_113{5,6,7,8,9}_*.md` all present, minted across three separate mints tonight
> 1135 -> 1137 -> 1139 -> 1140), and the UI block is consumed through **WO-1053**. No edit was needed;
> this is the audit record, not a rewrite.
>
> ### THE TWO BLOCKS ARE DISJOINT ON PURPOSE. NEVER MERGE THEM.
> The CLI main line and the UI seat's reserved block are separate so both seats can mint in parallel
> **without reading each other's state**. Each seat bumps **its own row in the SAME edit as the mint**
> - a mint written to disk without bumping its row IS the collision (five of them on 2026-08-02).
>
> ### 61 DUPLICATE WO NUMBERS EXIST ON DISK. THEY ARE HISTORICAL. DO NOT "FIX" THEM.
> `board_build.py` reports **`DUPLICATE_WO_NUMBERS 61 number(s) claimed by more than one file`**.
> Every one of them predates this banner being the sole authority - they are what the banner exists
> to prevent, not evidence that it is failing now. **Resolution is first-on-disk-and-referenced-wins**
> (CLAUDE.md sec.2). STOP - **renumbering them is forbidden**: a live WO number is cited from RESULT files,
> commit messages, other tickets and the owner's own notes, so renumbering breaks every reference to
> buy tidiness nobody asked for. The board FLAGS them by design (WO-937) and never renumbers.
> NOTE that a chunk of the 61 are not real collisions at all - they are companion files sharing one
> ticket's number (`WORK_ORDER_1026_IMPLEMENTATION_PLAN.md` beside
> `WORK_ORDER_1026_raid_defense_consequence_loop.md`, same for 1027 / 1038 / 1101 / 1114). Those are
> correct as they stand.
>
> ### NO OTHER DOC MAY STATE A NEXT-FREE NUMBER. Several still do, and every one is stale.
> Found 2026-08-21 outside this file - **do not mint from any of them**:
> `.claude/skills/triage-web-issue/SKILL.md` and `docs/ASYNC_DEBUG_LOOP.md` (both teach "next free =
> **688**", ~450 stale); `docs/HANDOVER.md` ("main line 853 / UI seat 863"); `docs/MASTER_CATALOG.md`
> and `docs/MASTER_CATALOG/docs-wo-state.md` ("412" and "836"); `AGENT_OPENERS.md` and
> `CLAUDE_BEST_SUGGESTIONS_ARCHITECTURE_DESIGN_UI_MOCKUPS.md` ("430+"). Dated
> `CANON_GROUND_TRUTH_*.md` files also carry numbers, but those are frozen ledger entries and are
> fine as history. A copied number is the bug **even when it was right the day it was written** -
> that is exactly how the retired 860-899 UI block kept re-seeding collisions from CLAUDE.md sec.2.

> ## ⚠ RECONCILED 2026-08-23 (CLI): main line next free = **1159**.
> *(CLI seat minted **WO-1157** and **WO-1158** and bumped 1157 -> 1159 in this SAME edit.
> **1157** = a purchase asks the wallet three times and should ask once (session token).
> **1158** = the SERVER must quote the SKR price; a client-resolved price against a
> server-pinned constant is paid-but-not-granted the moment the market moves.)*
>
> ### superseded: RECONCILED 2026-08-23 (CLI): main line next free = **1157**.
> *(CLI seat minted **WO-1156** and bumped 1156 -> 1157 in this SAME edit. **WO-1156** = portal
> foliage clearance (split from WO-1062 §4) + whether the threshold aura inherits the same wrong
> axis WO-1062 just corrected.)*
>
> ### superseded: RECONCILED 2026-08-23 (CLI): main line next free = **1156**.
> *(CLI seat minted **WO-1155** and bumped 1155 -> 1156 in this SAME edit. **WO-1155** = a
> projectile torn down in flight strands its VFX loop slot forever -- found by the WO-1057 lane.)*
>
> ### superseded: RECONCILED 2026-08-23 (CLI): main line next free = **1155**.
> *(CLI seat minted **WO-1154** and bumped 1154 -> 1155 in this SAME edit. **WO-1154** = elemental
> affinity offered as a Cathedral of Magic PERK earned at MAX tower level -- owner ask 2026-08-23,
> child of WO-907.)*
>
> ### superseded: RECONCILED 2026-08-22 (CLI): main line next free = **1154**.
> *(CLI seat minted **WO-1152** and **WO-1153** and bumped 1152 -> 1154 in this SAME edit.
> **1152** = WoodenWatchtowerBuilder NO LONGER RUNS - it aborts on L1, a level that renders fine,
> so the repo has no working way to regenerate the wrapper prefabs that are the ORIENTATION
> AUTHORITY. **1153** = gates are not covered by the WO-972 wall footprint carve-out; gate_stone
> is type Gate so it still claims its measured mesh, and WO-972 never tested a gate.)*
> *(CLI seat minted **WO-1151** and bumped 1151 -> 1152 in this SAME edit. **1151** = the ARCANE
> TOWER PLANS BEACON IS UNDISCOVERABLE - the VFX is owner-APPROVED and untouched; the gap is that
> nothing tells the player what the light is. Owner ruling: the Echo speaks a one-line nudge. The
> ticket names the trap - it must be a DIALOGUE SCREEN, never a spawned Echo body, because
> EchoWorldPresence is the single appearance owner.)*
> *(CLI seat minted **WO-1150** and bumped 1150 -> 1151 in this SAME edit. **1150** = THE MONTHLY
> LEDGER (season pass) PANEL IS UNREADABLE - Close clipped off the top of the screen, the Echoes
> HUD chip drawing THROUGH the modal, the build notice truncated AND overhanging, no obsidian
> frame, thirty identical day cards, and four colour-only signals. Written from a Seeker capture,
> not a description.)*
> *(CLI seat minted **WO-1149 - MON** and bumped 1149 -> 1150 in this SAME edit. **1149** = the
> world keeps running during a purchase - the owner was KILLED mid purchase-test. MON lane.)*
> *(CLI seat minted **WO-1148** and bumped 1148 -> 1149 in this SAME edit. **1148** = EVERY HUD
> label must fit its box - WO-1144 fixed the two it measured and did not generalise; the device
> shows "Raids ..." and "SK... 209" still truncated.)*
> *(**WO-1147** consumed by the MON lane split - the combined monetization ticket became
> **WO-1146 MON (rewarded ads)** + **WO-1147 MON (purchasing)**, owner ruling "split it".)*
> *(**WO-1146** consumed by the MON lane - `WORK_ORDER_1146_MON_monetization_activation_purchases_and_ads.md`.)*
>
> ### ⭐ THE **MON** LANE - MONETIZATION, DEDICATED AND PRIORITISED (owner 2026-08-22)
> **MON is a LANE TAG, NOT a number series.** Owner ruling, verbatim: *"add as a tag WO XX - MON"*.
> A MON ticket takes an ORDINARY number from THIS banner and carries `_MON_` in its filename:
> `WORK_ORDER_<num>_MON_<slug>.md`. The board renders it **`WO-1146 - MON`**, sorts the lane
> FIRST, and gives it its own filter chip beside the bucket chips.
>
> ⛔ **WHY IT IS A TAG AND NOT ITS OWN SERIES.** A private `MON001` series would have joined the
> 18 legacy UNNUMBERED files: no sort order and - the real cost - **the duplicate guard would be
> OFF for exactly the tickets that matter most, and nothing would say so.** `tools/board_build.py`
> records that same lesson against PROD. One numbering authority; the lane is a tag on top of it.
>
> ⚠ A MON file WITHOUT the `WORK_ORDER_` stem is classified a **Doc** by the board and drops out
> of every Ready sweep. That is how `MON001_...md` was invisible when first authored.
> *(CLI seat minted **WO-1145** and bumped 1145 -> 1146 in this SAME edit. **1145** = f8-ack.ps1
> is a HIGH-WATER POINTER, not a per-item queue - acking the newest silently closes every older
> capture. Reproduced live 2026-08-22: acked 3583 while 3582 was oldest-pending. Same failure as
> 2026-08-10, which lost the owner's seq 2307 + 2308.)*
> *(CLI seat minted **WO-1142**, **WO-1143**, **WO-1144** and bumped 1142 -> 1145 in this SAME
> edit. **1142** = Structures/ArcaneSpire_1 + "arcane tower" fail to resolve, losing a building
> every boot (80 errors x 8/8 autopilot runs). **1143** = the siege catapult renders oversized and
> vertical in raids (owner felt-test). **1144** = truncated + colliding HUD labels seen in the
> same capture. All three from the 2026-08-22 headed autopilot run.)*
> *(**WO-1141 was minted by the Codex seat WITHOUT bumping this banner in the same edit** -
> `WORK_ORDER_1141_dungeon_system_audit_verify_and_ship.md` is on disk while the banner still
> read 1141, so the NEXT mint by either seat would have collided on 1141. Reconciled by the CLI
> seat after verifying the file exists. THE RULE (CLAUDE.md s2): mint and bump in the SAME edit,
> or the banner stops being the authority the moment two seats are live.)*
> *(CLI seat minted **WO-1140** and bumped 1140 -> 1141 in this SAME edit. **1140** = raiders still
> beeline for CRYSTAL collectors that can no longer be robbed - SiegeRoleValue rewards a target
> with nothing to take. Surfaced by the WO-1139 collector-loot rewire.)*
> *(CLI seat minted **WO-1139** and bumped 1139 -> 1140 in this SAME edit. **1139** = implement the
> ruled loss stakes (theft 15% floor-protected, crystals exempt, offline included) + the bounded
> repair bill; split out of WO-1026 whose own deliverable shipped 2026-08-21.)*
> *(CLI seat minted **WO-1137** + **WO-1138** and bumped 1137 -> 1139 in this SAME edit.
> **1137** = the hardcoded fallback catalog palette covers 3 of 28 rows, has drifted 4x, and
> presents a silent wrong-game as success - delete it and fail loud, or generate it.
> **1138** = the hollow-pass ratchet only inspects a 4-line window, so 5 of 6 hollow passes in
> one suite escaped it. Both found during the 2026-08-21 gate sweep.)*

> *(CLI seat minted **WO-1135** + **WO-1136** and bumped 1135 -> 1137 in this SAME edit.
> **1135** = WALL TIER MATERIALS ARE NOT TRACKED (all three tiers resolve only via embedded
> FBX materials; `Assets/Resources/Walls/Materials/` does not exist). **1136** = staff_A has no
> decidable sheathe orientation (symmetrical taper, relGap=0). Both surfaced by NEW oracles on
> 2026-08-21 - pre-existing debt newly made visible, not new breakage.)*

> *(**UI seat (Claude UI)** minted **WO-1132** and bumped 1132 -> 1133 in this SAME edit. ⚠ minted from the CLI MAIN LINE rather than the UI block (next free 1050); no collision, but the blocks are disjoint for a reason — see the ticket. **WO-1132** = THE NIGHT
> MARKET — Realm Pack Store presentation redesign. The store works the way a RECEIPT works: five
> browsable SKUs of 25 as 132 px text rows in one scroll column, no art, no price relationship
> between rows, `Coming soon` in the buy rail, the treasury address as 12 px legalese and NO SKR
> balance on screen. The WO-1118 thinning was CORRECT — you cannot price vapor — but it left a shelf
> with nothing to look at. Two-column split (spotlight + four fixed bands: Free -> Gap -> Basket ->
> Patronage) inside the UNCHANGED Obsidian modal. ⛔ PRESENTATION ONLY: the money path, the three
> payment refusals and `RealmStorePurchase` defaultOn:false are NOT this WO's to soften — every lane
> lands with the rail still CLOSED and the whole screen still renders. OWNER RULING 2026-08-21
> *"both stacked"*: Patronage carries Founder's Vow AND Keeper's Almanac, both `anchorOnly` — priced,
> unbuyable, so they anchor while shipping ZERO vapor, which is the only way anchoring gets past the
> WO-1118 rule. Four additive `packs.json` fields (band/orbTint/compareTo/anchorOnly); NO sku renamed
> (`sku` is the live OwnedItemIds key). Palette is luminance-stepped (gold 195 / verdant 177 / ember
> 145 / aether 113) because the owner reads this build without reliable hue discrimination — colour
> NEVER carries a message alone. File `WorkOrders/WORK_ORDER_1132_night_market_store_redesign.md`,
> READY.)*
> *(Docs seat minted **WO-1130** and bumped 1130 -> 1131 in this SAME edit — THE R2 PUSH IS PART OF
> THE SHIP CHAIN. Owner ruling 2026-08-20 after playing a build in which EVERY enemy rendered as a
> tinted capsule: *"wire the r2 push into the ship chain."* ⛔ ROOT CAUSE WAS NOT CODE: enemy and
> structure art is served REMOTELY from R2 with NO local fallback, re-running the Addressables
> grouper re-hashed every bundle, and the new bundles were never pushed — so the APK installed and
> launched perfectly and showed capsules with NO error on screen. THIRD OCCURRENCE (08-18 caught by
> hand, `16e22dba3` conceding *"NO GATE COULD HAVE CAUGHT THIS"*; 08-19 = WO-1124's wrong-target
> content; 08-20 = this). PROD-011's gate WORKED and still lost — it printed a FIX command for a
> human to run, and THAT is the step that got skipped: a gate whose remedy is "someone remembers to
> run another command" is not a gate. New `tools/r2-ship.ps1` is now the ONE place the push/verify
> rules live (push the PARENT `ServerData`, verify the EXPLICIT `ServerData/Android`, judge the
> MARKER on a FRESH log), called by all three ship scripts — which had ALREADY drifted into doing
> different things. ⚠ STILL OPEN: a raw `adb install` touches none of the three scripts and is
> ungated.)*
> *(CLI seat minted **WO-1129** and bumped 1129 -> 1130 in this SAME edit — THE ART TREE
> RECONCILIATION, an owner-requested dedicated overnight session. Owner: *"I think we need one
> dedicated overnight session to properly map everything into the proper structure of the tree...
> that's why I was trying to get everybody to do it in a way that we start replacing literals with
> string variables or constants."* MEASURED: **111 distinct asset-path literals** in code, and FOUR
> competing conventions for one job (TripoTex/, OrcTex/, per-model .fbm, Incoming_Tripo staging).
> Each seat that touched enemy art invented its own home for it, which is exactly why a
> model-name search could report "no texture anywhere" while the art sat in a folder nobody
> named. The deliverable is a DERIVED resolver + a coverage oracle, not a tidy-up.)*
> *(CLI seat minted **WO-1128** and bumped 1128 -> 1129 in this SAME edit — SERVER-RECONCILED
> OFFLINE ACCRUAL. Owner question 2026-08-20: *"would we be able to verify that their offline data
> was valid... getting resources from their pets harvesting while they're offline"*. Answer: do not
> verify it — make it not matter. The server stamps `last_seen` and RECOMPUTES time-derived accrual
> from its OWN clock on sync, capping the client claim. `ResourceCollector.CatchUpAway` already
> clamps a BACKWARDS clock to zero; the open hole is a FORWARDS clock, currently bounded only by
> container capacity — a design property silently doing security work. Also carries the
> local-save-loss warning for the offline opt-in panel.)*
> *(CLI seat minted **WO-1127** and bumped 1127 -> 1128 in this SAME edit — the BATTLE-END
> QUIESCENCE GATE. Owner ruling 2026-08-20 after weighing a full scene-swap against a teardown
> contract and choosing the contract: a scene load does NOT reset `Time.timeScale`, does not touch
> 350 `DontDestroyOnLoad` call sites across 212 files, and does not clear ~290 mutable statics in
> the Vfx+Arena modules alone — so the measured 3.75 s hub reload would have bought a frozen town
> AFTER a loading screen. The gate asserts the world is back to baseline at battle resolve and
> names whichever invariant is wrong. Works in-place AND across the real scene loads the dungeon /
> garrison / raid paths already do.)*
> *(CLI seat minted **WO-1117..1122** and bumped 1117 -> 1123 in this SAME edit — monetization
> profitability program. **1117** = program spine (pack audit + phase map). **1118** = honest SKU
> shelf (hide vapor; keep impulse; rewrite $2/$5 ladder). **1119** = crystal sink + 2x harvest boost
> Version B. **1120** = ads effective free path (stop free grants; LevelPlay; placements live).
> **1121** = live money rails + Buy gate checklist. **1122** = season pass SPEC + revenue KPI.
> Realm Store permanent storefront lives as **PROD-003** (not 1117 — renumbered into PROD series).)*
> *(Earlier same day: CLI minted **WO-1116** and bumped 1116 -> 1117. **1116** = the OPERATOR /
> LIVE-OPS DASHBOARD. **Phase 1 is BUILT**: `api/admin/stats.js` (read-only aggregate endpoint --
> overview / retention / funnel / economy / players, reusing db.js's constant-time X-Admin-Key auth
> VERBATIM, every query a LIMITed parameterised SELECT, player ids masked first4...last4) plus
> `site/admin.html` (unlisted, noindex, key-held-in-MEMORY-ONLY page -- site/ is a PUBLIC deployment).
> **Phase 2 is SPEC ONLY**: issuing grants from the panel is a WRITE surface on a published,
> payments-adjacent game, so the WO specifies it as a BOUND promo code (promo_codes.bound_wallet,
> already enforced at api/promo/redeem.js:172) with a mandatory audit row per write, and explicitly
> REFUSES the direct-write-to-player_data route. 4 open rulings. Pairs with WO-1115, which owns the
> PLAYER-facing half of the same rail.)*
> *(CLI seat minted **WO-1115** and bumped 1115 -> 1116 in this SAME edit. **1115** = REDEEM CODES —
> player promotions ("50% off") AND a dev grant that works on a RELEASE APK, which is the point:
> DeNelle.DevTools is stripped on the APK (asmdef: UNITY_EDITOR || DEVELOPMENT_BUILD) so there is no
> in-game grant surface on a shipped build, and a development APK would test a binary nobody
> downloads. ⛔ THE ARCHITECTURE IS DECIDED BY ONE FACT: the game is PUBLISHED, so anything the
> CLIENT can decide an attacker can decide — codes are SERVER-VALIDATED over the wallet-signed
> request rail the owner proved working today, and there is NO offline fallback (that fallback IS the
> exploit: "turn on airplane mode"). A leaked dev code is harmless because the server binds it to her
> wallet. 5 open rulings. No code yet.)*
> *(CLI seat minted **WO-1113** and bumped 1113 -> 1114 in this SAME edit. **1113** = DUNGEON STATUS,
> a remotely-flippable in-world door state (owner ruling 2026-08-17: flip a dungeon open/closed with
> NO store build, "so we can make all dungeons feel like the two real ones with depth"; follow-up
> ruling: **the client side carries the most weight**, so scope is ~80% client / ~20% backend and the
> client must be fully correct with the backend switched OFF). Reuses the existing in-repo `api/`
> (Vercel + Neon) -- no new infrastructure. The load-bearing rule: **a closed dungeon reads as WORLD,
> never as BUILD STATUS** -- "under construction"/"coming soon"/"dev"/"WIP" are BANNED player-facing
> strings and get a REGRESSION ORACLE, not a comment, because that is the rule most likely to rot.
> Status READY TO IMPLEMENT, 3 open rulings. No code yet.)*
> ## âš  RECONCILED 2026-08-17 (UI seat): UI seat next free = **1063**.
> *(UI seat minted **WO-1063 through WO-1068** and bumped 1063 -> 1069 in this SAME edit. Gear program:
> 1063 program authority; 1064 measured daily-Gold pricing; 1065 affinity runtime; 1066 weapon effects,
> Elarion naming and semantic VFX verbs; 1067 visual certification; 1068 store comparison/hot swap.
> Specifications only until the owner assigns implementation. Regenerate BOARD.html; never hand-edit it.)*
> *(UI seat minted **WO-1062** and bumped 1062 -> 1063 in this SAME edit. **WO-1062** = THE DUNGEON
> PORTAL IS A FLAT PLANE and reads differently from every angle - owner shot the SAME Stoneback Tier-2
> portal from NE/NW/SW and got three different objects. CAUSE IS IN THE CODE'S OWN COMMENT: the owner
> VFX pick at `DungeonWorldPortalSpawner.cs:223-225` is a MAGIC CIRCLE *"use this rotated"* - a
> face-on asset stood upright becomes a vertical disc with ONE viewing direction, so NW reads right,
> SW reads flat and NE collapses to black shards (the unlit back face). ⛔ OWNER RULING 2026-08-22
> WITH EXACT VALUES: *"two vfs each facing outwards"*, *"one rotated 90 other rotated -90"*, *"put .25
> between them"* - same CircleVfx key both times, no new tag, ships immediately. THE SW SCREENSHOT IS
> THE VISUAL REFERENCE; do not re-tune the effect. Edge-on from beside the arch is CORRECT and must
> not be filled in with a third plane. ⛔ SECOND, STRUCTURAL FINDING - THE MAGENTA PATCH: MagentaGuard
> scans *"on every scene load"* (`:20-22`) but the spawner builds portals at RUNTIME after load
> (`:570`), so runtime-spawned objects are NEVER SCANNED - a hole that explains how magenta survives a
> project with both a global guard and a dedicated Portal fixer. Fix = a public `Scan(GameObject)`
> entry the spawner calls. Check the break-log for a guard line first: caught-and-recovered vs
> never-scanned look identical on screen and have different fixes. File
> `WorkOrders/WORK_ORDER_1062_portal_does_not_read_from_every_angle.md`, READY.)*
> *(UI seat minted **WO-1061** and bumped 1061 -> 1062 in this SAME edit. **WO-1061** = THE EQUIP
> DRAWER LISTS NOTHING - owner screenshot 2026-08-22, Thrain (mage) has an Oakheart Staff EQUIPPED and
> "Change Weapon (Main Hand)" offers him NOTHING, not even the staff he is holding. FUNCTIONAL defect:
> a core verb is unreachable. ⛔ ONE CANDIDATE IS ALREADY ELIMINATED - an empty `TargetClass` CANNOT
> cause it: gate (D) in `EquipVM.RebuildCompatible` is guarded by `!string.IsNullOrEmpty(job)`, so a
> blank class SKIPS the class filter and would show MORE, never fewer. If a seat proposes it, that is
> backwards. §12 HELD: three candidates, ONE log line separates them - `owned=0` (equipped != owned;
> the staff was granted to the loadout with no inventory entry - LEADING, because an item you are
> WEARING should be the guaranteed entry), all `fits=false` (weapon `job` rows never equal this
> hero's class), or all `offHand=true`. ⛔ DO NOT loosen the class gate to fix it - `GearCatalog.cs:
> 572-576` records F8 seq-642 Fix B, where the gate hole was *"masked only because the shop/equip UI
> pre-filters its lists"*; a more permissive UI re-opens it. ⛔ DO NOT special-case the equipped item
> into the list - that destroys the evidence. ⚠ CANNOT WAIT FOR THE REDESIGN: WO-1133's compare pane
> consumes THIS query, so an empty result ships that ticket's flagship feature dead. The owner's UX
> point (*"clicking item opens this new window"*) is already answered by the Armory Rail's permanent
> pane - do NOT design a better drawer. File `WorkOrders/WORK_ORDER_1061_equip_drawer_lists_nothing.md`,
> READY TO TRIAGE.)*
> *(UI seat minted **WO-1059** and **WO-1060** and bumped 1059 -> 1061 in this SAME edit.
> **WO-1059** = THE HERO PREVIEW RENDER TEXTURE IS BLANK AT THE SOURCE (F8 seq=3585, photographed by
> the owner same day). The probe names the layer itself: *"blank at the SOURCE, not at the panel. Fix
> the model/culling, not the RawImage."* BOTH consumers are blank - InventoryPaperDoll (seq=2833) and
> now EquipmentPanel (seq=3585, via the VIEW GEAR ribbon `InventoryUIBuilder.cs:341`) - so it is the
> shared `HeroPreviewViewer` rig, not per-panel wiring. Leading candidate is the OPTIONAL `HeroPreview`
> layer falling back to 31 vs the camera cullingMask; confirm, do not assume. CLOSES WO-1133 D1 and
> blocks that redesign's GEAR SECTION ONLY.
> **WO-1060** = THE CLAMP ORACLE, owner-requested *"yes do the clamp oracle"*. FOUR panels in three
> days (1051/1056/1058 + the equip drawer), every one compile- and regression-green. PROD-008 says no
> oracle can see orientation because "looks wrong" is not computable - but the moment a layout breaks
> IS discrete and observable: `ClampMinTouch` GROWING a control. Assert A = the clamp must never fire
> (record-only instrumentation; do NOT weaken the clamp). Assert B = no two interactive rects may
> intersect, INCLUDING the shared Close - B is what catches WO-1058, where both controls clear the
> floor and still overlap. Marker `UI_TOUCH_OK n/n panels`, judged on a FRESH log never the exit code.
> ⚠ Must measure POST-SCALER (`ElarionUiKit.cs:1057` - raw screen px before the scaler applies, the
> F8-5 root cause) at >=2 landscape aspects. Panel set DERIVED from the UICaptureLaunch enumeration,
> never hand-listed. Ships reporting-only with a 4-entry baseline allow-list that may only ever
> SHRINK, and the mechanism is DELETED when it empties. Files
> `WorkOrders/WORK_ORDER_1059_hero_preview_render_texture_is_blank_at_source.md` and
> `WORK_ORDER_1060_touch_clamp_and_overlap_oracle.md`, both READY.)*
> *(UI seat minted **WO-1058** and bumped 1058 -> 1059 in this SAME edit. **WO-1058** = ONE PRIMARY
> SLOT PER ROW - "Upgrade" becomes "Finish Now" IN PLACE. Owner request 2026-08-22: *"reuse the same
> button and make it finish now so you dont have to move"*, plus the ruling *"they can double click
> and be done / if in hurry and burn the crystals"* - DOUBLE-TAP IS A SANCTIONED FEATURE. ⛔ THE ASK
> ALSO REMOVES A LIVE HAZARD, and it is arithmetic: the upgrade-candidate `Upgrade` button sits at
> x 0.76-0.98 (`ManageScreenPanel.cs:725-727`) and the queued row's `Cancel` at x 0.885-0.98 (`:856`)
> - Cancel is ENTIRELY INSIDE where Upgrade just was, so the very double-tap the owner wants CURRENTLY
> CANCELS THE JOB SHE JUST QUEUED, while `Finish` sits a third of the row away at 0.455-0.655. Fix: a
> fixed PRIMARY slot at 0.76-0.98 that is ALWAYS the positive action (Upgrade -> Finish Now, reusing
> the existing two-line verb/cost CTA so the price is on the face before the finger arrives), with
> Cancel moved to 0.40-0.55, Move up to 0.57-0.72 (it collided too) and a deliberate 0.72-0.76 gap.
> ⛔ NO LOCKOUT, NO COOLDOWN, NO CONFIRM on the second tap - the fast path IS the feature; a seat that
> adds friction has undone the ticket. ClampMinTouch must be a NO-OP. The panel ALSO clips per the
> WO-1056 class - noted, NOT folded, fix it with that one. File
> `WorkOrders/WORK_ORDER_1058_one_primary_slot_upgrade_becomes_finish_now.md`, READY.)*
> *(UI seat minted **WO-1057** and bumped 1057 -> 1058 in this SAME edit. **WO-1057** = "RANDOM VFX
> STUCK AROUND" (F8 seq=3583) IS UNANSWERABLE - give the loop pool NAMES. ⛔ ROOT FINDING:
> `VFXManager._activeLoops` is an **int** (`:200`), so the pool knows HOW MANY loops are live and
> NEVER WHICH. A leaked loop is invisible by construction; the harvested tail carries ZERO `[Flow:Vfx]`
> lines, which is the finding rather than a gap in the capture. §12 HELD - this WO builds the
> INSTRUMENT and deliberately does NOT guess at the leak. Fix: a registry keyed by handle carrying
> type + OWNER + startedAt + position, dumped into the BreakCaptureHarness auto-harvest as
> `[Flow:Vfx] LOOPS n/m` sorted OLDEST FIRST - age is the leak signal, and one read then names it the
> way the FloorDiag dump named the pink floor. ⚠ SECOND DEFECT FOUND: `:1113` clamps with
> `Mathf.Max(0, _activeLoops - freed)` - a clamp only matters if the value CAN go negative, so it
> hides the very drift it implies; the registry removes it and turns silent drift into a real error.
> Candidates listed but EXPLICITLY NOT CONCLUDED: RealmStoreBeacon (shipped YESTERDAY by WO-1052 -
> newest code, not likeliest evidence; do NOT disable it on suspicion), GearAura seats (WO-930 is this
> symptom verbatim), HarvestAura (one per collector AND per mine = largest persistent population),
> EnemyAuraVFX on despawn, Poi_* markers. ⛔ A LEAK IS NOT FIXED BY RAISING THE TIER. File
> `WorkOrders/WORK_ORDER_1057_vfx_loop_registry_and_stuck_loop_dump.md`, READY.)*
> *(UI seat minted **WO-1056** and bumped 1056 -> 1057 in this SAME edit. **WO-1056** = ARMIES/LOADOUTS
> PANEL STACKS IN THE SCARCE AXIS - owner screenshot 2026-08-22 shows THREE layers of buttons on one
> band, the CTA over the body text and the wallet row crossing the panel split. SAME ROOT CAUSE AS
> WO-1051 AT 3x SCALE, so treat it as a CLASS not an incident. ⚠ NOTE WHAT IS NOT WRONG: this panel
> resolves `layout.bodyLeft/bodyRight/footer` CORRECTLY (`ArmyMusterPanel.cs:87-99`) - the zone
> discipline is right; what is missing is arithmetic against the touch floor. THE NUMBERS: canvas ref
> 1080x1920 match-width -> ~486 units tall in landscape; panel ~437; `FrameCrafting.bodyRight` ~317.
> `MinTouchPx=112`, so the right well holds TWO interactive rows and is given SEVEN. Every button band
> is authored `y 0.08` of that well = ~25 units and ClampMinTouch grows it to 112 - a 4.5x inflation -
> while the two rows sit only ~32 units apart, so they overlap by ~80 units (71% of their height).
> That IS the stack in the screenshot. ⛔ THE FIX IS NOT NEW NUMBERS: 7 rows x 112 = 784 units and the
> WHOLE PANEL is 437. Landscape is short and wide - PACK HORIZONTALLY. Budget = THREE bands max:
> loadout chips as ONE horizontal row (~237 each), roster rows at 112 in the existing scroll zone,
> and Name/Save/Muster as one action strip (~300 each); `bodyRight` becomes READ-ONLY text. Acceptance
> is that ClampMinTouch is a NO-OP - if it fires the layout was authored wrong. File
> `WorkOrders/WORK_ORDER_1056_armies_loadouts_panel_stacks_in_the_scarce_axis.md`, READY.)*
> *(UI seat minted **WO-1055** and bumped 1055 -> 1056 in this SAME edit. **WO-1055** = "RANGER TOWER
> STILL ON ITS SIDE", F8 seq=3581. ⚠ FIRST: there is NO `ranger` id in the catalog - the building is
> `tower_ground_archer` / displayName "Archer Tower", while the VFX keys call it `RangerTower*`. ONE
> BUILDING, THREE NAMES; needs one owner ruling, not three guesses (the forge/armorer/workshop
> precedent). ⛔ WHY "STILL": both recent orientation sweeps worked the CATALOG channel - PROD-007
> (offsets.json is INERT for structures) and the 2026-08-18 axis-bake retirement - but THIS row's
> correction lives in NEITHER: its row is already (0,0,0)/manual and the -90 is BAKED INTO THE PREFABS
> by WoodenWatchtowerBuilder. The one channel neither sweep touched. ⛔ THE OBVIOUS FIX IS WRONG: do
> NOT copy -90 into the row - `ReskinForLevel` never applies the base euler (`StructureFactory.cs:
> 467-469`, F8-2 2026-07-07) so L2/L3 would stay down, AND the euler lands AFTER the height fit, so
> the fit measures the SHORT axis (L2 reads 0.519) and scale becomes 9.25x instead of 4.80x - a
> 1.93x-oversized tower. §12 HELD: three candidates, ONE measurement separates them - WHICH LEVEL is
> down (L1 only = prefab bake double-applied vs bakeAxisConversion; L2/L3 = tier prefab lost the bake;
> all three = the mesh conversion). Instrument EXISTS: WoodenWatchtowerBuilder measures aspect,
> UprightAspectMin=1.2, PROD-008 measured 1.70-1.92 upright vs 0.52-0.59 down. Owner's town shows
> upgraded structures in the same capture, so L2/L3 LEADS - leading, not concluded. PROD-008 binds:
> ships with a HEIGHT-FIDELITY assert proven red-before/green-after. File
> `WorkOrders/WORK_ORDER_1055_ranger_archer_tower_on_its_side.md`, READY TO TRIAGE.)*
> *(UI seat minted **WO-1054** and bumped 1054 -> 1055 in this SAME edit. **WO-1054** = AN OPTIONAL SFX
> OVERRIDE MISS RAISES A FALSE ERROR AND TRIPS F8. From F8 capture seq=3577. ⛔ NOTHING IS SILENT:
> `ProceduralSfx.ForKind` (`AbilityAudioBridge.cs:91`) reads `LoadClip("Sfx/"+kind) ?? Generate(kind)`
> - the `??` IS the designed fallback and the TODO above it calls the key an OVERRIDE. But it omits
> the `optional:` arg, so it takes `LoadClip`'s `false` default and lands on `FlowTrace.Fail`
> (`AudioAssetLoader.cs:197`) saying "REQUIRED by its caller", which is FALSE for this caller. The
> loader is RIGHT and its default is RIGHT - the CALL SITE failed to declare itself; the param's own
> doc names "a synth-fallback SFX key" as the canonical case. Fix = `optional: true`, one line.
> ⚠ NOT one key: the key is `"Sfx/"+kind` over AbilityEffect and only FOUR clips exist in
> `Resources/Sfx`, so EVERY unauthored effect kind fires once per session. ⛔ Do NOT author a Strike
> clip (silences one key, leaves the rest) and do NOT flip the loader default (a real missing music
> track would go silent AND quiet). A false ERROR costs an owner F8 press and teaches every seat to
> skim past errors - the exact instinct §12/§14 exist to reverse. File
> `WorkOrders/WORK_ORDER_1054_optional_sfx_miss_raises_false_error.md`, READY.)*
> *(UI seat minted **WO-1053** and bumped 1053 -> 1054 in this SAME edit. **WO-1053** = BATTLE PACKS +
> MONTHLY REWARD PACKS - a RENUMBER, not a new ticket: `WORK_ORDER_battle_and_monthly_packs.md` had no
> number in its filename, so `board_build.py` keyed it **WO-?** and bucketed the owner's TOP PRIORITY
> item with the unnumbered parked tickets, where nothing could cite it. `git mv` so history follows;
> content unchanged. Also assigned a real seat: CLI implements, UI seat authored the design pass.
> ⛔ RE-VERIFIED 2026-08-21 per its own aged banner, and FOUR THINGS HAD MOVED since 2026-06-28: (1)
> glimmer is stripped from every pack (owner ruling, pinned by BuyGateAndPriceLadderRegression) and a
> pass tier is pack contents by another name; (2) **THE COSMETIC RENDER SEAM IS LANDING RIGHT NOW** -
> `HeroBodySwapper.cs:1058` Attach + `HeroArmorVisual.cs:371/889` RefreshOn now exist and
> `CosmeticApplier.cs` is modified-uncommitted, so the ruling's "called from nowhere" premise was true
> when written and is being repaired; both families pay out mostly in COSMETICS, and a pack
> disappoints once where a season disappoints for a month; (3) **`ISkrLedger` DOES NOT EXIST** - the
> only hit in the tree is a doc comment at `IPiPlatform.cs:8`, and neither does `LocalSkrLedger`, so
> every `skr` Grant has no writer and §9's V1 acceptance line cannot be met; (4) the price ceiling
> moved $4.99 -> $49.99 (WO-1121), so a pass/card can price on the full ladder. OWNER RULINGS closed
> four open questions: calendar-month seasons ~30 tiers; pass buyable with EARNED SKR; NEVER sell
> tiers; free lane drips no SKR. File `WorkOrders/WORK_ORDER_1053_battle_and_monthly_packs.md`, READY.)*
> *(UI seat minted **WO-1052** and bumped 1052 -> 1053 in this SAME edit. **WO-1052** = REALM STORE
> LANDMARK BEACON, owner request *"some special aura around the realm store so they always know where
> it is"*. ⛔ THE OBVIOUS BUILD HAS ALREADY FAILED ON RECORD: a persistent Family-A loop is exactly
> what `VfxLoopBudget.cs:8-25` documents saturating, and TWO of the six cited captures name the POI
> markers themselves - `Poi_Landmark` and `Poi_NodeAura` SKIPPED at cap. Village tier is 24 and it is
> where a phone spends the most wall-clock. So the layer that GUARANTEES findability costs ZERO loop
> slots: Layer A = geometry + a real Light (mast above the roofline), Layer B = the near aura,
> proximity-gated via VfxAuraProximityCuller with GearAura single-seat handle discipline, Layer C =
> a compass bearing via HudCompassWidget.ObjectiveProvider - WITHOUT C the request is unmet, since an
> aura shows nothing to a player facing away. Greyscale is the acceptance channel per EnemyAuraVFX:
> the store is a metronomic vertical MAST against the Heart-as-mass and the portal-as-doorway. ⛔ The
> store stays baked hub furniture with NO catalog row (PROD-003: a row makes it sellable/movable/
> damageable and *"a raid takes revenue OFFLINE"*). ⛔ THE VFX KEY IS HELD FOR AN OWNER TAG per the
> standing no-creative-pick rule - Layers A and C are unblocked and can start now. File
> `WorkOrders/WORK_ORDER_1052_realm_store_landmark_beacon.md`, READY.)*
> *(UI seat minted **WO-1051** and bumped 1051 -> 1052 in this SAME edit. **WO-1051** = DAILY CHEST
> PANEL LAYOUT - the two claim buttons are authored ON TOP OF the shared Close bar. Proven as
> ARITHMETIC, not a hunch: the CTAs sit at y 0.10-0.28 (x 0.06-0.48 / 0.52-0.94) and
> `ElarionUiKit.DefaultCloseZone` (`:297`) is (0.360, 0.050, 0.640, 0.125), so BOTH rects intersect
> the Close in x AND y, and being later siblings they DRAW ON TOP - burying the one exit control the
> screen has, which matters because owner canon forbids an X for close (`:858`). ROOT CAUSE IS
> STRUCTURAL, not a wrong number: `DailyChestController.cs:92` parents to
> `_modal.chrome.content.transform` while every other panel prefers `layout.body` - so it authors raw
> fractions over the whole panel with no knowledge of the frame zones. ⚠ Reparenting ALONE does not
> fix it: the DEFAULT body floor (0.10) itself sits inside the close box, so the CTA band must also
> be RAISED - adopt the sanctioned `FrameRaid` precedent at `:418-423`, which fixed this exact
> collision once already. Four more source-verified defects ride along: labels overhang the body well
> (Label defaults x 0.03/0.97 vs well 0.06/0.94); `medallionIcon: "icon_chest"` resolves to NOTHING
> (no such sprite anywhere under Resources) on a frame declaring `hasMedallion=false`; a READY ad
> button still wears the Gray face because only `.interactable` is toggled (also a colourblind
> failure); and the panel is 80%x64% of canvas for two sentences. Reward values, the ad gate and the
> UTC-day gate are explicitly NOT this ticket. File
> `WorkOrders/WORK_ORDER_1051_daily_chest_panel_layout.md`, READY.)*
> *(⚠ THE ROW ABOVE WAS STALE. It read "next free = 1045" while WORK_ORDER_1045_* AND
> WORK_ORDER_1046_* both existed on disk — the 1046 mint wrote its bump into the PROSE below and
> never updated THE ROW. That is §2 in its quietest form: the banner was wrong in exactly the way
> that makes the next seat collide, which is how 1043 collided earlier the same day. 1045/1046/1048
> are CONSUMED; 1047 is deliberately left unused. **1048** = BakeLayoutBatch silently strips every
> chest/trap/key/lock because populateForPlay defaults to FALSE and the headless entry point takes
> the default — it emptied three shipped dungeons, exited 0, and was caught only by
> [authored-placed]. Minted at the owner's instruction.)*
> *(UI seat minted **WO-1048** and bumped 1048 -> 1049 in this SAME edit. **1048** = owner F8 seq 2515
> (flagged, `dg_bonecrypt`): *"LEave **still** on all steps with an exit portal in dungeons."*
> `DungeonController.HydrateExits:643` spawns ONE "Leave Dungeon" arch in the ENTRY ROOM and is called
> at `:364` inside the per-run/floor hydration sequence — **if that sequence runs per floor, every floor
> gets its own exit.** ⚠ **"steps" IS AMBIGUOUS** (dungeon FLOORS / stair-traversal links / tutorial
> steps) and the three readings imply DIFFERENT fixes — **confirm with the owner, do not guess.**
> ★ If floors: an exit at every depth **deletes the risk of descending**, which undercuts the
> torch/oil/darkness system AND **WO-1041 §3's "deeper = better" gem tiering — a reward curve paying for
> elected risk that no longer exists.** ⛔ **COUNTER-CONSTRAINT: never make a run un-leavable** — the same
> function already warns *"run could be un-leavable!"*, and WO-987's confirm + the spawn grace + the
> arming latch all exist to stop accidental exits. This is WHERE/WHEN an exit lives, never removal.
> ⚠ Interacts with `HydrateCheckpoints` in the same sequence — read it or a fix silently changes
> checkpoint semantics. ⚠ **"still" = recurrence** — check WO-1007 / WO-1008 / WO-987 for a regression,
> the WO-962→WO-1036 pattern. Also folded into **WO-1047**: `SweepPlaceholderCubes()` exists at `:367`
> and the orange cube SURVIVED it — read that sweeper first, and note it runs BEFORE later spawners so
> a one-shot sweep is structurally blind to anything created after hydration. READY.)*
>
> *(UI seat minted **WO-1047** and bumped 1047 -> 1048 in this SAME edit. **1047** = owner in a dungeon:
> *"target attaches to this item and there is a floating key next to it. Something is broken it feels."*
> ✅ **HALF OF IT IS WORKING AS DESIGNED — do NOT "fix" the floating key.** `ComposedPropVisuals.BuildKey`
> deliberately spins+bobs it *"so it reads as a pickup from across a dark room"*, and it logs *"was
> INVISIBLE before WO-1112"* — grounding it re-breaks that readability fix. ★ The real defect is the
> other object: `HeroTargetIndicator:9` gathers **"alive HOSTILE IDamageables"**, so a PROP is
> registering as a hostile damageable — a reticle that locks to furniture makes targeting untrustworthy
> everywhere. ⚠ Plausible mechanism (NOT confirmed): WO-853 dual-implemented `IDamageable` +
> `IDamageableStructure` across structures; a prop deriving/registering like a structure would inherit
> that hostility. ⛔ §12 — the orange cube is UNIDENTIFIED and matches NONE of the four builders in
> `ComposedPropVisuals` (Key=cylinder+cubes, Lock=plate, TrapPad=pad, OilStone=**cylinder plinth +
> SPHERE bowl**), so **dump the object the reticle holds (`CurrentTarget` is already in hand) before
> editing**. Fix at the REGISTRATION SOURCE, never as a filter patch in the indicator, or every future
> prop repeats it. ⚠ Prove ENEMIES still target after the change — a hostile-set edit can silently
> un-target real combatants. ⚠ An ORANGE cube may be a placeholder path with NO probe covering it
> (`[Flow:MagentaProbe]` only covers magenta). READY.)*
>
> *(UI seat minted **WO-1046** and bumped 1046 -> 1047 in this SAME edit. **1046** = owner asked what an
> Archer Tower upgrade actually changes (panel says only *"Stronger Archer Tower at Level 3"* for
> 225 wood + 100 iron). ★ **THE ANSWER LOOKS LIKE: NOTHING BUT THE MODEL AND THE PROJECTILE ART.**
> The **Arcane Spire HAS** an authored ladder (`towers.json levels`: range 14/17/21, damage 12/22/40,
> cooldown 1.1/0.9/0.7, and a NAME per tier — Arcane/Runed/Warded Spire). The **Archer Tower has NO
> per-level stat data**: `structures-catalog tower_ground_archer` carries `maxLevel:3` + `upgradeCost[]`
> + `canHitAir:false` and no range/damage/cooldown ladder; `DefenseTower.cs` level references are only
> `upgradeVisualPath` (_L2/_L3 model) and `ArcherTowerLevel1/2_Projectile` (VFX); the only
> `LevelMultiplier` in the tree is `StorageCapsCatalog`'s (unrelated). ⚠ **If confirmed this is a
> BALANCE defect and a FALSE CLAIM, not a copy nit** — the player pays for a reskin. ⛔ §12: static
> reading LOCATED this, it does not CONCLUDE it — **measure effective range/damage/cooldown/HP at L1/L2/L3
> first**, then it is either a UI ticket (show the deltas) or an owner escalation (author the ladder).
> ★ Owner's splash / better-targeting / dual-targeting ask = NEW MECHANICS, and they are the **WC3
> "Counters" pillar the design review lists as MISSING**. `tower-perks.json` already has a `tiers`
> structure (the WC3-style perk tree) — **check it before designing anything new.** READY.)*
>
> *(UI seat minted **WO-1045** and bumped 1045 -> 1046 in this SAME edit. **1045** = the upgrade button
> is ENABLED-AND-INERT when the builder queue is at capacity (owner 2026-08-17). ★ The codebase already
> names this: `BuildTimerService.TryBuySlot:301-315` — *"STEP ONE failed. Say WHAT unlocks it - an
> unexplained locked button is the bug"* — and already returns player-readable ASCII reasons, already
> carries state BY TEXT for colourblindness, and already exposes `InsufficientCrystalsPrefix` for store
> routing. The button consumes NONE of it; do not write new copy. ⚠ Name WHICH limit was hit:
> `freeBuildSlots`=2 (concurrency) vs `queueDepthPerLine`=5 (depth) — different remedies, and the config
> FORBIDS implementing depth by raising concurrency. ⚠ Player-facing buys go through `TryBuySlot`, never
> the `[Obsolete]` `GrantSlot`. ⚠ Sequence with WO-1027 (the IDLE face of the same question).
> ⛔ **COLLISION NOTE:** this was minted as 1043 from a STALE banner read while this sweep had already
> consumed 1043 + 1044; theirs were first-on-disk-and-referenced so they won per §2, and this renumbered
> to 1045. The lesson is the rule itself — **re-read the banner AT MINT TIME, never carry a private
> running counter across a long session.**)*
>
> *(UI seat minted **WO-1044** and bumped 1044 -> 1045 in this SAME edit. **1044** = BIOME + TUNNEL
> IDENTITY -- the creative half of tonight's portal/tunnel spoke. The four biomes turned out to be
> ALREADY authored in depth (`docs/ECHOES_OF_ELARION_NARRATIVE.md` sec.3-5b,
> `docs/regions-narrative-and-npcs.md` sec.2-5) with NPCs, crystal grades and region intro voice lines
> written, so the WO ASSEMBLES that and fills only the real gaps: first-arrival image per biome,
> palette described in value/texture/light (owner is red/green colourblind), one Echo associated per
> march (flavour ONLY -- never a harvest gate, CLAUDE.md sec.7), and the tunnel's name + origin.
> Recommends renaming the DISPLAY STRING "The Hollow Roads" -> **"The Rootways"** while LEAVING the id
> `dg_hollow_roads` alone (it is a hard contract in `BiomeRoads.ArmRoomIdFor`, the graph json, the
> injector and BiomeRoadsRegression). Status PROPOSAL, 9 open rulings listed. No code, no data, no bake.)*
> The paragraph below still says the UI block is *"consumed through 1029"*. **That went stale by
> fourteen numbers** â€” `WorkOrders/WORK_ORDER_10{30..43}_*.md` all exist on disk, minted 2026-08-15/16
> (the newest, **1043** = the pending attended dungeon re-bake, minted this sweep and STILL READY â€”
> it is a genuinely un-run bake, not a done lane). 1000â€“1043 are CONSUMED; the UI seat mints at **1044**.
> âš  This is exactly the Â§2 failure mode the banner warns about â€” a number written into prose in one edit
> and never bumped in the next. **Bump THIS row in the same edit as any UI-block mint.** The main line is
> unaffected and disjoint (1100â€“, next free 1112 â€” `WORK_ORDER_1111_*` is the highest on disk).
>
> **1109 / 1110 / 1111** = the RAID READINESS SET, from the SME audit run against the owner's
> *"can i finally test raids fully?"* (2026-08-16). The loop IS closed and there is NO UXML in the raid
> path â€” but: **1109** every raid spawns the EMERGENCY pill-hero because `RaidHeroSpawner` DOES NOT
> EXIST (only a comment claims it does), dropping a `FlowTrace.Fail` into F8 on every entry and
> training everyone to ignore Hero Fails; **1110** the one softlock hatch (`BuildHud()` unguarded, and
> it gates the clock-expiry subscriber, so a throw = no tray, no Retreat, no timeout), four silent
> catches incl. a reward multiplier that silently pays x1 instead of x2.2, and a death exit that
> forfeits loot retreat pays; **1111** NO harness has ever loaded a raid scene â€” 1109 would have been
> caught by one headless raid load.
> **1108** = ECHO SIMPLIFICATION (owner-approved 2026-08-16, "Get it done. Do it that way" +
> "exactly now finish it"): Echoes auto-harvest with a player-picked resource (âš  ALREADY BUILT â€”
> do not greenfield), Echo COUNT drives repair passively (the repair chip retires; stored `repair:N`
> read-migrates, no schema bump), and the Echo escorts to the gate then VANISHES, reappearing once
> after the first battle (âš  no despawn path for a pet exists anywhere today). Also carries a CANON
> CORRECTION: CLAUDE.md Â§7's "affinity DOUBLES the yield" is FALSE against live tuning â€” the match
> bonus is additive (+0.03 on a +0.02 base), ~+3% absolute, per the owner's own "+5% not 55%" ruling.
> *(âš  **1106 WAS MINTED AS A FILE WITHOUT BUMPING THIS BANNER** â€” by this seat, hours after writing
> the note below that names mint-without-bump as THE collision cause. No collision resulted (single
> seat today), but the rule is the rule: the bump rides the SAME edit as the mint. Reconciled here.
> **1106** = the "can't afford" reason is unreadable behind the red shaded footprint (owner F8 seq
> 2504); READY, serialize behind the build-mode column work.
> **1107** = BUILD-MODE COLUMN FIT (owner-approved 2026-08-16, "Get it done. Do it that way"): the
> right-edge column claimed 1080 ref px of a canvas that is only **965.4** tall at the Seeker's
> 2670x1200 â€” so Done overlapped the Town quick-tab, and had done since before today. Fixed by laying
> the D14 verb rail HORIZONTALLY (band 132x384 -> 384x132, y 114..246) AND dropping the quick-tab
> height 132 -> 112 (MinTouch floor). âš  MEASURED: the rail move ALONE was insufficient â€” with 132px
> tabs the binding tenant becomes the CAROUSEL DOCK (98..401), leaving Done 17.6px over. Both changes
> were required. Now 923 required vs 941.4 available = 42.4px headroom, clamp never fires. Also
> deleted a hand-copied `QuickTabStackTopPx` duplicate (BuildHudController now reads BuildPaletteUI's).
> âš  Ultrawide DESKTOP 21:9 (2560x1080 -> 935.3, 3440x1440 -> 931.7) still overflows by ~12-15px and
> the clamp WILL fire there; not a shipping target, recorded rather than hidden. Source is tagged
> `COLUMN-FIT 2026-08-16` â€” grep that token.)*
> *(CLI minted **WO-1105** and bumped 1105 -> 1106 in this SAME edit. **1105** = the Ranger plays like
> a swordsman. MEASURED: his authored kit is fully ranged (Q 15 m / W 12 m / E 15 m / R aoe), so the
> defect is the PRIMARY ATTACK â€” `PlayerAttackController._attackRange = 3.2f` is a class-agnostic
> melee sweep, and that file's own comment asserts ranged classes' "real attacks route through
> AbilityDef.Range", which is false for the spammable primary. Build: ranged primary on the bow anim +
> projectile path, tap-to-select with the ALREADY-SHIPPED Marker 2 Pointer Loop target marker, and
> bow iconography/names on the action bar (HeroCatalog authors knight names â€” the same defect that hit
> the owner as a MAGE, recorded at HudModelProducers.cs:65). âš  One owner ruling open: auto-target vs
> strict tap-to-select. âš  Do NOT flip ff.lockon â€” that is a parked camera risk, not target selection.)*
> *(CLI minted **WO-1104** and bumped 1104 -> 1105 in this SAME edit. **1104** = the Arcane Spire
> plans MOMENT â€” owner asked "when do i get the arcane spire plans?" mid-playtest and the answer was
> NEVER: F8 seq 2434-2442 caught `CastleDefensePlansService` throwing on the UNDECLARED `SpawnPoint`
> tag every 3 s scan, which also made its own fallback seat unreachable. Every earn-gate had already
> passed. Fix = resolve by `WaveSpawnPoint` COMPONENT (landed); threshold moved 2 -> 3 per her ruling
> "it should be given after wave 3"; the celebration screen + the Aldwin FTUE call-to-arms are new
> build in that WO. âš  It COLLIDES with WO-1031's guide despawn â€” after wave 3 the Echo body may
> already be gone, so whichever lands second verifies the other.)*
> *(CLI minted **WO-1103** and bumped 1103 -> 1104 in this SAME edit. **1103** = kill rewards:
> per-enemy base value + bounded variance + kill-count scaling in the arena, and the overworld
> ranged-kill earned-rewards notification â€” full source-cited audit 2026-08-16 embedded in the WO;
> also fixes two proven arena payout bugs (capped spawns pay full roster; 5% bonus boss pays 0).)*
> *(CLI minted **WO-1102** and bumped 1102 -> 1103 in this SAME edit. **1102** = fleet instances
> discard Step-level stdout: `run-autopilot-fleet.ps1:127` launches the player with NO `-logFile`,
> so N>1 instances contend for the default Player.log and non-error FlowTrace evidence is LOST
> (proven 2026-08-16: two DungeonLoop runs with the WO-994 probes live left zero Step lines on
> disk; Player.log mtime never moved). Fix = per-instance `-logFile <runDir>\player.log`.)*
> *(CLI minted **WO-1101** and bumped 1101 -> 1102 in this SAME edit. **1101** = environment
> variety lane, owner directive 2026-08-16 "from grass textures to different biome maps" â€”
> terrain/ground texture variety + per-area biome map spec; file
> `WorkOrders/WORK_ORDER_1101_biome_maps_and_grass_texture_variety.md`.)* **782â€“859 + 900â€“999 CONSUMED; the
> CLI line JUMPS 1000â†’1100** â€” 1000â€“1099 is the UI seat's reserved block (consumed through 1029, their
> row below) and the main line ran into it: the C-1 conflict went live. Same fix shape as the original
> two-block split: disjoint again by construction. âš  FLAGGED FOR OWNER RATIFICATION (C-1) â€” if she
> prefers different ranges, renumber forward only, never reuse.
> *(CLI minted **WO-1100** and bumped 1100 -> 1101 in this SAME edit. **1100** = the dungeon-portal
> threshold aura renders with a NULL material/shader on slot 0 â€” owner F8 seq 2404â€“2415 (12 identical
> proving lines, 2026-08-16 06:53, editor session): `[Flow:MagentaProbe] FAIL
> cause=DungeonWorldPortalSpawner.BuildPortal obj='...[Hovl_Portal_Threshold_Aura]' material='NULL'
> shader='NULL' class=M2`. The `Portal_Threshold_Aura` key resolves to a Hovl-derived prefab whose
> slot-0 material is GONE on this machine â€” the VFX self-containment class (the `Casting_Fire`
> precedent: `CopyAsset` mirrors the prefab, not its materials), most likely introduced/exposed by
> `d7e2e4eae` "real pack vortex" + the `0e4690036` portal-material metas. MagentaGuard's WO-869
> recovery sweep fires but a NULL slot is not a broken-shader repaint. READY â€” triage step: name the
> prefab the key resolves to, check its material GUIDs against disk + the `VFX_ART_MIRROR_OK` gate's
> scope (this prefab is evidently OUTSIDE it â€” that gate scans `Resources/VFX`, so a portal prefab
> living elsewhere escapes; widening the gate's scope is part of DONE).)*
> ## (superseded header, kept for history) RECONCILED 2026-08-15 (CLI): main line next free = ~~1000~~. **782â€“859 + 900â€“999 CONSUMED.**
> *(CLI bumped 999 -> 1000 in the SAME edit as minting **WO-999**. **999** = class resource economy
> residual â€” ability-face cost pips + ResourceDisplayName on the bar + ranger Quick Shot Focus +
> owner balance rulings left open by WO-997 (which is DONE implementation; RESULT on disk).
> **997** = class resource system DONE (pools/costs/bar floats/`[class-resource]` oracle).
> **998** CLOSED â€” SUPERSEDED by UI seat WO-1024 (hub repair surface). Numbers stay consumed.)*
> *(âš  994 and 995 were minted as FILES without bumping this banner â€” the exact mint-without-bump that
> Â§2 names as the collision cause, committed by the seat that spent the day enforcing it against others.
> Caught and reconciled 2026-08-14 while minting 996. No collision occurred; both files are on disk and
> referenced.)*
> - **996** = **`armor.json`'s two canonical copies are PARTIALLY DISJOINT â€” each holds content the
>   other lacks, and the class ladders exist in only ONE of them.** MEASURED AT SOURCE 2026-08-14:
>   Resources **v2 / 24 rows**, StreamingAssets **v1 / 30 rows**; **15 Resources-only** (the entire
>   `armor_{knight,mage,ranger}_{common..legendary}` ladder) and **21 StreamingAssets-only** (all
>   `blink_armor_*`). Only **9 rows shared**. âš  **THIS IS NOT THE WEAPONS SHAPE** â€” weapons is a
>   deliberate curated subset from `GearCurationExporter` (Resources-only ids = 0). Armor has no such
>   pipeline and has drifted in **BOTH** directions, so "Resources is a subset" is FALSE here and any
>   oracle written on that assumption is wrong for armor. âš  **SHIPPING RISK:** Resources WINS at runtime
>   so the editor looks fine; whatever reads the StreamingAssets fallback gets a roster with **no class
>   armor ladders at all**. Schema versions also differ (v2 vs v1). Found FOUR independent times on
>   2026-08-14 â€” the 300â€“599 sweep, WO-544's verification, WO-976's assertion-(d) scoping, and WO-500
>   step 1's new subset oracle. â›” **Do NOT "fix" it by copying rows across.** Decide which copy is
>   AUTHORITATIVE and which is DERIVED, then make one generate the other. READY.
> - **995** = dungeon boot self-evicts to town via the exit trigger (see `WorkOrders/WORK_ORDER_995_*.md`).
> - **994** = the shield seat is stranded against the base WO-970 moved (see `WorkOrders/WORK_ORDER_994_*.md`).
> - **993** = **PETS ARE DESCOPED TO HELPERS â€” retire the pet PHYSICAL-PRESENCE stack (aura + progression),
>   but DO NOT TOUCH `PetHeroLeash`.** OWNER RULING 2026-08-14, verbatim: *"we dont use the pet aura
>   anymore since we descoped them to simply helpers and not physical items around us"*, *"same with pet
>   progression"*, *"auracontroller can be retired"*. Echoes are **systems** (harvest lanes, flat defense
>   %), not companions that stand next to you â€” so the aura/level-visual surface is dead **by design**,
>   not by accident.
>   **`PetHeroLeash` GOES TOO â€” owner ruling, same breath: *"pet leash gone too"*.**
>   âš  **BUT IT HAS 47 NON-SELF REFERENCES AND IS THE TUTORIAL GUIDE LEAD** (`GuideLeadMovementRegression`,
>   `TutorialFlow.SetLeadTarget` â€” the WO-962 latch feeds it). The pet plumbing was REPURPOSED into the
>   thing that walks the player through the FTUE. **So removing it is NOT a delete, it is a FTUE change:
>   the guided walk must be given a replacement lead or the step must be removed, IN THE SAME CHANGE.**
>   Deleting the symbol and leaving the tutorial step standing produces a step that silently stops
>   leading â€” a dead FTUE with a green gate. Retire by SYMBOL, never by folder.
>   Clean to retire (verified 2026-08-14): `AuraController` â€” 1 non-self ref (`GearAura.cs:8`);
>   `PetAuraVFX` â€” 1 non-self ref and it is a **comment** (`ParticlePackVfxBatchBuilder.cs:1055`);
>   `PetBrain` â€” 2 refs, **both inside `AuraController`**, so they go to zero with it.
>   âš  Also void: WO-128's acceptance criterion *"WO-58 aura â€” BUILT â€” DO NOT BREAK"*, which has been
>   protecting something that **never ran** (`WORK_ORDER_58.RESULT.md:38-43` claims `PetProgression`
>   calls `SetLevel`/`PlayLevelUpBurst`; **neither call exists at HEAD** â€” a hollow assertion caught on
>   2026-05-30 and never closed). Orphans the `Aura_PetLevel1/2/3` catalog keys and the pet registrant in
>   `VfxAuraProximityCuller`. READY.
> - **992** = **SIX classes ship in every build, compile clean, and are NEVER INSTANTIATED â€” dead code
>   the legacy-close does NOT remove.** Found by the 2026-08-14 phantom sweep: `WeatherManager`,
>   `TorchFireController`, `AuraController` (WO-52/55/58), `BattlePassManager`, `CryptoPaymentManager`,
>   `CosmeticApplier` (WO-73), plus the WO-87 Cinemachine controller. Each has an honest RESULT file
>   that flagged *"scene wiring = manual editor work"*; **that wiring never happened and nobody noticed
>   for ~2.5 months.** âš  **WHATEVER THOSE OLD TICKETS RESOLVE TO, IT DOES NOT TOUCH THE CODE** â€” closing
>   one removes the only record of why the code is there, so the finding is hoisted here instead. Decide
>   per class: WIRE IT or DELETE IT. **Owner dispositions 2026-08-14:** `WeatherManager` = **KEEP**
>   (*"will play into the zones for the map"*); `TorchFireController` + `AuraController` = **RESEARCH
>   FIRST** (she suspects the latter should target towers/portals, but WO-58's own title is "pet aura
>   system" â€” expect a divergence); the other three she reads as *"ideas not implementations yet"* â€”
>   confirm before deleting, and âš  do NOT wire `CryptoPaymentManager` without an explicit owner call. âš  **METHOD NOTE, load-bearing:** Unity serialises script refs by
>   **GUID**, so a class-NAME grep across `.unity`/`.prefab` finds nothing and reads as "no problem".
>   Prove wiring by reading the `.cs.meta` guid and searching THAT across scenes/prefabs. WO-87's shape
>   is the giveaway: controller exists, GUID in no scene, and the builder line that would seat it is
>   COMMENTED OUT (`VillageSceneBuilder.Characters.cs:119`). READY.
> - **991** = **The Healing Caravan: MOBILE (very slow) + an unlockable heal FIELD for the Tree of Life
>   and nearby troops.** OWNER DESIGN 2026-08-14, verbatim: *"the healing tower idea is what caravans
>   replaced. this way they can eventually be unlocked to recover damage like for tree of life and
>   nearby troops"* + *"by a caravan its mobile, but very slow"*. This is **why** a caravan replaced the
>   tower rather than re-skinning it: a tower is a fixed point; a caravan trades placement permanence for
>   **reach**, and very slow movement is the cost that balances a heal field that can go where it is
>   needed. âš  **NOT SHIPPED â€” `healing_caravan` currently carries `behaviorId: HealingFountain`, a
>   static bespoke singleton.** Mobility and the heal field are both design intent today; no doc may
>   claim the caravan moves. âš  The retired `HealerTower` case (`StructureFactory.cs:935`, kept by WO-990)
>   is the **worked example of exactly the support-FIELD pattern this needs** â€” build on it, do not
>   reinvent it, and do not resurrect `tower_healer`. **SPEC â€” needs design detail before implementation.**
> - **990** = **RETIRE the `tower_healer` catalog row â€” it has never been buildable â€” but KEEP the
>   `HealerTower` BEHAVIOUR, which is the reference implementation of the field pattern.** OWNER RULING
>   2026-08-14: *"i do not know what the town healer is"* â†’ *"retire"*. It is unrecognisable because it
>   is **unreachable**: `tower_healer` appears in NO build category (`build-categories.json` lists only
>   `healing_caravan`), and `BuildCardArtRegression.cs:64` says so in a comment â€”
>   *"legacy Support verb only - not reachable from Town/Def"*. âš  **It cost a pin today:** WO-947 spent
>   owner ruling 2 (*"yes AoE healing"*) partly on a building nobody can build, and an agent reported it
>   as *"Support, not locked"* and player-reachable â€” a claim the data refutes. Third id-over-data
>   misread of the day (see WO-989, arcane-tower).
>   â›” **DO NOT DELETE THE BEHAVIOUR.** `StructureFactory.cs:935` `case "HealerTower"` is, per its own
>   header, *"WO-891. The FIRST instance of the general support/offensive FIELD pattern, and the proof of
>   its thesis: a new structure is stats plus TWO TAGS"* â€” and `:925` holds a commented-out
>   `case "SlowFieldTower":`, the intended next sibling. Deleting it discards the worked example the
>   pattern is meant to be copied from. **Retire the ROW and the menu/catalog surface; keep the code as
>   the documented reference.** READY.
> - **989** = **`tower_wall_wizard` still carries a wizard IDENTITY for a structure the owner renamed to
>   Ballista â€” rename the id (and prefab path) behind a READ-MIGRATION ALIAS.** OWNER ASK 2026-08-14:
>   *"tower_wall_wizard - Where did that name come from? Should match Ballista"*. Traced: the id dates
>   from the ORIGINAL build-catalog commit `9de2aac56`, where it genuinely was a wizard tower. The owner
>   ruling of **2026-07-08** (quoted in the row's own `orientation.note`) renamed the MODEL to a ballista
>   and the row was retuned to match â€” `displayName "Ballista"`, `element None`, `projectileStyle "bolt"`.
>   **The display name and the stats were renamed; the IDENTITY never was.** The `id` and
>   `visualPrefabPath: "Structures/WizardTower_1"` are the last two fields still calling it a wizard.
>   âš  **THIS IS THE COST OF LEAVING IT:** WO-947 read the row as MAGICAL *from its id* and would have
>   sent **70 crystals in the wrong direction**; it took an owner pin (2026-08-14, *"thats a baliista
>   mechanical"*) to settle a question the data had already answered. A stale identity is not cosmetic â€”
>   it actively misroutes downstream work, the same way the stale WO-number block and the hardcoded repo
>   root did.
>   â›” **NOT A FIND-AND-REPLACE.** The id is referenced in **15 files** AND **catalog ids are PERSISTED**
>   (save schema v36 `everBuiltStructureIds`; base layouts replay by id). A bare rename orphans every
>   saved town holding one. **Use the project's own precedent â€” the `harvest:3` â†’ `wood:3` token change
>   was READ-MIGRATED with no schema bump.** READY.
> - **988** = **`headed-dungeon-capture.ps1` reports `HEADED_CAPTURE_OK` on a run that loaded the WRONG
>   SCENE with a FROZEN CLOCK.** PROVEN 2026-08-14: a run tagged `wo1007-portal-camera` emitted
>   `HEADED_CAPTURE_OK 10 shots`; the copied `Player.log` from that same run says
>   `scene='Main_Castle_Overworld'` (the TOWN â€” the `-Scene` parameter is accepted and then never
>   forces a load) and `WORLD CLOCK FROZEN: Time.timeScale=0.00 ... The hero CANNOT move, turn or`.
>   All ten shots are the frozen town; the synthetic WASD landed in an **open bug-report text field**
>   visible in `10_facing_exit.png`. âš  **Same class as WO-984** â€” the harness proves a frame rendered
>   and nothing else, which is precisely what its own closing line already admits (*"A green marker
>   proves a frame rendered, never that it looks right"*). **FIX:** after load, assert from the live log
>   that (a) the ACTIVE SCENE equals `-Scene`, (b) `Time.timeScale > 0`, (c) the hero POSITION CHANGED
>   between `01_idle` and `03_forward_far`, and (d) no modal/text-input has focus. Any failure =
>   non-zero exit and NO marker. A capture that cannot fail is worse than no capture: it manufactures
>   evidence. READY.
> - **987** = **Dungeon exit portal: TOUCH to interact, then a "CONTINUE TO EXIT / CANCEL" confirm.**
>   OWNER RULING 2026-08-14, verbatim: *"should be action on interacting with the portal. Touch portal to
>   interact"*, *"if you want a confirm there that could be smart"*, *"confirm exiting portal"*,
>   *"continue or exit"*, clarified to **"continue to exit or cancel"**.
>   âš  **THE TWO FACES ARE `Continue to exit` AND `Cancel` â€” they are NOT two ways forward.** The first
>   reading ("Continue" vs "Exit") would have shipped a dialog offering to keep playing or to leave,
>   which is a different feature and an easy mis-build. Cancel RETURNS THE PLAYER TO THE RUN, unchanged.
>   Today the exit is proximity/prompt-driven; the ruling makes **contact with the portal** the trigger
>   and adds a **two-choice confirm** so a player cannot lose a run by walking into the exit. âš  Do NOT
>   reuse the raw violet plate â€” the interact button is being re-skinned to the Obsidian kit under
>   WO-1005 Part 1 (owner confirmed the purple goes, town included). READY.
> - **986** = **`PlacementGrid.FootprintCells` SQUARES the grid claim, so every THIN structure over-claims
>   on its narrow axis â€” WO-972 routed around this for walls only.** SURFACED 2026-08-14 while verifying
>   WO-972. `PlacementGrid.cs:235-238` computes ONE scalar and returns `new Vector2Int(cells, cells)`;
>   `StructureFactory.cs:693` collapses the mesh with `Max(b.size.x, b.size.z)`, discarding the 1.42 m
>   depth. Walls were fixed by feeding the claim a different METRIC (authored fp=2.1 -> `Ceil(2.1/3)=1`),
>   **not** by fixing the squaring. So any other row whose mesh overshoots one axis by even 1% still
>   claims a square block on its thin axis. âš  **This is an OWNER-SCOPED decision, deliberately NOT slipped
>   into the wall ticket:** a real fix means a non-square `(x,z)` footprint threaded through the grid, the
>   occupancy map, the yaw-inflation path (`:262-264`, `|sin|+|cos|`) **and every saved layout's occupancy
>   replay** â€” i.e. it touches every placeable structure and existing player saves. Decide whether thin
>   structures other than walls actually hurt in play before paying that. **SPEC â€” needs an owner call.**
> - **985** = **`DungeonHero.FaceHeading`'s dead `KeeperRelative` branch still applies `ModelYawOffset = 90f`
>   â€” the THIRD fragment of a matched pair whose other two halves were removed today.** 2026-08-14: the
>   camera's `_headingYawOffset = 90f` existed *solely* to undo `FaceHeading`'s `Euler(0,-90,0)`. Removing
>   only the `-90` left the camera 90Â° to the side (F8 seq 2328; delta constant 90.0 across 39 heartbeats).
>   Both halves are now zeroed â€” **but `KeeperRelative` is a third copy of the same offset, currently
>   unreachable.** It is bannered STALE, not deleted, because deleting an unreachable branch destroys the
>   evidence of what the pair used to be. âš  The hazard is specific: if anyone re-enables that branch it
>   re-introduces the exact bug against a camera that no longer compensates. **Do NOT "clean it up" and do
>   NOT flip it on to test â€” decide whether the branch has a future, then either wire it WITH a zeroed
>   offset or remove it in one deliberate edit that names the pairing.** READY.
> - **984** = **The Unity method wrapper judges success by LOG TEXT, not by a MARKER â€” so a gate that
>   never ran reports exit 0.** PROVEN THREE WAYS on 2026-08-14: (1) `powershell -File
>   tools\run-unity-method.ps1` â€” a path that **does not exist**, the runner lives at repo root â€” exited
>   **0**; (2) the same call with the script found but `-LogName` missing exited **1** only because
>   PowerShell's own mandatory-parameter check caught it, not the wrapper; (3) reading `Builds\build.log`
>   instead of the gate's own log showed `COMPILE_GATE_OK : 0` on a tree that was in fact clean. âš  **This
>   is the SAME defect class as the 44-row hollow-assertion registry, sitting in the tooling we use to
>   verify everything else** â€” and it is worse than a hollow trace, because a hollow gate makes every
>   downstream "verified" claim unfounded. The wrapper's own header admits the design (*"judge success
>   from the log (compile errors / exceptions / 'Aborting batchmode')"*) â€” that was a reasonable choice
>   when markers did not exist; markers exist now and are per-entry-point distinct (`COMPILE_GATE_OK`,
>   `REGRESSION_OK <n>/<n> suites`, `CHECKIN_SUITE_OK`, `SESSION_GUARDS_OK`, `UI_CAPTURE_OK`). **FIX:**
>   require the caller to declare the expected marker, and FAIL when it is absent, when the log is older
>   than the run, or when the log does not exist. Absence of an error is not evidence of success.
>   Acceptance: a deliberately-broken invocation (bad path, bad method name, stale log) must exit
>   NON-ZERO. Today none of those do. READY.
> - **983** = **Ground fog THROUGHOUT the composed dungeons** â€” owner direction 2026-08-14, verbatim
>   *"THIS THROUGHOUT THE DUNGEON"*, pointing at the Unity Particle Pack demo scene **Ground Fog**
>   (*"slow moving noise + a sprite sheet animation to give the effect of rolling fog"*).
>   **THE KEY IS ALREADY CATALOGUED â€” map it verbatim, do NOT pick a substitute** (memory
>   `vfx-map-owner-tags-no-creative-pick`): `PP_GroundFog` â†’
>   `Assets/UnityTechnologies/ParticlePack/EffectExamples/Smoke & Steam Effects/Prefabs/GroundFog.prefab`.
>   âš  Do NOT use `Env_GroundFog` â€” that `VFXType` ordinal is an ORPHAN with **no catalog row and no
>   prefab** (`VFX_AUDIO_WIRING_MAP.md`); it looks like the right name and renders nothing.
>   **Today `PP_GroundFog` has exactly ONE consumer â€” `DungeonWorldPortalSpawner`, the OVERWORLD portal.
>   Nothing inside a dungeon plays it.** That is the whole gap; the asset and the key both already exist.
>   â›” **THE TRAP, AND IT IS THE EXPENSIVE ONE.** That prefab lives in a **GITIGNORED** root
>   (`Assets/UnityTechnologies/`, the 191 MB Particle Pack). Referencing it straight from the bake
>   reproduces the 2026-08-06 P0 verbatim â€” **27 of 28 tracked VFX prefabs / 183 references pointed into
>   gitignored art** and rendered magenta-or-nothing on every machine without the packs. It works HERE
>   and is invisible until a clone. **Mirror it first** via `VfxResourceArtMirror` into
>   `Assets/Resources/VFX/` (deps included â€” `CopyAsset` duplicates the PREFAB ONLY), then wire the
>   mirrored copy, then confirm `VFX_ART_MIRROR_OK`.
>   âš  Second trap: **fog is a LOOP.** A loop played fire-and-forget permanently consumes one of the
>   **20 global loop slots** and never returns it (the 08-06 loop-cap P0 â€” after ~20 the archer renders
>   no projectile and the Tree of Life aura starves). Seat one instance per ROOM with a retained handle
>   released on room unload, or declare a finite lifetime so `VFXManager` routes it through the
>   leak-proof oneshot path. **Never one per tick, never per enemy.**
>   Seat it in the bake beside the existing dressing pass (`DungeonDresser.DressRoom`, already seats ~8
>   props/room and is wired into `DungeonBaker` pre-NavMesh), so every composed dungeon gets it and the
>   fleet cannot diverge. Acceptance = a headed capture (`tools/capture/headed-dungeon-capture.ps1`)
>   showing rolling fog in `dg_ember_deep`, **plus** the absence of `SKIPPED - active loops 20/20`
>   across that run. **READY.**
>
> *(banner bumped 983 â†’ 984 in the SAME edit as the mint.)*
> - **982** = **`GraphDungeonComposer` emits the compose-layout to StreamingAssets ONLY â€” so every bake
>   silently creates dual-copy drift, and Resources (the copy that WINS at runtime) keeps the stale one.**
>   PROVEN BY A BAKE, 2026-08-14: a clean `ComposeAllBatch` run left **all 7** `dg_*.json` layouts drifted,
>   StreamingAssets stamped 09:00 today against Resources still at 08-08/08-10. Synced by hand this time.
>   âš  **This is the ROOT of the 2026-08-08 incident that `5f0e23aa` treated as a one-off** â€” that commit
>   caught `dg_sunken_vault.json` holding the OLD 17-room layout in Resources and fixed the file; the
>   MECHANISM that produced it was never fixed, so it reproduced across all seven the very next time
>   anyone baked. **âš  And nothing catches it:** `RoomForgeRegression.cs:162`'s dual-copy sweep is a
>   hardcoded 3-file list containing **no `dg_*` layout at all** (audit F24), so the gate is structurally
>   blind to exactly the files the composer writes. FIX = have the emit path write BOTH canonical roots
>   (mirror `CanonicalJson`'s dual-copy law), **and** widen `RoomForgeRegression` to enumerate every
>   `dg_*` layout on disk rather than a literal list â€” the guard and the writer must land together or the
>   next bake re-opens it. **READY.**
>
> *(banner bumped 982 â†’ 983 in the SAME edit as the mint.)*
> - **981** = **The skill-point grants in `HeroProgression` are INFERRED and silently droppable** â€”
>   found by the orchestrator while gate-reviewing the WO-977 fix, 2026-08-14. Two sections, one file,
>   one fix session. **Â§A â€” the starter latch is not persisted, it is GUESSED FROM LEVEL.**
>   `HeroProgression.cs:202` (`RestoreFromSave`) does `if (_level > 1) _hasGrantedStarterPoints = true;`
>   on the stated assumption *"a restored hero past level 1 already received its first-level-up starter
>   gift in the run that earned the level"* â€” **which is precisely the assumption WO-977 disproves.** So
>   WO-977's retry-on-next-level-up holds only WITHIN a session: a player whose grant failed at the
>   level-1â†’2 boundary and then reloads is re-latched at `:202` and loses both points permanently. The
>   durable fix is to persist the latch (a `SaveSchema` bump â€” mind the CORE_SAVE version-triple oracle,
>   `SaveMigrator` top step must equal `CurrentVersion`) **or** derive it from a measured point count
>   rather than from level. âš  **Until this lands, WO-977's new Fail message overstates its guarantee**
>   and the message wording must say "this session". **Â§B â€” the per-level grant at `:259` drops a point
>   silently on a null `SkillSystem.Instance`.** It IS `try`-wrapped, so a *throw* is loud, but the `?.`
>   no-op is not: `SkillSystem.Instance` is a plain static self-bootstrapped `AfterSceneLoad` while
>   `HeroProgression.Bootstrap` is `BeforeSceneLoad`, so the null window is real, not theoretical â€” and
>   this one fires on EVERY level, not once. Same treatment as WO-977: measure the `AvailablePoints`
>   delta, `Fail` naming the consequence when it does not move. **READY.**
>
> *(banner bumped 981 â†’ 982 in the SAME edit as the mint.)*
> - **980** = **Dungeon camera FRAMING after the WO-968 fix â€” blown-out wall, hero as silhouette.**
>   The camera fix itself is PROVEN (43 heartbeats, 15 distinct rig poses, heal line fired once naming
>   the DESTROYED CinemachineFollow verbatim). This is about what the now-working camera SHOWS:
>   `03_walk_end.png` / `08_final.png` are dominated by a near-white wall with the hero a black
>   silhouette against a torch. **Kept separate from WO-968 deliberately** â€” *"the camera follows"* and
>   *"the player can see where they are going"* are two claims and only the first is proven; folding
>   them would let a proven fix carry an unproven one. **May well be WORKING AS INTENDED**: the old
>   camera was parked across the room, so this is the first time anyone has seen the intended
>   over-the-shoulder framing. â›” **OWNER RULING FIRST, asked as behaviour not colour** (she is
>   red/green colourblind): *can you tell where you are going, or does it read as a bright blur?*
>   Candidate fixes if a defect â€” torch intensity, bloom/post-exposure, rig distance/height, hero
>   rim-light. âš  **Do NOT touch the follow logic or `HealBodyStage`.** Before/after proof pairs in
>   `docs/proof/2026-08-10-dungeon-headed{,-AFTER-camera-fix}/`. **READY (blocked on her ruling).**
> - **979** = **`WaveFeedbackDirector` reports a HUD bind that can never succeed** â€”
>   `Assets/_Modules/Village/Waves/WaveFeedbackDirector.cs:321` prints
>   `hudBound={CoreServices.Hud != null}` while `FindHud()` at `:325` is a **stub whose entire body is
>   `return null;`**. The bind is ALWAYS null. This is not merely an unfalsifiable trace â€” **it reports
>   a DIFFERENT VARIABLE than the one it names**, so a reader checking "did the wave HUD bind?" gets an
>   answer about `CoreServices.Hud` instead. Either finish `FindHud` or delete the seam and say so;
>   what must not stand is a stub with a trace that implies it works. **READY.**
> - **978** = **Economy callers echo the amount REQUESTED, not the amount CREDITED** â€” an entire class,
>   found in the 2026-08-10 hollow-assertion audit: `RaidVictoryController.cs:277`,
>   `DailyQuestRewardBridge.cs:126`, `ChallengeOutpostVictoryController.cs:147`,
>   `PopulationService.cs:211` all log the value they passed in as though it landed.
>   `EconomyService.Grant` routes to the **clampable `EarnedIncome`** kind, so **a capped town bank pays
>   0 while the log reads `+500 crystals`.** âš  The authority itself is HONEST â€” `EconomyService.cs:416`
>   prints the post-clamp amount AND the resulting total â€” so the fix is caller-side: log the returned
>   credited amount, never the argument. Player-facing: this is the shape of "I did the raid and got
>   nothing" being invisible in every capture. **READY.**
> - **977** = **Starter skill points can be silently never granted, and the latch says otherwise** â€”
>   `Assets/_Modules/Village/Hero/HeroProgression.cs:269` logs *"granted 2 starter skill points"*, but
>   the latch flips at **`:266`, BEFORE** two null-conditional grants which â€” unlike the identical call
>   twelve lines above â€” are **NOT** wrapped in the try/catch that would `Fail`. A null `SkillSystem`
>   yields **zero points, latched forever**, with the log reading granted. **Fires for every player
>   exactly once**, which is the worst possible cadence: unreproducible on a second run of the same
>   save. Fix = grant first, latch only on confirmed success, and wrap it like its neighbour. **READY.**
> - **976** = **`hasSurface` is a false green â€” `panelSettings=ok canvas=ok` proves nothing** â€”
>   `Assets/_Modules/Core/UI/AddressableUIManager.cs:234` emits
>   `panelSettings=ok canvas=ok => hasSurface=`, but both halves are **non-null checks**. A panel with
>   both references present can still be **zero-sized, offscreen, behind another sort order, or fully
>   transparent** â€” and the line prints `ok` through every one of those. Same disease as WO-973's
>   `bubble=ok`, on a **far more trafficked path**: this is the shared UI surface resolver, not one
>   NPC's speech bubble. Found by sweep during the WO-973 read-only prep, 2026-08-10.
>   Weaker siblings, same shape, listed so the sweep isn't repeated: `CompanionGearSetup.cs:208`
>   (`result=ok` after an `AddComponent` that essentially cannot return null) and
>   `HudCompassWidget.cs:529` (`hero=ok`, a non-null check). `TowerLoopDevHarness.cs:171` is a dev
>   harness â€” ignore.
>   âš  **The fix is NOT to delete these lines** (Â§12 â€” instrumentation is never stripped). It is to
>   make them assert something that can FAIL: resolved rect size, visibility, sort order. A trace
>   that cannot fail is worse than no trace, because it actively steers the next reader away from the
>   broken thing â€” which is precisely what cost this project a pixel-discovery on WO-973. **READY.**
> - **975** = **The `Gear` Addressables group points at a GITIGNORED art pack** â€” architect verified
>   2026-08-10. `AddressableAssetsData/AssetGroups/Gear.asset` is **git-TRACKED** and holds **426
>   entries**; resolved GUIDs land in `Assets/Blink/Art/...`, and `git check-ignore -v` returns
>   `.gitignore:350:/Assets/Blink/`. **A tracked group asset ASSERTS content that a fresh clone does
>   not have** â€” worse than the `polyperfect`/`KayKit` case, because the assertion looks authoritative.
>   Consequence on a clone/CI: 426 dangling entries â†’ a degenerate `gear_assets_all_*.bundle` â†’
>   `EquipmentController.cs:744`, `HeroArmorVisual.cs:199` and `HeroBodySwapper.cs:148` all fail their
>   `LoadAssetAsync` = **no weapons, no armour, no hero body**. Existing partial net
>   `DataRegression.cs:2642` (`AddressableKeyExists`) returns `false` on throw with only a
>   `LogWarning`, so it is a soft signal, NOT a fence. Fix = promote the referenced prefabs into a
>   tracked location (precedent: the `Resources/Structures` negation at `.gitignore:150-176`) and/or a
>   regression that HARD-fails when entry count â‰  resolvable-GUID count. **READY.**
> - **974** = **The Addressables content build has NO SEAM â€” it rides a machine-local Editor
>   Preference** â€” architect verified 2026-08-10. `AddressableAssetSettings.asset:61` â†’
>   `m_BuildAddressablesWithPlayerBuild: 0`, and the enum at package source
>   (`AddressableAssetSettings.cs:210-215`) reads `PlayerBuildOption.PreferencesValue = 0` â€” *"use the
>   global settings stored in preferences."* There is **ZERO** explicit content build in the tree:
>   `WebGLBuild.cs:127`, `DesktopBuild.cs:241` and `AndroidBuild.cs:105` each call
>   `BuildPipeline.BuildPlayer` and nothing else; no `BuildPlayerContent` call exists anywhere under
>   `Assets/`. **Whether bundles are rebuilt is decided by an uncommitted per-machine preference.** It
>   is evidently ON on this box (tonight's build emitted fresh bundles), which is exactly what makes it
>   dangerous â€” it works here by luck, and a fresh clone / CI runner / a seat that ever toggled it ships
>   **stale or absent** `StreamingAssets/aa` with **NO loud failure**; Addressables simply cannot
>   resolve `gear/*` at runtime. Fix = either `m_BuildAddressablesWithPlayerBuild: 1` or an explicit
>   `AddressableAssetSettings.BuildPlayerContent()` at the head of each build entry point, logging its
>   result. **Deliberately NOT landed in the 2026-08-10 release window â€” it is a build-path change.**
>   **READY.**
> - **973** = **Bryn's speech bubble is a giant skewed world-space card** â€” found in the PIXELS during the
>   WO-968 headed dungeon proof (`Dungeon_HealersCottage`, 2026-08-11), not by the owner. Screenshots
>   `01_idle`â€“`05_right` are ~60 % covered by a trapezoid card reading *"Bryn the Waâ€¦ / The path opens
>   easyâ€¦ / mind the rocks â€” thâ€¦ / cottage keeps her shâ€¦"*, clipped off the right edge. **The trapezoid
>   IS the diagnosis:** a screen-space canvas cannot skew, so this is a **world-space** canvas seen in
>   perspective at wildly wrong scale. Paired data from the same run:
>   `[Flow:Dungeon] Bryn.Configure 'bryn-the-wanderer' at (-31.00, 0.00, -2.00) (speakRadius=6, bubble=ok)`
>   â€” i.e. `bubble=ok` reports success while the thing is unreadable, so the trace is asserting
>   construction and NOT legibility. It clears when the hero leaves the 6 m speak radius, which is what
>   proves it is the speak bubble and not a HUD panel.
>   Dialogue lane. Text is CLIPPED, so this is a readability defect, not cosmetic.
>   âš  Do NOT "fix" it by moving the camera â€” the parked WO-968 camera was frozen at the bind seat for
>   this entire run, so the card's apparent size is measured against a stationary camera 5 m away.
>   Re-measure AFTER the camera fix lands before choosing a scale, or you will tune against a bug.
>   **READY (needs a headed re-shot post-camera-fix as its first step).**
> - **972** = **Walls cannot be built beside each other** â€” owner F8 **seq 2327**, verbatim:
>   *"cannot build walls beside each other"* (`Main_Castle_Overworld`, 2026-08-11 02:05 UTC).
>   PROVEN BY CAPTURE, her Player.log:
>   `[Flow:Build] REJECT Occupied cell=(17,16) fp=(2x2) gate=CellGrid occupantCell=(17,17) occupant='wall_wood'`
>   â€” **a wall claims a 2x2 block**, while `[Flow:Structure] 'wall_wood' carries Collider 'MeshCollider'
>   bounds size=(3.03, 3.73, 1.42)` proves the palisade is a **one-cell tile** (3.03 m across, 1.42 m
>   thick) on a 3.00 m cell. TWO COLLAPSES STACK: `MeasureUprightFootprintMetres` reduces the mesh to
>   `Max(size.x, size.z)` (the 1.42 m depth is discarded), then `FootprintCells` ceils **and squares** it
>   â€” so a **1 % overshoot** (3.03 on 3.00) doubles the claim and re-applies that doubling to the thin
>   axis that was never over a cell. Second symptom, same root: her landed run sits on a **6 m pitch**
>   (`Occupy 12_17 / 14_17 / 16_17`, centres x=-7.50/-1.50/+4.50) â€” every wall run has a ~3 m hole
>   between segments.
>   âš  NOT intentional pathing protection â€” the gate-clearance rule reports `BlocksGate` and never fired;
>   the reject is `gate=CellGrid`. Scaffold / singleton / render-elsewhere all eliminated from the trace.
>   FIX IS CLAIM-SIDE ONLY, **the mesh is never touched** â€” so the walls-excluded-from-height-cadence
>   carve-out holds, and the **NavMeshObstacle is byte-identical** (`Clamp(rendered*0.85, cellSize, claim)`
>   resolves to the captured 3x3 m box at BOTH the old 2x2 and the new 1x1 claim). **No save migration:**
>   `CellToWorld` seats on the ORIGIN CELL centre independent of footprint, so every saved wall replays
>   in place and merely claims fewer cells.
>   ALSO SHIPPED: **words, never colour alone** (owner is red/green colourblind) â€” the refusal now names
>   the occupant, and a permanent `FlowTrace.Once` states authored-vs-measured metres, the datum that was
>   logged NOWHERE and had to be bounded from a collider dump during RCA.
>   Regression `WallAdjacencyRegression [wall-adjacency]` written; registration handed to the committer
>   (`DataRegression.cs` is lane-fenced).
>   File `WorkOrders/WORK_ORDER_972_walls_cannot_be_built_beside_each_other.md`.
>   **Code in tree, brace/NUL clean â€” awaiting batch-gate + commit; PO felt-verifies + closes.**
>
> *(banner bumped 972 â†’ 973 in the SAME edit as the mint.)*
> - **971** = **Remove the original tutorial â€” ONE tutorial, ONE guide** â€” owner ruling 2026-08-10,
>   verbatim: *"why are two tutorials active?"* / *"remove the original"* / *"only the new wolf one
>   stays"* / *"from data"*. PROVEN BY CAPTURE, her Player.log on the 20:42 build:
>   `[Flow:SylasSteward] Sylas steward spawned at (2.00, 0.08, 3.00)` and
>   `[Flow:Tutorial] guide BODY summoned ('ice-wolf') at (2.00, 0.06, 3.00)` â€” **two guide bodies two
>   centimetres apart** â€” with the ONE spotlight alternating, `FocusMask resolved highlightId=world.guide
>   target=Sylas` then `target=Pet_ice-wolf`, while the strip read "Follow Aldwin to the gate". Her
>   screenshot shows the gold ring on a peasant NPC with the wolf inside the same ring.
>   âš  TWO HALVES, ARMED BY DIFFERENT ROUTES â€” this is why it had to come from data: `TutorialDirector`
>   (the legacy FTUE flow) was **dormant-but-present** (self-destructs at `Title`, plus an ff.tutorialv2
>   stand-down) â€” exactly the state the ruling forbids; `SylasStewardInjector` **armed every hub load**
>   and is the half she actually saw. WO-1014 retired the nine legacy `tut_*` DIALOGUE ids correctly, but
>   the injector spawns independently of any dialogue.
>   âš  WO-1014's "gate the stand-in" fix was committed and in her build and **never executed** â€” its own
>   stand-down trace occurs **ZERO** times in her log while the steward is still on screen at t=127s. The
>   owner overruled the approach regardless: a fallback that can share the screen with the real guide is
>   a second guide. REMOVED, not gated.
>   REMOVED: `SylasStewardInjector.cs`, `TutorialDirector.cs`, `PetIntroduction.cs`,
>   `TutorialDirectorHubGateTest.cs`, and `ResolveGuide`'s stand-in link (chain is now pet body â†’ Heart).
>   `AssertStewardSurvivesNewGame` **reversed** into `AssertExactlyOneGuideBody` â€” it asserted the
>   opposite of the ruling and would have gone GREEN on the build she rejected.
>   âœ” CARVE-OUTS VERIFIED, NOT ASSUMED: **Sylas the CHARACTER stays** (`HeroCanonNames.cs`,
>   `hero.ranger` in en.json, his abilities.json kit, `SylasFirstMeeting`) â€” only his guide-BODY role
>   went. And the "legacy" tutorial folder is NOT wholly legacy: `TutorialFlow` adds `TutorialWaveSpawner`,
>   `DialogueCommandSink` adds `TutorialAutoWalk`+`TutorialHudOverlay`, `ElaraWaveThreeJoin`/
>   `StoryCompanionInjector` call `CompanionSpawner` â€” deleting the folder would have been an
>   orphaned-reference outage that compiles and blanks. Both carve-outs are now regression-pinned.
>   Regression `OneGuideBodyRegression [one-guide-body]` rewritten (6 cases, dry-run green on the real
>   tree) + the runtime `AssertExactlyOneGuideBody` 8s watch window.
>   ALSO IDENTIFIED, ticket separately: her *"vfx yes thing on the tree"* is **`Poi_NodeAura` â†’
>   `Magic circle sun loop`**, the POI callout for the invisible DDOL `Collector_lumbermill` stranded at
>   world (0,0,0) â€” 12 m in front of the Heart anchor, so it reads as a plume at the roots.
>   `AmbientAuraPolicy` misses it because it gates by KEY string, not prefab.
>   File `WorkOrders/WORK_ORDER_971_remove_the_original_tutorial_one_guide_only.md`.
>   **DONE â€” awaiting batch-gate + commit; PO felt-verifies + closes.**
>
> *(banner bumped 971 â†’ 972 in the SAME edit as the mint.)*
> - **970** = **The bounds align can only YAW â€” a weapon whose mesh is not authored Y-long never stands
>   up** â€” owner felt-report, playing the Mage: the Emberglass Staff (`tripo_staff_a`) *"is not being held
>   correctly."* PROVEN BY CAPTURE, her Player.log, then settled at source.
>   `WeaponBoundsOrient.AlignAxesYLongXNarrowZWide` built its result as
>   `Quaternion.LookRotation(Cross(xAxis, yAxis), yAxis)` with **`yAxis = Vector3.up` a CONSTANT** â€” so the
>   output was **yaw-only BY CONSTRUCTION**, and a yaw can never lift a Z-long mesh onto +Y. `alignLong`,
>   the only term that could tilt it, was used to pick a sign and discarded. Capture signature, twice, a
>   month apart: staff `raw b0=(0.001,0.001,0.021) -> aligned b1=(0.021,0.001,0.001)` and shield
>   `raw (0.008,0.002,0.01) -> (0.01,0.002,0.008)` â€” both just X and Z SWAPPED, longest on X, never Y.
>   Four downstream seats are written against "prop +Y = the long axis" (`EnsureHandleAtShortYEnd`,
>   `SeatHiltLowerHalf`, `ComputeMeleeGripRotation`, `ComputeSheathRotation`), so all four ran on the
>   staff's **1 mm thickness axis**: sheathed `worldBounds=(0.079, 0.097, 1.265)` â€” the whole 1.265 m along
>   world Z, **dead horizontal through her back** â€” and a **2 cm** grip seat on a **1.3 m** haft.
>   âš  The 2026-07-06 shield RCA stood on this exact line, fixed the SCALE symptom, and recorded verbatim
>   *"the align's ROTATION is left as-is"* â€” half-fixed a month ago.
>   âš  **INDEPENDENT of WO-966** (settled at source, NOT assumed): `HeroBodySwapper.cs:263` applies the -90
>   to the BODY ROOT and the skeleton is its child, so mesh and prop rotate TOGETHER â€” a body yaw cannot
>   change how the weapon sits relative to the body. Landing 966 would have changed nothing here.
>   âš  NOT staff-specific â€” permutation-specific: any prop whose SOURCE mesh is not authored Y-long. A
>   Y-long greatsword passes untouched, which is why swords looked fine and this survived.
>   âœ” CLEARED, not assumed: the 1.72 parent-scale compensate is CORRECT (`1.264 x 1/1.72 x 1.72 = 1.264`,
>   matched at BOTH sockets). Adjacent find, ticket separately: back compensates unconditionally (`:1819`)
>   while the hand guards on `_weaponParentCompensate` (`:1834`) -> a `fullOverride` prop renders a
>   different SIZE drawn vs sheathed (`shield_A` is the live candidate).
>   FIX (landed, one file): `localRotation = Inverse(LookRotation(Axis(med), Axis(lng)))` â€” DERIVED basis
>   change, no compensating Euler, no pitch where a yaw is meant. Permanent `[Flow:Equip] AlignAxes` trace
>   added: `longAxis=` + a Y-longest `aligned b1` on her next equip is the proving line.
>   âš  OWNER PIN (Â§4, untouched): `_staffGripEuler=(0,90,0)` and `sword_A` rot `(117,-2,110)` were dialed on
>   the broken base and may want a re-dial â€” her hands, not ours.
>   File `WorkOrders/WORK_ORDER_970_weapon_align_is_yaw_only_long_axis_never_reaches_y.md`.
>   **DONE â€” awaiting batch-gate + commit.**
>
> *(banner bumped 970 â†’ 971 in the SAME edit as the mint.)*
> - **969** = **Opening Pause over the victory summary destroys the pending home return (45s strand)** â€”
>   owner F8 **2315**, scene `Dungeon_HealersCottage`. PROVEN BY CAPTURE, whole chain in her Player.log:
>   `PanelManager:NotifyOpened` ('Pause') -> `previous.Close()` -> `EndStateView.CloseFromArbiter`
>   -> `Destroy(gameObject)` -> `PostureSignals:SetEndState(false)` (`EndStateView.cs:1665`), then
>   `[BREAK] error: [Flow:BattleArena] STRANDING WATCHDOG FIRED after 45s - the victory panel was
>   destroyed without firing its Continue action, so the deferred home return never ran. Returning the
>   hero anyway. If you are reading this, find WHAT destroyed the end-state (a wave banner or another
>   modal opening over it) - the watchdog is a safety net, NOT the fix.` (`BattleArena.cs:2495`).
>   ROOT CAUSE, read at source: the arena's ONLY route home (`doMaskedReturn`) was owned by
>   `EndStateVM.Primary`, i.e. by a GameObject any modal may destroy. Pause is registered
>   `RegisterBattleAllowed` (`PauseController.cs:182`) so no gate refuses it â€” and none should.
>   FIX = shape **(c)**: the transition is made INDEPENDENT of the panel's lifetime (hand-back on the
>   MODEL, `EndStateVM.HandBackPendingTransition`, called from BOTH abandon choke points). (a) would
>   have to block Pause; (b) fixes only Pause and leaves the other two destroy paths. Watchdog kept
>   verbatim + pinned by the regression.
>   File `WorkOrders/WORK_ORDER_969_endstate_pending_transition_handoff.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 969 â†’ 970 in the SAME edit as the mint.)*
> - **968** = **HIGHEST â€” Dungeon locomotion: mover ownership, dead camera basis, frozen camera** â€”
>   owner F8 **2312** verbatim: *"This problem  gets marked as Highest on the board. Everything is wrong
>   check locomotion"*, and F8 **2313** 22 s later, same scene: *"No camera movement"*. Both in
>   `Dungeon_HealersCottage`. PROVEN FROM THE CAPTURE, not theorised â€” three seams, one shape:
>   **(1)** the hero's mover FLIPS mid-session and nothing logs which is live â€” `[Flow:HeroLoco] vel=0.00`
>   while the root moved (neutralize ON, `DungeonHero` moving; `dYaw=12.0` at 60 fps is exactly its
>   720 deg/s cap) versus `[Flow:HeroDrift] vel=(0.000,5.000)` with live input, which is only reachable
>   when the neutralize is OFF (`SetScriptedMove(zero)` -> `ReadMoveInput` at `HeroLocomotion.cs:1517`
>   would make the `input.y > 0.5` gate impossible). **(2)** the animator is fed a COMPONENT, not the
>   world â€” `ActorAnimator` is the sole `Speed` writer and takes `HeroLocomotion.Velocity` (`:1107`),
>   dead by design in a dungeon, while `DungeonHero`'s competing write can be a permanent no-op because
>   `_animator` is resolved ONCE in `Awake` (`DungeonHero.cs:138-149`), before the async body swap.
>   **(3)** the movement basis is IDENTITY â€” `[Flow:HeroDrift] camYaw=0.0` on every line because that
>   field is `SmartMobileCamera.CameraYaw` and **no `SmartMobileCamera` exists in the scene** (script
>   GUID count 0), so the stick is world-absolute; `DungeonHero` meanwhile uses `Camera.main`. The
>   camera itself is parked at `yaw=180` = `spawn.facingY 90` + `_headingYawOffset 90`, i.e. its
>   Bind-time seat (authored yaw is 0, so it did move once and then stopped).
>   âš  Carries a **MASKING WARNING**: fixing the basis alone while the camera is frozen inverts the
>   stick 180 deg and reads as a NEW bug â€” camera + basis must ship together.
>   âš  **INDEPENDENT of WO-966** (that is the constant 94.5 deg Mage MESH yaw, every scene); this is
>   dungeon-only mover/basis ownership. They STACK â€” do not tune one against the other. The `dYaw`
>   swings are `DeltaAngle(0, rootYaw)` and are a SYMPTOM of the dead basis, not a third facing defect.
>   Instrumentation (3 permanent heartbeats: `[Flow:HeroOwner]`, `[Flow:DungeonMover]`,
>   `[Flow:DungeonCam]`) is ALREADY LANDED â€” every remaining unknown is now one capture away.
>   File `WORK_ORDER_968_dungeon_locomotion_ownership_and_camera_seam.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 968 â†’ 969 in the SAME edit as the mint.)*
> - **967** = **The dungeon action bar defaults to the KNIGHT kit (hardcoded literal)** â€” owner F8 2312,
>   verbatim: *"in dungeon i have the knights action bar loading"* + *"as Thrain"*, playing a MAGE.
>   SETTLED FROM SOURCE, no further capture needed: three hand-written `"knight"` string literals in
>   `HudModelProducers.cs` (**:392** the reported bug, **:87** + **:139** latent). NOT an enum-zero
>   default â€” `HeroClass`'s zero is Mage (`Enums.cs:49`) and `AbilityCatalog.DefaultClass` is `"mage"`.
>   Dungeon-only because the composed hero is baked with HeroLocomotion + HeroBodySwapper only
>   (`DungeonBaker.cs:1168-1187`) and `EnsureHeroCombatComponents` provisions nine components but never
>   `HeroAbilities` â€” so `FindAnyObjectByType<HeroAbilities>()` is null and `:392` asserts Knight. The
>   NAME stayed right (Thrain IS the Mage name, `en.json:145`) because `HeroVitalsProducer` has a sticky
>   `_classId` cached from town across the `DontDestroyOnLoad` host and the ability producer has none.
>   âš  This is a REPEAT of the F8 seq-642 defect already fixed in `GearLoadout.CurrentJob` â€” same
>   persisted-state fallback, never applied to the second reader. âš  The seam is INSTRUMENTATION-SILENT
>   (zero hits across Player.log + break-log for every ability/identity tag), which is half of why it
>   cost a session; the WO ships the traces regardless of the fix.
>   File `WORK_ORDER_967_dungeon_action_bar_defaults_to_knight.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 967 â†’ 968 in the SAME edit as the mint.)*
> - **966** = **Hero body faces the wrong way while running (Mage NW when running N)** â€” owner F8 2309.
>   MEASURED, not guessed: `HeroFacingAudit.MeasureAll` reports Mage needs **4.5 deg** and Ranger **3.7 deg**
>   to face +Z, while `HeroBodySwapper.cs:263` applies **-90** to every non-Knight body â€” a **94.5 deg**
>   error on the mesh. KnightV3 measures 0 and agrees, which is why only the new classes show it. The -90
>   is the Tripo-era convention applied to CC/AccuRIG models that arrived 2026-08-06. Fix is a one-line
>   owner choice (derive vs constant) recorded in the WO. **âš  RENUMBERED from 965** â€” the CLI seat wrote
>   the file without bumping this banner (the 08-02 collision failure, repeated); the F8-queue lane minted
>   965 correctly and is cited in `CLAUDE.md:431`, so it keeps the number.
>   File `WORK_ORDER_966_hero_facing_offset_when_running.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 966 â†’ 967 in the SAME edit as the mint.)*
> - **965** = **F8 inbox is a QUEUE â€” no owner capture is ever dropped again** â€” a real harness defect,
>   proven on disk today: the seat acked seq **2306**, the next ping it ever saw was **2309**, and seq
>   **2307** (*"both NPC and echo but no movement"*) + **2308** (`[Flow:Tutorial] STEP-STUCK ::
>   founding_walk`) reached NO seat. Cause: `LATEST_CAPTURE.md` + `PING.json` were single slots (a burst
>   overwrote itself), `f8-ack.ps1` acked PING's **newest** seq (burying everything below it), and the
>   per-seq file name `capture-<HHmmss>.md` collided inside one second. Fix = append-only
>   `logs/f8-inbox/QUEUE.jsonl` + per-seq capture files + oldest-first `f8-check-inbox.ps1` +
>   one-at-a-time `f8-ack.ps1`, with supersede/unqueued/lost all LOUD in `queue-events.log`. Exit codes,
>   PING seq and ACK watermark contracts preserved; the owner changes nothing.
>   File `WORK_ORDER_965_f8_inbox_capture_queue_no_drops.md`. **DONE â€” awaiting batch-gate + commit.**
>
> *(banner bumped 965 â†’ 966 in the SAME edit as the mint.)*
> - **964** = **Unearned structures are HIDDEN, not shown-locked** â€” owner F8 2303, verbatim: *"dont show
>   the spire, leave as blank till earned, allows us to unlock new items and not reveal what they are"*.
>   âš  REVERSES WO-1013's visible-locked Spire card, which shipped the SAME DAY (`bd9d54d9`); both rulings
>   are recorded in the WO. Good news: it is a DATA move â€” `build-categories` already has both buckets
>   (`lockedIds` filters the row OUT, `visibleLockedIds` renders it greyed), so the Spire moves buckets
>   and `ProgressionUnlocks.IsUnlocked` is already the earn gate. âš  Carries an OWNER QUESTION: this is the
>   opposite policy to WO-960's armor-store greyed ladder, which also shipped today â€” both can be right
>   (shop = aspiration, new structure = surprise) but only one can be the house rule.
>   File `WORK_ORDER_964_hide_unearned_structures_until_unlocked.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 964 â†’ 965 in the SAME edit as the mint.)*
> - **963** = **Build carousel follows the tutorial's teaching order** â€” owner F8 2302, verbatim: *"Can
>   we order the carousel in order of how the tutorial presents them?"* RCA'd live: there is NO sort â€”
>   `BuildPaletteVM.Rebuild` foreaches the registry query, so the order IS the row order in
>   `structures-catalog.json`. Fix = an owner-tunable display order in the catalog (data, not code),
>   seeded Lumbermill â†’ Tower â†’ Workshop â†’ Armorer per `tutorial-steps.json` orders 20/30/1050/1060,
>   with current catalog order as the stable tiebreak. The palette must NOT read the tutorial script at
>   runtime â€” presentation never depends on a teaching flow.
>   File `WORK_ORDER_963_build_carousel_tutorial_order.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 963 â†’ 964 in the SAME edit as the mint.)*
> - **962** = **`guide_gate` must LATCH, not re-resolve â€” the WALK beat chases a moving gate** â€” owner
>   F8 2301, proven in her Player.log: the anchor resolved to `WaveSpawnPoint-S` (-3.43,0.08,-38.63),
>   then `guide-lead SET` fired again at (37.29,0.08,-0.21) [east] and (3.07,0.08,38.68) [north] as she
>   walked, so `hero.reached:guide_gate` was never reachable and the step STEP-STUCK at 123s and was
>   watchdog-SKIPPED. Fix = resolve the gate ONCE on step ENTER and latch it for the step's life.
>   Pure logic, no art. File `WORK_ORDER_962_guide_gate_anchor_latch.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 962 â†’ 963 in the SAME edit as the mint.)*
> - **961** = **The founding Echo guide gets a BODY, and it is the Ice Wolf** â€” owner ruling 2026-08-10
>   ("we should have Ice wolf", pointing at `Assets/Resources/Pets/ice-wolf.fbx`). REVERSES the
>   2026-07-16 call recorded at `TutorialFlow.cs:1307-1319` (aether-sprite "ethereal spirit, NOT the
>   quadruped ice-wolf that T-posed"). Proven at source: the body is not spawned AT ALL today â€”
>   `[Flow:Tutorial] grant.starterPet â€” visible echo MODEL birth SCRAPPED (echoes are portrait cards
>   now)` â€” while the WALK objective still says "Follow {guide} to the gate". Real cost is NOT the mesh
>   (tracked, and `PetDeployer` already loads `Pets/<species>`): `ice-wolf.fbx.meta` is
>   `animationType: 2` / `avatarSetup: 0` / `clipAnimations: []`, there are ZERO `.controller` and ZERO
>   `.anim` under `Resources/Pets`, and `Pets/Pet` + `Pets/PetIdle` are in the known-missing baseline â€”
>   so it needs a rig + idle + walk + controller or it ships as a sliding bind-pose statue (QR-5.3).
>   âš  The comment claiming aether-sprite is "the only HUMANOID rig" is FALSE at source â€” its meta is
>   Generic with no avatar too; it only reads ethereal because `EchoSpiritPresentation` hovers it.
>   Canon is on the ruling's side: the unlock card in her session reads `id=echo-frosthowl`, and
>   Frosthowl IS the ice wolf. File `WORK_ORDER_961_founding_guide_body_ice_wolf.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 961 â†’ 962 in the SAME edit as the mint.)*
> - **960** = **Armor store: locked-preview ladder (greyed + Lv N, next-5-levels window)** â€” owner
>   ruling 2026-08-10. RCA'd start: armor.json has 24 rows (per-class rarity ladders), store shows 3 â€”
>   a visibility/filter defect, not missing content; level-gate derivation to be found/proposed as
>   data. File `WORK_ORDER_960_armor_store_locked_preview_window.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 960 â†’ 961 in the SAME edit as the 960 mint.)*
> - **959** = **Weapon flame aura only while unsheathed** â€” owner ruling F8 2297 ("only show the
>   flames on the sword when unsheathed"). One gate at the GearAura seam, all element auras; the
>   "unsheathed" state mapping named in the RESULT for her felt-confirm.
>   File `WORK_ORDER_959_weapon_flame_aura_only_unsheathed.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 959 â†’ 960 in the SAME edit as the 959 mint.)*
> - **958** = **Dungeon camera stops fighting the player in small rooms** â€” owner F8 2289 (auto-rotate
>   + tight-space framing in dg_ember_deep). Capture-first tuning, all values in DungeonCameraProfile
>   (the one authority), owner felt-pass closes. File `WORK_ORDER_958_dungeon_camera_tight_rooms_stability.md`.
>   **READY TO IMPLEMENT.**
>
> *(banner bumped 958 â†’ 959 in the SAME edit as the 958 mint.)*
> - **957** = **EXIT beacon on EVERY stairwell in multi-floor dungeons** â€” owner F8 2287 + screenshot
>   (EXIT arrow on a mid-dungeon descent). Hypothesis: beacon placement predates WO-930 multi-floor
>   (stairs used to BE the exit); fix = one designated exit per layout + per-layout regression.
>   Companions WO-1007/1008 own presentation. File `WORK_ORDER_957_exit_beacon_on_every_stairwell.md`.
>   **READY TO IMPLEMENT.**
>
> *(banner bumped 957 â†’ 958 in the SAME edit as the 957 mint.)*
> - **956** = **An enemy reads GREEN â€” hostility never sits on the red/green axis** â€” owner F8 2269 +
>   clarification (enemy showing green; owner red/green colourblind). RCA-first (heal-cast glow on the
>   hollow-acolyte healer is the lead candidate â€” the never-ticketed 08-06 "Cast_Heal green glow"
>   item); fix = faction-driven effect presentation, hostile palette/shape.
>   File `WORK_ORDER_956_enemy_reads_green_hostility_cue.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 956 â†’ 957 in the SAME edit as the 956 mint.)*
> - **955** = **VFXManager.Acquire NRE â€” pool free list hands back a destroyed host** â€” captured
>   exception (owner session 2026-08-10, arena-death churn via HeroHpStateAura): Acquire:876 threw on
>   a destroyed pooled host's transform. Fix = dead-slot evict+rebuild with a Warn, find the teardown
>   destroyer; ONESHOT saturation stays separate. File `WORK_ORDER_955_vfx_pool_destroyed_host_nre.md`.
>   **READY TO IMPLEMENT.**
>
> *(banner bumped 955 â†’ 956 in the SAME edit as the 955 mint.)*
> - **954** = **Hollow family still wears KayKit skeletons + enemy idâ†’model mapping goes data-driven** â€”
>   owner report 2026-08-10, RCA'd live: authored (no fallback), the whole hollow town-wave family maps
>   to plain KayKit `Skeleton_*` across FOUR divergent code tables; enemies.json has no model column.
>   Mechanics READY (data column + one resolver, behavior-preserving seed); the hollow re-skin model
>   pick = owner creative pin. File `WORK_ORDER_954_hollow_family_models_data_driven.md`.
>
> *(banner bumped 954 â†’ 955 in the SAME edit as the 954 mint.)*
> - **953** = **Harvest "+N" pops via the damage-number spawner + gated-faucet honesty** â€” owner
>   rulings 2026-08-10 (reuse the damage-points spawner; her zero-iron RCA'd live to the
>   phantom-income gate: 'forge' never built on her blank save, correct-but-silent). Picker shows a
>   NEEDS cue; pet-node demo rates (5/5) promoted to owner-tunable data, values unchanged.
>   File `WORK_ORDER_953_harvest_drip_feedback_and_gated_faucet_honesty.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 953 â†’ 954 in the SAME edit as the 953 mint.)*
> - **952** = **EndState wave-clear panel compresses body below content size** â€” the panel's own
>   FlowTrace net fired twice in one session (need=276px well=249px scale=0.9, screen-height clamp);
>   fix = reflow-not-shrink + a capture case at the failing resolution asserting the Fail line's
>   ABSENCE. File `WORK_ORDER_952_endstate_panel_body_compression.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 952 â†’ 953 in the SAME edit as the 952 mint.)*
> - **951** = **Echo Hollow repurposed: tap it â†’ the Echoes popup opens** â€” owner ruling 2026-08-10
>   (F8 2266 + confirmation "Simple and easy"). Not removed, not a skins store; keeper Talk routes to
>   the existing roster panel. Capacity/awakening-stage/skins-counter recorded as UNPINNED extensions.
>   File `WORK_ORDER_951_echo_hollow_opens_echo_roster.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 951 â†’ 952 in the SAME edit as the 951 mint.)*
> - **950** = **Drillmaster + teach toast appear on a blank-town save with NO barracks** â€” owner
>   felt-report 2026-08-10, RCA'd live: the injector's OnSceneLoaded path checks unlock but never
>   `MayBakedTwinSurface`, while the sweep shows surfaced=0/everBuilt empty on the same save. Fix at
>   the Inject seam + ownership reconcile + once-teach burn guard.
>   File `WORK_ORDER_950_drillmaster_without_barracks_blank_town.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 950 â†’ 951 in the SAME edit as the 950 mint.)*
> - **949** = **Death UX: respawn IN TOWN + starter potions + teach the cost of dying** â€” owner F8s
>   2026-08-10 10:20/10:22 verbatim ("On Death I should respawn in town not where I died", "start the
>   user with some potions, and explain to them consequences of dying with resources"). Discovery-first
>   on what death costs today; potion-button-at-zero + no-apothecary caveats carried in the WO.
>   File `WORK_ORDER_949_death_ux_respawn_town_potions_teaching.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 949 â†’ 950 in the SAME edit as the 949 mint.)*
> - **948** = **Walls: build at L1 only, upgrade to climb (CoC model)** â€” owner ruling 2026-08-10 on
>   first seeing Castle Structures ("enforce them to start with a level one wall... like CoC does
>   it"). Verified: `BuildPaletteUI.cs:1105-1106` offers wall_wood AND wall_stone as placeables; the
>   walls.json ladder already exists and heart-mitigation already pays. Scope = palette enforcement +
>   the woodâ†’stone rung only; deeper tiers/gates stay in WO-904 behind raid-steal.
>   File `WORK_ORDER_948_walls_build_l1_only_upgrade_to_climb.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 948 â†’ 949 in the SAME edit as the 948 mint.)*
> - **947** = **Cost-basket separation: regular = wood+iron, magical/ethereal = crystal-based, never
>   all three** â€” owner economy ruling 2026-08-10 (verbatim in the WO). Audit found 6 of 29 entries
>   violating; SPEC pending 4 owner classification pins (healer/caravan/jeweler/arcane-tower + the
>   crystals-pair-with-iron-or-wood call), then mechanical data edit + invariant regression.
>   File `WORK_ORDER_947_cost_basket_separation_regular_vs_arcane.md`. **SPEC.**
>
> *(banner bumped 947 â†’ 948 in the SAME edit as the 947 mint.)*
> - **946** = **POI node auras + Tree of Life: retire the strong yellow, go subtle** â€” owner F8 seq 2252
>   verbatim look ruling (*"remove the yelllow from the nodes and the tree of Life (its a vfx) but we
>   want something subtle, not so strong"*). Needs EYES to verify (screencap loop), owner felt-close.
>   File `WORK_ORDER_946_poi_tree_aura_yellow_subtle.md`. **READY TO IMPLEMENT.**
> - **945** = **Tutorial: the SECOND tower runs the full 90s curve while the teaching wave lands** â€”
>   owner felt-report (Seeker + exe, multiple repros), RCA data-proven same morning: the 5s first-build
>   grace is per-structure-id, the tutorial asks for two towers of the SAME id, tower #2 ran 90s
>   (proving lines Player.log 51090 grace on #1 / 55450 no grace on #2 / 55676 cost-freebie DID fire).
>   Fix = while !Onboarded every build gets the grace (the 08-06 ruling's own stated intent), pallets
>   carve-out intact. File `WORK_ORDER_945_tutorial_second_tower_timer_grace.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 945 â†’ 947 in the SAME edit as the 945+946 mints â€” the rule that broke five times on 08-02.)*

> ## (superseded header) RECONCILED 2026-08-09 (CLI / THE RULES): main line next free = ~~945~~ â€” see the 2026-08-10 header above. **782â€“859 + 900â€“944 CONSUMED.**
> - **944** = **Placing: the item's title pins STATIC at the top of the screen** â€” owner F8 seq 2250
>   flagged live in the fresh 22:11 build (*"can we make the title of the item pin staticl maybe at the
>   top of the screen"*); retires the last follow behaviour (the pill), UI_PLAYBOOK Â§8's own preferred
>   answer. File `WORK_ORDER_944_placing_title_pinned_static_top.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 944 â†’ 945 in the SAME edit as the 944 mint.)*
> - **943** = **The docs get a HOME: wiki-style linked navigation over the doc lake** â€” owner
>   directive 2026-08-09 (*"almost like a Wiki... start at from home... the next CLI seat doesn't
>   have to dig"*): ONE GENERATED static home page (HOME.html or a BOARD nav rail, built beside
>   `board_build.py`) linking rules / architecture hub / north star + newest ground truth / board /
>   master catalog / VFX + sounds organization views. LINK never duplicate; generator fails on dead
>   links; newest-by-date resolution; composes with WO-937/938/940/1011. Overnight lane, carries the
>   aged-vs-new due-diligence rider. File `WORK_ORDER_943_docs_wiki_home_linked_canon.md`.
>   **READY TO IMPLEMENT.**
>
> *(banner bumped 943 â†’ 944 in the SAME edit as the 943 mint â€” the rule that broke five times on 08-02.)*
> - **942** = **UI capture harness: two capture-case gaps left by the WO-1010 pass** â€” the
>   `padon` case is byte-identical to `edgeclamp` (the identical-file-size tell) because the D12
>   no-toggle ruling dissolved what it photographed, and the D17 sprite-path dim-on-invalid has no
>   assertion. File `WORK_ORDER_942_ui_capture_case_gaps_wo1010.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 942 â†’ 943 in the SAME edit as the 942 mint â€” the rule that broke five times on 08-02.)*
> - **941** = **RumorBoard + RealmMap: controls overlap text (16 UI_GEOMETRY assertions)** â€” the
>   geometry oracle pins CloseButton/CTA/reward-label overlaps at both portrait sizes on RumorBoard and
>   a map-node disc over text on RealmMap at both landscape sizes; PRE-EXISTING (identical in the 20:41
>   and 22:04 runs, attributed before ticketing). File
>   `WORK_ORDER_941_rumorboard_realmmap_geometry_overlaps.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 941 â†’ 942 in the SAME edit as the 941 mint.)*
> - **940** = **Board: DATE-tag every ticket + "opened within" filter (age is DERIVED, never typed)**
>   â€” owner ruling 2026-08-09: *"i want aged tagged to every ticket"*, *"date tagged"*, *"so we can
>   filter opened within and see"*. Backs the one-week validity threshold (`SUNDAY_HOUSEKEEPING.md` Â§4):
>   you cannot apply "older than a week -> verify" if the board does not show age. **Carries a real
>   defect:** `tools/board_build.py:116` labels its Age column from `os.path.getmtime` â€” that is LAST
>   MODIFIED, not OPENED, so any edit resets a ticket's apparent age and "opened within" is unanswerable
>   today. File `WORK_ORDER_940_board_date_tagging_and_opened_within_filter.md`.
>   **READY TO IMPLEMENT.**
>
> *(banner bumped 940 â†’ 941 in the SAME edit as the 940 mint â€” the rule that broke five times on 08-02.)*
> - **939** = **Backend auth rail is compiled OFF in every shipped build (+ guest-id salt in the binary)**
>   â€” `BACKEND_AUTH_ENFORCED` is defined on NO platform row in `ProjectSettings.asset`, so
>   `BackendAuthConfig.cs:58`'s enforced branch is compiled out and `GameStateService` sends no auth
>   headers; real cloud saves therefore ride the GUEST rail, whose id is
>   `Sha256(deviceId + GuestIdSalt)` with the salt literal at `GameStateService.cs:1572`. Anyone who
>   derives that id can read AND overwrite that player's save. Server side is sound â€” the client just
>   never uses it. **Owner ruling 2026-08-09: OVERNIGHT, not a hotfix â€” no live player base, so
>   exposure is theoretical.** File `WORK_ORDER_939_backend_auth_rail_unreachable.md`.
>   **READY TO IMPLEMENT.**
>
> *(banner bumped 939 â†’ 940 in the SAME edit as the 939 mint â€” the rule that broke five times on 08-02.)*
> - **938** = **`RULES.md` â€” the one page the owner can point at** â€” the binding rules are currently
>   spread across CLAUDE.md, PREFLIGHT_GATE.md, SESSION_CANON_LOADER.md, docs/HANDOVER.md,
>   docs/TICKET_PIPELINE.md, docs/ARCHITECTURE_PRINCIPLES.md, docs/INSTRUMENTATION_STANDARD.md and
>   docs/BOARD.md, so "read the rules" has no single target. ONE numbered, non-negotiable list that
>   POINTS AT the deep docs rather than duplicating them (a copy is a future contradiction). File
>   `WORK_ORDER_938_rules_single_page.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 938 â†’ 939 in the SAME edit as the 938 mint â€” the rule that broke five times on 08-02.)*
> - **937** = **Board status-line hygiene + parser scope** â€” `--check` reports 91 Unlabeled, but it is
>   TWO problems: **20 are not work orders at all** (audits/briefs/handoffs/README living in
>   `WorkOrders/`, filename not `WORK_ORDER_<n>`) â†’ parser SCOPE fix, not a status fix; and **71 are
>   real WOs with a missing/empty `**Status:**` line** â†’ the actual defects. Fix scope first so the
>   count means something, then sweep the 71. File
>   `WORK_ORDER_937_board_status_hygiene_and_parser_scope.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 937 â†’ 938 in the SAME edit as the 937 mint â€” the rule that broke five times on 08-02.)*
> - **936** = **Catalog gating + progression truth pass** â€” `LockedIds` is READ-ONLY with no unlock
>   path, so "unlock-gated" ids (jeweler) are permanently hidden, not temporarily; the three
>   stockpiles declare `maxLevel:3` with NO tier rows, capping the wood/iron/food economy; and the
>   live `collector_lumbermill` routes its upgrades through the RETIRED `lumbermill` row. File
>   `WORK_ORDER_936_catalog_gating_and_progression_truth.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 936 â†’ 937 in the SAME edit as the 936 mint â€” the rule that broke five times on 08-02.)*
> - **935** = **Paid animation + VFX pack connection program** â€” inventory $1000s of Asset Store
>   packs (Hovl/Mirza/Spells/UT Particle/Supercyan/KayKit/Action), map what ships vs sits idle,
>   wire troop/hero combat anim+VFX end-to-end WITHOUT rebuying or forking catalogs; protect
>   self-containment (`Resources/VFX/_Shared`). File
>   `WORK_ORDER_935_paid_anim_vfx_connection_program.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 935 â†’ 936 in the SAME edit as the 935 mint â€” the rule that broke five times on 08-02.)*
> - **934** = **Army loadout bank (3 named presets + persist + muster polish)** â€” save/load/quick-fill
>   Raid Push / Wall Hold / Siege Prep; save schema v38; Armies button on Barracks. File
>   `WORK_ORDER_934_army_loadout_bank.md`. **IMPLEMENTED.**
>
> *(banner bumped 934 â†’ 935 in the SAME edit as the 934 mint â€” the rule that broke five times on 08-02.)*
> - **933** = **Siege Catapult troop (CoC scarcity + WC Demolisher)** â€” 8th roster unit at Barracks
>   T4 beside Outrider; `maxOwned:1` (wounded still blocks); role `siege` structure-prefer hunt;
>   range ~26 / slow / fragile / heavy cost; structure vs unit damage mult; machine visual
>   `Structures/Catapult`. File `WORK_ORDER_933_siege_catapult_troop.md`. **IMPLEMENTED.**
>
> *(banner bumped 933 â†’ 934 in the SAME edit as the 933 mint â€” the rule that broke five times on 08-02.)*
> - **932** = **Raids full functional audit + step-by-step fix ladder** â€” Path A teleport/deploy is
>   LOCKED V1 (raidwalk OFF); spine exists (HUDâ†’selectâ†’deployâ†’RaidBaseâ†’scoreâ†’victoryâ†’return) with
>   headless gates; gaps = prereq teach, full-army feel, Auto Recommend stub, scene honesty, win/
>   soft-lock integrity, eliteCount dead key, IronBastion orphan. Phases 0â€“6 to fully functional
>   Regular clear. File `WORK_ORDER_932_raids_full_functional_audit_and_fix.md`. **READY.**
>
> *(banner bumped 932 â†’ 933 in the SAME edit as the 932 mint â€” the rule that broke five times on 08-02.)*
> - **931** = **Close the StubWalletProvider free-grant hole** â€” `StubWalletProvider.cs` has NO
>   `#if UNITY_EDITOR`/`DEVELOPMENT_BUILD` guard, so it compiles into every shipped build and
>   `WalletService` auto-selects it on release desktop/WebGL (and Android without `SOLANA_SDK`). Chain:
>   Buy â†’ fake Connect â†’ mock 2000 SKR balance â†’ fabricated base58 sig â†’ `ApplyPackContents` grants the
>   pack for ZERO payment + fires `purchase_completed` with the fake txSig. `FeatureFlags.RealmStorePurchase`
>   (default OFF) is the ONLY gate, so it is **not urgent today, hard blocker the moment monetization
>   flips** â€” now precondition **3 of 3** in that flag's DO-NOT-TURN-ON block. Candidate fixes
>   (a) build-guard / (b) runtime refusal at the WalletService seam / (c) both are left UNPICKED â€”
>   architecture call. `WalletService.PayFlat` is in scope: same stub path, gated by NOTHING, dead only
>   because both callers are scene-absent (GUID sweep).
>   File `WORK_ORDER_931_stub_wallet_free_grant_hole.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 931 -> 932 in the SAME edit as the 931 mint â€” the rule that broke five times on 08-02.)*
> - **930** = **The stairwell is ONE room: midpoint to midpoint** â€” the owner's design, and the
>   replacement for the `_Up`/`_Down` pair model. A stairwell is a single room owning its subrooms,
>   connecting the MIDPOINT of the upper floor to the MIDPOINT of the lower; run is the footprint,
>   slope is DERIVED (25-31 deg, never near the 45 deg carve cliff); the upper level is a GALLERY so
>   the stair rises through OPEN AIR instead of squeezing under a slab with 0.36 m to spare.
>   **The composer needs NO change** â€” a socket already carries its own Y and `SolveMate` resolves
>   height for free. DELETES `StairUp`/`StairDown`, the vertical mate branch, `IsVertical`,
>   `SEALED_VERTICAL`, the floor holes and the ceiling shafts.
>   File `WORK_ORDER_930_stairwell_is_one_room_midpoint_to_midpoint.md`. **READY TO IMPLEMENT.**
> - **929** = **VFX aura reparented during activate/deactivate** â€” a real thrown Unity error, 3x in one
>   session, on POOLED enemies (`Cannot set the parent ... while activating or deactivating`).
>   File `WORK_ORDER_929_vfx_aura_reparent_during_activation.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 929 -> 931 in the SAME edit as the 930 mint. âš  929 was minted earlier today WITHOUT
> bumping this banner â€” the CLI's own violation of the rule it had been enforcing all day, caught and
> corrected here. It is the same slip that caused five collisions on 08-02: the mint and the bump must
> be ONE edit.)*
> - **928** = **Archer Tower: orientation, materials, footprint parity, and the Move path** â€” one
>   owner-ruled cluster from the 2026-08-08 felt-test (F8 2181-2192). All four root causes CAPTURED:
>   `VisualFactory.cs:140` wipes the L3 prefab's baked 270deg to identity, so the height-fit then
>   measures the wrong axis (scale 8.34x vs L1's 4.74x, bounds 4.91 x 4.80 x 8.34 against a 3x3 m
>   blocker and a declared footprint of 1.75); L3 wears the raw Tripo `wooden_watchtower_3d_model_basecolor`
>   instead of the built material; and PLACE on a Move ends in `CancelArmed` (`armed cleared`) with
>   `Two-step RE-DROP` never running. Two theories are already KILLED in the WO - do not re-run them.
>   File `WORK_ORDER_928_archer_tower_orientation_materials_footprint_move.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 928 -> 929 in the SAME edit as the 928 mint - the rule that broke five times on 08-02.)*
> - **927** = **PathPartial seam revalidation** â€” the design doc's Â§5.5.2 erosion justification is DEAD
>   (landing measured 1.30 m, path outcome unchanged). Capture M1â€“M7 on ONE failing seam (attachment-point
>   world coords, delta vector, connector scale/bounds/span, connector-disabled check, and a
>   `NavMesh.CalculateTriangulation` dump), then re-justify or retire the connector.
>   File `WORK_ORDER_927_pathpartial_seam_revalidation.md`. **READY TO IMPLEMENT** (owner-authored).
>
> *(banner bumped 927 â†’ 928 in the SAME edit as the 927 mint â€” the rule that broke five times on 08-02.)*
> - **926** = **Combat anim: legs/hips, foot slide, recovery, shield clip** (Imagine review P2).
>   File `WORK_ORDER_926_combat_anim_root_motion_recovery.md`. **SPEC / owner priority.**
> - **925** = **Kill/condition permanent foot fire VFX** under hero (Imagine â€” always-on sparks).
>   Instrument HeroHpStateAura first. File `WORK_ORDER_925_kill_persistent_foot_fire_vfx.md`. **READY.**
> - **924** = **Kill neon-green exit/climb debug volumes** â€” DungeonExitInteractable Unlit beams +
>   EXIT labels; stop pairing Climb/Descend with debug pillars. File
>   `WORK_ORDER_924_dungeon_green_debug_exit_climb_volumes.md`. **READY.** Map:
>   `REVIEW_MAP_IMAGINE_DUNGEON_2026-08-07.md`.
>
> *(banner bumped 924 â†’ 927 in the SAME edit as the 924â€“926 mint â€” the rule that broke five times on 08-02.)*
> - **923** = **Walkable multi-level stairs** â€” prefab kit (visual steps + invisible Cube ramp on
>   nose line, NOT Plane); rise=FloorSeparationY 6m; PathComplete on all multi-level bakes; retire
>   Descend ports when stair present. Source: `HANDOFF_GROK_DUNGEON_MULTILEVEL_NAV.md` + owner video.
>   File `WORK_ORDER_923_walkable_stair_prefab_kit.md`. **READY.**
>
> *(banner bumped 923 â†’ 924 in the SAME edit as the 923 mint â€” the rule that broke five times on 08-02.)*
> - **922** = **RoomForge: all rooms much wider** â€” master `Cell` 6â†’**10** m (optional 12);
>   1Ã—1 rooms 6Ã—6â†’10Ã—10; rebuild prefabs + recompose graphs + rebake. Combine bake with WO-919.
>   File `WORK_ORDER_922_roomforge_wider_rooms.md`. **READY.**
>
> *(banner bumped 922 â†’ 923 in the SAME edit as the 922 mint â€” the rule that broke five times on 08-02.)*
> - **921** = **Dungeon fire cosmetic vs hazard** â€” torch_lit + intensity-2 lights make rooms look
>   â€œencased in fireâ€ but do zero damage; real traps (spike/grate) damage but are invisible; no fire
>   kind. Dial cosmetic torches, telegraph traps, optional fire trap kind off spawn.
>   File `WORK_ORDER_921_dungeon_fire_cosmetic_vs_hazard.md`. **READY.**
>
> *(banner bumped 921 â†’ 922 in the SAME edit as the 921 mint â€” the rule that broke five times on 08-02.)*
> - **920** = **Dungeon camera: stationary exploration** â€” default OFF free-look FPV; locked OTS;
>   kill AvoidObstacles bounce; calm combat framing (prefer no FPVâ†”OTS thrash). Owner: camera
>   bouncing + wants stationary dungeon view. File `WORK_ORDER_920_dungeon_stationary_camera.md`.
>   **READY** (prefer after 919 enclose). Updates `DungeonFpvRegression` deliberately.
> - **919** = **RoomForge enclose: taller walls + ceilings + kill blue sky.** Composed rooms are
>   2.8 m open-top boxes (`DefaultDungeonRoomsBuilder`); baker never fog/sky-kills. Owner shots
>   2026-08-07 show half-frame blue sky. Raise walls â‰¥4 m, ceiling pass, Healerâ€™s ambient recipe,
>   re-bake composed layouts. File `WORK_ORDER_919_roomforge_enclose_taller_walls_ceilings.md`.
>   **READY.** WO-1000 remains the separate KayKit **outpost** builder.
>
> *(banner bumped 919 â†’ 921 in the SAME edit as the 919â€“920 mint â€” the rule that broke five times on 08-02.)*
> - **918** = **Board hygiene: close shipped WOs + RESULT files** for the audit five-findings
>   (`f329c8d5`), WO-899 PARTIAL, WO-1001, without closing READY VFX (890/892/1002). Notion mirror.
>   File `WORK_ORDER_918_board_hygiene_close_shipped_wos.md`. **READY.**
> - **917** = **WO-899 Â§4 residual** â€” dodge icon + empty skill-slot â€œ+â€ placeholder. Stick/compass/
>   attack landed in `a35163e1`; Â§4 deliberately not smuggled (no style-matched dodge art yet).
>   File `WORK_ORDER_917_hud_dodge_icon_empty_skill_slot.md`. **READY** (owner art pick if no icon).
> - **916** = **Marketing site vercel --prod** â€” repo tagline is canon (â€œEchoes of a Forgotten
>   Civilizationâ€); production may still serve retired â€œlast lightâ€ until verified deploy.
>   File `WORK_ORDER_916_site_canon_tagline_vercel_prod.md`. **READY.**
> - **915** = **RealmStorePurchase public-release re-gate + payment path.** Q9 turned Buy ON for the
>   sole tester; mainnet hard-block + empty SkrMintDevnet remain ship blockers. Owner rules A/B.
>   File `WORK_ORDER_915_realm_store_public_release_regate.md`. **READY FOR OWNER RULING.**
> - **914** = **Status mount: compass strip vs waveBlock layout.** WO-899 widened the strip; no UI
>   capture; calm posture co-occupies both widgets â€” measure rects first, fix only if collision.
>   File `WORK_ORDER_914_status_mount_compass_waveblock_layout.md`. **READY.**
> - **913** = **Arcane Element==visual regression.** `BoltVisualElement` is Aether in source but
>   `TowerProjectileMapRegression` never asserts Element/BoltVisualElement â€” Flame can ship green again.
>   File `WORK_ORDER_913_arcane_element_equals_visual_regression.md`. **READY.**
>
> *(banner bumped 912 â†’ 919 in the SAME edit as the 913â€“918 mint â€” the rule that broke five times on 08-02.)*
> - **912** = **Ad revenue for the FREE PATH** (provider, rolling window, remote config, ad-boost packs).
>   File `WORK_ORDER_912_ad_revenue_free_path.md`. **READY FOR OWNER RULING.** âš  Was on disk while the
>   banner still read next-free 912 â€” reconciled 2026-08-07; do not re-mint 912.
> - **911** = **Timer speed-ups actually available** â€” Instant crystals + Ad skip on ALL channels
>   (Builder/Train/Research); root cause: Instant only resolved Builder + dead Ad hid all CTAs.
>   Crystal packs stay existing currency (no new type). File
>   `WORK_ORDER_911_timer_speedup_crystals_all_channels.md`. **READY.**
>   âš  Also on disk: `WORK_ORDER_911_unified_queue_screen.md` (second 911 title â€” historical collision;
>   do not mint another 911).
> - **910** = **Ranger + Mage talent trees: 31 player-reachable nodes have no consumer.** Surfaced the
>   moment `TalentStrategyRegression`'s `HiddenTrees` was emptied â€” it had hardcoded `{"ranger","mage"}`,
>   so guard G3 had NEVER audited 40 player-reachable nodes while reporting green. **Ranger collapses to
>   ONE usable talent out of 20, Mage to five; both lose their entire tier-4 capstone row.** Knight (32)
>   and shared (9) are fully green, so this is isolated to the two classes unlocked 2026-08-05.
>   âš  **Hiding was CONSIDERED AND REJECTED** â€” `HeroTalentNodeDef.Hidden` had ZERO runtime readers
>   (its own comment lied), so `"hidden": true` would have turned the gate green while leaving every node
>   clickable; and hiding strands three whole tiers + orphans three nodes. `Hidden` is now genuinely
>   wired, so an owner ruling to hide will actually work. The 31 are tracked as a dated, ratcheted
>   baseline: new debt fails, and a baseline id that stops being dead ALSO fails.
>   File `WORK_ORDER_910_ranger_mage_talent_consumers.md`. **READY FOR OWNER RULING.**
>
> *(banner bumped 910 â†’ 911 in the SAME edit as the 910 mint â€” the rule that broke five times on 08-02.)*
> - **909** = **Activate Mage + Ranger in character selection (re-enable + verify).** Owner: create a WO
>   for CLI to make Mage/Ranger selectable. Gate `FeatureFlags.KnightOnly` already default-OFF
>   (`9a0ff548`); WO-861 landed kits/loadout/portraits/copy/rename â€” so this is a **re-enable + verify +
>   body-mesh finish**, not a build. Real open risk = Mage/Ranger body mesh (parked `.tripo-extracted`
>   FBX â†’ Blink base vs KayKit body). Owner steer: *"Mage should obviously live heavily in that realm"* â†’
>   Mage is the magic/VFX showcase. File `WORK_ORDER_909_activate_mage_ranger_character_select.md`. **READY.**
>
> *(banner bumped 909 â†’ 910 in the SAME edit as the 909 mint â€” the rule that broke five times on 08-02.)*
> - **908** = **Side menu: duplicate gear icon + wrong icon formatting.** Owner felt-test on the Seeker
>   (2670x1200): the left-side menu expands correctly, but TWO gear glyphs render in two different
>   styles â€” a gold/tan boxed gear seated on the **Music** row and overhanging the panel's left border,
>   and a grey outline gear drawn on top of the **"S" in "Settings"**. One icon, one style, seated in
>   its row. âš  Suspect the fraction-band / `ClampMinTouch` centre-grow class that broke WO-852/868/865
>   and both founding screens on 08-05 â€” check for a fraction-positioned band FIRST. Screenshot attached
>   in-repo at `docs/qa/screens/2026-08-05/gear-menu-double-icon.png`. Owner is routing this to the UI
>   team. File `WORK_ORDER_908_gear_menu_duplicate_icon.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 908 â†’ 909 in the SAME edit as the 908 mint â€” the rule that broke five times on 08-02.)*
> - **907** = **Elemental affinity â€” towers, enemies, and a match bonus that is never a lock.** Owner:
>   *"each tower could land a different affinity"*, *"they could both apply"* (visual AND damage), and â€”
>   asked whether enemies carry an element â€” *"they don't yet but should."* âš  **Governing rule is the
>   EXISTING Echo grammar (CLAUDE.md Â§7 / WO-830): a MATCH BONUS, NEVER A LOCK.** No tower may become
>   useless against an enemy type. âš  Only `tower_arcane_spire` authors an element today (Aether); the
>   other four author NONE, and enemies author none at all â€” **tower affinity without enemy affinity is
>   half a system, both land together or neither ships.** `IDamageable.cs:61` already documents the
>   element param as *"used for resist / bonus math"* â€” Â§4.1 is to find out whether that math exists,
>   is unwired, or was never written. âš  **Gates part of WO-870: element FIRST, visual SECOND** â€” picking
>   VFX before elements reproduces the exact Arcane Spire defect (Aether damage, Fire visuals).
>   âš  Balance blast radius: this re-opens WO-855's tower cost/DPS band hours after it landed.
>   File `WORK_ORDER_907_elemental_affinity_system.md`. **SPEC.**
> - **906** = **Catapult becomes a DEPLOYED offensive siege unit** (owner: *"deploy offensively"*). Moves
>   it between SYSTEMS â€” StructureFactory/DefenseTower â†’ TroopController/TroopDeployer â€” so it is NOT a
>   tag change. Currently authored as its opposite: `behaviorId: DefenseTower`, range 28, a placed
>   structure, and unreachable anyway (the build menu lists only the cheapest FOUR of five tower rows).
>   Named failure mode: half-of-each. WO-853's damageable walls/gates/towers is what makes a siege
>   weapon meaningful at all. File `WORK_ORDER_906_catapult_deployable_siege_unit.md`. **SPEC.**
>
> *(banner bumped 906 â†’ 908 in the SAME edit as the 907 mint â€” and correcting a 906 mint that went to
> disk WITHOUT a bump earlier tonight, which is the exact rule this banner exists to enforce.)*
> - **905** = **"Manage" â€” one screen for every upgrade, sorted by what you can afford.** Owner: a Manage
>   section under Bag showing all three rails with drill-in, because *"not sure what they can afford"*.
>   âš  **The content tabs and the queue channels CROSS**: Defensive structures AND Building upgrades both
>   run on the **Builder** channel and share one rail; troop upgrades are Research. **V1 ships THREE tabs**
>   (defensive / buildings / troops); weapons + armor are FUTURE and have **no queue at all** â€”
>   `GearProgression.Improve` is instant ("instant V1 â€” no job/channel"), the only sink costing resources
>   but no time. **Deliverable, not a side effect: the always-on queue panel comes OFF the play HUD once
>   Manage is reachable â€” Manage first, removal second.** Rationale worth keeping: discoverability by
>   walking is not discoverability. Drill-in reuses the EXISTING `BuildingUpgradePanelMvvm` (83 KB,
>   already registered); do not build a second upgrade panel.
>   File `WORK_ORDER_905_manage_screen_upgrade_browser.md`. **SPEC â€” depends on WO-864's rail component.**
> - **904** = **Fortification: upgradeable walls AND gates.** Walls already upgrade (`wall_wood`/`wall_stone`
>   author `maxLevel:3` + a 2-rung `upgradeCost`) and WO-853 made them damageable â€” but **`gate_stone`
>   authors NO `maxLevel` and NO `upgradeCost`**, so the verb answers `1 >= 1` and toasts "Max tier
>   reached" on a fresh gate. A perimeter is only as strong as its weakest authored point; a raider walks
>   the door while the reinforced walls stand untouched. **Blocked on raid-steal by design** â€” fortification
>   before there is anything to lose is a cost with no reason to pay it.
>   File `WORK_ORDER_904_fortification_walls_and_gates.md`. **SPEC.**
>
> *(banner bumped 904 â†’ 906 in the SAME edit as the 904 + 905 mint)*
>
> ### â›” THE MAIN LINE HAS COLLIDED WITH THE UI-SEAT BLOCK â€” READ BEFORE MINTING
> The main line consumed 859 and the next number, 860, is **inside the UI seat's reserved 860â€“899**
> (860/861/862/863 already consumed there). **The two blocks have MET.** The main line therefore
> **jumps to 900+**. Any main-line mint below 900 from here is a guaranteed collision.
>
> âš  **THIS PARAGRAPH WENT STALE AND IS CORRECTED 2026-08-07.** It read "the UI seat keeps 860â€“899
> (next free 864)". That is no longer true and was the SAME self-contradiction that seeded the
> earlier collision â€” a head that says one thing and a body row that says another. **Owner ruling:
> the UI seat moved to 1000â€“1099**; 860â€“899 is CLOSED (full at 899), and 1000 / 1001 / 1004 / 1005
> are already minted in the new block. The main line's next free is the HEAD BANNER (923), which is
> the sole authority â€” never this paragraph, never a number copied anywhere else.
>
> - **903** = **Storage pallet fill stacks (SMALL)** â€” lumberyard/foundry/silo show logs/ingots/sacks
>   as bank fill rises (~5% steps); reuse CollectorStackView/prop catalog. No economy rewrite.
>   File `WORK_ORDER_903_storage_pallet_fill_stacks.md`. **READY.**
> - **902** = **Archer Tower medieval castle visuals (Option A)** â€” retire Tribal T1â€“T3 for
>   `tower_ground_archer`; L1 `Tower_Castle_Round` â†’ L2 `Tower_Castle_Square` â†’ L3 `Tower_Medieval_Big`.
>   Catalog dual-copy + mirror Square into Resources if missing. No combat rewrite.
>   File `WORK_ORDER_902_archer_tower_medieval_castle_visuals.md`. **READY.**
> - **901** = **THE COLLECTOR LOOP (umbrella)** â€” owner directive "consolidate those into one idea and
>   implement". One idea: *your town keeps producing while you are away, into containers that visibly
>   fill to a cap and then stop, and storage raises what the town can hold.* Folds 857/858/859/900 into
>   one sequence (phases 0/Aâ€“G) and rules on their overlap. **âš  Ruling: Grok's 858 icon half and CLI's
>   900 tell are the SAME FEATURE â€” `CollectorStackView` (437 lines) already implements it and `Attach`
>   has ZERO CALLERS. WIRE IT, do not build it.** Phase F (wallet clamp) deliberately WITHHELD from the
>   autonomous pass â€” it clamps `EconomyService.Grant`, which every income path flows through.
>   File `WORK_ORDER_901_the_collector_loop.md`. **IN PROGRESS.**
> - **900** = **Collector "I am full" tell** â€” appendix of 901, phases D/E. `CollectorStackView.Attach`
>   has zero callers (recorded WO-783:186 + `UiObsidianConformanceRegression.cs:168`, never fixed): a
>   WIRING fix, not a UI build. HUD chip via a Core status gate mirroring `ObsidianQueueGate` â€” NOT
>   `IVillageHud` (that is imperative push; this is a polled snapshot). No new reflection.
>   File `WORK_ORDER_900_collector_full_tell.md`. **READY.**
> - **859** = **Per-collector capacity in HOURS + offline accrual** â€” appendix of 901, phases 0/A/B/C.
>   Collectors have NO offline accrual (zero consumers of `LastHarvestClaimMs`), and the capacity curve
>   **runs backwards**: capacity grows x3 L1â†’L5 while throughput grows x5.6, so upgrading a collector
>   SHORTENS unattended runtime (6-echo L5 farm fills in **5.7 min**). âš  **Carries a P0 `35485f31` did
>   NOT close: `ResourceCollectorBootstrap.EnsureFallbackCollector` creates live collectors
>   UNCONDITIONALLY without consulting `everBuiltStructureIds`** â€” a blank town earns again, and full
>   town income accrues while the player is in a DUNGEON. Prove headless before editing (Â§12).
>   File `WORK_ORDER_859_collector_capacity_hours_and_offline_accrual.md`. **READY.**
>   âš  **Renumbered from a collided 858 mint** â€” Grok's 858 was first-on-disk-and-referenced and wins.
>
> *(banner bumped 859 â†’ 902 in the SAME edit as the 859 + 900 + 901 mint)*
> - **858** = **Collector resource icons + high-value invasion targets** â€” billboard wood/iron/food/crystal
>   icons when pending (tap=Collect); catalog siegeValue/highValueTarget for premium collectors.
>   File `WORK_ORDER_858_collector_resource_icons_and_siege_value.md`, READY.
> - **857** = **CoC resource storage caps + HUD have/max** â€” bank max from lumberyard/foundry/silo
>   (`storageCapacity`) + baseCap; clamp grants; chips `current/max`. Collectors stay pending-only.
>   File `WORK_ORDER_857_coc_resource_storage_caps_hud.md`, READY.
> - **856** = **Crystal Mine actually pays out** â€” `mine_crystal` has never yielded a single crystal and
>   cannot: payout is gated at L3 (`CrystalMine.cs:188`), `_currentLevel` is a private field that persists
>   NOWHERE, and the catalog authors no `maxLevel`, so the upgrade verb answers `1 >= 1` and toasts
>   "Max tier reached." on a freshly-built mine. Root cause is the ORACLE:
>   `CrystalProductionRegression.cs:63-66` reflectively writes `_currentLevel` to max â€” a state no player
>   can reach â€” while claiming to prove yield "at a reachable level". Fix pulls the level from the
>   existing persisted `PlacedStructureData.level` (do NOT add a 4th level authority) and authors a
>   `[2,4,7]` per-wave curve. File `WORK_ORDER_856_crystal_mine_pays_out.md`. **READY.**
>   Spawns three separate WOs, NOT folded in: jeweler-as-crystal-upgrader (new feature, 5 ordered
>   steps â€” author the ladder LAST); `HealingFountain` (identical bug, and worse: it authors
>   `maxLevel:3` AND keeps the Coins F-key path, so two systems can each level one building); and a
>   generic `ApplyTierStats` level-receiver seam.
>
> *(banner bumped 856 -> 857 in the SAME edit as the WO-856 mint â€” the rule that broke 5x on 08-02)*
> - **855** = **Economy balance (mobile grind)** â€” data-first: tower/troop/gear costs, build+upgrade
>   times, gather yields, difficulty light pass, **generic tower spam softcap** (cost mult only).
>   NO system rewrites. File `WORK_ORDER_855_economy_balance_mobile_grind.md`, READY.
> - **854** = **Quest Completability Program** â€” owner ruled that a quest which can be ACCEPTED and TRACKED
>   but not completed is a BUG. Audit found **0 of 63 stages completable**: `QuestService.AdvanceQuest` has
>   exactly ONE caller and no shipped dialogue names any of the 24 quest ids. 7 phases behind a
>   `QUEST_REACH_OK <n>/63` oracle + ratchet. Adds `completeOn` to `QuestStage` (**no save bump** â€” catalog
>   content, not the persisted contract). File `WORK_ORDER_854_quest_completability_program.md`.
>   **READY (P0-P2, zero owner deps); P3-P7 gated on the Â§6 ruling set.**
>
> *(banner bumped 855 -> 856 with WO-855 economy mint)*
> - **853** = **Structures are targetable** â€” the disjoint-contract seam. `WallSegment.cs:28` + `Gate.cs:45`
>   implement `IDamageableStructure` while `TroopController.cs:449-469` sweeps for `IDamageable`; the two
>   are disjoint, so nothing can damage a wall, gate or enemy tower and "Razed %" counts bodies. Extends
>   the `RaidSpire` dual-interface precedent and gives `CombatFaction.Friendly` its first real producer.
>   âš  walls must STAY on layer `Structure` (it is the tower LoS mask). File
>   `WORK_ORDER_853_structures_are_targetable.md`. **READY** â€” one owner decision open (scoring weights).
>
> *(banner bumped 853 â†’ 854 in the SAME edit as the mint â€” the rule that broke 5x on 08-02)*
> - **851** = every-4th-wave BOSS encounters + statistical adaptation (owner rulings: statistics not
>   AI, every 4th wave, boss enemies at boss scale, Syndrath's flair â€” JSON-driven HP bar + boss
>   music reusing the least-used clip). File `WORK_ORDER_851_every_fourth_wave_adaptation.md`.
>   **SHIPPED as spec in `0bb46258` â€” keeps 851 (first-on-disk-and-referenced).**
> - **852** = Echo card fixed-band layout (UI-seat RCA: the WO-830 resource picker's 1/n fraction
>   slices collapse below MinTouchPx and the buttons stack up into the info block; same class as
>   WO-832 Â§4 / WO-841 fraction-band culling). **Renumbered from a collided 851 mint â€” the CLI's
>   851 was already committed. THE COLLISION WAS THE CLI'S FAULT: it wrote 851 to disk without
>   bumping this banner, so the UI seat correctly read "next free = 851".** READY.
> - **âš  FIVE collisions on 2026-08-02 alone.** The banner is only an authority if it is bumped in
>   the SAME edit as the mint â€” including by the CLI. Owner ratification of a reserved UI block
>   (860â€“899) is now overdue.
>
> ## âš  TWO-BLOCK ALLOCATION IN USE (2026-08-02 evening) â€” the collision fix, in practice
> | Block | Owner | Next free |
> |---|---|---|
> | **main line** | CLI | **â†’ READ THE HEADER AT THE TOP OF THIS FILE. THIS ROW NO LONGER CARRIES A NUMBER.** |
> | **1000â€“1099 reserved** | UI seat | **â†’ READ THE HEADER. THIS ROW NO LONGER CARRIES A NUMBER.** |
>
> ### âš  WHY THESE CELLS ARE EMPTY (2026-08-09 â€” a live re-mint hazard, not tidying)
> This table used to restate the next-free numbers. It read **932** for the main line while the header
> above said **939** â€” a seven-number gap, and **932â€“938 all exist on disk**. A seat trusting the table
> would have re-minted over SEVEN live work orders. The row even carried a note saying it had been
> "corrected 2026-08-09"; it went stale the SAME DAY, because 933â€“938 were minted after the correction.
>
> That is the exact failure this file's own rule warns about â€” *"never a number copied into any other
> doc"* â€” and the copy was **inside the numbering authority itself**. A duplicate cannot be kept honest
> by discipline; it can only be removed. **The header is the sole source. Do not restore numbers here.**
>
> *(WO-1021 AMENDED 2026-08-16 - no new number. Owner screenshot at WIS 252: *"Still Messy."* â˜… NEW
> Â§2.1d **FOCUS INFLATION â€” a SECOND defect on top of the Â§2.1b spacing gap.**
> `HeroSkillTreeVM.ResolveStates:839-856` resets `nextTaken` **PER TRACK** (its comment is correct:
> *"on an ORDERED track exactly ONE node may be Next"*), so the board carries **one `Next` per track**.
> The view at `HeroSkillTreePanelMvvm.cs:451-452` then renders EVERY `Next` at `NodeFocusPx` (168 vs
> 136) with a thick gold ring â€” so the file's own header premise, *"ONE thick gold FOCUS plate"*, is
> violated by construction the moment there is >1 track. At WIS 252 that is ~10 oversized gold plates
> overlapping. â›” **The VM is NOT wrong â€” do NOT "fix" `ResolveStates`** (WO-910's Inert rule depends on
> that loop). **The VIEW is over-consuming a per-track signal as a board-level one.** Fix = split
> SELECTED (board-level singleton, owns the big plate, **at most ONE ever â€” assert it**) from NEXT
> (per-track, quiet rim/pip, **same size**, shape-carried not size-carried, greyscale-safe).
> âš  Â§2.1b and Â§2.1d are INDEPENDENT and BOTH required â€” spacing alone still leaves 10 huge plates;
> focus alone still leaves overlap. Owner has said "messy" TWICE; half a fix earns a third report.)*
>
> *(WO-1042 Â§6 AMENDED 2026-08-16, no new number â€” owner: *"obsidian has a socketing example in their
> demo too"*. VERIFIED ON DISK: `Prefabs_Obsidian/**Socketing.prefab**` exists (assembled screen), plus
> `Socketing_Slot.png` (âœ… mirrored as `slot_socket.png`) and `Socketing_Slot_2.png` (âŒ NOT mirrored);
> `Enchanting.prefab` + `Crafting.prefab` are also unmined. This materially lowers the FUTURE socketing
> WO's UI cost â€” per Grok-02 Â§1 the assembled prefabs are the **"parameter source of truth - measure the
> hierarchy"**, the same relationship `TalentTree.prefab` has to WO-1021. âš  The SYSTEM is still the work
> (no stat pipeline / save schema / socket model), but layout + slot geometry + interaction grammar are
> answered. âš  Must be MIRRORED first â€” `Assets/Blink` is gitignored, and BLINK_SME Â§5.3 records the
> full-screen prefabs as `BlinkPrefabMirror`'s planned-but-UNSTARTED "second pass", so socketing would
> be its first customer. Recorded as intelligence only â€” NOT actioned in 1042.)*
>
> *(UI-seat bumped 1042 -> 1043 in the SAME edit as the WO-1042 mint â€” owner design 2026-08-16: the
> **ROUGH STONE -> JEWELER POLISH -> refined gem** link. She named a REAL gap WO-1041 left open:
> `jeweler-recipes.json` demands SPECIFIC gems (`ing_ember_crystal` etc.) in exact counts, so a direct
> finished-gem drop must "know" what the player needs and resolves all anticipation instantly. A rough
> unidentified stone with flavour text fixes both and gives the run GRADE a second expressive landing
> spot. â›” **THE POLISH TIMER MUST BE AN OBSIDIAN QUEUE JOB** â€” canon Â§8: that queue is the SINGLE HOME
> for ALL timed work; it inherits persistence, offline accrual, the v37 paid-basket cancel refund and
> the depth cap of 5/line for free. A bespoke timer is a second authority. âš  **RULING NEEDED on
> RUSH-FOR-CURRENCY: a paid instant-resolve of a RANDOM outcome is mechanically a LOOT BOX** and is
> regulated in several jurisdictions we ship to â€” recommend NO rush on a random job (rushing a
> DETERMINISTIC job is fine). âš  Also flagged: grade should shape ODDS only, not odds+tier+time (all
> three trivialises it). â˜… **The two directions are NOT the same size:** ring/amulet crafting is
> NEARLY FREE (recipes + chain + panel all shipped) while **socketing armor/weapons is PILLAR-SCALE and
> NOT BUILT** â€” only `slot_socket.png` art exists. **Do the ring path; file socketing separately** or a
> shipped free loop sits behind a months-long system. READY.)*
>
> *(UI-seat bumped 1040 -> 1042 in the SAME edit as the WO-1040 + WO-1041 mints, owner 2026-08-16
> *"Finishing a dungeon feels lackluster"*. **1040** = the TREASURE FOUND panel overlaps 3 text blocks
> (`DungeonTreasurePanel.cs:128-141` â€” `EnsureBand` GROWS the payout with line count while its
> neighbours sit at FIXED fractional anchors = the documented WO-865 class, same as WO-1030) + the
> payout is ALL dungeon consumables so the loop is CLOSED; owner ruled **GRADED RUNS** (capture kills /
> potions / deaths / time -> rate the run -> reward tier). âš  3 rubric traps written up: speed-weighting
> punishes EXPLORATION; potions+deaths DOUBLE-punish a hard fight; a completed run must ALWAYS pay.
> **1041** = â˜… **THE STONE LOOP IS ALREADY BUILT â€” it is missing ONE thing, a SOURCE.** I first scoped
> this pillar-sized; that was WRONG. Measured: gems EXIST (`ing_ember_crystal`/`ing_aether_shard`/
> `ing_heartstone_crystal`), the **Jeweler is BUILT** (`PanelId.JewelerCrafting=10`, `JewelerPanelMvvm`,
> `BuildingKind.JewelersBench=9`, **43 files**, WO-553), `jeweler-recipes.json` has **6 recipes**
> (base accessory + gems -> higher tier), and the ring chain `ring_iron->steadfast->embercoil->heartward`
> is authored (WO-543). â›” **DO NOT build a socketing system / stone catalog / new screen â€” all shipped.**
> The whole ask is a DROP TABLE. Owner ruled **WEIGHTED ODDS, not a no-deaths gate** â€” a flawless-run
> gate locks the median player out of the very reward that justifies the dungeon, and locks out the
> players who most need the power. Odds come from 1040's grade (ONE rubric). âš  Size rates against the
> REAL gem counts in the recipes. âš  **Gems must NEVER become purchasable** (WO-1037 just added impulse
> packs) or the pillar's justification is void. READY.)*
>
> *(UI-seat bumped 1039 -> 1040 in the SAME edit as the WO-1039 mint â€” owner 2026-08-16: build mode
> *"looks good but disjoined"*. â˜… **THIS IS ALREADY HER RULING AND IT WAS HALF-BUILT.**
> `docs/UI/OBSIDIAN_UI_DESIGN_skilltree_inventory.md` **Â§6** records verbatim: *"make a styling-type
> SINGLETON for ONE UI style for EVERYTHING - not piece this and piece that"*, with a full phased spec
> (Â§6.1-6.7). MEASURED: `Assets/_Modules/Core/UI/UiStyle.cs` **EXISTS** (phase a landed), kit partially
> routed (phase b), but **phase (c) migrate-panels NEVER HAPPENED for build mode** and phase (d) never
> began â€” **only 10 files in all of `_Modules` reference `UiStyle.`, and `BuildMenu.cs` has ZERO**;
> `ShopTheme.cs` still duplicates the palette Â§6.1 flagged. The screen has **4 independent chrome
> deciders**: `ObjectiveBannerUi` / `TutorialSkipUi` / `BuildHudController` / `BuildMenu` -> 3-4 plate
> languages in one frame. âš  SCOPE = **THIS SCREEN ONLY**, one increment of phase (c) â€” Â§6.6 lists EVERY
> screen as an offender and a migrate-everything ticket is the structural-refactor smuggling
> ARCHITECTURE_PRINCIPLES forbids. âš  Unify the LANGUAGE, not the emphasis (gold stays accents-only;
> a quieter category column may be correct hierarchy). âš  **4 tickets now touch build mode â€” 1033
> (landed), 1034, 1037, 1039 â€” SEQUENCE them, do not run as parallel lanes on the same files.** READY.)*
>
> *(UI-seat bumped 1038 -> 1039 in the SAME edit as the WO-1038 mint â€” **FIVE UNDECLARED TAGS ARE USED
> IN CODE; every one THROWS.** F8 seq 2434/2435 live: `UnityException: Tag: SpawnPoint is not defined`
> at `CastleDefensePlansService.ResolveGateSeat:183` -> a progression reward silently never places.
> â˜… AUDIT: `TagManager.asset` declares **exactly FOUR** tags (Tower/Building/HeartTarget/Player), but
> code uses `HeroTarget` **x13**, `SpawnPoint`, `ScreenFlash`, `Pet`, `Enemy` â€” all UNDECLARED.
> `FindWithTag`/`FindGameObjectsWithTag`/**`CompareTag` THROW** on an undeclared tag (they do not return
> null/false), so each is a latent crash; only `Guard.Try` coverage is hiding them. âš  `Enemy` is a
> **LAYER**, not a tag â€” tag/layer confusion. `HeartTarget` is declared but has **ZERO** uses.
> â›” **CLAUDE.md Â§7 IS FALSE**: it asserts "Enemy spawn tags: `SpawnPoint`" â€” the tag does not exist, and
> Â§7 ALREADY records that `HeroTarget` "was never declared" **while 13 call sites still use it**. The
> lesson was written down and the code was never swept. âš  **NO GATE CAN CATCH THIS** â€” tag names are
> runtime strings, so it compiles clean; that is how HeroTarget survived from a canon ruling to 13 live
> sites. Fix = per-tag DECLARE-or-REPLACE (prefer component lookups; a GameObject has ONE tag) + a
> regression asserting every tag literal is declared + correct Â§7 in the same commit (Â§15). HIGH.)*
>
> *(UI-seat bumped 1037 -> 1038 in the SAME edit as the WO-1037 mint â€” **shortfall = the offer moment.**
> Owner 2026-08-16: suggest a pack when an upgrade is short ("900 Wood - need 880 more"). RULINGS IN:
> build it **STUBBED + a flag that BLOCKS THE PROD PUSH** until monetization activates; and **option (b)
> â€” SINGLE-RESOURCE IMPULSE PACKS, small/medium/large, per resource type** ("small wood only / medium /
> large / same with all types"). â˜… THE FINDING THAT FORCED THE RULING: measured `packs.json` â€” all 13
> packs carry ONLY `coins/crystals/food/glimmer`, **wood=False iron=False**, so NO pack could fulfil a
> wood shortfall at all. âš  **THIS AMENDS WO-947** (regular basket = wood+iron was written assuming the
> regular basket is EARNED, not bought) â€” annotate WO-947 + the anchor or canon self-contradicts (Â§15).
> âš  Stockpile caps interact â€” decide overflow behaviour before selling a resource the player cannot
> receive. â›” **ALL OF IT IS DISPLAY-ONLY until WO-931's 3 preconditions** â€” `StubWalletProvider` still
> grants packs for ZERO payment in every player; adding purchasable-looking SKUs now is the WO-931
> defect at greater volume. A default-off flag is NOT enough â€” WO-931 shipped because the stub had no
> BUILD-CONFIG guard. â˜… Rewarded-ad idea ("watch an ad for free 500 wood") = **WO-912, DO NOT RE-MINT**:
> the ad seam ALREADY SHIPPED (`IAdService.cs`, `AD_SEAM_OK`, `AD_COVENANT_OK`), provider RULED (Unity
> LevelPlay), **D3 is the sole blocker â€” no LevelPlay SDK under `Assets/`**. â›” LEGAL: the LIVE privacy
> policy claims NO ADS (`PRIVACY_POLICY.md:87-89`) â€” owner/attorney must update the published page
> BEFORE any ad ships; live legal copy is not a seat's to edit. READY.)*
>
> *(UI-seat bumped 1036 -> 1037 in the SAME edit as the WO-1036 mint â€” **`founding_walk` STEP-STUCK
> RECURS AFTER WO-962 SHIPPED.** F8 seq 2433 (125s) + seq 2343 (241s), two sessions a day apart, same
> missing `hero.reached:guide_gate`. âš  WO-962 is DONE (`e2759f1e9`, the guide_gate LATCH) and was filed
> against this exact symptom â€” **do NOT re-implement it**; read it, confirm the latch holds at runtime,
> then look DOWNSTREAM. Three causes needing opposite fixes: latch regressed / event never fires with a
> good anchor / hero never arrives. â˜… CHECK FIRST: the 08-15 harvest carried `[Flow:HeroOwner]
> timeScale=0.00 WORLD CLOCK FROZEN` â€” that alone would explain both captures. âš  COORDINATE WITH
> WO-1031: its guide-despawn ruling can turn this INTERMITTENT stall into a DETERMINISTIC hard block on
> the first minute if the wolf despawns before the gate event. HIGH â€” it blocks the FTUE. READY.)*
>
> *(Also folded into WO-1025 from F8 seq 2428-2431: **the Heart audit is BOTH a lead and a noise source.**
> `AuditHeartPresentation` found a live non-suppressed emitter at the Heart with material `Distortion`
> / shader `Shader Graphs/HS_Distortion` (**HS_ = Hovl Studio**) â€” start the emitter hunt there. AND
> `DescribeParticle:556` reads `mat.mainTexture` UNGUARDED, which Unity logs as an ERROR when the shader
> has no `_MainTex` â€” so the diagnostic floods F8 four times per scene load. **Guard with
> `HasProperty("_MainTex")`, do NOT delete the audit** (Â§12). Same class as the WO-1022 Â§6 MagentaGuard
> noise: a diagnostic succeeding while logging at a severity that costs the owner triage attention.)*
>
> *(UI-seat bumped 1031 -> 1036 in the SAME edit as the WO-1031/1032/1033/1034/1035 mints, owner asks
> 2026-08-16. **â›” 1031 CARRIES A TRAP â€” READ IT BEFORE ANY "REMOVE FROST" WORK: "Frost" IS the FTUE
> GUIDE WOLF.** `PetTaskController.SpeakerName()` maps `ice-wolf -> "Frost"`, and `ice-wolf` is the
> guide body (`OneGuideBodyRegression:13` "guide BODY summoned ('ice-wolf')",
> `FoundingGuideWolfBodyRegression:69-74`). A grep-and-delete of "Frost" DELETES THE FTUE GUIDE the owner
> confirmed is correct. Remove the ENGAGE PROMPT (the `proximity` auto-fire at
> `PetTaskController.cs:137-145` is why it keeps popping), never the wolf. â˜… **And "Frost" is a CANON
> VIOLATION**: the Echo's real name is **Aldwin** (`EchoRosterCatalog.cs:18`, `TutorialGuide.cs:20/61-65`,
> objective strip "Follow Aldwin to the gate") â€” `SpeakerName()` is an invented naming scheme bypassing
> the name authority. âš  Alduin (necromancer) != Aldwin (Ice Echo), one letter apart, pinned by
> `DungeonLoreReadableRegression:74-91` â€” do NOT "correct" one into the other Â·
> **1032** wolf runs sideways: `PetDeployer.cs:442` `PetForwardYaw = -90f` was authored for the RETIRED
> Tripo fox (`ice-wolf-fox-legacy.fbx`) and is still applied to the NEW wolf.fbx â€” âš  fix by DERIVING the
> yaw, not by changing the constant, and CHECK flame-pup/aether-sprite don't break Â·
> **1033** Skip button -> `BuildObsidianButton` + top-middle (currently unstyled, overlaps the build rail
> AND the Echoes chip) Â· **1034** build tooltip "tap to place, then rotate if needed" Â·
> **1035** portal VFX seated INSIDE the mesh (1/3 height, bounds-centred, DERIVED from renderer bounds) â€”
> âš  also check the live `[Flow:MagentaProbe] FAIL ... BuildPortal` (F8 2404-2415) FIRST, the "blobs" may
> be magenta fallback geometry. **1032 + 1035 are both the canon hand-authored-vs-DERIVED pattern.** READY.)*
>
> *(UI-seat bumped 1030 -> 1031 in the SAME edit as the WO-1030 mint â€” owner screenshot 2026-08-16:
> the **Echo task dialogue ("Frost") CLIPS ITS OPTION LIST**. Chain traced:
> `PetTaskController.BuildEngageDef():168-212` -> `DialogueService.PlayDef` -> `DialogueView`.
> âš  **Options ARE measured** (`optionsPx` IS summed into `contentPx`, `DialogueView.cs:734-740`) â€” the
> bug is the **CEILING**: `Mathf.Clamp(contentPx, MinBodyPx, _maxBodyPx)` where `_maxBodyPx` is
> proportional to `CanvasLocalHeight()` via the HUD-safe band (`:664-678`), so LANDSCAPE starves it and
> the overflow is the bottom of the option list. **Do NOT delete the clamp or widen the HUD-safe band** â€”
> that clamp IS the 2026-07-16 owner fix and the band is what clears TargetInfo + the action bar. Fix =
> reserve OPTIONS height FIRST, clamp the TEXT: text scrolls, choices never do. Also: portrait is a
> generic silhouette â€” check the resolver KEY (display name "Frost" vs an id) before commissioning art;
> a nameâ†’key mismatch looks identical to missing art. âš  **OWNER RULING same day: the FTUE guide IS the
> wolf.fbx and is CORRECT** â€” WO-1022 Â§5's WO-993/`PetHeroLeash` note was corrected because it pointed a
> seat at removing it. READY.)*
>
> *(UI-seat bumped 1026 -> 1030 in the SAME edit as the WO-1026/1027/1028/1029 mints â€” the owner's
> **CoC + WC3 design review**, full analysis in `docs/DESIGN_REVIEW_COC_WC3_LENS_2026-08-15.md`.
> VERDICT: both engines are BUILT and NEITHER CLOSES ITS LOOP. **1026** = the base is never attacked, so
> player-authored layout has ZERO consequence (grep: `RaidDefen*`/`DefenseReport`/`Revenge`/`Trophy` = 0
> hits) â€” highest leverage, needs an owner ruling on PvE-siege vs async-PvP vs ghost-PvP first Â·
> **1027** = the queue is mechanically better than CoC's but has no IDLE-BUILDER ACHE and no session
> shape; surfacing only, cheapest item Â· **1028** = 4 dungeons are `PathComplete` + torch/oil/darkness
> ~90% built and there is NO reason to descend and NO payoff that feeds town â€” largest built-but-parked
> value in the tree Â· **1029** = `ClanService` is a self-declared PlayerPrefs stub, `Donat*` = 0 hits;
> ship DONATIONS not wars, sequenced LAST and BLOCKED on `api/` being PREVIEW-only.
> âš  **WO-910 (31 of 40 dead Ranger/Mage talent nodes) OUTRANKS ALL FOUR and is already open â€” do NOT
> re-mint it.** It is the WC3 pillar broken on the very screen WO-1021 is polishing. READY.)*
>
> *(UI-seat bumped 1025 -> 1026 in the SAME edit as the WO-1025 mint â€” owner 2026-08-15: *"these graphics
> on tree look amatuerish"* (Heart of Elarion, hub centre). âš  **THE OBVIOUS DIAGNOSIS IS WRONG:** F8
> seq=2398 harvest proves BOTH authored loops are WITHHELD at this tree â€”
> `whiteSwirlSuppressed=True treeAuraSuppressed=True treeHandle=none` â€” so the yellow cone + white
> starburst on screen are **NOT** `Aura_HeartPulse` / `TreeofLifeAura_Aura`. Do NOT re-tag those and do
> NOT flip the suppression flags (they are deliberate, WO-1002 + the owner's "stray heal VFX be gone").
> The emitter is UNIDENTIFIED â€” step 1 is INSTRUMENT the tree's child hierarchy per Â§12, likely particle
> children baked into the prefab (which would render regardless of the controller's flags, explaining the
> contradiction). Separate contributor: the tree model has ONE texture
> (`enchantedtree3dmodel_basecolor.JPEG`, no normal/roughness/AO) so it renders flat under URP. READY.)*
>
> *(UI-seat bumped 1024 -> 1025 in the SAME edit as the WO-1024 mint â€” **STRUCTURES BURN WITH NO REPAIR
> SURFACE AT ALL.** Owner F8 seq=2398/2342: `WallRepairController=ABSENT HubRepairAffordance=ABSENT
> WaveManager=Active` in `Main_Castle_Overworld`. Root cause PROVEN and the code predicted it in its own
> bail-path comment (`HubRepairAffordance.cs:111-116`): the installer is a ONE-SHOT on `sceneLoaded` and
> `SceneHasRepairables()` runs while the player-built town is still EMPTY â€” placement restores AFTER
> scene load, the gate bails, and it NEVER retries. `StructureDamageVisuals` installs unconditionally, so
> fire renders with no repair option. âš  NOT a coverage bug â€” do NOT widen `SceneHasRepairables()` again
> (already widened once); **the bug is the TIMING, not the set.** READY.)*
>
> *(UI-seat bumped 1023 -> 1024 in the SAME edit as the WO-1023 mint â€” talent icon map AUDITED 2026-08-15
> and it is GOOD: 83/83 coverage, 0 orphans, 0 iconPath mismatches, 0 missing sprites, both canonical
> copies byte-identical. Three real findings: (1) **two pairs of talents render the IDENTICAL icon** â€”
> `Rogue7` claimed by knight.t2n6 Venombrand + ranger.t2n2 Venomcraft, and `Arcanist1` by mage.t1n1
> Arcane Focus + shared.n9 Arcane Bolt; (2) **NO regression pins any of it** â€” `grep talent-icon-map`
> over `Assets/Editor/` returns ZERO, i.e. the exact WO-996 armor.json shape (two copies, no oracle,
> Resources wins at runtime so the Editor looks fine); (3) `emblem/` (25 class crests) and `classslot/`
> (25 themed plates) are ALREADY COMMITTED and UNUSED â€” free per-tree visual identity. Archetype
> coherence: mage strongest (19/20 Elementalist), ranger strong (13/20 Assassin), knight widest spread
> across 14 folders **and correct** â€” its economy/fortification nodes have no Warrior equivalent, so do
> NOT narrow it. Governing rule confirmed: match the SKILL's meaning, not the tree's class. READY.)*
>
> *(UI-seat bumped 1022 -> 1023 in the SAME edit as the WO-1022 mint â€” `Main_Castle_Overworld.unity`
> carries **56 references to three DELETED prefab GUIDs** (StorefrontCrate / CourtyardFloor / StorefrontVine).
> `cc122e844` (WO-608 seam cleanup, 2026-07-04) removed the assets; the scene's pointers survived because
> no gate reads scene GUID refs. Throws on EVERY scene open â€” ~48 of the 60 F8 captures queued 2026-08-15,
> which is what buried two real tutorial STEP-STUCK signals. Decide DELETE-REFS vs RESTORE-PREFABS first;
> never hand-edit the .unity. READY.)*
>
> *(UI-seat bumped 1021 -> 1022 in the SAME edit as the WO-1021 mint â€” talent tree, the last four gaps to
> the Obsidian demo AFTER `61a2a701c` closed sizing/connectors/frontier: (1) the lattice is still FIXED
> px `GraphUnitWpx/Hpx = 1180x780` against a ~1695x493 body well, so the graph hugs the upper-left and
> leaves a dead black third; (2) the viewport paints an OPAQUE slab over `frame_talent` â€” the named
> Grok-02 Â§6 failure mode â€” while mirrored `panel_talent` + `deco_talent_1/2` sit unused; (3) the Wisdom
> chip ellipsizes to "WIS..." over the board, breaking the no-ellipsis wallet law; (4) locked skill art
> reads too dark. Plus one anomaly to INSTRUMENT not guess: a bare "1"/"0" cost pip where the data has no
> zero-cost node. Multi-rank (`3/3`) is OUT â€” explicit V1 non-goal. READY.)*
>
> *(UI-seat bumped 1020 -> 1021 in the SAME edit as the WO-1020 mint â€” WALLS CANNOT BE PLACED
> ADJACENT to each other (owner F8 seq=2327). Trace shows ghostValid=True while a neighbouring
> 'wall_wood@16_17' is still BUILDING (remaining=8s) â€” suspect the in-progress build job's cell
> reservation blocks the adjacent cell, or a rotated footprint over-claims. Walls are useless if
> they cannot form a RUN.)*
>
> *(UI-seat bumped 1019 -> 1020 in the SAME edit as the WO-1019 mint â€” Thrain/MAGE action bar: the
> authored defaults in abilities.json are CORRECT and all-magic (Q Fireball / W Arcane Shell / E Mend /
> R Meteor), so the reported "inherits the previous character's hotswap, nothing explicit for DPS" is a
> RUNTIME BINDING defect â€” the bar does not rebind to the selected hero's class defaults on hero switch.
> PLUS an owner kit ruling: the Mage plays single-target pull-and-kill and wants POISON, DRAIN (steal
> health), FIREBALL and THUNDER â€” poison/drain/thunder do not exist in the mage pool today.)*
>
> *(UI-seat bumped 1018 -> 1019 in the SAME edit as the WO-1018 mint â€” **F8 CAPTURES ARE STILL BEING
> BURIED â€” WO-965's fix is HALF-LANDED.** The ack script correctly acks oldest-first, but the PRODUCER
> never wrote QUEUE.jsonl entries (3 live WARNs 2026-08-10: "seq=NNNN had no QUEUE.jsonl entry;
> recovered from <file> (producer running pre-WO-965 code?)"), so the pending list is rebuilt from a
> single recovered capture file and the ack watermark then buries the un-recovered older seq. Observed:
> acking with 2313 pending closed 2314 and reported "Inbox clean" â€” 2313 was never triaged by any seat.)*
>
> *(UI-seat bumped 1017 -> 1018 in the SAME edit as the WO-1017 mint â€” F8 seq=2314 ERROR: TOWN SYSTEMS
> RUN INSIDE DUNGEONS. `TownActivityProbe.Poll` (`TownActivityProbe.cs:147`) FAILs with
> `suspended=False policy=SuspendAndResume reason='none'` in `Dungeon_HealersCottage` â€” the scene-driven
> suspension gate never fires for dungeon scenes, so town systems (incl. an Enemy in the active scene)
> stay alive off-hub.)*
>
> *(UI-seat bumped 1016 -> 1017 in the SAME edit as the WO-1016 mint â€” **HIGHEST (owner F8 seq=2312)**:
> hero locomotion is DEAD in dungeons. Captured proof: world position advances (Zone x=-28.0,z=-1.8 ->
> x=-26.9,z=-4.7) while `[Flow:HeroLoco] vel=0.00 m/s` EVERY frame and the animator holds ONE clip
> `mixamo.com(w=1.00)` with `[Flow:GaitF] speedP=0.00` â€” the hero SLIDES through the dungeon in idle.
> Velocity source is not fed by whatever moves the hero in this scene.)*
>
> *(UI-seat bumped 1015 -> 1016 in the SAME edit as the WO-1015 mint â€” EQUIPMENT/paperdoll screen is
> broken: ~40% dead space above the content, the hero PREVIEW BOX RENDERS EMPTY, every slot's label +
> value + hint OVERPRINT each other, and the rogue "Orient" button appears HERE TOO â€” proving WO-1010 D1
> is a GLOBAL stray control, not a build-mode one. Also the Echoes chip bleeds through the modal.)*
>
> *(UI-seat bumped 1014 -> 1015 in the SAME edit as the WO-1014 mint â€” TUTORIAL NARRATIVE COHERENCE: TWO
> guide arcs are live at once (the legacy hard-coded "Sylas" human-scout script AND the new {guide}
> pet-Echo founding arc), so two guides spawn, the wolf never introduces itself, the name drifts
> ("Storm"), the wolf does not LEAD the walk, a second wolf is introduced at the entrance, and the pet
> asks for orders before its utility was ever explained. Retire the legacy arc, author the wolf's
> identity, fix the lead + the ask-order ordering.)*
>
> *(UI-seat bumped 1013 -> 1014 in the SAME edit as the WO-1013 mint â€” "Castle Defense Plans": survive
> wave 2 -> a physical drop at the gate unlocks the Arcane Spire card (starts VISIBLE but LOCKED,
> "Recover the plans") + funds the first build; the player still builds it themselves (reinforces the
> WO-1010 loop); delivered through the WO-1012 contextual one-shot kit; canon: recovered knowledge of
> the fallen civilization.)*
>
> *(UI-seat bumped 1012 -> 1013 in the SAME edit as the WO-1012 mint â€” tutorial/FTUE presentation + pacing
> redesign: retire the boxed markers / fat top banner / Next-Next coach cards for a spotlight mask + ONE
> chevron / ghost-finger + thin bottom objective strip with beads; the GUIDE is a rotating HERO â€”
> guide=(playerHeroClass+1)%4 over the HeroClass enum, never yourself, retiring the KayKit "Sylas"
> stand-in; pacing = the owner's dynamic arc (walk with the guide -> build ONE piece -> ONE cannon ->
> the timers, one line -> enemies at the gate -> win + handoff). Bones stay: tutorial-steps.json,
> TutorialFlow, TutorialSignals, Onboarded gate, grants.)*
>
> *(UI-seat bumped 1011 -> 1012 in the SAME edit as the WO-1011 mint â€” BOARD workflow acclimation for the
> CLI: adopt BOARD.html/board_build.py as the live board (Notion retired 2026-08-08), wire regeneration
> into session boot, canonize the status-line vocabulary + same-commit status hygiene, then sweep the
> ~516-stale-READY status debt.)*
>
> *(UI-seat bumped 1010 -> 1011 in the SAME edit as the WO-1010 mint â€” build-mode UI redesign from real
> tester feedback ("buttons everywhere"): owner-picked Direction B "Carousel + minimize" (first pick C,
> reversed to B on re-read, 2026-08-08) â€” card carousel that minimizes to an edge tab on select, contextual
> chips on the ghost, optional D-pad toggle; retires the Rotate/PLACE/Cancel intent bar + always-on D-pad.)*
>
> *(UI-seat bumped 1009 -> 1010 in the SAME edit as the WO-1009 mint â€” composed-dungeon interactable ART +
> AFFORDANCE pass: chests are gold PRIMITIVE CUBES (BreakableContainer.Create), key pickups + locked ports
> are INVISIBLE triggers (the locked door has NO mesh) â€” give each a real KayKit prop + a self-explaining
> "what/how" cue. Exit = WO-1007; exit beacon = WO-1008. Owner felt-test 2026-08-08 "dont understand the action".)*
>
> *(bumped 1008 -> 1009 in the SAME edit as the WO-1008 mint â€” dungeon EXIT beacon must read as LIGHT.
> `DungeonExitInteractable.cs:233` builds a `PrimitiveType.Cube` on `Universal Render Pipeline/**Unlit**`
> (`:284`), so it ignores every light in the scene: invisible as a defect while dungeons were bright,
> screaming since WO-919/1004 dropped ambient to #0a0a10. Owner 2026-08-08: "big green bar doesnt make
> sense". **PAIR IT WITH WO-1007** â€” that one replaces the archway and explicitly keeps this beacon.)*
>
> âš  **COLLISION, RESOLVED 2026-08-08 09:26.** Both seats minted **1007** within two minutes, on the same
> object. `WORK_ORDER_1007_dungeon_exit_real_asset.md` (09:24) was first on disk AND banner-referenced,
> so it KEEPS 1007 per the Â§2 rule; the beacon WO renumbered to 1008. **This is the failure mode the two
> disjoint blocks were meant to prevent, and it still happened â€” because both seats were working the
> SAME 1000-block.** The block split protects CLI-vs-UI, not UI-vs-UI. If two sessions are going to mint
> in the 1000s at once, they need sub-ranges or one of them has to stop minting.
>
> *(UI-seat bumped 1007 â†’ 1008 with the WO-1007 mint â€” real dungeon EXIT asset: replace the primitive
> emerald-cube archway in DungeonExitInteractable.BuildVisual with a KayKit Dungeon Remastered prop
> (lit stone doorway/portal or stairs-up), keeping the walk-in trigger + beacon; distinct from the purple entry.)*
>
> *(UI-seat bumped 1006 â†’ 1007 with the WO-1006 mint â€” Manage becomes a launcher: the long combined upgrade
> scroll moves OUT into dedicated per-category browser panels reached by buttons on Manage, each row showing
> cost + benefit + time-to-build + affordability, drilling into the existing single-item detail panels.)*
>
> *(UI-seat bumped 1005 â†’ 1006 with the WO-1005 mint â€” dungeon UI cohesion: reskin the flat-purple "Descend"
> prompt to the Obsidian kit + fix the mirrored "EXIT" world label + one obsidian-gold theme for all dungeon overlays.)*
>
> *(UI-seat bumped 1004 â†’ 1005 with the WO-1004 mint â€” composed-dungeon (Pipeline A) visual fixes: kill the
> rainbow-atlas floor, strip stray purple/green debug/socket/magenta markers from the build, and extend the
> WO-1000 enclose+relight (ceiling, dark ambient+fog, candle-VFX light) to the composer so every baked dungeon is clean.)*
>
> *(UI-seat bumped 1003 â†’ 1004 with the WO-1003 mint â€” replace town NPCs (KayKit adventurers + CGTrader
> civilians) with the CraftPix Free Medieval People pack (14 dressed townsfolk, shared atlas, license
> commercial-green), staged tracked in Resources/NPCs/People, Humanoid-retargeted onto the shared animator.)*
>
> *(UI-seat bumped 1002 â†’ 1003 with the WO-1002 mint â€” remove the yellow aura plume at the hub Heart of
> Elarion tree base (HeartAuraController tree-ambient loop; extend the hub withhold to cover it).)*
>
> *(UI-seat bumped 1001 â†’ 1002 with the WO-1001 mint â€” Deep Dungeon Program: extend Pipeline A (JSON
> room-graph composer) into a full complex-dungeon engine (deep multi-level stairs, enemy families, boss
> wiring, loot/chests, oil/darkness risk-reward), then three large themed deep dungeons authored as graphs.)*
>
> *(UI-seat bumped 1000 â†’ 1001 with the WO-1000 mint â€” Starter dungeon (KayKit Challenge Outpost) visual
> overhaul: enclose the top / kill daylight, KayKit textured shell + ceiling, candle-VFX lighting, fog/haze,
> real props â€” to the Healer's Cottage bar.)*
> | ~~860â€“899~~ | ~~UI seat~~ | â›” **CLOSED â€” 860â€“899 ALL CONSUMED.** Last mint 899 = HUD polish (analog joystick + wide compass + attack/dodge blend + empty-slot "add skill"). Do not mint here again. |
>
> ### âš  OWNER RULING 2026-08-07: the UI seat moves to the **1000s**.
> Her words: *"we can move to 1000's."* The old 860â€“899 block filled up, and the previously
> *recommended* "913+" was **WRONG and would have collided** â€” the CLI main line is at next-free
> **912** and climbing, so 913 is the CLI's very next number but one. The two blocks must stay
> **DISJOINT**, which is the entire point of the two-block scheme (five collisions in one day,
> 2026-08-02, all caused by two seats sharing a number space).
>
> **1000â€“1099 is the UI seat's. The main line stays below 1000 and must never cross it.**
> If the main line ever approaches 1000, allocate the CLI a fresh block rather than eating into
> this one. Each seat still bumps ITS OWN row in the SAME edit as its mint â€” that rule is unchanged
> and is what actually prevents collisions; a disjoint block only removes the chance of a tie.
>
> *(UI-seat bumped 898 â†’ 899 with the WO-898 mint â€” queue progress bars + "Complete now" with crystals
> (any item/channel; 5-min-bracket cost, flat under 5 min). âš  **899 is the LAST number in the UI-seat
> 860â€“899 block â€” the block is now full after one more mint; a new UI-seat range must be allocated.**)*
>
> *(UI-seat bumped 895 â†’ 898 in the same edit as three mints: WO-895 building-upgrade "next-only" redesign +
> stateful button, WO-896 skill-tree connected-progression-line redesign, WO-897 army composition auto-queue.
> 894 = Victory screen spinning stars.)*
>
> *(âš  **Row corrected 2026-08-06:** the main-line cell read `910` while the RECONCILED banner at the top of
> this file â€” written in the same edit as the WO-910 mint â€” already said next free = **911** and
> `900â€“910 CONSUMED`. The file contradicted itself, which is precisely how a collision starts. The
> top banner wins; this row now agrees with it.)*
>
> *(UI-seat bumped 894 â†’ 895 in the SAME edit as the WO-894 mint â€” Victory screen: real spinning 5-point
> stars + exact wireframe layout, replacing the diamond/no-spin BuildStarRow in EndStateView.)*
>
> *(UI-seat bumped 885 â†’ 894 in the SAME edit as the WO-885â€“893 VFX mints â€” 885 umbrella index +
> 886 death Â· 887 on-hit Â· 888 heal/HP/item auras Â· 889 combat auras/nearest-N Â· 890 harvest Â·
> 891 healer structure Â· 892 building damage Â· 893 portals/spawn/dissolve. Earlier: 884 VFX facade,
> 909 Mage/Ranger. Main-line mirror corrected 908 â†’ 910 after the 909 mint.)*
>
> âš  **This table drifted AGAIN (corrected 2026-08-05).** The UI-seat row read `864` while
> `WorkOrders/` holds an unbroken 860â†’883 â€” twenty numbers stale, and 864 itself is not only
> consumed but is cited as a live dependency by the WO-905 spec ("depends on WO-864's rail
> component"). Three of the range (878/879/880/881/882/883) shipped in commits `31888576`,
> `d185f43c`, `572f1289`. Minting from this row would have collided on the first try â€” the exact
> failure that struck five times on 2026-08-02. `CANON_GROUND_TRUTH_2026-08-05.md` Â§8 already had
> 884 right; the SOLE AUTHORITY was the file that was wrong.
>
> âš  **Prior drift (2026-08-04).** It read `853` while the header row above it read `856` â€” two
> numbers in ONE file, the same two-authority failure the header warns about. The header is the
> authority; this table is a convenience mirror. **Bump BOTH rows in the same edit as a mint, or
> delete this table.**
>
> - **863** = Vercel one-pager + hosted privacy policy (the two dApp Store listing URLs). File
>   `WORK_ORDER_863_vercel_landing_and_privacy_page.md`, READY.
>   âš  **Banner reconciled 2026-08-04 by the CLI: 863 was minted to disk without the banner being bumped**,
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
>   clamps to max(slack, wakeRadius) â€” "a mob may pursue as far as it can perceive"; the entrance
>   camp stays fixed (8.1m > wake 6). SHIPPED, oracle case 7 pins both halves.
> - **850** = deepest-room TREASURE cache (owner request 2026-08-02: "treasure at deepest, simple
>   crafting supply") â€” chest at the dungeon's deepest room granting basic crafting materials. OPEN.
> - **âš  the proposed UI-seat reserved block moves to 860â€“899** (850â€“859 now consumed/reserved by the
>   main line; owner still to ratify).
> - **842** = dual-wallet unify (GameState = single Wood/Iron authority; the 985k-can't-afford-800 capture) Â·
>   **843** = destroyed/sold singleton cards rebuildable (IsPlayerBuilt split from IsBuilt) Â·
>   **844** = Bag potions apply their real effect (was TryRemove + lie) Â·
>   **845** = login error mapping + password reset ("Internal error" F8) Â·
>   **846** = bug-report attribution + notify (playerId = BoundWallet; api bugreports view; watcher trio) Â·
>   **847** = wallet-first Android login ("connect wallet or play as guest"; desktop keeps email).
>   All SHIPPED in commits `a7e4acb2` / `731840e7` (2026-08-02).
> - **839** = raid deploy screen cleanup (UI seat; renumbered from a collided 834 mint) Â·
>   **840** = armorer reachability + shop panel cleanup (UI seat; was 835) Â·
>   **841** = upgrade panel countdown live-tick (UI seat; was 836). All READY, specs on disk.
> - **âš  PROPOSED RULE (owner to ratify â€” the collision struck 3x on 2026-08-02 alone):**
>   **the UI seat mints ONLY from a reserved block 850â€“899**; the CLI mints the main line from this
>   banner. Two seats can then mint in parallel without collision; the CLI reconciles 850-block WOs
>   into the main sequence only if/when renumbering is ever needed. Until ratified, the CLI keeps
>   renumbering collisions by first-on-disk-and-referenced-wins.
> - **837** = Stockpiles cap resource capacity (owner ruling: lumberyard/foundry/silo(="Quarry"?) are
>   the stockpiles â€” OUT of the FoundingKit array; their storageCapacity becomes the live wallet-cap
>   mechanic; founding_stores tutorial re-spec). File `WORK_ORDER_837_stockpiles_cap_capacity.md`, READY.
> - **836** = MASTER_CATALOG full SME refresh (owner-ordered 14-agent fleet, docs-only). File
>   `WORK_ORDER_836_master_catalog_sme_refresh.md`, IN FLIGHT.
> - **835** = HUD action bar: show only APPLICABLE buttons, re-packed (UI-seat spec; renumbered from a
>   collided 833 mint â€” KayKit idle keeps 833). Two OWNER CONFIRM defaults inside (hide Raids until
>   discovery; constant-width vs stretch). File `WORK_ORDER_835_hud_action_bar_applicability_repack.md`, READY.
> - **834** = Blank-founding towns: baked default-town structures stand DOWN until first player build
>   (everBuiltStructureIds, save v36, blank-town gate on every surfacing path â€” 4 seams). File
>   `WORK_ORDER_834_blank_town_baked_standdown.md`, IMPLEMENTED pending gates. *(Renumbered from a
>   collided 832 mint â€” the UI seat's 832 below keeps the number.)*
> - **833** = KayKit NPC idle animation (T-pose F8 fix: shared KayKitNpcIdle.controller retargeting the
>   Knight mocap m-standby-idle onto the 12 Humanoid bodies; oracle-gated). File
>   `WORK_ORDER_833_kaykit_npc_idle_animation.md`, IMPLEMENTED pending gates.
> - **832** = Building-upgrade panel: ONE true gold Upgrade button (tab demoted to underline-tab,
>   in-card gold button removed; UI-seat spec). File `WORK_ORDER_832_building_upgrade_one_true_button.md`,
>   IMPLEMENTED pending gates.
> - **830** = Echo harvest affinity + synergy (all 6 echoes -> unique harvest affinities Wood/Iron/Food/Gold/Crystals/Repairs, 3 disclosed pair-synergies, 1 hidden tri-synergy; UI-seat minted). **831** = Echo emergence 2D sprite beat (new sprite + dialogue advance at unlock, no 3D). Files `WORK_ORDER_830/831_*.md`, READY.
> - **825â€“829** = **IMMERSIVE WORLD / REALM MAP** program. Master **825**; children:
>   **826** parchment Realm Map UI (`realm-map.json`), **827** discovery+travel+ZoneManager identity,
>   **828** cheap live minimap, **829** Withering/biome/content pins. Files `WORK_ORDER_825`â€¦`829_*.md`, READY.
> - **824** = CoC+WC3 **PLAYER ENJOYMENT** master program: PO fun bar + binding ship Waves 0â€“6
>   (817 glance â†’ 822 teach â†’ 774 deploy â†’ 809 readiness â†’ 800/805/821/799 â†’ 806/807 â†’ stakes/spice).
>   Gap fills: soft first-raid ruling, Work empty-state teach, hub truth pass. Does NOT re-implement
>   children. File `WORK_ORDER_824_coc_wc3_player_enjoyment_program.md`, READY.
> - **823** = Post-review HARDENING pack: `ArmyReadiness.Compute` single source (rewire 820 Publish+Open),
>   founding Echo soft-deadline, over-queue/readiness EditMode oracles, 819/820 RESULT hygiene.
>   Does NOT own teach/KayKit/queue visual/perks (822/818/817/821). File
>   `WORK_ORDER_823_post_review_hardening_pack.md`, READY â€” **implement 823 Phase A before 822**.
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
>   phases 0â€“6). Folds 798/801/816. Engine frozen. File
>   `WorkOrders/WORK_ORDER_817_coc_wc3_queue_visual_system.md`, READY.
> - **816** = Queue timer bars (= 817 Phase 2). File `WORK_ORDER_816_*`.
> - **813** = Barracks discovery/teach (B+C). **Depends on 812.** File `WORK_ORDER_813_*`.
> - **812** = **ADD Barracks placeable** (catalog + free first place + train entry). Authority for presence.
>   File `WORK_ORDER_812_introduce_barracks.md`. âš  **NUMBER COLLISION:** Claude also wrote
>   `WORK_ORDER_812_echo_harvest_choice_and_affinity.md` â€” **renumber that to 815** before implement;
>   barracks introduce keeps 812.
> - **814** = gear max-level ability (Claude). File `WORK_ORDER_814_gear_max_level_ability.md` â€” triage later.
> - **811** = Echo gather wood/iron/food OR repair. **810** = Rumor Board layout.
> - **Program hub:** `docs/WC3_COC_EXPERIENCE_ANALYSIS.md` â€” includes Â§2A army ladder (unlockâ†’trainâ†’troop Lâ†’gearâ†’readiness).
> - **806** = Barracks progression spine UX. **807** = troop power readability. **808** = hero gear levels
>   (**Option A LOCKED**). **809** = war readiness score. Files `WORK_ORDER_806`â€“`809_*`.
> - **800** = building focus card unify. **801** = queue glance implement (blocked on 798). **802â€“805** raid/build as prior banner.
> - **798/799/774** still in program hub (queue design, cancel engine, raid P0).
> - **799** = queue CANCEL verb + REFUND plumbing (engine). Panel-row cancel UI waits on 798/801 chips.
>   File `WorkOrders/WORK_ORDER_799_queue_cancel_refund_engine.md`, READY.
> - **798** = WC3 queue VISUAL design (Claude read-only; build on live Builders chip + 5-deep rows).
>   File `WorkOrders/WORK_ORDER_798_wc3_style_queue_visual.md`, READY FOR UI SEAT. Pack: `docs/UI/WO-798_wc3_queue/`.
> - **774** = raid loadout + deploy ring + Army/Deploy naming (CoC P0) â€” referenced by program hub, already READY.
> - **786â€“794** = specs on disk (star reveal, WOs 787â€“792 ticket batch, 793 tree-quest NPC, 794 upgrade verb).
> - **795** = no-stacked-screens scroll standard (owner F8 seq 466 + full 16-panel headed audit).
> - **796** = Room-Forge dungeons bake a REAL hero body (capsule/pill F8; NOT the 782 standee item).
> - **797** = dungeon rooms own their enemies (per-area seating + confinement; entrance-cluster F8).
> - **782** = RESERVED, no file yet â€” the night-wrap of 2026-07-26 (`docs/qa/NIGHT_WRAP_2026-07-26.md`) already
>   claimed 782 for the **capsule NPC/boss standee** item (re-source `DungeonSceneBuilder` from tracked
>   `Resources/Enemies` + `Resources/NPCs`, re-bake `Dungeon_HealersCottage.unity` editor-closed). Held rather
>   than collided with; write the file under 782 when that work starts.
> - **783** = SME-fan-out fix wave â€” **IMPLEMENTED this session.** Raid VICTORY now settles the army
>   (`ReconcileAfterRaid` had ONE caller = retreat; `AddVeterancy` had ZERO, so winning was free); Healer's
>   Cottage made REACHABLE (third `AuthoredPortal` row, south seat â€” the richest dungeon was dev-overlay-only);
>   `[ui-obsidian]` ratchet ARMED + a namespace-qualified regex blind spot closed (it was hiding `OutpostHub.cs`
>   as a false "resolved"); waves.json dead-authoring made LOUD; Echoes-button safe-area inset; FPV headers
>   corrected to match the owner's re-affirmed default-ON ruling. Carries 3 DEFERRED owner rulings (D1 wave
>   authority, D2 the third raid exit via hero death, D3 veterancy pacing).
>   File `WorkOrders/WORK_ORDER_783_sme_findings_fix_wave.md`.
> - **784** = Echo lanes â€” wire the CONSUMERS. Canon said "3 of 4 stub"; code says **all four** are write-only
>   (even Harvest's Core contract has zero readers â€” `EchoService.RatePerSecond` bypasses `EchoLaneBonuses`).
>   Phase 1 = make the Core contract the single seam + wire Defense (the owner's ruled "easy one": flat +x% to
>   the whole city defensive package) + open the picker to it + fix the founding-echo identity contradiction
>   (3 of 6 souls have an unreachable calling). File `WorkOrders/WORK_ORDER_784_echo_lane_consumers.md`, READY.
> - **785** = VFX runtime-art survivability. **117 of 121** owner-tagged VFX rows point into gitignored packs
>   (Hovl Studio 59, UnityTechnologies 54, Mirza Beig 3, Spells Pack 1); the catalog is tracked but binds them
>   by GUID, so on the laptop / a fresh clone / CI they all dangle and â€” unlike the character packs â€” there is
>   **no runtime fallback at all**. Promote the WIRED set into tracked `Resources/VFX/`, make a missing pack
>   loud, add a resolve oracle. Owner-only creative constraint restated: promote what she tagged, never
>   substitute. File `WorkOrders/WORK_ORDER_785_vfx_runtime_art_survivability.md`, READY.
> - **786** = Raid End: punchy star reveal + audio â€” **OWNER-AUTHORED spec**, transcribed verbatim.
>   0.45s dramatic hold -> stars slam in left-to-right (0 -> 1.3 -> 1.0, ease-out, screen shake heavier
>   on the 3rd) -> 3-star-only premium layer (gold flash + radial pulse + a **FLAWLESS** stamp) -> 0.4s
>   appreciation beat -> normal victory panel. Total added time <= 2.1s, 60fps on Seeker.
>   **Owner ruled 2026-07-30: ADD DOTween** (`com.demigiant.dotween` via the OpenUPM scoped registry
>   already in `Packages/manifest.json` â€” headless-doable, no Asset Store download; lands as its own
>   isolated change with an IL2CPP/stripping verification, never folded into another batch).
>   Depends on the star rules, which SHIPPED the same day (WO-783 D3) â€” note 3 stars is now genuinely
>   rare, so **FLAWLESS** actually means something (under the old formula every victory scored 3).
>   File `WorkOrders/WORK_ORDER_786_raid_star_reveal.md`, READY.
> - **787** = Web-build Sign In surface correctness (owner felt-report 2026-07-30, screenshot). THREE bundled
>   fixes on the one reported symptom: (a) LoginPanelController lays a full-height 0.04â€“0.97 fraction layout
>   inside `chrome.layout.body`, which WO-714 P6's close-band reservation compresses (body.y raised up to 0.45)
>   â†’ the intro + 2 fields + 4 buttons overlap ("stacked"); the panel HIDES its Close so it should lay on the
>   full-rect `chrome.content` instead. (b) "Sign in with Google" is APK-only â€” hide `_google` off Android.
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
> - **789** = Wave 5 boss swap â€” replace the TEST-ONLY apex dragon Syndrath (HP 4200) with a lower
>   ground boss **Cave Troll (`troll`)** pinned to **1050 HP** (1/4 of the dragon). Owner felt-report
>   2026-07-30 + owner boss choice. waves.json `waveId:5` carries a self-labelled "TEST ONLY â€¦ REVERT
>   before ship" `apexBoss` block; delete it, add `boss` + a wave-level HP override to 1050 (ground
>   `boss` field has no hp override today â€” add one mirroring `apexBoss.hp`, OR a `troll`@1050 boss
>   variant). Wave 20 keeps the real Syndrath â€” DO NOT TOUCH. Edit BOTH Resources + StreamingAssets
>   copies. File `WorkOrders/WORK_ORDER_789_wave5_boss_swap.md`, READY. Lane 2 Combat/AI (data).
> - **790** = Outpost/garrison enemy PRESENTATION broken (owner felt-report 2026-07-30, screenshots):
>   flat GREEN/ORANGE textureless enemies + weapon not seated. Green = EnemyFactory designed tint
>   fallback (`EnemyFactory.cs:216-253`) firing because Orc-family meshes ship no albedo; orange =
>   capsule fallback (`:669-677`) on a failed mesh load. Weapon-not-seated = `AttachmentOffsetRegistry`
>   grip untuned + `ff.enemyweapons` gate (`EnemyFactory.cs:185-198,557-576`). Lane 9 VFX/Art.
>   File `WorkOrders/WORK_ORDER_790_outpost_enemy_presentation.md`, READY.
> - **791** = Outpost/garrison enemy AI/placement broken: spawns OFF NavMesh â†’ never moves/chases AND
>   floats above ground (no snap). `EnemyFactory.cs:45-54,318-324` (off-mesh spawn only reported),
>   `Enemy.cs:1000-1008` (DriveNav gated on isOnNavMesh), `EnemyOutpost.cs:565-595` (SnapToNav no-op),
>   OuterWorld navmesh coverage under the outpost anchor (`RaidOutpostSystem.cs:185-217`). Lane 2/5
>   Combat/World. File `WorkOrders/WORK_ORDER_791_outpost_enemy_offnavmesh.md`, READY.
> - **792** = Enemy attacks deal ZERO damage to the hero (owner felt-report 2026-07-30). Enemyâ†’
>   HeroHealth melee path; candidate-level RCA (needs a headless proving read: is HeroHealth.TakeDamage
>   never called, or called with 0?). Lane 2 Combat. File `WorkOrders/WORK_ORDER_792_enemy_zero_damage_to_hero.md`, READY.
> - Granary dungeon "does not work â†’ pops to outpost/arena" = **already ticketed WO-776** (gate the
>   contentless Folk's Granary stub) â€” NOT YET APPLIED; the stub's invisible `DungeonStubEncounter`
>   insta-warps to `BattleArena`. See `WorkOrders/WORK_ORDER_775_777_dungeon_debt_program.md` + gaps
>   P1-D/P1-H (`docs/qa/GAMEPLAY_GAPS_2026-07-26.md:48,52`).
> Mint the next new WO from THIS line's next-free = **793**; bump it in the same edit.

> ## âš  RECONCILED 2026-07-26 (Sunday housekeeping, on `wip`): next free WO = **782**. **761â€“781 CONSUMED.** (**779** = UI spacing/layout conformance sweep (OWNER-requested) â€” kill the overlap/clip/truncation class (Echo-flavor-flood, pet-roster-stack, queue clip): layout.body discipline + touch/contrast + ratchet oracle; run AFTER 778; **780** = FTUE first-tower affordability â€” prepaidTower/crystal grant so the taught build doesn't stall; **781** = wire ArmyStorage.TickRecovery â€” wounded troops never heal, borderline P0 (renumbered from 779). Files `WorkOrders/WORK_ORDER_779/780/781_*.md`, READY.) (**778** = Queue UX completion â€” kind-labels+target identity, HUD reachability (P0-A), layout.body/scroll, Barracks Train strip, Trainâ†’EnqueueTraining flip, sell-time buttons (P0-B); file `WorkOrders/WORK_ORDER_778_queue_ux_completion.md`, READY.) (**775/776/777** = dungeon-debt program â€” 775 hero-vitals-from-HeroHealth/HeroAbilities (770.10a), 776 Folk's-Granary gate-off-reachable (770.6), 777 door-consolidation + kill walk-by auto-teleport (770.5); one file `WorkOrders/WORK_ORDER_775_777_dungeon_debt_program.md`, READY.) 761 (structure fire) + 762 (builder queue) + 763 (Wisdom earned) + 764 (hub Y-height) + 765 (capture Default Town) + 766 (Seeker wallet) + 767 (texture caps) + 768 (thin-client migration) + 769 (Firebase auth) all have `WorkOrders/WORK_ORDER_76*.md` files. **770 (dungeon functional loop), 771 (COC Teleport/Deploy raid system, v2), 772 (shared enemy system â€” classes/families/armor/weapons), 773 (common Obsidian job queue)** are firmed specs in `docs/qa/WORK_ORDER_77*.md` (validation-signed-off, `docs/qa/dungeon-raid-validation-2026-07-26.md`). These four use **decimal sub-orders** (770.1â€“770.11, 771.0â€“771.14) â€” sub-tasks, not new WO numbers. **Status (refreshed 2026-07-26):** 770.1/.2/.3/.3b/.4/.7/.9 DONE; **773 SHIPPED** (multi-channel queue, save v35); **771.9 DONE** (barracks progression, in bank); **772 Phase 1 UNBLOCKED** (Hollow Ones APPROVED per PAIN_POINTS Â§1.1; Wildlands DEFERRED) â€” EnemyResolver DONE/wired; 770.5/.6/.8/.10/.11 (=775/776/777 + others) BACKLOG; 774 raid felt-slice SPEC. See `docs/qa/SUNDAY_STATUS_2026-07-26.md`. **774** (raid V1 felt-slice: loadout handoff + deploy ring + Army/Deploy naming) minted 2026-07-26 from the Grok CoC systems review â€” SPEC READY, sequenced AFTER WO-771.9 integration + barracks-catalog-structure; file `WorkOrders/WORK_ORDER_774_raid_loadout_deployring_naming.md`. Mint the next new WO from THIS line's next-free = **775**; bump it in the same edit.
> ## âš  RECONCILED 2026-07-24 (CLI, on `wip`): next free WO = **761** (SUPERSEDED â€” 761â€“773 consumed, see 07-26 banner above). **755â€“759 CONSUMED** (the 07-19 banner's "755" was STALE â€” 755/756 + 757 dragon-breath + 758 particle-VFX-mental-model + 759 wire-manual-picks were all minted since without a banner bump). New this session: **760** = Syndrath dragon â€” complete the licensed-asset swap (Assets/Dragon 71047; delete RedDragon 1.2; git-rm old CC-BY-NC) + fly-inâ†’landâ†’burn-towersâ†’retarget-Tree behavior; file `WorkOrders/WORK_ORDER_760_dragon_syndrath_fly_land_burn_tree.md`, READY (owner-requested 2026-07-24). Mint from THIS line's next-free = **761**.
> ## âš  RECONCILED 2026-07-19 EVENING (CLI, on `wip`): next free WO = **755**. (**754** = VFX Caster Particle Pack multi-layer preview fix â€” IMPLEMENTED). **739â€“753 all CONSUMED.** (felt-test fix wave). New this evening:
> - **750** = Right ActionBar naming + Warden's Grace redesign â€” **SPEC, READY** (blocked on 2 clip IDs); Attack + Q/W/E/R named skills (Sword Wielding/Sword Heroic/Shield Charge/Warden's Grace/Radiant Strike), mobile HUD shows NO key-letters. File `WORK_ORDER_750_right_actionbar_naming_and_warden_grace_redesign.md`.
> - **751** = Y-height normalization â€” **IMPLEMENTED** this wave: default 4m + tower 7m override + siege 3m override + Y-height audit tool. File `WORK_ORDER_751_y_height_normalization.md`.
> - **752** = Echo founding-card overhaul + post-tutorial interjection â€” **SPEC + creative sign-off** (awaiting owner copy). Echo = essence of a person the tree guards; 6 named people (Aldwin/Elowen/Corvin/Bran/Doran/Maren). File `WORK_ORDER_752_echo_founding_card_and_post_tutorial_interjection.md`.
> - **753** = Destructible lifecycle â€” **IN PROGRESS** (CLI committing): destroyed items = no rebuild + full-cost + VFX cleanup via a new `Destructible` component. Spec file pending on disk.
> (The 07-18d banner below said next-free = 752; it is SUPERSEDED â€” 752 + 753 were minted this evening.)
>
> ## âš  RECONCILED 2026-07-18d (CLI, on `wip`): next free WO = **752** (2026-07-19; 748/749 DONE + RESULT-filed; 750 = Right ActionBar naming/Warden's Grace redesign SPEC; 751 = Y-height normalization IMPLEMENTED â€” default 4m + tower 7m override + audit tool). **739â€“748 all CONSUMED.** (**748** = Founding choice "Default Town" vs "Build Your Own" â€” resurrect the pre-WO-695 `ff.strategicplacement`-OFF prebuilt city as an onboarding choice; apply the prebuilt town as movable `BaseLayout` records (StrategicPlacementMigration.BakedRows + CastleHubBuilder ring pos), NOT the old locked bakes; new `FoundingChoiceController` after PetSelect; FTUE auto-satisfies via TryAutoCompleteAlreadyBuilt; granted (no cost). Movability CONFIRMED for the 8 catalogued buildings. Risks: merged-world coord mismatch, lumbermill-vs-lumberyard id, uncatalogued stations. SPEC file `WorkOrders/WORK_ORDER_748_default_town_choice.md`, READY. Owner-requested 2026-07-18.)
>
> ## âš  RECONCILED 2026-07-18c (CLI, on `wip`): next free WO = **748**. **739â€“747 all CONSUMED.** (**747** = Gear curation -> runtime, "Option A" (architect-ruled 07-18): the Gear Caster curates the FULL StreamingAssets gear library (owner's 65 blink weapons `included` in `GearCurationPicks.json`) but runtime loads the Resources copy FIRST (34 wpn / 20 armor, blink-free) -> the curated set + blink-armor class-defaults NEVER load in-game. Fix: NEW `GearCurationExporter` writes Resources = the curated subset (picks.included âˆª code-referenced default ids); `DataWebRegression` made curation-aware for weapons/armor (assert Resources == curated projection, not byte-identity) w/ marker `GEAR_CURATION_OK`; runtime load order unchanged (Resources-first, WebGL-safe); keeps blink per owner "consistency" call. Owner action: curate armor in the Caster. In implementation. See `GAP_AUDIT_2026-07-18.md`.)
>
> ## âš  RECONCILED 2026-07-18b (CLI, on `wip` â€” the branch that wins; UI seat flagged the working-tree copy flip-flopping to "739" via branch merges, so this is the authoritative record): next free WO = **747**. **739â€“746 all CONSUMED.** (**746** = Build-Mode/FTUE placement tickets BM-1/2/3 â€” BM-1 PLACE-return-to-shop (wiring: success path never calls `BuildPaletteUI.Expand()`), BM-2 Echo-Hollow singleton palette gate (hoist `SingletonAlreadyBuilt`, render "Built" state), BM-3 wrong-spotlight-glow (Â§12 capture-first: UiSpotlight highlightId + resolved target + card-registration ids to split the 2 suspects); file `WORK_ORDER_746_buildmode_placement_tutorial_tickets.md`, READY.) (**744** = strict-MVVM whole-game UI migration â€” spec `docs/UI_MVVM_MIGRATION_PLAN.md`; conformance-oracle ratchet `UiMvvmConformanceRegression` + **6 of 7 silos landed** on `wip` (B/C/D/E/F/G-safe), ~33 views on VMs, oracle debt 28â†’16, all Â§2c-tested; BattleHud+Dialogue landmines pending; CLI-minted 07-18 â€” a concurrent UI banner edit had dropped this line, re-recorded here.) (**745** = Room Forge regression oracle + FlowTrace instrumentation â€” UI-seat mint, file `WORK_ORDER_745_room_forge_regression_flowtrace.md`, READY; folds into the 740-743 Room Forge program close.) (**740â€“743** = Room Forge into mainline PROGRAM â€” `WorkOrders/WORK_ORDER_PROGRAM_740_743_room_forge_into_mainline.md`, source branch `feat/room-forge-dungeon-baker`: **740** Room Forge + DungeonBaker socketed room pipeline (scaffold LANDED on branch; sockets Door/Arch/StairUp/StairDown, JSON compose layouts, door-touch-door bake gate, seal-unmated, NavMesh bake; file `WORK_ORDER_740_room_forge_dungeon_baker.md`) Â· **741** default rooms + materials smoke Â· **742** bake demo layout smoke Â· **743** canon/README/RESULT close [741â€“743 spec files still to be written â€” program table names them but they are not on disk yet].) (**739** = Generic Obsidian building-upgrade tier panel (Enhancement Path) â€” ONE data-driven panel for all 6 building ids, tier trees from `docs/design/BUILDING_UPGRADE_TREES.md` rev 2, owner-pinned VM binding map, adds `costIron`, mobile-first NO hotkeys; mockup `docs/UI_Mockups/building_upgrade_obsidian_template.html`; file `WORK_ORDER_739_generic_obsidian_upgrade_tier_panel.md` â€” READY TO IMPLEMENT. NOTE: a 2026-07-17b banner bump recording this mint was overwritten by a later edit â€” re-recorded here.)
> ## âš  REFRESHED 2026-07-17: (superseded â€” was next free WO = **739**). (**738** = Echo per-echo agency + specialization â€” the post-pivot Path-B model: 6 collectible spirits with element/level/assigned-lane, passive lane bonuses (Harvest/Crafting live; Defense = offline city-raid only, never a real fight; Exploration = dungeons only), reconciled onto EchoRosterCatalog/EchoAssignments/EchoService + echoes-balance.json, save v32â†’33; file `WORK_ORDER_738_echo_per_echo_agency_specialization.md` â€” SPEC, awaiting owner pins.)
> ## âš  REFRESHED 2026-07-16c: (superseded â€” was next free WO = **738**) (**737** = Barracks Train panel proper Obsidian layout â€” zone map, lock/select/CTA states, SME refs `UI_BLINK_TEMPLATE_CANON` + Grok-02 + inventory locked cells; file `WORK_ORDER_737_barracks_train_obsidian_layout.md`) (**732â€“736** = Barracks troop roster + tier unlocks â€” program `WorkOrders/WORK_ORDER_PROGRAM_732_736_barracks_troop_roster.md`: **732** data Â· **733** unlock gate Â· **734** tier copy Â· **735** visuals Â· **736** regression) (**723â€“731** = CoC Arena + Barracks â†’ AI camps â†’ async PvP â€” `WORK_ORDER_PROGRAM_723_731_coc_arena_barracks.md`) (prior 2026-07-16: next free was **732**; **722** = Obsidian expansion tail ï¿½ **721** = HUD vitals fill contract ï¿½ **720** = founding-critical Obsidian FIX ï¿½ **719** = dedicated Build HUD CoC ï¿½ **718** = kit-law regression ï¿½ **717** = unstyled-class kill ï¿½ **716** = capture/pair-walk gate ï¿½ Grok-03 program ï¿½ **715** = Hovl towers/melee/spell combat VFX proper wire ï¿½ READY) (**714** = Obsidian conformance PROGRAM â€” pack styling across ALL screens, 3 phases (kit primitives â†’ per-screen lanes â†’ image-pair sweep gate), UI-seat mint 07-13 READY Â· **713** = inventory panel Obsidian conformance + hero render window + consumable hot-swap belt, UI-seat mint 07-13 READY [the UI-seat file briefly minted as 710 is renumbered â€” its 710 file is now a pointer stub] Â· **712** = courtyard navmesh island diag, fleet-captured Â· **711** = HealersCottage content dressing, torch-teacher NPC (owner live walk) Â· **710** = phased founding, staged palette reveal / chunk-selectable Â· **709** = echo workforce global multiplier + workforce HUD panel â€” UI-seat mint 07-13, spec awaiting owner pins 1â€“4 Â· **708** = wall builder drag-lines, base-creation completer Â· **707** = town catalog grooming, one-building-per-trade + pallet stores Â· **706** = build-palette portraits complete set, UI-seat art Â· **705** = onboarding duplicate-UIDocument RCA, fleet-captured) (**704** = trailer fullscreen/aspect, ticket VID-1) (**703** = blank-start residual standdown, ticket BLANK-1) (**699** = hero-select ability chips SEL-1 Â· **700** = Android APK/Seeker test [UI-seat mint, correctly took next-free] Â· **701** = Mending Echoes offline repair [renumbered from colliding fresh 686] Â· **702** = Founding of Elarion FTUE [renumbered from colliding fresh 699]) (688â€“695 consumed by the collision renumber below; **696** = repair-before-upgrade context, renumbered from a colliding fresh 684 mint on 07-13; **697** = currency compact-format + icon chips, ticket RES-1; **698** = encounter budget + scouting, renumbered from a colliding fresh 685 mint). Prior refresh 07-12: 685/686/687 = web-trace lifecycle trio (685 retention/TTL cron, 686 ingestion hardening, 687 read/triage surface); 684 = outstanding-items board. Disk max (program) = **737** (CoC 723â€“731 + roster 732â€“736 + Obsidian train layout 737).
> âš  **UI-SEAT SYNC NOTE (2026-07-13):** the spec-writer seat is minting in the pre-renumber 674â€“685 space â€” every fresh mint there collides. UI-seat numbers translate: its 682=695 (strategic placement) Â· its 683=693 (jeweler) Â· its 684=696 (repair context) Â· its 685=698 (encounter budget). Point the UI seat at THIS banner before its next mint.
> The per-lane detail below is **FROZEN HISTORY** (pre-430 era â€” ~270 numbers stale); do **NOT** mint from it.
> **Collisions RESOLVED 2026-07-13** (677â€“681 each had two specs on disk; the 07-12 evening-arc/ticket-board side kept its number, the other spec was renumbered â€” files renamed + headers updated):
> - 677 kept `mobile_buildmode_move_unreachable` (spec + RESULT) Â· `asset_caster_toolkit_family` â†’ **688**
> - 678 kept `pi_sdk_timeout_clean_wrap` (spec + RESULT) Â· `hovl_vfx_fidelity` (spec + RESULT) â†’ **689**
> - 679 kept `crystal_economy_faucets` (owner ask 07-12) Â· `swordshield_full_wiring` (Grok package) â†’ **690**
> - 680 kept `enhancement_tier_gate_legibility` (ticket UPG-1) Â· `blink_orcs_activation` (Grok package) â†’ **691**
> - 681 kept `echo_select_intro_and_assign` (ticket ECHO-1) Â· `blink_icons_import` (Grok package) â†’ **692**
> - 683 kept `build_screen_dpad` (spec + RESULT, ticket closed by PO 07-13) Â· `jeweler_crafting_mobile_readability` â†’ **693** (untracked â€” stage when claimed)
> - 685 kept `webtrace_retention_ttl_cron` (banner-canonical, board row Done 07-13) Â· `webtrace_lifecycle` â†’ **694** (untracked â€” stage when claimed)
> - 682 kept `web_quiet_error_surface` (spec + RESULT, board row Done 07-13) Â· `strategic_placement_lock_on` â†’ **695** (untracked â€” stage when claimed; implementation agent pre-briefed on the rename)
> Rule stands: **mint from THIS banner's next-free, bump it in the same edit.**

Branch **feat/tower-core-loop**. Numbers only, run order. `â†’` = serial (same lane, in order);
commas = parallel-safe. Detail in `MASTER_PIPELINES_BACKLOG_2026-06-06.md`. New WOs â‰¥290 are spec'd by
this session's design docs (see "Newly minted" below) â€” full WO files on request.
**Numbering authority = the master doc + this file, NOT the filesystem max. Next free WO = 430**
(287/288/306â€“343 used, 289 free, 290â€“305 minted, 339â€“343 refill; **352â€“390 minted out-of-band by the
2026-06-08/09 sessions** and slotted on 2026-06-10; **344â€“351 skipped â€” treat as used/reserved,
do NOT mint** per CLAUDE.md; **391â€“411 minted on-board (Notion) by the 2026-06-10/11 owner/CLI
sessions** â€” specs live in the Notion rows, only WO-405 has a repo spec file â€” slotted on 2026-06-11;
**412â€“428 minted on-board (Notion) by the 2026-06-11/12 owner sessions** â€” playtest bug sweep, slotted
below on 2026-06-12; **429** = the repo "store stock from DB" spec renumbered from a colliding WO-414).
âš  **Number collisions (clean up via Lane 0 dedupe item):** repo has duplicate WO files for 329/330/331/333/334
(and legacy 43/46/106â€“111/129/136â€“138/152/159/179/181/253â€“257/279/280/282/301); Notion board also carries a
*different* 328â€“339 P0-bug block (06-08 session) vs this file's 328â€“339. Do not reuse any of these numbers.
**Live board (Notion mirror):** https://app.notion.com/p/f3115f05ecf940cf8968bd82bbbdff9f â€” see `NOTION_SOURCE_OF_TRUTH.md`.

---

## Newly minted this session (â‰¥290 â€” keeps lanes full)

- **290** QuestService + quest tracker UI (backbone for all questlines) â€” *foundational, do early*
- **291** Vendor Yarn pack (9 NPCs) + NPCCommandBridge quest verbs (StartQuest/Advance/Complete/GiveKeystone)
- **292** Keystone â†’ Spire finale wiring (â‰¥6 Keystones â†’ Spire Defense â†’ Necromancer)
- **293** Crafting tiers (Common/Fine/Master/Legendary) + legendary recipe system
- **294** Forgemasters' Saga: 4 deep crafter Yarn files + 3 reconciliation scenes
- **295** Legendary set "Aegis of Elarion" items + Oathweld ward effect
- **296** Reforge choice (Heart vs cleansed regions) â†’ finale/ending wiring
- **297** Pet acquisition + slots (tame / egg-hatch / rescue)
- **298** Pet skill catalog content + balance (4 branches + signatures)
- **299** Pet bond questlines (Fenn "Wild Hearts" + per-species)
- **300** Elarion weaponsmithing lore integration (item flavor, maker's marks, appraisal)
- **301** Party persistence â€” wallet-keyed roster in GameState (+ migrate pet PlayerPrefs blob)
- **302** Floating health-bar oversize fix (green-pill host-scale)
- **303** Combat party HUD wire-to-live-data (HUDManager)
- **304** Brom's rumor board (quest-board UI; can fold into 290)
- **305** Relic-recovery quests (Dawnedge / garrison blades / pattern-blade)
- **339** SaveSchema: add quest state versioning + migration stub (anchor for all quest WOs)
- **340** PlayerPrefs migration: legacy pet/party data â†’ GameState on load
- **341** Backend: auth token refresh + expiry handling
- **342** WebGL: memory optimization + GC pressure reduction
- **343** Analytics: event batching + periodic backend flush

## Out-of-band block 352â€“390 (minted 2026-06-08/09 sessions; slotted 2026-06-10)

- **352â€“353, 355â€“357** build-mode/UI panels (preview, palette filters, portrait layout, placement validation, touch) â€” L4
- **354** upgrade tier display + synergy â€” L11
- **358** âœ“DONE Yarn welcome Â· **359** combat feedback â€” L9 Â· **360** companion echo outpost â€” L12
- **361** wave rewards/passive XP â€” L6 Â· **362** enemy wave composition â€” L2 Â· **363** orientation validation gate â€” L0
- **364** companion gear â€” L12 Â· **365â€“367** idle poses/routines + town camera â€” L10 Â· **368** âœ“DONE camera regression
- **369** arena monument â€” L1 Â· **370â€“372** monument VFX, combat SFX, battle music â€” L9
- **373** critical regression gates â€” L0 Â· **374â€“378** UI fixes (char select, Yarn threading=375, hero pose, dialogue block, town HUD) â€” L4
- **379** echo auto-summon â€” L12 Â· **380** âœ“DONE gear icon Â· **381** ATB arena cleanup â€” L2 Â· **382** âœ“DONE hero HP
- **383** castleâ†”outerworld seam â€” L5 Â· **384** castle stairs â€” L1 (CastleHubBuilder single-writer) Â· **385** castle camera (fade landed, pending playtest) â€” L4
- **386** battle visualization (understand-phase done) â€” L2/L4 Â· **387** âœ“DONE camera-relative movement
- **388** player castle as arena defender (SPEC) â€” L2 Â· **389** arena mode attack/defend (partial built) â€” L2/L6/L4 Â· **390** battle potion loadout (SPEC, after 389) â€” L6/L2

## Out-of-band block 391â€“411 (minted on-board 2026-06-10/11 owner/CLI sessions; slotted 2026-06-11)

Newly minted: specs live in the Notion rows (only **405** has a repo `WORK_ORDER_*.md` file â€” backfill the rest when claimed).
âš  **HUD/UI gate:** 400/403/404/411 are **Blocked on WO-405** (UGUI design-system owner-approval gate) â€” do not pick up until 405 is Done.

- **392** Warcraft-style tiered building upgrades (Lumbermill/Forge/Armorer) â€” L11 Â· **407** Arcane Tower tiers (extends 392) â€” L11
- **393** low-contrast yellow text on building-upgrade UI â€” L4 Â· **394** build click gives no feedback (surface block reason) â€” L11
- **395** resource node / mine interaction visual replacement (asset-library audit first) â€” L5
- **398** Knight still dealing ranged damage (melee-only) â€” L2 Â· **399** Knight melee weapon skill set â€” L3
- **400** Inventory rework to mockup (after 403) â€” L4 Â· **401** Blacksmith vendor presentation (signboard + Yarn-only) â€” L12
- **403** UNIFIED context HUD shell + TOWN mode (RESPEC; rebuild, don't patch; needs 405âœ“kit) â€” L4
- **404** combat HUD group (same canvas as 403, waits on 403) â€” L4
- **405** âœ“kit-approved â€” UGUI design system `ElarionUiKit` (P0, blocks all HUD work; repo file exists) â€” L4
- **406** shops empty â€” vendor inventories not populated â€” L6 Â· **408** WebGL texture optimization 223â†’<60 MB (scripts committed, NOT run) â€” L10
- **409** magenta tower materials (Standardâ†’URP swap, NOT broad fixer) + UI sprite `*`/`#` glyphs â€” L0
- **411** Town HUD â‰  `hud_mobile_town.png` mockup (10 deviations; blocked on 405, folds into 403 path) â€” L4
- **391 / 396 / 397 / 402** â€” used on-board (rows not mirrored here yet â€” see the Notion board for titles) â†’ do NOT mint
- **410** â˜…P0 â€” 0.1 fps in MainCastle_Hall: main-thread GC storm + combat-object leak â€” L10 (title mirrored 2026-06-12)

## Out-of-band block 412â€“429 (minted on-board 2026-06-11/12 owner sessions; slotted 2026-06-12)

Owner playtest bug sweep + vendor/storefront chain. Specs live in the Notion rows (repo spec files exist
only for **413** and **429**; backfill others when claimed).
âš  **Collision resolved 2026-06-12:** the repo file `WORK_ORDER_414_store_stock_from_db.md` collided with
Notion's WO-414 (TALK-glow black disc). Repo spec **renumbered â†’ WO-429** (`WORK_ORDER_429_store_stock_from_db.md`);
the old 414 file is marked SUPERSEDED. Notion's 414 stands.

- **412** â— Vendor Wares BUY tab empty (layout fix `ca89d9b` landed; build-test + catalog-load open) â€” L6
- **413** upgradable vs shoppable building menus (data-driven; repo spec exists) â€” L6
- **414** black circle under TALK button (AttentionGlowUi first-frame) â€” L4
- **415** vendor storefront UI from Tech hud elements pack (after 412) â€” L4 Â· **416** hide "Talk:" world prompt (supersedes 411 #9) â€” L4
- **417** â˜…DO FIRST: Settings/Dev Tools rows blank (owner's test harness) â€” L4 Â· **421** battle HUD skill bar empty â€” L4 Â· **428** hero damage not shown on HUD â€” L4
- **418** castleâ†’OuterWorld hard-pop blend â€” L5 Â· **426** enable node/outpost claim loop â€” L5
- **419** enemies don't attack after castleâ†’OuterWorld â€” L2 Â· **423** hero attacks without facing target â€” L2
- **422** Echo Warden pet-selection gate + unlock quest â€” L12
- **424** harvested resources not in HUD count â€” L6 Â· **425** hero spawns unarmed (default weapon) â€” L6
- **420 / 427** â€” used on-board (titles not mirrored â€” see the Notion board) â†’ do NOT mint
- **429** store stock served from Neon DB (StoreService + offline-first fallback; repo spec exists) â€” L7

---

## Lanes (topped up)

**Lane 0 â€” Verify/build now:** 283âœ“, 284âœ“, 285âœ“, 286âœ“, 107, 108âœ“, 109, 110, 111, 329(regression suite), 302, 303, 363(orientation gate), 373(regression gates), 409(magenta towers + UI glyphs)  ~~328 CLOSED (ambiguous/no repro)~~
**Lane 1 â€” World/Env (VillageSceneBuilder = SOLE WRITER, serial):** 253 â†’ 166âœ“ â†’ 167 â†’ 168 â†’ 157 â†’ 137, then 173, 245, 246, 247, 263, 311, 312, 313, 321, 323, 369(arena monument), 384(castle stairs â€” CastleHubBuilder)
**Lane 2 â€” Combat/AI (parallel):** 254, 255, 135, 145, 146, 147, 155, 128, 287(SPEC), 310, 315, 316, 317, 318, 320, 326, 327, 330(DTT cyan hero), 331(DTT hotkeys), 332(DTT aim sensitivity), 333(village deathâ†’DTT/ATB HIGH), 335(ATB purple capsule bug HIGH) â†’ 336(ATB village wall environment), 362(wave composition), 381(ATB arena cleanup), 386(battle viz, w/ L4), 388(arena defender base SPEC), 389(arena mode, partial), 398(Knight melee-only), 419(enemies passive after transition), 423(rotate-to-target)
**Lane 3 â€” Combat Feel (serial):** 288(in-progress) â†’ 213 â†’ 217 â†’ 218 â†’ 219 â†’ 220, then 295 (legendary set feel), 319 (DTT parity/anim), 399 (Knight melee skill set)
**Lane 4 â€” UI/HUD (parallel):** 307 â†’ 308, 309; 303, 302, 110, 124, 156, 178âœ“, 237, 257, 304, 322, 337(Echo Hollow dialogue overlap HIGH), 338(Echo Hollow rebrand â€” UI strings), 352, 353, 355, 356, 357, 374, 375, 376, 377, 378, 385(castle camera, pending playtest), 380âœ“, 382âœ“; **405âœ“kit â†’ 403 â†’ 404, 400, 411** (unified HUD path â€” P0 chain, 400/403/404/411 blocked on 405 Done), 393, 414(TALK-glow disc), 416(hide Talk prompt), 417(â˜…DO FIRST: Settings/DevTools rows), 421(battle HUD skill bar), 428(hero damage HUD); 415(vendor storefront skin, after 412)
**Lane 5 â€” World/Exploration:** 164 â†’ 153âœ“, 159, 160, 165, 142, 143, 144, 154, 305, 324, 383(castleâ†”outerworld seam), 395(node/mine visual replacement), 418(castleâ†’OW blend), 426(claim loop)
**Lane 6 â€” Economy/Progression:** 228 â†’ 229, 151, 115, 117, 119, 194, 293, 297, 298, 325, 361(wave rewards), 390(potion loadout, after 389), 406(empty vendor shops), 412â—(Vendor Wares BUY empty), 413(upgradable vs shoppable menus), 424(harvestâ†’HUD count), 425(default weapon)
**Lane 7 â€” Persistence/Backend:** 301 â†’ 339 â†’ 340, 341; 120, 80, 129, 121, 118, 429(store stock from DB â€” needs React-repo GET endpoint)
**Lane 8 â€” Monetization/Store:** 72, 73âœ“, 74, 75, 76, 77, 78, 79, 80, 236
**Lane 9 â€” VFX/Audio (parallel):** 256, 264, 272, 195, 170, 171, 66âœ“, 111, 243, 359(combat feedback), 370(monument VFX), 371(combat SFX), 372(battle music)
**Lane 10 â€” Build/Deploy/Perf:** 196 â†’ 211 â†’ 342, 343; 191, 51, 53, 54, 57, 282(HELD), 365, 366, 367, 368âœ“, 408(texture opt â€” scripts committed, not run), 410(â˜…P0 GC storm 0.1fps MainCastle_Hall)
**Lane 11 â€” Build Mode / Player Base:** 108âœ“ â†’ 215, 282, 113, 114, 181, 104, 239, 292, 314, 334(tower placement rotate menu), 354(upgrade synergy), 392(building upgrade tiers) â†’ 407(arcane tower tiers), 394(build-block feedback)
**Lane 12 â€” Narrative/Onboarding/Quests:** 290 â†’ 291 â†’ 304, 230, 222 â†’ 227, 238, 277, 116, 235, 133, 294, 296, 299, 300, 338(Echo Hollow rebrand â€” Yarn + DESIGN-DECISIONS), 358âœ“, 360(echo outpost), 364(companion gear), 379(echo auto-summon), 401(blacksmith presentation), 422(Echo Warden pet gate + unlock quest)

**Hard rules:** ONE agent in Lane 1. `GameState.cs`/`SaveSchema` field-adds (Lanes 5/6/7/11/301/339) additive,
one-at-a-time. **Do early:** 164 (zone), wallet/economy merge, 290 (QuestService), 339 (SaveSchema anchor) â€” many lanes depend on them.
Overlaps: 108 (5/11), 282 (10/11), 80 (7/8), 111 (0/9), 295 (3/6), 340 (7/301).

