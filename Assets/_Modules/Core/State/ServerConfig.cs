// =============================================================================
// ServerConfig — backend-controlled remote configuration record.
// -----------------------------------------------------------------------------
// Deserialized from the "config" field of the api/game/load.js response.
// All values are set via Vercel environment variables in the backend dashboard —
// no Unity rebuild or code change needed to adjust live game parameters.
//
// USAGE (read-only at runtime):
//   var cfg = GameStateService.Instance.ServerConfig;
//   if (Random.value <= cfg.BossWaveCrystalDropChance) { ... }
//
// BACKEND CONTRACT:
//   api/game/load.js returns:
//     { "success": true, "data": { ...playerState }, "config": { ...ServerConfig } }
//   Each field maps to a Vercel env var (see field comments below).
//   Missing fields fall back to safe defaults (hardcoded below).
//
// NULL SAFETY:
//   GameStateService.ServerConfig is never null — it returns Default when the
//   backend omits the config block (offline, first boot, old backend version).
// =============================================================================

using Newtonsoft.Json;

namespace DeNelle.Core.State
{
    /// <summary>
    /// Backend-controlled live game parameters. Set via Vercel env vars;
    /// delivered in the config block of every api/game/load response.
    /// All fields are nullable so missing env vars degrade to safe defaults.
    /// </summary>
    public sealed class ServerConfig
    {
        // ── Boss wave crystal drops ───────────────────────────────────────────

        /// <summary>
        /// Probability (0.0–1.0) that a boss wave awards bonus Aether Crystals.
        /// Vercel env: BOSS_CRYSTAL_DROP_CHANCE. Default 0.45 (45%).
        /// </summary>
        [JsonProperty("bossWaveCrystalDropChance")]
        public float? BossWaveCrystalDropChance { get; set; }

        /// <summary>
        /// Minimum Aether Crystals awarded on a successful boss-wave drop.
        /// Vercel env: BOSS_CRYSTAL_MIN. Default 1.
        /// </summary>
        [JsonProperty("bossWaveCrystalMin")]
        public int? BossWaveCrystalMin { get; set; }

        /// <summary>
        /// Maximum Aether Crystals awarded on a successful boss-wave drop.
        /// Vercel env: BOSS_CRYSTAL_MAX. Default 3.
        /// </summary>
        [JsonProperty("bossWaveCrystalMax")]
        public int? BossWaveCrystalMax { get; set; }

        /// <summary>
        /// Every Nth wave is treated as a boss wave for crystal drop purposes.
        /// Vercel env: BOSS_WAVE_INTERVAL. Default 5.
        /// </summary>
        [JsonProperty("bossWaveInterval")]
        public int? BossWaveInterval { get; set; }

        // ── Pack / IAP sales ──────────────────────────────────────────────────

        /// <summary>
        /// True while a store-wide pack sale is active.
        /// Vercel env: PACK_SALE_ACTIVE (true/false). Default false.
        /// </summary>
        [JsonProperty("packSaleActive")]
        public bool? PackSaleActive { get; set; }

        /// <summary>
        /// Percentage discount applied to all packs during a sale (0–100).
        /// Vercel env: PACK_SALE_DISCOUNT_PCT. Default 0.
        /// </summary>
        [JsonProperty("packSaleDiscountPct")]
        public int? PackSaleDiscountPct { get; set; }

        /// <summary>
        /// Human-readable sale label shown on the PackStore banner, e.g. "Summer Sale 🔥".
        /// Vercel env: PACK_SALE_LABEL. Default null (no banner).
        /// </summary>
        [JsonProperty("packSaleLabel")]
        public string PackSaleLabel { get; set; }

        // ── Special events ────────────────────────────────────────────────────

        /// <summary>
        /// Internal slug of the active special event, or null when no event is running.
        /// Vercel env: ACTIVE_EVENT_NAME. Examples: "founders_weekend", "harvest_moon".
        /// </summary>
        [JsonProperty("activeEventName")]
        public string ActiveEventName { get; set; }

        /// <summary>
        /// Bonus Aether Crystals awarded per wave cleared during a special event.
        /// Vercel env: EVENT_BONUS_CRYSTALS. Default 0.
        /// </summary>
        [JsonProperty("eventBonusCrystals")]
        public int? EventBonusCrystals { get; set; }

        /// <summary>
        /// UTC unix timestamp (seconds) when the active event expires.
        /// Vercel env: EVENT_EXPIRY_UTC. Default 0 (never expires while set).
        /// </summary>
        [JsonProperty("eventExpiryUtc")]
        public long? EventExpiryUtc { get; set; }

