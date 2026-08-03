# MASTER BACKLOG - everything needing done (known dictionary, 2026-07-19)

> # ⚠ ROWS ARE STALE — many have SHIPPED (flagged 2026-08-03)
> **Cross-check any row against `CANON_GROUND_TRUTH_2026-08-03.md` + the 07-22 §5 module digests before
> treating it as open.** Known-shipped since this audit: DG-01 dungeon exit, DG-02 dungeon dressing,
> CITY-01 wave curve, ECON-01/02/03, FTUE-01/02, the dungeon prop/light pass, the raid rescale + spire
> objective, raid troop animation + magenta sweep, and the defense-cap unification. The VERDICT paragraph
> below describes a July-19 tree and reads as far bleaker than the current one.
>
> **Why it is stale:** the 2026-08-02 Sunday sweep skipped step 5 (refresh the known dictionaries).
> Banner per §15 — frozen ledger, not rewritten. Its *findings* remain a good work source; its *statuses*
> do not.

> Comprehensive find-all-work audit: 60 agents across 11 systems. The plumbing is mature; the game
> breaks at the WIRING + CONTENT edges (built systems disconnected from the reachable surface).
> Refresh every Sunday (SUNDAY_HOUSEKEEPING.md). VERDICT: The engine, plumbing, and instrumentation are genuinely strong; the gap between "built" and "a complete good game" is overwhelmingly one of disconnection and dead content — the systems that would make it feel finished are authored but never wired to the one surface the player reaches. The only playable dungeon is an exit-less, prop-less, light-less box while a full dungeon-mechanics stack and a 211-prop KayKit pack sit unused; the wave difficulty curve, wall defense, and per-kill reward economy are authored-but-dead so the core defend loop neither escalates nor rewards; ~90% of the soundscape is placeholder synth while licensed WAVs sit at the wrong path; boss/melee/status VFX render as billboard squares; paid packs charge real crypto and silently drop the goods; and the FTUE teaches mecha

**Stats:** {"totalFindings": 124, "note": "Materials + Economy-balance audits were truncated in the source data; true total is higher", "bySeverity": {"P1": 21, "P2": 59, "P3": 44}, "byKind": {"broken": 42, "missing": 39, "design": 20, "polish": 23}, "bySystem": {"dungeon": 20, "vfx": 16, "battle": 14, "economy_progression": 14, "economy_balance": 7, "audio_sfx": 14, "ftue_onboarding": 12, "ui_functional": 10, "ui_visual": 9, "city_wave_pillar": 6, "materials_render": 2}, "newBeyondKnownFeltBugs": 118, "knownFeltBugsCovered": 9, "newInstancesOfKnownClass": 6}

# MASTER BACKLOG — Echoes of Elarion / Defenders of the Realm

_Synthesized 2026-07-19 from 11 parallel read-only audits (Dungeon, VFX, Battle, UI-functional, UI-visual, FTUE, Economy, Materials, City/Wave pillar, Audio/SFX, Economy-balance). 124 enumerated findings (Materials + Economy-balance sections were truncated in the source, so true count is higher). All findings are BEYOND the owner's known felt-bug list unless flagged (context). Format per item: **[SEV] title** — one-line fix — effort — evidence._

> **The through-line:** the plumbing is mature almost everywhere; the game breaks at the WIRING and CONTENT edges. Systems that are "built" are frequently disconnected from the one reachable surface (the composed dungeon has none of the dungeon mechanics; authored SFX/VFX/portraits/packs never reach the runtime paths; the wave difficulty curve, wall defense, and kill-reward economy are all authored-but-dead). Fixing "wire the built thing to the reachable path" is the dominant, highest-leverage class of work.

---

## TOP 20 DO-NOW (highest leverage, ordered)

