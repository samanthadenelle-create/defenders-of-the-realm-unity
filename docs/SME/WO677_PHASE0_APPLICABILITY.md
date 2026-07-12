# WO-677 Phase 0 — Applicability Assessment (all SME dossiers)

**Date:** 2026-07-12
**Input:** all nine SME dossiers (`docs/SME/README.md` router set + `docs/HOVL_STUDIO_SME.md` + the store ledger), each opportunity/gap assessed against the actual code at HEAD (branch `wip/village2-and-f8-tickets`, commit `c6912c9d`).
**Question answered per item:** can the pack's intended logic be applied to our implementation successfully?
**Verification:** every top claim was spot-checked against the working tree (file:line), not taken on faith. Discrepancies found are flagged inline and collected in §Verification Notes.

---

## Executive summary

The dossier fleet's cross-pack headline holds up under code verification: **the project owns far
more production-ready content than it uses, and almost every high-value gap is a small, well-seamed
wiring task, not an art or architecture project.** The single biggest player-felt lever is not an
asset at all — it is one number in one profile (global bloom at 0 while every Hovl demo runs at 5).

### IMPLEMENT-NOW list (ranked by player-felt value per unit effort, max 8)

| # | Item | Effort | What the player gets |
|---|---|---|---|
| 1 | **Global bloom on** — raise `Assets/DefaultVolumeProfile.asset` Bloom intensity from 0 (verified) to ~1.5–2.5, threshold ~1.1; template already in-repo at `BattleArena.cs:163-166` | S | Every VFX in the game (Hovl, Lana, Spells Pack) gains the demo glow at zero per-effect cost — the root cause of "nothing like the demo" |
| 2 | **Raid BGM code fix** — add the missing `Raid` entry to `MusicTrackRegistry.Defs` (verified absent, `MusicTrack.cs:139-159`) + `ClipFor`/`SetMusicClip` cases in `AudioService` (~8 lines) | S | Offensive raids finally play their shipped 198 s brass theme instead of leftover ambient |
| 3 | **Lana material conversion, committed** — batch-swap the 19 tracked legacy `.mat` files to URP Particles/Unlit with correct additive blends (reuse `ConfigureUrpParticleBlend`) | S | ~35 catalog VFX render correctly everywhere (not just via VFXManager's per-spawn heal), and every spawn stops paying a renderer-walk tax |
| 4 | **Hovl demo-parity code pass** — hue-shift tint (port `HS_CameraHolder` algorithm into `ApplyStartColor`, `VFXManager.Hovl.cs:344-351`, verified flat-fill) + projectile soft-stop (`ReturnHovlAfterDelay` exists at `:325-329`, just not called by `Stop()`) | S/M | Recolored spells keep their bright cores; projectile trails fade naturally instead of vanishing mid-air on impact |
| 5 | **Blink 500 Spell Icons → concept-icons.json** — extend `RpgUiImporter.BuildEntryTable()` (or a sibling `BlinkIconImporter`) to mirror icon classes; add rows to the verified `ConceptIconResolver` seam (`Assets/_Modules/Core/UI/ConceptIconResolver.cs`, data at `Resources/Data/Canonical/concept-icons.json`) | S/M | Real professional ability/talent icons replace placeholder glyphs; this doubles as Icon Caster Phase 1 |
| 6 | **Knight motion-castings reconciliation** — the JSON's own owner-picks comment (2026-07-11) says `skill1=atk_jump`, but the live row still points at `fist_flyingkick_m.fbx` (verified — see Verification Notes); apply the documented picks via Motion Caster + rebake | S | The owner's already-ruled Heroic Leap replaces the off-theme kung-fu kicks; skill animations match class fantasy |
| 7 | **Audio quick wins** — import the purchased-never-downloaded leohpaz pack (48 clips) + drop one WAV at `Resources/Sfx/Heal` (verified missing) | S | The Knight's owner-picked heal cast stops being silent; up to ~20 synth placeholder tones (explosions, combos, level-up, tower fire) become authored audio |
| 8 | **Supercyan roster expansion** — add map entries to `SupercyanResourceWire.cs` (verified map at `:34`) for the six unused Humanoid bodies (barbarian, demon, mage, orc, skeleton, wizard) + `troops.json`/enemy rows | S/M | Instant troop/NPC/enemy variety from bodies already rigged, already URP-fixed, already facing-rule-documented |

### Caster-tool build order recommendation (from the fit analysis below)

1. **Icon Caster** — clearest seam (ConceptIconResolver + concept-icons.json, both verified), biggest untouched inventory (608 PNGs, zero references), pure data output. Build first.
2. **Audio Caster** — after leohpaz import there are ~112 clips to audition/map onto the coverage-audit gap table; the drop-in convention (`Resources/Sfx/<Name>`) makes the action trivial and safe.
3. **Character Caster** — the largest inventory (KayKit 33 Mystery characters + 8 Adventurers, polyperfect 240 villager rigs, Blink 12 orcs, Supercyan 6 idle bodies) but the most per-item work (committed-copy step, rig-family routing, roster canon decisions). Build third, with the rig-verdict logic from the dossiers baked in.
4. **Gear Caster** — weapons are already auto-cataloged end-to-end (GearCatalogGenerator → Addressables → EquipmentController native seating); the tool adds owner curation + grip/Offset Forge handoff. Useful, not urgent.
5. **Texture/Environment Caster** — gated on the WO-479 chunk-composer direction; park until that lands.

### Compliance (⛔ — ranked by risk, see full section at the end)

1. **Black Dragon license** — the apex wave-20 boss model is CC BY-NC / editorial on every free tier. Ship blocker for any monetized release (Pi hackathon, July 31). Buy the CGTrader commercial license or replace.
2. **Audio provenance** — all three logged Freesound IDs resolve to unrelated sounds; the 17-WAV recorded combat set's licenses are effectively unknown, and Suno commercial rights need a one-line confirmation. Pre-launch re-verify required.

---

## Verdict cards — by pack

Legend: **A-NOW** = APPLICABLE-NOW (wiring named) · **A-WORK** = APPLICABLE-WITH-WORK (gap + effort S/M/L + WO) · **N/A** = NOT-APPLICABLE (why).

---

### 1. Hovl Studio RPG VFX Bundle (docs/HOVL_STUDIO_SME.md)

**H1. Global bloom (dossier rec #1)**
- **Verdict: A-NOW.** Seam verified: `Assets/DefaultVolumeProfile.asset` Bloom `active: 1` but `intensity: 0`, threshold 0.9 (read from the asset this session). In-repo template: `BattleArena.cs:163-166` (`BuildArenaBloom` at `:907+`) already builds a local volume at 1.4/1.2. Change the default profile value (or add a global gameplay Volume the arena way); verify main cameras have `m_RenderPostProcessing: 1`.
- **Player-felt value:** every effect in the game gains the halo/glow the vendor authored the HDR materials for — the single largest "demo vs ours" delta.
- **Caster-tool fit:** direct wiring, no tool needed. (Owner felt-verifies the strength — it is a taste dial.)

**H2. Hue-shift recolor instead of flat-fill**
- **Verdict: A-NOW.** Seam verified: `VFXManager.Hovl.cs:344-351` writes one flat `MinMaxGradient(color)` into every child PS, exactly as the dossier claims. The reference algorithm ships in the pack (`HSFiles/Scripts/For demo scenes/HS_CameraHolder.cs` — cache HSV, change hue only, preserve saturation/value/alpha). ~20-line port; callers unchanged.
- **Player-felt value:** recolored fireballs/bolts keep their hot cores and halo structure instead of turning into single-tone mush that dips under the bloom threshold.
- **Caster-tool fit:** direct wiring. (The shipped VFX Caster then previews the corrected look.)

**H3. Projectile soft-stop on impact**
- **Verdict: A-NOW.** Seam verified: `ReturnHovlAfterDelay` exists at `VFXManager.Hovl.cs:325-329` but `VFXHandle.Stop()` calls the hard-clear path. Wire a `StopSoft()` (`Stop(true, StopEmitting)` + deferred return ~0.6 s) and use it from the projectile callers (`RangedAttackVFX.cs:178-181`, `HeroAbilities.cs:1424`, `ArcaneTower.cs:392+`).
- **Player-felt value:** every ranged shot's trail fades naturally behind the impact instead of popping out of existence mid-air.
- **Caster-tool fit:** direct wiring.

**H4. Impact orientation to surface normal**
- **Verdict: A-NOW** (low priority). Pass `Quaternion.FromToRotation(Vector3.up, normal)` where a hit normal is known (`Enemy.cs:1595`, `HeroAbilities.cs:1373`, `TowerCombat.cs:567`). Ground hits can stay identity.
- **Player-felt value:** wall/steep-surface hits stop looking pasted-flat.
- **Caster-tool fit:** direct wiring.

**H5. Prewarm looping auras**
- **Verdict: A-NOW** (small). Enable Prewarm on the main PS for `IsLoop` catalog rows at pool-build time (`CreateHovlInstance`, `VFXManager.Hovl.cs:278+`).
- **Player-felt value:** Heal_Aura / Aegis_Shield / Taunt_Aura appear mid-cycle instead of ramping from empty.
- **Caster-tool fit:** direct wiring.

**H6. Projectile point-light**
- **Verdict: A-WORK.** Gap: the script-free loop prefabs we use ship no Light (verified in the dossier against `Projectile 16 fire.prefab`). Effort **S/M** — pooled Light on the follower path, gated behind a quality tier (vendor warns lights are the expensive part). Belongs in the same Hovl-parity WO as H2–H5.
- **Player-felt value:** night/dusk projectiles illuminate the ground as they fly, like the demo.
- **Caster-tool fit:** direct wiring.

**H7. Pooled AOE sound ownership**
- **Verdict: A-WORK.** Gap: `HS_EffectSound` is Start()-driven and not pool-aware — first play sounds, pooled replays are silent, and the AudioSources bypass our mixer (ties to AUDIO card A6). Effort **S**. Decide one owner: strip prefab audio and route via `CoreServices.Audio`, or `PlayOneShot` on re-enable + assign `outputAudioMixerGroup`.
- **Player-felt value:** AOE spells sound every time and respect the player's volume/mute.
- **Caster-tool fit:** direct wiring (the Audio Caster could later expose the harvested Hovl `Skill *.wav` clips as SfxId overrides).

**H8. 3D Lasers Pack**
- **Verdict: N/A today.** No beam skill exists in the kit — this is a NEW FEATURE, not a gap (per §13 triage rules it goes back as a spec, not an RCA-fix). When a beam skill is specced, the prefabs are unusable without `Hovl_Laser` intact (assign `HitEffect`, call `DisablePrepare()` before pool return, re-target the raycast).
- **Player-felt value:** none until a beam skill is designed.
- **Caster-tool fit:** VFX Caster (shipped) can preview them if the catalog ever adds keys.

**H9. Distortion materials (HS_Distortion / HS_BlendDistort, 4 mats)**
- **Verdict: N/A until wanted.** Requires Opaque Texture ON in the URP asset first; nothing references them.

**Explicitly fine as-is (do not churn, per dossier §6):** string-key catalog + generator, pooling, script-free loop-prefab + own-mover pattern (vendor-endorsed), shader state, skipping `ProofUrpParticleShaders` for Hovl.

---

### 2. Sword & Shield mocap + siblings (docs/SME/SWORD_SHIELD_MOCAP_SME.md)

**S1. Morning picks via Motion Caster (skill1/skill2/parry/dodge/victory)**
- **Verdict: A-NOW — with a data-inconsistency finding.** The tool (`Assets/Editor/MotionCasterWindow.cs`, verified present) and the registry (`Assets/Resources/Data/Canonical/motion-castings.json`) are shipped. **Spot-check finding:** the knight target's own `_comment` records OWNER PICKS 2026-07-11 — `skill1 = atk_jump` (Heroic Leap), `skill2 = plain Slash`, `block = atk_shieldswipe01 with Block2 chain` — but the live `skill1`/`skill2` rows still point at `fist_flyingkick_m.fbx` / `fist_whirlwindkick_m.fbx` (verified by parsing the JSON this session). Either the picks were documented but never applied to the rows, or the rows were reverted. Reconcile first (one Motion Caster session + rebake), then add the `parry`/`dodge` rows the dossier recommends (`sword_parryup`, `sword_parrybackward01`) — noting parry/dodge also need a gameplay trigger to be felt (see S3).
- **Player-felt value:** the Knight's skills read as knightly (leap, shield rush) instead of off-theme kung-fu kicks.
- **Caster-tool fit:** **Motion Caster (shipped)** — this is exactly its job.

**S2. Shield-swipe two-beat chain**
- **Verdict: ALREADY DONE (dossier claim FAILED spot-check).** The dossier's gap #2 says "no second-beat state wired", but `KnightPackageControllerBuilder.cs:415-437` builds a `Block2` state that chains `ShieldSwipe01 → ShieldSwipe02` when the extracted clip exists, and the JSON comment confirms the owner pick relies on it. The dossier audited the older `HeroAnimatorFactory.cs` (KnightMocap controller); the live knight rows point at `HeroPackages/Knight/Animations/Extracted/` — the KnightPackage controller. No work needed beyond confirming the extracted `ShieldSwipe02` clip exists at bake.
- **Player-felt value:** already delivered (2-beat shield bash).
- **Caster-tool fit:** n/a.

**S3. Directional block/parry matrix (6 blocks + 9 parries)**
- **Verdict: A-WORK, effort L.** Gap: no gameplay concept of incoming-attack direction exists; the animator needs a selector (the `TurnDir`/`HitDir` param pattern exists to model after) and combat needs to compute the direction. This is a combat-feature WO, not a data pick. Propose: **new WO "Knight defensive matrix"** (or fold into the combat-pivot north-star lane).
- **Player-felt value:** blocks and parries that visibly answer the direction you're hit from — the pack's authored fighter identity.
- **Caster-tool fit:** Motion Caster supplies the clips; the selector is code.

**S4. 2-D strafe locomotion blend (8 walk/run directional clips + 4 turns)**
- **Verdict: A-WORK, effort M.** Gap: current tree is forward-only 1-D. Needs a 2-D blend tree in the knight controller builder + strafe input plumbed from locomotion. Propose: **new WO "Knight 2-D combat locomotion"**. Worth doing when combat-feel polish resumes; the clips are on disk and named.
- **Player-felt value:** the hero circles and backs off in combat like a fighter instead of always running forward.
- **Caster-tool fit:** Motion Caster binds the clips; the blend tree is controller-builder code.

**S5. Directional hit-reacts (`m-ss-damage-01/02/03`)**
- **Verdict: A-WORK, effort S/M.** One hit-react is wired; the two variants + a direction selector are not. Small controller-builder change + `HitDir` already exists as a param concept. Fold into S3's WO.
- **Player-felt value:** getting hit from behind looks different from getting hit head-on.
- **Caster-tool fit:** Motion Caster for the clip rows.

**S6. Idle tiers (`idle_alert` / `idle_battle`)**
- **Verdict: A-WORK, effort S** (or park). A three-tier guard hub is flavor; a single data pick could at least swap `combatIdle` variants. Low priority.
- **Player-felt value:** subtle stance variety.
- **Caster-tool fit:** Motion Caster.

---

### 3. KayKit (docs/SME/KAYKIT_SME.md)

**K1. 33 Mystery Monthly rigged characters → enemy/NPC roster**
- **Verdict: A-WORK, effort S per character (pipeline proven), M for a themed batch.** Wiring: drop FBX → `AssetImportPostprocessor` imports Generic → committed-copy into `Resources/Enemies/` (§2.3 rule — never load from the gitignored pack) → assign the `HumanoidEnemy`/`LargeEnemy` controller → `EnemyFactory` id row. `EnemyFactory.cs:350` already reserves the brute/tank id slot; `AmbientNPC.cs` already accepts real models over primitives. `KayKitAnimProof` is the visual verification harness. Roster candidates ranked in the dossier (OrcRaider, Werewolf pair, Vampire, Witch, FrostGolem/BlackKnight/Clanker heavies; Paladin/Helpers/villagers as NPCs).
- **Player-felt value:** real enemy variety and village NPCs from zero-license-cost, already-rigged models — the thing the game keeps needing.
- **Caster-tool fit:** **Character Caster** — this inventory is the tool's main payload (browse → rig verdict → preview on the shared clips → promote to roster).

**K2. Furniture Bits + RPG Tools Bits for interiors (WO-673 synergy)**
- **Verdict: A-WORK, effort M.** Gap: needs the committed-copy step + placement (castle hall dressing bake, or player-building interiors under the WO-673 pivot). Flagged "high value, untapped" since 2026-05-19. Belongs to **WO-673** (player-defined map) or a castle-interior dressing WO.
- **Player-felt value:** furnished, lived-in interiors.
- **Caster-tool fit:** Texture/Environment Caster (or direct builder work).

**K3. Dungeon Remastered full FBX warehouse for the chunk composer**
- **Verdict: A-WORK, effort L — belongs to WO-479.** The builders use only the glTF live-set subset; the 283-unique-model warehouse is the natural chunk vocabulary. Gated on the chunk-composer north-star design.
- **Player-felt value:** varied dungeon interiors.
- **Caster-tool fit:** Texture/Environment Caster, later.

**K4. Resource Bits loot/harvest visuals**
- **Verdict: A-WORK, effort S/M.** Committed-copy + loot-table visual rows. Small contained WO.
- **Player-felt value:** drops that look like what they are (gems, ore, gold piles).
- **Caster-tool fit:** could ride the Gear Caster's browse pattern; direct wiring is fine.

**K5. Skeletons 1.1 weapons as enemy-held props**
- **Verdict: A-WORK, effort S.** `EquipmentController`'s attach path + OffsetForge grips work for enemies too (dossier §5.6). Ties to the `EnemyWeapons` flag.
- **Player-felt value:** armed skeletons read as threats.
- **Caster-tool fit:** **Gear Caster** (same seat-in-hand preview + grip data flow).

**K6. Adventurers alt textures (4 palettes × 7 classes)**
- **Verdict: A-WORK, effort S.** Free variant skins for ATB enemies / NPC crowds. Colorblind note: palette variants must not be the only differentiator between enemy types — pair with shape/name.
- **Caster-tool fit:** Character Caster variant picker.

**K7. Free upstream upgrades (Character Animations 1.2, Skeletons re-download)**
- **Verdict: A-NOW (owner errand, $0).** Re-download fills the empty `Skeletons 1.1/characters/fbx/` folder and adds ~12 clips + glTF. No code.
- **Caster-tool fit:** n/a.

**K8. Mystery Series 6 purchase (~$20, Orc Brute + Cleric on-theme)**
- **Verdict: parked — owner purchase decision.**

---

### 4. polyperfect + Quaternius (docs/SME/POLYPERFECT_QUATERNIUS_SME.md)

**P1. Animate the village — 240 rigged villagers + 5-clip Generic system + crowd de-sync helpers**
- **Verdict: A-WORK, effort S/M — the highest-leverage NEXT-WO item.** The whole system is present and unused: `Rigs_M/` prefabs (Animator, no controller), `CTL_People_*` drop-in controllers, `AnimationDelay`/`AnimationOffset` (namespace `Polyperfect.Common`, asmdef present). Wiring: an editor-bake "VillagerInjector" per the dossier's verified recipe (instantiate rig prefab → assign controller or one shared 2-state Idle/Walk controller → add delay/offset). Medieval-safe skins shortlisted (knight, monk, lord, farmer pair, servant — all verified on disk). Limits respected: 5 clips only, Generic rigs (our Humanoid mocap does NOT retarget onto them).
- **Player-felt value:** MainCastle_Hall and Village2 feel alive — a direct ten-year-old-test win.
- **Caster-tool fit:** **Character Caster** (browse the 240 skins, preview animated, promote to injector list).

**P2. Prop animation for free (windmill/watermill rotation controllers)**
- **Verdict: A-NOW.** Drop `CTL_Rotation_Y_360_3s_Loop` on the `Windmill_Medieval` blades / `Watermill_Medieval` wheel at bake time — the buildings are already placed, the controllers ship in the pack, no code.
- **Player-felt value:** the village's most visible landmarks move.
- **Caster-tool fit:** direct wiring (one builder tweak).

**P3. ClaimableCamp Quaternius stub → real art**
- **Verdict: A-NOW/A-WORK, effort S.** Verified: `ClaimableCamp.cs:135-153` spawns three labeled primitive cubes ("QuaterniusEnemyProp"). The harvest pipeline (`Village2Build.HarvestQuaterniusBuildings`) already produces HouseA/HouseC/HouseD/KitTower prefabs. Gap: the pack is gitignored, so the camp spawner needs committed/mirrored copies (the `Resources/Structures` mirror pattern via `CatalogPrefabImporter` is the precedent). Small contained WO.
- **Player-felt value:** enemy outposts look like camps, not debug cubes.
- **Caster-tool fit:** direct wiring.

**P4. Enterable buildings (double-sided walls + 48 unused Window-Door modules)**
- **Verdict: A-WORK, effort L — belongs with WO-584/WO-479.** Real capability (walls model interiors), but composing interiors is the chunk-composer's job. Park until that lane opens.
- **Player-felt value:** walk into buildings.
- **Caster-tool fit:** Texture/Environment Caster, later.

**P5. Dungeon dressing from owned kits (polyperfect Fantasy_M dungeon set + free Quaternius Fantasy Props MegaKit, CC0)**
- **Verdict: A-WORK, effort M.** Zero license cost; needs the WO-479 vocabulary decision first for coherence.
- **Caster-tool fit:** Texture/Environment Caster.

**P6. Farm/ambient animals (Animals_M, 28 static meshes)**
- **Verdict: A-WORK, effort S** — static dressing only (the animated animals pack is not owned). Cow/Hen/Pig/Sheep_White around the Farm.
- **Player-felt value:** the farm reads as a farm.
- **Caster-tool fit:** direct builder dressing.

**P7. Quaternius separate collision meshes (`Collision_<Name>.fbx`)**
- **Verdict: A-WORK, effort S per piece, only where collision accuracy matters** (currently render-mesh/builder colliders suffice). Park unless a collision bug names a piece.

**P8. Catalog tier-description correction (`_T` ≠ "Tribal"; `_H`/`_L` don't exist)**
- **Verdict: A-NOW (doc fix, one line each in `docs/polyperfect-asset-catalog.md` + `docs/POLYPERFECT_NOTES.md`).** §15 same-breath canon rule.

---

### 5. Blink family (docs/SME/BLINK_SME.md)

**B1. 500 RPG Spell Icons + 25 emblems + 25 themed action-bar slots**
- **Verdict: A-NOW (the pipeline exists end-to-end — verified).** Wiring: mirror the needed classes via `RpgUiImporter.BuildEntryTable()` (`Assets/Editor/RpgUiImporter.cs`, entry table at :184-213) or a sibling `BlinkIconImporter` → committed `Resources/RpgUi/` → map ability/skill ids in `Resources/Data/Canonical/concept-icons.json` → resolved by `ConceptIconResolver` (`Assets/_Modules/Core/UI/ConceptIconResolver.cs`, verified: null-safe, cached, `override` flag for owner picks) → consumed by the HUD/talent-tree callers that already keep glyph fallbacks. Colorblind rule: pick icons by silhouette distinctiveness, never hue.
- **Player-felt value:** the ability bar, talent tree, and hero-select gain a professional visual identity overnight.
- **Caster-tool fit:** **Icon Caster — this is the tool's entire reason to exist**, and the WO-677 spec names this exact seam. Implement-now item #5 covers the manual first pass; the tool industrializes it.

**B2. Stylized Orcs Bundle (12 Humanoid-rigged NPCs + 2×22-clip sets + ready controllers + 15 weapon props)**
- **Verdict: A-WORK, effort M — owner canon decision required first.** Wiring is the proven WO-545 pattern (Addressables group per enemy family) + `EnemyFactory` rows; zero rig war (professionally Humanoid-rigged, verified in the dossier from the .meta). BUT the Tripo Knight/Orcs-first roster is settled canon — per the confirm-before-reverting rule this is an *offer* to the owner (extra families/variants/boss, or clip retargeting onto existing humanoid enemies), not a directive.
- **Player-felt value:** a full extra enemy family (or a dungeon boss) with strafes/casting/stun/death clips we currently lack.
- **Caster-tool fit:** **Character Caster.**

**B3. Obsidian full-screen prefab second pass (Inventory/Merchant/TalentTree/CharacterCreation/…)**
- **Verdict: A-WORK, effort M.** `BlinkPrefabMirror.cs` (verified present; 40 prefabs already mirrored, 80 files in `Resources/RpgUi/prefabs/`) scoped v1 to the HUD-critical set; full screens are the explicit deferred pass. Value is reference/mining for screens still built procedurally — the master-frame formula stays canon.
- **Player-felt value:** faster, more polished screen buildout.
- **Caster-tool fit:** direct (extend the mirror's scope table).

**B4. Texture bundles (~9 GB, 9 biomes — StylizedDungeon/Necromancer standouts)**
- **Verdict: A-WORK, effort L — gated on WO-479 biome/dungeon direction.** Needs mirroring/Addressables (gitignored). Park until the composer lane opens.
- **Caster-tool fit:** **Texture/Environment Caster** — its primary payload when built.

**B5. Unclaimed free armor sets (~15, ids 338224–338258)**
- **Verdict: A-NOW (owner errand, $0, claim while links are valid).** Low priority while armor swaps are junked — claim, don't import.

**B6. Cursors (10) / Decoration (38) / Shapes (9) sprites, Titillium font**
- **Verdict: A-WORK, effort S.** Custom cursor set is cheap polish through the existing importer. Park behind bigger wins.

**B7. Blink armor as hero visual / LowPoly base body for the hero / UXML for Blink UI**
- **Verdict: N/A — settled DO-NOT-REUSE (dossier §5.6).** `FeatureFlags.BlinkArmor` stays default-off; hero = dedicated Knight rig; UI stays code-built uGUI. `BlinkWardrobe` prefix-toggle dressing remains live for NPC bodies only.

**B8. Gear Caster foundation (400 weapons)**
- **Verdict: A-NOW as a pipeline (already live), A-WORK as a tool (effort M).** The full chain is verified live: `BlinkAddressableMarker` → `GearCatalogGenerator` (primary gear source, `loadVia=addressable`) → `EquipmentController` native grip-at-origin seating (verified `:101`, `:197` — `knight_starter = Native(Sword("sword_A"))` = the Blink prefab). The tool's added value is owner browse/preview-in-hand/weapons.json row + Offset Forge handoff — curation, not plumbing.
- **Caster-tool fit:** **Gear Caster** — build after Icon/Audio.

---

### 6. Non-Hovl VFX packs (docs/SME/VFX_PACKS_SME.md)

**V1. Lana Studio 19-material conversion (dossier fix rank #1)**
- **Verdict: A-NOW.** The 19 legacy materials are git-tracked (only committed VFX pack); the proven recipe exists (`ConfigureUrpParticleBlend` + `_BaseMap` bind, `VFXManager.cs:737+`); run it once as an editor pass against the material assets and commit. Do NOT use the generic `MagentaMaterialFixer` sweep (URP/Lit deadens additive glows). Keep `ProofUrpParticleShaders` armed as the safety net. Acceptance per dossier: a Lana prefab dropped without VFXManager renders correctly; zero swap logs on catalog spawns.
- **Player-felt value:** ~35 catalog VFX permanently correct everywhere + every spawn stops paying the runtime-heal cost.
- **Caster-tool fit:** direct wiring.

**V2. Spells Pack hygiene**
- **Verdict: A-NOW (tiny) / parked.** Content is URP-native and correctly wired (four mirrored prefabs). Do NOT import the sidecar URP/HDRP packages (would add 465 duplicate prefabs + a competing pipeline asset). Add the 23 demo-environment mats + 21 MB sidecars to the polish-end purge list; mark the bundled install doc stale in the next WO touching the pack.
- **Caster-tool fit:** VfxParade already parades it.

**V3. Mirza Beig Ultimate VFX**
- **Verdict: N/A — deferred-purge candidate (canon: purge waits for polish end).** Zero runtime references, officially no SRP support, heaviest texture payload. Carve-outs: individual effects can be copied out and re-materialed on URP Particles/Unlit on demand; the Plexus/ForceField/Affector scripts are pipeline-agnostic and liftable if a constellation/vortex moment is ever wanted. Never wire `[+distortion]`, `terrainRain`, `softSnowfall*` prefabs.

**V4. VfxParade source-root parameterization**
- **Verdict: A-WORK, effort S (wishlist).** One-line root change + manifest rebake lets the same in-build parade serve Hovl/Lana. Nice-to-have; the shipped VFX Caster covers editor-side browsing.

---

### 7. Character packs (docs/SME/CHARACTER_PACKS_SME.md)

**C1. Six unused Supercyan Humanoid bodies → troops/NPCs/enemy variants**
- **Verdict: A-NOW (per body).** Wiring verified: one map entry each in `SupercyanResourceWire.cs` (map at `:34`, `("Knight","SC_Footman")` pattern) → prefab variant in `Resources/Heroes/` → `troops.json`/roster row (`"modelYaw": 0` — Supercyan faces +Z). Shader conversion already done (spot-checked by the dossier: URP/Lit GUID in `fantasy_knight_body.mat`). Demon/skeleton/orc slot as enemy skins; barbarian/mage/wizard as troop classes or castle NPCs.
- **Player-felt value:** immediate roster variety with zero rig work.
- **Caster-tool fit:** **Character Caster** (or direct wiring for the first two — implement-now item #8).

**C2. Supercyan 351-clip combat library (arming/attack per weapon family)**
- **Verdict: A-WORK, effort M.** Humanoid clips retarget onto ANY Humanoid rig in the project — natural filler for troop attack beats (troops currently have no weapon visuals/attack animations). Needs clip picks + controller rows; the Motion Caster registry pattern extends here.
- **Player-felt value:** troops that visibly fight.
- **Caster-tool fit:** Motion Caster (shipped) — add Supercyan clips to its library scan.

**C3. Supercyan weapon prefabs + item-override system / accessory (wearable) tech**
- **Verdict: A-WORK, effort M each.** Visible troop weapons via `WithItemLogic` prefabs + `AnimatorOverridesApplier`, or just static hand-parented meshes (cheaper). The `AccessoryLogic` skinned-rebind pattern maps onto the Wardrobe/Dressable capability canon for cosmetic slots. Both are follow-ons to C1/C2.
- **Caster-tool fit:** Gear Caster (weapon preview) / Character Caster (accessories).

**C4. DragonWaveId stale constant**
- **Verdict: A-NOW (bug-class, S).** `TownsfolkDialogue.cs:106` `DragonWaveId = 4` vs the canonical 20-wave table (apexBoss on wave 20). One verification pass on `TierForWave` call sites, else NPC dread-dialogue escalates 16 waves early. Per §12: instrument/verify the call site before editing.
- **Player-felt value:** NPC dread builds at the right time.
- **Caster-tool fit:** direct wiring.

**C5. Dragon attack/death clips**
- **Verdict: parked.** The code-driven phase beats work as designed; `DragonAnimatorSetup` reserved Attack/Death states so a future bespoke clip drops in with zero code change. Only worth touching alongside the license resolution (⛔ compliance #1).

**C6. CGTrader NPC set reuse**
- **Verdict: A-NOW (policy).** Keep reusing the four bodies for new vendor/quest NPCs before commissioning anything (five injectors already demonstrate the pattern). No new work — a standing rule.

**C7. Hygiene (stale `PeopleCharacterImporter` map, empty Cathedral folder, orphaned Pet textures, FighterClass LODs)**
- **Verdict: parked to the polish-end purge** (canon: `asset-purge-deferred-to-polish-end`), except the one-line `TripoMaterialFixer.cs:7-8` comment fix (mis-attributes the dragon as Tripo, hiding the license issue) which should ride the compliance fix.

---

### 8. Audio (docs/SME/AUDIO_SME.md)

**A1. Raid BGM fix (~8 lines)**
- **Verdict: A-NOW (verified this session).** `MusicTrackRegistry.Defs` stops at Arena — no `Raid` entry (`MusicTrack.cs:139-159`, read directly); the enum value exists (`:49`) and `AudioBootstrap.cs:136` loads the clip that then falls through the `SetMusicClip` switch. Add the Defs entry + `_raidClip` field + `ClipFor`/`SetMusicClip` cases; fix the stale "wired" row in `docs/AUDIO/AUDIO_CLIP_MANIFEST.md:46` same-breath.
- **Player-felt value:** raids get their driving brass theme (shipped, paid for in build size, currently unreachable).
- **Caster-tool fit:** direct wiring.

**A2. Heal.wav drop-in**
- **Verdict: A-NOW.** Verified: no `Heal.wav` in `Assets/_Modules/Audio/Resources/Sfx/`; the knight `castHeal` row carries `"sfxId": "Heal"` (verified in the JSON). One file at the right name un-silences the owner's newest combat-feel pick. Candidate source: leohpaz Heals-and-Buffs or Hovl `Skill *.wav`. Naming trap documented: Motion Caster path wants `Heal`, the SfxId enum path wants `Sfx_Heal` — this row needs `Heal`.
- **Player-felt value:** the heal cast the owner just picked stops being silent.
- **Caster-tool fit:** **Audio Caster** is exactly this flow industrialized; the one file is a direct drop meanwhile.

**A3. Import the purchased leohpaz pack (48 clips)**
- **Verdict: A-NOW.** Purchased 2026-06-29, never downloaded (verified by the dossier against the Asset Store cache). Wiring = renaming files into `Assets/_Modules/Audio/Resources/Sfx/` per manifest names — zero code. Audition first (retro style vs our realistic Freesound set; owner taste call).
- **Player-felt value:** explosions, combos, level-up, tower fire, wave-start, build-denied stop sounding like synth test tones.
- **Caster-tool fit:** **Audio Caster** — the audition+map step is the tool's core loop; the coverage-audit table (§3 of the dossier) is its ready-made checklist.

**A4. Boot-warning cleanup / music-rotation pools**
- **Verdict: A-NOW (S).** Either source the 3+1 Suno pool variants or delete the five dead `TryAssignClip/TryAddClip` lines (`AudioBootstrap.cs:106,125-127,129`). Every boot currently logs 5 warns and WO-171 rotation is a no-op.
- **Player-felt value:** battle-music variety (if sourced) or a clean log (if trimmed).

**A5. Heart audio identity (all-silent category)**
- **Verdict: A-WORK, effort M — a sourcing task, not code.** Wiring already exists (WO-571 convention paths via `VillageAudioResources`); 3 ambient beds + 2 stingers + a voice line are missing. Suno for beds; TTS/recorded read for the VO. Propose: **new WO "Heart audio identity"** — the core defend-the-Heart loop has zero sound today.
- **Player-felt value:** the game's central object reacts audibly to being hurt.
- **Caster-tool fit:** Audio Caster maps them once sourced.

**A6. Hovl prefab AudioSources bypass the mixer**
- **Verdict: A-WORK, effort S.** Same item as Hovl card H7 — one editor pass assigning `outputAudioMixerGroup`, or strip-and-route through `PlaySfxAtPosition` (which also harvests the 12 pro WAVs as `Sfx_*` overrides, killing five synth tones).
- **Player-felt value:** VFX sounds obey the volume slider.

**A7. Housekeeping (orphans battle.mp3 / Victory.mp3 / bellssteel-panic.mp3; retire WaveMusicController; decide SfxClipLibrary vs drop-in convention; refresh AUDIO_CLIP_MANIFEST)**
- **Verdict: A-NOW (S) but low felt value — batch into the next audio WO.** Note bellssteel-panic is a natural StructureAttackAlert cue (owner decision flagged in the manifest).

**A8. Missing categories (ambient world beds, inventory foley, NPC voice, victory fanfare)**
- **Verdict: parked — acquisition/generation backlog**, revisit after A1–A6 land.

---

## NEXT-WO list (build after the implement-now batch, roughly ordered)

1. **Villager/crowd animation injector** (P1 — polyperfect 240 rigs + 5 clips + de-sync helpers; biggest felt "alive world" win; S/M).
2. **Character Caster + KayKit Mystery roster batch** (K1 + B2 + C1 leftovers — the tool and its first payload; owner roster-canon consult for the Blink orcs).
3. **Knight combat-feel completion WO** (S3 + S4 + S5 — 2-D strafe blend, defensive matrix, directional hit-reacts; the pack's authored fighter kit).
4. **Quaternius camp props** (P3 — replace the verified cube stub; small, contained).
5. **Heart audio identity** (A5 — sourcing WO; core-loop silence).
6. **Hovl polish tail** (H6 point-light, H7/A6 sound ownership — fold into the demo-parity lane).
7. **Audio Caster** (A2/A3 industrialized; SfxId + drop-in convention mapping with the coverage table as checklist).
8. **Gear Caster** (B8 + K5 — browse/seat/grip flow over the already-live Addressables chain; Offset Forge handoff).
9. **Obsidian second-pass prefab mirror** (B3).
10. **Supercyan troop combat visuals** (C2 + C3 — clips + weapon props).
11. **Prop-rotation bake tweak** (P2 — could also ride any village re-bake WO immediately).
12. **Texture/Environment Caster + Blink biome textures + KayKit/polyperfect dungeon dressing** (B4, K3, P4, P5 — all gated on WO-479/WO-584 direction).

## Parked / NOT-APPLICABLE

- **Mirza Beig Ultimate VFX** — deferred-purge candidate; scripts liftable on demand (V3).
- **Hovl 3D Lasers / distortion mats** — no consuming feature exists; spec-first if wanted (H8, H9).
- **Blink armor-as-hero / LowPoly hero base / UXML** — settled DO-NOT-REUSE (B7).
- **Spells Pack sidecar packages + demo-env mats** — purge-list fodder; never import (V2).
- **KayKit off-theme packs** (City Builder, Space Base, Restaurant, Platformer, Block, Board Game, Holiday) + duplicate Adventurers folder + polyperfect Landmarks/Scifi/etc. — purge-time.
- **Mystery Series 6 / Complete KayKit purchases** — owner spending decision (K8).
- **Blink free armor-set claims** — owner errand; claim links, don't import (B5).
- **Dragon bespoke clips** — with the license resolution only (C5).
- **Folder hygiene batch** (empty Cathedral, Pet husk, FighterClass LODs, stale importer maps) — polish-end purge, except the TripoMaterialFixer comment fix (C7).
- **Audio acquisition backlog** (ambient beds, foley, VO) — after the wired gaps close (A8).

---

## ⛔ Compliance items (ranked by risk, not value)

### 1. Black Dragon license — SHIP BLOCKER for commercial release
The apex wave-20 boss ("Syndrath the Devourer", `DragonBoss.cs`, spawned by the canonical
`waves.json` terminal wave) is Dennis Haupt / 3DHaupt's free dragon. Every free distribution is
non-commercial: Sketchfab = CC Attribution-NonCommercial (confirmed via the Sketchfab API record),
Free3D = personal/editorial, CGTrader free tier = editorial. The Pi hackathon target (July 31) is a
monetized release path. **Owner decision required before ship:** buy the paid CGTrader commercial
license, or replace the model (the code is ready for either — the animator states and phase machine
are model-agnostic; a KayKit/store dragon could drop in). Side task riding this: fix the
`TripoMaterialFixer.cs:7-8` comment that mis-attributes the dragon as Tripo-generated and hides the
provenance. Risk if ignored: shipping a commercially-unlicensed centerpiece boss.

### 2. Audio provenance — pre-launch re-verify required
`Assets/Audio/SFX/Combat/SOURCE_LICENSE.md` logs three Freesound IDs, and all three were verified
this cycle to resolve to **unrelated sounds** (a synth brass note, a statue falling in snow, a
synthesizer sequence — one of them CC BY-NC). The 17-WAV recorded combat set's true sources and
licenses are therefore unknown; the remaining 14 files have no IDs logged at all. Actions:
re-locate the true sources and record real licenses (CC-BY needs a credits line; CC BY-NC is
unusable commercially); confirm the owner's Suno account tier grants commercial rights for the 15
BGM tracks; keep the leohpaz set (Asset Store EULA — clean) as the licensed fallback for any clip
that can't be cleared. Risk if ignored: unlicensed audio in a monetized build; mitigation is cheap
because every gap has a clean-licensed substitute path.

---

## Verification notes — dossier claims spot-checked this session

**Confirmed against code (sample):** DefaultVolumeProfile Bloom intensity 0 (read from the asset);
`ApplyStartColor` flat-fill (`VFXManager.Hovl.cs:344-351`); `ReturnHovlAfterDelay` exists unused by
`Stop()`; `MusicTrackRegistry.Defs` has no Raid entry while `AudioBootstrap.cs:136` assigns the
clip; `ConceptIconResolver` + concept-icons.json seam as described; `EquipmentController` native
flag + Blink `knight_starter` (`:101`, `:197`); `SupercyanResourceWire` map (`:34`); `ClaimableCamp`
cube stub (`:135-153`); `Resources/Sfx/Heal` missing while the castHeal row names it; 40+ mirrored
Blink prefabs on disk; knight skill1/skill2 rows = kung-fu kicks (matches the dossier's
current-usage claim).

**Two dossier claims FAILED or aged out:**
1. **SWORD_SHIELD_MOCAP_SME.md gap #2 ("shield-swipe chain plays beat 1 only — no second-beat state
   wired") is STALE.** `KnightPackageControllerBuilder.cs:415-437` builds a `Block2` state chaining
   `ShieldSwipe01 → ShieldSwipe02`, and the JSON's owner-picks comment relies on it. The dossier
   audited the older `HeroAnimatorFactory.cs` controller; the live knight rows point at the
   KnightPackage extracted clips. The dossier should be banner-flagged on that line (§15).
2. **Data inconsistency inside `motion-castings.json` itself:** the knight target's `_comment`
   records OWNER PICKS 2026-07-11 (`skill1 = atk_jump` Heroic Leap, `skill2 = plain Slash`), but the
   live `skill1`/`skill2` rows still point at `fist_flyingkick_m.fbx` / `fist_whirlwindkick_m.fbx`
   (both `manual: true`). Either the picks were never applied to the rows or were reverted without
   updating the comment. Implement-now item #6 resolves it — but confirm with the owner which state
   is canon before editing (`confirm-before-reverting-canon` rule).
