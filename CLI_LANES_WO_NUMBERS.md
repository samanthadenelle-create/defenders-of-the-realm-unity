# Lanes — Work-Order Numbers Only (for CLI)  ·  reconciled 2026-06-12 (nightly refill)

> ## ⚠ RECONCILED 2026-08-15 (CLI): main line next free = **1000**. **782–859 + 900–999 CONSUMED.**
> *(CLI bumped 999 -> 1000 in the SAME edit as minting **WO-999**. **999** = class resource economy
> residual — ability-face cost pips + ResourceDisplayName on the bar + ranger Quick Shot Focus +
> owner balance rulings left open by WO-997 (which is DONE implementation; RESULT on disk).
> **997** = class resource system DONE (pools/costs/bar floats/`[class-resource]` oracle).
> **998** CLOSED — SUPERSEDED by UI seat WO-1024 (hub repair surface). Numbers stay consumed.)*
> *(⚠ 994 and 995 were minted as FILES without bumping this banner — the exact mint-without-bump that
> §2 names as the collision cause, committed by the seat that spent the day enforcing it against others.
> Caught and reconciled 2026-08-14 while minting 996. No collision occurred; both files are on disk and
> referenced.)*
> - **996** = **`armor.json`'s two canonical copies are PARTIALLY DISJOINT — each holds content the
>   other lacks, and the class ladders exist in only ONE of them.** MEASURED AT SOURCE 2026-08-14:
>   Resources **v2 / 24 rows**, StreamingAssets **v1 / 30 rows**; **15 Resources-only** (the entire
>   `armor_{knight,mage,ranger}_{common..legendary}` ladder) and **21 StreamingAssets-only** (all
>   `blink_armor_*`). Only **9 rows shared**. ⚠ **THIS IS NOT THE WEAPONS SHAPE** — weapons is a
>   deliberate curated subset from `GearCurationExporter` (Resources-only ids = 0). Armor has no such
>   pipeline and has drifted in **BOTH** directions, so "Resources is a subset" is FALSE here and any
>   oracle written on that assumption is wrong for armor. ⚠ **SHIPPING RISK:** Resources WINS at runtime
>   so the editor looks fine; whatever reads the StreamingAssets fallback gets a roster with **no class
>   armor ladders at all**. Schema versions also differ (v2 vs v1). Found FOUR independent times on
>   2026-08-14 — the 300–599 sweep, WO-544's verification, WO-976's assertion-(d) scoping, and WO-500
>   step 1's new subset oracle. ⛔ **Do NOT "fix" it by copying rows across.** Decide which copy is
>   AUTHORITATIVE and which is DERIVED, then make one generate the other. READY.
> - **995** = dungeon boot self-evicts to town via the exit trigger (see `WorkOrders/WORK_ORDER_995_*.md`).
> - **994** = the shield seat is stranded against the base WO-970 moved (see `WorkOrders/WORK_ORDER_994_*.md`).
> - **993** = **PETS ARE DESCOPED TO HELPERS — retire the pet PHYSICAL-PRESENCE stack (aura + progression),
>   but DO NOT TOUCH `PetHeroLeash`.** OWNER RULING 2026-08-14, verbatim: *"we dont use the pet aura
>   anymore since we descoped them to simply helpers and not physical items around us"*, *"same with pet
>   progression"*, *"auracontroller can be retired"*. Echoes are **systems** (harvest lanes, flat defense
>   %), not companions that stand next to you — so the aura/level-visual surface is dead **by design**,
>   not by accident.
>   **`PetHeroLeash` GOES TOO — owner ruling, same breath: *"pet leash gone too"*.**
>   ⚠ **BUT IT HAS 47 NON-SELF REFERENCES AND IS THE TUTORIAL GUIDE LEAD** (`GuideLeadMovementRegression`,
>   `TutorialFlow.SetLeadTarget` — the WO-962 latch feeds it). The pet plumbing was REPURPOSED into the
>   thing that walks the player through the FTUE. **So removing it is NOT a delete, it is a FTUE change:
>   the guided walk must be given a replacement lead or the step must be removed, IN THE SAME CHANGE.**
>   Deleting the symbol and leaving the tutorial step standing produces a step that silently stops
>   leading — a dead FTUE with a green gate. Retire by SYMBOL, never by folder.
>   Clean to retire (verified 2026-08-14): `AuraController` — 1 non-self ref (`GearAura.cs:8`);
>   `PetAuraVFX` — 1 non-self ref and it is a **comment** (`ParticlePackVfxBatchBuilder.cs:1055`);
>   `PetBrain` — 2 refs, **both inside `AuraController`**, so they go to zero with it.
>   ⚠ Also void: WO-128's acceptance criterion *"WO-58 aura — BUILT — DO NOT BREAK"*, which has been
>   protecting something that **never ran** (`WORK_ORDER_58.RESULT.md:38-43` claims `PetProgression`
>   calls `SetLevel`/`PlayLevelUpBurst`; **neither call exists at HEAD** — a hollow assertion caught on
>   2026-05-30 and never closed). Orphans the `Aura_PetLevel1/2/3` catalog keys and the pet registrant in
>   `VfxAuraProximityCuller`. READY.
> - **992** = **SIX classes ship in every build, compile clean, and are NEVER INSTANTIATED — dead code
>   the legacy-close does NOT remove.** Found by the 2026-08-14 phantom sweep: `WeatherManager`,
>   `TorchFireController`, `AuraController` (WO-52/55/58), `BattlePassManager`, `CryptoPaymentManager`,
>   `CosmeticApplier` (WO-73), plus the WO-87 Cinemachine controller. Each has an honest RESULT file
>   that flagged *"scene wiring = manual editor work"*; **that wiring never happened and nobody noticed
>   for ~2.5 months.** ⚠ **WHATEVER THOSE OLD TICKETS RESOLVE TO, IT DOES NOT TOUCH THE CODE** — closing
>   one removes the only record of why the code is there, so the finding is hoisted here instead. Decide
>   per class: WIRE IT or DELETE IT. **Owner dispositions 2026-08-14:** `WeatherManager` = **KEEP**
>   (*"will play into the zones for the map"*); `TorchFireController` + `AuraController` = **RESEARCH
>   FIRST** (she suspects the latter should target towers/portals, but WO-58's own title is "pet aura
>   system" — expect a divergence); the other three she reads as *"ideas not implementations yet"* —
>   confirm before deleting, and ⚠ do NOT wire `CryptoPaymentManager` without an explicit owner call. ⚠ **METHOD NOTE, load-bearing:** Unity serialises script refs by
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
>   needed. ⚠ **NOT SHIPPED — `healing_caravan` currently carries `behaviorId: HealingFountain`, a
>   static bespoke singleton.** Mobility and the heal field are both design intent today; no doc may
>   claim the caravan moves. ⚠ The retired `HealerTower` case (`StructureFactory.cs:935`, kept by WO-990)
>   is the **worked example of exactly the support-FIELD pattern this needs** — build on it, do not
>   reinvent it, and do not resurrect `tower_healer`. **SPEC — needs design detail before implementation.**
> - **990** = **RETIRE the `tower_healer` catalog row — it has never been buildable — but KEEP the
>   `HealerTower` BEHAVIOUR, which is the reference implementation of the field pattern.** OWNER RULING
>   2026-08-14: *"i do not know what the town healer is"* → *"retire"*. It is unrecognisable because it
>   is **unreachable**: `tower_healer` appears in NO build category (`build-categories.json` lists only
>   `healing_caravan`), and `BuildCardArtRegression.cs:64` says so in a comment —
>   *"legacy Support verb only - not reachable from Town/Def"*. ⚠ **It cost a pin today:** WO-947 spent
>   owner ruling 2 (*"yes AoE healing"*) partly on a building nobody can build, and an agent reported it
>   as *"Support, not locked"* and player-reachable — a claim the data refutes. Third id-over-data
>   misread of the day (see WO-989, arcane-tower).
>   ⛔ **DO NOT DELETE THE BEHAVIOUR.** `StructureFactory.cs:935` `case "HealerTower"` is, per its own
>   header, *"WO-891. The FIRST instance of the general support/offensive FIELD pattern, and the proof of
>   its thesis: a new structure is stats plus TWO TAGS"* — and `:925` holds a commented-out
>   `case "SlowFieldTower":`, the intended next sibling. Deleting it discards the worked example the
>   pattern is meant to be copied from. **Retire the ROW and the menu/catalog surface; keep the code as
>   the documented reference.** READY.
> - **989** = **`tower_wall_wizard` still carries a wizard IDENTITY for a structure the owner renamed to
>   Ballista — rename the id (and prefab path) behind a READ-MIGRATION ALIAS.** OWNER ASK 2026-08-14:
>   *"tower_wall_wizard - Where did that name come from? Should match Ballista"*. Traced: the id dates
>   from the ORIGINAL build-catalog commit `9de2aac56`, where it genuinely was a wizard tower. The owner
>   ruling of **2026-07-08** (quoted in the row's own `orientation.note`) renamed the MODEL to a ballista
>   and the row was retuned to match — `displayName "Ballista"`, `element None`, `projectileStyle "bolt"`.
>   **The display name and the stats were renamed; the IDENTITY never was.** The `id` and
>   `visualPrefabPath: "Structures/WizardTower_1"` are the last two fields still calling it a wizard.
>   ⚠ **THIS IS THE COST OF LEAVING IT:** WO-947 read the row as MAGICAL *from its id* and would have
>   sent **70 crystals in the wrong direction**; it took an owner pin (2026-08-14, *"thats a baliista
>   mechanical"*) to settle a question the data had already answered. A stale identity is not cosmetic —
>   it actively misroutes downstream work, the same way the stale WO-number block and the hardcoded repo
>   root did.
>   ⛔ **NOT A FIND-AND-REPLACE.** The id is referenced in **15 files** AND **catalog ids are PERSISTED**
>   (save schema v36 `everBuiltStructureIds`; base layouts replay by id). A bare rename orphans every
>   saved town holding one. **Use the project's own precedent — the `harvest:3` → `wood:3` token change
>   was READ-MIGRATED with no schema bump.** READY.
> - **988** = **`headed-dungeon-capture.ps1` reports `HEADED_CAPTURE_OK` on a run that loaded the WRONG
>   SCENE with a FROZEN CLOCK.** PROVEN 2026-08-14: a run tagged `wo1007-portal-camera` emitted
>   `HEADED_CAPTURE_OK 10 shots`; the copied `Player.log` from that same run says
>   `scene='Main_Castle_Overworld'` (the TOWN — the `-Scene` parameter is accepted and then never
>   forces a load) and `WORLD CLOCK FROZEN: Time.timeScale=0.00 ... The hero CANNOT move, turn or`.
>   All ten shots are the frozen town; the synthetic WASD landed in an **open bug-report text field**
>   visible in `10_facing_exit.png`. ⚠ **Same class as WO-984** — the harness proves a frame rendered
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
>   ⚠ **THE TWO FACES ARE `Continue to exit` AND `Cancel` — they are NOT two ways forward.** The first
>   reading ("Continue" vs "Exit") would have shipped a dialog offering to keep playing or to leave,
>   which is a different feature and an easy mis-build. Cancel RETURNS THE PLAYER TO THE RUN, unchanged.
>   Today the exit is proximity/prompt-driven; the ruling makes **contact with the portal** the trigger
>   and adds a **two-choice confirm** so a player cannot lose a run by walking into the exit. ⚠ Do NOT
>   reuse the raw violet plate — the interact button is being re-skinned to the Obsidian kit under
>   WO-1005 Part 1 (owner confirmed the purple goes, town included). READY.
> - **986** = **`PlacementGrid.FootprintCells` SQUARES the grid claim, so every THIN structure over-claims
>   on its narrow axis — WO-972 routed around this for walls only.** SURFACED 2026-08-14 while verifying
>   WO-972. `PlacementGrid.cs:235-238` computes ONE scalar and returns `new Vector2Int(cells, cells)`;
>   `StructureFactory.cs:693` collapses the mesh with `Max(b.size.x, b.size.z)`, discarding the 1.42 m
>   depth. Walls were fixed by feeding the claim a different METRIC (authored fp=2.1 -> `Ceil(2.1/3)=1`),
>   **not** by fixing the squaring. So any other row whose mesh overshoots one axis by even 1% still
>   claims a square block on its thin axis. ⚠ **This is an OWNER-SCOPED decision, deliberately NOT slipped
>   into the wall ticket:** a real fix means a non-square `(x,z)` footprint threaded through the grid, the
>   occupancy map, the yaw-inflation path (`:262-264`, `|sin|+|cos|`) **and every saved layout's occupancy
>   replay** — i.e. it touches every placeable structure and existing player saves. Decide whether thin
>   structures other than walls actually hurt in play before paying that. **SPEC — needs an owner call.**
> - **985** = **`DungeonHero.FaceHeading`'s dead `KeeperRelative` branch still applies `ModelYawOffset = 90f`
>   — the THIRD fragment of a matched pair whose other two halves were removed today.** 2026-08-14: the
>   camera's `_headingYawOffset = 90f` existed *solely* to undo `FaceHeading`'s `Euler(0,-90,0)`. Removing
>   only the `-90` left the camera 90° to the side (F8 seq 2328; delta constant 90.0 across 39 heartbeats).
>   Both halves are now zeroed — **but `KeeperRelative` is a third copy of the same offset, currently
>   unreachable.** It is bannered STALE, not deleted, because deleting an unreachable branch destroys the
>   evidence of what the pair used to be. ⚠ The hazard is specific: if anyone re-enables that branch it
>   re-introduces the exact bug against a camera that no longer compensates. **Do NOT "clean it up" and do
>   NOT flip it on to test — decide whether the branch has a future, then either wire it WITH a zeroed
>   offset or remove it in one deliberate edit that names the pairing.** READY.
> - **984** = **The Unity method wrapper judges success by LOG TEXT, not by a MARKER — so a gate that
>   never ran reports exit 0.** PROVEN THREE WAYS on 2026-08-14: (1) `powershell -File
>   tools\run-unity-method.ps1` — a path that **does not exist**, the runner lives at repo root — exited
>   **0**; (2) the same call with the script found but `-LogName` missing exited **1** only because
>   PowerShell's own mandatory-parameter check caught it, not the wrapper; (3) reading `Builds\build.log`
>   instead of the gate's own log showed `COMPILE_GATE_OK : 0` on a tree that was in fact clean. ⚠ **This
>   is the SAME defect class as the 44-row hollow-assertion registry, sitting in the tooling we use to
>   verify everything else** — and it is worse than a hollow trace, because a hollow gate makes every
>   downstream "verified" claim unfounded. The wrapper's own header admits the design (*"judge success
>   from the log (compile errors / exceptions / 'Aborting batchmode')"*) — that was a reasonable choice
>   when markers did not exist; markers exist now and are per-entry-point distinct (`COMPILE_GATE_OK`,
>   `REGRESSION_OK <n>/<n> suites`, `CHECKIN_SUITE_OK`, `SESSION_GUARDS_OK`, `UI_CAPTURE_OK`). **FIX:**
>   require the caller to declare the expected marker, and FAIL when it is absent, when the log is older
>   than the run, or when the log does not exist. Absence of an error is not evidence of success.
>   Acceptance: a deliberately-broken invocation (bad path, bad method name, stale log) must exit
>   NON-ZERO. Today none of those do. READY.
> - **983** = **Ground fog THROUGHOUT the composed dungeons** — owner direction 2026-08-14, verbatim
>   *"THIS THROUGHOUT THE DUNGEON"*, pointing at the Unity Particle Pack demo scene **Ground Fog**
>   (*"slow moving noise + a sprite sheet animation to give the effect of rolling fog"*).
>   **THE KEY IS ALREADY CATALOGUED — map it verbatim, do NOT pick a substitute** (memory
>   `vfx-map-owner-tags-no-creative-pick`): `PP_GroundFog` →
>   `Assets/UnityTechnologies/ParticlePack/EffectExamples/Smoke & Steam Effects/Prefabs/GroundFog.prefab`.
>   ⚠ Do NOT use `Env_GroundFog` — that `VFXType` ordinal is an ORPHAN with **no catalog row and no
>   prefab** (`VFX_AUDIO_WIRING_MAP.md`); it looks like the right name and renders nothing.
>   **Today `PP_GroundFog` has exactly ONE consumer — `DungeonWorldPortalSpawner`, the OVERWORLD portal.
>   Nothing inside a dungeon plays it.** That is the whole gap; the asset and the key both already exist.
>   ⛔ **THE TRAP, AND IT IS THE EXPENSIVE ONE.** That prefab lives in a **GITIGNORED** root
>   (`Assets/UnityTechnologies/`, the 191 MB Particle Pack). Referencing it straight from the bake
>   reproduces the 2026-08-06 P0 verbatim — **27 of 28 tracked VFX prefabs / 183 references pointed into
>   gitignored art** and rendered magenta-or-nothing on every machine without the packs. It works HERE
>   and is invisible until a clone. **Mirror it first** via `VfxResourceArtMirror` into
>   `Assets/Resources/VFX/` (deps included — `CopyAsset` duplicates the PREFAB ONLY), then wire the
>   mirrored copy, then confirm `VFX_ART_MIRROR_OK`.
>   ⚠ Second trap: **fog is a LOOP.** A loop played fire-and-forget permanently consumes one of the
>   **20 global loop slots** and never returns it (the 08-06 loop-cap P0 — after ~20 the archer renders
>   no projectile and the Tree of Life aura starves). Seat one instance per ROOM with a retained handle
>   released on room unload, or declare a finite lifetime so `VFXManager` routes it through the
>   leak-proof oneshot path. **Never one per tick, never per enemy.**
>   Seat it in the bake beside the existing dressing pass (`DungeonDresser.DressRoom`, already seats ~8
>   props/room and is wired into `DungeonBaker` pre-NavMesh), so every composed dungeon gets it and the
>   fleet cannot diverge. Acceptance = a headed capture (`tools/capture/headed-dungeon-capture.ps1`)
>   showing rolling fog in `dg_ember_deep`, **plus** the absence of `SKIPPED - active loops 20/20`
>   across that run. **READY.**
>
> *(banner bumped 983 → 984 in the SAME edit as the mint.)*
> - **982** = **`GraphDungeonComposer` emits the compose-layout to StreamingAssets ONLY — so every bake
>   silently creates dual-copy drift, and Resources (the copy that WINS at runtime) keeps the stale one.**
>   PROVEN BY A BAKE, 2026-08-14: a clean `ComposeAllBatch` run left **all 7** `dg_*.json` layouts drifted,
>   StreamingAssets stamped 09:00 today against Resources still at 08-08/08-10. Synced by hand this time.
>   ⚠ **This is the ROOT of the 2026-08-08 incident that `5f0e23aa` treated as a one-off** — that commit
>   caught `dg_sunken_vault.json` holding the OLD 17-room layout in Resources and fixed the file; the
>   MECHANISM that produced it was never fixed, so it reproduced across all seven the very next time
>   anyone baked. **⚠ And nothing catches it:** `RoomForgeRegression.cs:162`'s dual-copy sweep is a
>   hardcoded 3-file list containing **no `dg_*` layout at all** (audit F24), so the gate is structurally
>   blind to exactly the files the composer writes. FIX = have the emit path write BOTH canonical roots
>   (mirror `CanonicalJson`'s dual-copy law), **and** widen `RoomForgeRegression` to enumerate every
>   `dg_*` layout on disk rather than a literal list — the guard and the writer must land together or the
>   next bake re-opens it. **READY.**
>
> *(banner bumped 982 → 983 in the SAME edit as the mint.)*
> - **981** = **The skill-point grants in `HeroProgression` are INFERRED and silently droppable** —
>   found by the orchestrator while gate-reviewing the WO-977 fix, 2026-08-14. Two sections, one file,
>   one fix session. **§A — the starter latch is not persisted, it is GUESSED FROM LEVEL.**
>   `HeroProgression.cs:202` (`RestoreFromSave`) does `if (_level > 1) _hasGrantedStarterPoints = true;`
>   on the stated assumption *"a restored hero past level 1 already received its first-level-up starter
>   gift in the run that earned the level"* — **which is precisely the assumption WO-977 disproves.** So
>   WO-977's retry-on-next-level-up holds only WITHIN a session: a player whose grant failed at the
>   level-1→2 boundary and then reloads is re-latched at `:202` and loses both points permanently. The
>   durable fix is to persist the latch (a `SaveSchema` bump — mind the CORE_SAVE version-triple oracle,
>   `SaveMigrator` top step must equal `CurrentVersion`) **or** derive it from a measured point count
>   rather than from level. ⚠ **Until this lands, WO-977's new Fail message overstates its guarantee**
>   and the message wording must say "this session". **§B — the per-level grant at `:259` drops a point
>   silently on a null `SkillSystem.Instance`.** It IS `try`-wrapped, so a *throw* is loud, but the `?.`
>   no-op is not: `SkillSystem.Instance` is a plain static self-bootstrapped `AfterSceneLoad` while
>   `HeroProgression.Bootstrap` is `BeforeSceneLoad`, so the null window is real, not theoretical — and
>   this one fires on EVERY level, not once. Same treatment as WO-977: measure the `AvailablePoints`
>   delta, `Fail` naming the consequence when it does not move. **READY.**
>
> *(banner bumped 981 → 982 in the SAME edit as the mint.)*
> - **980** = **Dungeon camera FRAMING after the WO-968 fix — blown-out wall, hero as silhouette.**
>   The camera fix itself is PROVEN (43 heartbeats, 15 distinct rig poses, heal line fired once naming
>   the DESTROYED CinemachineFollow verbatim). This is about what the now-working camera SHOWS:
>   `03_walk_end.png` / `08_final.png` are dominated by a near-white wall with the hero a black
>   silhouette against a torch. **Kept separate from WO-968 deliberately** — *"the camera follows"* and
>   *"the player can see where they are going"* are two claims and only the first is proven; folding
>   them would let a proven fix carry an unproven one. **May well be WORKING AS INTENDED**: the old
>   camera was parked across the room, so this is the first time anyone has seen the intended
>   over-the-shoulder framing. ⛔ **OWNER RULING FIRST, asked as behaviour not colour** (she is
>   red/green colourblind): *can you tell where you are going, or does it read as a bright blur?*
>   Candidate fixes if a defect — torch intensity, bloom/post-exposure, rig distance/height, hero
>   rim-light. ⚠ **Do NOT touch the follow logic or `HealBodyStage`.** Before/after proof pairs in
>   `docs/proof/2026-08-10-dungeon-headed{,-AFTER-camera-fix}/`. **READY (blocked on her ruling).**
> - **979** = **`WaveFeedbackDirector` reports a HUD bind that can never succeed** —
>   `Assets/_Modules/Village/Waves/WaveFeedbackDirector.cs:321` prints
>   `hudBound={CoreServices.Hud != null}` while `FindHud()` at `:325` is a **stub whose entire body is
>   `return null;`**. The bind is ALWAYS null. This is not merely an unfalsifiable trace — **it reports
>   a DIFFERENT VARIABLE than the one it names**, so a reader checking "did the wave HUD bind?" gets an
>   answer about `CoreServices.Hud` instead. Either finish `FindHud` or delete the seam and say so;
>   what must not stand is a stub with a trace that implies it works. **READY.**
> - **978** = **Economy callers echo the amount REQUESTED, not the amount CREDITED** — an entire class,
>   found in the 2026-08-10 hollow-assertion audit: `RaidVictoryController.cs:277`,
>   `DailyQuestRewardBridge.cs:126`, `ChallengeOutpostVictoryController.cs:147`,
>   `PopulationService.cs:211` all log the value they passed in as though it landed.
>   `EconomyService.Grant` routes to the **clampable `EarnedIncome`** kind, so **a capped town bank pays
>   0 while the log reads `+500 crystals`.** ⚠ The authority itself is HONEST — `EconomyService.cs:416`
>   prints the post-clamp amount AND the resulting total — so the fix is caller-side: log the returned
>   credited amount, never the argument. Player-facing: this is the shape of "I did the raid and got
>   nothing" being invisible in every capture. **READY.**
> - **977** = **Starter skill points can be silently never granted, and the latch says otherwise** —
>   `Assets/_Modules/Village/Hero/HeroProgression.cs:269` logs *"granted 2 starter skill points"*, but
>   the latch flips at **`:266`, BEFORE** two null-conditional grants which — unlike the identical call
>   twelve lines above — are **NOT** wrapped in the try/catch that would `Fail`. A null `SkillSystem`
>   yields **zero points, latched forever**, with the log reading granted. **Fires for every player
>   exactly once**, which is the worst possible cadence: unreproducible on a second run of the same
>   save. Fix = grant first, latch only on confirmed success, and wrap it like its neighbour. **READY.**
> - **976** = **`hasSurface` is a false green — `panelSettings=ok canvas=ok` proves nothing** —
>   `Assets/_Modules/Core/UI/AddressableUIManager.cs:234` emits
>   `panelSettings=ok canvas=ok => hasSurface=`, but both halves are **non-null checks**. A panel with
>   both references present can still be **zero-sized, offscreen, behind another sort order, or fully
>   transparent** — and the line prints `ok` through every one of those. Same disease as WO-973's
>   `bubble=ok`, on a **far more trafficked path**: this is the shared UI surface resolver, not one
>   NPC's speech bubble. Found by sweep during the WO-973 read-only prep, 2026-08-10.
>   Weaker siblings, same shape, listed so the sweep isn't repeated: `CompanionGearSetup.cs:208`
>   (`result=ok` after an `AddComponent` that essentially cannot return null) and
>   `HudCompassWidget.cs:529` (`hero=ok`, a non-null check). `TowerLoopDevHarness.cs:171` is a dev
>   harness — ignore.
>   ⚠ **The fix is NOT to delete these lines** (§12 — instrumentation is never stripped). It is to
>   make them assert something that can FAIL: resolved rect size, visibility, sort order. A trace
>   that cannot fail is worse than no trace, because it actively steers the next reader away from the
>   broken thing — which is precisely what cost this project a pixel-discovery on WO-973. **READY.**
> - **975** = **The `Gear` Addressables group points at a GITIGNORED art pack** — architect verified
>   2026-08-10. `AddressableAssetsData/AssetGroups/Gear.asset` is **git-TRACKED** and holds **426
>   entries**; resolved GUIDs land in `Assets/Blink/Art/...`, and `git check-ignore -v` returns
>   `.gitignore:350:/Assets/Blink/`. **A tracked group asset ASSERTS content that a fresh clone does
>   not have** — worse than the `polyperfect`/`KayKit` case, because the assertion looks authoritative.
>   Consequence on a clone/CI: 426 dangling entries → a degenerate `gear_assets_all_*.bundle` →
>   `EquipmentController.cs:744`, `HeroArmorVisual.cs:199` and `HeroBodySwapper.cs:148` all fail their
>   `LoadAssetAsync` = **no weapons, no armour, no hero body**. Existing partial net
>   `DataRegression.cs:2642` (`AddressableKeyExists`) returns `false` on throw with only a
>   `LogWarning`, so it is a soft signal, NOT a fence. Fix = promote the referenced prefabs into a
>   tracked location (precedent: the `Resources/Structures` negation at `.gitignore:150-176`) and/or a
>   regression that HARD-fails when entry count ≠ resolvable-GUID count. **READY.**
> - **974** = **The Addressables content build has NO SEAM — it rides a machine-local Editor
>   Preference** — architect verified 2026-08-10. `AddressableAssetSettings.asset:61` →
>   `m_BuildAddressablesWithPlayerBuild: 0`, and the enum at package source
>   (`AddressableAssetSettings.cs:210-215`) reads `PlayerBuildOption.PreferencesValue = 0` — *"use the
>   global settings stored in preferences."* There is **ZERO** explicit content build in the tree:
>   `WebGLBuild.cs:127`, `DesktopBuild.cs:241` and `AndroidBuild.cs:105` each call
>   `BuildPipeline.BuildPlayer` and nothing else; no `BuildPlayerContent` call exists anywhere under
>   `Assets/`. **Whether bundles are rebuilt is decided by an uncommitted per-machine preference.** It
>   is evidently ON on this box (tonight's build emitted fresh bundles), which is exactly what makes it
>   dangerous — it works here by luck, and a fresh clone / CI runner / a seat that ever toggled it ships
>   **stale or absent** `StreamingAssets/aa` with **NO loud failure**; Addressables simply cannot
>   resolve `gear/*` at runtime. Fix = either `m_BuildAddressablesWithPlayerBuild: 1` or an explicit
>   `AddressableAssetSettings.BuildPlayerContent()` at the head of each build entry point, logging its
>   result. **Deliberately NOT landed in the 2026-08-10 release window — it is a build-path change.**
>   **READY.**
> - **973** = **Bryn's speech bubble is a giant skewed world-space card** — found in the PIXELS during the
>   WO-968 headed dungeon proof (`Dungeon_HealersCottage`, 2026-08-11), not by the owner. Screenshots
>   `01_idle`–`05_right` are ~60 % covered by a trapezoid card reading *"Bryn the Wa… / The path opens
>   easy… / mind the rocks — th… / cottage keeps her sh…"*, clipped off the right edge. **The trapezoid
>   IS the diagnosis:** a screen-space canvas cannot skew, so this is a **world-space** canvas seen in
>   perspective at wildly wrong scale. Paired data from the same run:
>   `[Flow:Dungeon] Bryn.Configure 'bryn-the-wanderer' at (-31.00, 0.00, -2.00) (speakRadius=6, bubble=ok)`
>   — i.e. `bubble=ok` reports success while the thing is unreadable, so the trace is asserting
>   construction and NOT legibility. It clears when the hero leaves the 6 m speak radius, which is what
>   proves it is the speak bubble and not a HUD panel.
>   Dialogue lane. Text is CLIPPED, so this is a readability defect, not cosmetic.
>   ⚠ Do NOT "fix" it by moving the camera — the parked WO-968 camera was frozen at the bind seat for
>   this entire run, so the card's apparent size is measured against a stationary camera 5 m away.
>   Re-measure AFTER the camera fix lands before choosing a scale, or you will tune against a bug.
>   **READY (needs a headed re-shot post-camera-fix as its first step).**
> - **972** = **Walls cannot be built beside each other** — owner F8 **seq 2327**, verbatim:
>   *"cannot build walls beside each other"* (`Main_Castle_Overworld`, 2026-08-11 02:05 UTC).
>   PROVEN BY CAPTURE, her Player.log:
>   `[Flow:Build] REJECT Occupied cell=(17,16) fp=(2x2) gate=CellGrid occupantCell=(17,17) occupant='wall_wood'`
>   — **a wall claims a 2x2 block**, while `[Flow:Structure] 'wall_wood' carries Collider 'MeshCollider'
>   bounds size=(3.03, 3.73, 1.42)` proves the palisade is a **one-cell tile** (3.03 m across, 1.42 m
>   thick) on a 3.00 m cell. TWO COLLAPSES STACK: `MeasureUprightFootprintMetres` reduces the mesh to
>   `Max(size.x, size.z)` (the 1.42 m depth is discarded), then `FootprintCells` ceils **and squares** it
>   — so a **1 % overshoot** (3.03 on 3.00) doubles the claim and re-applies that doubling to the thin
>   axis that was never over a cell. Second symptom, same root: her landed run sits on a **6 m pitch**
>   (`Occupy 12_17 / 14_17 / 16_17`, centres x=-7.50/-1.50/+4.50) — every wall run has a ~3 m hole
>   between segments.
>   ⚠ NOT intentional pathing protection — the gate-clearance rule reports `BlocksGate` and never fired;
>   the reject is `gate=CellGrid`. Scaffold / singleton / render-elsewhere all eliminated from the trace.
>   FIX IS CLAIM-SIDE ONLY, **the mesh is never touched** — so the walls-excluded-from-height-cadence
>   carve-out holds, and the **NavMeshObstacle is byte-identical** (`Clamp(rendered*0.85, cellSize, claim)`
>   resolves to the captured 3x3 m box at BOTH the old 2x2 and the new 1x1 claim). **No save migration:**
>   `CellToWorld` seats on the ORIGIN CELL centre independent of footprint, so every saved wall replays
>   in place and merely claims fewer cells.
>   ALSO SHIPPED: **words, never colour alone** (owner is red/green colourblind) — the refusal now names
>   the occupant, and a permanent `FlowTrace.Once` states authored-vs-measured metres, the datum that was
>   logged NOWHERE and had to be bounded from a collider dump during RCA.
>   Regression `WallAdjacencyRegression [wall-adjacency]` written; registration handed to the committer
>   (`DataRegression.cs` is lane-fenced).
>   File `WorkOrders/WORK_ORDER_972_walls_cannot_be_built_beside_each_other.md`.
>   **Code in tree, brace/NUL clean — awaiting batch-gate + commit; PO felt-verifies + closes.**
>
> *(banner bumped 972 → 973 in the SAME edit as the mint.)*
> - **971** = **Remove the original tutorial — ONE tutorial, ONE guide** — owner ruling 2026-08-10,
>   verbatim: *"why are two tutorials active?"* / *"remove the original"* / *"only the new wolf one
>   stays"* / *"from data"*. PROVEN BY CAPTURE, her Player.log on the 20:42 build:
>   `[Flow:SylasSteward] Sylas steward spawned at (2.00, 0.08, 3.00)` and
>   `[Flow:Tutorial] guide BODY summoned ('ice-wolf') at (2.00, 0.06, 3.00)` — **two guide bodies two
>   centimetres apart** — with the ONE spotlight alternating, `FocusMask resolved highlightId=world.guide
>   target=Sylas` then `target=Pet_ice-wolf`, while the strip read "Follow Aldwin to the gate". Her
>   screenshot shows the gold ring on a peasant NPC with the wolf inside the same ring.
>   ⚠ TWO HALVES, ARMED BY DIFFERENT ROUTES — this is why it had to come from data: `TutorialDirector`
>   (the legacy FTUE flow) was **dormant-but-present** (self-destructs at `Title`, plus an ff.tutorialv2
>   stand-down) — exactly the state the ruling forbids; `SylasStewardInjector` **armed every hub load**
>   and is the half she actually saw. WO-1014 retired the nine legacy `tut_*` DIALOGUE ids correctly, but
>   the injector spawns independently of any dialogue.
>   ⚠ WO-1014's "gate the stand-in" fix was committed and in her build and **never executed** — its own
>   stand-down trace occurs **ZERO** times in her log while the steward is still on screen at t=127s. The
>   owner overruled the approach regardless: a fallback that can share the screen with the real guide is
>   a second guide. REMOVED, not gated.
>   REMOVED: `SylasStewardInjector.cs`, `TutorialDirector.cs`, `PetIntroduction.cs`,
>   `TutorialDirectorHubGateTest.cs`, and `ResolveGuide`'s stand-in link (chain is now pet body → Heart).
>   `AssertStewardSurvivesNewGame` **reversed** into `AssertExactlyOneGuideBody` — it asserted the
>   opposite of the ruling and would have gone GREEN on the build she rejected.
>   ✔ CARVE-OUTS VERIFIED, NOT ASSUMED: **Sylas the CHARACTER stays** (`HeroCanonNames.cs`,
>   `hero.ranger` in en.json, his abilities.json kit, `SylasFirstMeeting`) — only his guide-BODY role
>   went. And the "legacy" tutorial folder is NOT wholly legacy: `TutorialFlow` adds `TutorialWaveSpawner`,
>   `DialogueCommandSink` adds `TutorialAutoWalk`+`TutorialHudOverlay`, `ElaraWaveThreeJoin`/
>   `StoryCompanionInjector` call `CompanionSpawner` — deleting the folder would have been an
>   orphaned-reference outage that compiles and blanks. Both carve-outs are now regression-pinned.
>   Regression `OneGuideBodyRegression [one-guide-body]` rewritten (6 cases, dry-run green on the real
>   tree) + the runtime `AssertExactlyOneGuideBody` 8s watch window.
>   ALSO IDENTIFIED, ticket separately: her *"vfx yes thing on the tree"* is **`Poi_NodeAura` →
>   `Magic circle sun loop`**, the POI callout for the invisible DDOL `Collector_lumbermill` stranded at
>   world (0,0,0) — 12 m in front of the Heart anchor, so it reads as a plume at the roots.
>   `AmbientAuraPolicy` misses it because it gates by KEY string, not prefab.
>   File `WorkOrders/WORK_ORDER_971_remove_the_original_tutorial_one_guide_only.md`.
>   **DONE — awaiting batch-gate + commit; PO felt-verifies + closes.**
>
> *(banner bumped 971 → 972 in the SAME edit as the mint.)*
> - **970** = **The bounds align can only YAW — a weapon whose mesh is not authored Y-long never stands
>   up** — owner felt-report, playing the Mage: the Emberglass Staff (`tripo_staff_a`) *"is not being held
>   correctly."* PROVEN BY CAPTURE, her Player.log, then settled at source.
>   `WeaponBoundsOrient.AlignAxesYLongXNarrowZWide` built its result as
>   `Quaternion.LookRotation(Cross(xAxis, yAxis), yAxis)` with **`yAxis = Vector3.up` a CONSTANT** — so the
>   output was **yaw-only BY CONSTRUCTION**, and a yaw can never lift a Z-long mesh onto +Y. `alignLong`,
>   the only term that could tilt it, was used to pick a sign and discarded. Capture signature, twice, a
>   month apart: staff `raw b0=(0.001,0.001,0.021) -> aligned b1=(0.021,0.001,0.001)` and shield
>   `raw (0.008,0.002,0.01) -> (0.01,0.002,0.008)` — both just X and Z SWAPPED, longest on X, never Y.
>   Four downstream seats are written against "prop +Y = the long axis" (`EnsureHandleAtShortYEnd`,
>   `SeatHiltLowerHalf`, `ComputeMeleeGripRotation`, `ComputeSheathRotation`), so all four ran on the
>   staff's **1 mm thickness axis**: sheathed `worldBounds=(0.079, 0.097, 1.265)` — the whole 1.265 m along
>   world Z, **dead horizontal through her back** — and a **2 cm** grip seat on a **1.3 m** haft.
>   ⚠ The 2026-07-06 shield RCA stood on this exact line, fixed the SCALE symptom, and recorded verbatim
>   *"the align's ROTATION is left as-is"* — half-fixed a month ago.
>   ⚠ **INDEPENDENT of WO-966** (settled at source, NOT assumed): `HeroBodySwapper.cs:263` applies the -90
>   to the BODY ROOT and the skeleton is its child, so mesh and prop rotate TOGETHER — a body yaw cannot
>   change how the weapon sits relative to the body. Landing 966 would have changed nothing here.
>   ⚠ NOT staff-specific — permutation-specific: any prop whose SOURCE mesh is not authored Y-long. A
>   Y-long greatsword passes untouched, which is why swords looked fine and this survived.
>   ✔ CLEARED, not assumed: the 1.72 parent-scale compensate is CORRECT (`1.264 x 1/1.72 x 1.72 = 1.264`,
>   matched at BOTH sockets). Adjacent find, ticket separately: back compensates unconditionally (`:1819`)
>   while the hand guards on `_weaponParentCompensate` (`:1834`) -> a `fullOverride` prop renders a
>   different SIZE drawn vs sheathed (`shield_A` is the live candidate).
>   FIX (landed, one file): `localRotation = Inverse(LookRotation(Axis(med), Axis(lng)))` — DERIVED basis
>   change, no compensating Euler, no pitch where a yaw is meant. Permanent `[Flow:Equip] AlignAxes` trace
>   added: `longAxis=` + a Y-longest `aligned b1` on her next equip is the proving line.
>   ⚠ OWNER PIN (§4, untouched): `_staffGripEuler=(0,90,0)` and `sword_A` rot `(117,-2,110)` were dialed on
>   the broken base and may want a re-dial — her hands, not ours.
>   File `WorkOrders/WORK_ORDER_970_weapon_align_is_yaw_only_long_axis_never_reaches_y.md`.
>   **DONE — awaiting batch-gate + commit.**
>
> *(banner bumped 970 → 971 in the SAME edit as the mint.)*
> - **969** = **Opening Pause over the victory summary destroys the pending home return (45s strand)** —
>   owner F8 **2315**, scene `Dungeon_HealersCottage`. PROVEN BY CAPTURE, whole chain in her Player.log:
>   `PanelManager:NotifyOpened` ('Pause') -> `previous.Close()` -> `EndStateView.CloseFromArbiter`
>   -> `Destroy(gameObject)` -> `PostureSignals:SetEndState(false)` (`EndStateView.cs:1665`), then
>   `[BREAK] error: [Flow:BattleArena] STRANDING WATCHDOG FIRED after 45s - the victory panel was
>   destroyed without firing its Continue action, so the deferred home return never ran. Returning the
>   hero anyway. If you are reading this, find WHAT destroyed the end-state (a wave banner or another
>   modal opening over it) - the watchdog is a safety net, NOT the fix.` (`BattleArena.cs:2495`).
>   ROOT CAUSE, read at source: the arena's ONLY route home (`doMaskedReturn`) was owned by
>   `EndStateVM.Primary`, i.e. by a GameObject any modal may destroy. Pause is registered
>   `RegisterBattleAllowed` (`PauseController.cs:182`) so no gate refuses it — and none should.
>   FIX = shape **(c)**: the transition is made INDEPENDENT of the panel's lifetime (hand-back on the
>   MODEL, `EndStateVM.HandBackPendingTransition`, called from BOTH abandon choke points). (a) would
>   have to block Pause; (b) fixes only Pause and leaves the other two destroy paths. Watchdog kept
>   verbatim + pinned by the regression.
>   File `WorkOrders/WORK_ORDER_969_endstate_pending_transition_handoff.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 969 → 970 in the SAME edit as the mint.)*
> - **968** = **HIGHEST — Dungeon locomotion: mover ownership, dead camera basis, frozen camera** —
>   owner F8 **2312** verbatim: *"This problem  gets marked as Highest on the board. Everything is wrong
>   check locomotion"*, and F8 **2313** 22 s later, same scene: *"No camera movement"*. Both in
>   `Dungeon_HealersCottage`. PROVEN FROM THE CAPTURE, not theorised — three seams, one shape:
>   **(1)** the hero's mover FLIPS mid-session and nothing logs which is live — `[Flow:HeroLoco] vel=0.00`
>   while the root moved (neutralize ON, `DungeonHero` moving; `dYaw=12.0` at 60 fps is exactly its
>   720 deg/s cap) versus `[Flow:HeroDrift] vel=(0.000,5.000)` with live input, which is only reachable
>   when the neutralize is OFF (`SetScriptedMove(zero)` -> `ReadMoveInput` at `HeroLocomotion.cs:1517`
>   would make the `input.y > 0.5` gate impossible). **(2)** the animator is fed a COMPONENT, not the
>   world — `ActorAnimator` is the sole `Speed` writer and takes `HeroLocomotion.Velocity` (`:1107`),
>   dead by design in a dungeon, while `DungeonHero`'s competing write can be a permanent no-op because
>   `_animator` is resolved ONCE in `Awake` (`DungeonHero.cs:138-149`), before the async body swap.
>   **(3)** the movement basis is IDENTITY — `[Flow:HeroDrift] camYaw=0.0` on every line because that
>   field is `SmartMobileCamera.CameraYaw` and **no `SmartMobileCamera` exists in the scene** (script
>   GUID count 0), so the stick is world-absolute; `DungeonHero` meanwhile uses `Camera.main`. The
>   camera itself is parked at `yaw=180` = `spawn.facingY 90` + `_headingYawOffset 90`, i.e. its
>   Bind-time seat (authored yaw is 0, so it did move once and then stopped).
>   ⚠ Carries a **MASKING WARNING**: fixing the basis alone while the camera is frozen inverts the
>   stick 180 deg and reads as a NEW bug — camera + basis must ship together.
>   ⚠ **INDEPENDENT of WO-966** (that is the constant 94.5 deg Mage MESH yaw, every scene); this is
>   dungeon-only mover/basis ownership. They STACK — do not tune one against the other. The `dYaw`
>   swings are `DeltaAngle(0, rootYaw)` and are a SYMPTOM of the dead basis, not a third facing defect.
>   Instrumentation (3 permanent heartbeats: `[Flow:HeroOwner]`, `[Flow:DungeonMover]`,
>   `[Flow:DungeonCam]`) is ALREADY LANDED — every remaining unknown is now one capture away.
>   File `WORK_ORDER_968_dungeon_locomotion_ownership_and_camera_seam.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 968 → 969 in the SAME edit as the mint.)*
> - **967** = **The dungeon action bar defaults to the KNIGHT kit (hardcoded literal)** — owner F8 2312,
>   verbatim: *"in dungeon i have the knights action bar loading"* + *"as Thrain"*, playing a MAGE.
>   SETTLED FROM SOURCE, no further capture needed: three hand-written `"knight"` string literals in
>   `HudModelProducers.cs` (**:392** the reported bug, **:87** + **:139** latent). NOT an enum-zero
>   default — `HeroClass`'s zero is Mage (`Enums.cs:49`) and `AbilityCatalog.DefaultClass` is `"mage"`.
>   Dungeon-only because the composed hero is baked with HeroLocomotion + HeroBodySwapper only
>   (`DungeonBaker.cs:1168-1187`) and `EnsureHeroCombatComponents` provisions nine components but never
>   `HeroAbilities` — so `FindAnyObjectByType<HeroAbilities>()` is null and `:392` asserts Knight. The
>   NAME stayed right (Thrain IS the Mage name, `en.json:145`) because `HeroVitalsProducer` has a sticky
>   `_classId` cached from town across the `DontDestroyOnLoad` host and the ability producer has none.
>   ⚠ This is a REPEAT of the F8 seq-642 defect already fixed in `GearLoadout.CurrentJob` — same
>   persisted-state fallback, never applied to the second reader. ⚠ The seam is INSTRUMENTATION-SILENT
>   (zero hits across Player.log + break-log for every ability/identity tag), which is half of why it
>   cost a session; the WO ships the traces regardless of the fix.
>   File `WORK_ORDER_967_dungeon_action_bar_defaults_to_knight.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 967 → 968 in the SAME edit as the mint.)*
> - **966** = **Hero body faces the wrong way while running (Mage NW when running N)** — owner F8 2309.
>   MEASURED, not guessed: `HeroFacingAudit.MeasureAll` reports Mage needs **4.5 deg** and Ranger **3.7 deg**
>   to face +Z, while `HeroBodySwapper.cs:263` applies **-90** to every non-Knight body — a **94.5 deg**
>   error on the mesh. KnightV3 measures 0 and agrees, which is why only the new classes show it. The -90
>   is the Tripo-era convention applied to CC/AccuRIG models that arrived 2026-08-06. Fix is a one-line
>   owner choice (derive vs constant) recorded in the WO. **⚠ RENUMBERED from 965** — the CLI seat wrote
>   the file without bumping this banner (the 08-02 collision failure, repeated); the F8-queue lane minted
>   965 correctly and is cited in `CLAUDE.md:431`, so it keeps the number.
>   File `WORK_ORDER_966_hero_facing_offset_when_running.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 966 → 967 in the SAME edit as the mint.)*
> - **965** = **F8 inbox is a QUEUE — no owner capture is ever dropped again** — a real harness defect,
>   proven on disk today: the seat acked seq **2306**, the next ping it ever saw was **2309**, and seq
>   **2307** (*"both NPC and echo but no movement"*) + **2308** (`[Flow:Tutorial] STEP-STUCK ::
>   founding_walk`) reached NO seat. Cause: `LATEST_CAPTURE.md` + `PING.json` were single slots (a burst
>   overwrote itself), `f8-ack.ps1` acked PING's **newest** seq (burying everything below it), and the
>   per-seq file name `capture-<HHmmss>.md` collided inside one second. Fix = append-only
>   `logs/f8-inbox/QUEUE.jsonl` + per-seq capture files + oldest-first `f8-check-inbox.ps1` +
>   one-at-a-time `f8-ack.ps1`, with supersede/unqueued/lost all LOUD in `queue-events.log`. Exit codes,
>   PING seq and ACK watermark contracts preserved; the owner changes nothing.
>   File `WORK_ORDER_965_f8_inbox_capture_queue_no_drops.md`. **DONE — awaiting batch-gate + commit.**
>
> *(banner bumped 965 → 966 in the SAME edit as the mint.)*
> - **964** = **Unearned structures are HIDDEN, not shown-locked** — owner F8 2303, verbatim: *"dont show
>   the spire, leave as blank till earned, allows us to unlock new items and not reveal what they are"*.
>   ⚠ REVERSES WO-1013's visible-locked Spire card, which shipped the SAME DAY (`bd9d54d9`); both rulings
>   are recorded in the WO. Good news: it is a DATA move — `build-categories` already has both buckets
>   (`lockedIds` filters the row OUT, `visibleLockedIds` renders it greyed), so the Spire moves buckets
>   and `ProgressionUnlocks.IsUnlocked` is already the earn gate. ⚠ Carries an OWNER QUESTION: this is the
>   opposite policy to WO-960's armor-store greyed ladder, which also shipped today — both can be right
>   (shop = aspiration, new structure = surprise) but only one can be the house rule.
>   File `WORK_ORDER_964_hide_unearned_structures_until_unlocked.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 964 → 965 in the SAME edit as the mint.)*
> - **963** = **Build carousel follows the tutorial's teaching order** — owner F8 2302, verbatim: *"Can
>   we order the carousel in order of how the tutorial presents them?"* RCA'd live: there is NO sort —
>   `BuildPaletteVM.Rebuild` foreaches the registry query, so the order IS the row order in
>   `structures-catalog.json`. Fix = an owner-tunable display order in the catalog (data, not code),
>   seeded Lumbermill → Tower → Workshop → Armorer per `tutorial-steps.json` orders 20/30/1050/1060,
>   with current catalog order as the stable tiebreak. The palette must NOT read the tutorial script at
>   runtime — presentation never depends on a teaching flow.
>   File `WORK_ORDER_963_build_carousel_tutorial_order.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 963 → 964 in the SAME edit as the mint.)*
> - **962** = **`guide_gate` must LATCH, not re-resolve — the WALK beat chases a moving gate** — owner
>   F8 2301, proven in her Player.log: the anchor resolved to `WaveSpawnPoint-S` (-3.43,0.08,-38.63),
>   then `guide-lead SET` fired again at (37.29,0.08,-0.21) [east] and (3.07,0.08,38.68) [north] as she
>   walked, so `hero.reached:guide_gate` was never reachable and the step STEP-STUCK at 123s and was
>   watchdog-SKIPPED. Fix = resolve the gate ONCE on step ENTER and latch it for the step's life.
>   Pure logic, no art. File `WORK_ORDER_962_guide_gate_anchor_latch.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 962 → 963 in the SAME edit as the mint.)*
> - **961** = **The founding Echo guide gets a BODY, and it is the Ice Wolf** — owner ruling 2026-08-10
>   ("we should have Ice wolf", pointing at `Assets/Resources/Pets/ice-wolf.fbx`). REVERSES the
>   2026-07-16 call recorded at `TutorialFlow.cs:1307-1319` (aether-sprite "ethereal spirit, NOT the
>   quadruped ice-wolf that T-posed"). Proven at source: the body is not spawned AT ALL today —
>   `[Flow:Tutorial] grant.starterPet — visible echo MODEL birth SCRAPPED (echoes are portrait cards
>   now)` — while the WALK objective still says "Follow {guide} to the gate". Real cost is NOT the mesh
>   (tracked, and `PetDeployer` already loads `Pets/<species>`): `ice-wolf.fbx.meta` is
>   `animationType: 2` / `avatarSetup: 0` / `clipAnimations: []`, there are ZERO `.controller` and ZERO
>   `.anim` under `Resources/Pets`, and `Pets/Pet` + `Pets/PetIdle` are in the known-missing baseline —
>   so it needs a rig + idle + walk + controller or it ships as a sliding bind-pose statue (QR-5.3).
>   ⚠ The comment claiming aether-sprite is "the only HUMANOID rig" is FALSE at source — its meta is
>   Generic with no avatar too; it only reads ethereal because `EchoSpiritPresentation` hovers it.
>   Canon is on the ruling's side: the unlock card in her session reads `id=echo-frosthowl`, and
>   Frosthowl IS the ice wolf. File `WORK_ORDER_961_founding_guide_body_ice_wolf.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 961 → 962 in the SAME edit as the mint.)*
> - **960** = **Armor store: locked-preview ladder (greyed + Lv N, next-5-levels window)** — owner
>   ruling 2026-08-10. RCA'd start: armor.json has 24 rows (per-class rarity ladders), store shows 3 —
>   a visibility/filter defect, not missing content; level-gate derivation to be found/proposed as
>   data. File `WORK_ORDER_960_armor_store_locked_preview_window.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 960 → 961 in the SAME edit as the 960 mint.)*
> - **959** = **Weapon flame aura only while unsheathed** — owner ruling F8 2297 ("only show the
>   flames on the sword when unsheathed"). One gate at the GearAura seam, all element auras; the
>   "unsheathed" state mapping named in the RESULT for her felt-confirm.
>   File `WORK_ORDER_959_weapon_flame_aura_only_unsheathed.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 959 → 960 in the SAME edit as the 959 mint.)*
> - **958** = **Dungeon camera stops fighting the player in small rooms** — owner F8 2289 (auto-rotate
>   + tight-space framing in dg_ember_deep). Capture-first tuning, all values in DungeonCameraProfile
>   (the one authority), owner felt-pass closes. File `WORK_ORDER_958_dungeon_camera_tight_rooms_stability.md`.
>   **READY TO IMPLEMENT.**
>
> *(banner bumped 958 → 959 in the SAME edit as the 958 mint.)*
> - **957** = **EXIT beacon on EVERY stairwell in multi-floor dungeons** — owner F8 2287 + screenshot
>   (EXIT arrow on a mid-dungeon descent). Hypothesis: beacon placement predates WO-930 multi-floor
>   (stairs used to BE the exit); fix = one designated exit per layout + per-layout regression.
>   Companions WO-1007/1008 own presentation. File `WORK_ORDER_957_exit_beacon_on_every_stairwell.md`.
>   **READY TO IMPLEMENT.**
>
> *(banner bumped 957 → 958 in the SAME edit as the 957 mint.)*
> - **956** = **An enemy reads GREEN — hostility never sits on the red/green axis** — owner F8 2269 +
>   clarification (enemy showing green; owner red/green colourblind). RCA-first (heal-cast glow on the
>   hollow-acolyte healer is the lead candidate — the never-ticketed 08-06 "Cast_Heal green glow"
>   item); fix = faction-driven effect presentation, hostile palette/shape.
>   File `WORK_ORDER_956_enemy_reads_green_hostility_cue.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 956 → 957 in the SAME edit as the 956 mint.)*
> - **955** = **VFXManager.Acquire NRE — pool free list hands back a destroyed host** — captured
>   exception (owner session 2026-08-10, arena-death churn via HeroHpStateAura): Acquire:876 threw on
>   a destroyed pooled host's transform. Fix = dead-slot evict+rebuild with a Warn, find the teardown
>   destroyer; ONESHOT saturation stays separate. File `WORK_ORDER_955_vfx_pool_destroyed_host_nre.md`.
>   **READY TO IMPLEMENT.**
>
> *(banner bumped 955 → 956 in the SAME edit as the 955 mint.)*
> - **954** = **Hollow family still wears KayKit skeletons + enemy id→model mapping goes data-driven** —
>   owner report 2026-08-10, RCA'd live: authored (no fallback), the whole hollow town-wave family maps
>   to plain KayKit `Skeleton_*` across FOUR divergent code tables; enemies.json has no model column.
>   Mechanics READY (data column + one resolver, behavior-preserving seed); the hollow re-skin model
>   pick = owner creative pin. File `WORK_ORDER_954_hollow_family_models_data_driven.md`.
>
> *(banner bumped 954 → 955 in the SAME edit as the 954 mint.)*
> - **953** = **Harvest "+N" pops via the damage-number spawner + gated-faucet honesty** — owner
>   rulings 2026-08-10 (reuse the damage-points spawner; her zero-iron RCA'd live to the
>   phantom-income gate: 'forge' never built on her blank save, correct-but-silent). Picker shows a
>   NEEDS cue; pet-node demo rates (5/5) promoted to owner-tunable data, values unchanged.
>   File `WORK_ORDER_953_harvest_drip_feedback_and_gated_faucet_honesty.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 953 → 954 in the SAME edit as the 953 mint.)*
> - **952** = **EndState wave-clear panel compresses body below content size** — the panel's own
>   FlowTrace net fired twice in one session (need=276px well=249px scale=0.9, screen-height clamp);
>   fix = reflow-not-shrink + a capture case at the failing resolution asserting the Fail line's
>   ABSENCE. File `WORK_ORDER_952_endstate_panel_body_compression.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 952 → 953 in the SAME edit as the 952 mint.)*
> - **951** = **Echo Hollow repurposed: tap it → the Echoes popup opens** — owner ruling 2026-08-10
>   (F8 2266 + confirmation "Simple and easy"). Not removed, not a skins store; keeper Talk routes to
>   the existing roster panel. Capacity/awakening-stage/skins-counter recorded as UNPINNED extensions.
>   File `WORK_ORDER_951_echo_hollow_opens_echo_roster.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 951 → 952 in the SAME edit as the 951 mint.)*
> - **950** = **Drillmaster + teach toast appear on a blank-town save with NO barracks** — owner
>   felt-report 2026-08-10, RCA'd live: the injector's OnSceneLoaded path checks unlock but never
>   `MayBakedTwinSurface`, while the sweep shows surfaced=0/everBuilt empty on the same save. Fix at
>   the Inject seam + ownership reconcile + once-teach burn guard.
>   File `WORK_ORDER_950_drillmaster_without_barracks_blank_town.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 950 → 951 in the SAME edit as the 950 mint.)*
> - **949** = **Death UX: respawn IN TOWN + starter potions + teach the cost of dying** — owner F8s
>   2026-08-10 10:20/10:22 verbatim ("On Death I should respawn in town not where I died", "start the
>   user with some potions, and explain to them consequences of dying with resources"). Discovery-first
>   on what death costs today; potion-button-at-zero + no-apothecary caveats carried in the WO.
>   File `WORK_ORDER_949_death_ux_respawn_town_potions_teaching.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 949 → 950 in the SAME edit as the 949 mint.)*
> - **948** = **Walls: build at L1 only, upgrade to climb (CoC model)** — owner ruling 2026-08-10 on
>   first seeing Castle Structures ("enforce them to start with a level one wall... like CoC does
>   it"). Verified: `BuildPaletteUI.cs:1105-1106` offers wall_wood AND wall_stone as placeables; the
>   walls.json ladder already exists and heart-mitigation already pays. Scope = palette enforcement +
>   the wood→stone rung only; deeper tiers/gates stay in WO-904 behind raid-steal.
>   File `WORK_ORDER_948_walls_build_l1_only_upgrade_to_climb.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 948 → 949 in the SAME edit as the 948 mint.)*
> - **947** = **Cost-basket separation: regular = wood+iron, magical/ethereal = crystal-based, never
>   all three** — owner economy ruling 2026-08-10 (verbatim in the WO). Audit found 6 of 29 entries
>   violating; SPEC pending 4 owner classification pins (healer/caravan/jeweler/arcane-tower + the
>   crystals-pair-with-iron-or-wood call), then mechanical data edit + invariant regression.
>   File `WORK_ORDER_947_cost_basket_separation_regular_vs_arcane.md`. **SPEC.**
>
> *(banner bumped 947 → 948 in the SAME edit as the 947 mint.)*
> - **946** = **POI node auras + Tree of Life: retire the strong yellow, go subtle** — owner F8 seq 2252
>   verbatim look ruling (*"remove the yelllow from the nodes and the tree of Life (its a vfx) but we
>   want something subtle, not so strong"*). Needs EYES to verify (screencap loop), owner felt-close.
>   File `WORK_ORDER_946_poi_tree_aura_yellow_subtle.md`. **READY TO IMPLEMENT.**
> - **945** = **Tutorial: the SECOND tower runs the full 90s curve while the teaching wave lands** —
>   owner felt-report (Seeker + exe, multiple repros), RCA data-proven same morning: the 5s first-build
>   grace is per-structure-id, the tutorial asks for two towers of the SAME id, tower #2 ran 90s
>   (proving lines Player.log 51090 grace on #1 / 55450 no grace on #2 / 55676 cost-freebie DID fire).
>   Fix = while !Onboarded every build gets the grace (the 08-06 ruling's own stated intent), pallets
>   carve-out intact. File `WORK_ORDER_945_tutorial_second_tower_timer_grace.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 945 → 947 in the SAME edit as the 945+946 mints — the rule that broke five times on 08-02.)*