1. **[P1] Wave difficulty scaling is DEAD** (City) — create/assign `WaveScalingCurve.asset` in both scenes + runtime null-fallback; wave 19 enemies currently spawn at wave-1 stats. `CITY-01`
2. **[P1] Core wave loop pays flat 4 gold + 0 hero XP per kill** (Economy) — add `xpReward`/`coinReward` to every `enemies.json` row; the most-played mode gives no progression. `BLIND-03-01`
3. **[P1] Building-upgrade tap is a no-op** (Economy) — KNOWN felt-bug; root is the dual city-tier vs legacy-level fork claiming the same buildings — pick one authority. `BLIND-03-02 / ECON-06`
4. **[P1] dg_starter_loop is a roach motel — no exit exists** (Dungeon) — bake a return portal/trigger into `PopulateForPlay`; player is trapped, must force-quit. `DG-01`
5. **[P1] Playable dungeon rooms are bare primitive boxes** (Dungeon) — add an archetype-driven KayKit dressing pass; 211-FBX prop pack is imported and unused. `DG-02`
6. **[P1] Vendor/raid/training modals bypass PanelManager + back button** (UI) — register Shop/Train/Raid/TowerSwap/WelcomeBack with the arbiter; fixes stacking + Android-back at once. `UIF-01 / UIF-02`
7. **[P1] Echo/pet modals stack unreadably** (UI) — KNOWN felt-bug; same class as #6 — route EchoCard/roster/WelcomeBack/GearOffer/TalentTree through PanelManager. `UI-05 / UIF-04`
8. **[P1] Paid packs advertise Glimmer + convenience tokens but grant neither** (Economy) — bridge `ApplyPackContents` to `GlimmerCurrencyService`; real crypto charged, goods silently dropped. `ECON-01`
9. **[P1] Pack cosmetics land in a store the shop never reads (unequippable)** (Economy) — grant SKUs into `GlimmerCurrencyService` on pack purchase. `ECON-02`
10. **[P1] Authored/licensed SFX orphaned — game runs on placeholder synth** (Audio) — one editor mirror pass copying real WAVs into `Resources/Sfx/` upgrades the whole soundscape. `BLIND-02-A01`
11. **[P1] Swing whoosh, weapon-draw, footsteps, dragon-roar are SILENT** (Audio) — mirror the 4 clips (covered by #10) or add synth fallbacks; combat/movement have no sound. `BLIND-02-A02 / A03`
12. **[P1] Wall upgrades give the Heart ZERO protection** (City) — `heartDamageMultiplier`/`spikeDamage` have no loader/consumer; wire walls.json or delete the false mitigation copy. `CITY-02 / CITY-03`
13. **[P1] Founding choice (Default Town vs Build Your Own) is unreachable** (FTUE) — call `FoundingChoiceController.PresentOrContinue` from the HeroSelect bypass branch. `FTUE-01`
14. **[P1] FTUE teaches storefront defensive placement that doesn't exist** (FTUE) — enemies never target storefronts; rewrite the lesson or build storefront targeting. `FTUE-02`
15. **[P1] FTUE tells player to tap a world "Echo light" that was scrapped** (FTUE) — repoint copy to the real "Pets" roster button. `FTUE-04`
16. **[P1] Hero weapon renders magenta (dead Standard shader)** (Materials) — KNOWN felt-bug; source-fixed via `RecoverWeaponMaterialsToUrp` — verify + extend to enemy props (`MAT-01`, armed behind flag).
17. **[P1] URP Terrain shader not pinned in build (pink-floor risk)** (Materials) — add `IPreprocessBuildWithReport` hook; `EnsureShadersIncluded` is a manual menu item today. `MAT-02`
18. **[P1] No crystal PRODUCTION building, yet crystal is the dominant sink** (Economy) — give crystal-mine a yield ladder or re-price every crystal sink to node throughput. `BLIND-03-03`
19. **[P2→DO-NOW] Ballista renders white (missing material) + orphaned castle VFX + no headless screenshot pass** (Materials/VFX) — KNOWN felt-bugs; fix ballista material, replicate ArcaneAura orphan-guard on HealingFountain/EnvironmentVFX, and stand up the AutoPilot screenshot gate (`VFX-13`).
20. **[P2→DO-NOW] Echo card copy confusing + "Leveled Up to 1" nonsense** (FTUE/UI) — KNOWN felt-bug; apply WO-752 Part A essence copy + "awaken" header for count==1 across all 6 Echo rows. `FTUE-09`

---

## MASTER BACKLOG BY SYSTEM

### CITY / WAVE-DEFENSE PILLAR (the most-played surface)
- **[P1] `CITY-01` Wave stat-scaling dead** — WaveScalingCurve null in both scenes, no asset exists; enemies never escalate — M — `WaveManager.cs:115,1427`, scene YAML `:1614/:2848`.
- **[P1] `CITY-02` Wall upgrades give Heart no protection** — walls.json fully orphaned (no loader), `heartDamageMultiplier` zero consumers — M — `WallTierData.cs`, `walls.json`.
- **[P1] `BLIND-03-01` Flat 4 gold + 0 XP per kill** — enemies.json has no reward fields — M — `Enemy.cs:2278,2293`.
- **[P2] `CITY-03` Spiked Steel (top wall tier) does nothing** — `spikeDamagePerSecond` no consumer, same mesh as tier 2 — M.
- **[P2] `CITY-04` heart.json orphaned** — HeartController hardcodes 0–100 scale, diverges from canon (160 HP, phases, regen) — M.
- **[P2] `CITY-06` No headless proof wave curve escalates** — add DataRegression asserting applied multiplier >1 past wave 1 — M.
- **[P3] `CITY-05` Wave-1 canon drift** — comment says "8 walkers", authored wave is 4 mixed — S.

### DUNGEON PILLAR (the only reachable dungeon is un-shippable)
- **[P1] `DG-01` No exit/return — roach motel** — S/M.
- **[P1] `DG-02` Bare primitive rooms, zero props** — L.
- **[P2] `DG-03` Rich DungeonController mechanics never meet composed scene** — extract a scene-agnostic `DungeonRuntimeBootstrap` — L.
- **[P2] `DG-04` "Hold the last light" lantern/darkness pillar absent** from playable dungeon — M.
- **[P2] `DG-05` Archetype metadata drives no content** (reward/lore/boss rooms indistinguishable) — L.
- **[P2] `DG-06` No boss/climax** — whole content-room library unused — M.
- **[P2] `DG-07` No reward loop** — WO-749 loot wired only to healers-cottage — M.
- **[P2] `DG-08` Stair rooms are dead-ends leading nowhere** — M.
- **[P2] `DG-09` Composed scene saved BINARY not YAML** (corruption class that broke d4) — S.
- **[P2] `DG-10` Dungeon HUD + crafting panel are UXML** → render empty on device — M.
- **[P2] `DG-13` No checkpoint/respawn anchor** — death = full restart — M.
- **[P2] `DG-19` Only one hand-authored graph, no variety/generation** — L.
- **[P3] `DG-11` Tincture + Lightbearer Cloak built, zero callers** — S.
- **[P3] `DG-12` `inDarkness` hardcoded false** — encounter multiplier dead — S.
- **[P3] `DG-14` Single shared material, no visual variety** — M.
- **[P3] `DG-15` Enemy spawns hardcoded to room ids** (brittle) — S.
- **[P3] `DG-16` No ambient dungeon audio** — S.
- **[P3] `DG-17` Anchor_Center marker never consumed** — S.
- **[P3] `DG-18` Stale "portals gated off" comment** while flag defaults on — S.
- **[P3] `DG-20` No traps/hazards** despite hazard props imported — M.

### ECONOMY / PROGRESSION / MONETIZATION
- **[P1] `ECON-01` Packs grant no Glimmer/tokens** — M.
- **[P1] `ECON-02` Pack cosmetics unequippable** (split-brain ownership) — M.
- **[P1] `BLIND-03-02` Two parallel upgrade economies for same buildings** (likely root of upgrade-tap no-op) — L.
- **[P1] `BLIND-03-03` No crystal production building** — M.
- **[P1] `BLIND-03-04` Difficulty curve far shallower than player-power growth** — L.
- **[P1] `BLIND-03-05` Six currencies, inconsistent sinks; Iron under-demanded** — L.
- **[P2] `ECON-03` Building perks over-gated one Village-Tier too high** (off-by-one, first perk unbuyable) — S.
- **[P2] `ECON-04` Arcane Forge Magic tier + whole Magic/TechTree axis unreachable dead content** — M.
- **[P2] `ECON-05` Resource buildings pay income even when never built** — M.
- **[P2] `ECON-07` Upgrade panel shows in-session pool while spend uses GameState (Wood/Iron divergence)** — S.
- **[P2] `ECON-08` Convenience tokens advertised on every pack, no store/effect** — L.
- **[P2] `BLIND-03-06` Dead bossOnly gem loot on enemy-source tables** — S.
- **[P2] `BLIND-03-07` Two conflicting Echo-slot unlock systems** (milestones vs wave-clears) — M.
- **[P3] `ECON-09` BattlePass is a dormant third ownership store** — M.
- **[P3] `ECON-10` Orphaned FarmUpgrades/WatchtowerUpgrades json (dead stone economy)** — S.
- **[P3] `ECON-11` Village Tier caps at 3 but ladders reach 4–6** (top tiers collapse onto one gate) — S.
- **[P3] `ECON-12` Resource-building income only ticks in home hub, no offline catch-up** — M.
- **[P3] `ECON-13` SKR is the only purchasable rail, and it's a devnet stub** — M.
- **[P3] `ECON-14` GameState.Magic is an earn-nothing/spend-nothing surfaced currency** — M.

### ONBOARDING / FTUE
- **[P1] `FTUE-01` Founding choice unreachable** — S.
- **[P1] `FTUE-02` Teaches unbuilt storefront defense** — L.
- **[P1] `FTUE-03` WO-752 Part B (exit interjection → pet tutorial) entirely unbuilt; no pet-tutorial component exists** — L.
- **[P1] `FTUE-04` Teaches tapping a scrapped world "Echo light"** — M.
- **[P2] `FTUE-05` Core claim loop (silo → wallet) never taught** — M.
- **[P2] `FTUE-06` `ctx_gear_equip` hint dead — trigger signal never raised** — S.
- **[P2] `FTUE-07` Founding Echo taught three times, collision-prone** — M.
- **[P2] `FTUE-08` First-fight depends on a preview flag; reverting leaves a non-skippable empty 120s wait** — M.
- **[P2] `FTUE-09` Essence reframe unbuilt; card reads "Ice Elemental" (folds in "Leveled Up to 1")** — M.
- **[P3] `FTUE-10` Shipped copy carries acknowledged STALE flags** — S.
- **[P3] `FTUE-11` Legacy OnboardingFlow not gated on ff.tutorialv2** (double-tutorial landmine) — S.
- **[P3] `FTUE-12` Founding copy pushes Harvest but Frosthowl's lane is Exploration** — S.

### BATTLE / COMBAT FEEL
- **[P2] `BC-01` Left-click double-fires melee AND ability Q** (knight leaps unexpectedly every ~6s) — S.
- **[P2] `BC-02` Arena gear drop AUTO-EQUIPS over better gear** (silent downgrade, no inventory) — M.
- **[P2] `BC-03` Ultimate (R) has no cast VFX** (variant 4 = null) — S.
- **[P2] `BC-04` All residual status VFX suppressed** (burns/poison/heal/taunt/shield invisible — acute for colorblind owner) — S.
- **[P2] `BC-05` Per-ability authored VFX all dead** (every skill looks identical) — M.
- **[P2] `BC-06` Knight casts play unarmed martial-arts KICKS** — M.
- **[P2] `BC-09` Arena is one bare clearing for every fight** (no cover/props/hazards/verticality) — L.
- **[P2] `BC-10` No manual target-switch** — enemy role system (focus healer/caster) unusable — M.
- **[P3] `BC-07` Heal cast renders as blink/fireball** — S.
- **[P3] `BC-08` Q/W cast beats silent (no wind-up VFX)** — S.
- **[P3] `BC-11` Every fight = single-pack clear (no waves/phases/objectives)** — L.
- **[P3] `BC-12` Knight carries a MANA pool** (off-brand for melee) — M.
- **[P3] `BC-13` Ability descriptions reference the Heart/village that don't exist in arena** — S.
- **[P3] `BC-14` Arena defense-unit stats hardcoded; SKR wager is a client stub** — M.

### VFX
- **[P2] `VFX-01` All Boss & Elite VFX + SFX fall to procedural squares/silence** — the marquee fight is bare — M.
- **[P2] `VFX-02` ~20 owner-tagged Hovl per-tower keys dead** (no consumer) — L.
- **[P2] `VFX-03` Every VfxManualPicks row isLoop=true** (latent loop-cap leak the moment they're wired) — S.
- **[P2] `VFX-04` Dungeon scene has zero ambient VFX** (no torch fire/fog/portal glow) — L.
- **[P2] `VFX-05` Hero basic melee fires no impact/slash VFX** — S.
- **[P2] `VFX-07` Empowered-tower glow (Aura_EmpowerTower) fully unbuilt** — M.
- **[P2] `VFX-11` Three parallel projectile-VFX systems, no shared truth** — L.
- **[P2] `VFX-13` No headless VFX-coverage/key-join oracle** (would have caught most of these) — M.
- **[P3] `VFX-06` Enemy melee VFX-silent** — S.
- **[P3] `VFX-08` knight castHeal plays blink swirl not heal** — S.
- **[P3] `VFX-09` Portal enter/exit bursts procedural** — S.
- **[P3] `VFX-10` Env/pet/decal types uncataloged → squares** — M.
- **[P3] `VFX-12` Typo'd catalog keys silently no-op** — S.
- **[P3] `VFX-14` Hovl pooled instances skip URP magenta-proof** (2D/logic/RPG-pack risk) — S.
- **[P3] `VFX-15` `_hovlKeyOf` write-only dead state** — S.
- **[P3] `VFX-16` Dead rows Frost_Projectile/Collector_Full** (Collector_Full = cheap high-value juice) — S.

### AUDIO / SFX (near-ownerless pillar)
- **[P1] `A01` Authored SFX orphaned by Resources path mismatch** — M.
- **[P1] `A02` Swing/weapon-draw/dragon-roar OUTRIGHT SILENT** — S.
- **[P1] `A03` Footsteps silent; two competing footstep systems** — S.
- **[P2] `A04` motion-castings sfxId 21/22 empty** (MotionCaster actions silent) — M.
- **[P2] `A05` Lantern low-oil flicker/breath/oil audio silent** — S.
- **[P2] `A06` Heartwood ambient bed + hit/fall stingers silent** — M.
- **[P2] `A07` audio-mix.json ducking/transitions authored but never implemented** — M.
- **[P2] `A08` UI click/confirm/deny coverage near-zero** (licensed pack unused) — M.
- **[P2] `A09` Music pool refs missing clips; authored battle/boss tracks orphaned; two battle-music systems** — M.
- **[P2] `A10` No SfxId→clip oracle** (coverage unverified, runs on synth) — S.
- **[P3] `A11` ProceduralSfx recipes duplicated runtime vs editor** (drift) — S.
- **[P3] `A12` No general world/hub/overworld ambient bed** — M.
- **[P3] `A13` PlayerAttackController whoosh/perfect-hit clips dead on runtime hero** — S.
- **[P3] `A14` Enemy per-type hit/death/cast sets unassigned → uniform synth** — M.

### UI — FUNCTIONAL & VISUAL
- **[P1] `UIF-01` Vendor/raid/training modals bypass PanelManager** (stack, no world-prompt suppress, no battle-lock) — M.
- **[P1] `UIF-02` PAUSE/BACK + Android back can't dismiss non-arbiter screens** — S (fixed by UIF-01).
- **[P2] `UIF-03` Barracks Train UI opens with no combat gate** (trainable mid-battle) — S.
- **[P2] `UIF-04` Echo roster→card is a drill-down dead-end** — M.
- **[P2] `UIF-05` WelcomeBackPopup is click-through** (taps pass to HUD) — S.
- **[P2] `UIF-06` RaidDeploy "Auto Recommend" stub; no real troop selection** (raid-prep hollow) — L.
- **[P2] `UI-01` Q/W/E/R keyboard-letter badges on combat medallions** (touch game) — S.
- **[P2] `UI-02` Three clashing portrait art styles, none match the world** — L.
- **[P2] `UI-03` AI-gibberish "rune" text baked into Echo portraits** — M.
- **[P2] `UI-04` Help toast lists keyboard controls on a phone** — S.
- **[P2] `UI-05` WelcomeBack/EchoTutorial/GearOffer/TalentTree bypass PanelManager** — M.
- **[P2] `UI-06` Kit text bands authored as height fractions** (systemic TextFitGuard churn) — M.
- **[P3] `UIF-07` TowerSwapMenu is UXML gated on inspector PanelSettings** (silently dead on device) — M.
- **[P3] `UIF-08` Wallet/demo surfaces outside arbiter; Jupiter swap is UXML+stub** — M.
- **[P3] `UIF-09` MusicSelectionPanel self-handles Esc** (dead on mobile) — S.
- **[P3] `UIF-10` No editmode guard that every 31000+ modal registers** — M.
- **[P3] `UI-07` Economy buildings show identical portrait every tier** (no visual progression) — M.
- **[P3] `UI-08` Five overlay canvases share sortingOrder 5000** (undefined draw order) — S.
- **[P3] `UI-09` Owner DEV chip overlaps bottom-left backup D-pad** — S.

### MATERIALS / RENDER
- **[P1] `MAT-02` URP Terrain/Lit shader not pinned in build** — pink-floor risk; `EnsureShadersIncluded` is manual-only — S.
- **[P2] `MAT-01` Enemy weapon props get no shader recovery** (orc axe magenta/white, armed behind EnemyWeapons flag) — S.
- _(Materials audit truncated in source — extract shared `MaterialRecovery` utility, add pre-build validation, and de-duplicate the 7 parallel recovery copies are the flagged themes.)_

---

## CONTENT / DESIGN — the bigger builds (frame as Work Orders)

**WO-A: Dungeon dressing & mechanics unification (L, P1)** — Author an archetype-driven `DungeonDresser` that scatters KayKit props (torches=light source, chest in reward rooms, pillars/banners in halls, spike/grate hazard tiles), bake an exit portal + checkpoints, and attach a scene-agnostic `DungeonRuntimeBootstrap` so the composed dungeon inherits Lantern/darkness, lore, chests, WO-749 loot, ambient audio, and a boss climax. Extend the graph with BossKeep→RewardVault→exit. Covers `DG-01..08,13,16,20`, `VFX-04`, `A05`. This single arc turns the roach-motel into the "hold the last light" pillar.

**WO-B: Per-ability + per-tower VFX & SFX authoring pass (M, P2)** — Add a data seam (`vfxCast/vfxProjectile/vfxImpact` + `sfxId`) consumed by TowerCombat, PooledProjectile, HeroAbilities (per-ability fallback), and ActionBundlePlayer. Populate boss/elite/portal/empower catalog rows, fill motion-castings sfxId + melee impact keys, wire the owner's ~20 dead Hovl tower keys, fix the isLoop mis-tags, and add residual status loops. Ship the `VFX-13` + `A10` coverage oracles first so gaps can't re-ship silently. Covers `VFX-01,02,03,05,06,07,11`, `BC-03,04,05`, `A04,A14`.

**WO-C: Audio soundscape reconnection (M, P1)** — One editor mirror step populates `Resources/Sfx/` from the authored/licensed WAVs (Combat pack + Leohpaz UI/Battle + Hovl), reconciles the music clip names, builds the Heartwood ambient beds + lantern audio + UI confirm/deny + a per-context ambient bed, and stands up a `MixDirector` that applies audio-mix.json ducking/boss-silence. Converts a ~90%-synth soundscape to authored audio. Covers `A01,A02,A03,A05,A06,A07,A08,A09,A12`.

**WO-D: Battle-feel & arena content (L, P2)** — Author 2–3 tactical arena layouts per biome (cover pillars, chokes, a hazard, destructible loot props), add encounter templates (multi-wave / reinforcement / protect-objective / boss phases), ship mobile tap-to-target so the enemy role system is usable, decouple the melee click from ability-Q, and route arena gear drops into an inventory (compare-before-equip). Covers `BC-01,02,09,10,11`.

**WO-E: Economy coherence & source-vs-sink model (L, P1)** — Author one `ECONOMY_BALANCE` spec: one upgrade authority per building (retire the legacy fork), a faucet→sink table per currency (fix Iron under-demand, crystal production, Magic dead axis), enemy kill rewards scaled by role, and a wave-scaling curve modeled against the real player-power curve. Reconcile the Echo-unlock fork and the pack Glimmer/cosmetic/token grants. Covers `BLIND-03-01..07`, `ECON-01..08`, `CITY-01`.

**WO-F: Portrait & art-style consolidation (L, P2)** — Lock ONE portrait style, regenerate all vendor/building/Echo portraits to it (kill the AI-gibberish rune text), author tier-2/-3 economy-building portraits (or a procedural tier treatment), and drop the Q/W/E/R + keyboard-control chrome for touch. Covers `UI-01,02,03,04,07`.

**WO-G: Modal-arbiter cleanup + FTUE honesty pass (M, P1)** — Register every hand-rolled 31000+ modal with PanelManager (+ editmode guard), fix the Echo drill-down and click-through popups, make the founding choice reachable, rewrite FTUE copy to teach only real mechanics (claim loop, real Pets button, real controls), build WO-752 Part B exit-interjection→pet-tutorial, and apply the Essence reframe. Covers `UIF-01..05,10`, `UI-05`, `FTUE-01..12`.

---

## STATS

See the `stats` object: 124 enumerated findings (Materials + Economy-balance audits truncated in source → true total higher), 21 P1 / 59 P2 / 44 P3; by kind 42 broken / 39 missing / 20 design / 23 polish; essentially all 124 are NEW beyond the ~8–9 known felt-bugs (auditors deliberately excluded the known list; only a handful are new instances of a known class — modal-stacking, hotkey-letters, magenta/white, "Leveled Up to 1").

---

## VERDICT

The engine, plumbing, and instrumentation are genuinely strong — modal arbiter, VFX pooling/magenta-proofing, wave flow-tracing, material recovery net, save/economy scaffolding all exist and are well-built. **The gap between "built" and "a complete good game" is almost entirely one of DISCONNECTION and DEAD CONTENT: the systems that would make it feel finished are authored but never wired to the one surface the player actually reaches.** The only playable dungeon is an exit-less, prop-less, light-less gray box while a full dungeon-mechanics stack and a 211-prop pack sit unused; the wave difficulty curve, wall defense, and per-kill reward economy are authored-but-dead so the core defend loop neither escalates nor rewards; ~90% of the soundscape is placeholder synth while licensed WAVs sit at the wrong path; boss/melee/status VFX render as billboard squares; paid packs charge real crypto and silently drop the goods; and the FTUE teaches at least two mechanics that don't exist. Fix the wiring (turn dead assets and dormant systems back on), fill the reward/scaling/audio joins, and stand up the missing headless coverage oracles, and the game crosses from "impressive tech demo with a hollow middle" to "complete and good" — because most of the content is already in the repo, just unplugged.