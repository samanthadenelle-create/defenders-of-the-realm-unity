using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DeNelle.Core.State;
using Newtonsoft.Json;

namespace DeNelle.Core.Social
{
    /// <summary>
    /// Public, layout-only representation of a town. This is an allowlist DTO, deliberately
    /// separate from GameState and the persisted save envelope. It must never grow a raw-save,
    /// wallet, inventory, balance, purchase, or private-roster field.
    /// </summary>
    [Serializable]
    public sealed class PublicTownSnapshot
    {
        public const int CurrentSchemaVersion = 2;

        [JsonProperty("schemaVersion")] public int SchemaVersion = CurrentSchemaVersion;
        [JsonProperty("snapshotId")] public string SnapshotId;
        [JsonProperty("publicOwnerId")] public string PublicOwnerId;
        [JsonProperty("snapshotVersion")] public long SnapshotVersion;
        [JsonProperty("catalogVersion")] public int CatalogVersion;
        [JsonProperty("minimumClientVersion")] public string MinimumClientVersion;

        /// <summary>
        /// Explicit publication intent. New snapshots are private by default. A future server
        /// endpoint must authenticate the owner and choose whether to publish; this flag alone
        /// never grants publication authority.
        /// </summary>
        [JsonProperty("publishRequested")] public bool PublishRequested = false;

        [JsonProperty("structures")] public List<PublicPlacedStructure> Structures =
            new List<PublicPlacedStructure>();
        [JsonProperty("equippedCosmeticSkus")] public List<string> EquippedCosmeticSkus = new List<string>();
        [JsonProperty("publicHeroLineup")] public List<PublicLevelledSku> PublicHeroLineup = new List<PublicLevelledSku>();
        [JsonProperty("publicArmyLineup")] public List<PublicArmySku> PublicArmyLineup = new List<PublicArmySku>();
        [JsonProperty("selectedEchoes")] public List<PublicLevelledSku> SelectedEchoes = new List<PublicLevelledSku>();
        [JsonProperty("echoesSaved")] public int EchoesSaved;
        [JsonProperty("bannerSku")] public string BannerSku;
        [JsonProperty("titleSku")] public string TitleSku;
        [JsonProperty("townLevel")] public int TownLevel = 1;
        [JsonProperty("publicAchievementSkus")] public List<string> PublicAchievementSkus = new List<string>();
        [JsonProperty("leaderboardRank")] public int? LeaderboardRank;
    }

    /// <summary>Sanitized projection of PlacedStructureData's replay-safe layout semantics.</summary>
    [Serializable]
    public sealed class PublicPlacedStructure
    {
        [JsonProperty("itemId")] public string ItemId;
        [JsonProperty("cellX")] public int CellX;
        [JsonProperty("cellZ")] public int CellZ;
        [JsonProperty("yawSteps")] public int YawSteps;
        [JsonProperty("level")] public int Level;
        [JsonProperty("yawOffset")] public float YawOffset;
        [JsonProperty("worldY")] public float WorldY;
        [JsonProperty("wallMounted")] public bool WallMounted;
    }

    [Serializable]
    public class PublicLevelledSku
    {
        [JsonProperty("sku")] public string Sku;
        [JsonProperty("level")] public int Level;
    }

    [Serializable]
    public sealed class PublicArmySku : PublicLevelledSku
    {
        [JsonProperty("count")] public int Count;
    }

