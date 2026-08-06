// =============================================================================
// VFXCatalogGenerator (WO-504 slice 1) - SCRIPT-authors the VFXCatalog asset that
// wires the AUTHORED, GIT-COMMITTED VFX prefabs (Lana Studio + Spells Pack + the
// custom Resources projectiles) onto the VFXType enum, so combat plays the pro
// VFX we own instead of the procedural AbilityVfxKit fallback.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor   (editor-only)
//
// WHY THIS EXISTS (the finding, WO-504):
//   VFXManager resolves a VFXType -> prefab through a ScriptableObject VFXCatalog
//   (serialized Entry[] of {Type, Prefab, PoolSize, IsLoop, MinQuality}). NO
//   VFXCatalog asset existed, and VFXManager is not placed in any scene/prefab -
//   so _catalog was always null and EVERY effect fell back to procedural. This
//   generator CREATES the asset and populates it from curated prefab paths. A
//   companion runtime change (VFXManager auto-loads Resources/VFX/VFXCatalog when
//   _catalog is null) makes the wiring take effect with no inspector drag-drop.
//
// THE RULE (WO-504, honoured here):
//   * ONLY git-committed packs are referenced: Assets/Lana Studio/Casual RPG VFX,
//     Assets/Spells Pack/Particles/Prefabs, Assets/Resources/VFX/Projectiles.
//     NOTHING under Assets/Mirza Beig/** (gitignored, absent on clone).
//   * CURATED - "one soldier, not the brigade": ONE best prefab per wired VFXType.
//     Unity strips unreferenced assets from the build, so the rest stay benched on
//     disk. The asset is the ONLY new thing in Resources - no whole pack is dumped
//     into a Resources folder (build-size guard).
//   * Authored by SCRIPT (this generator), never inspector drag-drop (owner canon).
//   * Any VFXType NOT wired here keeps the procedural AbilityVfxKit fallback.
//
// WHY REFLECTION / SerializedObject:
//   CORRECTED 2026-08-05: this block used to claim "DeNelle.Editor.asmdef does NOT
//   reference DeNelle.Village". It DOES - DeNelle.Village sits in the references array
//   of Assets/Editor/DeNelle.Editor.asmdef, and has for some time. So the reflection
//   here is BELT-AND-BRACES, not a necessity, and nobody should contort a new editor
//   script to avoid a dependency that already exists.
//   It still earns its keep for one real reason: resolving VFXType BY NAME means a type
//   named in the Map but absent from the enum degrades to a skipped row, rather than
//   fail-compiling the whole DeNelle.Editor assembly and taking the compile gate down
//   for every parallel lane. Keep the by-name resolution; drop the false premise.
//
// THE PICKS ARE BONES: the exact prefab per type is the owner's to felt-tune. To
// re-point any mapping, edit the Map table below and re-run. Idempotent.
//
// RUN:
//   Editor menu : Defenders/VFX/Generate VFX Catalog
//   Batchmode   : DeNelle.Editor.VFXCatalogGenerator.Generate
//   Prints marker: VFX_CATALOG_OK on success.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor generator that builds Resources/VFX/VFXCatalog.asset mapping VFXType
    /// to curated git-committed prefabs. Reflection + SerializedObject so it never
    /// compile-depends on DeNelle.Village. Idempotent; prints VFX_CATALOG_OK.
    /// </summary>
    public static class VFXCatalogGenerator
    {
        private const string Marker     = "VFX_CATALOG_OK";
        private const string AssetDir   = "Assets/Resources/VFX";
        private const string AssetPath  = "Assets/Resources/VFX/VFXCatalog.asset";

        private const string CatalogTypeName = "DeNelle.Village.VFXCatalog, DeNelle.Village";
        private const string VfxTypeEnumName  = "DeNelle.Village.VFXType, DeNelle.Village";

        // -- Curated map: VFXType enum name -> {prefab asset path, isLoop, minQuality} --
        // ONE best prefab per wired type, git-committed packs only. Owner re-points
        // any line here and re-runs. Types absent from this table use procedural.
        private struct Pick
        {
            public string Path;
            public bool   IsLoop;
            public int    MinQuality;   // 0 always, 1 skip-Low, 2 High-only
            public int    PoolSize;
            public Pick(string path, bool isLoop = false, int minQuality = 0, int poolSize = 4)
            { Path = path; IsLoop = isLoop; MinQuality = minQuality; PoolSize = poolSize; }
        }

        private const string Lana   = "Assets/Lana Studio/Casual RPG VFX/Prefabs/";
        private const string Spells = "Assets/Spells Pack/Particles/Prefabs/";
        private const string Res    = "Assets/Resources/VFX/Projectiles/";
        private const string BossVfx = "Assets/Resources/VFX/Boss/";

        // The curated table. Keep it minimal - high-traffic battle types first.
        private static readonly Dictionary<string, Pick> Map = new Dictionary<string, Pick>
        {
            // -- Impacts (oneshot hits) ----------------------------------------
            // Battle-polish: the melee hit is the highest-traffic combat moment.
            // Upgrade from the small Hit_stone spark to a readable SLASH ARC so every
            // sword connect reads as a strike, not a pebble poof. Still a cheap oneshot.
            { "Impact_Physical",        new Pick(Lana + "Slash/Slash_stone_once.prefab") },
            // FIREBALL headline (overnight build): the mage/hero fireball's regular fire-impact
            // burst is the rich Spells Pack Spell_Fire_6 (a fiery detonation) instead of the small
            // Lana Hit_fire spark, so a fireball reads as a real fireball. URP-proofed at load by
            // VFXManager.ProofUrpParticleShaders (memory: half-upgraded URP pack = pixelated).
            { "Impact_Flame",           new Pick(Res + "Spell_Fire_6.prefab") },
            { "Impact_Ice",             new Pick(Lana + "Range_attack/Hit_frost.prefab") },
            { "Impact_Aether",          new Pick(Lana + "Range_attack/Hit_magic.prefab") },
            { "Impact_Heal",            new Pick(Lana + "Range_attack/Hit_heart.prefab") },
            { "Impact_ExplosionFire",   new Pick(Spells + "Projectiles/Explosion/Explosion_Fire.prefab") },
            { "Impact_ExplosionAether", new Pick(Spells + "Projectiles/Explosion/Explosion_Arcane.prefab") },
            { "Impact_ShockwaveRing",   new Pick(Lana + "Burst/Burst_rings.prefab") },
            { "Impact_ShardsBurst",     new Pick(Lana + "Burst/Burst_sharp.prefab") },
            { "Impact_SmokeWisps",      new Pick(Lana + "Burst/Poof_generic.prefab") },

            // -- Projectiles (custom WebGL-safe Resources bodies; loop until hit) -
            { "Projectile_ArcaneBolt",  new Pick(Res + "Projectile_Arcane.prefab", isLoop: true) },
            { "Projectile_FrostBolt",   new Pick(Res + "Projectile_Ice.prefab",    isLoop: true) },
            { "Projectile_Arrow",       new Pick(Lana + "Range_attack/Projectiles_green_shuriken.prefab", isLoop: true) },
            // FIREBALL travel beat (overnight build): the fire projectile / fireball body is the
            // Spells Pack Projectile_Fire_3 (a proper flaming bolt with trail) instead of the small
            // custom Res orb, so the fireball is visible streaking to its target. Loop until impact;
            // SpellVfxFactory maps a Fire spell's projectile -> Projectile_FlameArrow.
            { "Projectile_FlameArrow",  new Pick(Res + "Projectile_Fire_3.prefab", isLoop: true) },
            { "Projectile_EnemyCasterBolt", new Pick(Lana + "Range_attack/Projectiles_dark_magic.prefab", isLoop: true) },

            // -- Casts (wind-up on caster) -------------------------------------
            // BATTLE-POLISH (owner: "better spell effects on casting overall"):
            //   The cast/wind-up is now a READABLE "gathering energy" charge moment
            //   on the caster, using the Lana Orbs/Flash/Area families (orbiting
            //   particles that converge = a clear charge) instead of a faint flash.
            //   CRITICAL fresh-clone fix: the prior picks pointed at the Spells Pack
            //   (Casting_*), which is GITIGNORED + NOT git-tracked - so on a clean
            //   clone every hero cast silently fell back to procedural (the weak look
            //   the owner is seeing). EVERY pick below is a GIT-COMMITTED Lana prefab,
            //   so the impressive cast survives a fresh checkout. Element-coded +
            //   cheap oneshots (loops are scaled down via the Orbs' own short life).
            { "Cast_MageCharge",        new Pick(Lana + "Orbs/Orbs_electric.prefab") },               // arcane violet gather
            // FIREBALL cast windup (overnight build): the fire-charge wind-up is the Spells Pack
            // Casting_Fire (gathering embers at the caster's hand) so the fireball reads as a real
            // charge -> release. Fires on the caster in sync with the Cast animation trigger
            // (Combat_Spell_Fireball) via HeroAbilities.CastResolved -> SpawnVfx -> SpellVfxFactory
            // (Fire spell resolves cast -> Cast_FireCharge). URP-proofed at load.
            { "Cast_FireCharge",        new Pick(Res + "Casting_Fire.prefab") }, // ember gather (Meteor/Radiant/Fireball)
            { "Cast_KnightSlam",        new Pick(Lana + "Burst/Flash_dubble_circle.prefab") },        // bigger double-ring cast pulse
            { "Cast_RangerDraw",        new Pick(Lana + "Orbs/Orbs_leaves.prefab") },                 // nature-green gather at the bow
            // SOFT HEAL (overnight build): the heal ability (Healing Beacon / Mending) fires ONLY
            // VFXType.Cast_Heal on the caster (HeroAbilities Heal branch -> VFXManager.Play(Cast_Heal)).
            // Wire it to the Spells Pack Buff_Nature — a calm, gentle rising green restoration glow
            // (soft, not flashy). Reads as "heal" via shape + rising motion + the heal number, not
            // colour alone (owner is red/green colourblind). URP-proofed at load.
            { "Cast_Heal",              new Pick(Spells + "Buffs/Buff_Nature.prefab") },              // soft green restoration glow (heal)
            { "Cast_FrostNova",         new Pick(Lana + "Area_generic/Area_generic_blue_outbreak.prefab") }, // spreading frost ground ring
            { "Cast_NecromancerSummon", new Pick(Lana + "Area_generic/Area_generic_green_outbreak.prefab") }, // dark/poison summon swell
            { "Cast_EnemyCaster",       new Pick(Lana + "Orbs/Orbs_electric.prefab") },               // enemy caster violet swell

            // -- Deaths (oneshot burst) ----------------------------------------
            { "Death_Skeleton",         new Pick(Lana + "Burst/Poof_generic.prefab") },
            { "Death_Boss",             new Pick(Spells + "Projectiles/Explosion/Explosion_Dark.prefab") },
            { "Death_Brute",            new Pick(Lana + "Burst/Poof_electric.prefab") },
            { "Death_Wolf",             new Pick(Lana + "Burst/Poof_water.prefab") },
            { "Death_Tiefling",         new Pick(Spells + "Projectiles/Explosion/Explosion_Fire_2.prefab") },
            { "Death_Generic",          new Pick(Lana + "Burst/Poof_generic.prefab") },
            // Battle-polish: dungeon enemy death fires this live but had no wired prefab
            // (procedural only). A darker owned Dark explosion reads bigger than the
            // village Poof for the dungeon run. Owned, cheap oneshot.
            { "Death_EnemyExplosion_Dungeon", new Pick(Spells + "Projectiles/Explosion/Explosion_Dark_2.prefab") },

            // -- Auras (persistent loops) --------------------------------------
            { "Aura_Flame",             new Pick(Lana + "Fire/Fire_medium.prefab",  isLoop: true, minQuality: 1) },
            { "Aura_Ice",               new Pick(Lana + "Fog/Fog_frost.prefab",     isLoop: true, minQuality: 1) },
            { "Aura_Healer",            new Pick(Lana + "Regeneration/Regeneration_health_loop.prefab", isLoop: true, minQuality: 1) },
            { "Aura_EnemyCaster",       new Pick(Lana + "Orbs/Orbs_electric.prefab", isLoop: true, minQuality: 1) },
            { "Aura_Necromancer",       new Pick(Lana + "Fog/Fog_poison.prefab",     isLoop: true, minQuality: 1) },
            { "Aura_SmokeReaper",       new Pick(Lana + "Fog/Fog_speedSlow.prefab",  isLoop: true, minQuality: 1) },

            // -- Boss (Particle Pack, WO-759) -----------------------------------
            // The dragon's sustained mouth-cone breath. Its prefab is a TRACKED
            // DUPLICATE under Assets/Resources/VFX/Boss/ - authored by
            // BossFireBreathBuilder out of the gitignored UnityTechnologies pack,
            // precisely so this row does not join the 117 catalog rows that point
            // into gitignored art with no runtime fallback (WO-785).
            //
            // THIS ROW MUST LIVE HERE, not only in the builder. Build() does
            // 'entries.arraySize = rows.Count' (see below) - it rebuilds Entries[]
            // wholesale from this table, so any row written ONLY by the builder is
            // silently dropped the next time someone runs Generate, and the boss
            // falls back to a procedural loop that still LOOKS like a working
            // breath. Curated table = the durable home; the builder is idempotent
            // and re-runnable against it.
            { "Boss_FireBreath",        new Pick(BossVfx + "Boss_FireBreath.prefab", isLoop: true, minQuality: 1, poolSize: 2) },

            // -- Environment ---------------------------------------------------
            { "Env_TorchFlame",         new Pick(Lana + "Fire/Fire_small.prefab",   isLoop: true, minQuality: 1) },

            // -- Juice / Feedback ----------------------------------------------
            { "Juice_CriticalHit",      new Pick(Lana + "Burst/Flash_star.prefab") },
            { "Juice_KillStreak",       new Pick(Lana + "Burst/Burst_rainbow_mist.prefab") },
            { "Juice_WaveClear",        new Pick(Lana + "States/Level_up.prefab") },
            { "WaveClear_Celebration",  new Pick(Lana + "States/Level_up.prefab") },
            { "Juice_LevelUp",          new Pick(Lana + "States/Level_up.prefab") },
            { "LevelUp_Celebration",    new Pick(Lana + "States/Level_up.prefab") },
            { "Combo_Tier1",            new Pick(Lana + "Burst/Flash_circle.prefab") },
            { "Combo_Tier2",            new Pick(Lana + "Burst/Flash_dubble_circle.prefab") },
        };

        // -- Menu / batch entry ------------------------------------------------

        [MenuItem("Defenders/VFX/Generate VFX Catalog")]
        public static void Generate()
        {
            try
            {
                int wired = Build();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[VFXCatalogGenerator] Wired {wired} VFXType entries into {AssetPath}.");
                Debug.Log(Marker);
            }
            catch (Exception e)
            {
                Debug.LogError($"[VFXCatalogGenerator] FAILED: {e.Message}\n{e.StackTrace}");
                // No marker on failure - the gate withholds VFX_CATALOG_OK.
            }
        }

        // -- Core build --------------------------------------------------------

        private static int Build()
        {
            var catalogType = Type.GetType(CatalogTypeName);
            if (catalogType == null)
                throw new Exception($"Could not resolve type '{CatalogTypeName}'. Is DeNelle.Village compiled?");

            var enumType = Type.GetType(VfxTypeEnumName);
            if (enumType == null)
                throw new Exception($"Could not resolve enum '{VfxTypeEnumName}'.");

            EnsureDir(AssetDir);

            // Load or create the catalog ScriptableObject.
            var catalog = AssetDatabase.LoadAssetAtPath(AssetPath, catalogType) as ScriptableObject;
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance(catalogType);
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }

            // Build the resolved (enumValue, prefab, pick) rows from the curated map.
            var rows = new List<(int enumValue, GameObject prefab, Pick pick, string typeName)>();
            int skippedMissing = 0;
            foreach (var kv in Map)
            {
                string typeName = kv.Key;
                if (!Enum.IsDefined(enumType, typeName))
                {
                    Debug.LogWarning($"[VFXCatalogGenerator] VFXType.{typeName} not defined - skipping.");
                    continue;
                }
                int enumValue = (int)Enum.Parse(enumType, typeName);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(kv.Value.Path);
                if (prefab == null)
                {
                    // Missing prefab (e.g. pack not imported on this machine). Skip - the
                    // type then keeps its procedural fallback. Never hard-fail the gate on
                    // an absent OPTIONAL pack prefab.
                    Debug.LogWarning($"[VFXCatalogGenerator] prefab missing for {typeName}: '{kv.Value.Path}' " +
                                     "- type stays procedural.");
                    skippedMissing++;
                    continue;
                }
                rows.Add((enumValue, prefab, kv.Value, typeName));
            }

            // Write Entries[] via SerializedObject (no compile-time Village dependency).
            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("Entries");
            if (entries == null)
                throw new Exception("VFXCatalog has no serialized 'Entries' array property.");

            entries.arraySize = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                // typeName is no longer discarded - the IsLoop derivation below names the
                // offending row when the Map literal and the prefab disagree.
                var (enumValue, prefab, pick, typeName) = rows[i];
                var e = entries.GetArrayElementAtIndex(i);

                var pType    = e.FindPropertyRelative("Type");
                var pPrefab  = e.FindPropertyRelative("Prefab");
                var pPool    = e.FindPropertyRelative("PoolSize");
                var pLoop    = e.FindPropertyRelative("IsLoop");
                var pMinQ    = e.FindPropertyRelative("MinQuality");
                var pLife    = e.FindPropertyRelative("LifetimeOverride");

                if (pType   != null) pType.enumValueIndex      = EnumIndexFor(enumType, enumValue);
                if (pPrefab != null) pPrefab.objectReferenceValue = prefab;
                if (pPool   != null) pPool.intValue            = pick.PoolSize;

                // IsLoop is DERIVED FROM THE PREFAB, never from the Map literal - see the
                // long note in HovlVfxCatalogGenerator for the P0 this closes. Three entries
                // here contradict their own art (Projectile_Arrow, Projectile_EnemyCasterBolt,
                // Aura_EnemyCaster are all rate-0 + single burst but declared isLoop: true).
                //
                // The derivation is NOT simply "read the root". Lana's Fire_medium.prefab has
                // a root ParticleSystem with its emission module DISABLED sitting over a child
                // that emits 15/sec on loop; strict root-reading would call it a one-shot and
                // cut Aura_Flame, Env_TorchFlame, Aura_Necromancer and Aura_SmokeReaper off
                // mid-burn. TryDerive falls through a disabled shell to the first system that
                // can actually emit, which is why those four correctly stay loops.
                if (pLoop != null)
                {
                    bool derived;
                    string detail;
                    if (DeNelle.Editor.Regression.VfxLoopFlagRegression.TryResolveExpected(typeName, prefab, out derived, out detail))
                    {
                        if (derived != pick.IsLoop)
                            Debug.LogWarning($"[VFXCatalogGenerator] '{typeName}' Map says isLoop:{pick.IsLoop} " +
                                             $"but the prefab derives {derived} - using the PREFAB. {detail}");
                        pLoop.boolValue = derived;
                    }
                    else
                    {
                        Debug.LogWarning($"[VFXCatalogGenerator] '{typeName}' could not be derived ({detail}) " +
                                         $"- falling back to the Map literal isLoop:{pick.IsLoop}.");
                        pLoop.boolValue = pick.IsLoop;
                    }
                }
                if (pMinQ   != null) pMinQ.intValue            = pick.MinQuality;
                if (pLife   != null) pLife.floatValue          = 0f;   // auto-detect
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);

            Debug.Log($"[VFXCatalogGenerator] {rows.Count} wired, {skippedMissing} skipped (missing prefab).");
            return rows.Count;
        }

        // SerializedProperty.enumValueIndex is the ORDINAL position in the enum's
        // value list, not the underlying int. Map the underlying value back to its
        // ordinal so the catalog stores the right VFXType.
        private static int EnumIndexFor(Type enumType, int underlyingValue)
        {
            var values = Enum.GetValues(enumType);
            for (int i = 0; i < values.Length; i++)
                if ((int)values.GetValue(i) == underlyingValue) return i;
            return 0;
        }

        private static void EnsureDir(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            // Create parents as needed (Assets/Resources, then Assets/Resources/VFX).
            var parts = dir.Split('/');
            string cur = parts[0];   // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
