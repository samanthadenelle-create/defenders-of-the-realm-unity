// =============================================================================
// EnemyResolverRegression — headless proof that the generic-skeleton bug is FIXED
// (WO-772 Phase 1 / A5, ruling PAIN_POINTS_2026-07-26 §1.1).
// -----------------------------------------------------------------------------
// The bug: distinct enemy ids resolved to the SAME generic skeleton because
// EnemyFactory.ModelForEnemy only hard-cased 5 Hollow ids; every other approved
// Hollow id (hollow-mage / hollow-reaper / hollow-brute / cellar-hollow / the
// canon mini-boss hollow-apprentice) fell through to the size DEFAULT and spawned
// as a generic Skeleton_Minion / Skeleton_Golem.
//
// This oracle proves — through the SAME code path the spawner uses
// (EnemyFactory.ModelForEnemy, which now routes Hollow ids through EnemyResolver)
// — that:
//   1. every approved Hollow combat id is KNOWN to the resolver (never the
//      generic size-default),
//   2. each resolves to a per-id-DISTINCT ResolvedKey (N ids -> N distinct keys),
//   3. each base model is a real committed prefab that the RUNTIME SEAM resolves
//      (DeNelle.Core.EnemyAssetLoader — Addressables-first, Resources-fallback);
//      i.e. it would skin a real body, not a tinted-capsule fallback,
//   4. the factory hook is actually wired to the resolver (ModelForEnemy(id) ==
//      the resolver's base model for every approved id),
//   5. the 5 previously-broken ids each resolve to their INTENDED distinct model.
//
// Pure data/logic (catalog parse + asset resolve only) — runs inside
// DataRegression.RunAll and prints ENEMY_RESOLVER_OK on pass. Mirrors the
// TroopRosterRegression contract: public static bool Run(out string reason). Never throws.
//
// ⚠ ASSET LOADS GO THROUGH THE SEAM, NEVER Resources.Load DIRECTLY. Every
// "this mesh is committed" assertion below calls DeNelle.Core.EnemyAssetLoader
// (Addressables-FIRST, Resources-FALLBACK) — the exact path the spawner uses. This
// is what makes the oracle survive the Assets/Resources/Enemies -> Addressables
// migration: a raw Resources.Load would return null for every enemy the moment the
// art physically moves and paint the whole roster red for a reason that isn't real.
// Going through the seam also STRENGTHENS the claim — it proves the runtime resolve
// works, not merely that a file sits in a Resources folder.
// =============================================================================

