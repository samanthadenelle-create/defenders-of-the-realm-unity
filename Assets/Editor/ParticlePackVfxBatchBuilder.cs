// =============================================================================
// ParticlePackVfxBatchBuilder - SCRIPT-authors the ASSET half of the 16 VFXType
// values Grok appended on 2026-08-05 (the registry / handbook batch that sits
// after Boss_FireBreath in VFXType.cs). Until this runs, every one of those names
// resolves to NOTHING in VFXCatalog and VFXManager falls through to the
// procedural AbilityVfxKit placeholder - the effect "works" and looks generic.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor   (editor-only)
//
// WHY A BUILDER AND NOT HAND-AUTHORED YAML:
//   Every step below touches a .prefab / .asset. Hand-editing that YAML is banned
//   (CLAUDE.md sections 0 + 3 - mount garble + resave corruption history), so the work
//   goes through AssetDatabase / PrefabUtility / SerializedObject and UNITY owns the
//   serialization. Nothing here writes a byte of YAML itself.
//
// IT IS THE BossFireBreathBuilder PATTERN, GENERALISED TO A TABLE. That builder is
// the one in this repo that has been run and gated (commit 7f3971a3); its five
// disciplines are carried verbatim rather than re-derived:
//
//   1. A pack prefab is a MULTI-LAYER RECIPE. CopyAsset the WHOLE tree into
//      Assets/Resources/VFX/<area>/ - never flatten, never reference the pack in
//      place. Assets/UnityTechnologies/ is GITIGNORED (.gitignore:399), and 117 of
//      121 catalog rows already point into gitignored art with no runtime fallback
//      (WO-785). The copies this builder makes are tracked; they must not join them.
//      The duplicate's descendant + ParticleSystem counts are VERIFIED against the
//      source (plus the optional second layer) and any shortfall hard-fails the row.
//
//   2. PROVE the emission family from the REAL ASSET, never from the doc comment.
//      rateOverTime / rateOverDistance / burst count / main.loop are read off every
//      layer of the duplicate and logged per layer. A row whose enum doc claims one
//      family and whose art reads as the opposite is REFUSED (no catalog row, and
//      the whole run withholds its success marker) rather than silently catalogued
//      with the wrong flag. Rows whose doc explicitly permits either family (see
//      Env_SteamBurst, "Family B Impact or short A") declare Family.Either and are
//      recorded, not judged.
//
//   3. playOnAwake is CLEARED on every ParticleSystem in every copy. Every one of
//      these pack recipes ships with playOnAwake:1; a prewarmed pool instance would
//      otherwise emit a stray effect at the world origin the moment it is created.
//      Nothing is lost - VFXManager Clear()s and Play()s the whole tree explicitly.
//
//   4. Quality tiers DISABLE children at runtime; this builder never deletes a layer.
//
//   5. THE ISLOOP FLAG IS NOT DERIVED HERE. It comes from
//      DeNelle.Editor.Regression.VfxLoopFlagRegression.TryResolveExpected - the
//      single home of the rule, which also honours standing owner rulings.
//      DeNelle.Editor already references DeNelle.EditorRegression. A second
//      derivation is precisely the divergence that caused the P0 fixed in bd532d5b:
//      one surface believed a checkbox, another believed the art.
//
// THE OTHER TRAP THIS BUILDER ALONE CANNOT CLOSE:
//   VFXCatalogGenerator.Build() does 'entries.arraySize = rows.Count' - it rebuilds
//   Entries[] wholesale from its curated Map. A row written ONLY here is silently
//   deleted the next time anyone regenerates the catalog, and the effect falls back
//   to a procedural loop that still LOOKS like it works. So every row in the table
//   below ALSO has a matching Map entry in Assets/Editor/VFXCatalogGenerator.cs.
//   That generator derives IsLoop from the prefab through the same shared oracle,
//   so the two surfaces cannot disagree.
//
// IDEMPOTENT - safe to run twice:
//   * A duplicate is copied only when absent; an existing one is REUSED, so its GUID
//     - and therefore every catalog reference to it - survives.
//   * Gameplay TUNING (scale / emission density / gravity / speed / lifetime) is
//     applied ONLY to a freshly copied prefab. A reused prefab is treated as
//     owner-tuned and is reported as preserved, never stomped. To re-derive a
//     recipe from scratch, delete its .prefab under Assets/Resources/VFX/ and re-run.
//   * playOnAwake is re-cleared on every run (it is a correctness invariant, not a
//     taste call) and only marks the prefab dirty when something actually changed.
//   * Catalog rows are looked up by VFXType and UPDATED in place; only a missing row
//     grows the array. No duplicate rows, ever.
//
// WHY REFLECTION FOR THE ENUM:
//   The 16 values have landed, so naming them in C# would compile today. Resolving
//   them BY NAME anyway means a later rename or removal degrades to one skipped row
//   with a clear warning, instead of fail-compiling the whole DeNelle.Editor
//   assembly and taking the compile gate down for every parallel lane. Both existing
//   VFX builders do the same; the consistency is deliberate.
//
// WO-886 (2026-08-05) ADDED THE DEATH LADDER to this same table rather than minting a
// third builder. Three capabilities came with it, all of them general:
//   * Row.RequiredSystems - a row may DECLARE its recipe's layer count and hard-fail if
//     the source disagrees. WO-886 requires BigExplosion pooled WHOLE (8 layers); a
//     trimmed recipe still renders a plausible explosion, so it must be asserted.
//   * Row.Aliases - extra VFXType names whose catalog row points at the SAME prefab, so
//     the legacy Death_Boss alias physically cannot drift from Boss_Death.
//   * Row.BurstOnce - clears main.loop + main.prewarm on a one-shot recipe. Every
//     explosion in this pack ships looping:1 with its payload in a t=0 burst, and
//     VFXManager reclaims a pooled oneshot at duration + max startLifetime, so the burst
//     RE-FIRES mid-life. Measured on BigExplosion: duration 2 s, max startLifetime 2 s
//     -> reclaim ~4.3 s -> the boss detonates TWICE. Felt-visible, hence opt-in per row.
//
// WO-887 (2026-08-05) ADDED NO ROWS, and that is the finding, not a gap. Its whole
// surface ladder (Flesh/Metal/Stone/Wood/SandImpacts) was MEASURED and REFUSED: those
// five pack recipes are demo TARGETS - the prefab root is a mesh + renderer + collider
// with the particle tree hanging underneath - and the child that PickAuthority lands on
// emits 5/sec on loop, so the art derives CONTINUOUS under a contract that requires
// IsLoop=false. There is also no Impact_Flesh/_Metal/_Stone/_Wood/_Dirt enum value and,
// verified at source, no surface signal anywhere in the game to choose between them.
// The refusals are recorded in DeferredTypes with their numbers; WO-887's deliverable
// turned out to be CALL SITES (the ranged-release Cast_MuzzleFlash, which this builder
// already ships, and element-correct tower impacts), not new art.
//
// DELIBERATELY NOT DONE (reported, never faked):
//   * Enemy_Spawn / Despawn_Dissolve. Their recipes (Misc/Respawn, Misc/Dissolve)
//     are SCRIPTED effects: each carries a MonoBehaviour from the pack's own
//     Misc Effects/Scripts/SpawnEffect.cs plus a demo MESH for the script to
//     dissolve. Copying them would produce a prefab that (a) renders a demo mesh
//     wherever it plays and (b) carries a missing-script reference on any machine
//     without the gitignored pack. Those two moments need a real runtime component
//     driving the TARGET's own material cutoff - authoring work, not a CopyAsset.
//     They are left out of the table and named in the run report.
//
// RUN:
//   Editor menu : Defenders/VFX/Build Particle Pack VFX Batch
//   Batchmode   : DeNelle.Editor.ParticlePackVfxBatchBuilder.Build
//   Markers     : PARTICLE_PACK_VFX_BUILD_OK / PARTICLE_PACK_VFX_BUILD_FAIL
//                 (distinct to this entry point - a marker shared with another
//                  entry point cannot say WHICH step passed, which is the
//                  2026-08-02 gate defect.)
//
// DOES NOT TOUCH: VFXType.cs (append is Grok's single-owner edit, WO-884 section 0.2),
// VFXManager, the Vfx facade, VfxElementTables, any scene, or the Particle Pack
// itself (SOURCE RECIPE ONLY - never reimported, duplicated in place, or modified).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor builder for the WO-884 registry batch: mirrors the chosen Particle Pack
    /// recipes into committed Resources/VFX prefabs and wires their VFXCatalog rows.
    /// Idempotent; prints PARTICLE_PACK_VFX_BUILD_OK on success.
    /// </summary>
    public static class ParticlePackVfxBatchBuilder
    {
        // -- Markers (distinct per entry point) --------------------------------
        private const string MarkerOk   = "PARTICLE_PACK_VFX_BUILD_OK";
        private const string MarkerFail = "PARTICLE_PACK_VFX_BUILD_FAIL";
        private const string Tag        = "[ParticlePackVfxBatchBuilder] ";

        // -- Paths --------------------------------------------------------------
        // NOTE the spaces AND the ampersands in the pack folder names - both are
        // legal in an AssetDatabase path and must survive verbatim.
        private const string PackRoot =
            "Assets/UnityTechnologies/ParticlePack/EffectExamples/";

        private const string Fire   = PackRoot + "Fire & Explosion Effects/Prefabs/";
        private const string Smoke  = PackRoot + "Smoke & Steam Effects/Prefabs/";
        private const string Weapon = PackRoot + "Weapon Effects/Prefabs/";
        private const string Misc   = PackRoot + "Misc Effects/Prefabs/";

        private const string DestEnv     = "Assets/Resources/VFX/Env/";
        private const string DestWeapon  = "Assets/Resources/VFX/Weapon/";
        private const string DestAura    = "Assets/Resources/VFX/Aura/";
        private const string DestHarvest = "Assets/Resources/VFX/Harvest/";
        private const string DestDeath   = "Assets/Resources/VFX/Death/";   // WO-886 death ladder

        private const string CatalogPath = "Assets/Resources/VFX/VFXCatalog.asset";

        // -- Type names resolved at run time (see header) -----------------------
        private const string CatalogTypeName = "DeNelle.Village.VFXCatalog, DeNelle.Village";
        private const string VfxTypeEnumName = "DeNelle.Village.VFXType, DeNelle.Village";

        // Name given to an optional second recipe layer merged into a destination.
        private const string SecondaryPrefix = "Layer_";

        // A scaled-down emission rate must never reach zero - a silent 0/sec emitter
        // is an invisible effect that still books a pool slot.
        private const float MinScaledRate = 0.05f;

        // =====================================================================
        //  The table
        // =====================================================================

        /// <summary>What the enum doc comment claims this recipe's emission family is.</summary>
        private enum Family
        {
            /// <summary>Doc says continuous / IsLoop=true. Art must agree.</summary>
            Continuous,
            /// <summary>Doc says burst / IsLoop=false. Art must agree.</summary>
            Burst,
            /// <summary>Doc explicitly permits either - measure and record, do not judge.</summary>
            Either
        }

        private struct Row
        {
            public string  TypeName;      // VFXType member name, resolved by name
            public string  Source;        // pack recipe (gitignored source)
            public string  Secondary;     // optional 2nd recipe merged in as a child layer
            public string  Dest;          // committed destination .prefab
            public Family  Expect;        // what the enum doc claims
            public int     MinQuality;    // 0 always, 1 skip-Low, 2 High-only
            public int     PoolSize;
            public bool    Required;      // a missing source on a required row fails the run
            public float   Scale;         // default root scale for a FRESH copy
            public float   RateMul;       // emission density multiplier (1 = untouched)
            public float   SpeedMul;      // startSpeed multiplier      (1 = untouched)
            public float   LifeMul;       // startLifetime multiplier   (1 = untouched)
            public float   Gravity;       // absolute gravityModifier override; NaN = untouched
            public string  Why;           // the registry line this row implements

            // -- WO-886 additions -------------------------------------------------
            /// <summary>
            /// When &gt; 0, the SOURCE recipe must carry EXACTLY this many ParticleSystems
            /// or the row hard-fails. WO-886 requires BigExplosion pooled WHOLE (8 layers);
            /// a pack reimport that silently trimmed a layer would otherwise ship a boss
            /// death missing its debris or its smoke column and nothing would say so.
            /// 0 = no declared count (the dest-vs-source LAYER LOSS guard still applies).
            /// </summary>
            public int     RequiredSystems;

            /// <summary>
            /// Extra VFXType member names whose catalog row points at the SAME dest prefab.
            /// WO-886 names this explicitly: Death_Boss (legacy alias) and Boss_Death must
            /// both be BigExplosion. Sharing ONE prefab is what makes the alias unable to
            /// drift - two copies would be two things to re-tune and forget.
            /// </summary>
            public string[] Aliases;

            /// <summary>
            /// Clear main.loop (and main.prewarm, which Unity only permits on a looping
            /// system) on every layer of a FRESH copy. A death is fire-and-reclaim, but
            /// every explosion recipe in this pack ships looping:1 with duration 2 s and its
            /// whole payload in a t=0 burst - MEASURED on BigExplosion: 8 layers, duration
            /// 2 s, max startLifetime 2 s, so VFXManager.DetectDuration reclaims the pooled
            /// instance at ~4.3 s and the burst RE-FIRES at t=2. A boss death would detonate
            /// twice. This is a correctness invariant for a one-shot, exactly like
            /// playOnAwake, not a taste call - but it IS felt-visible, so it is opt-in per
            /// row and reported rather than applied to every recipe the builder owns.
            /// </summary>
            public bool    BurstOnce;
        }

        /// <summary>"leave this knob alone" sentinel for Row.Gravity (0 is a real value).</summary>
        private const float None = float.NaN;

        // Sources: docs/vfx/VFX_CREATIVE_PICKS_REGISTRY.md sections 6b/6c/6e/7 +
        // docs/vfx/VFX_PREFAB_HANDBOOK.md sections 5.1-5.5. Every tuning value below
        // implements a MOTION phrase written in the registry - motion, not colour, is
        // what makes these read for a red/green-colourblind owner, so an untuned copy
        // of one shared source under two names would defeat the whole point.
        private static readonly Row[] Rows = new[]
        {
            // -- Environment (P1 dungeon dress) --------------------------------
            // TinyFlames, NOT Misc/Candles, for the candle. Measured: Candles.prefab
            // is 6 GameObjects / 3 ParticleSystems / 3 MeshRenderers - it carries the
            // candle GEOMETRY. Env_Candle is a "prop candle FLAME loop" attached to a
            // prop that already has its own mesh, so shipping Candles would render a
            // second set of candles wherever the effect plays. The enum doc names
            // "Candles or TinyFlames" and the handbook (5.1) lists TinyFlames as the
            // candle alt; TinyFlames is 1 pure-particle layer. Scaled down for a wick.
            new Row { TypeName = "Env_Candle",   Source = Fire  + "TinyFlames.prefab",
                      Dest = DestEnv + "Env_Candle.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 6, Required = true,
                      Scale = 0.45f, RateMul = 1f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      Why = "registry: dungeon/prop candle flame loop; handbook 5.1 candle alt" },

            new Row { TypeName = "Env_SteamVent", Source = Smoke + "RisingSteam.prefab",
                      Dest = DestEnv + "Env_SteamVent.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 4, Required = true,
                      Scale = 1f, RateMul = 1f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      Why = "registry 6/handbook 5.4: geothermal vent rising steam loop" },

            // Family.Either on purpose: the enum doc itself says "Family B Impact or
            // short A". Measured, PressurisedSteam is a 2-layer rate-20/rate-15 looping
            // jet - so it lands as CONTINUOUS. Recorded, not judged.
            new Row { TypeName = "Env_SteamBurst", Source = Smoke + "PressurisedSteam.prefab",
                      Dest = DestEnv + "Env_SteamBurst.prefab", Expect = Family.Either,
                      MinQuality = 1, PoolSize = 4, Required = true,
                      Scale = 1f, RateMul = 1f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      Why = "registry 6/handbook 5.4: pressurised steam jet (trap/pipe)" },

            // -- Combat release -------------------------------------------------
            // MinQuality 0: a release flash is the player's confirmation that a shot
            // left the barrel. It is combat legibility, not dressing, so it plays on
            // every tier. Handbook section 10 also calls out MuzzleFlash as the canonical
            // "must be IsLoop=false" row.
            new Row { TypeName = "Cast_MuzzleFlash", Source = Weapon + "MuzzleFlash.prefab",
                      Dest = DestWeapon + "Cast_MuzzleFlash.prefab", Expect = Family.Burst,
                      MinQuality = 0, PoolSize = 8, Required = true,
                      Scale = 1f, RateMul = 1f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      Why = "registry 1 beat 4 / handbook 5.2: barrel muzzle release burst" },

            // -- HP-state auras (registry 6b) -----------------------------------
            // MinQuality 0 for BOTH HP tells, deliberately against the "ambient = 1"
            // default. Registry section 8 item 7 rules that these world-space auras become the
            // PRIMARY low-HP read because today's tell is a RED edge vignette that the
            // owner (red/green colourblind) cannot see. A primary survival read that
            // vanishes on a Low-quality device is the same bug in a new place.
            new Row { TypeName = "Aura_LowHealth", Source = Smoke + "SmokeEffect.prefab",
                      Dest = DestAura + "Aura_LowHealth.prefab", Expect = Family.Continuous,
                      MinQuality = 0, PoolSize = 2, Required = false,
                      Scale = 0.7f, RateMul = 1f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      Why = "registry 6b: guttering wisps under 0.30 HP" },

            // "TinyFlames FAST gutter (candle-about-to-die)" - denser + smaller than the
            // candle it shares a source with, which is exactly the near-panic cadence
            // and shrinking flame the registry asks for.
            new Row { TypeName = "Aura_NearDeath", Source = Fire + "TinyFlames.prefab",
                      Dest = DestAura + "Aura_NearDeath.prefab", Expect = Family.Continuous,
                      MinQuality = 0, PoolSize = 2, Required = false,
                      Scale = 0.55f, RateMul = 1.8f, SpeedMul = 1f, LifeMul = 0.7f, Gravity = None,
                      Why = "registry 6b: fast gutter sub-tier under 0.25 HP" },

            new Row { TypeName = "Aura_HealingInProgress", Source = Smoke + "RisingSteam.prefab",
                      Dest = DestAura + "Aura_HealingInProgress.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 2, Required = false,
                      Scale = 0.8f, RateMul = 1f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      Why = "registry 6b: calm upward regen column, opposite motion to the gutter" },

            // "RisingSteam LOW held" - the item seat is a body-held aura, so it is
            // smaller and sparser than the cast-driven heal column above it.
            new Row { TypeName = "Aura_ItemHeal", Source = Smoke + "RisingSteam.prefab",
                      Dest = DestAura + "Aura_ItemHeal.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 2, Required = false,
                      Scale = 0.5f, RateMul = 0.6f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      Why = "registry 6c: GearAura heal seat, reuses heal rising-language" },

            // -- Harvest / economy auras (registry 6e) ---------------------------
            // The five resources are split by MOTION VECTOR, never by hue. DustMotes
            // ships at 100/sec (a room-fill ambience); a per-node aura wants a third
            // of that, hence the shared 0.35 density on the two dust rows.
            //
            // Iron alone merges a SECOND recipe - SparksEffect - as a child layer, which
            // is the registry's literal "heavy dust settling + metal spark glint". The
            // positive gravityModifier is the "settling" half.
            new Row { TypeName = "Harvest_Iron", Source = Misc + "DustMotesEffect.prefab",
                      Secondary = Misc + "SparksEffect.prefab",
                      Dest = DestHarvest + "Harvest_Iron.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 3, Required = false,
                      Scale = 1f, RateMul = 0.35f, SpeedMul = 1f, LifeMul = 1f, Gravity = 0.15f,
                      Why = "registry 6e: heavy dust SETTLING + metal spark glint" },

            // Wood is the same dust at neutral gravity: "flat SIDEWAYS-drifting chip
            // motes". Gravity 0 is the flat half; no spark layer keeps it apart from Iron.
            new Row { TypeName = "Harvest_Wood", Source = Misc + "DustMotesEffect.prefab",
                      Dest = DestHarvest + "Harvest_Wood.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 3, Required = false,
                      Scale = 1f, RateMul = 0.35f, SpeedMul = 1f, LifeMul = 1f, Gravity = 0f,
                      Why = "registry 6e: flat sideways-drifting chip motes" },

            // "FireFlies (SPARSE) - light motes rising slowly (pollen)".
            new Row { TypeName = "Harvest_Food", Source = Misc + "FireFlies.prefab",
                      Dest = DestHarvest + "Harvest_Food.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 3, Required = false,
                      Scale = 1f, RateMul = 0.5f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      Why = "registry 6e: sparse rising pollen motes" },

            // "FireFlies (DENSE shimmer) - suspended twinkling, NO TRAVEL". The near-zero
            // startSpeed is the "no travel" half and is what motion-splits Crystal from
            // Food and Gold, all three of which are sparkles.
            new Row { TypeName = "Harvest_Crystal", Source = Misc + "FireFlies.prefab",
                      Dest = DestHarvest + "Harvest_Crystal.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 3, Required = false,
                      Scale = 1f, RateMul = 1.6f, SpeedMul = 0.15f, LifeMul = 1f, Gravity = 0f,
                      Why = "registry 6e: dense suspended twinkle, no travel" },

            // "SparksEffect (bright, SHORT) - glint pops that FALL (coin-shimmer)".
            new Row { TypeName = "Harvest_Gold", Source = Misc + "SparksEffect.prefab",
                      Dest = DestHarvest + "Harvest_Gold.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 3, Required = false,
                      Scale = 1f, RateMul = 0.5f, SpeedMul = 1f, LifeMul = 0.6f, Gravity = 0.4f,
                      Why = "registry 6e: short falling glint pops, motion-split vs Crystal" },

            // "FireFlies rising bob, LOW emission - rising = come pick me up".
            new Row { TypeName = "Collector_Ready", Source = Misc + "FireFlies.prefab",
                      Dest = DestHarvest + "Collector_Ready.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 4, Required = false,
                      Scale = 1f, RateMul = 0.6f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      Why = "registry 6e: ready-to-collect beacon, rising bob" },

            // == WO-886 DEATH LADDER (registry section 5) ==========================
            // The ladder escalates by RECIPE + LAYER COUNT + SCALE, in that order of
            // importance - all three survive greyscale, which hue does not (owner is
            // red/green colourblind). Measured layer counts, off the real assets:
            //   SmallExplosion 4 | DustExplosion 5 | EnergyExplosion 4 | BigExplosion 8
            // so a trash pop and a boss set-piece differ in the number of things
            // happening on screen, not only in how big they are.
            //
            // EVERY row here is Family.Burst and every one MEASURES as a burst at the
            // root: rateOverTime 0, rateOverDistance 0, one burst at t=0 (counts 5 / 30 /
            // 3 / 3). No death may be catalogued as a loop - a fire-and-forget loop
            // permanently burns one of the 20 global loop slots, and a wave produces
            // deaths by the dozen.
            //
            // MinQuality 0 across the board, deliberately: the death burst is how the
            // player knows the thing they hit is GONE. A kill confirmation that vanishes
            // on a Low-tier device is a combat-legibility bug, not saved dressing.
            //
            // NO gravity / speed overrides on this ladder. The recipes already carry the
            // motion language the registry asks for (DustExplosion = grounded sand and
            // debris, EnergyExplosion = symmetric radial energy, SmallExplosion = fire and
            // rising embers, BigExplosion = debris + a smoke-trail column), and this
            // builder's tuning pass applies a knob to EVERY layer including the flat
            // shockwave ring - which a gravity override would visibly droop. Motion here
            // is the recipe's; scale is ours.

            // Trash floor. Also the target of VfxPool.SpawnDeathBurst, which is what a
            // kill falls back to when the enemy carries no species data at all, so this
            // is the single highest-traffic death in the game -> the largest pool.
            new Row { TypeName = "Death_Generic", Source = Fire + "SmallExplosion.prefab",
                      Dest = DestDeath + "Death_Generic.prefab", Expect = Family.Burst,
                      MinQuality = 0, PoolSize = 8, Required = true,
                      Scale = 1f, RateMul = 1f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      RequiredSystems = 4, BurstOnce = true,
                      Why = "registry 5: Death_Generic = SmallExplosion (trash pop, ladder floor)" },

            // "SmallExplosion (ember)" - same recipe as the floor, held LONGER so the
            // embers hang and rise instead of snapping out. That lifetime difference is
            // the whole read ("tiefling = rising ember"); an untuned second copy of the
            // floor recipe under a second name would be indistinguishable and pointless.
            // NOT mapped to any roster enemy - see the run report.
            new Row { TypeName = "Death_Tiefling", Source = Fire + "SmallExplosion.prefab",
                      Dest = DestDeath + "Death_Tiefling.prefab", Expect = Family.Burst,
                      MinQuality = 0, PoolSize = 4, Required = true,
                      Scale = 1.05f, RateMul = 1f, SpeedMul = 1f, LifeMul = 1.25f, Gravity = None,
                      RequiredSystems = 4, BurstOnce = true,
                      Why = "registry 5: Death_Tiefling = SmallExplosion (ember), lingering rise" },

            // Golem / heavy. 5 layers incl. a 30-count sand burst - the "grounded dust"
            // read, one rung above the floor by both layer count and scale.
            new Row { TypeName = "Death_Brute", Source = Fire + "DustExplosion.prefab",
                      Dest = DestDeath + "Death_Brute.prefab", Expect = Family.Burst,
                      MinQuality = 0, PoolSize = 6, Required = true,
                      Scale = 1.15f, RateMul = 1f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      RequiredSystems = 5, BurstOnce = true,
                      Why = "registry 5: Death_Brute (golem) = DustExplosion, grounded dust" },

            // Dungeon runs read darker/bigger than the village floor without needing a
            // different recipe family from the elite rung above it.
            new Row { TypeName = "Death_EnemyExplosion_Dungeon", Source = Fire + "EnergyExplosion.prefab",
                      Dest = DestDeath + "Death_EnemyExplosion_Dungeon.prefab", Expect = Family.Burst,
                      MinQuality = 0, PoolSize = 6, Required = true,
                      Scale = 1.2f, RateMul = 1f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      RequiredSystems = 4, BurstOnce = true,
                      Why = "registry 5: Death_EnemyExplosion_Dungeon = EnergyExplosion" },

            // "EnergyExplosion (full)" - the same recipe as the dungeon rung, scaled up
            // and held longer so an elite kill reads as a bigger version of the same idea
            // rather than a different element. Elites are a TIER, not a species.
            new Row { TypeName = "Elite_Death", Source = Fire + "EnergyExplosion.prefab",
                      Dest = DestDeath + "Elite_Death.prefab", Expect = Family.Burst,
                      MinQuality = 0, PoolSize = 4, Required = true,
                      Scale = 1.45f, RateMul = 1f, SpeedMul = 1f, LifeMul = 1.15f, Gravity = None,
                      RequiredSystems = 4, BurstOnce = true,
                      Why = "registry 5: Elite_Death = EnergyExplosion (full)" },

            // THE SET PIECE. 8 layers pooled WHOLE (BigExplosion, Embers, SmokeTrail,
            // Embers (1), Light, Debris, AdditonalSmoke, Shockwave) - RequiredSystems
            // hard-fails the row if the recipe ever arrives with fewer, because a boss
            // death quietly missing its debris or its smoke column still LOOKS like an
            // explosion and nobody would catch it.
            //
            // Death_Boss is the LEGACY ALIAS and shares this one prefab, so the two can
            // never drift apart (WO-886 calls this out by name). One asset, two catalog
            // rows, one thing to tune.
            new Row { TypeName = "Boss_Death", Source = Fire + "BigExplosion.prefab",
                      Dest = DestDeath + "Boss_Death.prefab", Expect = Family.Burst,
                      MinQuality = 0, PoolSize = 2, Required = true,
                      Scale = 1.8f, RateMul = 1f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      RequiredSystems = 8, BurstOnce = true, Aliases = new[] { "Death_Boss" },
                      Why = "registry 5: Boss_Death AND Death_Boss = BigExplosion (8-layer, whole)" },
        };

        // The moments this builder deliberately leaves alone, each with the reason the run
        // report repeats verbatim so nobody re-attempts one as a CopyAsset. Every WO-886
        // entry cites the MEASUREMENT that forced the deferral, not an opinion about it.
        private static readonly string[] DeferredTypes =
        {
            "Enemy_Spawn: Misc/Respawn is a SCRIPTED recipe (pack MonoBehaviour " +
            "SpawnEffect.cs + a demo mesh). A copy would carry a missing script on any " +
            "clone without the gitignored pack and would render a demo mesh. Needs a " +
            "runtime component driving the TARGET's material cutoff - authoring, not a copy.",

            "Despawn_Dissolve: Misc/Dissolve, same shape (pack script + 2 demo meshes). " +
            "Same reason, same remedy.",

            // -- WO-886 deferrals. Every one is a MEASUREMENT, not an opinion. ------
            "Death_Skeleton (WO-886): the ratified recipe is SparksEffect + a SmokeEffect " +
            "wisp. MEASURED off the real assets, BOTH are CONTINUOUS, not bursts. " +
            "SparksEffect's root emits rateOverTime 80/sec on loop (its other layers 10 and " +
            "10; the ONLY burst in the recipe is a 0.2 s 'SparkDeathEffect' child that is not " +
            "the derivation authority), and SmokeEffect is a single layer at 20/sec on loop. " +
            "WO-886 requires every death to be BURST, and the shared oracle derives IsLoop " +
            "from the ROOT - so cataloguing this would either hand a rate-emitting loop to a " +
            "fire-and-forget death (one of the 20 global loop slots gone per kill) or force " +
            "IsLoop=false onto a still-emitting system and reclaim it mid-emit. Refused, not " +
            "faked. Death_Skeleton KEEPS its existing committed Lana Burst/Poof_generic row, " +
            "which is genuinely burst-shaped and already tracked, so nothing regresses - the " +
            "hollow trash pop stays the smallest rung of the ladder. A human must either " +
            "re-pick a burst recipe for the bone scatter or rule that the sparks may be " +
            "re-authored into a one-shot.",

            "Death_Wolf (WO-886): same measurement, same refusal - SparksEffect (crystal) + a " +
            "slow Steam drift are both continuous. Keeps its committed Lana Burst/Poof_water " +
            "row. Note also that NO roster enemy is a wolf (enemies.json families are hollow / " +
            "orc / troll; Ice Wolf is a PET), so nothing plays this type today regardless.",

            // -- WO-887 deferrals. Every one is a MEASUREMENT or a missing enum value, not an
            // opinion, and each is spelled out so nobody re-attempts it as a CopyAsset.
            "WO-887 SURFACE IMPACTS (FleshImpacts / MetalImpacts / StoneImpacts / WoodImpacts / " +
            "SandImpacts): REFUSED on three independent grounds, any one of which is disqualifying. " +
            "(1) DEMO GEOMETRY. All five recipes are shaped as a demo TARGET, not as an effect: the " +
            "prefab ROOT carries a MeshFilter (built-in mesh fileID 10207), a MeshRenderer with a " +
            "pack material and a SphereCollider, sits at the demo-scene position, and parents ONE " +
            "child that holds the actual particle tree. A CopyAsset would render a lit primitive AND " +
            "add a physics collider at every hit point. MuzzleFlash - the one Weapon-folder recipe " +
            "this builder DOES ship - carries none of those three components, which is exactly why it " +
            "was safe. Same shape as the Enemy_Spawn / Despawn_Dissolve demo-mesh refusal above. " +
            "(2) THE ART MEASURES CONTINUOUS AT THE DERIVATION AUTHORITY. The root holds no " +
            "ParticleSystem, so VfxLoopFlagRegression.PickAuthority falls through to the first " +
            "can-emit system in hierarchy order, and in ALL FIVE that system is the child named " +
            "'HitEffect': main.loop TRUE with rateOverTime 5/sec CONSTANT (emission enabled, " +
            "GameObject active) -> derivedIsLoop TRUE. WO-887 requires every impact to be " +
            "IsLoop=false. The burst layers are real but they are NOT the authority: Flesh = " +
            "Streaks 5 + Mist 10 + Decal 1 at t=0; Metal = Dust 30 + Sparks 20 + Decal 1; Wood = " +
            "Dust 30 + Decal 1 plus a 300/sec curve-spiked WoodSplinters; Stone = Decal 1 plus " +
            "500/sec ImpactDebris and 1000/sec Dust; Sand = Decal 1 plus two 1000/sec dust layers. " +
            "BurstOnce would technically clear main.loop and force the derivation to false, but on " +
            "HitEffect that leaves a 5/sec emitter trickling for its full 5 s duration - a finite " +
            "stream, not a hit - which is the same 'force a burst flag onto a live emitter' move " +
            "refused for Death_Skeleton. (3) NO VFXType AND NO SURFACE SIGNAL. There is no " +
            "Impact_Flesh / _Metal / _Stone / _Wood / _Dirt value in the enum and appending is " +
            "Grok's single-owner edit (WO-884 section 0.2), so these would be Resources bytes with " +
            "no consumer - and nothing could choose between them anyway (see the surface-signal note " +
            "below). A human must re-pick recipes whose root IS the effect, or rule that the child " +
            "sub-tree may be extracted from the demo target.",

            "WO-887 SURFACE SIGNAL IS ABSENT (reported, never invented): the WO assumes flesh / " +
            "metal / stone / wood / dirt detection exists at the hit sites. VERIFIED AT SOURCE - it " +
            "does not. No SurfaceType/MaterialType/HitSurface enum or field anywhere; no " +
            "Collider.sharedMaterial read (the repo's ONE .physicMaterial is a LeanTouch demo asset " +
            "referenced by zero prefabs and zero scenes); TagManager holds only role tags (Tower, " +
            "Building, HeartTarget, Player) and role layers (Tower, Building, Enemy, Structure - " +
            "wood palisades, stone walls and steel gates all share 'Structure'); RepoProps / " +
            "CatalogEntry have no material field (material words survive only inside ids like " +
            "'wall_wood' and prefab paths); and both footstep implementations play ONE clip with no " +
            "surface query. The nearest real signal is WallTier { Wood, Iron, ReinforcedSteel } on " +
            "WallSegment, which covers player walls only and is a progression index. Defining a " +
            "surface taxonomy is DESIGN and belongs to the owner, so nothing was invented; the " +
            "ELEMENT half of WO-887 - which has a real source (DamageElement via " +
            "TowerCombat.AbilityToElement) - was wired instead.",

            "WO-887 element-proc recipes (TinyExplosion fire, IceLance shards, EnergyExplosion " +
            "arcane, GoopSpray nature): NOT built, because every one would be bytes with no " +
            "consumer. The four impact moments that DO have enum values are already pointed at " +
            "deliberate, tracked, better picks - Impact_Flame at the Spells Pack Spell_Fire_6 " +
            "detonation (the 'fireball headline' pick), Impact_ExplosionAether at Explosion_Arcane, " +
            "Impact_Ice at Lana Hit_frost, Impact_Physical at the Lana slash ARC that an owner " +
            "ruling on 2026-08-02 chose over an impact burst - so re-pointing them at smaller pack " +
            "recipes would be a downgrade dressed as progress. The nature/poison row has no home at " +
            "all: DamageElement is { None, Aether, Flame, Ice } - there is no Nature, Shadow or " +
            "Lightning element in this game, so GoopSpray could never be selected.",

            // -- WO-888 (heal + HP + item auras) deferrals. Same discipline: each is a
            // MEASUREMENT off the real asset, not an opinion, and each is a REFUSAL to
            // catalogue art whose family contradicts the beat it would serve. -----------
            "Cast_Heal repoint (WO-888): the ratified recipe (registry 6a) is a RisingSteam warm " +
            "column. MEASURED off the pack asset, RisingSteam is CONTINUOUS - rateOverTime 3/sec on " +
            "loop, a single layer (the same source this builder already ships as Env_SteamVent and " +
            "Aura_HealingInProgress). But every Cast_Heal CALL SITE is a one-shot " +
            "(VFXManager.Play(VFXType.Cast_Heal, ...) from the hero's heal branch), so repointing it " +
            "would either reclaim a still-emitting system mid-emit or hand a rate-emitting loop to a " +
            "fire-and-forget call - one of the 20 global loop slots gone per cast. Refused, not faked. " +
            "Cast_Heal KEEPS its committed Spells Buffs/Buff_Nature row. NOTE FOR THE OWNER: that row " +
            "is a GREEN glow, i.e. the heal still reads partly by hue. WO-888 covers that on the " +
            "channels it owns - the RISING motion of Aura_HealingInProgress and the heal number - but a " +
            "colour-free CAST beat needs either a burst-shaped rising recipe or a ruling that the cast " +
            "may be held as a short loop with an explicit Stop.",

            "Impact_Heal repoint (WO-888): the ratified recipe (registry 6a) is a FireFlies upward " +
            "burst. MEASURED, FireFlies is CONTINUOUS - 5/sec on the root plus 1/sec on its second " +
            "layer, both looping (the same source already shipped as Harvest_Food / Harvest_Crystal / " +
            "Collector_Ready, all catalogued Family A). Impact_Heal is Family B by definition - it is " +
            "fired fire-and-forget from four HeroHealth paths - so the same refusal applies. It keeps " +
            "its committed Lana Range_attack/Hit_heart row, which IS burst-shaped and IS tracked.",

            "Arcane weapon aura (WO-888 registry 6c): the ratified recipe reuses Aura_EnemyCaster " +
            "(Lana Orbs_electric) at 'faint'. VFXCatalogGenerator names that row, in its own comment, " +
            "as one of three rows that are rate-0 + a single burst while declaring isLoop:true - the " +
            "art is a BURST. Held as a gear loop it would pop once and then hold a loop slot showing " +
            "nothing until the weapon is unequipped. GearAuraMap therefore REFUSES arcane (and " +
            "lightning, which registry section 8 item 8 keeps procedural rather than take a gitignored " +
            "Legacy dependency) and reports the reason. Fire -> Aura_Flame and frost -> Aura_Ice are " +
            "SERVED: both derive continuous. Only knight_flameblade carries element:'fire' in " +
            "weapons.json today, so fire is the one gear aura with live data.",

            "Aura_HealingInProgress / Aura_ItemHeal SCALE (WO-888, not a deferral - a measured " +
            "SURPRISE worth recording): both shipped at root scale 1.25, NOT the 0.8 / 0.5 their rows " +
            "declare. ApplyTuning only applies Row.Scale to a copy whose scale is still 1, and the " +
            "pack's RisingSteam ships at 1.25, so both were correctly reported 'scale PRESERVED " +
            "(already tuned)' and stayed room-sized - and identical to each other, losing the 'LOW " +
            "held' distinction the registry asks of the item seat. Rebuilding them would change their " +
            "GUIDs, so WO-888 seats them on the body at the CALL SITE instead (HeroHpStateAura / " +
            "GearAura scale multipliers, each carrying this measurement). If the owner would rather " +
            "fix the asset, delete the two .prefab files and re-run - the rows already say 0.8 / 0.5.",

            "Death lingering loops (WO-886 'Lingering' column): SmokeEffect settle/column and " +
            "the WildFire lick MEASURE as genuine Family A loops (SmokeEffect 20/sec looping; " +
            "WildFire 100 + 5 + 20/sec looping across 3 layers), which is exactly what the WO " +
            "asks for - 'a SEPARATE capped loop', never folded into the burst. But there is NO " +
            "VFXType for a death linger, and appending to VFXType is Grok's single-owner edit " +
            "(WO-884 section 0.2 / handbook Step 3). Building the prefabs now would ship " +
            "Resources bytes with no consumer and no catalog row. Deferred pending the enum " +
            "values; the recipes are picked, measured and ready.",
        };

        // =====================================================================
        //  Entry point
        // =====================================================================

        /// <summary>
        /// Mirrors every table row's pack recipe into a committed Resources prefab and
        /// writes its VFXCatalog row. Idempotent. Prints PARTICLE_PACK_VFX_BUILD_OK on
        /// success; on ANY failure prints PARTICLE_PACK_VFX_BUILD_FAIL and no success marker.
        /// </summary>
        [MenuItem("Defenders/VFX/Build Particle Pack VFX Batch")]
        public static void Build()
        {
            var errors  = new List<string>();
            var built   = new List<string>();
            var aliased = new List<string>();   // WO-886: extra catalog rows sharing a row's prefab
            var skipped = new List<string>();
            var summary = new StringBuilder();

            try
            {
                var enumType = Type.GetType(VfxTypeEnumName);
                if (enumType == null)
                    throw new Exception("could not resolve '" + VfxTypeEnumName +
                                        "'. Is DeNelle.Village compiled?");

                var catalogType = Type.GetType(CatalogTypeName);
                if (catalogType == null)
                    throw new Exception("could not resolve '" + CatalogTypeName +
                                        "'. Is DeNelle.Village compiled?");

                var catalog = AssetDatabase.LoadAssetAtPath(CatalogPath, catalogType) as ScriptableObject;
                if (catalog == null)
                    throw new Exception("VFXCatalog asset not found at '" + CatalogPath +
                                        "'. Run Defenders/VFX/Generate VFX Catalog first - this builder " +
                                        "ADDS rows, it never creates or rebuilds the catalog.");

                var so = new SerializedObject(catalog);
                var entries = so.FindProperty("Entries");
                if (entries == null)
                    throw new Exception("VFXCatalog has no serialized 'Entries' array property.");

                int packDeps = 0;

                foreach (var row in Rows)
                {
                    string rowError = null;
                    try
                    {
                        if (!Enum.IsDefined(enumType, row.TypeName))
                        {
                            string msg = "VFXType." + row.TypeName + " is not defined - the enum append " +
                                         "is Grok's single-owner edit (WO-884 section 0.2). Row skipped.";
                            if (row.Required) rowError = msg; else skipped.Add(msg);
                            continue;
                        }

                        var source = AssetDatabase.LoadAssetAtPath<GameObject>(row.Source);
                        if (source == null)
                        {
                            string msg = row.TypeName + ": source recipe MISSING at '" + row.Source +
                                         "' - the Particle Pack must be imported on this machine " +
                                         "(never reimport it as part of a build). Nothing was faked; " +
                                         "the type keeps its procedural fallback.";
                            if (row.Required) rowError = msg; else skipped.Add(msg);
                            continue;
                        }

                        GameObject secondary = null;
                        if (!string.IsNullOrEmpty(row.Secondary))
                        {
                            secondary = AssetDatabase.LoadAssetAtPath<GameObject>(row.Secondary);
                            if (secondary == null)
                                Debug.LogWarning(Tag + row.TypeName + ": optional second layer '" +
                                                 row.Secondary + "' is missing - shipping the primary " +
                                                 "recipe alone rather than failing the row.");
                        }

                        var report = new StringBuilder();
                        GameObject dest = BuildOne(row, source, secondary, report);

                        bool isLoop;
                        if (!MeasureAndResolve(row, dest, report, out isLoop, out rowError))
                            continue;

                        WriteCatalogRow(enumType, entries, row, row.TypeName, dest, isLoop, report);

                        // WO-886 aliases: extra VFXType names that must resolve to the SAME
                        // prefab. Written from the one Row so a legacy alias cannot drift
                        // away from the value it aliases. An alias that is not in the enum
                        // is a warning, never a silent skip.
                        if (row.Aliases != null)
                        {
                            foreach (var alias in row.Aliases)
                            {
                                if (!Enum.IsDefined(enumType, alias))
                                {
                                    Debug.LogWarning(Tag + row.TypeName + ": alias VFXType." + alias +
                                                     " is not defined - the alias row was NOT written, so that " +
                                                     "name keeps its procedural fallback.");
                                    continue;
                                }
                                WriteCatalogRow(enumType, entries, row, alias, dest, isLoop, report);
                                aliased.Add(alias + "->" + row.TypeName);
                            }
                        }

                        packDeps += AuditPackDependencies(row, dest, report);

                        built.Add(row.TypeName + "(IsLoop=" + isLoop + ",MinQ=" + row.MinQuality + ")");
                        Debug.Log(Tag + row.TypeName + " -> " + row.Dest + " :: " + report);
                    }
                    catch (Exception rowEx)
                    {
                        rowError = row.TypeName + ": " + rowEx.Message;
                    }
                    finally
                    {
                        if (rowError != null) errors.Add(rowError);
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                foreach (var d in DeferredTypes) Debug.LogWarning(Tag + "DEFERRED - " + d);

                summary.Append("built ").Append(built.Count).Append('/').Append(Rows.Length)
                       .Append(" row(s) [").Append(string.Join(", ", built.ToArray())).Append("]; ")
                       .Append("alias catalog row(s) sharing a prefab: ").Append(aliased.Count)
                       .Append(" [").Append(string.Join(", ", aliased.ToArray())).Append("]; ")
                       .Append("deferred ").Append(DeferredTypes.Length)
                       .Append(" (Enemy_Spawn + Despawn_Dissolve scripted recipes; WO-886 Death_Skeleton, ")
                       .Append("Death_Wolf and the death lingering loops; WO-887 the five surface impacts, ")
                       .Append("the absent surface signal and the element-proc recipes - see the DEFERRED ")
                       .Append("warnings, each carries its measurement); ")
                       .Append("skipped ").Append(skipped.Count).Append("; ")
                       .Append("prefab dependencies still resolving into the gitignored pack: ")
                       .Append(packDeps).Append(" (materials/textures/shaders - the PREFABS are " +
                                                "tracked, their art is not; see the per-row warnings)");

                foreach (var s in skipped) Debug.LogWarning(Tag + "SKIPPED - " + s);

                // One throw, one catch, ONE failure-marker emission site (below). Rows that
                // did succeed are already saved and the run is idempotent, so re-running
                // after a human resolves the error costs nothing and repeats nothing.
                if (errors.Count > 0)
                    throw new Exception(errors.Count + " row error(s): " +
                                        string.Join(" | ", errors.ToArray()));

                Debug.Log(Tag + "DONE. " + summary);
                Debug.Log(MarkerOk + " - " + summary);
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + "FAILED: " + e.Message + "\n" + e.StackTrace);
                Debug.LogError(MarkerFail + " - " + e.Message + " || progress: " + summary);
            }
        }

        // =====================================================================
        //  1-2. Duplicate the recipe (whole tree) + tune + playOnAwake
        // =====================================================================

        private static GameObject BuildOne(Row row, GameObject source, GameObject secondary,
                                           StringBuilder report)
        {
            int srcDescendants = CountDescendants(source.transform);
            int srcSystems     = source.GetComponentsInChildren<ParticleSystem>(true).Length;

            // WO-886: a row may DECLARE how many layers its recipe must have. Checked
            // against the SOURCE, before anything is copied, so a pack that arrived
            // trimmed fails here rather than shipping a boss death that is missing its
            // debris and still looks like an explosion.
            if (row.RequiredSystems > 0 && srcSystems != row.RequiredSystems)
                throw new Exception("RECIPE LAYER COUNT: '" + row.Source + "' carries " + srcSystems +
                                    " ParticleSystem(s) but this row REQUIRES exactly " + row.RequiredSystems +
                                    ". The recipe must be pooled WHOLE (never flattened, never trimmed); " +
                                    "a short recipe still renders something plausible, which is why this " +
                                    "is asserted rather than eyeballed. Verify the Particle Pack import.");

            int expectDescendants = srcDescendants;
            int expectSystems     = srcSystems;
            if (secondary != null)
            {
                // +1 for the merged layer's own root, plus everything under it.
                expectDescendants += CountDescendants(secondary.transform) + 1;
                expectSystems     += secondary.GetComponentsInChildren<ParticleSystem>(true).Length;
            }

            report.Append("source='").Append(source.name).Append("'(")
                  .Append(srcDescendants).Append(" descendants, ")
                  .Append(srcSystems).Append(" systems)");
            if (secondary != null)
                report.Append(" + layer='").Append(secondary.name).Append('\'');
            report.Append("; ");

            EnsureDir(DirOf(row.Dest));

            bool freshCopy = false;
            var dest = AssetDatabase.LoadAssetAtPath<GameObject>(row.Dest);
            if (dest == null)
            {
                if (!AssetDatabase.CopyAsset(row.Source, row.Dest))
                    throw new Exception("AssetDatabase.CopyAsset('" + row.Source + "' -> '" +
                                        row.Dest + "') returned false.");
                AssetDatabase.ImportAsset(row.Dest, ImportAssetOptions.ForceUpdate);
                dest = AssetDatabase.LoadAssetAtPath<GameObject>(row.Dest);
                freshCopy = true;
                if (dest == null)
                    throw new Exception("copied to '" + row.Dest + "' but the asset would not load back.");
            }
            report.Append(freshCopy ? "copied NEW; " : "reused EXISTING (idempotent, GUID preserved); ");

            string destRootName = NameOf(row.Dest);

            // -- Edit the prefab asset through prefab contents (Unity owns the write).
            GameObject contents = PrefabUtility.LoadPrefabContents(row.Dest);
            try
            {
                bool dirty = false;

                if (contents.name != destRootName)
                {
                    report.Append("root renamed '").Append(contents.name).Append("' -> '")
                          .Append(destRootName).Append("'; ");
                    contents.name = destRootName;
                    dirty = true;
                }

                // -- Merge the optional second recipe as a plain child layer. It is a
                // DISCONNECTED clone (Object.Instantiate, not PrefabUtility.Instantiate
                // Prefab) precisely so the committed prefab does not gain a nested-prefab
                // dependency on the gitignored pack.
                if (secondary != null && FindChildByName(contents.transform, SecondaryPrefix + secondary.name) == null)
                {
                    var clone = (GameObject)UnityEngine.Object.Instantiate(secondary);
                    clone.name = SecondaryPrefix + secondary.name;
                    clone.transform.SetParent(contents.transform, false);
                    clone.transform.localPosition = Vector3.zero;
                    clone.transform.localRotation = Quaternion.identity;
                    clone.transform.localScale    = Vector3.one;
                    report.Append("merged layer '").Append(clone.name).Append("'; ");
                    dirty = true;
                }

                // -- Gameplay tuning: FRESH copies only. A prefab already on disk is
                // treated as owner-tuned and is never stomped (CLAUDE.md: the picks are
                // bones; the owner felt-tunes them).
                if (freshCopy)
                {
                    dirty |= ApplyTuning(row, contents, report);
                }
                else
                {
                    report.Append("tuning PRESERVED (scale=").Append(Fmt(contents.transform.localScale))
                          .Append(", emission untouched - existing prefab treated as owner-tuned; ")
                          .Append("delete the .prefab to re-derive); ");
                }

                // -- playOnAwake off on every layer, EVERY run. Every one of these pack
                // recipes ships playOnAwake:1; VFXManager Clear()s and Play()s the whole
                // tree explicitly, so nothing is lost - but a prewarmed pool instance
                // would otherwise fire once at the world origin the moment it is created.
                int cleared = 0;
                foreach (var ps in contents.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    if (!main.playOnAwake) continue;
                    main.playOnAwake = false;
                    cleared++;
                }
                if (cleared > 0)
                {
                    report.Append("playOnAwake cleared on ").Append(cleared).Append(" system(s); ");
                    dirty = true;
                }

                // -- WO-886 BURST-ONCE. Re-applied EVERY run, like playOnAwake, because it
                // is a correctness invariant of a one-shot rather than a taste call: every
                // explosion recipe in this pack ships looping:1 + prewarm:1 with duration
                // 2 s and its whole payload in a t=0 burst, while VFXManager reclaims a
                // pooled oneshot at DetectDuration = duration + max startLifetime (~4.3 s
                // MEASURED on BigExplosion). The burst therefore RE-FIRES at t=2 and a boss
                // death detonates twice. prewarm is cleared first - Unity only permits it on
                // a looping system, so clearing loop while prewarm is set is invalid.
                if (row.BurstOnce)
                {
                    int unlooped = 0;
                    foreach (var ps in contents.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        var main = ps.main;
                        if (!main.loop && !main.prewarm) continue;
                        main.prewarm = false;
                        main.loop    = false;
                        unlooped++;
                    }
                    if (unlooped > 0)
                    {
                        report.Append("burst-once: loop+prewarm cleared on ").Append(unlooped)
                              .Append(" system(s) (one-shot invariant, prevents the t=duration re-fire); ");
                        dirty = true;
                    }
                }

                if (dirty) PrefabUtility.SaveAsPrefabAsset(contents, row.Dest);
                else       report.Append("already in target state; ");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.ImportAsset(row.Dest, ImportAssetOptions.ForceUpdate);
            dest = AssetDatabase.LoadAssetAtPath<GameObject>(row.Dest);
            if (dest == null)
                throw new Exception("'" + row.Dest + "' would not reload after the edit pass.");

            // -- THE REVIEW-FAILURE GUARD: the whole multi-layer tree must be present.
            // Checked AFTER the edit pass so the merged layer is counted too.
            int dstDescendants = CountDescendants(dest.transform);
            int dstSystems     = dest.GetComponentsInChildren<ParticleSystem>(true).Length;
            if (dstDescendants != expectDescendants || dstSystems != expectSystems)
                throw new Exception("LAYER LOSS: '" + row.Dest + "' has " + dstDescendants +
                                    " descendants / " + dstSystems + " ParticleSystems but the recipe " +
                                    "requires " + expectDescendants + " / " + expectSystems +
                                    ". The multi-layer tree must survive intact (never flatten; quality " +
                                    "tiers DISABLE children, they do not delete them). Delete '" +
                                    row.Dest + "' and re-run to rebuild it clean.");

            report.Append("tree=").Append(dstDescendants).Append(" descendants / ")
                  .Append(dstSystems).Append(" systems [");
            AppendChildNames(dest.transform, report);
            report.Append("] VERIFIED vs recipe; ");

            return dest;
        }

        /// <summary>
        /// Applies the row's documented motion tuning to every ParticleSystem in the fresh
        /// copy. Each knob implements a literal phrase from the creative registry (see the
        /// Why field); nothing here is invented taste. A scaled emission rate is floored so
        /// a multiplier can never silently produce a 0/sec (invisible) emitter.
        /// </summary>
        private static bool ApplyTuning(Row row, GameObject contents, StringBuilder report)
        {
            bool dirty = false;

            Vector3 scale = contents.transform.localScale;
            bool untouched = Mathf.Approximately(scale.x, 1f)
                          && Mathf.Approximately(scale.y, 1f)
                          && Mathf.Approximately(scale.z, 1f);
            if (!Mathf.Approximately(row.Scale, 1f) && untouched)
            {
                contents.transform.localScale = Vector3.one * row.Scale;
                report.Append("scale 1 -> ").Append(row.Scale.ToString("0.##")).Append("; ");
                dirty = true;
            }
            else if (!untouched)
            {
                report.Append("scale PRESERVED at ").Append(Fmt(scale)).Append(" (already tuned); ");
            }

            bool doRate = !Mathf.Approximately(row.RateMul, 1f);
            bool doSpeed = !Mathf.Approximately(row.SpeedMul, 1f);
            bool doLife = !Mathf.Approximately(row.LifeMul, 1f);
            bool doGrav = !float.IsNaN(row.Gravity);
            if (!doRate && !doSpeed && !doLife && !doGrav) return dirty;

            int touched = 0;
            foreach (var ps in contents.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (doRate)
                {
                    var em = ps.emission;
                    em.rateOverTime     = ScaledRate(em.rateOverTime, row.RateMul);
                    em.rateOverDistance = ScaledRate(em.rateOverDistance, row.RateMul);
                }

                var main = ps.main;
                if (doSpeed) main.startSpeed    = Scaled(main.startSpeed, row.SpeedMul);
                if (doLife)  main.startLifetime = Scaled(main.startLifetime, row.LifeMul);
                if (doGrav)  main.gravityModifier = new ParticleSystem.MinMaxCurve(row.Gravity);
                touched++;
            }

            report.Append("tuned ").Append(touched).Append(" system(s) [");
            if (doRate)  report.Append("rate x").Append(row.RateMul.ToString("0.##")).Append(' ');
            if (doSpeed) report.Append("speed x").Append(row.SpeedMul.ToString("0.##")).Append(' ');
            if (doLife)  report.Append("life x").Append(row.LifeMul.ToString("0.##")).Append(' ');
            if (doGrav)  report.Append("gravity=").Append(row.Gravity.ToString("0.##"));
            report.Append("] per registry (\"").Append(row.Why).Append("\"); ");
            return true;
        }

        // =====================================================================
        //  3. Measure the emission family, then let the SHARED oracle decide IsLoop
        // =====================================================================

        /// <summary>
        /// Logs the measured emission of every layer, checks the measurement against what
        /// the enum doc claims, and takes the IsLoop flag from the shared derivation
        /// (VfxLoopFlagRegression.TryResolveExpected), which also honours owner rulings.
        /// Returns false with an error when the art contradicts the doc, or when the flag
        /// cannot be derived at all - in both cases NO catalog row is written.
        /// </summary>
        private static bool MeasureAndResolve(Row row, GameObject dest, StringBuilder report,
                                              out bool isLoop, out string error)
        {
            isLoop = false;
            error  = null;

            var systems = dest.GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length == 0)
            {
                error = row.TypeName + ": '" + row.Dest + "' has NO ParticleSystem at all - " +
                        "nothing would ever emit.";
                return false;
            }

            // -- Measure EVERY layer off the real asset and log it. The doc comment is
            // evidence, not proof; these numbers are the proof.
            bool anyRate = false;
            report.Append("emission{");
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                var em = ps.emission;
                var mn = ps.main;

                float rateTime = em.enabled ? VfxLoopFlagRegressionRateTime(em) : 0f;
                float rateDist = em.enabled ? VfxLoopFlagRegressionRateDist(em) : 0f;
                int   bursts   = em.enabled ? em.burstCount : 0;
                if (rateTime > 0f || rateDist > 0f) anyRate = true;

                if (i > 0) report.Append(", ");
                report.Append(ps.gameObject.name)
                      .Append(":rateOverTime=").Append(rateTime.ToString("0.##"))
                      .Append(",rateOverDistance=").Append(rateDist.ToString("0.##"))
                      .Append(",bursts=").Append(bursts)
                      .Append(",loop=").Append(mn.loop ? "Y" : "N")
                      .Append(em.enabled ? string.Empty : ",EMISSION-OFF");

                Debug.Log(Tag + row.TypeName + " layer '" + ps.gameObject.name +
                          "': rateOverTime=" + rateTime.ToString("0.##") +
                          " rateOverDistance=" + rateDist.ToString("0.##") +
                          " bursts=" + bursts +
                          " main.loop=" + mn.loop +
                          " emissionEnabled=" + em.enabled +
                          " playOnAwake=" + mn.playOnAwake);
            }
            report.Append("}; ");

            // -- THE FLAG ITSELF IS NOT DERIVED HERE. One home for the rule, so the tool
            // that writes the flag and the gate that judges it can never disagree.
            string detail;
            if (!DeNelle.Editor.Regression.VfxLoopFlagRegression.TryResolveExpected(
                    row.TypeName, dest, out isLoop, out detail))
            {
                error = row.TypeName + ": the shared loop-flag derivation could not read this " +
                        "prefab (" + detail + "). Refusing to guess a flag - no catalog row written.";
                return false;
            }

            string measured = isLoop ? "CONTINUOUS" : "BURST";
            report.Append("family=").Append(measured)
                  .Append(" -> IsLoop=").Append(isLoop)
                  .Append(" [shared derivation: ").Append(detail).Append("]; ");

            // -- The doc-versus-art check. A row whose enum doc claims the OPPOSITE family
            // is refused outright; writing the flag anyway is exactly how a burst prefab
            // ends up holding one of the 20 global loop slots for the rest of the session.
            if (row.Expect == Family.Continuous && !isLoop)
            {
                error = row.TypeName + ": FAMILY MISMATCH. The enum doc claims a CONTINUOUS loop, " +
                        "but the art reads as a BURST (" + detail + "; any layer emitting by rate: " +
                        anyRate + "). Refusing to author an IsLoop=false row under a loop-shaped " +
                        "contract, and refusing to force IsLoop=true on a self-terminating prefab - " +
                        "that leaks a loop slot forever. A human must re-pick the recipe or correct " +
                        "the doc.";
                return false;
            }
            if (row.Expect == Family.Burst && isLoop)
            {
                error = row.TypeName + ": FAMILY MISMATCH. The enum doc claims a one-shot BURST, but " +
                        "the art reads as CONTINUOUS (" + detail + "). Cataloguing it as a loop would " +
                        "hand out a VFXHandle nothing stops and burn one of the 20 loop slots; " +
                        "cataloguing it as a one-shot would reclaim a still-emitting system. A human " +
                        "must re-pick the recipe or correct the doc.";
                return false;
            }
            if (row.Expect == Family.Either)
            {
                report.Append("(enum doc permits EITHER family - measured value recorded, not judged); ");
                Debug.Log(Tag + row.TypeName + ": the enum doc permits either family; the art measures " +
                          measured + ", so the catalog row carries IsLoop=" + isLoop + ".");
            }

            return true;
        }

        // The emission max readers go through the shared oracle's curve maths so a
        // two-constants / curve-authored rate is measured the same way the gate measures it.
        private static float VfxLoopFlagRegressionRateTime(ParticleSystem.EmissionModule em)
        {
            return DeNelle.Editor.Regression.VfxLoopFlagRegression.MaxOf(em.rateOverTime);
        }

        private static float VfxLoopFlagRegressionRateDist(ParticleSystem.EmissionModule em)
        {
            return DeNelle.Editor.Regression.VfxLoopFlagRegression.MaxOf(em.rateOverDistance);
        }

        // =====================================================================
        //  4. VFXCatalog row (SerializedObject - Unity owns the serialization)
        // =====================================================================

        /// <summary>
        /// Writes (or updates in place) the catalog row for ONE VFXType name.
        /// <paramref name="typeName"/> is passed separately from <paramref name="row"/> so a
        /// WO-886 alias can be written from the same Row and therefore cannot drift from the
        /// value it aliases - same prefab, same IsLoop, same pool, one place to tune.
        /// </summary>
        private static void WriteCatalogRow(Type enumType, SerializedProperty entries, Row row,
                                            string typeName, GameObject prefab, bool isLoop,
                                            StringBuilder report)
        {
            int enumValue   = (int)Enum.Parse(enumType, typeName);
            int enumOrdinal = EnumOrdinalFor(enumType, enumValue);

            // Find an existing row for this type (UPDATE, never append a duplicate).
            int rowIndex = -1;
            for (int i = 0; i < entries.arraySize; i++)
            {
                var t = entries.GetArrayElementAtIndex(i).FindPropertyRelative("Type");
                if (t != null && t.enumValueIndex == enumOrdinal) { rowIndex = i; break; }
            }

            bool appended = rowIndex < 0;
            if (appended)
            {
                rowIndex = entries.arraySize;
                entries.arraySize = rowIndex + 1;
            }

            var e = entries.GetArrayElementAtIndex(rowIndex);
            var pType   = e.FindPropertyRelative("Type");
            var pPrefab = e.FindPropertyRelative("Prefab");
            var pPool   = e.FindPropertyRelative("PoolSize");
            var pLoop   = e.FindPropertyRelative("IsLoop");
            var pMinQ   = e.FindPropertyRelative("MinQuality");
            var pLife   = e.FindPropertyRelative("LifetimeOverride");

            if (pType == null || pPrefab == null || pPool == null ||
                pLoop == null || pMinQ   == null || pLife == null)
                throw new Exception("VFXCatalog.Entry is missing an expected field " +
                                    "(Type/Prefab/PoolSize/IsLoop/MinQuality/LifetimeOverride) - " +
                                    "the row shape changed; update this builder before running it again.");

            pType.enumValueIndex         = enumOrdinal;
            pPrefab.objectReferenceValue = prefab;
            pPool.intValue               = row.PoolSize;
            pLoop.boolValue              = isLoop;
            pMinQ.intValue               = row.MinQuality;
            pLife.floatValue             = 0f;   // auto-detect from the particle duration

            report.Append("catalog row '").Append(typeName).Append("' ")
                  .Append(appended ? "APPENDED" : "UPDATED")
                  .Append(" at index ").Append(rowIndex).Append('/').Append(entries.arraySize)
                  .Append(" (ordinal ").Append(enumOrdinal).Append(", value ").Append(enumValue)
                  .Append(", IsLoop=").Append(isLoop)
                  .Append(", PoolSize=").Append(row.PoolSize)
                  .Append(", MinQuality=").Append(row.MinQuality).Append("); ");
        }

        // =====================================================================
        //  5. Dependency audit - instrument the gap, do not paper over it
        // =====================================================================

        /// <summary>
        /// Counts how many of a destination prefab's dependencies STILL resolve into the
        /// gitignored pack. CopyAsset duplicates the prefab, not the materials / textures /
        /// shaders it points at, so a committed prefab can still be art-less on a fresh
        /// clone. That is true of the already-shipped Boss_FireBreath as well (3 pack
        /// materials), so this builder does not silently change the precedent - it MEASURES
        /// the exposure and reports the number so a human can rule on mirroring the art.
        /// </summary>
        private static int AuditPackDependencies(Row row, GameObject dest, StringBuilder report)
        {
            var deps = AssetDatabase.GetDependencies(row.Dest, true);
            var offenders = new List<string>();
            foreach (var d in deps)
            {
                if (d == row.Dest) continue;
                if (d.StartsWith(PackRoot, StringComparison.OrdinalIgnoreCase)) offenders.Add(d);
            }

            report.Append("packDeps=").Append(offenders.Count).Append("; ");
            if (offenders.Count > 0)
            {
                Debug.LogWarning(Tag + row.TypeName + ": the prefab is committed, but " +
                                 offenders.Count + " of its dependencies still live in the " +
                                 "GITIGNORED pack and will be missing on a fresh clone -> " +
                                 offenders[0] + (offenders.Count > 1 ? " (+" + (offenders.Count - 1) +
                                 " more)" : string.Empty) +
                                 ". Same exposure as the already-shipped Boss_FireBreath; mirroring " +
                                 "the materials/textures/shaders is a separate, deliberate decision.");
            }
            return offenders.Count;
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>Scales a MinMaxCurve in whatever mode it was authored in.</summary>
        private static ParticleSystem.MinMaxCurve Scaled(ParticleSystem.MinMaxCurve c, float k)
        {
            switch (c.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return new ParticleSystem.MinMaxCurve(c.constant * k);
                case ParticleSystemCurveMode.TwoConstants:
                    return new ParticleSystem.MinMaxCurve(c.constantMin * k, c.constantMax * k);
                case ParticleSystemCurveMode.Curve:
                    return new ParticleSystem.MinMaxCurve(c.curveMultiplier * k, c.curve);
                case ParticleSystemCurveMode.TwoCurves:
                    return new ParticleSystem.MinMaxCurve(c.curveMultiplier * k, c.curveMin, c.curveMax);
                default:
                    return c;
            }
        }

        /// <summary>
        /// Scales an emission rate, but never from "emitting" down to "silent": a
        /// multiplier that would drive a live rate below MinScaledRate is clamped so the
        /// tuning can thin a stream without accidentally turning the effect off (which
        /// would also flip its derived family from loop to one-shot).
        /// </summary>
        private static ParticleSystem.MinMaxCurve ScaledRate(ParticleSystem.MinMaxCurve c, float k)
        {
            float max = DeNelle.Editor.Regression.VfxLoopFlagRegression.MaxOf(c);
            if (max <= 0f) return c;                      // already silent - leave it alone
            float applied = k;
            if (max * k < MinScaledRate) applied = MinScaledRate / max;
            return Scaled(c, applied);
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
                if (root.GetChild(i).name == name) return root.GetChild(i);
            return null;
        }

        private static int CountDescendants(Transform root)
        {
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root) n++;
            }
            return n;
        }

        private static void AppendChildNames(Transform root, StringBuilder sb)
        {
            var names = new List<string>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root) names.Add(t.name);
            }
            sb.Append(string.Join(" + ", names.ToArray()));
        }

        /// <summary>
        /// SerializedProperty.enumValueIndex is the ORDINAL position in the enum's value
        /// list, not the underlying int - map the value back to its ordinal, or every row
        /// silently points at the wrong art.
        /// </summary>
        private static int EnumOrdinalFor(Type enumType, int underlyingValue)
        {
            var values = Enum.GetValues(enumType);
            for (int i = 0; i < values.Length; i++)
            {
                if ((int)values.GetValue(i) == underlyingValue) return i;
            }
            throw new Exception("enum value " + underlyingValue + " has no ordinal in " + enumType.Name + ".");
        }

        private static string DirOf(string assetPath)
        {
            int slash = assetPath.LastIndexOf('/');
            return slash < 0 ? assetPath : assetPath.Substring(0, slash);
        }

        private static string NameOf(string assetPath)
        {
            int slash = assetPath.LastIndexOf('/');
            string file = slash < 0 ? assetPath : assetPath.Substring(slash + 1);
            int dot = file.LastIndexOf('.');
            return dot < 0 ? file : file.Substring(0, dot);
        }

        private static void EnsureDir(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            var parts = dir.Split('/');
            string cur = parts[0];                       // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        private static string Fmt(Vector3 v)
        {
            return "(" + v.x.ToString("0.###") + ", " + v.y.ToString("0.###") + ", " + v.z.ToString("0.###") + ")";
        }
    }
}
