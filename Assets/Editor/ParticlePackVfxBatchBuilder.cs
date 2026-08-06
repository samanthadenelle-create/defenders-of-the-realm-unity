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
        };

        // The two values this builder deliberately leaves alone, with the reason the
        // run report repeats verbatim so nobody re-attempts them as a CopyAsset.
        private static readonly string[] DeferredTypes =
        {
            "Enemy_Spawn: Misc/Respawn is a SCRIPTED recipe (pack MonoBehaviour " +
            "SpawnEffect.cs + a demo mesh). A copy would carry a missing script on any " +
            "clone without the gitignored pack and would render a demo mesh. Needs a " +
            "runtime component driving the TARGET's material cutoff - authoring, not a copy.",

            "Despawn_Dissolve: Misc/Dissolve, same shape (pack script + 2 demo meshes). " +
            "Same reason, same remedy.",
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

                        WriteCatalogRow(enumType, entries, row, dest, isLoop, report);
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
                       .Append("deferred ").Append(DeferredTypes.Length)
                       .Append(" (Enemy_Spawn, Despawn_Dissolve - scripted recipes, see log); ")
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

        private static void WriteCatalogRow(Type enumType, SerializedProperty entries, Row row,
                                            GameObject prefab, bool isLoop, StringBuilder report)
        {
            int enumValue   = (int)Enum.Parse(enumType, row.TypeName);
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

            report.Append("catalog row ").Append(appended ? "APPENDED" : "UPDATED")
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
