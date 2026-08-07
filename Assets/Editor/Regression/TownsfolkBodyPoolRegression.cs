// =============================================================================
// TownsfolkBodyPoolRegression [townsfolk-bodies] — pins the castle-hub townsfolk
// BODY contract end to end: pool -> prefab -> renderer -> material -> texture.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Markers: TOWNSFOLK_BODIES_OK / _FAIL.
//
// Owner ruling 2026-08-07: "REPLACE the town's villager bodies with the 14
// CraftPix medieval people" — until then the WHOLE town wandered on Mevina + Tob,
// two faces for every villager in Elarion.
//
// WHY THIS SUITE EXISTS, CASE BY CASE. Each one guards a failure that is SILENT —
// no exception, no red line, just a town that looks wrong:
//
//   1. [pool]        CastleTownsfolkInjector.BodyPool is a private string[] of
//                    Resources paths. A typo in one entry does not fail to
//                    compile; it fails at runtime as "missing Resources/... -
//                    placeholder villager used" and the owner sees a grey capsule
//                    standing in her town. The pool is also compared against the
//                    prefabs ACTUALLY on disk, because the other half of that bug
//                    is a prefab that was never built.
//
//   2. [resolve]     Every entry must come back from Resources.Load. The injector
//                    calls exactly that and falls back to a capsule on null.
//
//   3. [renderers]   Every prefab must satisfy the injector's own VerifyRenders
//                    contract (>=1 enabled Renderer with a real mesh) or the body
//                    is DESTROYED at spawn and replaced by the capsule.
//
//   4. [materials]   THE CASE THAT EARNS ITS KEEP. This repo has repeatedly shipped
//                    URP-but-UNTEXTURED meshes that render flat grey/dark and passed
//                    a shader-only check — the 2026-08-05 VFX self-containment pass
//                    found 73 such dependencies, and WO-719's white arcane spire and
//                    the white Knight were the same class. So a slot is only green
//                    when ALL THREE hold: material non-null (a null slot renders
//                    magenta), shader is URP (a Built-in shader renders wrong under
//                    URP), AND an albedo texture is actually BOUND (URP + no texture
//                    is the grey body). Checking the shader alone is what let that
//                    ship before.
//
//   5. [shared-mat]  The pack has ONE 64x64 atlas for all 14 bodies, so there is ONE
//                    material. If a future edit splits it into 14, this says so.
//
//   6. [legacy]      NPC_Peasant_Mevina / NPC_Peasant_Tob must STILL resolve. They
//                    left THIS pool but seven other call sites still load them
//                    (CastleVendorNpcInjector, CastleCompanionIntroducerInjector,
//                    SylasStewardInjector, QuestCastNpcInjector, VillageNpcInjector,
//                    TorchWardenDress). Deleting them while "replacing the villager
//                    bodies" would blank the vendors and the quest cast.
//
// REFLECTION NOTE: BodyPool is private, and this suite reads it by reflection so it
// pins the REAL runtime array rather than a regex over source text that could drift
// from what actually compiles. CLAUDE.md section 10 bans new System.Reflection in
// BRIDGE scripts (the runtime cross-module glue); this is an editor-only oracle that
// ships in no player build.
//
// Standalone: run-unity-method.ps1
//   -Method DeNelle.Editor.Regression.TownsfolkBodyPoolRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeNelle.Village;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class TownsfolkBodyPoolRegression
    {
        /// <summary>Where CraftPixPeopleBuilder writes the loadable bodies.</summary>
        private const string PrefabDir = "Assets/Resources/NPCs/CraftPixPeople";

        /// <summary>The Resources.Load prefix every pool entry must carry.</summary>
        private const string ResourcePrefix = "NPCs/CraftPixPeople/";

        /// <summary>The ONE material all 14 bodies share (CraftPixPeopleBuilder authors it).</summary>
        private const string SharedMaterialPath = "Assets/Art/People/CraftPix/CraftPixPeople.mat";

        /// <summary>The pack's single shared palette atlas.</summary>
        private const string AtlasPath = "Assets/Art/People/CraftPix/people_texture_map.png";

        /// <summary>The pool size the owner ruling set. A silent shrink is a silent town.</summary>
        private const int ExpectedPoolCount = 14;

        /// <summary>Still loaded by seven other call sites — must never be collateral.</summary>
        private static readonly string[] LegacyBodies =
        {
            "NPCs/NPC_Peasant_Mevina",
            "NPCs/NPC_Peasant_Tob",
        };

        /// <summary>Standalone batch entry — prints the TOWNSFOLK_BODIES_OK/_FAIL marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("TOWNSFOLK_BODIES_OK - " + reason);
            else Debug.LogError("TOWNSFOLK_BODIES_FAIL: " + reason);
        }

        /// <summary>Covenant contract for DataRegression.RunAll ([townsfolk-bodies]). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string[] pool = ReadBodyPool(failures);
            int resolved = 0, slots = 0;

            Case(failures, "pool", () => Case1_PoolShape(failures, pool));
            Case(failures, "resolve", () => resolved = Case2_EveryEntryResolves(failures, pool));
            Case(failures, "renderers", () => Case3_EveryBodyRenders(failures, pool));
            Case(failures, "materials", () => slots = Case4_EverySlotIsUrpAndTextured(failures, pool));
            Case(failures, "shared-mat", () => Case5_SharedMaterialIsTheOne(failures, pool));
            Case(failures, "legacy", () => Case6_LegacyBodiesSurvive(failures));

            if (failures.Count == 0)
            {
                reason = $"TOWNSFOLK-BODIES OK - 6/6 cases pass ({pool.Length}/{ExpectedPoolCount} pool entries, " +
                         $"{resolved} prefabs resolved through Resources.Load, {slots} submesh slot(s) all " +
                         "URP + albedo-bound on ONE shared material, legacy peasant bodies intact)";
                return true;
            }
            reason = "TOWNSFOLK-BODIES FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        // =====================================================================
        //  CASE 1 — the pool is the shape the owner ruled, and matches what is BUILT
        // =====================================================================
        private static void Case1_PoolShape(List<string> failures, string[] pool)
        {
            if (pool.Length != ExpectedPoolCount)
                failures.Add($"[pool] CastleTownsfolkInjector.BodyPool has {pool.Length} entries, expected " +
                             $"{ExpectedPoolCount}. The owner ruling is 14 CraftPix bodies; a shrunken pool " +
                             "silently narrows the town back down to a handful of repeated faces.");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in pool)
            {
                if (string.IsNullOrEmpty(entry)) { failures.Add("[pool] an empty BodyPool entry would load nothing and spawn a capsule"); continue; }
                if (!seen.Add(entry))
                    failures.Add($"[pool] '{entry}' appears TWICE - a duplicate wastes a slot and makes twins more likely");
                if (!entry.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                    failures.Add($"[pool] '{entry}' is not under '{ResourcePrefix}' - the CraftPix repoint is incomplete " +
                                 "or an old People-pack path was left behind");
            }

            // The other half of the same bug: an entry that is spelled fine but was never
            // built. Compare the pool against the prefabs ACTUALLY on disk, both ways.
            if (!AssetDatabase.IsValidFolder(PrefabDir))
            {
                failures.Add($"[pool] '{PrefabDir}' does not exist - the bodies were never built. " +
                             "Run Defenders/Art/Build CraftPix Townsfolk Bodies " +
                             "(DeNelle.Editor.CraftPixPeopleBuilder.Build).");
                return;
            }

            var onDisk = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir })
                                      .Select(AssetDatabase.GUIDToAssetPath)
                                      .Select(p => ResourcePrefix + Path.GetFileNameWithoutExtension(p))
                                      .ToList();

            var missing = pool.Where(e => !onDisk.Contains(e)).ToList();
            if (missing.Count > 0)
                failures.Add($"[pool] {missing.Count} pool entr(ies) have no prefab in {PrefabDir}: " +
                             string.Join(", ", missing) + ". Re-run CraftPixPeopleBuilder.Build.");

            var orphans = onDisk.Where(p => !pool.Contains(p)).ToList();
            if (orphans.Count > 0)
                failures.Add($"[pool] {orphans.Count} built prefab(s) are NOT in BodyPool and can never spawn: " +
                             string.Join(", ", orphans) + ". Either add them to the pool or stop building them.");
        }

        // =====================================================================
        //  CASE 2 — every entry comes back from the call the injector actually makes
        // =====================================================================
        private static int Case2_EveryEntryResolves(List<string> failures, string[] pool)
        {
            int ok = 0;
            foreach (var entry in pool)
            {
                if (string.IsNullOrEmpty(entry)) continue;
                var prefab = Resources.Load<GameObject>(entry);
                if (prefab == null)
                {
                    failures.Add($"[resolve] Resources.Load<GameObject>(\"{entry}\") returned NULL. " +
                                 "CastleTownsfolkInjector.SpawnVillager warns and spawns a grey capsule " +
                                 "placeholder in the middle of the owner's town.");
                    continue;
                }
                ok++;
            }
            return ok;
        }

        // =====================================================================
        //  CASE 3 — every body satisfies the injector's own VerifyRenders contract
        // =====================================================================
        private static void Case3_EveryBodyRenders(List<string> failures, string[] pool)
        {
            foreach (var entry in pool)
            {
                var prefab = LoadBody(entry);
                if (prefab == null) continue;   // already reported by [resolve]

                if (!HasVisibleMesh(prefab))
                    failures.Add($"[renderers] '{entry}' has no enabled Renderer carrying a real mesh. " +
                                 "The injector's VerifyRenders destroys such a body and uses a capsule " +
                                 "placeholder instead - an invisible villager, with only a warning.");
            }
        }

        /// <summary>Mirrors CastleTownsfolkInjector.VerifyRenders exactly.</summary>
        private static bool HasVisibleMesh(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled) continue;
                bool hasMesh =
                    (r is SkinnedMeshRenderer smr && smr.sharedMesh != null) ||
                    (r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null);
                if (hasMesh) return true;
            }
            return false;
        }

        // =====================================================================
        //  CASE 4 — every submesh slot: non-null + URP + ALBEDO ACTUALLY BOUND
        // =====================================================================
        private static int Case4_EverySlotIsUrpAndTextured(List<string> failures, string[] pool)
        {
            int slots = 0;
            foreach (var entry in pool)
            {
                var prefab = LoadBody(entry);
                if (prefab == null) continue;

                bool sawAny = false;
                foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || MeshOf(r) == null) continue;
                    var mats = r.sharedMaterials;

                    // A short array is not cosmetic: the trailing submeshes draw with NO
                    // material at all, which is the untextured/magenta slab.
                    int subMeshes = Mathf.Max(1, MeshOf(r).subMeshCount);
                    if (mats == null || mats.Length < subMeshes)
                    {
                        failures.Add($"[materials] '{entry}' renderer '{r.gameObject.name}' has " +
                                     $"{(mats == null ? 0 : mats.Length)} material slot(s) for {subMeshes} submesh(es) - " +
                                     "the trailing submeshes render with no material.");
                        continue;
                    }

                    for (int i = 0; i < mats.Length; i++)
                    {
                        sawAny = true;
                        slots++;
                        var m = mats[i];
                        if (m == null)
                        {
                            failures.Add($"[materials] '{entry}' renderer '{r.gameObject.name}' submesh {i} has a " +
                                         "NULL material - it renders MAGENTA.");
                            continue;
                        }
                        if (!IsUrp(m))
                        {
                            failures.Add($"[materials] '{entry}' renderer '{r.gameObject.name}' submesh {i} is on shader " +
                                         $"'{(m.shader != null ? m.shader.name : "<null>")}' which is not a Universal RP " +
                                         "shader - it lights wrong (or magenta) in this project's URP pipeline.");
                            continue;
                        }
                        if (AlbedoOf(m) == null)
                            failures.Add($"[materials] '{entry}' renderer '{r.gameObject.name}' submesh {i} is URP but has " +
                                         "NO albedo texture bound (_BaseMap/_MainTex both null) - it renders as a flat " +
                                         "grey/dark body. A shader-only check passes this; that is exactly how this " +
                                         "project has shipped untextured meshes before.");
                    }
                }

                if (!sawAny)
                    failures.Add($"[materials] '{entry}' exposed no paintable material slot at all - nothing was verified " +
                                 "for this body, which must never read as a pass.");
            }
            return slots;
        }

        // =====================================================================
        //  CASE 5 — there is ONE shared material, it is real, and the bodies use it
        // =====================================================================
        private static void Case5_SharedMaterialIsTheOne(List<string> failures, string[] pool)
        {
            var shared = AssetDatabase.LoadAssetAtPath<Material>(SharedMaterialPath);
            if (shared == null)
            {
                failures.Add($"[shared-mat] the shared material '{SharedMaterialPath}' does not exist. " +
                             "Run Defenders/Art/Build CraftPix Townsfolk Bodies to author it.");
                return;
            }

            if (!IsUrp(shared))
                failures.Add($"[shared-mat] the shared material is on shader " +
                             $"'{(shared.shader != null ? shared.shader.name : "<null>")}', not a Universal RP shader.");

            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (atlas == null)
                failures.Add($"[shared-mat] the shared atlas '{AtlasPath}' does not exist - the owner-downloaded " +
                             "CraftPix texture is not staged, so every body would be untextured.");
            else if (AlbedoOf(shared) != atlas)
                failures.Add("[shared-mat] the shared material's albedo is not the staged people_texture_map atlas " +
                             $"(bound: '{(AlbedoOf(shared) != null ? AlbedoOf(shared).name : "<null>")}').");

            // ONE material for 14 bodies is the whole point of a shared atlas. Anything else
            // means a future edit split them and quietly multiplied the draw materials.
            var distinct = new HashSet<Material>();
            foreach (var entry in pool)
            {
                var prefab = LoadBody(entry);
                if (prefab == null) continue;
                foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || MeshOf(r) == null) continue;
                    foreach (var m in r.sharedMaterials) if (m != null) distinct.Add(m);
                }
            }
            if (distinct.Count > 1)
                failures.Add($"[shared-mat] the 14 bodies are spread across {distinct.Count} distinct materials " +
                             $"({string.Join(", ", distinct.Where(m => m != null).Select(m => m.name))}) - the pack " +
                             "ships ONE atlas, so there must be ONE material.");
            else if (distinct.Count == 1 && !distinct.Contains(shared))
                failures.Add("[shared-mat] the bodies all share a material, but it is NOT the authored " +
                             $"'{SharedMaterialPath}' - the builder's binding drifted.");
        }

        // =====================================================================
        //  CASE 6 — the two bodies this repoint REPLACED must still exist
        // =====================================================================
        private static void Case6_LegacyBodiesSurvive(List<string> failures)
        {
            foreach (var legacy in LegacyBodies)
            {
                if (Resources.Load<GameObject>(legacy) != null) continue;
                failures.Add($"[legacy] '{legacy}' no longer resolves. It left CastleTownsfolkInjector's pool but " +
                             "seven other call sites still load it (CastleVendorNpcInjector, " +
                             "CastleCompanionIntroducerInjector, SylasStewardInjector, QuestCastNpcInjector, " +
                             "VillageNpcInjector, TorchWardenDress) - deleting it blanks the vendors and the " +
                             "quest cast into capsule placeholders.");
            }
        }

        // -------- helpers --------

        /// <summary>
        /// The REAL private BodyPool array off CastleTownsfolkInjector (see the header's
        /// reflection note). A missing/renamed field is a hard failure, never an empty pass:
        /// an oracle that silently verifies zero entries is worse than no oracle.
        /// </summary>
        private static string[] ReadBodyPool(List<string> failures)
        {
            try
            {
                var field = typeof(CastleTownsfolkInjector)
                    .GetField("BodyPool", BindingFlags.NonPublic | BindingFlags.Static);
                if (field == null)
                {
                    failures.Add("[pool] CastleTownsfolkInjector has no static field named 'BodyPool' - it was " +
                                 "renamed or removed, so the townsfolk body contract cannot be verified at all.");
                    return Array.Empty<string>();
                }
                var value = field.GetValue(null) as string[];
                if (value == null)
                {
                    failures.Add("[pool] CastleTownsfolkInjector.BodyPool is not a string[] (or is null) - the " +
                                 "injector cannot pick a body.");
                    return Array.Empty<string>();
                }
                return value;
            }
            catch (Exception ex)
            {
                failures.Add("[pool] reading CastleTownsfolkInjector.BodyPool THREW " + ex.GetType().Name + ": " + ex.Message);
                return Array.Empty<string>();
            }
        }

        private static GameObject LoadBody(string entry)
            => string.IsNullOrEmpty(entry) ? null : Resources.Load<GameObject>(entry);

        private static Mesh MeshOf(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr) return smr.sharedMesh;
            var mf = r.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        /// <summary>True when the material draws through the Universal RP.</summary>
        private static bool IsUrp(Material m)
        {
            if (m == null || m.shader == null) return false;
            return m.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal);
        }

        /// <summary>
        /// The albedo texture URP actually samples. Both names are checked because this
        /// project's runtime material fixers read _MainTex first and _BaseMap second, so a
        /// binding on only one of them can be invisible to half the pipeline.
        /// </summary>
        private static Texture AlbedoOf(Material m)
        {
            if (m == null) return null;
            if (m.HasProperty("_BaseMap"))
            {
                var t = m.GetTexture("_BaseMap");
                if (t != null) return t;
            }
            if (m.HasProperty("_MainTex"))
            {
                var t = m.GetTexture("_MainTex");
                if (t != null) return t;
            }
            return m.mainTexture;
        }

        // Guard each case so one throw becomes a labelled failure, not a dead suite.
        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }
    }
}
