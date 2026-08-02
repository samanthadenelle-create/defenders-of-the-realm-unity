// SCAFFOLD — CLI must build-verify under Unity Test Framework
// =============================================================================
// BlankTownGateTests (EditMode) — WO-834 blank-founding baked standdown.
// -----------------------------------------------------------------------------
// Owner F8 seq 592: a "Build Your Own" founding loaded FULL of baked default-town
// structures because every surfacing path keyed on "nothing placed". The fix is
// the persisted everBuiltStructureIds ledger (save v36) + the pure rule
// StructureSingleton.MayBakedTwinSurface(id, everBuilt, migrated).
//
// Behavioral, headless (no MonoBehaviour singletons — mirrors ArmyReadinessTests):
//   1. the pure surfacing rule's truth table (unit-testable by design — WO-834
//      made the rule a pure static precisely so this suite can pin it);
//   2. SaveMigrator.MigrateToV36 seeding (blank save -> EMPTY list; established
//      save -> BaseLayout UNION FreeBuildsUsed UNION the template grant; sold
//      singleton -> freebie id survives WITHOUT the template grant);
//   3. GameState.MarkEverBuilt / HasEverBuilt ledger semantics (idempotent,
//      case-insensitive, null-tolerant).
// =============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class BlankTownGateTests
    {
        // ── 1. The pure surfacing rule ───────────────────────────────────────

        [Test]
        public void migrated_save_with_empty_ledger_suppresses_the_twin()
        {
            // THE seq-592 fix: Build-Your-Own = marker true (ResetToNewGame) + empty set.
            Assert.That(StructureSingleton.MayBakedTwinSurface(
                "collector_farm", new List<string>(), strategicPlacementMigrated: true),
                Is.False, "blank founding must show a truly blank town");
        }

        [Test]
        public void migrated_save_with_null_ledger_suppresses_the_twin()
        {
            Assert.That(StructureSingleton.MayBakedTwinSurface(
                "collector_farm", null, strategicPlacementMigrated: true),
                Is.False, "a null ledger on a migrated save reads as nothing-ever-built");
        }

        [Test]
        public void unmigrated_save_always_surfaces()
        {
            // Legacy pre-v30 save awaiting its one-shot migration AND the Default-Town
            // founding load (WO-748 arms Default Town by clearing the marker).
            Assert.That(StructureSingleton.MayBakedTwinSurface(
                "collector_farm", new List<string>(), strategicPlacementMigrated: false),
                Is.True, "while the bake owns the town the gate must stay open");
        }

        [Test]
        public void ever_built_id_surfaces_on_a_migrated_save()
        {
            // WO-819 sell-resurface: the id stays in the ledger forever, so the baked
            // twin returns after a sell.
            Assert.That(StructureSingleton.MayBakedTwinSurface(
                "collector_farm", new List<string> { "collector_farm" }, true),
                Is.True);
        }

        [Test]
        public void ledger_compare_is_case_insensitive()
        {
            Assert.That(StructureSingleton.MayBakedTwinSurface(
                "COLLECTOR_FARM", new List<string> { "collector_farm" }, true),
                Is.True, "catalog-id convention is OrdinalIgnoreCase");
        }

        [Test]
        public void null_or_empty_id_never_surfaces()
        {
            Assert.That(StructureSingleton.MayBakedTwinSurface(
                null, new List<string> { "x" }, false), Is.False);
            Assert.That(StructureSingleton.MayBakedTwinSurface(
                "", new List<string> { "x" }, false), Is.False);
        }

        // ── 2. MigrateToV36 seeding ──────────────────────────────────────────

        [Test]
        public void blank_save_seeds_an_empty_ledger()
        {
            // The owner's captured save shape: persisted=0 records, nothing burned.
            var s = new SaveSchema.PersistedState
            {
                BaseLayout = new List<PlacedStructureData>(),
                FreeBuildsUsed = new List<string>(),
            };
            s = SaveMigrator.Migrate(s, 35);

            Assert.That(s.EverBuiltStructureIds, Is.Not.Null, "seed must be present-but-empty, not null");
            Assert.That(s.EverBuiltStructureIds, Is.Empty, "a blank save must NOT inherit any surface right");
        }

        [Test]
        public void established_save_seeds_layout_freebies_and_template()
        {
            var s = new SaveSchema.PersistedState
            {
                BaseLayout = new List<PlacedStructureData>
                {
                    new PlacedStructureData("collector_farm", 1, 1, 0, level: 1,
                        yawOffset: 0f, worldY: 0f, wallMounted: false),
                },
                FreeBuildsUsed = new List<string> { "workshop" },
            };
            s = SaveMigrator.Migrate(s, 35);

            Assert.That(s.EverBuiltStructureIds, Does.Contain("collector_farm"), "BaseLayout leg");
            Assert.That(s.EverBuiltStructureIds, Does.Contain("workshop"), "FreeBuildsUsed leg");
            // The frozen template grant — existing towns keep today's Lever-1 pre-stand
            // and the WO-724 baked-barracks-at-unlock verbatim.
            Assert.That(s.EverBuiltStructureIds, Does.Contain("barracks"), "template grant (unlock right)");
            Assert.That(s.EverBuiltStructureIds, Does.Contain("pet-house"), "template grant (census row)");
            Assert.That(s.EverBuiltStructureIds, Does.Contain("apothecary"), "template grant (station row)");
        }

        [Test]
        public void sold_singleton_save_keeps_the_freebie_id_without_the_template()
        {
            // Placed-then-SOLD before v36: record gone, freebie flag remains. The id must
            // survive (its baked twin keeps resurfacing per WO-819) but an EMPTY layout
            // must NOT drag the whole template grant in (blank founding stays blank).
            var s = new SaveSchema.PersistedState
            {
                BaseLayout = new List<PlacedStructureData>(),
                FreeBuildsUsed = new List<string> { "pet-house" },
            };
            s = SaveMigrator.Migrate(s, 35);

            Assert.That(s.EverBuiltStructureIds, Does.Contain("pet-house"));
            Assert.That(s.EverBuiltStructureIds, Does.Not.Contain("barracks"),
                "empty-BaseLayout save must not receive the template grant");
        }

        [Test]
        public void migrator_never_clobbers_an_existing_ledger()
        {
            var s = new SaveSchema.PersistedState
            {
                BaseLayout = new List<PlacedStructureData>(),
                EverBuiltStructureIds = new List<string> { "workshop" },
            };
            s = SaveMigrator.Migrate(s, 35);

            Assert.That(s.EverBuiltStructureIds, Is.EqualTo(new List<string> { "workshop" }),
                "an already-seeded field must pass through untouched (additive-only law)");
        }

        [Test]
        public void fully_null_v35_save_seeds_an_empty_ledger()
        {
            // A partial save with neither list present — the null-tolerant floor.
            var s = SaveMigrator.Migrate(new SaveSchema.PersistedState(), 35);
            Assert.That(s.EverBuiltStructureIds, Is.Not.Null);
            Assert.That(s.EverBuiltStructureIds, Is.Empty);
        }

        // ── 3. GameState ledger semantics ────────────────────────────────────

        [Test]
        public void mark_ever_built_is_idempotent_and_case_insensitive()
        {
            var state = ScriptableObject.CreateInstance<GameState>();
            try
            {
                Assert.That(state.MarkEverBuilt("barracks"), Is.True, "first add");
                Assert.That(state.MarkEverBuilt("barracks"), Is.False, "repeat add is a no-op");
                Assert.That(state.MarkEverBuilt("BARRACKS"), Is.False, "case-variant is the same id");
                Assert.That(state.EverBuiltStructureIds.Count, Is.EqualTo(1));
                Assert.That(state.HasEverBuilt("Barracks"), Is.True);
                Assert.That(state.HasEverBuilt("collector_farm"), Is.False);
                Assert.That(state.MarkEverBuilt(null), Is.False, "null id never throws or records");
                Assert.That(state.MarkEverBuilt(""), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(state);
            }
        }

        [Test]
        public void mark_ever_built_self_heals_a_null_list()
        {
            var state = ScriptableObject.CreateInstance<GameState>();
            try
            {
                state.EverBuiltStructureIds = null;   // hostile/legacy in-memory shape
                Assert.That(state.MarkEverBuilt("workshop"), Is.True);
                Assert.That(state.HasEverBuilt("workshop"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(state);
            }
        }
    }
}
