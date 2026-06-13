// =============================================================================
// SaveSchema — the strongly-typed save shape + validation (spec §2.5)
// -----------------------------------------------------------------------------
// The C# analog of src/store/saveSchema.ts (the Zod schema). Defines:
//   - the SaveFile envelope  (the React `SaveExport` — format/storeVersion/...)
//   - PersistedState         (the 41-field payload — every field nullable so a
//                             partial save deserializes, mirroring `.partial()`)
//   - Validate()             (the C# port of `safeParse` — rejects NaN/Infinity,
//                             clamps numerics via NonNegInt/FiniteInt rules)
//
// CurrentVersion = 10, FileFormat = 1. PlayerPrefs key `dotr-save` replaces the
// React localStorage key (storage layer mandated by the port spec — improvement
// #4 NOT adopted).
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DeNelle.Core.State
{
    /// <summary>
    /// Save-file constants, the persisted shape, the envelope and the validator.
    /// </summary>
    public static class SaveSchema
    {
        // ── Versioning ───────────────────────────────────────────────────────
        /// <summary>CURRENT_SCHEMA_VERSION — bumped whenever the persisted shape changes.</summary>
        public const int CurrentVersion = 21;  // v21 — node-settlement persistence (WO-159: claim/HP/phase + 3-day razed lockout survive save/load); v20 — gear inventory persistence (shop purchases survive reload + Neon sync); v19 — arenaDefense placed-defender layout (WO-389); v18 — fold AetherCrystals into Resources.Crystals (single-source-of-truth); v17 — zone graph persistence (WO-164); v16 — party roster (WO-301); v15 — magic tech-axis currency (DEF-121/WO-230); v14 — baseLayout (WO-108); v13 — buildJobs + adSkip (WO-172)
        /// <summary>SaveExport.format — bumped only if the envelope shape changes.</summary>
        public const int FileFormat = 1;

        // ── Storage keys ─────────────────────────────────────────────────────
        /// <summary>PlayerPrefs key holding the live save (React localStorage 'dotr-save').</summary>
        public const string PlayerPrefsKey = "dotr-save";
        /// <summary>Legacy standalone audio/difficulty store key — read+removed by v8→v9.</summary>
        public const string LegacySettingsKey = "realm-defenders-settings";

        // ── Engine constants the save layer needs ────────────────────────────
        /// <summary>Id of the starter dungeon, seeded discovered from a fresh save.</summary>
        public const string StarterDungeonId = "healers_cottage";

        /// <summary>
        /// Shared Newtonsoft settings — registers the global string-enum converter
        /// and the TutorialStep converter so every save round-trips to the EXACT
        /// React wire strings. Used by both Save() and Load() in the service.
        /// </summary>
        public static JsonSerializerSettings JsonSettings
        {
            get
            {
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Include,
                    Formatting = Formatting.None,
                };
                // StringEnumConverter honours [EnumMember] — kebab/lowercase wire values.
                settings.Converters.Add(new StringEnumConverter());
                settings.Converters.Add(new TutorialStepConverter());
                return settings;
            }
        }

        // =====================================================================
        //  SaveFile — the SaveExport envelope (the live save + downloadable file)
        // =====================================================================

        /// <summary>
        /// The save-file envelope — the React <c>SaveExport</c>. Stored verbatim
        /// in PlayerPrefs (wallet/exportedAt are harmless extra metadata on the
        /// live save, and keep one shape across live-save and exported-file).
        /// </summary>
        [Serializable]
        public sealed class SaveFile
        {
            /// <summary>File format version — NOT the schema version. Always 1.</summary>
            [JsonProperty("format")] public int Format = FileFormat;
            /// <summary>Mirrors CURRENT_SCHEMA_VERSION so imports can pick the migrate target.</summary>
            [JsonProperty("storeVersion")] public int StoreVersion = CurrentVersion;
            /// <summary>ISO-8601 timestamp the save was written.</summary>
            [JsonProperty("exportedAt")] public string ExportedAt;
            /// <summary>Wallet the save is tagged to, or null.</summary>
            [JsonProperty("wallet")] public string Wallet;
            /// <summary>The 41-field persisted payload.</summary>
            [JsonProperty("state")] public PersistedState State = new PersistedState();
        }

        // =====================================================================
        //  PersistedState — the 41-field payload (the persistedStateSchema)
        // =====================================================================

        /// <summary>
        /// The persisted slice of GameState — every field nullable so a partial
        /// save (older/newer build) still deserializes, mirroring Zod's
        /// <c>.partial()</c>. Newtonsoft drops unknown extra keys (e.g. the stale
        /// <c>prepTimerLocked</c>), matching <c>.partial()</c>'s tolerance.
        /// </summary>
        [Serializable]
        public sealed class PersistedState
        {
            [JsonProperty("pets")] public List<PetData> Pets;
            [JsonProperty("starterPetId")] public string StarterPetId;
            // Audit P2 (save-load): the player-named starter pet (WO-277) must round-trip;
            // additive-default-on-read — null on old saves keeps the SO's value.
            [JsonProperty("petName")] public string PetName;
            [JsonProperty("onboarded")] public bool? Onboarded;
            [JsonProperty("bestWave")] public double? BestWave;
            [JsonProperty("resources")] public ResourceBalance? Resources;
            [JsonProperty("ownedItemIds")] public List<string> OwnedItemIds;
            [JsonProperty("petBonds")] public List<double> PetBonds;
            [JsonProperty("voidshards")] public double? Voidshards;
            [JsonProperty("towers")] public List<double> Towers;
            [JsonProperty("towerAbilities")] public List<double> TowerAbilities;
            [JsonProperty("wallLevel")] public double? WallLevel;
            [JsonProperty("stone")] public double? Stone;
            [JsonProperty("iron")] public double? Iron;
            [JsonProperty("wood")] public double? Wood;
            [JsonProperty("buildingCooldowns")] public Dictionary<string, double> BuildingCooldowns;
            [JsonProperty("pendingBuilds")] public List<PendingTowerBuild> PendingBuilds;
            [JsonProperty("tutorialStep")] public TutorialStep? TutorialStep;
            [JsonProperty("joystickSensitivity")] public double? JoystickSensitivity;
            [JsonProperty("movementStyle")] public MovementStyle? MovementStyle;
            [JsonProperty("muted")] public bool? Muted;
            [JsonProperty("musicVolume")] public double? MusicVolume;
            [JsonProperty("sfxVolume")] public double? SfxVolume;
            [JsonProperty("difficulty")] public Difficulty? Difficulty;
            [JsonProperty("voiceOvers")] public bool? VoiceOvers;
            [JsonProperty("ownedPets")] public List<PetSpecies> OwnedPets;
            [JsonProperty("seenTutorials")] public Dictionary<string, bool> SeenTutorials;
            [JsonProperty("boundWallet")] public string BoundWallet;
            [JsonProperty("heroClass")] public HeroClass? HeroClass;
            [JsonProperty("inventory")] public AtbInventory? Inventory;
            [JsonProperty("gearInventory")] public Dictionary<string, int> GearInventory;  // v20 — shop-bought gear counts (persists + Neon-syncs); null on old saves → defaults empty
            [JsonProperty("atbLossStreak")] public double? AtbLossStreak;
            [JsonProperty("breachStyle")] public BreachStyle? BreachStyle;
            [JsonProperty("buildingDamage")] public Dictionary<string, double> BuildingDamage;
            [JsonProperty("dungeons")] public DungeonProgress Dungeons;
            [JsonProperty("activeDungeonRun")] public ActiveDungeonRun ActiveDungeonRun;
            [JsonProperty("quests")] public QuestProgress Quests;
            [JsonProperty("regions")] public RegionProgress Regions;
            [JsonProperty("myInviteCode")] public string MyInviteCode;
            [JsonProperty("contacts")] public List<ChatContact> Contacts;
            [JsonProperty("blockedCodes")] public List<string> BlockedCodes;
            [JsonProperty("inbox")] public List<ChatMessage> Inbox;
            [JsonProperty("lastInboxSyncAt")] public double? LastInboxSyncAt;

            // ── v11 — Tower Empowerment (DEPRECATED v18) ─────────────────────────
            /// <summary>
            /// DEPRECATED (save v18) — the legacy Aether Crystals balance was folded into
            /// <c>resources.crystals</c> (the single source of truth) by SaveMigrator
            /// MigrateToV18. The JsonProperty is KEPT (removing it is a breaking change)
            /// but is no longer written meaningfully — it serializes as 0. Read crystals
            /// from <c>resources.crystals</c>.
            /// </summary>
            [JsonProperty("aetherCrystals")] public double? AetherCrystals;

            // ── v12 — Offline harvest accrual (WO-115) ───────────────────────────
            /// <summary>
            /// Unix-ms of the last offline-harvest accrual claim. Nullable per the
            /// <c>.partial()</c> convention; absent on an older save → defaults to 0 on
            /// load (no retroactive haul), so no explicit migration step is needed
            /// (same additive-default-on-read pattern as <c>aetherCrystals</c>).
            /// </summary>
            [JsonProperty("lastHarvestClaimMs")] public double? LastHarvestClaimMs;

            // ── v13 — Build/upgrade timers + ad-skip (WO-172) ────────────────────
            /// <summary>
            /// In-flight construction/upgrade jobs. Absent on an older save → defaults
            /// to an empty list on load (no jobs), so no explicit migration step is
            /// needed (same additive-default-on-read pattern as <c>lastHarvestClaimMs</c>).
            /// </summary>
            [JsonProperty("buildJobs")] public List<BuildJobData> BuildJobs;

            /// <summary>Rewarded-ad build-skips used in the current local day (daily cap). Absent → 0.</summary>
            [JsonProperty("adSkipsUsedToday")] public double? AdSkipsUsedToday;

            /// <summary>Local-day key the ad-skip counter belongs to. Absent → null (counter resets on first claim).</summary>
            [JsonProperty("adSkipDayKey")] public string AdSkipDayKey;

            // ── v14 — Player build mode base layout (WO-108) ─────────────────────
            /// <summary>
            /// The player's placed-structure base layout. Nullable per the
            /// <c>.partial()</c> convention; absent on an older save → the v13→v14
            /// migration step seeds it to an empty list (existing players keep the
            /// default VillageSceneBuilder village until they first build + save).
            /// </summary>
            [JsonProperty("baseLayout")] public List<PlacedStructureData> BaseLayout;

            // ── v15 — Magic tech-axis currency (DEF-121 / WO-230) ────────────────
            /// <summary>
            /// Magic — the building-upgrade tech-tree gating currency (NOT a harvestable).
            /// Nullable per the <c>.partial()</c> convention; absent on an older save →
            /// defaults to 0 on load (no retroactive grant), so no explicit migration
            /// step is needed (same additive-default-on-read pattern as <c>aetherCrystals</c>).
            /// </summary>
            [JsonProperty("magic")] public double? Magic;

            // ── v16 — Party roster (WO-301) ──────────────────────────────────────
            /// <summary>
            /// The persisted party roster — companion ids in join order. Nullable per
            /// the <c>.partial()</c> convention; absent on an older save → defaults to an
            /// empty party on load (no companions until one joins), so no explicit
            /// migration step is needed (same additive-default-on-read pattern as
            /// <c>baseLayout</c>/<c>magic</c>).
            /// </summary>
            [JsonProperty("partyMemberIds")] public List<string> PartyMemberIds;

            // ── v17 — Zone graph persistence (WO-164) ────────────────────────────
            /// <summary>
            /// Per-region zone records — discovery/clear flags, the neighbor graph, and
            /// the City/Horde destination tag (WO-164). Seeded from
            /// <see cref="DeNelle.Core.World.ZoneManager.DefaultZoneGraph"/> on a fresh
            /// save (or backfilled on load for a pre-v17 save). Append-only field at the
            /// END so older saves stay loadable. ZoneState stores its enum keys as NAMES
            /// (strings) deliberately, so it survives enum renumbering.
            /// </summary>
            [JsonProperty("zones")] public List<DeNelle.Core.World.ZoneState> Zones;

            // ── v19 — Arena defense placed-defender layout (WO-389) ──────────────
            /// <summary>
            /// The player's pre-placed Arena DEFENDERS — the CoC-style defense layer
            /// (a List of <see cref="PlacedDefenderData"/>, SEPARATE from the
            /// <c>baseLayout</c> base buildings). Nullable per the <c>.partial()</c>
            /// convention; absent on an older save → defaults to an empty list on load
            /// (no pre-placed defense until the player sets one up), so no explicit
            /// migration step is needed (same additive-default-on-read pattern as
            /// <c>magic</c>/<c>partyMemberIds</c>). Append-only field at the END so
            /// older saves stay loadable.
            /// </summary>
            [JsonProperty("arenaDefense")] public List<PlacedDefenderData> ArenaDefense;

            // ── v21 — Node-settlement persistence (WO-159) ───────────────────────
            /// <summary>
            /// Per-site node-settlement records (WO-159) — the claim/harvest/defend/
            /// deplete loop's persisted state: claim phase, defence HP, and the
            /// razed-site game-day lockout. Nullable per the <c>.partial()</c>
            /// convention; absent on an older save → the v20→v21 migration step seeds
            /// it to an empty list (no claimed settlements until the player builds one),
            /// so claims/HP/3-day lockout survive a save/load round-trip instead of
            /// evaporating. Append-only field at the END so older saves stay loadable.
            /// <see cref="DeNelle.Core.World.SettlementState"/> stores its region as the
            /// enum NAME (string) so it survives enum renumbering.
            /// </summary>
            [JsonProperty("settlements")] public List<DeNelle.Core.World.SettlementState> Settlements;
        }

        // =====================================================================
        //  Numeric clamp helpers — the C# port of the Zod nonNegInt / finiteInt
        // =====================================================================

        /// <summary>
        /// <c>nonNegInt</c> — requires a finite number, transforms to
        /// <c>max(0, floor(n))</c>. Throws <see cref="SaveValidationException"/>
        /// for NaN / ±Infinity.
        /// </summary>
        public static int NonNegInt(double n, string fieldPath)
        {
            RequireFinite(n, fieldPath);
            var floored = Math.Floor(n);
            return (int)Math.Max(0.0, floored);
        }

        /// <summary>
        /// <c>finiteInt</c> — requires a finite number, transforms to
        /// <c>floor(n)</c> (may be negative).
        /// </summary>
        public static long FiniteInt(double n, string fieldPath)
        {
            RequireFinite(n, fieldPath);
            return (long)Math.Floor(n);
        }

        /// <summary>Rejects NaN / ±Infinity (the Zod <c>Number.isFinite</c> refine).</summary>
        public static double RequireFinite(double n, string fieldPath)
        {
            if (double.IsNaN(n) || double.IsInfinity(n))
                throw new SaveValidationException(fieldPath, "must be a finite number");
            return n;
        }

        // =====================================================================
        //  Validate — the C# port of persistedStateSchema.safeParse
        // =====================================================================

        /// <summary>
        /// Validates and clamps a parsed <see cref="PersistedState"/> in place,
        /// mirroring <c>persistedStateSchema.safeParse</c>: rejects NaN/Infinity
        /// numerics, clamps via the NonNegInt/FiniteInt rules (§2.1), and reports
        /// the first bad field path on failure (mirrors the React
        /// "invalid field: &lt;path&gt;" toast). Volumes / joystick sensitivity
        /// are finite-checked only — the React schema does NOT clamp them on load.
        /// </summary>
        public static SaveValidationResult Validate(PersistedState raw)
        {
            if (raw == null)
                return SaveValidationResult.Failure("state", "missing payload");

            try
            {
                // ── Resources / currencies → nonNegInt ───────────────────────
                if (raw.Resources.HasValue)
                {
                    var r = raw.Resources.Value;
                    r.Crystals = NonNegInt(r.Crystals, "resources.crystals");
                    r.Food = NonNegInt(r.Food, "resources.food");
                    r.Coins = NonNegInt(r.Coins, "resources.coins");
                    raw.Resources = r;
                }
                if (raw.BestWave.HasValue) raw.BestWave = NonNegInt(raw.BestWave.Value, "bestWave");
                if (raw.Voidshards.HasValue) raw.Voidshards = NonNegInt(raw.Voidshards.Value, "voidshards");
                if (raw.Stone.HasValue) raw.Stone = NonNegInt(raw.Stone.Value, "stone");
                if (raw.Iron.HasValue) raw.Iron = NonNegInt(raw.Iron.Value, "iron");
                if (raw.Wood.HasValue) raw.Wood = NonNegInt(raw.Wood.Value, "wood");
                if (raw.WallLevel.HasValue) raw.WallLevel = NonNegInt(raw.WallLevel.Value, "wallLevel");
                if (raw.AtbLossStreak.HasValue) raw.AtbLossStreak = NonNegInt(raw.AtbLossStreak.Value, "atbLossStreak");
                if (raw.AetherCrystals.HasValue) raw.AetherCrystals = NonNegInt(raw.AetherCrystals.Value, "aetherCrystals");
                if (raw.Magic.HasValue) raw.Magic = NonNegInt(raw.Magic.Value, "magic");

                // ── Integer arrays → nonNegInt per entry ─────────────────────
                ClampNonNegList(raw.PetBonds, "petBonds");
                ClampNonNegList(raw.Towers, "towers");
                ClampNonNegList(raw.TowerAbilities, "towerAbilities");

                // ── Pets → level / xp nonNegInt ──────────────────────────────
                if (raw.Pets != null)
                {
                    for (var i = 0; i < raw.Pets.Count; i++)
                    {
                        var p = raw.Pets[i];
                        if (p == null) continue;
                        p.Level = NonNegInt(p.Level, $"pets.{i}.level");
                        p.Xp = NonNegInt(p.Xp, $"pets.{i}.xp");
                    }
                }

                // ── Inventory → nonNegInt ────────────────────────────────────
                if (raw.Inventory.HasValue)
                {
                    var inv = raw.Inventory.Value;
                    inv.Potions = NonNegInt(inv.Potions, "inventory.potions");
                    inv.ManaCrystals = NonNegInt(inv.ManaCrystals, "inventory.manaCrystals");
                    inv.Cleanses = NonNegInt(inv.Cleanses, "inventory.cleanses");
                    inv.Torches = NonNegInt(inv.Torches, "inventory.torches");
                    raw.Inventory = inv;
                }

                // ── Pending builds → slot/ability nonNegInt, finishAt finiteInt ─
                if (raw.PendingBuilds != null)
                {
                    for (var i = 0; i < raw.PendingBuilds.Count; i++)
                    {
                        var pb = raw.PendingBuilds[i];
                        pb.Slot = NonNegInt(pb.Slot, $"pendingBuilds.{i}.slot");
                        pb.Ability = NonNegInt(pb.Ability, $"pendingBuilds.{i}.ability");
                        pb.FinishAt = FiniteInt(pb.FinishAt, $"pendingBuilds.{i}.finishAt");
                        raw.PendingBuilds[i] = pb;
                    }
                }

                // ── ActiveDungeonRun → LootStash ints nonNegInt, startedAt finiteInt ─
                if (raw.ActiveDungeonRun != null)
                {
                    raw.ActiveDungeonRun.StartedAt =
                        FiniteInt(raw.ActiveDungeonRun.StartedAt, "activeDungeonRun.startedAt");
                    var loot = raw.ActiveDungeonRun.Loot;
                    if (loot != null)
                    {
                        loot.Crystals = NonNegInt(loot.Crystals, "activeDungeonRun.loot.crystals");
                        loot.Food = NonNegInt(loot.Food, "activeDungeonRun.loot.food");
                        loot.Coins = NonNegInt(loot.Coins, "activeDungeonRun.loot.coins");
                        loot.Stone = NonNegInt(loot.Stone, "activeDungeonRun.loot.stone");
                        loot.Iron = NonNegInt(loot.Iron, "activeDungeonRun.loot.iron");
                        loot.Wood = NonNegInt(loot.Wood, "activeDungeonRun.loot.wood");
                    }
                }

                // ── Chat messages → sentAt / readAt finiteInt ────────────────
                if (raw.Inbox != null)
                {
                    for (var i = 0; i < raw.Inbox.Count; i++)
                    {
                        var m = raw.Inbox[i];
                        if (m == null) continue;
                        m.SentAt = FiniteInt(m.SentAt, $"inbox.{i}.sentAt");
                        if (m.ReadAt.HasValue)
                            m.ReadAt = FiniteInt(m.ReadAt.Value, $"inbox.{i}.readAt");
                    }
                }
                if (raw.LastInboxSyncAt.HasValue)
                    raw.LastInboxSyncAt = FiniteInt(raw.LastInboxSyncAt.Value, "lastInboxSyncAt");
                if (raw.LastHarvestClaimMs.HasValue)
                    raw.LastHarvestClaimMs = FiniteInt(raw.LastHarvestClaimMs.Value, "lastHarvestClaimMs");

                // ── Build jobs (WO-172) → startMs/durationMs finiteInt; counter nonNegInt ─
                if (raw.BuildJobs != null)
                {
                    for (var i = 0; i < raw.BuildJobs.Count; i++)
                    {
                        var j = raw.BuildJobs[i];
                        j.StartMs = FiniteInt(j.StartMs, $"buildJobs.{i}.startMs");
                        j.DurationMs = Math.Max(0, FiniteInt(j.DurationMs, $"buildJobs.{i}.durationMs"));
                        raw.BuildJobs[i] = j;
                    }
                }
                if (raw.AdSkipsUsedToday.HasValue)
                    raw.AdSkipsUsedToday = NonNegInt(raw.AdSkipsUsedToday.Value, "adSkipsUsedToday");

                // ── Zones (WO-164) → default empty list (never null on disk) ─
                if (raw.Zones == null)
                    raw.Zones = new List<DeNelle.Core.World.ZoneState>();

                // ── Settlements (WO-159) → default empty list (never null on disk) ─
                if (raw.Settlements == null)
                    raw.Settlements = new List<DeNelle.Core.World.SettlementState>();

                // ── Volumes / joystick → finite-only (NOT clamped on load) ───
                if (raw.JoystickSensitivity.HasValue)
                    RequireFinite(raw.JoystickSensitivity.Value, "joystickSensitivity");
                if (raw.MusicVolume.HasValue) RequireFinite(raw.MusicVolume.Value, "musicVolume");
                if (raw.SfxVolume.HasValue) RequireFinite(raw.SfxVolume.Value, "sfxVolume");

                return SaveValidationResult.Success(raw);
            }
            catch (SaveValidationException ex)
            {
                return SaveValidationResult.Failure(ex.FieldPath, ex.Reason);
            }
        }

        private static void ClampNonNegList(List<double> list, string fieldPath)
        {
            if (list == null) return;
            for (var i = 0; i < list.Count; i++)
                list[i] = NonNegInt(list[i], $"{fieldPath}.{i}");
        }
    }

    /// <summary>Thrown internally by the clamp helpers; carries the bad field path.</summary>
    public sealed class SaveValidationException : Exception
    {
        public string FieldPath { get; }
        public string Reason { get; }

        public SaveValidationException(string fieldPath, string reason)
            : base($"invalid field: {fieldPath} ({reason})")
        {
            FieldPath = fieldPath;
            Reason = reason;
        }
    }

    /// <summary>The result of <see cref="SaveSchema.Validate"/> — the C# safeParse return.</summary>
    public sealed class SaveValidationResult
    {
        /// <summary>True when the payload validated cleanly.</summary>
        public bool Ok { get; private set; }
        /// <summary>The validated, clamped payload (only when <see cref="Ok"/>).</summary>
        public SaveSchema.PersistedState Data { get; private set; }
        /// <summary>First bad field path (only when not <see cref="Ok"/>).</summary>
        public string FieldPath { get; private set; }
        /// <summary>Why the field was rejected (only when not <see cref="Ok"/>).</summary>
        public string Reason { get; private set; }

        /// <summary>A human-readable failure reason mirroring the React import toast.</summary>
        public string Message => Ok
            ? null
            : $"Save file is corrupt or tampered (invalid field: {FieldPath}).";

        public static SaveValidationResult Success(SaveSchema.PersistedState data)
            => new SaveValidationResult { Ok = true, Data = data };

        public static SaveValidationResult Failure(string fieldPath, string reason)
            => new SaveValidationResult { Ok = false, FieldPath = fieldPath, Reason = reason };
    }
}
