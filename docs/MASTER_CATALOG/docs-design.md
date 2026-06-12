# MASTER CATALOG — docs-design (the `docs/` tree)

Section catalog for **`docs/**/*.md`** — every design doc, port-note, audit, QA doc,
and roadmap note. **137 markdown files** (excludes this `MASTER_CATALOG/` folder).
Verified by reading each file's header/intent block on 2026-06-12, branch
`feat/tower-core-loop`. This area is **documentation only** — no CODE / SCENE / DATA
items live here (those are cataloged in the sibling section files: `core.md`,
`village-*.md`, `battle-atb.md`, `data-catalogs.md`, etc.). Where a doc *describes*
code, the gist notes what it claims; the FLAGS section at the bottom records where the
doc's claim is **stale vs. the live code/canon**.

> **current/stale legend.** `CURRENT` = matches live architecture/canon. `STALE` =
> superseded premise or naming (kept for history). `MIXED` = intent current but
> embedded facts drifted. `HISTORICAL` = a dated snapshot/audit, true-as-of-date, not
> meant to track live state. `SPEC` = forward design, not-yet-built (current as intent).

---

## A. Canon / Vision / Brand (source-of-truth design)

- **NORTH_STAR.md** — *MIXED.* The one-picture vision: "build your stronghold, claim/defend
  resource nodes, grow while away" = CoC base-building × Warcraft resource war. Every WO
  routes against it. Embedded Pi-economy line is **superseded by Solana/$SKR** (see memory
  `solana-skr-funding-thesis`).
- **NORTH_STAR_PROGRESS.md** — *HISTORICAL (2026-06-03).* Ladder tracker: assessed at "Rung
  2→3 mid-climb" (Defend-the-Town = where-we-live; world bones in; base/build not started).
  A dated trajectory snapshot, not live.
- **ARCHITECTURE_NORTH_STAR.md** — *CURRENT.* Technical mirror of the vision: 6 load-bearing
  principles (data-driven, server-authoritative, deterministic/headless, swappable-behind-
  interfaces, etc.) that keep the dream reachable without a rewrite.
- **ARCHITECTURE_PRINCIPLES.md** — *CURRENT / BINDING.* Project law (2026-06-10): HP-B2B
  bounded-context per component; presentation is a separate layer that never touches objects;
  "what is right not what is easy." Referenced by `CLAUDE.md` as binding.
