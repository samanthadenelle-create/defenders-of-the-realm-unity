# HANDOVER — the one sheet a new session reads to be productive now

> **Read order for a new session:** ★ the SESSION HANDOVER block immediately below (2026-07-03) →
> this sheet → `docs/MASTER_CATALOG.md` (mandatory, be the SME) → `docs/ARCHITECTURE.md` (the
> architecture hub) → the relevant `docs/MASTER_CATALOG/<area>.md` for what you're about to touch.
> **ALSO MANDATORY before any work:** read `OVERNIGHT_AUTOPILOT_LOG.md` (the overnight run's full
> ledger + open findings) and the auto-memory index `MEMORY.md` (esp.
> `world-architecture-gated-regions-playable-connectors.md` and
> `autopilot-chaos-not-one-scripted-path.md`). The code wins on truth — comments lie.
>
> **Canon maintenance (WO-520, BINDING — CLAUDE.md §15):** the single live anchor is
> `CANON_GROUND_TRUTH_<date>.md` at repo root. Update the relevant load-bearing doc in the SAME
> change as any architecture/state/canon shift (or add a top-of-file `STALE:` flag). Weekly 5-minute
> skim of the read-first set against the anchor. Dated ledgers are frozen — banner, never rewrite.

---

## ★★ SESSION HANDOVER — 2026-07-12 EVENING (mobile-web demo wave) ★★

**WHERE WE ARE:** Branch `wip/village2-and-f8-tickets`; **5 lane commits LOCAL tonight, push HELD**
for the owner's word. NEXT CLI: **read `START_HERE.md` (repo root) FIRST.**

**LANDED (all gated):** WO-678 Pi 120s timeout clean wrap — template unhandledrejection/showBanner
ownership (`66b3272f`) · WO-677+683 build-mode touch verbs — uGUI verb bar; kit d-pad re-hosted on the
build overlay publishing HudMoveInput, merged into the arrow-key move read; "Rotate Left/Right" text
labels; palette chips de-glyphed; AssertBuildMoveChain DPAD probe link (`c963a553`) · hidden mobile dev
unlock — 5 taps on help title → Grant Resources (`33799026`) · WO-682 quiet web errors — db-proven
`Loading FSB failed for audio clip "SwordSwing"` + 167ms/4000ms stalls; 13 Sfx metas swept of WebGL
platformSettingOverrides; AudioService Guard-wrapped + PrewarmCombatSfx on Battle/Arena music cue +
dead-clip quarantine; new SFX_WEBGL_OK oracle (`965309a6`) · docs + **`CANON_GROUND_TRUTH_2026-07-12`
anchor (supersedes 07-08)** (`683b917b`). RESULT files written for 677/678/682/683.

**GATES:** COMPILE_GATE_OK; DataRegression = the 3 known pre-existers only, zero new. Windows build
SUCCESS tonight; at handoff a chain is RUNNING: 4-bot fleet (seeds 8200) + ship WebGL (**NO -DevBuild**
— kills the Development-overlay "giant json failure screen" class) + Vercel preview.

