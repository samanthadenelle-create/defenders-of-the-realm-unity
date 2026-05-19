// =============================================================================
// Canonical data — PetCatalog loader tests (EditMode)
// -----------------------------------------------------------------------------
// qa-test-plan.md TC-XC-09: pets.json must load via the typed PetCatalog and
// hydrate the three starter guardian pets with their five Bond-rank rows and the
// deploy-ring geometry.
//
// PetCatalog reads StreamingAssets/Data/Canonical/pets.json synchronously — a
// real directory in the Editor — so the loader runs in EditMode with no stub.
// =============================================================================

using NUnit.Framework;
using UnityEngine;
using DeNelle.Pets;

namespace DeNelle.Data.Tests
{
    [TestFixture]
    public class PetCatalogTest
    {
        [SetUp]
        public void SetUp()
        {
            PetCatalog.Reload();
        }

        // =====================================================================
        //  Parse + entry count
        // =====================================================================

        [Test]
        public void pets_json_loads_exactly_three_starter_pets()
        {
            Assert.That(PetCatalog.Pets, Is.Not.Null);
            Assert.That(PetCatalog.Pets.Count, Is.EqualTo(3),
                "pets.json must hydrate the three starter guardian pets.");
        }

        [Test]
        public void all_three_starter_pet_ids_are_present()
        {
            foreach (var id in new[] { "pet-aether-sprite", "pet-flame-pup", "pet-ice-wolf" })
            {
                Assert.That(PetCatalog.Find(id), Is.Not.Null,
                    $"pets.json must contain '{id}'.");
            }
        }

        [Test]
        public void all_three_species_resolve_via_find_by_species()
        {
            foreach (var species in new[] { "aether-sprite", "flame-pup", "ice-wolf" })
            {
                Assert.That(PetCatalog.FindBySpecies(species), Is.Not.Null,
                    $"FindBySpecies('{species}') must resolve a def.");
            }
        }

        [Test]
        public void unknown_lookups_return_null()
        {
            Assert.That(PetCatalog.Find("pet-nonexistent"), Is.Null);
            Assert.That(PetCatalog.Find(null), Is.Null);
            Assert.That(PetCatalog.FindBySpecies("dragon"), Is.Null);
            Assert.That(PetCatalog.FindBySpecies(null), Is.Null);
        }

        // =====================================================================
        //  Field sanity
        // =====================================================================

        [Test]
        public void every_pet_carries_five_bond_ranks_zero_through_four()
        {
            foreach (var pet in PetCatalog.Pets)
            {
                Assert.That(pet.BondRanks, Is.Not.Null, $"{pet.Id} has no bondRanks.");
                Assert.That(pet.BondRanks.Count, Is.EqualTo(5),
                    $"{pet.Id} must carry the five BOND_STAGES rows (ranks 0..4).");
                for (int rank = 0; rank <= 4; rank++)
                {
                    var row = pet.RankAt(rank);
                    Assert.That(row, Is.Not.Null, $"{pet.Id} missing bond rank {rank}.");
                }
            }
        }

        [Test]
        public void bond_rank_stats_climb_with_rank()
        {
            // petData.ts petAttack(rank) = 9 + rank*5 — strictly increasing.
            foreach (var pet in PetCatalog.Pets)
            {
                var r0 = pet.RankAt(0);
                var r4 = pet.RankAt(4);
                Assert.That(r4.AttackDamage, Is.GreaterThan(r0.AttackDamage),
                    $"{pet.Id} attack must grow from rank 0 to rank 4.");
                Assert.That(r4.MaxHp, Is.GreaterThanOrEqualTo(r0.MaxHp),
                    $"{pet.Id} maxHp must not shrink from rank 0 to rank 4.");
            }
        }

        [Test]
        public void rank_zero_is_the_wandering_stage_with_no_perk()
        {
            foreach (var pet in PetCatalog.Pets)
            {
                var r0 = pet.RankAt(0);
                Assert.That(string.IsNullOrEmpty(r0.Perk), Is.True,
                    $"{pet.Id} rank-0 ('Wandering') must carry no perk.");
            }
        }

        [Test]
        public void rank_at_clamps_out_of_range_indices()
        {
            var pet = PetCatalog.Pets[0];
            Assert.That(pet.RankAt(-5), Is.SameAs(pet.RankAt(0)), "negative rank clamps to 0.");
            Assert.That(pet.RankAt(99), Is.SameAs(pet.RankAt(4)), "over-range rank clamps to 4.");
        }

        [Test]
        public void every_pet_has_combat_tuning_and_a_home_slot()
        {
            var seenSlots = new System.Collections.Generic.HashSet<int>();
            foreach (var pet in PetCatalog.Pets)
            {
                Assert.That(pet.HuntSpeed, Is.GreaterThan(0f), $"{pet.Id} huntSpeed > 0.");
                Assert.That(pet.AttackRange, Is.GreaterThan(0f), $"{pet.Id} attackRange > 0.");
                Assert.That(pet.AttackCooldown, Is.GreaterThan(0f), $"{pet.Id} attackCooldown > 0.");
                Assert.That(pet.SlotIndex, Is.InRange(0, 2), $"{pet.Id} slotIndex must be 0..2.");
                Assert.That(seenSlots.Add(pet.SlotIndex), Is.True,
                    $"{pet.Id} reuses deploy slot {pet.SlotIndex} — slots must be unique.");
            }
        }

        // =====================================================================
        //  Deploy-ring geometry — petPost() port
        // =====================================================================

        [Test]
        public void deploy_radius_is_positive()
        {
            Assert.That(PetCatalog.DeployRadius, Is.GreaterThan(0f));
        }

        [Test]
        public void deploy_slot_positions_sit_on_the_ring_around_the_heart()
        {
            var heart = new Vector3(10f, 0f, -4f);
            float r = PetCatalog.DeployRadius;
            for (int slot = 0; slot < 3; slot++)
            {
                var pos = PetCatalog.DeploySlotPosition(slot, heart);
                Assert.That(pos.y, Is.EqualTo(heart.y).Within(1e-4),
                    "deploy slots stay on the Heart's plane.");
                float planarDist = new Vector2(pos.x - heart.x, pos.z - heart.z).magnitude;
                Assert.That(planarDist, Is.EqualTo(r).Within(1e-3),
                    $"slot {slot} must be exactly DeployRadius from the Heart.");
            }
        }

        [Test]
        public void deploy_slot_positions_are_deterministic()
        {
            var heart = Vector3.zero;
            for (int slot = 0; slot < 3; slot++)
            {
                Assert.That(PetCatalog.DeploySlotPosition(slot, heart),
                    Is.EqualTo(PetCatalog.DeploySlotPosition(slot, heart)),
                    "petPost() is a pure function — same inputs, same output.");
            }
        }
    }
}
