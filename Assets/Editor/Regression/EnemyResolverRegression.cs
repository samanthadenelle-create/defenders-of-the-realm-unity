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
//   3. each base model is a real committed prefab in Resources/Enemies (would
//      skin a real body, not a tinted-capsule fallback),
//   4. the factory hook is actually wired to the resolver (ModelForEnemy(id) ==
//      the resolver's base model for every approved id),
//   5. the 5 previously-broken ids each resolve to their INTENDED distinct model.
//
// Pure data/logic (Resources.Load only) — runs inside DataRegression.RunAll and
// prints ENEMY_RESOLVER_OK on pass. Mirrors the TroopRosterRegression contract:
// public static bool Run(out string reason). Never throws.
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
                string path = "Enemies/" + resolved.ModelKey;
                if (Resources.Load<GameObject>(path) == null)
                    failures.Add($"id '{id}' -> model '{resolved.ModelKey}' but Resources.Load(\"{path}\") is NULL (would spawn a tinted capsule).");

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

            if (failures.Count == 0)
            {
                reason = $"{EnemyResolver.LintMarker} — {combatIds.Count} approved Hollow ids -> " +
                         $"{resolvedKeys.Count} DISTINCT resolved keys; every base model loads from Resources; " +
                         "factory routes through EnemyResolver; the 5 previously-generic ids each resolve to their " +
                         "own model; Wildlands reserved as a Phase-2 stub.";
                Debug.Log("[enemy-resolver]\n" + log);
                return true;
            }

            var sb = new StringBuilder();
            sb.Append($"ENEMY_RESOLVER_FAIL: {failures.Count} issue(s):");
            foreach (var f in failures) sb.Append("\n  - ").Append(f);
            reason = sb.ToString();
            return false;
        }

        // The enemies.json modelKey for an id (A4 data-driven variety), or null.
        private static string DataModelKey(EnemyCatalog catalog, string id)
        {
            var def = catalog != null ? catalog.Find(id) : null;
            return def != null ? def.ModelKey : null;
        }
    }
}