- **DESIGN-DECISIONS.md** — *CURRENT / BINDING.* Changelog of creative shifts from the
  board-game original: village renamed Elarion (#1), Cathedral Spire replaces Heart-Tree (#2),
  no Keep (#3), Mage/Knight/Ranger trio (#7), etc. PM source-of-truth for canon.
- **BRAND_BIBLE.md** — *MIXED (name-locking).* Rebrand "Echoes of Elarion: Alerion's Awakening";
  supersedes working title "Defenders of the Realm." Spelling Echoes/Echos still pending owner
  confirm; the rebrand sweep (WO-138) is not fully applied across docs.
- **BRAND_NOTE_wall_segment.md** — *CURRENT (origin story).* The logo seed = a wall segment;
  the 2026-05-30 "why does this wall look wrong" question that cracked open the whole architecture.
- **GAME_DESCRIPTION.md** — *CURRENT.* Store-listing copy (1-line / medium / full). "A civilization
  gave their souls to survive." Uses Elarion/Heartwood canon.
- **whitepaper.md** — *STALE (2026-05-18, v1.0).* Token/economy whitepaper; references the Vercel
  React live build + early wallet posture. Pre-Solana-pivot framing; archival.
- **PI_PITCH.md** — *STALE.* One-pager pitching Pi Network as flagship economy. Superseded by the
  Solana/$SKR thesis; kept for history.

## B. Narrative / Story / Worldbuilding

- **narrative-bible.md** — *MIXED (v1 canon).* The world-in-a-paragraph + voice rules + snippet
  library. Heart-Grove/Heart-Tree premise; STORYLINE.md reframes this to the Cathedral Spire but
  keeps tone/characters. Voice rules still binding.
- **STORYLINE.md** — *CURRENT (v2).* Reframes the world post Heart-Tree-burned → Cathedral Spire
  at centre. Supersedes narrative-bible's tree premise. Drops the lantern motif → "Stone Choir."
- **ECHOES_OF_ELARION_NARRATIVE.md** — *MIXED (rebrand v1).* Cohesive lore layer for the Echoes
  rebrand; reconciles canon + region/crystal economy. Still uses some Avalon naming in spots.
- **regions-narrative-and-npcs.md** — *SPEC.* Four climate regions (WO-107) worldbuilding + NPCs +
  the ward-tether rule that lets Defend-the-Town become Defend-and-Explore. Names pending ratify.
- **dungeons-storyline.md** — *SPEC.* Meta-arc threading the dungeon system into fiction; per-
  questline beats + lore-stone voice. Feeds dungeon JSON authoring.
- **PARTY_OF_FOUR_STORYLINE.md** — *SPEC.* Writes a party-of-4 into the lone-Keeper canon so you
  travel as four when you leave Elarion — the precondition that makes party-vs-party targeting matter.
- **BARD_SUPPORT_CLASS_DESIGN.md** — *SPEC.* Recruitable wandering Bard = the missing support
  archetype (party-buff songs); welds encounter→party→battle.
- **LEGENDARY_GEAR_DESIGN.md** — *SPEC.* Story hooks toward legendary weapons/armor + one Legendary
  set per party member, each tied to canon (master's lost gear, regions, First Light, the Wound).

## C. Engine / Architecture design (the "create" substrate)

- **CHARACTER_ARCHITECTURE.md** — *SPEC (target).* Unify Hero/Enemy/Pet/Townsfolk onto one
  `Character` substrate + swappable `brain` + universal action verb. Foundation for auto-battle.
- **CHARACTER_REFACTOR_PLAN.md** — *SPEC (impl-ready).* Phased EXTRACTION+UNIFICATION of the above,
  grounded in existing code; brain-seam is the deterministic/headless enabler.
- **CHARACTER_CREATOR.md** — *SPEC.* 3 fixed heroes → first 3 presets of a modular character creator;
  hero = composition of catalog parts (same catalog⊥repo engine as the village builder).
- **WORLD_ENGINE_ARCHITECTURE.md** — *SPEC (impl-ready).* The generic typed-`def`+dispatcher layer
  ABOVE the character refactor — wraps actors as one domain; also drives terrain/weather/structures.
- **CATALOG_SYSTEM.md** — *SPEC.* The Catalog half of catalog⊥repo (look vs behavior): types/parts/
  granularity (single cell OR completed prefab) feeding build-mode.
- **ENGINE_MASTER_PLAN.md** — *SPEC.* Consolidates CHARACTER_REFACTOR_PLAN + WORLD_ENGINE_ARCHITECTURE
  into one scope + foundation-first build order. "Everything is a typed def + a controller."
- **MONOLITH_SPLIT_PLAN.md** — *SPEC / PLAN (2026-06-10).* Marching orders to split >800-line offenders
  per ARCHITECTURE_PRINCIPLES; read-only analysis, no `.cs` touched.
- **refactor-feature-modules-spec.md** — *STALE.* Argues to split the ~9,000-line React
  `Village3D.tsx`. Pre-Unity-port-era (TS module sprawl); the Unity codebase already is modular asmdefs.

## D. Build Mode / Base-building / Catalog content

- **BUILD_MODE_ARCHITECTURE.md** — *MIXED (reconciliation).* The CREATE verb (Rungs 4→6); states the
  machine is ~70% built (BuildModeController/PlacementGrid/GhostPreview/BaseLayoutLoader wired for
  towers; save v14). Gaps: palette content, full 4-resource economy, upgrade verb, mobile touch.
  Supersedes the lowercase `build-mode-architecture.md`.
- **build-mode-architecture.md** — *SPEC (WO-108, largely now implemented).* Original "let the player do
  what VillageSceneBuilder does" architecture; superseded by the uppercase reconciliation doc above.
- **PLAYER_BASE_DESIGN_CATALOG_ROADMAP.md** — *SPEC.* Architecture+roadmap for a player-friendly base-
  design catalog (placement=role); spawns the WO list. "Reconcile not replace" — catalog data model + 
  placement runtime already exist.
- **DEFENSIVE_CATALOG.md** — *SPEC.* Defensive Catalog v0 — first 4 testable towers (Archer/Wizard +2);
  placement determines role. Each entry = a `CatalogEntry`.
- **DEFENSE_DEPTH_ANALYSIS.md** — *HISTORICAL/SPEC (2026-05-30).* Where defensive progression is *thin*
  (vs. missing): shallow ceilings, 3 depth problems. Verified against code as of date.
- **SCROLL_BLUEPRINT_SYSTEM_DESIGN.md** — *SPEC.* Explore→find encrypted scroll→Scholar decodes→unlocks
  a new defensive UNIT. The tangible "earn new defenses" loop.
- **WORLD_COLLECTION_MODEL_DIRECTIVE.md** — *SPEC / DIRECTIVE (2026-06-10).* Owner vision: castle →
  castles → collection-of-castles-in-realm (nested collections, capability-as-property). Not implemented.
- **world-construction-plan.md** — *SPEC.* Outward-in authoring order: build outer rings first, leave
  shared centre (castle/moat/plaza) for last. Grounded in VillageSceneBuilder + ExteriorTerrainBuilder.

## E. Combat / Enemies / Encounters / AI design

- **BATTLE_2D_PARTY_DESIGN.md** — *SPEC.* Resolve the 1-hero-targeting gap by leaning into classic FF
  2D party battles (side-view, time gauges, Attack/Magic/Item, pick-target) on the built `DeNelle.BattleATB`.
- **MONSTER_FAMILY_ARCHITECTURE.md** — *SPEC (Combat/AI lane).* Leader/follower roaming "Monster Family"
  packs (a squad, not a swarm). Flags a naming collision with the existing loose `EnemyFamilyTestSpawner`.
- **ENCOUNTER_SYSTEM_DESIGN.md** — *SPEC.* The random-encounter "texture between objectives" layer woven
  into existing systems (the exploration dead-air gap).
- **ALERT_INTEL_SYSTEM_DESIGN.md** — *SPEC.* Alerts as diegetic intel you EARN with lookouts (town safe,
  world exposed) — turns a UI toggle into a gameplay system.
- **enemy-codex.md** — *SPEC (design codex, 2026-05-19).* Definitive enemy+boss design; non-canon names
  flagged for owner ratify. Sourced from kaykit catalog + enemies.json + Enemy.cs + ENEMY_DEFS.
- **REGION_ENEMY_ROSTER.md** — *SPEC.* Who roams where (open-world threat map): living wildlands vs
  Wound-tied demonic; read by the roaming scaler (WO-143) + zone ThreatLevel.
- **elemental-codex.md** — *SPEC (2026-05-27).* Designer reference for element→VFX tint assignments
  (owner to ratify). Sourced from enemy/ability data + Mirza Beig VFX.
- **enemy-mob-sets-work-order.md** — *WORK ORDER (2026-05-27, master branch).* Enemy archetypes (Tank/
  Healer/Ranged already in EnemyBrain) + VFX layer. P0/P1.
- **ENCOUNTER_SYSTEM_DESIGN / DEFENSE_DEPTH** cross-link the scroll/blueprint + bard docs above.

## F. Economy / Progression / Monetization design

- **RESOURCE_ECONOMY_DESIGN.md** — *SPEC.* The never-drawn economy flow: rates/balance/conversion chain/
  pacing (owner: hybrid fast-early/slow-late, tunable numbers not magic constants).
- **TALENT_TREE_V2_DESIGN.md** — *SPEC.* Deeper Witcher-style talent tiers + respec; vs today's flat 6
  binary nodes/hero. Grounded in HeroTalentCatalog/Modifiers/TalentTreePanel.
- **ITEM_DROPS_CONSUMABLES_DESIGN.md** — *SPEC + DARK SCAFFOLD.* Item drops + consumable crafting on the
  near-complete village crafting station; ships dark, needs sign-off.
- **GLIMMER_ECONOMY_OPEN_QUESTION.md** — *OPEN (2026-05-31).* Glimmer = cosmetic soft-currency
  (GlimmerCurrencyService); "Crystals→Glimmer not allowed" locked. Earn-rate routed to creative/monetization.
- **monetization-v2-spec.md** — *SPEC (locked 2026-05-17).* SKR packs + seasonal pass; 4 currency rails
  (SKR/SOL/USDC/Stripe); cosmetic+convenience, no combat-stat power. References React-era `src/` paths.
- **admin-console-spec.md** — *SPEC (backend).* Password-protected `/admin` Vercel dashboard for live-ops
  (config in Postgres `game_config`, not env vars). Backend repo, not Unity.
- **anti-cheat-spec.md** — *SPEC (locked 2026-05-17).* Layered server-authoritative validation + wallet
  behavior scoring + honeypots to protect the SKR-yield rewards. Backend.
- **wallets-of-record.md** — *CURRENT (registry).* Canonical wallet addresses (publisher / rewards
  distributor hardware-backed; 1M SKR stake private, Option A). Single source for other docs.

## G. Asset-pack notes & catalogs (check before referencing a prefab)

- **INSTALLED_PACKS_INDEX.md** — *CURRENT (2026-06-05).* One-line index of every third-party pack → its
  `_NOTES.md` → the hook. Read first.
- **KAYKIT_NOTES.md** — *CURRENT.* Technical companion (rig/anim/material/path) for KayKit; 21 packs +
  curated live-set; URP-fix via `Tools▸DeNelle▸Fix KayKit Materials`.
- **kaykit-asset-catalog.md** — *CURRENT (2026-05-19).* The creative pick-list of the whole KayKit
  collection (per-building/hero/enemy/boss mapping). Import from `fbx(unity)/`.
- **POLYPERFECT_NOTES.md** / **polyperfect-asset-catalog.md** — *CURRENT.* Low Poly Ultimate Pack
  (gitignored, 246 MB, `SM_<Name>.fbx`) that replaced heavy Tripo village meshes; notes = technical,
  catalog = pick-list.
- **QUATERNIUS_NOTES.md** — *CURRENT.* Medieval Village MegaKit (CC0, URP) = source art for the
  Village2 factory/sister-city generator. Import from `Modules/Prefabs/`.
- **SPELLS_PACK_NOTES.md** — *CURRENT.* Zakhanfx elemental spell/projectile/shield/aura prefabs (Mage/
  towers/casts); needs bundled URP `.unitypackage` or magenta.
- **MIRZABEIG_VFX_NOTES.md** — *CURRENT.* 300+ general VFX prefabs (namespace `MirzaBeig`); Particle
  Scaler tool = the way to resize any pack.
- **LANA_RPG_VFX_NOTES.md** — *CURRENT.* ~128 cute casual-RPG VFX prefabs; run Upgrade-for-URP or magenta.
- **MAGIC_VFX_LIBRARY.md** — *CURRENT.* The Spells Pack mapped to gameplay (creative menu, no new art).
- **MASTER_ASSET_REFERENCE.md** — *CURRENT (2026-06-04).* "Things we can actually use" → backs the
  `MasterAssetCatalog` ScriptableObject (defense/walls/etc.).
- **LEANTOUCH_NOTES.md** — *CURRENT.* CW/Lean.Touch touch-input framework; the 3 asmdefs a consumer refs.
- **UNITASK_NOTES.md** — *CURRENT.* UniTask v2.5.10 = the project's standard async primitive (~119 sites).
- **YARNSPINNER_DIALOGUE_NOTES.md** — *CURRENT.* Yarn Spinner 3 architecture (DialogueRunner/Presenters/
  LineAdvancer) + ClassicRPG presenter; how to advance/customize lines.

## H. Catalog/legacy spec layout docs (older naming)

- **avalon-village-layout-spec.md** — *STALE (2026-05-18, "Avalon").* Interior layout spec from the KayKit
  Hexagon pack era — Elarion + Keeper's Keep side-by-side. Keep removed; village renamed Elarion;
  Village2 generator is canonical now. Historical.
- **village-review-suggestions.md** — *HISTORICAL (2026-05-19).* Art-direction review checklist of the
  Week-3 built village. Approvable tweak items; many superseded by later builds.
- **dungeons-3d-unity-layout-spec.md** — *SPEC (2026-05-18, "Avalon dungeons").* How-to-build-in-Unity
  layer for 7 dungeon scenes (KayKit Dungeon Remastered). "Avalon" naming stale; layout conventions live.
- **dungeon-3d-healers-cottage-design.md** — *SPEC.* D1 Healer's Cottage POC design (6 rooms/beats/
  mini-boss). The built reference template; references React `src/content/quests/`.
- **DUNGEON_DESIGNS.md** — *SPEC.* D2–D6 full buildable specs grounded in dungeons-storyline + enemy-codex;
  D1 is BUILT as reference.

## I. Audits / Reviews / Gap analyses (dated snapshots — HISTORICAL unless noted)

- **audit/architecture-review.md** — *HISTORICAL (2026-05-19).* Module isolation/dependency-direction
  review of Weeks 1–7 C# + asmdefs. Findings tagged ARC-/CODE-.
- **audit/memory-audit.md** — *HISTORICAL (2026-05-19).* Memory-leak/disposal audit vs the ≤400 MB /
  ≤33 ms Week-8 gate. (See memory `hardening-must-be-systematic` for the live posture.)
- **audit/mobile-performance.md** — *HISTORICAL (2026-05-19).* Mobile-readiness risk vs the Seeker; P0
  fixes drove `port-notes/mobile-settings.md`.
- **audit/missing-components.md** — *HISTORICAL (2026-05-19).* Gap analysis (what a shippable game lacks),
  monetization-loop focus; the P0-N list drove the port-notes onboarding/settings/audio slices.
- **audit/security-audit.md** — *HISTORICAL (2026-05-19).* Wallet/secrets/save-load trust-boundary review.
- **audit/input-controls.md** — *HISTORICAL (2026-05-19).* On-screen-controls + D-pad-sensitivity review
  for the Seeker; drove an implementation pass.
- **UNITY_BEST_PRACTICES_AUDIT.md** — *HISTORICAL (2026-06-03).* Read-only Unity-6/URP/WebGL hot-path +
  lifecycle anti-pattern sweep of `Assets/_Modules/**`. Guidance only.
- **REUSABILITY_AUDIT_2026-06-03.md** — *HISTORICAL (2026-06-03).* Creation-site audit: runtime ~90%
  on-factory; holdouts = PetDeployer + PatriciaLightController; editor builders dup ~500 lines.
- **VISION_GAP_ANALYSIS_2026-05-30.md** — *HISTORICAL (2026-05-30).* Maps NORTH_STAR → where-we-are; the
  one missing keystone + connective tissue in build order.
- **PROPER_VERTICALITY_PLAN.md** — *SPEC / PROPOSAL.* Hero verticality (DEF-147 hover exploit, "stairs
  don't climb", rampart LIFT band-aid); verdict "mostly true, one correction." Read-only.
- **CAMERA_INPUT_OVERHAUL.md** — *SPEC (READY, DEF-202/204).* Third-person follow cam + camera-yaw-
  rebased movement + hard guarantee against the "always turn left" curl. (Validated direction — see
  memory `camera-relative-follow-validated`.) DTT/PatriciaLight out of scope.
- **diagnosis-report.md** — *HISTORICAL (2026-05-22).* Village-recovery root cause: NOT GUID drift —
  the Tripo asset pipeline + a few uncommitted `.meta`.
- **QA_player_sanity_pass_2026-05-30.md** — *HISTORICAL (2026-05-30).* Static (not runtime) player-
  journey audit traced to file:line; predicted behavior, to confirm in a real build.
- **acceptance_verification_2026-05-30.md** — *HISTORICAL (2026-05-30).* Static verification of each WO's
  own acceptance criteria; companion to the sanity pass.
- **bug-triage.md** — *HISTORICAL.* Read-only gameplay-runtime bug triage (Enemies/Hero/Buildings/State/
  Vfx) with ready-to-apply fix diffs (none applied). Notes `VFXManager.Play` is static (no `.Instance?.Play`).

## J. Backlog / Triage / ROI / WO-strategy docs

- **ARCHIVED_ISSUES_2026_06_04.md** — *HISTORICAL.* 205 archived Linear issues (193 done / 11 cancel /
  1 dup), freed for the Pre-Prod board. (Linear since retired → Notion, per memory.)
- **BACKLOG_TRIAGE_2026-06-04.md** — *HISTORICAL.* Grant-firefight triage ranked by ROI toward an end-to-
  end playable demo; CODE-only vs SCENE/BAKE vs BLOCKER legend.
- **WO_ROI_TRIAGE.md** — *HISTORICAL (2026-06-03).* 153-issue Linear backlog ROI classification into
  pickable silos; superseded same day by ROI_PLAN.
- **ROI_PLAN_2026-06-03.md** — *HISTORICAL (2026-06-03).* Post heroes/tutorial/HUD-build ROI plan; "the
  build is what the grant judges"; supersedes the morning WO_ROI_TRIAGE.
- **WO-391_INTERACTION_SEPARATION_STRATEGY.md** — *SPEC / STRATEGY (2026-06-10).* "Do it correctly when
  it's time" plan to separate interaction/presentation per ARCHITECTURE_PRINCIPLES; intentionally not
  implemented yet.
- **WO-403_405_RECONCILIATION_AND_AM_PLAN.md** — *HISTORICAL/PLAN (2026-06-10).* Reconciles WO-403↔405
  built overnight (owner gates in AM); flags the author's own WO-403 divergence rather than papering over.
- **ARENA_SOLUTION.md** — *SPEC / SYNTHESIS (2026-06-10).* 4-agent solutioning synthesis (WO-388/389/386/
  390); "the arena is ~75% already built" — needs connective tissue + a 2D-presenter + the defend&watch
  inversion, not new combat/AI/economy.
- **recovery-work-orders.md** — *STALE (2026-05-22).* Original 7-agent village-recovery plan premised on
  "missing GUID references" — diagnosis-report.md proved that wrong. Historical context only.
- **claude-code-work-order.md** — *STALE (2026-05-22).* Village-recovery handoff WO on `master`/`ba393bb`,
  Unity `6000.4.7f1`. Recovery is long done.

## K. Port-notes (Week-1→7 v2 port slices — mostly HISTORICAL "source written, integrator wires")

All `port-notes/*` are dated **2026-05-18→21** snapshots of the React→Unity port; nearly all end with
"source written; cannot run Unity here; integrator verifies." Treat as **HISTORICAL** build records, not
live state.

- **animation-setup.md** — Native Unity Animator wiring for the roster; AnimatorSetup.cs editor script.
  *Superseded by* the later canonical pipeline (`ANIMATION_PIPELINE.md`, WO-283).
- **atb-engine-port.md** — Line-by-line port contract for `src/lib/atb/*` → BattleATB; literal equivalent,
  bugs flagged-not-fixed.
- **audio-system.md** — Mixer + AudioService + per-scene BGM (audit P0-9); AudioMixer asset written.
- **canon-data.md** — Week-1 canon extraction → `canon-strings.json` / `en.json` (provenance in `_`-keys).
- **core-state-port.md** — Core State/Persistence/Routing port spec (GameState/SaveSchema/SaveMigrator/
  SceneRouter/Theme); literal React translation.
- **core-test-fix.md** — Root-cause + fix for the Core EditMode failure (`_state` null in
  `GameStateService.Reset()`) → 59 passing.
- **dev-panel.md** — `DeNelle.DevTools` in-game QA console; **compiled OUT of release builds** (key
  property). (See memory: two dev panels exist — DevPanelController + AdminOverlay.)
- **dragon-boss.md** — Black Dragon flying-boss: URP material fix + rig/clips + AnimatorController +
  MonoBehaviour scaffold.
- **dragon-wave-wiring.md** — Wire "Syndrath the Devourer" (DragonBoss.cs) into the Avalon wave loop as
  apex wave-boss. "Avalon" naming stale.
- **dungeon-integration.md** — Healer's Cottage playable end-to-end + BUG-008 fix (ATB hard-coded village
  return even for dungeon).
- **hud-module.md** — First `DeNelle.HUD` component (VillageHud.uxml/uss) for Week-4 Wave-1.
  *Flag:* UXML-sourced HUD — per memory `uxml-uidocuments-dont-render-in-builds`, code-built UI is now law.
- **mobile-settings.md** — Applies the mobile-performance audit's 6 P0 + P1 URP fixes (source only).
- **onboarding.md** — First-run tutorial closing audit P0-11 (intro re-played every launch; `Onboarded`
  never set).
- **package-manifest.md** — Exact `Packages/manifest.json` spec; Unity `6000.4.7f1`, URP, .NET Std 2.1
  (Solana SDK). Editor since bumped to `6000.4.8f1` (per memory) — version line drifts.
- **realm-map-data.md** — Authors `realm-map.json` (Avalon home + 5 regions + Withering border); v1.1
  feature ahead of the regions port. "Avalon" stale.
- **settings-pause.md** — Settings screen + pause overlay (audit P0-8/P0-10); UI Toolkit.
- **tripo-asset-pipeline.md** — Replace placeholder pet/hero/cathedral with 7 Tripo FBXs + postprocessor.
  (See memory `tripo-fbx-material-fixer` + `tripo-mesh-displacement-trap` for live gotchas.)
- **wall-prop-fixes.md** — Two VillageSceneBuilder placement bugs (surgical edits). "Avalon" naming.
- **week4-buildings.md** — Crystal Mine/Pet House/Arcane Tower/Workshop/Farm + build menu (crystals cost).
- **week4-hero-pets-gate.md** — Hero Q/W/E/R kit + 3 starter pets + cardinal-gate force-field (<25% HP).
- **week4-integration.md** — Wiring Week-4 C# into `Village.unity` for Wave 1. **`Village.unity` is now
  abandoned/corruption-cursed — Village2 is canonical** (memory `village2-is-canonical`).
- **week4-waves.md** — WaveManager + enemies + breach→ATB transition.
- **week5-dungeon-foundation.md** — Healer's Cottage hero walk + Cinemachine follow + wall colliders.
- **week6-dungeon-systems.md** — Lantern-oil mechanic + Bryn NPC + lore-stones + scripted ATB +
  checkpoints. *Lantern motif later dropped (STORYLINE.md "Stone Choir").*
- **week7-wallet.md** — Solana Unity SDK wallet on devnet + 5-pack store + devnet "purchase."

## L. QA / Acceptance / Test docs (HISTORICAL, Weeks 1–8 v2 foundation)

- **qa/qa-test-plan.md** — *HISTORICAL (living, 2026-05-19).* 90-case functional matrix vs spec Part 5/9;
  Weeks 1–4 compiling, 5–7 mid-integration, no end-to-end build yet (as of date).
- **qa/regression-suite.md** — *HISTORICAL (2026-05-19).* Automated EditMode coverage over stable modules;
  companion to the test plan.
- **qa/uat-script.md** — *HISTORICAL (refreshed 2026-05-20).* Non-engineer Week-8 acceptance walkthrough;
  refreshed for the Elarion/Spire pivot (village renamed, Keep/Banner removed, build menu reduced to Tower).
- **qa/owner-acceptance-checklist.md** — *HISTORICAL.* Owner-called-out acceptance items beyond spec Part 9;
  DONE/PARTIAL/GAP/IN-PROGRESS states (2026-05-19 prioritization).
- **qa/po-validation-village-maps.md** — *HISTORICAL (2026-05-19).* PO acceptance of the Avalon village
  interior + 4 area maps (built-vs-spec). "Avalon" naming.
- **qa/bug-log.md** — *HISTORICAL (2026-05-20).* Running bug tracker seeded from unity-decisions "Flags"
  + Week-5/6/7 port-note open items.

## M. Roadmap / Future-scope notes

- **roadmap/live-ops-scope.md** — *SPEC (future).* Live-ops & admin-portal scope NOT part of the 8-week
  port; tracked for the post-Week-8 budget decision.
- **roadmap/next-build.md** — *SPEC (living).* Goals for the build cycle after the v2 foundation (v2.1+);
  art/content/feature wishlist captured as the owner raises them.

## N. Pack/process/decision logs & cross-cutting specs

- **unity-decisions.md** — *CURRENT (living log).* Every non-trivial port architectural call (per spec
  Part 6), week-by-week, with "reversible?" column + "Flags raised." Owner reads to stay oriented.
- **addressables-implementation-plan.md** — *SPEC (2026-05-28, impl-ready).* Addressables golden rules +
  group plan (skins never share base groups). (Note: memory says backend never deployed; this is client-side.)
- **audio-mix-spec.md** — *CURRENT / canonical (locked 2026-05-18).* Per-track default volume/fade/
  transition for the music registry; "music sets bones, doesn't perform."
- **tower-empowerment-spec.md** — *SPEC (2026-05-27).* Tower ability/upgrade design on the 3-level chain
  (TowerData/Tower MaxLevel=3/TowerCombat/SpecialAbility). Owner to ratify names/costs/elements.
- **webgl-hosting-notes.md** — *HISTORICAL (2026-05-29).* WO-123 overnight WebGL build result (~186 MB
  Brotli) + script-relay hosting kit. (Live deploy path = `ship-webgl.ps1`→butler→itch per memory.)
- **v2-unity-port-spec.md** — *STALE-canon / foundational (2026-05-18).* The authoritative Week-1→8 port
  contract + asset pipeline. **Canon names here are STALE** (Town="Avalon", hero="Blaise") — superseded by
  Elarion + the Mage/Knight/Ranger trio + the companion roster. Still the structural contract of record.
- **v2-unity-port-backend-spec.md** — *MIXED (locked 2026-05-18, Option A).* Backend operational contract
  (Vercel + Postgres + Solana RPC + wallet-nonce auth); Unity C# = the only client. "Avalon"/"Blaise"
  canon stale. (Backend was never deployed — memory `backend-never-connected`.)
- **README.md** — *CURRENT.* The `docs/` index ("~100 files, find your category, don't grep blind").
- **MASTER_CATALOG.md** — *CURRENT (2026-06-12).* The project master index pointing at this folder's
  per-area section files. Read-order: master → section → CLAUDE.md → PIPELINE_STATE → newest HANDOVER.
- **ANIMATION_PIPELINE.md** — *CURRENT / CANON (2026-06-06, WO-283).* Every model is Humanoid + two-tier
  anim set (shared base retarget + per-type clip folder); all clips carry `mixamorig`. Supersedes the
  earlier `port-notes/animation-setup.md` approach.

---

## FLAGS

### Stale-comment-vs-code / stale-canon-vs-live (the "pure transform" class)

The dominant mismatch in this area is **stale CANON baked into otherwise-structural docs** — the
equivalent of a doc asserting a fact the live project has moved past:

1. **"Avalon" town name** — asserted as non-negotiable canon in **v2-unity-port-spec.md**,
   **v2-unity-port-backend-spec.md**, and used throughout **avalon-village-layout-spec.md**,
   **dungeons-3d-unity-layout-spec.md**, **realm-map-data.md**, **dragon-wave-wiring.md**,
   **wall-prop-fixes.md**, **po-validation-village-maps.md**, **unity-decisions.md**,
   **ECHOES_OF_ELARION_NARRATIVE.md**. **LIVE CANON = "Elarion"** (DESIGN-DECISIONS.md #1, CLAUDE.md §7).
   A reader trusting these specs would ship the wrong town name.
2. **"Blaise" mage hero name** — locked as canon in **v2-unity-port-spec.md** /
   **v2-unity-port-backend-spec.md**. Live roster = **Thrain/Grom/Sylas/Elara** (memory
   `companion-roster-canon`); no "Blaise" in the shipping cast.
3. **Heart-Tree / Heart-Grove premise** — **narrative-bible.md** centers the living world-tree; the world
   was reframed (tree burned → **Cathedral Spire**) in **STORYLINE.md** + DESIGN-DECISIONS.md #2. Docs in
   group B that still lead with the tree are pre-pivot tone-canon.
4. **Lantern mechanic** — **port-notes/week6-dungeon-systems.md** + dungeon designs build on lantern-oil;
   **STORYLINE.md** header explicitly drops the lantern motif for the "Stone Choir." Dungeon lantern specs
   are stale-motif.
5. **`Village.unity` as the playable scene** — the entire **port-notes/week4-*** set wires Wave-1 into
   `Village.unity`. That scene is **abandoned/corruption-cursed**; **Village2 (generated)** is canonical
   (memory `village2-is-canonical`, plus the scene-resave-corruption memories). Following week4-integration
   verbatim would edit a dead scene.
6. **Pi-Network economy** — **PI_PITCH.md** + the embedded line in **NORTH_STAR.md** position Pi as the
   currency; **superseded by Solana/$SKR** (memory `solana-skr-funding-thesis`). monetization-v2-spec.md
   already reflects SKR, so the docs disagree with each other.
7. **Unity editor version drift** — **port-notes/package-manifest.md** + several QA docs say `6000.4.7f1`;
   live editor is **`6000.4.8f1`** (memory `unity-batchmode-relaunch-quirk`).
8. **React `src/` paths in shipped specs** — **monetization-v2-spec.md**, **dungeon-3d-healers-cottage-
   design.md**, **realm-map-data.md**, and the port-note contracts reference `src/lib/`, `src/components/`,
   `src/content/quests/`, `src/modules/wallet/`. Those are the **pre-port React repo**; the Unity client is
   the only live consumer. Path references are historical, not actionable in this repo.

### Dead / duplicate / superseded docs

- **build-mode-architecture.md** (lowercase, WO-108 spec) is superseded by **BUILD_MODE_ARCHITECTURE.md**
  (uppercase reconciliation, "~70% already built"). Two same-named docs, one stale.
- **WO_ROI_TRIAGE.md** superseded same day by **ROI_PLAN_2026-06-03.md** (the doc says so).
- **recovery-work-orders.md** + **claude-code-work-order.md** premised on a GUID-drift diagnosis that
  **diagnosis-report.md** disproved — dead recovery plans.
- **refactor-feature-modules-spec.md** targets the React `Village3D.tsx` monolith — irrelevant to the
  Unity asmdef structure; pre-port artifact.
- **port-notes/animation-setup.md** superseded by **ANIMATION_PIPELINE.md** (the Humanoid two-tier canon).

### Scene-gated / disabled / not-yet-built (SPEC docs describing things not live)

- Most of groups C/D/E/F are **SPEC** — designed, not built: CHARACTER_*/WORLD_ENGINE/ENGINE_MASTER_PLAN,
  CATALOG/PLAYER_BASE/SCROLL_BLUEPRINT/WORLD_COLLECTION, BATTLE_2D_PARTY/MONSTER_FAMILY/ENCOUNTER/ALERT,
  RESOURCE_ECONOMY/TALENT_TREE_V2/ITEM_DROPS. Treat as intent, not as a description of current code.
- **admin-console-spec.md / anti-cheat-spec.md / monetization-v2-spec.md** describe a **backend that was
  never deployed** (memory `backend-never-connected`) — pre-deploy gates, not live systems.
- **port-notes/dev-panel.md** — the DevTools console is **compiled OUT of release builds** (intentional
  release-gate, not a bug).
- **port-notes/hud-module.md** — UXML-sourced HUD; per memory `uxml-uidocuments-dont-render-in-builds`
  the project moved to **code-built UI** law; the uxml HUD path is the discouraged pattern.

### Broken / contradictory

- **Internal canon contradiction:** v2-unity-port-spec (Avalon/Blaise) vs DESIGN-DECISIONS/STORYLINE
  (Elarion / no-Blaise) — the two binding-tier docs disagree. DESIGN-DECISIONS wins (it's the changelog
  of *what changed away from* the port spec).
- **Economy-currency contradiction:** PI_PITCH/NORTH_STAR (Pi) vs monetization-v2-spec/wallets-of-record
  (SKR/Solana). SKR wins (later lock + the staking thesis).
- **No comment-vs-CODE mismatch is assertable from this area** — `docs/` contains no `.cs`. The HeroLocomotion
  "pure transform vs NavMeshAgent" class of bug lives in the `village-hero` code section, not here; the
  analogous failure mode *in docs* is the stale-canon list above, which a reader is just as likely to trust.
```