    public sealed class PublicTownSnapshotValidation
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; } = new List<string>();
    }

    /// <summary>
    /// Pure sanitizer and bounds validator. It accepts only a layout list rather than GameState,
    /// which makes accidental leakage of unrelated save fields structurally impossible.
    /// </summary>
    public static class PublicTownSnapshotPolicy
    {
        public const int MaxStructures = 300;
        public const int MinCell = -256;
        public const int MaxCell = 256;
        public const int MaxStructureLevel = 20;
        public const int MaxCosmetics = 16;
        public const int MaxHeroes = 4;
        public const int MaxArmyUnits = 12;
        public const int MaxEchoes = 4;
        public const int MaxAchievements = 32;
        public const int MaxPublicLevel = 1000;
        public const int MaxPublicCount = 1000000;

        private static readonly Regex SnapshotId =
            new Regex("^sh_[A-Za-z0-9_-]{16,93}$", RegexOptions.CultureInvariant);
        private static readonly Regex PublicOwnerId =
            new Regex("^po_[A-Za-z0-9_-]{16,93}$", RegexOptions.CultureInvariant);
        private static readonly Regex CatalogId =
            new Regex("^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant);
        private static readonly Regex ClientVersion =
            new Regex("^[0-9A-Za-z][0-9A-Za-z._-]{0,31}$", RegexOptions.CultureInvariant);

        public static PublicTownSnapshot FromLayout(
            IReadOnlyList<PlacedStructureData> layout,
            string snapshotId,
            string publicOwnerId,
            long snapshotVersion,
            int catalogVersion,
            string minimumClientVersion)
        {
            var result = new PublicTownSnapshot
            {
                SnapshotId = snapshotId,
                PublicOwnerId = publicOwnerId,
                SnapshotVersion = snapshotVersion,
                CatalogVersion = catalogVersion,
                MinimumClientVersion = minimumClientVersion,
                PublishRequested = false,
            };

            if (layout == null) return result;
            int count = Math.Min(layout.Count, MaxStructures);
            for (int i = 0; i < count; i++)
            {
                var source = layout[i];
                result.Structures.Add(new PublicPlacedStructure
                {
                    ItemId = source.itemId,
                    CellX = source.cellX,
                    CellZ = source.cellZ,
                    YawSteps = source.yawSteps,
                    Level = source.level,
                    YawOffset = source.yawOffset,
                    WorldY = source.worldY,
                    WallMounted = source.wallMounted,
                });
            }
            return result;
        }

        public static PublicTownSnapshotValidation Validate(PublicTownSnapshot snapshot)
        {
            var result = new PublicTownSnapshotValidation();
            if (snapshot == null)
            {
                result.Errors.Add("snapshot_required");
                return result;
            }

            if (snapshot.SchemaVersion != 1 && snapshot.SchemaVersion != PublicTownSnapshot.CurrentSchemaVersion)
                result.Errors.Add("schema_version_unsupported");
            if (string.IsNullOrEmpty(snapshot.SnapshotId) || !SnapshotId.IsMatch(snapshot.SnapshotId))
                result.Errors.Add("snapshot_id_invalid");
            if (string.IsNullOrEmpty(snapshot.PublicOwnerId) || !PublicOwnerId.IsMatch(snapshot.PublicOwnerId))
                result.Errors.Add("public_owner_id_invalid");
            if (snapshot.SnapshotVersion < 1) result.Errors.Add("snapshot_version_invalid");
            if (snapshot.CatalogVersion < 1) result.Errors.Add("catalog_version_invalid");
            if (string.IsNullOrEmpty(snapshot.MinimumClientVersion) ||
                !ClientVersion.IsMatch(snapshot.MinimumClientVersion))
                result.Errors.Add("minimum_client_version_invalid");

            if (snapshot.Structures == null)
            {
                result.Errors.Add("structures_required");
                return result;
            }
            if (snapshot.Structures.Count > MaxStructures) result.Errors.Add("structures_over_limit");

            for (int i = 0; i < snapshot.Structures.Count; i++)
                ValidateStructure(snapshot.Structures[i], i, result.Errors);

            ValidateSkuList(snapshot.EquippedCosmeticSkus, MaxCosmetics, "cosmetics", result.Errors);
            ValidateLevelledList(snapshot.PublicHeroLineup, MaxHeroes, "hero", result.Errors);
            ValidateArmyList(snapshot.PublicArmyLineup, result.Errors);
            ValidateLevelledList(snapshot.SelectedEchoes, MaxEchoes, "echo", result.Errors);
            ValidateSkuList(snapshot.PublicAchievementSkus, MaxAchievements, "achievements", result.Errors);
            if (snapshot.EchoesSaved < 0 || snapshot.EchoesSaved > MaxPublicCount)
                result.Errors.Add("echoes_saved_invalid");
            ValidateOptionalSku(snapshot.BannerSku, "banner_sku_invalid", result.Errors);
            ValidateOptionalSku(snapshot.TitleSku, "title_sku_invalid", result.Errors);
            if (snapshot.TownLevel < 1 || snapshot.TownLevel > MaxPublicLevel)
                result.Errors.Add("town_level_invalid");
            if (snapshot.LeaderboardRank.HasValue &&
                (snapshot.LeaderboardRank.Value < 1 || snapshot.LeaderboardRank.Value > MaxPublicCount))
                result.Errors.Add("leaderboard_rank_invalid");
            return result;
        }

        /// <summary>Publication requires an explicit owner action in addition to valid data.</summary>
        public static PublicTownSnapshotValidation ValidateForPublication(PublicTownSnapshot snapshot)
        {
            var result = Validate(snapshot);
            if (snapshot != null && !snapshot.PublishRequested)
                result.Errors.Add("publication_opt_in_required");
            return result;
        }

        private static void ValidateStructure(PublicPlacedStructure item, int index, List<string> errors)
        {
            string prefix = "structure_" + index + "_";
            if (item == null) { errors.Add(prefix + "required"); return; }
            if (string.IsNullOrEmpty(item.ItemId) || !CatalogId.IsMatch(item.ItemId))
                errors.Add(prefix + "item_id_invalid");
            if (item.CellX < MinCell || item.CellX > MaxCell ||
                item.CellZ < MinCell || item.CellZ > MaxCell)
                errors.Add(prefix + "cell_out_of_bounds");
            if (item.YawSteps < 0 || item.YawSteps > 3)
                errors.Add(prefix + "yaw_steps_invalid");
            if (item.Level < 1 || item.Level > MaxStructureLevel)
                errors.Add(prefix + "level_invalid");
            if (!Finite(item.YawOffset) || item.YawOffset < -180f || item.YawOffset > 180f)
                errors.Add(prefix + "yaw_offset_invalid");
            if (!Finite(item.WorldY) || item.WorldY < -20f || item.WorldY > 100f)
                errors.Add(prefix + "world_y_invalid");
        }

        private static void ValidateSkuList(List<string> values, int max, string field, List<string> errors)
        {
            if (values == null) { errors.Add(field + "_required"); return; }
            if (values.Count > max) errors.Add(field + "_over_limit");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
                if (string.IsNullOrEmpty(values[i]) || !CatalogId.IsMatch(values[i]) || !seen.Add(values[i]))
                    errors.Add(field + "_" + i + "_sku_invalid");
        }

        private static void ValidateLevelledList(List<PublicLevelledSku> values, int max, string field, List<string> errors)
        {
            if (values == null) { errors.Add(field + "_lineup_required"); return; }
            if (values.Count > max) errors.Add(field + "_lineup_over_limit");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (value == null || string.IsNullOrEmpty(value.Sku) || !CatalogId.IsMatch(value.Sku) ||
                    !seen.Add(value.Sku) || value.Level < 1 || value.Level > MaxPublicLevel)
                    errors.Add(field + "_" + i + "_invalid");
            }
        }

        private static void ValidateArmyList(List<PublicArmySku> values, List<string> errors)
        {
            if (values == null) { errors.Add("army_lineup_required"); return; }
            if (values.Count > MaxArmyUnits) errors.Add("army_lineup_over_limit");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (value == null || string.IsNullOrEmpty(value.Sku) || !CatalogId.IsMatch(value.Sku) ||
                    !seen.Add(value.Sku) || value.Level < 1 || value.Level > MaxPublicLevel ||
                    value.Count < 1 || value.Count > MaxPublicCount)
                    errors.Add("army_" + i + "_invalid");
            }
        }

        private static void ValidateOptionalSku(string value, string error, List<string> errors)
        {
            if (value != null && (value.Length == 0 || !CatalogId.IsMatch(value))) errors.Add(error);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
