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
// WO-892 + WO-893 (2026-08-05/06) ADDED TWO MORE CAPABILITIES, both general:
//   * Row.HovlKey - an ASSET-ONLY row. Not every VFX moment in this game is reached
//     through a VFXType: StructureDamageVisuals (WO-672) drives its tells through
//     VFXManager.PlayKey(STRING), the Hovl string-key path. Appending to VFXType is
//     Grok's single-owner edit (WO-884 section 0.2), so a row whose consumer is a
//     string key declares that key instead of a TypeName. It is copied, tuned,
//     playOnAwake-cleared, layer-verified and MEASURED exactly like every other row -
//     the only difference is that its consumer row lives in
//     HovlVfxCatalogGenerator.Map rather than VFXCatalogGenerator.Map. The measured
//     IsLoop is reported so that row can be authored to agree; VfxLoopFlagRegression
//     already audits the Hovl catalog's stored IsLoop against the prefab, so the two
//     surfaces still cannot drift.
//   * THE ROOT DEMO-GEOMETRY GUARD - the WO-887 lesson turned into a MACHINE CHECK
//     instead of a paragraph nobody re-reads. Five surface-impact recipes were refused
//     that night because their prefab ROOT is a demo TARGET (MeshFilter + MeshRenderer
//     + a Collider with the particle tree hanging underneath), so a CopyAsset would
//     render a lit primitive AND drop a physics collider at every play position. Every
//     row now PROVES its SOURCE ROOT carries none of those, and none of the pack's own
//     scripts, before anything is copied - hard-failing with the component list when it
//     does. It reuses WO-889's HasDemoGeometry / DescribeComponents predicate rather
//     than restating it; WO-889 strips demo geometry from CHILD subtrees, this refuses
//     it on the ROOT, and the two together are the whole rule.
//     MEASURED clean on all seven recipes added by WO-892/893 (MediumFlames 1 layer,
//     SmokeEffect 1, WildFire 3, SparksEffect 4, EnergyExplosion 4, DustExplosion 5,
//     BigExplosion 8 - zero MeshFilter, zero MeshRenderer, zero Collider, zero
//     MonoBehaviour on any of their prefab files).
//
// WO-890 + WO-891 (2026-08-05) ADDED NO ROWS EITHER, and again that is the finding.
//   * WO-890's six harvest recipes were built by THIS builder earlier the same night and
//     were RE-VERIFIED at source rather than rebuilt: every root carries only
//     GameObject/Transform/ParticleSystem (no MeshFilter, no MeshRenderer, no Collider -
//     none of the WO-887 demo-geometry problem), playOnAwake is 0 on every layer of all
//     six, and every one measures CONTINUOUS at its root, matching IsLoop=true. Their
//     per-row motion tuning survived intact and is genuinely distinct, so they are six
//     effects and not five aliases. WO-890's real deliverable was CALL SITES - all six
//     had ZERO runtime consumers, i.e. the art shipped and nothing ever played it.
//   * WO-891's healer needs no new art at all: Aura_Healer and Impact_Heal are both
//     already committed, tracked and family-correct (measured below), so the deliverable
//     was the behaviour + the element table, not a CopyAsset.
//
// DELIBERATELY NOT DONE (reported, never faked):
//   * Enemy_Spawn / Despawn_Dissolve. Their recipes (Misc/Respawn, Misc/Dissolve)
//     are SCRIPTED effects: each carries a MonoBehaviour from the pack's own
//     Misc Effects/Scripts/SpawnEffect.cs plus a demo MESH for the script to
//     dissolve. Copying them would produce a prefab that (a) renders a demo mesh
//     wherever it plays and (b) carries a missing-script reference on any machine
//     without the gitignored pack. Those two moments need a real runtime component
//     driving the TARGET's own material cutoff - authoring work, not a CopyAsset.
//     They are left out of the table and named in the run report. RE-MEASURED for
//     WO-893 rather than taken on trust - the numbers are in DeferredTypes.
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
        private const string DestDamage  = "Assets/Resources/VFX/Damage/";  // WO-892 structure damage states
        private const string DestPortal  = "Assets/Resources/VFX/Portal/";  // WO-893 portals + spawn tiers

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

            // -- WO-892 addition --------------------------------------------------
            /// <summary>
            /// The Hovl STRING KEY this recipe is consumed through, for rows that have no
            /// VFXType. <see cref="TypeName"/> is then left empty and NO VFXCatalog row is
            /// written; the consumer row lives in HovlVfxCatalogGenerator.Map instead.
            ///
            /// WHY A SECOND CONSUMER SURFACE EXISTS AT ALL: StructureDamageVisuals (WO-672)
            /// - the one structure-damage observer, which WO-892 re-skins rather than
            /// rewrites - drives every tell through VFXManager.PlayKey(STRING). Appending
            /// to VFXType is Grok's single-owner edit (WO-884 section 0.2), so minting five
            /// enum values here is not available; the string path is the one this observer
            /// already speaks. The row is still copied, tuned, playOnAwake-cleared,
            /// layer-verified and MEASURED identically - only the destination of the
            /// catalog write differs, and VfxLoopFlagRegression audits the Hovl catalog's
            /// stored IsLoop against the prefab exactly as it audits the typed one.
            /// </summary>
            public string  HovlKey;

            /// <summary>
            /// What to call this row in logs and errors: the VFXType name when it has one,
            /// otherwise the Hovl key. Never null for a well-formed row.
            /// </summary>
            public string Label
            {
                get { return !string.IsNullOrEmpty(TypeName) ? TypeName : HovlKey; }
            }

            /// <summary>True for an asset-only row whose consumer is a Hovl string key.</summary>
            public bool KeyOnly
            {
                get { return string.IsNullOrEmpty(TypeName); }
            }

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

            // -- WO-889 addition -------------------------------------------------
            /// <summary>
            /// Delete DEMO GEOMETRY from the copy: any child subtree that contains NO
            /// ParticleSystem at all and carries a MeshFilter / MeshRenderer / Collider.
            /// Applied EVERY run, like playOnAwake, because a lit primitive and a physics
            /// collider riding along with an aura are never correct - not a taste call.
            ///
            /// THIS IS NOT "FLATTENING". The never-flatten law (handbook 1.2) protects the
            /// multi-layer PARTICLE recipe; a stripped node by definition contains no
            /// particle layer. WO-887 REFUSED five recipes for demo geometry, but there the
            /// geometry was on the ROOT and PickAuthority fell through it to a child that
            /// derived the wrong family - the prefab was shaped as a demo TARGET rather than
            /// an effect. The distinction that makes a strip safe here: the ROOT itself must
            /// hold the authoritative ParticleSystem, so removing a scenery sibling cannot
            /// move the derivation authority or change the measured family. The builder
            /// asserts exactly that before stripping, and refuses the row otherwise.
            /// </summary>
            public bool    StripGeometry;
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

            // == WO-889 PERSISTENT COMBAT AURAS (registry section 6d) ==============
            // Every row here MEASURED CONTINUOUS off the real asset before it was written
            // (per-layer numbers are in the run log and in the WO-889 result). All are
            // Family A, played through PlayAura and ended through a held VFXHandle.
            //
            // ONLY the moments with NO committed art today are built. Four of registry
            // 6d's rows (Aura_Ice / Aura_Flame / Aura_Necromancer / Aura_SmokeReaper)
            // already point at richer, GIT-TRACKED Lana recipes and are deliberately NOT
            // repointed - the measurements that forced that call are in DeferredTypes.
            //
            // GREYSCALE IS THE ACCEPTANCE CHANNEL (owner is red/green colourblind): each
            // aura differs from its neighbours by DENSITY, LAYER COUNT, LIFETIME and
            // GRAVITY - never by the tint of the material.

            // Aura_EnemyCaster: the one registry 6d row that is a genuine FIX rather than
            // a re-skin. Its incumbent (Lana Orbs/Orbs_electric) MEASURES AS A BURST -
            // authority layer 'orbs' is main.loop FALSE with rateOverTime 0 - so held as an
            // aura it pops once and then occupies a loop slot rendering NOTHING until the
            // caster dies. VFXCatalogGenerator's own comment already names this row as one
            // of three that "contradict their own art". ElectricalSparks is a true loop:
            // root ParticleSystem, main.loop TRUE, rateOverTime 50/sec constant.
            //
            // Thinned to 40% (50 -> 20/sec) and seated small: the pack ships this as a
            // demo-scale spark fountain, and a caster wants a CRACKLING CONDUIT clinging to
            // a body. Greyscale read = high-frequency, short-lived point flicker tight to
            // the silhouette - the only aura in the set with that stochastic sparkle
            // cadence (the fogs roil, the dust settles, the flames lick).
            //
            // StripGeometry: MEASURED, this recipe carries a 'Plane' child with a
            // MeshFilter + MeshRenderer + MeshCollider - demo-scene scenery for the sparks
            // to bounce off. Copied as-is it would render a lit primitive AND add a physics
            // collider on every caster enemy. The root holds the authoritative
            // ParticleSystem, so removing that scenery cannot move the derivation.
            new Row { TypeName = "Aura_EnemyCaster", Source = Misc + "ElectricalSparks.prefab",
                      Dest = DestAura + "Aura_EnemyCaster.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 4, Required = false, StripGeometry = true,
                      Scale = 0.6f, RateMul = 0.4f, SpeedMul = 1f, LifeMul = 0.6f, Gravity = None,
                      Why = "registry 6d: crackling conduit; REPLACES a burst-shaped incumbent" },

            // Aura_Dust: no committed art at all today (no catalog row, no consumer until
            // this WO). GroundFog at half density, half lifetime and a POSITIVE gravity so
            // it SETTLES at the feet instead of hanging as room fog - the registry flags
            // "fog != kicked-dust" and gravity is what closes that gap. Greyscale read = a
            // flat, low sheet hugging the ground, the only aura in the set with no vertical
            // extent at all.
            new Row { TypeName = "Aura_Dust", Source = Smoke + "GroundFog.prefab",
                      Dest = DestAura + "Aura_Dust.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 4, Required = false,
                      Scale = 0.5f, RateMul = 0.5f, SpeedMul = 1f, LifeMul = 0.5f, Gravity = 0.12f,
                      Why = "registry 6d: foot dust, low; gravity SETTLES it (fog != kicked dust)" },

            // -- Pet level ladder (registry 6d "density escalation") ---------------
            // The three rungs escalate by RECIPE then LAYER COUNT then DENSITY, which is
            // the same greyscale-first ladder WO-886 used for deaths. A pet owner must be
            // able to see a level-up with the colour removed:
            //   L1 DustMotes  1 layer  - dull motes, flat drift, no twinkle
            //   L2 FireFlies  2 layers - discrete twinkling points that bob
            //   L3 FireFlies + SparksEffect, 3+ layers - twinkle PLUS falling glints
            // so the rungs differ in WHAT IS HAPPENING, not merely in how much of it.

            // DustMotes ships at 100/sec (a room-fill ambience); a pet-sized aura wants a
            // fraction of that. Gravity 0 = the flat sideways drift that keeps L1 visually
            // "inert" next to L2's bobbing twinkle.

            new Row { TypeName = "Aura_TalentNode", Source = Misc + "FireFlies.prefab",
                      Dest = DestAura + "Aura_TalentNode.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 3, Required = false,
                      Scale = 0.6f, RateMul = 1.5f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      Why = "TALENT-TREE NODE aura = FireFlies, discrete bobbing twinkle. Built as the pet L2 rung in registry 6d; the pet ladder was deleted 2026-08-20 and this recipe survives because the TALENT TREE uses it (TalentNodeVfxRig.AuraResourcePath)." },

            // The top rung MERGES a second recipe rather than merely turning L2 up - a
            // denser copy of L2 would be indistinguishable from L2 at a glance, which is
            // exactly the failure the registry's "density escalation" phrase invites.

            // -- Boss phase ladder (registry 6d "calm -> enraged -> seething") -----
            // DragonBoss already drives these three VFXType values through ONE handle
            // (_auraHandle, swapped on every phase transition), so the art is the only
            // half that was missing. The escalation is deliberately a RECIPE CHANGE at
            // each step, not a scale ramp:
            //   P1 RisingSteam   1 layer  - a slow thin rising column. Calm, sparse, vertical.
            //   P2 MediumFlames  1 layer  - a body-hugging flame envelope. Licking, mid-frequency.
            //   P3 WildFire      3 layers - base + 100/sec embers + 20/sec fire. A seething boil
            //                               that throws ember scatter well past the silhouette.
            // In greyscale that is: thin-and-vertical -> dense-and-clinging -> multi-layer
            // and spitting. Phase is legible from the SHAPE of the effect with all colour
            // removed, and from the count of distinct things moving.
            //
            // These never fight the boss's fire-breath: DragonBoss holds the breath on
            // _breathHandle parented to the mouth SOCKET, and the phase aura on a separate
            // _auraHandle parented to the body transform. Two fields, two parents, and
            // StopPhaseAura runs before every re-start.
            //
            // NOTE (measured, and it will show in the run log): the pack's RisingSteam ships
            // at root scale 1.25, and ApplyTuning only writes Row.Scale onto a copy whose
            // scale is still exactly 1 - so P1 will report "scale PRESERVED (already tuned)"
            // and stay at 1.25 rather than taking the 2.0 below. On a dragon that is
            // acceptable (the boss is large), which is why this is recorded rather than
            // worked around; see the WO-888 note in DeferredTypes for the same measurement.
            new Row { TypeName = "Boss_Aura_Phase1", Source = Smoke + "RisingSteam.prefab",
                      Dest = DestAura + "Boss_Aura_Phase1.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 2, Required = false,
                      Scale = 2f, RateMul = 1.2f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      Why = "registry 6d: boss P1 CALM = RisingSteam, thin vertical column" },

            new Row { TypeName = "Boss_Aura_Phase2", Source = Fire + "MediumFlames.prefab",
                      Dest = DestAura + "Boss_Aura_Phase2.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 2, Required = false,
                      Scale = 2.2f, RateMul = 2f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      Why = "registry 6d: boss P2 ENRAGED = MediumFlames, clinging envelope" },

            // Speed up rather than merely scale up: a faster-running fire snaps and recovers
            // instead of drifting, which is what "seething" reads as when hue is removed
            // (the same channel HeroHpStateAura uses for its near-death gutter).
            new Row { TypeName = "Boss_Aura_Phase3", Source = Fire + "WildFire.prefab",
                      Dest = DestAura + "Boss_Aura_Phase3.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 2, Required = false,
                      Scale = 2.5f, RateMul = 1.4f, SpeedMul = 1.3f, LifeMul = 1f, Gravity = None,
                      RequiredSystems = 3,
                      Why = "registry 6d: boss P3 SEETHING = WildFire, 3-layer boil + ember scatter" },

            // == WO-892 STRUCTURE DAMAGE STATES (registry section 6g) ==============
            // These five are HovlKey rows: their consumer is StructureDamageVisuals, which
            // plays every tell through VFXManager.PlayKey(STRING). See Row.HovlKey.
            //
            // WHAT THIS REPLACES, AND WHY IT IS A FIX RATHER THAN A RE-SKIN. The observer
            // asks for two keys today, "Ember_Burn" (smolder + fire) and "Raid_Explosion"
            // (the break burst). VERIFIED AT SOURCE:
            //   * Ember_Burn is declared in HovlVfxCatalogGenerator.Map as
            //     "RPG VFX Bundle/Random effect prefabs/Debuff 1.prefab" - a path that DOES
            //     NOT EXIST in the pack on this machine (the folder holds "Debuff chain"
            //     and "Debuff scythe"; there is no "Debuff 1"). The generator skips a row
            //     whose prefab will not load, so Ember_Burn is ABSENT from the shipped
            //     HovlVfxCatalog.asset - grep the asset, the key is not there. PlayKey on a
            //     key the catalog does not hold is a throttled no-op. THE SMOLDER AND FIRE
            //     LOOPS HAVE THEREFORE NEVER RENDERED, on any machine, since WO-672.
            //   * Raid_Explosion IS in the asset, but points at Hovl Studio art, and
            //     /Assets/Hovl Studio/ is GITIGNORED (.gitignore:218) with ZERO files
            //     tracked - so the one damage tell that does resolve resolves only on a
            //     machine that happens to have the 236 MB pack on disk. Same WO-785
            //     exposure the death ladder was moved off.
            // Both are closed the same way the death ladder was: tracked Particle Pack
            // mirrors under Assets/Resources/VFX/Damage/, self-contained after
            // VfxResourceArtMirror.
            //
            // GREYSCALE IS THE ACCEPTANCE CHANNEL (the owner is red/green colourblind).
            // The four states differ by SMOKE DENSITY, FLAME PRESENCE, PULSE RHYTHM and
            // LAYER COUNT - every one of which survives with all colour removed:
            //   smolder  1 layer, thin slow smoke, no flame, steady
            //   fire     2 layers, flame present + a much denser smoke volume, steady
            //   critical 4 layers, no smoke at all, hard fast STROBE (rhythm) + a "!" glyph
            //   broken   one 5-layer grounded debris scatter, then 3 layers of low wide
            //            guttering burn over a shell that is no longer standing
            // LANDSCAPE PHONE (2670x1200): the vertical axis is the scarce one, so every
            // one of these is deliberately kept low and close to the structure. None of
            // them is a rising column.

            // Smolder (hp <= 0.5). SmokeEffect is ONE layer at 20/sec on loop (measured).
            // Thinned to 45% and shortened: "taking damage", a wisp, NOT a fire.
            new Row { HovlKey = "Damage_Smolder", Source = Smoke + "SmokeEffect.prefab",
                      Dest = DestDamage + "Damage_Smolder.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 4, Required = false,
                      Scale = 0.9f, RateMul = 0.45f, SpeedMul = 1f, LifeMul = 0.8f, Gravity = None,
                      RequiredSystems = 1,
                      Why = "registry 6g: smolder = SmokeEffect low (light smoke wisp)" },

            // Fire (hp <= 0.25). MediumFlames (1 layer) with SmokeEffect MERGED as a second
            // layer - the registry's literal "MediumFlames + SmokeEffect". The step up from
            // smolder is therefore a FLAME APPEARING plus roughly double the smoke, not a
            // colour change: in greyscale you go from "a wisp" to "a lit thing making a lot
            // of smoke".
            new Row { HovlKey = "Damage_Fire", Source = Fire + "MediumFlames.prefab",
                      Secondary = Smoke + "SmokeEffect.prefab",
                      Dest = DestDamage + "Damage_Fire.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 4, Required = false,
                      Scale = 1f, RateMul = 1f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      RequiredSystems = 1,
                      Why = "registry 6g: fire = MediumFlames + SmokeEffect (active flame + smoke volume)" },

            // THE CRITICAL-SAVE BEACON - the gap WO-892 exists to close. SparksEffect, 4
            // layers, root emits by rate on loop (measured). Gravity 0 so the glints HANG
            // instead of falling, which is what motion-splits it from Harvest_Gold (0.4,
            // "coin-shimmer that falls"); short lifetime so each pop is a hard blink rather
            // than a streak. The URGENCY itself is NOT in this prefab: it is the fast fixed
            // strobe StructureDamageVisuals drives through VfxLoopModulator, plus the "!"
            // glyph. Rhythm and a glyph both survive greyscale; a red tint does not.
            new Row { HovlKey = "Damage_CriticalBeacon", Source = Misc + "SparksEffect.prefab",
                      Dest = DestDamage + "Damage_CriticalBeacon.prefab", Expect = Family.Continuous,
                      MinQuality = 0, PoolSize = 3, Required = false,
                      Scale = 1f, RateMul = 0.5f, SpeedMul = 1f, LifeMul = 0.5f, Gravity = 0f,
                      RequiredSystems = 4,
                      Why = "registry 6g: CRITICAL-save beacon = SparksEffect fast-pulse (alarm cadence)" },

            // Broken (hp = 0), beat 1: the one-shot. DustExplosion, 5 layers including a
            // 30-count sand burst - GROUNDED debris, which is what a building coming down
            // reads as (a fire explosion would read as a bomb). BurstOnce because every
            // explosion recipe in this pack ships looping:1 with its payload in a t=0 burst
            // and VFXManager reclaims a oneshot at duration + max startLifetime, so the
            // burst would otherwise RE-FIRE mid-life and the building would collapse twice.
            new Row { HovlKey = "Damage_BreakBurst", Source = Fire + "DustExplosion.prefab",
                      Dest = DestDamage + "Damage_BreakBurst.prefab", Expect = Family.Burst,
                      MinQuality = 0, PoolSize = 3, Required = false,
                      Scale = 1.25f, RateMul = 1f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      RequiredSystems = 5, BurstOnce = true,
                      Why = "registry 6g: broken = DustExplosion one-shot (grounded structural collapse)" },

            // Broken, beat 2: the lingering ruin. WildFire is 3 layers (100 + 5 + 20/sec,
            // all looping - measured); at 35% density and a longer hold it is a LOW, WIDE,
            // slow guttering burn rather than a burning field. It takes the SAME loop slot
            // the fire tier held (a broken shell is tier 2, not tier 3), so a ruin costs
            // nothing extra against the cap.
            new Row { HovlKey = "Damage_Ruin", Source = Fire + "WildFire.prefab",
                      Dest = DestDamage + "Damage_Ruin.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 3, Required = false,
                      Scale = 0.8f, RateMul = 0.35f, SpeedMul = 0.8f, LifeMul = 1.2f, Gravity = None,
                      RequiredSystems = 3,
                      Why = "registry 6g: broken linger = WildFire/Smoke column over the ruin" },

            // == WO-893 PORTALS + SPAWN TIERS (registry section 7) =================
            // These five DO have landed VFXType values (Env_DungeonPortal, Portal_Enter,
            // Portal_Exit, Elite_Spawn, Boss_Spawn all predate the WO-884 batch), so they
            // are ordinary typed rows. Nothing is appended to the enum here.
            //
            // MOTION VECTOR IS THE ONLY THING SEPARATING THREE OF THEM, and that is
            // deliberate - it is the acceptance criterion ("Portal_Enter vs Portal_Exit
            // distinguishable by MOTION vector, not colour"). EnergyExplosion serves enter,
            // exit and elite-spawn from one recipe with three different motion signs:
            //   enter        speed x1.25            particles thrown OUTWARD  (consumed)
            //   exit         speed x-1.0            particles drawn INWARD    (materialised)
            //   elite spawn  gravity -0.30          particles RISE            (arriving)
            // All three read in greyscale because direction has no hue.

            // The portal mouth accent. MediumFlames, 1 layer, continuous. SECONDARY by
            // construction: it is small (0.55) and thinned (0.6), and PortalVFXController
            // holds it only while the hero is close, so it can never become the portal's
            // identity - the procedural vortex stays the portal. Kept low and tight because
            // the phone is landscape and a tall flame is the part that crops.
            new Row { TypeName = "Env_DungeonPortal", Source = Fire + "MediumFlames.prefab",
                      Dest = DestPortal + "Env_DungeonPortal.prefab", Expect = Family.Continuous,
                      MinQuality = 1, PoolSize = 3, Required = false,
                      Scale = 0.55f, RateMul = 0.6f, SpeedMul = 1f, LifeMul = 1f, Gravity = None,
                      RequiredSystems = 1,
                      Why = "registry 7: portal keeps its procedural vortex + a SECONDARY MediumFlames mouth accent" },

            // Portal_Enter - stepping in. Outward, pushed harder than the recipe ships so
            // the burst reads as the portal throwing the world outward around the hero.
            new Row { TypeName = "Portal_Enter", Source = Fire + "EnergyExplosion.prefab",
                      Dest = DestPortal + "Portal_Enter.prefab", Expect = Family.Burst,
                      MinQuality = 0, PoolSize = 3, Required = false,
                      Scale = 1.15f, RateMul = 1f, SpeedMul = 1.25f, LifeMul = 1f, Gravity = None,
                      RequiredSystems = 4, BurstOnce = true,
                      Why = "registry 7: Portal_Enter = EnergyExplosion (outward)" },

            // Portal_Exit - emerging. THE MIRROR: a NEGATIVE startSpeed multiplier makes
            // every layer's particles travel toward the emitter instead of away from it, so
            // the same recipe implodes. Held slightly shorter so the convergence lands as a
            // snap rather than a drift. This is the one row in the table whose tuning is a
            // SIGN rather than a magnitude, and it is why Scaled() had to learn to keep a
            // two-constants range in order when the multiplier is negative (see Scaled).
            new Row { TypeName = "Portal_Exit", Source = Fire + "EnergyExplosion.prefab",
                      Dest = DestPortal + "Portal_Exit.prefab", Expect = Family.Burst,
                      MinQuality = 0, PoolSize = 3, Required = false,
                      Scale = 1.15f, RateMul = 1f, SpeedMul = -1f, LifeMul = 0.85f, Gravity = None,
                      RequiredSystems = 4, BurstOnce = true,
                      Why = "registry 7: Portal_Exit = EnergyExplosion (inward, mirror of enter)" },

            // Elite_Spawn - a rung below the boss. Same 4-layer EnergyExplosion as the elite
            // DEATH (Elite_Death, scale 1.45), which is the point: an elite arriving and an
            // elite dying are the same magnitude of event. What separates them is the
            // NEGATIVE gravity - a spawn RISES into being, a death falls apart. The registry
            // also says "dark"; that is a hue and is deliberately not implemented, because a
            // read the owner cannot see is not a read.
            new Row { TypeName = "Elite_Spawn", Source = Fire + "EnergyExplosion.prefab",
                      Dest = DestPortal + "Elite_Spawn.prefab", Expect = Family.Burst,
                      MinQuality = 0, PoolSize = 3, Required = false,
                      Scale = 1.3f, RateMul = 1f, SpeedMul = 1f, LifeMul = 1.1f, Gravity = -0.3f,
                      RequiredSystems = 4, BurstOnce = true,
                      Why = "registry 7: Elite_Spawn = EnergyExplosion, rising (arrival) not falling (death)" },

            // Boss_Spawn - the scale jump. 8 layers pooled WHOLE, the same set piece as
            // Boss_Death, at 1.6 against the death's 1.8 so the arrival is unmistakably big
            // but the death still tops the ladder. Rises, for the same reason as the elite.
            // The registry's LightningStormCloud accent is REFUSED - see DeferredTypes.
            new Row { TypeName = "Boss_Spawn", Source = Fire + "BigExplosion.prefab",
                      Dest = DestPortal + "Boss_Spawn.prefab", Expect = Family.Burst,
                      MinQuality = 0, PoolSize = 2, Required = false,
                      Scale = 1.6f, RateMul = 1f, SpeedMul = 1f, LifeMul = 1f, Gravity = -0.25f,
                      RequiredSystems = 8, BurstOnce = true,
                      Why = "registry 7: Boss_Spawn = BigExplosion (8-layer scale jump), rising" },
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
            // OWNER BAN (2026-08-16, verbatim): "D:\EoA\Assets\Resources\VFX\Projectiles\
            // Spell_Fire_6.prefab - Do Not use anywhere". Paired with the owner tag
            // "BigExplosion.prefab (UnityTechnologies ParticlePack) -> Fire Spell impact",
            // Impact_Flame moved off the old Spells Pack detonation onto the ParticlePack
            // BigExplosion mirror (Assets/Resources/VFX/Status/BigExplosion.prefab). The
            // deferral string below no longer names the banned prefab because these strings
            // are CODE to BannedVfxRegression's lint - the verbatim ban lives in this comment.
            "consumer. The four impact moments that DO have enum values are already pointed at " +
            "deliberate, tracked, better picks - Impact_Flame at the ParticlePack BigExplosion " +
            "mirror (repointed 2026-08-16 off the banned Spells Pack fire detonation, see the " +
            "owner-ban comment above + BannedVfxRegression), Impact_ExplosionAether at Explosion_Arcane, " +
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

            // -- WO-889 (persistent combat auras) refusals. Each is a MEASUREMENT off the
            // real assets, and the first one is the most consequential: the WO's recipe
            // table would have REPLACED working art with thinner art. -------------------
            "WO-889 Aura_Ice / Aura_Flame / Aura_Necromancer / Aura_SmokeReaper REPOINTS: REFUSED. " +
            "Registry 6d nominates DustMotesEffect / TinyFlames / PoisonGas / SmokeEffect for these " +
            "four. MEASURED off both sides, every one of those swaps is a DOWNGRADE on three counts. " +
            "(1) LAYER COUNT. The incumbent Lana recipes are richer: Fog_frost 5 layers (root 25/sec " +
            "+ fog 15 + sparks 15 + snowflakes 3 + snow 7), Fire_medium 5, Fog_poison 6, Fog_speedSlow " +
            "6 - against DustMotesEffect 1, TinyFlames 1, PoisonGas 3, SmokeEffect 1. " +
            "(2) THE INCUMBENTS ARE ALREADY CORRECT LOOPS. All four derive CONTINUOUS through the " +
            "shared oracle, so there is no loop-flag defect to fix - unlike Aura_EnemyCaster, which " +
            "is why that one row IS built above. " +
            "(3) SELF-CONTAINMENT WOULD REGRESS. The Lana pack is GIT-TRACKED (verified: " +
            "'Assets/Lana Studio/Casual RPG VFX/Prefabs/Fog/Fog_frost.prefab' is in git ls-files and " +
            "is not gitignored), whereas every Particle Pack copy still resolves its materials and " +
            "textures into the GITIGNORED pack (this builder's own AuditPackDependencies reports the " +
            "count per row). Trading tracked art for untracked art is the WO-785 exposure moving " +
            "backwards. " +
            "SPECIFICALLY ON ICE, which WO-889 singles out for a COLD-MOTION criterion ('slow drift " +
            "and settle, NOT upward'): the incumbent Fog_frost literally ships dedicated 'snowflakes' " +
            "and 'snow' layers, while registry 6d itself marks the DustMotes pick with a standing " +
            "'snow-gap' warning because it is an APPROXIMATION of snow. The criterion is met better " +
            "by the art already in the catalog than by the art the WO proposed to replace it with. " +
            "Note also that these two VFXTypes are LIVE CONSUMER-FACING: GearAuraMap resolves the " +
            "hero's fire and frost weapon auras to Aura_Flame / Aura_Ice (shipped tonight under " +
            "WO-888), so a repoint would silently re-skin hero gear under a combat-aura work order. " +
            "A human must rule explicitly if the thinner pack look is wanted anyway.",

            "WO-889 Aura_EmpowerTower: REFUSED - it has NO CONSUMER, and wiring one would override " +
            "owner-tagged art. VERIFIED AT SOURCE: the tower aura is ArcaneAura.cs, and it does not " +
            "use VFXType at all - it holds a HOVL STRING KEY loop via VFXManager.PlayKey, with the " +
            "key assigned per surface by the owner ('Arcane_Aura' default, 'Aura_HeartPulse' for the " +
            "combat Arcane Spire, 'Fountain_Heal_Aura' for the Cathedral of Magic) and its own doc " +
            "calling these 'SWAPPABLE DEFAULTS - the owner may retag any surface in the VFX Caster'. " +
            "Routing the tower through a CLI-picked Aura_EmpowerTower would substitute a creative " +
            "pick for an owner tag, which the standing VFX rule forbids (map owner tags verbatim, " +
            "never pick or substitute). ArcaneAura ALSO already implements the WO's escalation " +
            "(ApplyLevel L1/L2/L3 grows the ring and adds an L3 idle pulse) and deliberately uses " +
            "ONE-SHOTS for that pulse so a wall of L3 towers cannot blow the loop cap - re-plumbing " +
            "it would trade a cap-safe design for a loop-per-tower one. The owner should tag a key " +
            "if a different tower aura is wanted.",

            "WO-889 Aura_HeartPulse repoint to FireFlies: REFUSED as out-of-scope-by-blast-radius. " +
            "Registry 6d scopes this to 'combat/raid Hearts ONLY (hub tree withholds)', and the " +
            "withholding is ALREADY IMPLEMENTED - HeartAuraController sets _suppressWhiteSwirl when " +
            "the Heart has a visible tree centrepiece, so the hub tree does not play it. But this " +
            "VFXType has THREE consumers, not one: HeartAuraController, EchoSpiritPresentation (the " +
            "founding-Echo aura, _auraType = Aura_HeartPulse) and ArcaneAura's combat-spire key " +
            "assignment. It also currently has NO VFXType catalog row, so PlayLoop resolves it " +
            "through the HOVL BRIDGE in VFXManager.PlayLoop (TryGetHovlKeyForType -> the curated " +
            "'Aura_HeartPulse' glow). Adding a VFXType row would BYPASS that bridge for every " +
            "consumer at once, silently re-skinning the founding-Echo aura - a felt change to the " +
            "onboarding moment under a combat-aura ticket. Needs an owner ruling, not a builder row.",

            "WO-889 Pet_Aura_Fire / Pet_Aura_Ice: REFUSED, and the whole question is now MOOT. " +
            "The original refusal was: no element selector existed, because PlayPetAura switched on " +
            "LEVEL only and PetAuraVFX (its only caller) passed a serialized 1-3 with no element " +
            "field. OWNER RULING 2026-08-20 - 'delete the pet aura system but keep Aura_PetLevel2' - " +
            "so PetAuraVFX, VFXManager.PlayPetAura and the Aura_PetLevel1/3 rungs are DELETED: the " +
            "pet aura feature never shipped and had zero runtime references. The L2 recipe survives " +
            "under its true name, Aura_TalentNode, because the TALENT TREE uses it. There is no pet " +
            "aura ladder left to add an element tier to.",

            "WO-889 Aura_Healer: not built - it already has a committed row (Lana " +
            "Regeneration/Regeneration_health_loop) that derives CONTINUOUS, and no code plays it " +
            "yet. Registry 6f introduces the healer STRUCTURE that would consume it; until that " +
            "structure exists, repointing a working row at a thinner RisingSteam copy would be " +
            "churn with no consumer to benefit.",

            "Death lingering loops (WO-886 'Lingering' column): SmokeEffect settle/column and " +
            "the WildFire lick MEASURE as genuine Family A loops (SmokeEffect 20/sec looping; " +
            "WildFire 100 + 5 + 20/sec looping across 3 layers), which is exactly what the WO " +
            "asks for - 'a SEPARATE capped loop', never folded into the burst. But there is NO " +
            "VFXType for a death linger, and appending to VFXType is Grok's single-owner edit " +
            "(WO-884 section 0.2 / handbook Step 3). Building the prefabs now would ship " +
            "Resources bytes with no consumer and no catalog row. Deferred pending the enum " +
            "values; the recipes are picked, measured and ready.",

            // -- WO-893 deferrals. The Enemy_Spawn / Despawn_Dissolve entries at the top of
            // this array were RE-MEASURED for WO-893 rather than inherited on trust, because
            // the two moments are that WO's headline. The numbers below are what the .prefab
            // files actually contain. -------------------------------------------------------
            "Enemy_Spawn / Despawn_Dissolve (WO-893, RE-MEASURED 2026-08-06 - the deferral " +
            "STANDS and here is the count). Misc/Respawn.prefab: 3 ParticleSystem layers " +
            "(Rings, Embers, Smoke) hanging under a ROOT that itself carries a MeshFilter, a " +
            "MeshRenderer and a MonoBehaviour whose script guid is " +
            "585901dad4c09564db67dc1e08787f0e - resolved, that is the PACK'S OWN " +
            "Misc Effects/Scripts/SpawnEffect.cs. Misc/Dissolve.prefab: 3 layers (Embers, " +
            "Flakes, Smoke) plus TWO demo meshes ('Dissolve' and 'Ball Dissolve', each a " +
            "MeshFilter + MeshRenderer) and the SAME script guid. A CopyAsset therefore ships " +
            "a prefab that (a) renders a lit demo primitive at every spawn point and (b) " +
            "carries a MISSING SCRIPT reference on any clone without the gitignored pack; and " +
            "the .cs cannot be mirrored because a second copy would compile into " +
            "Assembly-CSharp alongside the pack's own and take the compile gate down for every " +
            "parallel lane (VfxResourceArtMirror PASS 1 exists for exactly this class of " +
            "problem and STRIPS pack code rather than duplicating it). Both roots also fail the " +
            "root demo-geometry guard added by this WO, so the builder would now refuse them " +
            "even if a row were written. " +
            "WHAT THESE MOMENTS ACTUALLY NEED, stated so it is a task and not a mystery: a " +
            "runtime component that drives the TARGET'S OWN material cutoff - swap the enemy's " +
            "renderers to a dissolve-capable shader for the duration, ramp _cutoff 1->0 to " +
            "materialise and 0->1 to dissolve, then RESTORE the original shared materials " +
            "(pooled enemies make that restore mandatory - the material-level twin of the " +
            "pooled-instance contamination VfxLoopModulator closes for emission). That needs a " +
            "committed ShaderLab dissolve shader authored and felt-checked, which is AUTHORING " +
            "WORK, not a table row, and it is not attempted blind here. " +
            "WHAT WAS DONE INSTEAD, and it is the WO's actual acceptance criterion: the " +
            "standard enemy spawn NOW FIRES VFXType.Enemy_Spawn, which it never did - the " +
            "moment had no VFX call at all. With no catalogued prefab the type resolves through " +
            "VFXManager's SpawnHeuristicFallback (a procedural burst chosen off the enum name), " +
            "so the moment reads today and upgrades for free the day the dissolve component is " +
            "authored, with no call-site change. Nothing was faked and no recipe was " +
            "substituted for the ratified one.",

            "Boss_Spawn LIGHTNING ACCENT (WO-893 / registry 7): the ratified recipe is " +
            "'BigExplosion + LightningStormCloud accent'. The BigExplosion half IS shipped " +
            "(8 layers, whole). The accent is NOT, on the registry's own instruction: section 8 " +
            "item 8 rules lightning stays PROCEDURAL specifically to avoid taking a dependency " +
            "on the Legacy Particles folder, and the handbook (5.8) says the same. The file is " +
            "also named 'LightnigStormCloud.prefab' in the pack - a typo in the pack itself, " +
            "which is one more reason not to build a shipped row on it. A human must either " +
            "lift the Legacy ruling or accept the boss arriving without a lightning accent; the " +
            "scale jump (8 layers at 1.6 vs the elite's 4 at 1.3) carries the tier read on its " +
            "own, and it carries it in greyscale.",

            "Summon (necromancer / pet) (WO-893 / registry 7): 'Respawn cutoff + Area_generic " +
            "ground swell'. The Respawn half is the scripted recipe refused above, and there is " +
            "no VFXType for a summon, so a row here would be half a recipe with no consumer. " +
            "The Area_generic ground swell is already committed and catalogued as " +
            "Cast_NecromancerSummon (Lana Area_generic_green_outbreak), so the swell half of the " +
            "moment is not missing - only the materialise half, which waits on the same dissolve " +
            "component as Enemy_Spawn.",

            "Ember_Burn (WO-892, a DEAD ROW found while re-skinning the damage states - NOT " +
            "fixed here, because fixing it means PICKING art and the owner picks art). " +
            "HovlVfxCatalogGenerator.Map declares Ember_Burn as 'RPG VFX Bundle/Random effect " +
            "prefabs/Debuff 1.prefab'. That file DOES NOT EXIST - the folder ships 'Debuff " +
            "chain.prefab' and 'Debuff scythe.prefab' and no 'Debuff 1'. The generator skips a " +
            "row whose prefab will not load, so the key is ABSENT from the shipped " +
            "HovlVfxCatalog.asset (grep it). Two things consume that key and BOTH have been " +
            "silently dead: StructureDamageVisuals' smolder + fire loops (fixed by this WO, " +
            "which moves them onto the tracked Damage_* mirrors) and abilities.json " +
            "knight.emberbrand-throw's 'vfxResidual', which is still dead - the burning-brand " +
            "DoT shows no residual burn on the struck enemy. Re-pointing it is a one-line map " +
            "edit once someone with authority names the replacement prefab; substituting one " +
            "unilaterally is the exact move the owner-tag rule forbids.",

            // -- WO-890 / WO-891 findings. No rows were added by either; these record WHY,
            // with the numbers, so nobody re-attempts a CopyAsset that is not needed. -------
            "Harvest_Gold (WO-890): the prefab is BUILT, MEASURED and CATALOGUED (4 layers, " +
            "5 / 5 / 40 per sec, gravity 0.4, lifetime x0.6 - short glint pops that FALL, " +
            "correctly motion-split from Harvest_Crystal's suspended twinkle) and it has NO " +
            "POSSIBLE CONSUMER. VERIFIED AT SOURCE: there is no gold harvestable in this game. " +
            "MineResource is { Iron, Wood, Food, AetherCrystal } (MineNode.cs) and " +
            "HarvestResource is { Crystals, Food, Wood, Iron } (ResourceBuildingProgression.cs); " +
            "the only 'Gold' in the tree is a HUD display field (Core/HudModel/HudModels.cs) " +
            "and a buildingTier goldCost, neither of which is harvested from anything. So the " +
            "row stays (it costs nothing and is ready the day a gold node exists) and NO call " +
            "site selects it. Inventing a gold resource is economy DESIGN and belongs to the " +
            "owner - the same refusal shape as WO-887's absent surface taxonomy.",

            "Aura_Healer NOT repointed to RisingSteam (WO-891): registry 6f/6d ratifies " +
            "'RisingSteam (low/wide)' for the healer field, and this builder could ship it - " +
            "MEASURED, RisingSteam is 1 layer at 3/sec looping, i.e. CONTINUOUS, which MATCHES " +
            "the Family A beat, so unlike the WO-888 Cast_Heal / Impact_Heal cases there is no " +
            "family contradiction and no grounds to refuse. It was not done because there is " +
            "nothing missing to fix: Aura_Healer ALREADY resolves to a committed, tracked, " +
            "family-correct loop - Lana Regeneration/Regeneration_health_loop, MEASURED as 6 " +
            "layers with main.loop TRUE on every one and rateOverTime 15/25/7/1/5/5 - so " +
            "isLoop:true is the art's own answer and WO-891's beat is served today. Repointing " +
            "would be a TASTE change to a working row, and this builder's own WO-887 note names " +
            "that move ('a downgrade dressed as progress'). ONE THING FOR THE OWNER TO RULE ON: " +
            "the Lana regeneration loop is a green-family effect, so the healer field currently " +
            "reads partly by HUE, which the owner cannot see. WO-891 covers that on the channels " +
            "it owns - the per-tick CAST beat is pure TIMING and the contact flash is pure SHAPE, " +
            "both colour-free - but if the FIELD itself must be colour-free, RisingSteam is the " +
            "ratified swap and it is one row plus one Map line, already measured and ready.",

            "Env_DestructionDust is a STAND-IN, not an owner pick (WO-891 adjacent, the " +
            "structure per-hit flinch). The moment is real and the enum value is landed - its " +
            "own doc reads 'Destroyable object impact dust (barrel, crate, wall section)' - but " +
            "it had NO catalog row, so it fell through to VFXManager's unmapped-type default, a " +
            "generic Aoe NOVA, which is the wrong idea entirely for a wall being struck. It is " +
            "now mapped to the committed Lana Burst/Poof_generic (MEASURED: 5 layers, main.loop " +
            "FALSE and rateOverTime 0 on every one -> BURST, so isLoop:false is the art's " +
            "answer), which authors nothing new and points at no gitignored path. It is the " +
            "closest committed dust poof in the tree and NOT a tagged creative pick; the owner " +
            "re-points that one line the moment she tags a real structure-impact recipe. The " +
            "obvious pack candidate, DustExplosion, was deliberately NOT copied: it is 5 layers " +
            "with a 30-count and 500-grain burst, already shipped as the Death_Brute rung, and " +
            "firing that on the hottest path in a raid (every enemy contact on every wall) is a " +
            "perf decision, not a look decision, and not one to make blind.",
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
            var keyed   = new List<string>();   // WO-892: asset-only rows consumed by a Hovl string key
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
                        // WO-892: a KeyOnly row has no VFXType at all - its consumer is a
                        // VFXManager.PlayKey(STRING) call and its catalog row lives in
                        // HovlVfxCatalogGenerator.Map. Skip the enum lookup for those, but
                        // insist the row actually declares SOMETHING to be called by; a row
                        // with neither a TypeName nor a HovlKey is a table typo that would
                        // otherwise silently write a prefab nothing can ever play.
                        if (string.IsNullOrEmpty(row.Label))
                        {
                            rowError = "table row for '" + row.Dest + "' declares neither a TypeName " +
                                       "nor a HovlKey - nothing could ever play it. Fix the table.";
                            continue;
                        }
                        if (!row.KeyOnly && !Enum.IsDefined(enumType, row.TypeName))
                        {
                            string msg = "VFXType." + row.TypeName + " is not defined - the enum append " +
                                         "is Grok's single-owner edit (WO-884 section 0.2). Row skipped.";
                            if (row.Required) rowError = msg; else skipped.Add(msg);
                            continue;
                        }

                        var source = AssetDatabase.LoadAssetAtPath<GameObject>(row.Source);
                        if (source == null)
                        {
                            string msg = row.Label + ": source recipe MISSING at '" + row.Source +
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
                                Debug.LogWarning(Tag + row.Label + ": optional second layer '" +
                                                 row.Secondary + "' is missing - shipping the primary " +
                                                 "recipe alone rather than failing the row.");
                        }

                        var report = new StringBuilder();
                        GameObject dest = BuildOne(row, source, secondary, report);

                        bool isLoop;
                        if (!MeasureAndResolve(row, dest, report, out isLoop, out rowError))
                            continue;

                        // WO-892: a KeyOnly row writes NO VFXCatalog row - it has no VFXType
                        // to key one by. Its consumer row is authored in
                        // HovlVfxCatalogGenerator.Map against this same tracked prefab, and
                        // the measured IsLoop is logged here so that row can be written to
                        // agree. VfxLoopFlagRegression audits the Hovl catalog's stored
                        // IsLoop against the prefab, so the two still cannot drift apart -
                        // the check simply happens in the gate rather than in this loop.
                        if (row.KeyOnly)
                        {
                            keyed.Add(row.HovlKey + "(IsLoop=" + isLoop + ")");
                            report.Append("NO VFXCatalog row (KeyOnly): consumer is ")
                                  .Append("VFXManager.PlayKey(\"").Append(row.HovlKey)
                                  .Append("\") - author that key in HovlVfxCatalogGenerator.Map ")
                                  .Append("pointing at '").Append(row.Dest)
                                  .Append("' with IsLoop=").Append(isLoop).Append("; ");
                        }
                        else
                        {
                            WriteCatalogRow(enumType, entries, row, row.TypeName, dest, isLoop, report);
                        }

                        // WO-886 aliases: extra VFXType names that must resolve to the SAME
                        // prefab. Written from the one Row so a legacy alias cannot drift
                        // away from the value it aliases. An alias that is not in the enum
                        // is a warning, never a silent skip.
                        if (row.Aliases != null && !row.KeyOnly)
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

                        built.Add(row.Label + "(IsLoop=" + isLoop + ",MinQ=" + row.MinQuality + ")");
                        Debug.Log(Tag + row.Label + " -> " + row.Dest + " :: " + report);
                    }
                    catch (Exception rowEx)
                    {
                        rowError = row.Label + ": " + rowEx.Message;
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
                       .Append("asset-only row(s) consumed by a Hovl STRING KEY (author these in ")
                       .Append("HovlVfxCatalogGenerator.Map, no VFXType exists): ").Append(keyed.Count)
                       .Append(" [").Append(string.Join(", ", keyed.ToArray())).Append("]; ")
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

            // WO-892/893 ROOT DEMO-GEOMETRY GUARD - the WO-887 refusal, made mechanical.
            //
            // Five surface-impact recipes were refused on 2026-08-05 because their prefab
            // ROOT is a demo TARGET rather than an effect: a MeshFilter + MeshRenderer +
            // Collider with the particle tree parented underneath. Copying one ships an
            // effect that renders a lit primitive AND drops a physics collider at every
            // position it plays. Misc/Respawn and Misc/Dissolve are the same shape with a
            // pack MonoBehaviour on top, which additionally becomes a MISSING SCRIPT on any
            // clone without the gitignored pack.
            //
            // That was written down; a written rule is re-litigated, a check is not. This
            // asserts it on the SOURCE, before a byte is copied. It is ROOT-ONLY on purpose:
            // WO-889's StripGeometry legitimately removes demo scenery from CHILD subtrees,
            // and the two must not fight - a child can be repaired, a root cannot, because
            // the root IS the thing the pool instantiates. HasDemoGeometry is WO-889's
            // predicate, called rather than restated (two copies of one rule is how a tool
            // and its gate come to disagree while both report success).
            var rootExtras = source.GetComponents<MonoBehaviour>();
            if (HasDemoGeometry(source) || (rootExtras != null && rootExtras.Length > 0))
            {
                string mono = "none";
                if (rootExtras != null && rootExtras.Length > 0)
                {
                    var names = new List<string>();
                    foreach (var mb in rootExtras)
                        names.Add(mb == null ? "MISSING SCRIPT" : mb.GetType().Name);
                    mono = string.Join(" + ", names.ToArray());
                }
                throw new Exception("DEMO GEOMETRY ON THE ROOT: '" + row.Source + "' carries [" +
                                    DescribeComponents(source) + "] and script(s) [" + mono +
                                    "] on the prefab ROOT. That is a demo TARGET, not an effect - " +
                                    "a copy would render a lit primitive, add a physics collider at " +
                                    "every play position, and (for a pack MonoBehaviour) carry a " +
                                    "missing-script reference on any clone without the gitignored " +
                                    "pack. This is the WO-887 shape and the Misc/Respawn + " +
                                    "Misc/Dissolve shape: the recipe must be RE-PICKED or the effect " +
                                    "AUTHORED, never repaired by a CopyAsset.");
            }

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

            // WO-889 STRIP: work out UP FRONT how many transforms the demo-geometry strip
            // will remove, so the LAYER LOSS guard below still compares like with like. It
            // is measured on the SOURCE (which is never modified) rather than counted after
            // the fact, so the guard cannot be satisfied by a strip that removed the wrong
            // thing.
            int strippedNodes = 0;
            if (row.StripGeometry)
            {
                // The strip is only safe when the ROOT itself is the derivation authority.
                // If the root held no emitting ParticleSystem, VfxLoopFlagRegression.
                // PickAuthority would fall through into the children and removing a child
                // subtree could silently move which layer decides the family - the exact
                // trap that made WO-887's five surface impacts unshippable.
                var rootPs = source.GetComponent<ParticleSystem>();
                if (rootPs == null || !rootPs.emission.enabled)
                    throw new Exception("STRIP REFUSED: '" + row.Source + "' asks for a demo-geometry " +
                                        "strip, but its ROOT holds no emitting ParticleSystem, so the " +
                                        "loop-flag authority would fall through to a child and removing " +
                                        "scenery could move which layer decides the family. This is the " +
                                        "WO-887 demo-TARGET shape - refuse the recipe, do not strip it.");

                var doomed = new List<Transform>();
                CollectDemoGeometryRoots(source.transform, doomed);
                foreach (var t in doomed)
                    strippedNodes += t.GetComponentsInChildren<Transform>(true).Length;

                report.Append("stripGeometry: ").Append(doomed.Count)
                      .Append(" demo node(s) / ").Append(strippedNodes)
                      .Append(" transform(s) marked [");
                for (int i = 0; i < doomed.Count; i++)
                {
                    if (i > 0) report.Append(" + ");
                    report.Append(doomed[i].name);
                }
                report.Append("]; ");
            }

            int expectDescendants = srcDescendants - strippedNodes;
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

                // -- WO-889 DEMO-GEOMETRY STRIP. Applied EVERY run, like playOnAwake,
                // because a lit primitive and a physics collider riding along with an aura
                // are a correctness problem, not a taste call: this effect plays parented to
                // an ENEMY, so a stray MeshCollider would push the body it is attached to.
                // Only subtrees containing NO ParticleSystem are eligible, so the
                // never-flatten law (handbook 1.2) cannot be violated by construction.
                if (row.StripGeometry)
                {
                    var doomed = new List<Transform>();
                    CollectDemoGeometryRoots(contents.transform, doomed);
                    int removed = 0;
                    foreach (var t in doomed)
                    {
                        if (t == null) continue;
                        removed += t.GetComponentsInChildren<Transform>(true).Length;
                        report.Append("stripped demo node '").Append(t.name).Append("' (")
                              .Append(DescribeComponents(t.gameObject)).Append("); ");
                        UnityEngine.Object.DestroyImmediate(t.gameObject);
                    }
                    if (removed > 0) dirty = true;
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
                error = row.Label + ": '" + row.Dest + "' has NO ParticleSystem at all - " +
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

                Debug.Log(Tag + row.Label + " layer '" + ps.gameObject.name +
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
                    row.Label, dest, out isLoop, out detail))
            {
                error = row.Label + ": the shared loop-flag derivation could not read this " +
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
                error = row.Label + ": FAMILY MISMATCH. The enum doc claims a CONTINUOUS loop, " +
                        "but the art reads as a BURST (" + detail + "; any layer emitting by rate: " +
                        anyRate + "). Refusing to author an IsLoop=false row under a loop-shaped " +
                        "contract, and refusing to force IsLoop=true on a self-terminating prefab - " +
                        "that leaks a loop slot forever. A human must re-pick the recipe or correct " +
                        "the doc.";
                return false;
            }
            if (row.Expect == Family.Burst && isLoop)
            {
                error = row.Label + ": FAMILY MISMATCH. The enum doc claims a one-shot BURST, but " +
                        "the art reads as CONTINUOUS (" + detail + "). Cataloguing it as a loop would " +
                        "hand out a VFXHandle nothing stops and burn one of the 20 loop slots; " +
                        "cataloguing it as a one-shot would reclaim a still-emitting system. A human " +
                        "must re-pick the recipe or correct the doc.";
                return false;
            }
            if (row.Expect == Family.Either)
            {
                report.Append("(enum doc permits EITHER family - measured value recorded, not judged); ");
                Debug.Log(Tag + row.Label + ": the enum doc permits either family; the art measures " +
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
                Debug.LogWarning(Tag + row.Label + ": the prefab is committed, but " +
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

        /// <summary>
        /// Scales a MinMaxCurve in whatever mode it was authored in.
        /// <para>
        /// WO-893 made the multiplier able to be NEGATIVE. Portal_Exit is the mirror of
        /// Portal_Enter, and the ONLY colour-free way to mirror a burst is to reverse its
        /// motion vector - a negative startSpeed multiplier turns an explosion into an
        /// implosion. That works directly for the two single-value modes, but negating a
        /// TWO-CONSTANTS range also SWAPS which end is the minimum: (2, 6) negated is
        /// (-2, -6), whose min is -6. Unity samples that range with Random.Range and a
        /// reversed pair is not a contract worth relying on, so the ends are put back in
        /// order here. The multiplier stays a single knob and the caller never has to know.
        /// </para>
        /// </summary>
        private static ParticleSystem.MinMaxCurve Scaled(ParticleSystem.MinMaxCurve c, float k)
        {
            switch (c.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return new ParticleSystem.MinMaxCurve(c.constant * k);
                case ParticleSystemCurveMode.TwoConstants:
                    if (k < 0f)
                        return new ParticleSystem.MinMaxCurve(c.constantMax * k, c.constantMin * k);
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

        // =====================================================================
        //  WO-889: demo-geometry detection
        // =====================================================================

        /// <summary>
        /// Collect the SHALLOWEST child subtrees that are pure demo scenery: they contain
        /// no ParticleSystem anywhere beneath them AND carry renderable geometry or a
        /// collider. Recursion stops at each hit, so a returned list never contains a node
        /// nested inside another returned node (which would double-count the strip and
        /// break the LAYER LOSS arithmetic).
        ///
        /// A subtree holding ANY ParticleSystem is never eligible - that is what keeps
        /// this from becoming a flatten. The root is never eligible; a recipe whose ROOT is
        /// demo geometry is the WO-887 shape and must be refused outright, not repaired.
        /// </summary>
        private static void CollectDemoGeometryRoots(Transform node, List<Transform> into)
        {
            if (node == null || into == null) return;
            for (int i = 0; i < node.childCount; i++)
            {
                var c = node.GetChild(i);
                if (c == null) continue;

                if (c.GetComponentsInChildren<ParticleSystem>(true).Length > 0)
                {
                    CollectDemoGeometryRoots(c, into);   // real layers live below - keep looking
                    continue;
                }

                if (HasDemoGeometry(c.gameObject)) { into.Add(c); continue; }

                CollectDemoGeometryRoots(c, into);
            }
        }

        /// <summary>True when <paramref name="go"/> carries mesh geometry or a collider -
        /// the three components that made WO-887's five surface recipes unshippable.</summary>
        private static bool HasDemoGeometry(GameObject go)
        {
            if (go == null) return false;
            if (go.GetComponent<MeshFilter>()   != null) return true;
            if (go.GetComponent<MeshRenderer>() != null) return true;
            if (go.GetComponent<Collider>()     != null) return true;
            return false;
        }

        /// <summary>Names the geometry components on a node, so the run report says WHAT was
        /// removed rather than only that something was.</summary>
        private static string DescribeComponents(GameObject go)
        {
            var parts = new List<string>();
            if (go.GetComponent<MeshFilter>()   != null) parts.Add("MeshFilter");
            if (go.GetComponent<MeshRenderer>() != null) parts.Add("MeshRenderer");
            var col = go.GetComponent<Collider>();
            if (col != null) parts.Add(col.GetType().Name);
            return parts.Count > 0 ? string.Join(" + ", parts.ToArray()) : "no geometry";
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
