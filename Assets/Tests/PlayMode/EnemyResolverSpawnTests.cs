// SCAFFOLD — CLI must build-verify under Unity Test Framework
// =============================================================================
// EnemyResolverSpawnTests (PlayMode) — WO-772 Phase 1 / A5 runtime oracle.
// -----------------------------------------------------------------------------
// LINT != RUNTIME. EnemyResolverRegression proves the id->model MAP is correct
// headlessly; this test proves the bug is fixed AT SPAWN — it drives the REAL
// EnemyFactory.Build path (the single enemy-creation path) for every approved
// Hollow combat id and asserts:
//   1. each id spawns a REAL skinned body (a SkinnedMeshRenderer with a mesh) —
//      NOT the tinted-capsule fallback (so no id silently degraded), and
//   2. the resolver hands N distinct ResolvedKeys for the N ids, and the actual
//      spawned meshes span >=6 distinct meshes — the fleet of Hollow types does
//      NOT collapse to one generic skeleton (the bug).
//
// Runs headless in batchmode. No baked NavMesh is needed — EnemyFactory.Build
// degrades gracefully (the agent just holds) and still skins the visual.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using DeNelle.Core.Enemies;
using DeNelle.Village;

namespace DeNelle.Tests.PlayMode
{
    [TestFixture]
    public class EnemyResolverSpawnTests
    {
        private Transform _parent;

        [SetUp]
        public void SetUp()
        {
            _parent = new GameObject("EnemyResolverSpawnTests_Root").transform;
        }

        [TearDown]
        public void TearDown()
        {
            if (_parent != null) Object.DestroyImmediate(_parent.gameObject);
        }

        // Build a minimal village EnemyDef for an id, reading the codex-intended model
        // straight from the resolver (so the def carries the same modelKey enemies.json
        // supplies — the data path the factory reads).
        private static EnemyDef DefFor(string id)
        {
            var resolved = EnemyResolver.Resolve(id);
            return new EnemyDef
            {
                Id = id,
                Name = id,
                Family = "hollow",
                ModelKey = resolved != null ? resolved.ModelKey : null,
                Height = 1.9f,
            };
        }

        private static SkinnedMeshRenderer FindSkinned(Enemy enemy)
        {
            if (enemy == null) return null;
            return enemy.GetComponentInChildren<SkinnedMeshRenderer>(true);
        }

        [UnityTest]
        public IEnumerator every_hollow_id_spawns_a_real_skinned_body_not_a_capsule()
        {
            foreach (var id in EnemyResolver.ApprovedHollowCombatIds)
            {
                var def = DefFor(id);
                Enemy enemy = EnemyFactory.Build(def, Vector3.zero, Quaternion.identity, _parent);
                yield return null;   // let Skin/animator settle a frame

                Assert.That(enemy, Is.Not.Null, $"EnemyFactory.Build returned null for '{id}'.");
                var smr = FindSkinned(enemy);
                Assert.That(smr, Is.Not.Null,
                    $"'{id}' spawned with NO SkinnedMeshRenderer — it degraded to the tinted-capsule fallback (generic-skeleton bug).");
                Assert.That(smr.sharedMesh, Is.Not.Null,
                    $"'{id}' skinned renderer has no mesh — capsule/empty body.");

                if (enemy != null) Object.DestroyImmediate(enemy.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator n_hollow_types_yield_n_distinct_resolved_keys_and_varied_meshes()
        {
            var resolvedKeys = new HashSet<string>();
            var meshes = new HashSet<string>();
            int ids = 0;

            foreach (var id in EnemyResolver.ApprovedHollowCombatIds)
            {
                ids++;
                var resolved = EnemyResolver.Resolve(id);
                Assert.That(resolved, Is.Not.Null, $"resolver returned null for approved id '{id}'.");
                resolvedKeys.Add(resolved.ResolvedKey);

                var def = DefFor(id);
                Enemy enemy = EnemyFactory.Build(def, Vector3.zero, Quaternion.identity, _parent);
                yield return null;
                var smr = FindSkinned(enemy);
                if (smr != null && smr.sharedMesh != null) meshes.Add(smr.sharedMesh.name);
                if (enemy != null) Object.DestroyImmediate(enemy.gameObject);
            }

            // Headline assertion: N distinct ids -> N distinct resolved keys.
            Assert.That(resolvedKeys.Count, Is.EqualTo(ids),
                $"{ids} Hollow ids produced only {resolvedKeys.Count} distinct ResolvedKeys — distinct ids collapsed to one body.");

            // Runtime proof: the spawned bodies span many meshes, not one generic skeleton.
            Assert.That(meshes.Count, Is.GreaterThanOrEqualTo(6),
                $"the Hollow roster spawned only {meshes.Count} distinct mesh(es) — expected >=6 (the varied silhouettes: Minion/Warrior/Rogue/Healer/Mage/Golem/Necromancer).");
        }
    }
}