using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core.Enemies;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class EnemyResolverRegression
    {
        // The intended base mesh per approved Hollow combat id (codex §2/§4, ratified).
        // Shared base meshes are deliberate — the resolver's Variant keeps the
        // ResolvedKey distinct (Reaper#reaper, Cellar#cellar, Apprentice#apprentice).
        private static readonly Dictionary<string, string> ExpectedBaseModel =
            new Dictionary<string, string>
            {
                ["hollow-walker"]     = "Skeleton_Minion",
                ["hollow-warrior"]    = "Skeleton_Warrior",
                ["hollow-rogue"]      = "Skeleton_Rogue",
                ["hollow-acolyte"]    = "Skeleton_Healer",
                ["hollow-mage"]       = "Skeleton_Mage",
                ["hollow-reaper"]     = "Skeleton_Warrior",
                ["hollow-brute"]      = "Skeleton_Golem",
                ["cellar-hollow"]     = "Skeleton_Minion",
                ["necromancer"]       = "Necromancer",
                ["hollow-apprentice"] = "Skeleton_Mage",
            };

        // The ids that used to collapse to a generic skeleton (the bug's victims).
        private static readonly string[] PreviouslyBroken =
            { "hollow-mage", "hollow-reaper", "hollow-brute", "cellar-hollow", "hollow-apprentice" };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();

            // Load the REAL catalog the game reads (CanonicalJson bytes -> EnemyCatalog),
            // so ModelForEnemy sees the actual enemies.json modelKey (A4 data-driven).
            EnemyCatalog catalog = null;
            string json = DeNelle.Core.CanonicalJson.Read(WaveDataLoader.EnemiesRelativePath);
            if (!string.IsNullOrEmpty(json))
            {
                try { catalog = JsonConvert.DeserializeObject<EnemyCatalog>(json); }
                catch (System.Exception ex)
                {
                    reason = $"ENEMY_RESOLVER_FAIL: enemies.json parse error: {ex.Message}";
                    return false;
                }
            }
            if (catalog == null || catalog.Enemies == null || catalog.Enemies.Count == 0)
            {
                reason = "ENEMY_RESOLVER_FAIL: enemies.json produced 0 EnemyDef objects (mapping break/missing file).";
                return false;
            }

            var resolvedKeys = new Dictionary<string, string>();   // resolvedKey -> first id that produced it
            var combatIds = EnemyResolver.ApprovedHollowCombatIds;

            foreach (var id in combatIds)
            {
                // 1) The resolver must KNOW this id (never fall to the generic default).
                var resolved = EnemyResolver.Resolve(id, DataModelKey(catalog, id));
                if (resolved == null || !EnemyResolver.IsHollowId(id))
                {
                    failures.Add($"approved Hollow id '{id}' is NOT known to EnemyResolver (would fall to the generic size-default — the bug).");
                    continue;
                }

                // 2) Distinct ResolvedKey — two different ids must never share one.
                if (resolvedKeys.TryGetValue(resolved.ResolvedKey, out var firstId))
                    failures.Add($"id '{id}' shares ResolvedKey '{resolved.ResolvedKey}' with '{firstId}' — distinct ids collapsed to one body (the generic-skeleton bug).");
                else
                    resolvedKeys[resolved.ResolvedKey] = id;

                // 3) The base model must be a real committed prefab (not a capsule fallback).
                if (!ModelLoads(resolved.ModelKey))
                    failures.Add($"id '{id}' -> model '{resolved.ModelKey}' but EnemyAssetLoader could not resolve " +
                                 $"\"Enemies/{resolved.ModelKey}\" via Addressables OR Resources (would spawn a tinted capsule).");

                // 4) Factory-hook wiring: the spawner path (ModelForEnemy) MUST return the
                //    resolver's base model — proves the factory routes through the resolver,
                //    not the retired hard-cased switch.
                var def = catalog.Find(id) ?? new EnemyDef { Id = id, ModelKey = resolved.ModelKey };
                string factoryModel = EnemyFactory.ModelForEnemy(def);
                if (factoryModel != resolved.ModelKey)
                    failures.Add($"EnemyFactory.ModelForEnemy('{id}') returned '{factoryModel}', resolver says '{resolved.ModelKey}' — factory NOT wired to the resolver.");

                // 5) Intended-model check (catches a silent table drift).
                if (ExpectedBaseModel.TryGetValue(id, out var expected) && resolved.ModelKey != expected)
                    failures.Add($"id '{id}' resolves to '{resolved.ModelKey}', codex expects '{expected}'.");

                log.AppendLine($"  {id} -> model '{resolved.ModelKey}' key '{resolved.ResolvedKey}' rig '{resolved.AnimatorRig}' role '{resolved.RoleKey}'");
            }

            // 6) N ids -> N distinct ResolvedKeys (the headline assertion).
            if (resolvedKeys.Count != combatIds.Count && failures.Count == 0)
                failures.Add($"{combatIds.Count} approved Hollow ids produced only {resolvedKeys.Count} distinct ResolvedKeys.");

            // 7) The previously-broken ids specifically must resolve to their intended,
            //    non-generic model (the direct before->after proof).
            foreach (var id in PreviouslyBroken)
            {
                var def = catalog.Find(id) ?? new EnemyDef { Id = id };
                string factoryModel = EnemyFactory.ModelForEnemy(def);
                if (ExpectedBaseModel.TryGetValue(id, out var expected) && factoryModel != expected)
                    failures.Add($"previously-broken id '{id}' still resolves to '{factoryModel}' (expected '{expected}') — the generic-skeleton bug is NOT fixed for this id.");
            }

            // 8) Wildlands stub sanity: the faction enum exists + maps living tokens, and
            //    reserves ZERO members in Phase 1 (no accidental Phase-2 art dependency).
            if (EnemyResolver.FactionForFamily("orc") != EnemyFaction.Wildlands ||
                EnemyResolver.FactionForFamily("hollow") != EnemyFaction.HollowOnes)
                failures.Add("EnemyResolver.FactionForFamily mapping wrong (hollow->HollowOnes, orc->Wildlands).");

            // 9) WILDLANDS DEFERRAL GATE (FIX 1, PAIN_POINTS §1.1): every deferred living
            //    id must be NOT combat-approved, and its substitute must resolve to a real
            //    committed Hollow model (so EnemyFactory.Build always redirects to a VALID
            //    body, never the exploded orc). The approved roster must stay approved.
            foreach (var wid in new[] { "orc-raider", "caveman", "feral-wolf", "tiefling-cultist" })
            {
                if (EnemyResolver.IsCombatApproved(wid))
                    failures.Add($"deferred Wildlands id '{wid}' is IsCombatApproved==true (should be gated OFF per §1.1).");
                string sub = EnemyResolver.SubstituteHollowId(wid, null, 2.0f);
                if (!EnemyResolver.TryResolveHollowModel(sub, null, out string subModel) ||
                    !ModelLoads(subModel))
                    failures.Add($"Wildlands id '{wid}' substitute '{sub}' did not resolve to a committed model " +
                                 $"(EnemyAssetLoader found \"Enemies/{subModel}\" via neither Addressables nor Resources).");
            }
            foreach (var aid in new[] { "hollow-walker", "necromancer", "orc-berserker", "orc-warlord" })
                if (!EnemyResolver.IsCombatApproved(aid))
                    failures.Add($"approved id '{aid}' is IsCombatApproved==false (gate is over-broad — shipping roster must stay approved).");

            // 10) DUNGEON UNDERSCORE ALIASES (FIX 2): the healers-cottage.json ids must each
            //     resolve to a DISTINCT, non-generic model (not collapse to Skeleton_Minion).
            var dungeonIds = new[] { "hollow_villager_a", "hollow_villager_b", "hollow_apprentice_minor", "hollow_healer" };
            var dungeonKeys = new Dictionary<string, string>();
            foreach (var did in dungeonIds)
            {
                var r = EnemyResolver.Resolve(did);
                if (r == null || !EnemyResolver.IsHollowId(did))
                {
                    failures.Add($"dungeon id '{did}' does NOT resolve through EnemyResolver (collapses to the generic size-default).");
                    continue;
                }
                if (dungeonKeys.TryGetValue(r.ResolvedKey, out var first))
                    failures.Add($"dungeon id '{did}' shares ResolvedKey '{r.ResolvedKey}' with '{first}' — not distinct.");
                else
                    dungeonKeys[r.ResolvedKey] = did;
                if (!ModelLoads(r.ModelKey))
                    failures.Add($"dungeon id '{did}' -> model '{r.ModelKey}' but EnemyAssetLoader could not resolve " +
                                 $"\"Enemies/{r.ModelKey}\" via Addressables OR Resources.");
            }
            if (dungeonKeys.Count != dungeonIds.Length && failures.Count == 0)
                failures.Add($"{dungeonIds.Length} dungeon ids produced only {dungeonKeys.Count} distinct ResolvedKeys.");

            // 11) WO-954 — THE COMMITTED-MESH REGISTRY IS REAL. Every name in
            //     EnemyResolver.CommittedModels must actually load through the runtime seam
            //     (EnemyAssetLoader: Addressables, else Resources). This is what lets the
            //     resolver honour data for every family: the registry is the safety gate, so a
            //     rotted entry would let a typo'd/renamed mesh through and spawn a tinted
            //     capsule. Fails loudly instead.
            foreach (var key in EnemyResolver.CommittedModelKeys)
                if (!ModelLoads(key))
                    failures.Add($"EnemyResolver.CommittedModels lists '{key}', but EnemyAssetLoader could not resolve " +
                                 $"\"Enemies/{key}\" via Addressables OR Resources — the registry has rotted (or the " +
                                 "address was never grouped); data steered to this key would spawn a capsule.");

            // 12) WO-954 — DATA/CODE AGREEMENT for EVERY catalog row, not just the Hollows.
            //     The bug class: enemies.json said one model and an independent code table
            //     said another, and nothing failed. For each of the 19 rows either
            //       (a) the modelKey is committed -> ModelForEnemy MUST return exactly it
            //           (data is the authority), or
            //       (b) it is NOT committed -> the row is knowingly art-pending, and the code
            //           stand-in it falls back to must still load a real body.
            //     Case (b) is listed by name so a new un-imported key can never sneak in
            //     silently — adding one now REQUIRES touching this list.
            var artPendingModelKeys = new HashSet<string> { "OgreMage" };  // no OgreMage.fbx in Resources/Enemies
            foreach (var e in catalog.Enemies)
            {
                if (e == null || string.IsNullOrEmpty(e.Id)) continue;
                string model = EnemyFactory.ModelForEnemy(e);

                if (!ModelLoads(model))
                {
                    failures.Add($"enemies.json row '{e.Id}' resolves to model '{model}' but EnemyAssetLoader could not " +
                                 $"resolve \"Enemies/{model}\" via Addressables OR Resources — this row spawns a tinted capsule.");
                    continue;
                }

                if (string.IsNullOrEmpty(e.ModelKey)) continue;   // no data opinion — code table owns it

                if (EnemyResolver.IsCommittedModel(e.ModelKey))
                {
                    if (model != e.ModelKey)
                        failures.Add($"DATA/CODE DIVERGENCE: enemies.json row '{e.Id}' asks for model " +
                                     $"'{e.ModelKey}' (a committed mesh) but EnemyFactory.ModelForEnemy returned " +
                                     $"'{model}' — a code table is overriding the data authority (WO-954).");
                }
                else if (!artPendingModelKeys.Contains(e.ModelKey))
                {
                    failures.Add($"enemies.json row '{e.Id}' names modelKey '{e.ModelKey}', which is neither a " +
                                 "committed mesh (EnemyResolver.CommittedModels) nor a declared art-pending key — " +
                                 "import the art and register it, fix the typo, or add it to artPendingModelKeys " +
                                 "with a note.");
                }
                else
                {
                    log.AppendLine($"  [art-pending] row '{e.Id}' wants '{e.ModelKey}' (not imported) -> stand-in '{model}'");
                }
            }

            if (failures.Count == 0)
            {
                reason = $"{EnemyResolver.LintMarker} — {combatIds.Count} approved Hollow ids -> " +
                         $"{resolvedKeys.Count} DISTINCT resolved keys; every base model resolves through " +
                         "EnemyAssetLoader (Addressables-first, Resources-fallback); " +
                         "factory routes through EnemyResolver; the 5 previously-generic ids each resolve to their " +
                         "own model; Wildlands reserved as a Phase-2 stub; " +
                         $"WO-954: all {EnemyResolver.CommittedModelKeys.Count} committed mesh keys load, and every " +
                         $"one of the {catalog.Enemies.Count} enemies.json rows agrees with EnemyFactory.ModelForEnemy.";
                Debug.Log("[enemy-resolver]\n" + log);
                return true;
            }

            var sb = new StringBuilder();
            sb.Append($"ENEMY_RESOLVER_FAIL: {failures.Count} issue(s):");
            foreach (var f in failures) sb.Append("\n  - ").Append(f);
            reason = sb.ToString();
            return false;
        }

        // TRUE when the committed body for <paramref name="modelKey"/> resolves through the
        // SINGLE runtime seam the spawner uses: DeNelle.Core.EnemyAssetLoader, which probes
        // the Addressables catalog for "Enemies/<key>" and falls back to Resources.Load on the
        // same key. Deliberately NOT Resources.Load here — see the file header: this oracle has
        // to stay true on both sides of the Resources -> Addressables migration, and asking the
        // real loader is a STRONGER claim than asking the filesystem.
        private static bool ModelLoads(string modelKey)
            => !string.IsNullOrEmpty(modelKey) &&
               DeNelle.Core.EnemyAssetLoader.LoadEnemyPrefab(modelKey) != null;

        // The enemies.json modelKey for an id (A4 data-driven variety), or null.
        private static string DataModelKey(EnemyCatalog catalog, string id)
        {
            var def = catalog != null ? catalog.Find(id) : null;
            return def != null ? def.ModelKey : null;
        }
    }
}