> ## (superseded header) RECONCILED 2026-08-09 (CLI / THE RULES): main line next free = ~~945~~ — see the 2026-08-10 header above. **782–859 + 900–944 CONSUMED.**
> - **944** = **Placing: the item's title pins STATIC at the top of the screen** — owner F8 seq 2250
>   flagged live in the fresh 22:11 build (*"can we make the title of the item pin staticl maybe at the
>   top of the screen"*); retires the last follow behaviour (the pill), UI_PLAYBOOK §8's own preferred
>   answer. File `WORK_ORDER_944_placing_title_pinned_static_top.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 944 → 945 in the SAME edit as the 944 mint.)*
> - **943** = **The docs get a HOME: wiki-style linked navigation over the doc lake** — owner
>   directive 2026-08-09 (*"almost like a Wiki... start at from home... the next CLI seat doesn't
>   have to dig"*): ONE GENERATED static home page (HOME.html or a BOARD nav rail, built beside
>   `board_build.py`) linking rules / architecture hub / north star + newest ground truth / board /
>   master catalog / VFX + sounds organization views. LINK never duplicate; generator fails on dead
>   links; newest-by-date resolution; composes with WO-937/938/940/1011. Overnight lane, carries the
>   aged-vs-new due-diligence rider. File `WORK_ORDER_943_docs_wiki_home_linked_canon.md`.
>   **READY TO IMPLEMENT.**
>
> *(banner bumped 943 → 944 in the SAME edit as the 943 mint — the rule that broke five times on 08-02.)*
> - **942** = **UI capture harness: two capture-case gaps left by the WO-1010 pass** — the
>   `padon` case is byte-identical to `edgeclamp` (the identical-file-size tell) because the D12
>   no-toggle ruling dissolved what it photographed, and the D17 sprite-path dim-on-invalid has no
>   assertion. File `WORK_ORDER_942_ui_capture_case_gaps_wo1010.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 942 → 943 in the SAME edit as the 942 mint — the rule that broke five times on 08-02.)*
> - **941** = **RumorBoard + RealmMap: controls overlap text (16 UI_GEOMETRY assertions)** — the
>   geometry oracle pins CloseButton/CTA/reward-label overlaps at both portrait sizes on RumorBoard and
>   a map-node disc over text on RealmMap at both landscape sizes; PRE-EXISTING (identical in the 20:41
>   and 22:04 runs, attributed before ticketing). File
>   `WORK_ORDER_941_rumorboard_realmmap_geometry_overlaps.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 941 → 942 in the SAME edit as the 941 mint.)*
> - **940** = **Board: DATE-tag every ticket + "opened within" filter (age is DERIVED, never typed)**
>   — owner ruling 2026-08-09: *"i want aged tagged to every ticket"*, *"date tagged"*, *"so we can
>   filter opened within and see"*. Backs the one-week validity threshold (`SUNDAY_HOUSEKEEPING.md` §4):
>   you cannot apply "older than a week -> verify" if the board does not show age. **Carries a real
>   defect:** `tools/board_build.py:116` labels its Age column from `os.path.getmtime` — that is LAST
>   MODIFIED, not OPENED, so any edit resets a ticket's apparent age and "opened within" is unanswerable
>   today. File `WORK_ORDER_940_board_date_tagging_and_opened_within_filter.md`.
>   **READY TO IMPLEMENT.**
>
> *(banner bumped 940 → 941 in the SAME edit as the 940 mint — the rule that broke five times on 08-02.)*
> - **939** = **Backend auth rail is compiled OFF in every shipped build (+ guest-id salt in the binary)**
>   — `BACKEND_AUTH_ENFORCED` is defined on NO platform row in `ProjectSettings.asset`, so
>   `BackendAuthConfig.cs:58`'s enforced branch is compiled out and `GameStateService` sends no auth
>   headers; real cloud saves therefore ride the GUEST rail, whose id is
>   `Sha256(deviceId + GuestIdSalt)` with the salt literal at `GameStateService.cs:1572`. Anyone who
>   derives that id can read AND overwrite that player's save. Server side is sound — the client just
>   never uses it. **Owner ruling 2026-08-09: OVERNIGHT, not a hotfix — no live player base, so
>   exposure is theoretical.** File `WORK_ORDER_939_backend_auth_rail_unreachable.md`.
>   **READY TO IMPLEMENT.**
>
> *(banner bumped 939 → 940 in the SAME edit as the 939 mint — the rule that broke five times on 08-02.)*
> - **938** = **`RULES.md` — the one page the owner can point at** — the binding rules are currently
>   spread across CLAUDE.md, PREFLIGHT_GATE.md, SESSION_CANON_LOADER.md, docs/HANDOVER.md,
>   docs/TICKET_PIPELINE.md, docs/ARCHITECTURE_PRINCIPLES.md, docs/INSTRUMENTATION_STANDARD.md and
>   docs/BOARD.md, so "read the rules" has no single target. ONE numbered, non-negotiable list that
>   POINTS AT the deep docs rather than duplicating them (a copy is a future contradiction). File
>   `WORK_ORDER_938_rules_single_page.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 938 → 939 in the SAME edit as the 938 mint — the rule that broke five times on 08-02.)*
> - **937** = **Board status-line hygiene + parser scope** — `--check` reports 91 Unlabeled, but it is
>   TWO problems: **20 are not work orders at all** (audits/briefs/handoffs/README living in
>   `WorkOrders/`, filename not `WORK_ORDER_<n>`) → parser SCOPE fix, not a status fix; and **71 are
>   real WOs with a missing/empty `**Status:**` line** → the actual defects. Fix scope first so the
>   count means something, then sweep the 71. File
>   `WORK_ORDER_937_board_status_hygiene_and_parser_scope.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 937 → 938 in the SAME edit as the 937 mint — the rule that broke five times on 08-02.)*
> - **936** = **Catalog gating + progression truth pass** — `LockedIds` is READ-ONLY with no unlock
>   path, so "unlock-gated" ids (jeweler) are permanently hidden, not temporarily; the three
>   stockpiles declare `maxLevel:3` with NO tier rows, capping the wood/iron/food economy; and the
>   live `collector_lumbermill` routes its upgrades through the RETIRED `lumbermill` row. File
>   `WORK_ORDER_936_catalog_gating_and_progression_truth.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 936 → 937 in the SAME edit as the 936 mint — the rule that broke five times on 08-02.)*
> - **935** = **Paid animation + VFX pack connection program** — inventory $1000s of Asset Store
>   packs (Hovl/Mirza/Spells/UT Particle/Supercyan/KayKit/Action), map what ships vs sits idle,
>   wire troop/hero combat anim+VFX end-to-end WITHOUT rebuying or forking catalogs; protect
>   self-containment (`Resources/VFX/_Shared`). File
>   `WORK_ORDER_935_paid_anim_vfx_connection_program.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 935 → 936 in the SAME edit as the 935 mint — the rule that broke five times on 08-02.)*
> - **934** = **Army loadout bank (3 named presets + persist + muster polish)** — save/load/quick-fill
>   Raid Push / Wall Hold / Siege Prep; save schema v38; Armies button on Barracks. File
>   `WORK_ORDER_934_army_loadout_bank.md`. **IMPLEMENTED.**
>
> *(banner bumped 934 → 935 in the SAME edit as the 934 mint — the rule that broke five times on 08-02.)*
> - **933** = **Siege Catapult troop (CoC scarcity + WC Demolisher)** — 8th roster unit at Barracks
>   T4 beside Outrider; `maxOwned:1` (wounded still blocks); role `siege` structure-prefer hunt;
>   range ~26 / slow / fragile / heavy cost; structure vs unit damage mult; machine visual
>   `Structures/Catapult`. File `WORK_ORDER_933_siege_catapult_troop.md`. **IMPLEMENTED.**
>
> *(banner bumped 933 → 934 in the SAME edit as the 933 mint — the rule that broke five times on 08-02.)*
> - **932** = **Raids full functional audit + step-by-step fix ladder** — Path A teleport/deploy is
>   LOCKED V1 (raidwalk OFF); spine exists (HUD→select→deploy→RaidBase→score→victory→return) with
>   headless gates; gaps = prereq teach, full-army feel, Auto Recommend stub, scene honesty, win/
>   soft-lock integrity, eliteCount dead key, IronBastion orphan. Phases 0–6 to fully functional
>   Regular clear. File `WORK_ORDER_932_raids_full_functional_audit_and_fix.md`. **READY.**
>
> *(banner bumped 932 → 933 in the SAME edit as the 932 mint — the rule that broke five times on 08-02.)*
> - **931** = **Close the StubWalletProvider free-grant hole** — `StubWalletProvider.cs` has NO
>   `#if UNITY_EDITOR`/`DEVELOPMENT_BUILD` guard, so it compiles into every shipped build and
>   `WalletService` auto-selects it on release desktop/WebGL (and Android without `SOLANA_SDK`). Chain:
>   Buy → fake Connect → mock 2000 SKR balance → fabricated base58 sig → `ApplyPackContents` grants the
>   pack for ZERO payment + fires `purchase_completed` with the fake txSig. `FeatureFlags.RealmStorePurchase`
>   (default OFF) is the ONLY gate, so it is **not urgent today, hard blocker the moment monetization
>   flips** — now precondition **3 of 3** in that flag's DO-NOT-TURN-ON block. Candidate fixes
>   (a) build-guard / (b) runtime refusal at the WalletService seam / (c) both are left UNPICKED —
>   architecture call. `WalletService.PayFlat` is in scope: same stub path, gated by NOTHING, dead only
>   because both callers are scene-absent (GUID sweep).
>   File `WORK_ORDER_931_stub_wallet_free_grant_hole.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 931 -> 932 in the SAME edit as the 931 mint — the rule that broke five times on 08-02.)*
> - **930** = **The stairwell is ONE room: midpoint to midpoint** — the owner's design, and the
>   replacement for the `_Up`/`_Down` pair model. A stairwell is a single room owning its subrooms,
>   connecting the MIDPOINT of the upper floor to the MIDPOINT of the lower; run is the footprint,
>   slope is DERIVED (25-31 deg, never near the 45 deg carve cliff); the upper level is a GALLERY so
>   the stair rises through OPEN AIR instead of squeezing under a slab with 0.36 m to spare.
>   **The composer needs NO change** — a socket already carries its own Y and `SolveMate` resolves
>   height for free. DELETES `StairUp`/`StairDown`, the vertical mate branch, `IsVertical`,
>   `SEALED_VERTICAL`, the floor holes and the ceiling shafts.
>   File `WORK_ORDER_930_stairwell_is_one_room_midpoint_to_midpoint.md`. **READY TO IMPLEMENT.**
> - **929** = **VFX aura reparented during activate/deactivate** — a real thrown Unity error, 3x in one
>   session, on POOLED enemies (`Cannot set the parent ... while activating or deactivating`).
>   File `WORK_ORDER_929_vfx_aura_reparent_during_activation.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 929 -> 931 in the SAME edit as the 930 mint. ⚠ 929 was minted earlier today WITHOUT
> bumping this banner — the CLI's own violation of the rule it had been enforcing all day, caught and
> corrected here. It is the same slip that caused five collisions on 08-02: the mint and the bump must
> be ONE edit.)*
> - **928** = **Archer Tower: orientation, materials, footprint parity, and the Move path** — one
>   owner-ruled cluster from the 2026-08-08 felt-test (F8 2181-2192). All four root causes CAPTURED:
>   `VisualFactory.cs:140` wipes the L3 prefab's baked 270deg to identity, so the height-fit then
>   measures the wrong axis (scale 8.34x vs L1's 4.74x, bounds 4.91 x 4.80 x 8.34 against a 3x3 m
>   blocker and a declared footprint of 1.75); L3 wears the raw Tripo `wooden_watchtower_3d_model_basecolor`
>   instead of the built material; and PLACE on a Move ends in `CancelArmed` (`armed cleared`) with
>   `Two-step RE-DROP` never running. Two theories are already KILLED in the WO - do not re-run them.
>   File `WORK_ORDER_928_archer_tower_orientation_materials_footprint_move.md`. **READY TO IMPLEMENT.**
>
> *(banner bumped 928 -> 929 in the SAME edit as the 928 mint - the rule that broke five times on 08-02.)*
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
> | **main line** | CLI | **→ READ THE HEADER AT THE TOP OF THIS FILE. THIS ROW NO LONGER CARRIES A NUMBER.** |
> | **1000–1099 reserved** | UI seat | **→ READ THE HEADER. THIS ROW NO LONGER CARRIES A NUMBER.** |
>
> ### ⚠ WHY THESE CELLS ARE EMPTY (2026-08-09 — a live re-mint hazard, not tidying)
> This table used to restate the next-free numbers. It read **932** for the main line while the header
> above said **939** — a seven-number gap, and **932–938 all exist on disk**. A seat trusting the table
> would have re-minted over SEVEN live work orders. The row even carried a note saying it had been
> "corrected 2026-08-09"; it went stale the SAME DAY, because 933–938 were minted after the correction.
>
> That is the exact failure this file's own rule warns about — *"never a number copied into any other
> doc"* — and the copy was **inside the numbering authority itself**. A duplicate cannot be kept honest
> by discipline; it can only be removed. **The header is the sole source. Do not restore numbers here.**
>
> *(UI-seat bumped 1026 -> 1030 in the SAME edit as the WO-1026/1027/1028/1029 mints — the owner's
> **CoC + WC3 design review**, full analysis in `docs/DESIGN_REVIEW_COC_WC3_LENS_2026-08-15.md`.
> VERDICT: both engines are BUILT and NEITHER CLOSES ITS LOOP. **1026** = the base is never attacked, so
> player-authored layout has ZERO consequence (grep: `RaidDefen*`/`DefenseReport`/`Revenge`/`Trophy` = 0
> hits) — highest leverage, needs an owner ruling on PvE-siege vs async-PvP vs ghost-PvP first ·
> **1027** = the queue is mechanically better than CoC's but has no IDLE-BUILDER ACHE and no session
> shape; surfacing only, cheapest item · **1028** = 4 dungeons are `PathComplete` + torch/oil/darkness
> ~90% built and there is NO reason to descend and NO payoff that feeds town — largest built-but-parked
> value in the tree · **1029** = `ClanService` is a self-declared PlayerPrefs stub, `Donat*` = 0 hits;
> ship DONATIONS not wars, sequenced LAST and BLOCKED on `api/` being PREVIEW-only.
> ⚠ **WO-910 (31 of 40 dead Ranger/Mage talent nodes) OUTRANKS ALL FOUR and is already open — do NOT
> re-mint it.** It is the WC3 pillar broken on the very screen WO-1021 is polishing. READY.)*
>
> *(UI-seat bumped 1025 -> 1026 in the SAME edit as the WO-1025 mint — owner 2026-08-15: *"these graphics
> on tree look amatuerish"* (Heart of Elarion, hub centre). ⚠ **THE OBVIOUS DIAGNOSIS IS WRONG:** F8
> seq=2398 harvest proves BOTH authored loops are WITHHELD at this tree —
> `whiteSwirlSuppressed=True treeAuraSuppressed=True treeHandle=none` — so the yellow cone + white
> starburst on screen are **NOT** `Aura_HeartPulse` / `TreeofLifeAura_Aura`. Do NOT re-tag those and do
> NOT flip the suppression flags (they are deliberate, WO-1002 + the owner's "stray heal VFX be gone").
> The emitter is UNIDENTIFIED — step 1 is INSTRUMENT the tree's child hierarchy per §12, likely particle
> children baked into the prefab (which would render regardless of the controller's flags, explaining the
> contradiction). Separate contributor: the tree model has ONE texture
> (`enchantedtree3dmodel_basecolor.JPEG`, no normal/roughness/AO) so it renders flat under URP. READY.)*
>
> *(UI-seat bumped 1024 -> 1025 in the SAME edit as the WO-1024 mint — **STRUCTURES BURN WITH NO REPAIR
> SURFACE AT ALL.** Owner F8 seq=2398/2342: `WallRepairController=ABSENT HubRepairAffordance=ABSENT
> WaveManager=Active` in `Main_Castle_Overworld`. Root cause PROVEN and the code predicted it in its own
> bail-path comment (`HubRepairAffordance.cs:111-116`): the installer is a ONE-SHOT on `sceneLoaded` and
> `SceneHasRepairables()` runs while the player-built town is still EMPTY — placement restores AFTER
> scene load, the gate bails, and it NEVER retries. `StructureDamageVisuals` installs unconditionally, so
> fire renders with no repair option. ⚠ NOT a coverage bug — do NOT widen `SceneHasRepairables()` again
> (already widened once); **the bug is the TIMING, not the set.** READY.)*
>
> *(UI-seat bumped 1023 -> 1024 in the SAME edit as the WO-1023 mint — talent icon map AUDITED 2026-08-15
> and it is GOOD: 83/83 coverage, 0 orphans, 0 iconPath mismatches, 0 missing sprites, both canonical
> copies byte-identical. Three real findings: (1) **two pairs of talents render the IDENTICAL icon** —
> `Rogue7` claimed by knight.t2n6 Venombrand + ranger.t2n2 Venomcraft, and `Arcanist1` by mage.t1n1
> Arcane Focus + shared.n9 Arcane Bolt; (2) **NO regression pins any of it** — `grep talent-icon-map`
> over `Assets/Editor/` returns ZERO, i.e. the exact WO-996 armor.json shape (two copies, no oracle,
> Resources wins at runtime so the Editor looks fine); (3) `emblem/` (25 class crests) and `classslot/`
> (25 themed plates) are ALREADY COMMITTED and UNUSED — free per-tree visual identity. Archetype
> coherence: mage strongest (19/20 Elementalist), ranger strong (13/20 Assassin), knight widest spread
> across 14 folders **and correct** — its economy/fortification nodes have no Warrior equivalent, so do
> NOT narrow it. Governing rule confirmed: match the SKILL's meaning, not the tree's class. READY.)*
>
> *(UI-seat bumped 1022 -> 1023 in the SAME edit as the WO-1022 mint — `Main_Castle_Overworld.unity`
> carries **56 references to three DELETED prefab GUIDs** (StorefrontCrate / CourtyardFloor / StorefrontVine).
> `cc122e844` (WO-608 seam cleanup, 2026-07-04) removed the assets; the scene's pointers survived because
> no gate reads scene GUID refs. Throws on EVERY scene open — ~48 of the 60 F8 captures queued 2026-08-15,
> which is what buried two real tutorial STEP-STUCK signals. Decide DELETE-REFS vs RESTORE-PREFABS first;
> never hand-edit the .unity. READY.)*
>
> *(UI-seat bumped 1021 -> 1022 in the SAME edit as the WO-1021 mint — talent tree, the last four gaps to
> the Obsidian demo AFTER `61a2a701c` closed sizing/connectors/frontier: (1) the lattice is still FIXED
> px `GraphUnitWpx/Hpx = 1180x780` against a ~1695x493 body well, so the graph hugs the upper-left and
> leaves a dead black third; (2) the viewport paints an OPAQUE slab over `frame_talent` — the named
> Grok-02 §6 failure mode — while mirrored `panel_talent` + `deco_talent_1/2` sit unused; (3) the Wisdom
> chip ellipsizes to "WIS..." over the board, breaking the no-ellipsis wallet law; (4) locked skill art
> reads too dark. Plus one anomaly to INSTRUMENT not guess: a bare "1"/"0" cost pip where the data has no
> zero-cost node. Multi-rank (`3/3`) is OUT — explicit V1 non-goal. READY.)*
>
> *(UI-seat bumped 1020 -> 1021 in the SAME edit as the WO-1020 mint — WALLS CANNOT BE PLACED
> ADJACENT to each other (owner F8 seq=2327). Trace shows ghostValid=True while a neighbouring
> 'wall_wood@16_17' is still BUILDING (remaining=8s) — suspect the in-progress build job's cell
> reservation blocks the adjacent cell, or a rotated footprint over-claims. Walls are useless if
> they cannot form a RUN.)*
>
> *(UI-seat bumped 1019 -> 1020 in the SAME edit as the WO-1019 mint — Thrain/MAGE action bar: the
> authored defaults in abilities.json are CORRECT and all-magic (Q Fireball / W Arcane Shell / E Mend /
> R Meteor), so the reported "inherits the previous character's hotswap, nothing explicit for DPS" is a
> RUNTIME BINDING defect — the bar does not rebind to the selected hero's class defaults on hero switch.
> PLUS an owner kit ruling: the Mage plays single-target pull-and-kill and wants POISON, DRAIN (steal
> health), FIREBALL and THUNDER — poison/drain/thunder do not exist in the mage pool today.)*
>
> *(UI-seat bumped 1018 -> 1019 in the SAME edit as the WO-1018 mint — **F8 CAPTURES ARE STILL BEING
> BURIED — WO-965's fix is HALF-LANDED.** The ack script correctly acks oldest-first, but the PRODUCER
> never wrote QUEUE.jsonl entries (3 live WARNs 2026-08-10: "seq=NNNN had no QUEUE.jsonl entry;
> recovered from <file> (producer running pre-WO-965 code?)"), so the pending list is rebuilt from a
> single recovered capture file and the ack watermark then buries the un-recovered older seq. Observed:
> acking with 2313 pending closed 2314 and reported "Inbox clean" — 2313 was never triaged by any seat.)*
>
> *(UI-seat bumped 1017 -> 1018 in the SAME edit as the WO-1017 mint — F8 seq=2314 ERROR: TOWN SYSTEMS
> RUN INSIDE DUNGEONS. `TownActivityProbe.Poll` (`TownActivityProbe.cs:147`) FAILs with
> `suspended=False policy=SuspendAndResume reason='none'` in `Dungeon_HealersCottage` — the scene-driven
> suspension gate never fires for dungeon scenes, so town systems (incl. an Enemy in the active scene)
> stay alive off-hub.)*
>
> *(UI-seat bumped 1016 -> 1017 in the SAME edit as the WO-1016 mint — **HIGHEST (owner F8 seq=2312)**:
> hero locomotion is DEAD in dungeons. Captured proof: world position advances (Zone x=-28.0,z=-1.8 ->
> x=-26.9,z=-4.7) while `[Flow:HeroLoco] vel=0.00 m/s` EVERY frame and the animator holds ONE clip
> `mixamo.com(w=1.00)` with `[Flow:GaitF] speedP=0.00` — the hero SLIDES through the dungeon in idle.
> Velocity source is not fed by whatever moves the hero in this scene.)*
>
> *(UI-seat bumped 1015 -> 1016 in the SAME edit as the WO-1015 mint — EQUIPMENT/paperdoll screen is
> broken: ~40% dead space above the content, the hero PREVIEW BOX RENDERS EMPTY, every slot's label +
> value + hint OVERPRINT each other, and the rogue "Orient" button appears HERE TOO — proving WO-1010 D1
> is a GLOBAL stray control, not a build-mode one. Also the Echoes chip bleeds through the modal.)*
>
> *(UI-seat bumped 1014 -> 1015 in the SAME edit as the WO-1014 mint — TUTORIAL NARRATIVE COHERENCE: TWO
> guide arcs are live at once (the legacy hard-coded "Sylas" human-scout script AND the new {guide}
> pet-Echo founding arc), so two guides spawn, the wolf never introduces itself, the name drifts
> ("Storm"), the wolf does not LEAD the walk, a second wolf is introduced at the entrance, and the pet
> asks for orders before its utility was ever explained. Retire the legacy arc, author the wolf's
> identity, fix the lead + the ask-order ordering.)*
>
> *(UI-seat bumped 1013 -> 1014 in the SAME edit as the WO-1013 mint — "Castle Defense Plans": survive
> wave 2 -> a physical drop at the gate unlocks the Arcane Spire card (starts VISIBLE but LOCKED,
> "Recover the plans") + funds the first build; the player still builds it themselves (reinforces the
> WO-1010 loop); delivered through the WO-1012 contextual one-shot kit; canon: recovered knowledge of
> the fallen civilization.)*
>
> *(UI-seat bumped 1012 -> 1013 in the SAME edit as the WO-1012 mint — tutorial/FTUE presentation + pacing
> redesign: retire the boxed markers / fat top banner / Next-Next coach cards for a spotlight mask + ONE
> chevron / ghost-finger + thin bottom objective strip with beads; the GUIDE is a rotating HERO —
> guide=(playerHeroClass+1)%4 over the HeroClass enum, never yourself, retiring the KayKit "Sylas"
> stand-in; pacing = the owner's dynamic arc (walk with the guide -> build ONE piece -> ONE cannon ->
> the timers, one line -> enemies at the gate -> win + handoff). Bones stay: tutorial-steps.json,
> TutorialFlow, TutorialSignals, Onboarded gate, grants.)*
>
> *(UI-seat bumped 1011 -> 1012 in the SAME edit as the WO-1011 mint — BOARD workflow acclimation for the
> CLI: adopt BOARD.html/board_build.py as the live board (Notion retired 2026-08-08), wire regeneration
> into session boot, canonize the status-line vocabulary + same-commit status hygiene, then sweep the
> ~516-stale-READY status debt.)*
>
> *(UI-seat bumped 1010 -> 1011 in the SAME edit as the WO-1010 mint — build-mode UI redesign from real
> tester feedback ("buttons everywhere"): owner-picked Direction B "Carousel + minimize" (first pick C,
> reversed to B on re-read, 2026-08-08) — card carousel that minimizes to an edge tab on select, contextual
> chips on the ghost, optional D-pad toggle; retires the Rotate/PLACE/Cancel intent bar + always-on D-pad.)*
>
> *(UI-seat bumped 1009 -> 1010 in the SAME edit as the WO-1009 mint — composed-dungeon interactable ART +
> AFFORDANCE pass: chests are gold PRIMITIVE CUBES (BreakableContainer.Create), key pickups + locked ports
> are INVISIBLE triggers (the locked door has NO mesh) — give each a real KayKit prop + a self-explaining
> "what/how" cue. Exit = WO-1007; exit beacon = WO-1008. Owner felt-test 2026-08-08 "dont understand the action".)*
>
> *(bumped 1008 -> 1009 in the SAME edit as the WO-1008 mint — dungeon EXIT beacon must read as LIGHT.
> `DungeonExitInteractable.cs:233` builds a `PrimitiveType.Cube` on `Universal Render Pipeline/**Unlit**`
> (`:284`), so it ignores every light in the scene: invisible as a defect while dungeons were bright,
> screaming since WO-919/1004 dropped ambient to #0a0a10. Owner 2026-08-08: "big green bar doesnt make
> sense". **PAIR IT WITH WO-1007** — that one replaces the archway and explicitly keeps this beacon.)*
>
> ⚠ **COLLISION, RESOLVED 2026-08-08 09:26.** Both seats minted **1007** within two minutes, on the same
> object. `WORK_ORDER_1007_dungeon_exit_real_asset.md` (09:24) was first on disk AND banner-referenced,
> so it KEEPS 1007 per the §2 rule; the beacon WO renumbered to 1008. **This is the failure mode the two
> disjoint blocks were meant to prevent, and it still happened — because both seats were working the
> SAME 1000-block.** The block split protects CLI-vs-UI, not UI-vs-UI. If two sessions are going to mint
> in the 1000s at once, they need sub-ranges or one of them has to stop minting.
>
> *(UI-seat bumped 1007 → 1008 with the WO-1007 mint — real dungeon EXIT asset: replace the primitive
> emerald-cube archway in DungeonExitInteractable.BuildVisual with a KayKit Dungeon Remastered prop
> (lit stone doorway/portal or stairs-up), keeping the walk-in trigger + beacon; distinct from the purple entry.)*
>
> *(UI-seat bumped 1006 → 1007 with the WO-1006 mint — Manage becomes a launcher: the long combined upgrade
> scroll moves OUT into dedicated per-category browser panels reached by buttons on Manage, each row showing
> cost + benefit + time-to-build + affordability, drilling into the existing single-item detail panels.)*
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