**WEB DEBUG LOOP PROVEN:** WebTrace (`?trace=1`) → POST `/api/trace` → Neon `analytics_events`; CLI
read path = the `[sig]` echo in Vercel runtime logs (`get_runtime_logs` / `vercel logs`) because
DATABASE_URL is sensitive/unpullable. `api/` lives **IN THIS REPO**, gitignored (`C:\EOA\api\`) —
the older "separate React repo" canon is WRONG.

**OWNER RULINGS tonight:** errors caught quietly (never a player-visible failure screen) · build-screen
d-pad = the kit d-pad + text labels · pre-warm combat audio on battle load.

**IN FLIGHT at handoff:** VFX Caster tagging extension — tag effect → Cast/Projectile/Impact key via
manual-overlay JSON, generator merges manual-wins.

**OPEN:** owner felt-pass on the new preview → push authorization · WO numbering authority refresh
(next free 684; 677/678 collisions) · loader-error beacon idea · preview SSO bypass friction.

---

## ★★ SESSION HANDOVER — 2026-07-11 F8 BATCH + ACTION KEYWORD REGISTRY (⚠ superseded as newest by the 2026-07-12 EVENING block above) ★★

**WHERE WE ARE:** Branch `wip/village2-and-f8-tickets`. Origin sits at `369c4f30`; local commits through
`10c60eb3` (push held for owner word). **Save schema = v29** (heroLevel/heroXp/heroLifetimeXp — F8-47).
**Felt-verify exe = `Builds/Windows/DefendersOfTheRealm.exe` stamped 2026-07-11 15:02:34.**

**LANDED (owner's 1:30 PM F8 batch, all RCA-proven-by-data):** F8-47 level-reset-on-outpost-return =
save-v29 persistence (`4064a44e`) · F8-43 compact-banner Continue CTA removed + F8-45 damage report
(WO-38 repair prompt self-installs into real wave scenes + WaveDamageReport rows w/ repair costs +
collector damage scales accrual — owner: "damage to collectors reduces economy") (`761d1d16`) · F8-46
option A: pursuit raises BattleLock via PursuitBattleProbe (`431f3ea0`) · F8-44 20-wave schedule, Syndrath
at wave 20, Necromancer cadence 6/12/18 (`c768fe6a`) · RepairTarget undeclared-HeroTarget-tag fix — latent
§7 violation woken by the F8-45 install, fleet-verified GONE (`10c60eb3`).

**NEW ARCHITECTURE:** `docs/ACTION_KEYWORD_REGISTRY_ARCHITECTURE.md` (BINDING for motion work) —
keyword→action registry (`motion-castings.json`, dual-copy): targets×keywords→{clip,vfxKey,sfxId,
vfxDelay,attachBone,playOneShot}, manual:true = owner canon, bake-time V1 / runtime Phase 2. Foundation
committed (`941ef16c`): MotionCastings resolver + ActionKeywords + builder seams (empty registry =
byte-identical bakes) + EditMode gate tests. WO-670 = Motion Caster authoring window (lane in flight),
WO-671 = action bundle rows + runtime ActionBundlePlayer (lane B done in tree, uncommitted). §9a of the
arch doc = the Grok Action System adopted/rejected ledger.

**FLEET (exe 15:02:34):** clean of new tickets; remaining = known pre-existers only (WO-453 encounter
strand, WO-602 home return, CavePortal reach) + AssertTutorialFirstTower probe drift (candidates occupied
by the 07-10 Colosseum_ArenaEntrance — player placement fine, probe fix filed). DataRegression: 3
pre-existers (arena ground texture → F8-37 evidence · B2 dual-wallet · pet-slot flag_17), zero new.

**NEXT:** owner felt-pass on exe 15:02:34 → push on her word · WO-670/671 lanes gate+commit ·
open owner pins: F8-40 max-tier tower identity · WO-614 tree→W/E/R rail seam · probe fix.

**MORNING ADDENDUM (2026-07-12 — current felt-verify exe = 2026-07-11 23:51:48):** overnight
session delivered: (1) **Regroup death-cycle FIXED** — RCA proved one death fired two racing
recovery systems (arena loss-return left the death latch set under ff.noautoheal; HeroHealth's
respawn double-warped); arena now owns loss recovery, HandleDeath defers w/ 10s net (36acc05f).
(2) **Registry-only motion VFX** (owner directive): all abilities.json Vfx* defaults + the
hardcoded per-swing Melee_Slash burst OFF; the ONLY VFX authority = owner Motion Caster rows,
now wired to runtime for the first time via ActionBundleCatalog (17862c51). (3) **Movement feel
restored** — f7740f4e's per-frame velocity snap rate-limited to 540°/s (ee52e399); stale-clip F8
sentinel retired. (4) **F8-49 ROOT-FIXED** — 19 Lana Studio mats upgraded to URP at source
(LANA_URP_FIX_OK, b5694a05). (5) **SME PROGRAM: all 8 pack dossiers written + committed** —
router `docs/SME/README.md`, ledger of all 34 store products; headlines: Hovl demos run Bloom 5
vs our Bloom OFF; Blink's rigged orc bundle + 608 icons unused; KayKit 33 rigged characters
unused; polyperfect 240 rigged villagers unused; ⛔ apex-dragon model CC BY-NC (license/replace
before commercial release, memory + dossier); Raid BGM dead-wired (~8-line fix, AUDIO_SME).
(6) **WO-677 minted** — Asset Caster toolkit family (Icon/Gear/Audio/Character/Texture Casters),
Phase 0 applicability assessment ready to run on the dossiers. Tools shipped earlier same night:
VFX Caster window + Motion Caster preview gear/mocap filter. 24 commits local, push HELD.

**NIGHT ADDENDUM (2026-07-11 late — superseded as newest by the MORNING block above):** the orc
frozen-bones family is CLOSED: RCA proved loose-part Tripo exports (mesh not skinned to the animated
skeleton — no importer fix exists); owner re-exported Warrior/Tank/Mage via AccuRig (proper
pelvis/spine skeleton); ImportOrcFamily verdicts = ENTIRE family "OK Humanoid" incl. the previously
unrepairable Berserker; the fleet's standing Berserker rig warning is GONE. Grok session landed 8
commits (T-pose take stripping, walkforward01 calm gait rework, post-combat facing/sheath sync,
camera recenter, VFX stacking, AccuRig Tank) — reconciled + gated. Motion Caster is now owner
self-service (bundle preview w/ VFX-on-bone, SFX audition, one-button FBX intake with per-take
T-pose + root-travel warnings). Grok escalation pack: `logs/debug/BROKEN_ITEMS_2026-07-11.md` +
`GROK_ESCALATION_2026-07-11_orc-rig-family.md`. Queued next: vfxEuler rotation dial on bundle rows ·
WO-674 walls · WO-675 panel · WO-673B fast-follows · push authorization.

**EVENING ADDENDUM (same day, waves 2+3 — superseded as newest by the NIGHT block above):** the 15:02
batch was FULLY felt-closed (F8-43/44/45/46/47 all owner-verified). Then landed, gated, fleet-clean
(only the 3 known pre-existers): **F8-48** Mend heals (28/28 casts were move-interrupted; now instant +
real cast take, `5c7782f9`+anim) · **WO-672/F8-50** unified damage lifecycle (hp==0 = broken shell
everywhere, damage bars + Ember smolder/fire tells, Raid_Explosion on break, `damage-states.json`;
Repair All on the wave report via the one crystal spend path; `80a2f944`+`1b3224f6`) · **F8-49** 135
built-in legacy particle slots URP-swapped at source via re-runnable MagentaMaterialFixer pass
(`15b8bf30`) · **owner clip picks ×5** (Leap=jump-stab, W=Slash, Block=swipe01→02 chain, Heal=
magespellcast-02, Fireball=magespellcast-04) — SwordShieldMovesImporter extracted 12 clips (first
Magical Moves extraction), KnightPackage rebaked, `[MotionCaster] (manual)` consume lines proven
(`54d5e9fd`) · **Q medallion "Dodge/Attack" text placeholder** (`977b3737`) · **endless waves past 20**
(owner ruling: manual DEFEND starts, stats+counts scale, apex returns as cycle capstone; `04481c59`) ·
fleet self-blindness fixes (probe validates via the real placement gate `2990aaf6`; bots reset wave
progress `d2f57867`). WO-670/671 committed (`8a0bdddd`/`8084d8ee`): Motion Caster window + runtime
ActionBundlePlayer. Open: WO-614 rail seam · F8-40 · pet-slot persistence · B2 dual-wallet · arena
ground texture (F8-37 evidence) · broken-state save persistence follow-up · push authorization.

---

## ★★ SESSION HANDOVER — 2026-07-08 WAVE-2 CLOSE (⚠ superseded as newest by the 2026-07-11 block above) ★★

**WHERE WE ARE:** Branch `wip/village2-and-f8-tickets`, **HEAD `d944d161`, 71 commits ahead of origin,
clean tree, push HELD for the owner's word.** Live anchor = **`CANON_GROUND_TRUTH_2026-07-08.md`**;
wave-2 prep + open board = **`CLI_PREP_2026-07-08_next-session.md`**. The overnight P0 fix (below) is
**owner felt-confirmed** (tutorial completes, placement works).

**WAVE 2 CLOSED (05:10):** all morning F8 lanes committed through `bb0094cc`; final fleet on exe
**2026-07-08 05:10:11 = ZERO tickets, all probes PASS**. Landed since the overnight sweep: F8-24 castle
wall-stairs swept from the SHIPPED merged scene + navmesh rebaked (`13e85e12`), F8-31/32 nameplate GUID
repair + portrait circle-mask, F8-33/35 Victory rows + BR ability icons, F8-15 death forensics
(`[Flow:DeathTrace]`), F8-6 tree pose, "Tap to continue ▸" passive hint replacing the Continue chip,
WO-614 skill-tree rulings stamped, fleet self-fixes (ArcaneTower fake-null NRE, popup oracle tap-advance
contract, compass ForceProviderPoll). **WebGL PREVIEW = https://defenders-of-the-realm-v2-h0h6hfsf5.vercel.app**
(from `bb0094cc`, READY; supersedes `2dizrqgws`). **Production untouched** (07-01 Pi build) — promotion +
push are the owner's. Fresh HEAD gates this session: `COMPILE_GATE_OK` + `REGRESSION_OK`. Save schema **v28**.

**NEXT:** owner felt-pass on exe 05:10:11 → name passes → push. Big next lane = **WO-614 skill-tree solo
rework** (RULED, READY). Open owner directives: F8-40 max-tier tower identity · F8-41 waves attack the
city · F8-42 repair costs (all in `CLI_PREP_2026-07-08_next-session.md`). Pre-existers unchanged: WO-602
home-return, CavePortal seam, WO-453 rep spawn.

---

## ★★ SESSION HANDOVER — 2026-07-08 overnight verified-root-cause sweep (⚠ superseded as newest by the WAVE-2 CLOSE block above) ★★

**WHERE WE ARE:** Branch `wip/village2-and-f8-tickets`, ~75 local commits through `7e663981`,
**push HELD for the owner's morning word**. The owner's night session ended with a BINDING directive
(now memory `step-in-step-out-verified-root-cause-every-bug` + TICKET_PIPELINE rule 0): every
reported-broken flow gets step-in/step-out gate instrumentation + a REAL-PATH automated probe, and
closes only with TWO verbatim captured lines (root cause + post-fix verification).

**THE P0:** "still cant do the tower" — dialogue Closed re-entrancy destroyed the successor
dialogue's panel → `InputSuppressed` stuck → build-mode Update frozen, zero click evaluations.
Fixed with a per-VM Closed identity guard (`82422d11`); every placement gate now names itself when
it blocks (`aec9feca`).

**VERIFIED (final exe 2026-07-07 23:50:04, 4/4 fleet runs, real input seams):** 8 new probes ALL
PASS — first-tower placement chain, dialogue chain survival, tutorial arms on fresh save (F8-29),
orient-modal release (F8-30), wave vendor rules (F8-14), compass pips (F8-16), scatter bands (F8-8),
hero albedo 19/19 with no WHITE HERO ROOT (`f4aeae8c` probes, `7e663981` retired the -nographics
HasProperty false-Fail — audits now read the serialized material sheet). Remaining fleet tickets =
the 3 known pre-existers only (WO-602 home-return, CavePortal seam reach, WO-453 rep spawn).

**FULL LEDGER:** `RESUME_2026-07-08_overnight-f8-sweep.md` (the morning report — verify list +
verbatim-line ledger + the honest open list). WebGL rebuilt from `7e663981` and deployed to the
**Vercel PREVIEW**: https://defenders-of-the-realm-v2-2dizrqgws.vercel.app — production untouched,
promotion is the owner's call. Windows felt-pass exe: `Builds/Windows/DefendersOfTheRealm.exe`
stamped **2026-07-07 23:50:04**.

---

## ★★ SESSION HANDOVER — 2026-07-07 evening F8 batch (⚠ superseded as newest by the 2026-07-08 overnight block above) ★★

**WHERE WE ARE:** Branch `wip/village2-and-f8-tickets`, 5 lanes committed LOCAL (`26cc6d47` →
`90541989`), **push held for owner felt-pass**. The owner's evening felt-test produced 7 F8 flags +
3 chat directives — all triaged live via the F8 watcher (QA read-only RCA agents, every fix
§12-data-proven), implemented, and verified: COMPILE_GATE_OK + REGRESSION_OK + fresh build + 4-bot
fleet (13/13 panels, popup-close clean, vendor talk-route 0 violations, combat invariants PASS).

**LANDED (ticket → commit):** F8-2 wizard tower z-90 (`26cc6d47` — orientation was authored but
inert, manual=false; now euler Z-90 + manual=true, and ReskinForLevel no longer applies base-authored
euler to tier models) · F8-6 wood node y+90 (`25da4062` — per-Wood LocalRotation pre-SeatFlat at
MineNodeVisual + HarvestSite) · F8-4 black interact removed for buildings (`3b795bf0` — NPC Talk is
the one path; uncovered buildings self-report via Warn; the old cover gate leaked on null hook ids
Apothecary/JewelersBench) · F8-7 target frame hides without a target + moved to its designed
TargetInfo zone, compass keeps Status in combat postures (`e01553aa`) · F8-3+F8-9 attack pill art
(root cause: icon_energy_sword shipped textureType:0, never a Sprite → fallback to old icon_sword;
proof Player.log:15629) + first-ever currency icon mirror, all five owner picks (gold=Blink
Gold_Currency, wood/food/crystal/iron=HudIcons) (`90541989`).

**HELD OPEN:** F8-1/F8-5 dialogue Close seat (RCA proves box-in-a-box in DialogueView.BuildUi —
inner DialogueInterior plate + frame + Close seated at the plate floor — but the current tree's
seat math may already differ from her build; adjudicate from a fresh capture before editing) ·
F8-8 roaming enemy families + danger gradient (owner directive, canon-grounded — needs a spec WO;
consolidate the divergent RegionMobSpawner/EnemyOutpost/GarrisonController stat blocks into
enemies.json as part of it) · F8-10 PartyBar 'Grom' label 0-glyphs (fleet 4/4, pre-existing class).

**VERIFY NEXT (owner):** fresh build at Builds/Windows — wizard tower roll, log-pile yaw, attack
pill = pixel energy-sword, no black interact at storefronts, compass visible in combat + target
frame only with a real target, resource chips show the five icons. Board = Task list tickets
F8-1..F8-10 with full hand-off logs.

**WAVE-2 ADDENDUM (2026-07-08 late night, commits `908add29`→wave end):** owner directive "get
everyone working" — 10 parallel agents, one batch gate, ONE build (owner ruling: no piecemeal
rebuilds — memory `one-build-one-handoff-never-retest-stale`). Landed: F8-8 scatter enemy families
(18 seeded records, 3 danger bands, sight-instantiated 85m/cull 115m — runtime traces pending owner
session) · F8-1/5 dialogue rebuilt on FrameCore (interior plate deleted) · F8-14 wave rules (vendors
hide, shops closed toast, build timers verified wall-clock) · tower identity (Ballista 22-range
physical BOLTS, Arcane CASTS — orb + Aether blast on arrival, new repo.projectileStyle) · F8-29
tutorial bootstrap fixed (one-shot hub gate on Title = V2 never constructed; now sceneLoaded re-arm)
· F8-10 PartyBar label (fleet-verified GONE) · F8-13 watchdog build-mode gate · F8-21 harvest verbs
· ff.combathud611 default ON · flag screenshots session-stamped (evidence-loss fix) · WO-613B
outpost chunk spec. Fleet: 3 confirmed = pre-existing knowns only. RCA docs:
docs/STRUCTURE_TRANSFORM_CENSUS_2026-07-08.md (+risks R1-R6, R1 = fit-before-upright). Exe 21:39:47.

**LATE-SESSION ADDENDUM (2026-07-08, commits `b45bb0bb`→`c7d913a3`):** RCA-PROOF-BY-DATA is now
BINDING pipeline rule 0 (`75e4d128`; owner directive — every ticket carries verbatim proving lines).
Landed: F8-11 DevTools scroll + yarn row removed · F8-12 dock pinned to real size (was ~5% of screen
— fraction-of-parent in the tiny Dock mount) · Wizard Tower → **Ballista** (owner ruling; upright
X-90; Ground placement — was stuck on the old WallWalk rule, 'stays red' RCA `164d0c24`) · Arcane
Spire base euler (-90,90,90) + WHITE FIX (extraction never ran + the remap step was never in the
code; new single-asset extract+remap+save, externalObjects verified — `f23d05ae`) · **Orient tool
saves locally** (StructureOrientationLocalStore, persistentDataPath overlay wins at catalog load —
the gear-offsets pattern; `96a90054`) · Ballista card art ×3 transparent (`917e8d23`) · F8-15 stage-1
death slow-trace (listener dump + down-beat milestones, `e95c538d`) · owner gear/sheathed harvests
(`c7d913a3`). RCA doc: `docs/RCA_DIALOGUE_DOUBLE_FRAME_2026-07-07.md` (owner decision pending:
option A = window frame + delete the interior plate). Board tickets F8-11..F8-16 filed. Exe built
2026-07-07 late evening carries ALL of it — the owner was felt-testing a stale exe earlier (proof:
session_start 19:24 vs exe 19:53); RELAUNCH before judging.

---

## ★★ SESSION HANDOVER — 2026-07-07 offset persistence (⚠ superseded as newest by the evening F8 batch above) ★★

**WHERE WE ARE:** Branch `wip/village2-and-f8-tickets`, **PUSHED** (owner logout handoff). Latest:
`0492d7dc` **fix(gear): local offset settings persist + immediate re-equip on save** — stacks on
overnight `88d6fbc9` (WYSIWYG scale parity + `@sheathed` registry + Seating Editor Drawn/Sheathed
toggle).

**WHAT CHANGED (gear offsets — owner ask: "save should stick in town immediately"):**
- **Local settings file (primary authority):** `Application.persistentDataPath/attachment-offsets.json`
  — every in-game Save writes here; entries **win over** shipped `Resources/OffsetForge/offsets.json`.
  Legacy `offsets-dev.json` auto-migrates on first boot. **PlayerPrefs** mirror: `dotr.attachment-offsets`
  (restores file if deleted).
- **Always fresh on apply:** `AttachmentOffsetRegistry.Reload()` runs before every `EquipBestForHero()`
  (scene load, gear swap, post-save re-seat).
- **Save = immediate re-equip:** Seating Editor Save persists → reload → full re-attach from file
  (not preview-only). Status shows the local path.
- **Town carry fallback:** when no `<mesh>@sheathed` entry exists, drawn keys (`sword_A`, `shield_A`)
  nudge the built-in back pose so hub/town isn't a second ignored orientation system.
- **Seating Editor default:** opens in **Sheathed** mode when hero is out of combat (town view).

**KEY FILES:** `AttachmentOffsetRegistry.cs`, `EquipmentController.cs` (`ApplySheathedOffset`,
`SaveSeating`, `TryResolveSheathedOffset`), `SeatingEditorOverlay.cs`. RCA:
`docs/RCA_WEAPON_OFFSETS_2026-07-07.md`.

**VERIFY NEXT (owner):** launch build → town → Seating Editor → dial → Save → walk hub without restart;
pose should match saved file. Optional fine-tune: explicit `sword_A@sheathed` / `shield_A@sheathed`
entries for perfect back pose (Drawn/Sheathed toggle).

**A/B ADDENDUM (2026-07-07, pre-felt-test review):** the `0492d7dc` drawn→sheathed fallback composed
the HAND-frame drawn euler (e.g. `sword_A` (117,-61,-111)) onto the chest-socket back pose — a frame
mismatch flagged in review. Now flag-gated: **default = position-only nudge** (frame-safe);
**`ff.sheathdrawnrot`=1 = the full pos+rot compose** as the backup if position-only doesn't carry the
town fix. Explicit `@sheathed` entries identical under both. Also: legacy `offsets-dev.json` is deleted
after migration (stale-shadow guard) + the RC3b Resources-first banner restored in
`AttachmentOffsetRegistry.cs`.

**COMMITS PUSHED THIS HANDOFF:** `75bffabd` → `88d6fbc9` → `3b4cfeac` → `b5547351` → `0492d7dc`.

---

## ★★ SESSION HANDOVER — 2026-07-06/07 (⚠ SUPERSEDED by offset block above for gear; UI/HUD lanes still valid) ★★

**WHERE WE ARE:** Branch `wip/village2-and-f8-tickets`, 4 lanes committed + PUSHED (owner-authorized
for the demo recording): **WO-611 combat HUD** (owner v8 design: inset vitals well, d-pad cross, attack
pill with owner pixel energy-sword art fit-to-frame, medallion arc, lock crosshair, hostile→CloseAll
incl. wave countdown; F1 blank-icon guarantee; F3 truthful enemy Level), **WO-612 build timers**
(WO-172 service finally wired at its documented seam — 15 s base, 2 slots, offline-fair scaffold+
countdown; `ff.buildtimers` ON; owner direction = grow to option-3 "free income": rewarded-ad skips,
never a wall, no real player cost), **tier-model reskin** (`StructureFactory.ReskinForLevel` — the
write-only `upgradeVisualPath` is now consumed on upgrade + reload; owner F8 "upgrade just makes it
bigger" fixed), **3-type palette** (Archer/Wizard/Arcane; catapult/siege/walls/gates filtered,
reversible), owner card art + Tripo tower models (Wizard base + ArcaneSpire 1→2→3; force-added — the
Structures folder is gitignored for polyperfect mirrors, Tripo assets are owner-sourced), **archer
Tribal ladder** (`_bug22` RESOLVED via CatalogPrefabImporter `_T`-root support), owner Seating-Editor
offsets harvested, **'K' high-level scatter rig** (Lv15–27, 120–200 m out, hold-ground, skull plates),
`ff.skrpreview` ON for the demo (panel self-labels PREVIEW·TESTNET).

**VERIFIED:** COMPILE_GATE_OK ×4, REGRESSION_OK, 4-bot fleet — 13/13 panels, popup-close clean,
economy/equip/save green; only pre-existing errors (white Paladin albedo, WO-602 home-return,
WO-453 encounter strand). **HONEST FINDING (owner asked):** there is NO hero-vs-enemy level-delta
damage rule — level = authored-HP band only; damage is stat-driven. A real level-gap curve = open
owner design decision. **OPEN F8 TICKETS (board #1–5):** town vitals bars outside plates (RCA done:
shared BuildPartyNameplate StatBars inset insufficient — ElarionUiKitNameplate.cs:133), XP bar +
Wisdom "434" chip unlabeled (+ TWO XP bars redundancy), resource rows without identifiers (WO-611 F4
currency art), build-palette proportions, death-panel overlap. **NEXT:** owner demo recording →
ticket batch #1–5 → WO-613 VFX moments (overnight spec READY) → WO-545 Addressables.

---

## ★★ SESSION HANDOVER — 2026-07-05 (⚠ SUPERSEDED by the 2026-07-06/07 block above) ★★

**WHERE WE ARE:** Branch `wip/village2-and-f8-tickets`. This session landed the **AccuRig skeleton
family** (Mage / Warrior / Ranger→`Skeleton_Rogue` / Healer), `SkeletonHumanoid.controller`, codex
catalog updates, **hollow-warrior → Skeleton_Warrior** with stats tuned off bruiser, and **proportional
sword/shield** on the hub knight (`EquipmentController`). The **rig importer** now self-verifies —
`PeopleCharacterImporter.ImportSkeletonFamily` runs a per-model avatar verdict (OK Humanoid / WARN
Generic / FAIL) + 3-pass bone-map repair, so a missing/mismapped bone surfaces at import, not as an
in-game T-pose. KayKit legacy kept for Minion / Golem / Necromancer. Earlier the same day, two
**hero-feel fixes** landed: (1) **walking animation / turn-clip conflict** — the `turnleft180`
turn-in-place clip (low-pivot, reads as a crouch) was fighting the walk-forward clip when the hero
turned while walking; fixed by making turn-in-place clips combat-only + slewing town facing by input
+ a town walk-speed cap so KnightMocap stays on the upright `Shared_Walk_Forward` gait (`86847b7f`);
and (2) **native sword grip** (SeatNative, `ff.weapongripinfer` rolled back — `d48bfd41` WO-478).
(Separate same-session combat-anim work: posture flip + directional death — `315d60e3` WO-609 /
`38c7fd4b` WO-586.) **Committed locally; push held for owner felt-pass.**

**VERIFY NEXT:** mixed hollow wave in Windows build — four silhouettes animate, warrior feels mid-tier
(not golem), knight gear scale. F8 queue still open (HUD left panel, Forge mobile, battle posture flip).
Full notes: `RESUME_2026-07-05_skeleton-family-handoff.md`.

**IMPORT (if re-exporting skeleton FBX):** `Defenders → Animation → Import Skeleton Family (AccuRig)`
or batchmode `PeopleCharacterImporter.ImportSkeletonFamily`.

---

## ★★ SESSION HANDOVER — 2026-07-03 (⚠ SUPERSEDED by 2026-07-05 block above) ★★

**WHERE WE ARE:** Branch `wip/village2-and-f8-tickets`; the 07-02→03 **convergence session** (~25
specialist agents, two felt-tests, 46 F8 flags all triaged) is in the tree, uncommitted — commit lanes
being staged by explicit path, **push held for owner felt-pass**. Current focus = **THE FEEL ARC**
(owner: "the most important thing is how it FEELS"; the ten-year-old test is the standing quality bar) —
this supersedes web/Pi stabilization as the live thread. Owner verdict after the convergence build:
"I love the terrain… feels like there is something real now."

**LANDED (fleet-verified):** **south vertical slice 6/6** round trips, `tapped=False` (masked warp
mid-bridge both ways — the natural raise→moat→water→bridge seam works; N/W/E waits for south "feels
perfect"). Post-processing was structurally DEAD until 07-02 — fixed (WorldFeelInjector, `ff.worldfeel`,
dusk palette) + terrain relief/treelines. Character/combat/UI feel passes across ~50 systems (double-sided
materials, anim cadence/smoothing, HUD-bleed fix, NPC cards, vendors data-mapped, WO-596 bug report,
end-state template). **Tutorial V2 BUILT** behind `ff.tutorialv2` (default OFF — flip after its own fleet
pass). Vercel preview = full convergence build; prod stays on the 07-01 Pi build until promoted.

**BINDING RULE RATIFIED:** **read-before-assert for EVERYTHING** — code and non-code; memory lines are
pointers, never answers. Plus the extended UI canon (earns-its-place, one action = one button, no dead
buttons, shared currency chip) and "what CAN stream, SHOULD stream."

**OPEN OWNER DECISIONS:** un-park seam un-stack WO-453 (encounter-return strands ~7.1km, publisher
critique #1) · promote preview→prod · push authorization · wall stairs · ramp decks · necromancer 50%
beat · caster cast clip · dungeon theme · CastleMoat default-ON.

**NEXT:** owner south walk → commit lanes → **WO-545 Addressables streaming**
(`docs/WEBGL_DELIVERY_PLAN_2026-07-03.md`). Resume doc: `RESUME_2026-07-03_morning.md`.

---

## ★ SESSION HANDOVER — 2026-06-28 (⚠ SUPERSEDED by the 2026-07-03 block above — kept for history) ★

**WHERE WE ARE:** Branch `wip/village2-and-f8-tickets`, HEAD `7c05cd1b`, **nothing pushed**. The
single-Knight-pivot arc plus the WO-560→584 arc landed: overworld real-time **BattleArena** (lock-on
WO-512, 9-zone HUD, victory/defeat + star rating), **Echo workforce** wired (1–4 echoes, offline
real-clock, **save v27**: v26 ring/amulet, v27 wall-mount seating), **village-tier upgrade** unlocking
the WO-432 building tree, **store redesign** (WO-501) + **gear balance** (WO-500), **Offset Forge** offsets
on weapon attach (WO-490/510), **castle moat + 4 drawbridges** (`ff.castlemoat`) + **four-side warp gates**
(RuntimeRegionGate) + **tree aura/tower glow** (`ff.hubambientvfx`). **WO-560→584 arc:** UI Blink
master-frame template (`docs/UI_BLINK_TEMPLATE_CANON.md`, BINDING), **title rebrand WO-570** ("Echoes of
Elarion" / tagline "Hold the last light."), **WO-584 dungeon/outpost/arena consolidation** (one warp-in
space primitive, 3 skins, resolver + ownership flip — replaces flat ATB dungeon, `ff.atbdungeon` OFF),
wave-loop-in-hub. itch web build is LIVE; Vercel parked.

**IN-FLIGHT (carried from the 06-26 snapshot — re-confirm against HEAD before trusting):** the
hero-priority structure sweep (`ff.enemystructureaware`) was **UNVERIFIED** (0 sweep acquires) as of
`8aa24c32`; HEAD has since advanced ~30 commits — re-check its status before pushing. Verify any untracked
`.cs` triage state with `git status` rather than assuming.

**CANON STATE (the corrections that supersede everything older):** hero = **single Tripo self-rigged
Knight ("Grom")**, static armor, no mesh-swap — **Blink hero rig JUNKED 06-22** (Blink = UI re-skin only).
**ATB is flat/static**; animated combat lives in the **overworld BattleArena**. Base-defense/tower-defense
= **V2-gated** (`ff.basebuilding`). **Defend-the-Tower/PatriciaLight REMOVED 06-09.** Yarn being dropped
(WO-455). Home = `MainCastle_Hall`; `Village2` = raid target; `Village.unity` ABANDONED.

**QUEUED (captured, not built):** WO-509 functional N/E/W moat seams · WO-513 coordinated orc family ·
WO-514 tower cap + Population→Saved-Echoes→SP + siege-AI (mobs target towers) · WO-430 offline-garrison.

**CREATIVE FORK CLOSED (owner ruling, WO-520 / memory `canon-maintenance-wo520`):** the **living
world-Tree** is canon (NOT the Cathedral Spire) — STORYLINE/DESIGN-DECISIONS "Spire replaces the Tree" is
SUPERSEDED. See `CANON_READINESS_LEDGER_2026-06-26.md`.

**RESUME POINT:** finish the targeting proof on HEAD → triage the two untracked `.cs` → then WO-509/513/514.
Full doc-canon reconciliation = **WO-520** (`CANON_READINESS_LEDGER_2026-06-26.md`).

---

## ★ SESSION HANDOVER — 2026-06-19 (⚠ SUPERSEDED by the 2026-06-26 block above — kept for history) ★

**WHERE WE ARE:** A long session (architecture + core-loop fixes + an overnight autopilot run).
Owner is rebooting her PC (an OS/audio patch — her machine audio, NOT a game bug; she has working
Realtek endpoints, it was a default-output-device thing). When she's back she will do a **manual
playthrough to validate before we push.** NOTHING below is pushed.

**⛔ DO NOT PUSH** until the owner confirms her playthrough passes. 6 local commits await review:

| Commit | What | Verified? |
|---|---|---|
| `10282535` | core-loop: enemies fight (partial-path + chase fix) + clear→claim→companion + **interim** travel-tap | enemies/claim: logic-only; travel-tap is TEMPORARY |
| `fd9314af` | WO-449 "continuous walk" loop | ⚠️ **built on a FALSE premise — see below; NOT working** |
| `62a8bb88` | Blink rig migration (armor on the playable hero body) | compile-clean, **NOT felt-verified** |
| `3a3e4aeb` | armor fix (bodyless-hero swap + re-entrant Addressables release) | ✅ verified resolved (overnight) |
| `721da6c5` | autopilot stale-log wipe (harness truth fix) | ✅ verified |
| `14a70111` | TriggerWave probe-flake fix | ✅ verified resolved (overnight) |

**THE BIG CORRECTION (do not re-trip on this):** the WO-449 walk loop (`fd9314af`) was built on the
premise "OuterWorld is one continuous NavMesh you walk freely." **The overnight autopilot FALSIFIED
that.** Reality (RCA-confirmed, in `OVERNIGHT_AUTOPILOT_LOG.md`): MainCastle_Hall and OuterWorld are
baked **STACKED at the same origin** (the DUAL-NAVMESH error, 12/12 every run), and the castle→
OuterWorld crossing is a **WARP by design** (`SceneTransitionTrigger` disables→warps→re-enables the
agent). So a continuous castle→outpost walk **is not possible in the current layout**; the warp lands
the hero in the overlap (0,0.5,-12), far from the ±70 outpost anchors, and the outpost never realizes
headless → **zero outpost/combat/walk coverage**. This is the **WO-453** cluster. DO NOT auto-"fix"
the dual-navmesh/gate-island/warp — it's owner-led world-architecture work.

**WO-453 = THE NEXT BIG THING (design ratified, spec not yet written).** Full canon in memory
`world-architecture-gated-regions-playable-connectors.md`. In one breath: the world is **HYBRID
GATED REGIONS** — 2–4 navmesh-stitched low-poly scenes per region, sized by a **measured** memory/
frame budget (not a scene count), **seamless WITHIN** a region, with **NATURAL/DIEGETIC gates BETWEEN**
regions that are usually **playable connectors** (cave/tunnel/gatehouse) doubling as load-mask +
spatial bridge + content. Mobile-first consensus (even Genshin gates between regions). A **danger
gradient** soft-gates (tougher enemies toward the outward gate; "get stronger before venturing
further"). Loss = **Elden-Ring drop & recover**: die → drop unbanked XP/currency + unequipped loot
(NEVER equipped gear; keep claims), compass marker to the cache, recover before a 2nd death OR **pay
tribute** (Echo retrieves it; can't afford → harvest locally or risk the run; big cache = 2 Echoes).
Mobile guards: an interruption must not count as the 2nd death; respawn distance scaled, not a trudge.
**Owner's locked picks for Region 1:** gatehouse/portcullis gate · **wooded** first region · death =
harsh-but-recoverable (Elden-Ring style). FIRST STEP when resumed: write `WORK_ORDER_453` for Region 1
(prove ONE seam: castle→connector→wooded region→walk to a visible, guarded outpost, with a perf-budget
probe + chaos-fleet oracles), then replicate the convention per region. NOT a blind build — confirm
the seam approach matches the canon, then go.

**OPEN, DEFERRED TO OWNER (not blind-patched):** the dialogue `Stop()`-race (`No node has been
selected` + TMPro NRE, intermittent). Full RCA + two fix options in `OVERNIGHT_AUTOPILOT_LOG.md`
ledger. Yarn content is correct; the real fix touches the Yarn runner lifecycle → owner decides.

**THE OVERNIGHT AUTOPILOT RUN (done, terminated 06:46):** 13 cycles, 168 bot runs, fire-and-dormant
via a session cron (now deleted). It found+fixed 3 things (the 2 verified ✅ above + the harness wipe),
verified the armor fix, deferred the dialogue race, and proved a STABLE hub baseline. Coverage is
HUB-ONLY (WO-453 blocks the rest). The chaos design (seeded per-bot, fixed oracles) is canon —
`autopilot-chaos-not-one-scripted-path.md`. The loop self-validated cycle 1 by hand and caught a
harness bug (stale appended logs faking "fixed bugs reappearing") — the lesson: **validate the
harness before trusting its metrics.**

**HOW WE WORKED THIS SESSION (the behaviors that earned trust — keep doing them):**
- **Instrument, don't guess** (§12). When the walk/armor "didn't work," we traced + RCA'd from real
  capture data, not hypotheses. We split "shows nothing" into data-empty vs built-but-invisible vs
  threw-and-skipped *before* touching code.
- **Validate before claiming; verify before pushing.** The autopilot caught that the armor fix only
  *looked* unresolved (stale logs) and that the walk premise was wrong — before the owner wasted time.
- **RCA-gate every fix; defer the deep/risky.** Not everything gets an autonomous fix — the dialogue
  runner-lifecycle change was deferred rather than blind-edited at 1am.
- **Deliver complete + verified, not piecemeal** (memory `deliver-complete-verified-not-piecemeal`).
  "Rather be right than ran many times." Confirm the felt bug is gone before reporting done.
- **Read the embedded canon FIRST** (memory `read-embedded-canon-first-or-owner-pays`) — don't
  re-derive what's already in the catalog/docs/memories; the owner pays for rework.
- **Structural/creative forks are the owner's call** — name them explicitly (we used AskUserQuestion
  for the walk approach, rig migration, region fiction). When we guessed a structural direction
  without confirming (the original Travel-button), it was wrong and cost a redo.

**RESUME POINT (do this in order):**
1. Owner reboots → does her playthrough against the validation list (below in this block / I gave it
   in chat). She'll **F8-capture** any failure.
2. For each failure: RCA from the F8 break-log + screenshot (`break-log.jsonl`), fix, gate, commit by
   explicit path. **Push only the items she confirms pass.**
3. Then start **WO-453 Region 1** (write the spec first; confirm the seam approach vs canon; build the
   one proven seam + perf probe + oracle; replicate).

**THE VALIDATION LIST (what the owner is checking — test in the EDITOR, not the exe: Play mode
resolves Addressables via the asset DB so the Blink body/armor load without a content build):**
- ✅ **Hotkeys stripped** — only WASD/arrows, weapon skills, F8, F9 do anything (F1/F12/J/K/L/etc.
  dead). F9 green overlay is EXPECTED.
- ✅ **Armor on the playable hero** — where the hero is the real Blink body, it wears its class set
  (Knight=Centurion, Ranger=BeastHunter, Mage=Dragonic): no T-pose, not naked, not a personless
  mannequin; weapon+bow in the hands. *If the hub/start hero looks like the old placeholder, that's
  expected* — the Blink body builds in the gameplay context (HeroBodySwapper), not the hub.
- ✅ **Enemies fight** — they detect, chase, and land hits (no freezing at range, no parking ~1m short).
- ✅ **Reach a base via the interim Travel tap → clear → next companion joins + returned.**
- ⛔ **NOT yet (don't log as regressions):** the natural distance-gated walk / "see it coming" — WO-453.

---

## 1. HOW WE WORK — the orchestrator / CLI-gatekeeper model

Three roles (CLAUDE.md §2, §11):

- **UI (Claude):** writes work orders + specs, does the flow-first triage / RCA, makes creative
  calls, and writes `.cs` (Windows path, Write/Edit only — see §2 below).
- **CLI (this seat / lead):** the **sole committer + gatekeeper**. Owns batchmode (gates, bakes,
  builds), reconciles every session's diffs by explicit path, commits, pushes **only on owner OK**.
- **Owner (Samantha):** PM; final creative + sequencing decisions; runs the editor for felt/playtest.

The loop:

1. **Flow-first triage** — what *should* happen given the state ("is this state even expected?"),
   NOT culprit-hunting a stack trace. Ambiguous tickets (no repro/screen/stack) bounce back.
2. **Fan out agents** — each does ONE focused task. Read-only **diagnosis/verify** agents are
   gate-free → fan out many. **Edit-only** implementation agents run on **file-disjoint silos**
   (the §9 lanes; same-file work = one agent), told NOT to gate/commit.
3. **Batch-gate ONCE** — the orchestrator runs the compile gate over the combined tree
   (`COMPILE_GATE_OK`), then **commits each lane by explicit path** (never `git add -A`).
4. **Push only after** the owner retests/confirms (felt/gameplay) or a regression passes
   (data/logic) — "push the ones that passed."

**Notion is the live WO board** — *Defenders of the Realm — Pipelines* "Work Orders" DB
(data source `5f66b263-c732-4075-b94a-f5f4de9f8087`). Full WO spec files stay in the repo
(`WORK_ORDER_NNN_*.md`). WO-numbering authority = `MASTER_PIPELINES_BACKLOG_2026-06-06.md`, **not**
the filesystem max. Migrated off Linear; see `NOTION_SOURCE_OF_TRUTH.md`.

---

## 2. THE NON-NEGOTIABLE RULES (binding — condensed)

1. **UI never touches code; CLI writes ALL code.** (Owner 2026-06-13, binding.) The UI session does
   RCA / specs / narrative / screenshots / board grooming — it does NOT write or edit `.cs`. Only CLI
   writes code, on the **Windows path with Write/Edit only** — never `cat >`/`echo >>` via the §0 Linux
   mount (it does NOT sync reliably; redirects truncate/duplicate/interleave). If a file is broken on
   Windows, only CLI fixes it. The
   **NUL-byte gate now enforces this**: `CompileGate.Run` scans every `Assets/**/*.cs` for embedded
   NUL bytes and withholds `COMPILE_GATE_OK` if any are found (catches mount-garble that looks clean).
2. **§1 Quality gate on every `.cs` you touch** — brace balance + leak-scan (no stray
   `</content>`/`</invoke>` junk from agent Writes) + NUL-scan. **`DeNelle.Editor.CompileGate.Run`
   is the authoritative gate** — its `COMPILE_GATE_OK` marker is the only proof a tree compiles clean.
3. **Reconcile, don't replace.** WO specs predate the branch — treat as intent, add additively,
   never blind-replace a file.
4. **Stage by explicit path — never `git add -A`.** LFS-clean textures show as ~132-byte pointer
   diffs; a blanket add mass-converts them. Stage each path you reviewed.
5. **Never hand-edit `.unity` scenes.** `Village.unity` is corruption-cursed and ABANDONED
   (`Village2` is canonical). Rebuild via the builder (`VillageSceneBuilder.BuildVillage`,
   `CastleHubBuilder` — but do NOT regen the hand-dialed castle, it reverts owner offsets).
6. **One committer.** Two committers duel on `.git/index.lock` → stale locks + false "pushed."
   Other sessions write + signal "ready"; the one committer reconciles.
7. **Unity editor must be CLOSED for any batchmode gate/bake/build** — project lock otherwise.

---

## 3. RULES WE ADDED THIS SESSION (the new canon)

- **INSTRUMENT-FIRST debugging (CLAUDE.md §12, BINDING).** We do **not** guess at bugs — we
  instrument the flow and let the data say where it dies. Four `DeNelle.Core` helpers in
  `Assets/_Modules/Core/Diagnostics/`:
  - **`FlowTrace`** — `Step/Warn/Fail/Throttle/Once/Measure`, `[Flow:<system>]`-tagged. Trace flow
    entry, every branch *taken*, every fallback, service resolution, and the render/commit seam.
  - **`Guard`** — `Try`/`TryEach`; **one bad object must never blank a whole list/screen** (list
    population uses `Guard.TryEach`). Never compile-stripped (it changes control flow).
  - **`BreakCaptureHarness`** — F8 flight recorder → `break-log.jsonl` + screenshots.
  - **`DataRegression`** — headless "real object in → assert → one marker" gate.
  - **No silent failures:** a `catch` that swallows without logging is forbidden; every fallback is
    a `Warn`, every real failure a `Fail` (error-level → lands in the recorder). Method =
    `docs/INSTRUMENTATION_STANDARD.md`.
- **The AutoPilot bot / fleet.** A headless player bot (`Assets/_Modules/DevTools/AutoPilot*`,
  `Assets/Editor/AutoPilot/`) drives the game and emits ranked tickets. The **player .exe needs no
  Unity license**, so `run-autopilot-fleet.ps1 -Count N` runs dozens of instances in parallel (each
  a distinct `--seed`/`--run`); `AutoPilotTickets.Emit` dedupes + ranks by how many runs reproduced
  each break. `-nographics` → logic/flow/crash coverage only (UITK picking won't resolve headless).
- **Confirm-to-cross seam + WarpTo.** Two-scene navmeshes don't auto-connect. `SceneTransitionTrigger`
  disables → warps → re-enables the hero's `NavMeshAgent` across the seam. Debug "can't cross/exit"
  as a **navmesh bake** issue, not colliders. The hero returns to a **return-point** (`ReturnScene`
  in `BattleParams` for combat; the seam warp for world crossings).
- **Hero tag = `Player` (one tag, now declared).** Locomotion/camera/HUD/triggers all
  `FindWithTag("Player")` (set in `HeroControlEnsurer.Ensure`). **Enemy AI finds the hero by
  COMPONENT** (`FindFirstObjectByType<HeroLocomotion>()`), NOT a `HeroTarget` tag — that tag was
  never declared and a GameObject has only one tag (CLAUDE.md §7).
- **Vendor-stock contract.** `Assets/_Modules/Village/Hero/VendorStockContract.cs` is the single
  source of truth for what each store TYPE sells (armorer=armor, etc.). Two consumers read the same
  `AllowedFor()` mapping: `ShopPanel.ShowBuy` filters stock; the AutoPilot bot asserts the built
  stock matches — so the bot checks intent, not a duplicate.
- **Seam radius / nav lesson.** The seam is a **proximity** trigger; the hero (a `NavMeshAgent`)
  stops at the **navmesh edge**, so the trigger radius must overlap the walkable surface or the hero
  never reaches it. Tune the seam against the bake, not the visual mesh.
- **Pet-from-shop flow.** Pets are acquired through the shop flow (not only PetSelect onboarding) —
  trace via `[Flow:*]` if a purchased pet doesn't appear.
- **OnboardingPanelGuard.** The "dev tools / UI dead after Yarn" bug: a UIDocument backed by the
  shared `OnboardingPanelSettings` leaked into a gameplay scene and its raycaster sat on top of the
  click stack, eating every click. `Assets/_Modules/Onboarding/OnboardingPanelGuard.cs` enforces the
  invariant (that panel may only intercept input in Title/HeroSelect/PetSelect) on every scene load.
  **Fixed.**

---

## 4. THE BUILD / GATE / BAKE CYCLE

All batchmode runs through `run-unity-method.ps1` (handles the relaunch-fork quirk — poll for the
exe/marker, not the wrapper exit code; the 505 license line is transient/non-fatal). **Editor must
be closed.**

| Task | Invocation |
|---|---|
| **Compile gate (authoritative)** | `run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run` → `COMPILE_GATE_OK` (brace + leak + NUL scan) |
| **Data/logic regression** | `run-unity-method.ps1 -Method DeNelle.Editor.DataRegression.RunAll -LogName data-regression.log` → `REGRESSION_OK` / `REGRESSION_FAIL` |
| **Castle rebake** | `BatchRebuildCastleFromRecipeAndBake` (do NOT regen the hand-dialed hub geometry) |
| **Outpost wiring** | `BatchWireOutpostsAndSave` |
| **Village rebuild** | `DeNelle.Editor.VillageSceneBuilder.BuildVillage` (never hand-edit the scene) |
| **Windows player build** | `build-windows.ps1` |
| **AutoPilot fleet** | `run-autopilot-fleet.ps1 -Count N` (player exe; no license needed) |
| **WebGL ship** | `ship-webgl.ps1` / `build-webgl-isolated.ps1 -Ship` → butler → itch |

- **F8 break-logs land in `break-log.jsonl`** (+ screenshots) via `BreakCaptureHarness`; fleet runs
  namespace theirs per `--run`. `Fail`/`LogError` lines are what the recorder captures.
- **exe-stub quirk (load-bearing):** incremental player builds skip re-emitting the exe stub → stale
  exe vs fresh scenes → `level3 corrupted` native crash. **ALWAYS delete `Builds/Windows` before
  `build-windows.ps1`.** Also: build via the Defenders→Build menu / `build-windows.ps1`, NOT the
  Build Profile "Build" button (it skips the Static-Batching-off mitigation).

---

## 5. CURRENT STATE + RESUME POINTS

**Playable loop:** Title → HeroSelect → PetSelect → `MainCastle_Hall` (home hub) with `OuterWorld`
streaming additively; south-gate seam → OuterWorld; raids via `RaidOutpostSystem` (4 in-world
outposts, ~10s delay) and additive `Garrison_*` scenes; `Village2` = TD raid target; ATB battles
return to `ReturnScene`. Store ~70% built (do NOT greenfield — `PackStore` exists; scene-wiring
disabled pending its own PanelSettings). Build mode wired end-to-end for towers (~70%).

**Recently fixed this session:**
- Dev-tools-dead-after-Yarn → `OnboardingPanelGuard` (§3).
- Archer/blast tower behavior — fixed.
- Vendor stock leakage (armorer selling weapons/potions) → `VendorStockContract` (§3).
- Raid outpost never found — 3-min spawn delay cut to 10s.

**Known-open / watch:**
- **South-gate ~34m nav reach** — verify the seam trigger radius overlaps the walkable navmesh
  (the hero stops at the navmesh edge; §3 seam lesson). Test in Play/build, not batchmode
  (`NavMesh.SamplePosition` fakes a complete path in headless).
- Remaining AutoPilot audit findings — work the ranked tickets from the latest fleet run.
- Cross-zone *AI* pathing across the seam is deferred (off-mesh links when raids walk between zones).

**Pointers:** `docs/ARCHITECTURE.md` (architecture hub) · `docs/MASTER_CATALOG.md` (verified-from-code
SME catalog) · `docs/INSTRUMENTATION_STANDARD.md` (the §12 method) · `docs/MODEL_CATALOG.md` +
`docs/polyperfect-asset-catalog.md` / `docs/kaykit-asset-catalog.md` (check before referencing a
prefab) · Notion "Work Orders" DB (live board) · `PIPELINE_STATE.md` (full pipeline detail).

---

*Maintenance: keep §3 and §5 current as the canon and the loop move. This sheet is the entry point —
depth stays in the deep-dives it points to.*
