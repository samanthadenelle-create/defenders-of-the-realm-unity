// =============================================================================
// EnemyWildlandsGateTests (EditMode) — pure-resolver oracle for the two fixes in
// the enemy resolve/build path (PAIN_POINTS_2026-07-26 §1.1).
// -----------------------------------------------------------------------------
// FIX 1 — the Wildlands "no spawn" ruling enforced at the resolver: the deferred
//   living roster (orc-raider / caveman / feral-wolf / tiefling-cultist) is NOT
//   combat-approved, and EnemyResolver.SubstituteHollowId hands a ratified Hollow
//   stand-in that resolves to a real committed model — so EnemyFactory.Build always
//   redirects a deferred id to a VALID body instead of the exploded Orc_Berserker.
//   The shipping roster (Hollow Ones + the Orc Warband + bosses) stays approved.
//
// FIX 2 — the dungeon UNDERSCORE hollow ids (hollow_villager_a / _b /
//   hollow_apprentice_minor / hollow_healer, from healers-cottage.json) resolve to
//   FOUR DISTINCT models rather than collapsing to the generic size-default.
//
// EditMode / pure logic — no GameObject, no NavMesh, no Resources.Load. The actual
// skinned-Build substitution is exercised by EnemyResolverSpawnTests (PlayMode) +
// EnemyResolverRegression (headless DataRegression). This locks the resolver
// CONTRACT the Build gate depends on.
// =============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Core.Enemies;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class EnemyWildlandsGateTests
    {
        private static readonly string[] DeferredWildlands =
            { "orc-raider", "caveman", "feral-wolf", "tiefling-cultist" };

        // ── FIX 1 ────────────────────────────────────────────────────────────────

        [Test]
        public void deferred_wildlands_ids_are_not_combat_approved()
        {
            foreach (var id in DeferredWildlands)
                Assert.That(EnemyResolver.IsCombatApproved(id), Is.False,
                    $"deferred Wildlands id '{id}' must NOT be combat-approved (§1.1).");
        }

        [Test]
        public void shipping_roster_ids_stay_combat_approved()
        {
            // The Hollow Ones + the shipping Orc Warband + bosses are NOT deferred.
            foreach (var id in new[] { "hollow-walker", "hollow-warrior", "necromancer",
                                       "orc-berserker", "orc-shaman", "orc-warlord" })
                Assert.That(EnemyResolver.IsCombatApproved(id), Is.True,
                    $"approved id '{id}' must stay combat-approved (gate must not be over-broad).");
        }

        [Test]
        public void substitute_for_each_deferred_id_is_a_resolvable_hollow_body()
        {
            // Heights mirror RegionMobSpawner.BuildRoamerDef so the size-based pick matches
            // what the live spawner actually hands EnemyFactory.Build.
            var height = new Dictionary<string, float>
            {
                ["orc-raider"] = 2.0f, ["caveman"] = 1.9f,
                ["feral-wolf"] = 1.2f, ["tiefling-cultist"] = 1.9f,
            };

            foreach (var id in DeferredWildlands)
            {
                string sub = EnemyResolver.SubstituteHollowId(id, null, height[id]);
                Assert.That(EnemyResolver.IsHollowId(sub), Is.True,
                    $"substitute '{sub}' for '{id}' is not an approved Hollow id.");
                Assert.That(EnemyResolver.IsCombatApproved(sub), Is.True,
                    $"substitute '{sub}' for '{id}' is itself gated — would loop.");
                Assert.That(EnemyResolver.TryResolveHollowModel(sub, null, out string model), Is.True,
                    $"substitute '{sub}' for '{id}' did not resolve a model.");
                Assert.That(string.IsNullOrEmpty(model), Is.False);
            }
        }

        [Test]
        public void substitute_picks_warrior_for_heavy_and_walker_for_light()
        {
            Assert.That(EnemyResolver.SubstituteHollowId("orc-raider", null, 2.0f), Is.EqualTo("hollow-warrior"),
                "a tall/heavy request should redirect to the armed Warrior.");
            Assert.That(EnemyResolver.SubstituteHollowId("feral-wolf", null, 1.2f), Is.EqualTo("hollow-walker"),
                "a small/light request should redirect to the basic Walker.");
        }

        // ── FIX 2 ────────────────────────────────────────────────────────────────

        [Test]
        public void dungeon_underscore_hollow_ids_all_resolve()
        {
            foreach (var id in new[] { "hollow_villager_a", "hollow_villager_b",
                                       "hollow_apprentice_minor", "hollow_healer" })
            {
                Assert.That(EnemyResolver.IsHollowId(id), Is.True,
                    $"dungeon underscore id '{id}' does not resolve (would collapse to the generic default).");
                Assert.That(EnemyResolver.Resolve(id), Is.Not.Null,
                    $"EnemyResolver.Resolve('{id}') returned null.");
            }
        }

        [Test]
        public void dungeon_underscore_hollow_ids_resolve_to_distinct_models()
        {
            var ids = new[] { "hollow_villager_a", "hollow_villager_b",
                              "hollow_apprentice_minor", "hollow_healer" };
            var keys = new HashSet<string>();
            foreach (var id in ids)
            {
                var r = EnemyResolver.Resolve(id);
                Assert.That(r, Is.Not.Null, $"'{id}' did not resolve.");
                keys.Add(r.ResolvedKey);
            }
            Assert.That(keys.Count, Is.EqualTo(ids.Length),
                $"{ids.Length} dungeon ids produced only {keys.Count} distinct ResolvedKeys — they collapsed to one body.");
        }

        [Test]
        public void underscore_and_hyphen_spellings_resolve_identically()
        {
            var underscore = EnemyResolver.Resolve("hollow_walker");
            var hyphen = EnemyResolver.Resolve("hollow-walker");
            Assert.That(underscore, Is.Not.Null);
            Assert.That(hyphen, Is.Not.Null);
            Assert.That(underscore.ResolvedKey, Is.EqualTo(hyphen.ResolvedKey),
                "Norm() must fold '_' -> '-' so both spellings resolve to the same body.");
        }
    }
}
