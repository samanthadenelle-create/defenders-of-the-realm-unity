using System.Collections.Generic;
using System.Linq;
using DeNelle.Core.Social;
using DeNelle.Core.State;
using NUnit.Framework;
using UnityEngine;

namespace DeNelle.Core.Tests
{
    [TestFixture]
    public sealed class PublicTownSnapshotTests
    {
        private static PublicTownSnapshot ValidSnapshot()
        {
            return PublicTownSnapshotPolicy.FromLayout(
                new List<PlacedStructureData>
                {
                    new PlacedStructureData("tower_archer", 2, -3, 1, 4, 45f, 2.5f, true),
                },
                "sh_7Hy3qP9mN2xK4v8Q",
                "po_Z4c8V1s6Q0rT5y2M",
                3,
                42,
                "2026.08.29");
        }

        [Test]
        public void sanitizer_copies_only_replay_safe_layout_semantics_and_defaults_private()
        {
            var snapshot = ValidSnapshot();
            var item = snapshot.Structures.Single();

            Assert.That(snapshot.PublishRequested, Is.False);
            Assert.That(snapshot.SchemaVersion, Is.EqualTo(PublicTownSnapshot.CurrentSchemaVersion));
            Assert.That(item.ItemId, Is.EqualTo("tower_archer"));
            Assert.That(item.CellX, Is.EqualTo(2));
            Assert.That(item.CellZ, Is.EqualTo(-3));
            Assert.That(item.YawSteps, Is.EqualTo(1));
            Assert.That(item.Level, Is.EqualTo(4));
            Assert.That(item.YawOffset, Is.EqualTo(45f));
            Assert.That(item.WorldY, Is.EqualTo(2.5f));
            Assert.That(item.WallMounted, Is.True);
            Assert.That(PublicTownSnapshotPolicy.Validate(snapshot).IsValid, Is.True);
            Assert.That(PublicTownSnapshotPolicy.ValidateForPublication(snapshot).Errors,
                Does.Contain("publication_opt_in_required"));
        }

        [Test]
        public void serialized_contract_has_no_private_save_or_identity_fields()
        {
            string json = JsonUtility.ToJson(ValidSnapshot()).ToLowerInvariant();
            string[] forbidden =
            {
                "wallet", "saveblob", "inventory", "balance", "resources", "crystals",
                "coins", "purchase", "entitlement", "private", "boundwallet", "accountid",
                "email", "session", "seedphrase", "roster", "rawsave", "authproof"
            };
            foreach (string name in forbidden)
                Assert.That(json, Does.Not.Contain(name), "public snapshot leaked forbidden field: " + name);
        }

        [Test]
        public void public_profile_is_catalog_only_bounded_and_contains_no_private_roster_shape()
        {
            var snapshot = ValidSnapshot();
            snapshot.EquippedCosmeticSkus.Add("skin_castle_firstwatch");
            snapshot.PublicHeroLineup.Add(new PublicLevelledSku { Sku = "hero_knight", Level = 7 });
            snapshot.PublicArmyLineup.Add(new PublicArmySku { Sku = "unit_archer", Level = 4, Count = 12 });
            snapshot.SelectedEchoes.Add(new PublicLevelledSku { Sku = "echo_luma", Level = 3 });
            snapshot.EchoesSaved = 43;
            snapshot.BannerSku = "banner_firstwatch";
            snapshot.TitleSku = "title_watchkeeper";
            snapshot.TownLevel = 8;
            snapshot.PublicAchievementSkus.Add("achievement_wave_7");
            snapshot.LeaderboardRank = 3;
            Assert.That(PublicTownSnapshotPolicy.Validate(snapshot).IsValid, Is.True);

            snapshot.PublicHeroLineup[0].Level = PublicTownSnapshotPolicy.MaxPublicLevel + 1;
            snapshot.PublicArmyLineup[0].Count = 0;
            snapshot.SelectedEchoes.Add(new PublicLevelledSku { Sku = "echo_luma", Level = 1 });
            snapshot.EquippedCosmeticSkus.Add("../../private");
            snapshot.EchoesSaved = -1;
            snapshot.LeaderboardRank = 0;
            var errors = PublicTownSnapshotPolicy.Validate(snapshot).Errors;
            Assert.That(errors, Does.Contain("hero_0_invalid"));
            Assert.That(errors, Does.Contain("army_0_invalid"));
            Assert.That(errors, Does.Contain("echo_1_invalid"));
            Assert.That(errors, Does.Contain("cosmetics_1_sku_invalid"));
            Assert.That(errors, Does.Contain("echoes_saved_invalid"));
            Assert.That(errors, Does.Contain("leaderboard_rank_invalid"));
        }

        [Test]
        public void validator_rejects_unversioned_nonopaque_and_out_of_bounds_payloads()
        {
            var snapshot = ValidSnapshot();
            snapshot.SchemaVersion = 99;
            snapshot.SnapshotId = "wallet-address-looking-value";
            snapshot.PublicOwnerId = "short";
            snapshot.SnapshotVersion = 0;
            snapshot.CatalogVersion = 0;
            snapshot.MinimumClientVersion = "bad version with spaces";
            snapshot.Structures[0].ItemId = "../../private-save";
            snapshot.Structures[0].CellX = PublicTownSnapshotPolicy.MaxCell + 1;
            snapshot.Structures[0].YawSteps = 4;
            snapshot.Structures[0].Level = 0;
            snapshot.Structures[0].YawOffset = float.NaN;
            snapshot.Structures[0].WorldY = float.PositiveInfinity;

            var errors = PublicTownSnapshotPolicy.Validate(snapshot).Errors;
            Assert.That(errors, Does.Contain("schema_version_unsupported"));
            Assert.That(errors, Does.Contain("snapshot_id_invalid"));
            Assert.That(errors, Does.Contain("public_owner_id_invalid"));
            Assert.That(errors, Does.Contain("snapshot_version_invalid"));
            Assert.That(errors, Does.Contain("catalog_version_invalid"));
            Assert.That(errors, Does.Contain("minimum_client_version_invalid"));
            Assert.That(errors, Does.Contain("structure_0_item_id_invalid"));
            Assert.That(errors, Does.Contain("structure_0_cell_out_of_bounds"));
            Assert.That(errors, Does.Contain("structure_0_yaw_steps_invalid"));
            Assert.That(errors, Does.Contain("structure_0_level_invalid"));
            Assert.That(errors, Does.Contain("structure_0_yaw_offset_invalid"));
            Assert.That(errors, Does.Contain("structure_0_world_y_invalid"));
        }

        [Test]
        public void sanitizer_hard_caps_structure_count_without_mutating_source()
        {
            var source = new List<PlacedStructureData>();
            for (int i = 0; i < PublicTownSnapshotPolicy.MaxStructures + 20; i++)
                source.Add(new PlacedStructureData("house", 0, 0, 0, 1));

            var snapshot = PublicTownSnapshotPolicy.FromLayout(source,
                "sh_7Hy3qP9mN2xK4v8Q", "po_Z4c8V1s6Q0rT5y2M", 1, 1, "1.0.0");

            Assert.That(snapshot.Structures.Count, Is.EqualTo(PublicTownSnapshotPolicy.MaxStructures));
            Assert.That(source.Count, Is.EqualTo(PublicTownSnapshotPolicy.MaxStructures + 20));
        }
    }
}