        /// <summary>
        /// Display string shown in the village HUD / PackStore for the active event,
        /// e.g. "Founders Weekend — double crystals all weekend!".
        /// Vercel env: EVENT_DISPLAY_TEXT. Default null.
        /// </summary>
        [JsonProperty("eventDisplayText")]
        public string EventDisplayText { get; set; }

        // ── Empowerment economy ───────────────────────────────────────────────

        /// <summary>
        /// Multiplier applied to all empowerment crystal costs.
        /// 1.0 = normal, 0.5 = half-price sale, 2.0 = increased cost.
        /// Vercel env: EMPOWERMENT_COST_MULTIPLIER. Default 1.0.
        /// </summary>
        [JsonProperty("empowermentCostMultiplier")]
        public float? EmpowermentCostMultiplier { get; set; }

        // ── Refunds ───────────────────────────────────────────────────────────

        /// <summary>
        /// Fraction of the original crystal cost returned on empowerment refund (0.0–1.0).
        /// 0.5 = 50% back, 1.0 = full refund. Vercel env: CRYSTAL_REFUND_RATE. Default 0.5.
        /// </summary>
        [JsonProperty("crystalRefundRate")]
        public float? CrystalRefundRate { get; set; }

        // ── Maintenance / kill-switch ─────────────────────────────────────────

        /// <summary>
        /// When true the game shows a maintenance banner and disables IAP.
        /// Vercel env: MAINTENANCE_MODE. Default false.
        /// </summary>
        [JsonProperty("maintenanceMode")]
        public bool? MaintenanceMode { get; set; }

        /// <summary>
        /// Optional maintenance message shown to players.
        /// Vercel env: MAINTENANCE_MESSAGE. Default null.
        /// </summary>
        [JsonProperty("maintenanceMessage")]
        public string MaintenanceMessage { get; set; }

        // ── Safe defaults (returned when backend omits the config block) ───────

        /// <summary>
        /// Safe-default instance used when the backend returns no config block.
        /// All gameplay-affecting fields fall back to conservative live values.
        /// </summary>
        public static readonly ServerConfig Default = new ServerConfig
        {
            BossWaveCrystalDropChance  = 0.45f,
            BossWaveCrystalMin         = 1,
            BossWaveCrystalMax         = 3,
            BossWaveInterval           = 5,
            PackSaleActive             = false,
            PackSaleDiscountPct        = 0,
            PackSaleLabel              = null,
            ActiveEventName            = null,
            EventBonusCrystals         = 0,
            EventExpiryUtc             = null,
            EventDisplayText           = null,
            EmpowermentCostMultiplier  = 1.0f,
            CrystalRefundRate          = 0.5f,
            MaintenanceMode            = false,
            MaintenanceMessage         = null,
        };

        // ── Accessor helpers ──────────────────────────────────────────────────

        /// <summary>Boss wave drop chance, falling back to 45% default.</summary>
        public float DropChance        => BossWaveCrystalDropChance ?? Default.BossWaveCrystalDropChance!.Value;
        /// <summary>Min crystals on a boss drop, falling back to 1.</summary>
        public int   DropMin           => BossWaveCrystalMin        ?? Default.BossWaveCrystalMin!.Value;
        /// <summary>Max crystals on a boss drop, falling back to 3.</summary>
        public int   DropMax           => BossWaveCrystalMax        ?? Default.BossWaveCrystalMax!.Value;
        /// <summary>Every Nth wave is a boss wave, falling back to 5.</summary>
        public int   BossInterval      => BossWaveInterval          ?? Default.BossWaveInterval!.Value;
        /// <summary>True if a pack sale is currently active.</summary>
        public bool  SaleActive        => PackSaleActive            ?? false;
        /// <summary>Discount percentage during a sale (0–100).</summary>
        public int   SaleDiscountPct   => PackSaleDiscountPct       ?? 0;
        /// <summary>Bonus crystals per wave during a special event (0 = no event).</summary>
        public int   EventBonus        => EventBonusCrystals        ?? 0;
        /// <summary>Empowerment cost multiplier (1.0 = normal).</summary>
        public float EmpowerMultiplier => EmpowermentCostMultiplier ?? 1.0f;
        /// <summary>Crystal refund fraction (0.5 = 50% back).</summary>
        public float RefundRate        => CrystalRefundRate         ?? 0.5f;
        /// <summary>True while the game is in maintenance mode.</summary>
        public bool  InMaintenance     => MaintenanceMode           ?? false;

        /// <summary>
        /// True if a special event is active and has not yet expired.
        /// </summary>
        public bool IsEventActive()
        {
            if (string.IsNullOrEmpty(ActiveEventName)) return false;
            if (!EventExpiryUtc.HasValue || EventExpiryUtc.Value == 0) return true;
            var now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return now < EventExpiryUtc.Value;
        }
    }
}
