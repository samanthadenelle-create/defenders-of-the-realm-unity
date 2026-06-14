// =============================================================================
// PlayerTroop — the persisted per-troop army record (WO-453 Step 2).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State
//
// One OWNED troop in the player's persisted army (GameState.Army.Owned). The
// army-state twin of the combat-only TroopDef/TroopController (DeNelle.Village):
// TroopDef is the static catalog stat-block, PlayerTroop is the SAVED INSTANCE
// (which footman/archer you own, how veteran it is, whether it is wounded).
//
// LOSS MODEL = WOUNDED-RECOVERY, NO PERMADEATH (owner-decided, WO-453): a downed
// troop is MARKED Wounded + given a recovery countdown — it is NEVER deleted, so
// the player's army roster is stable. A wounded troop is not deployable until its
// RecoveryRemaining ticks to 0 (TickRecovery on ArmyStorage clears the flag).
//
// ASSEMBLY NOTE: this lives in DeNelle.Core (NOT Village) — same reason as
// PlacedDefenderData/PlacedStructureData: GameState.Army is a Core field and Core
// may not reference Village (CS0234 circular). Pure data — no Village types, no
// Unity refs beyond [Serializable]. Round-trips through the save layer
// (SaveSchema / GameState), so it stays JSON-friendly (Newtonsoft) and additive.
// The TroopDef stat lookup (slots / cost) is supplied BY Village at the call site
// (ArmyStorage's resolver delegates), so this type never reaches into the catalog.
// =============================================================================

using System;
using Newtonsoft.Json;

namespace DeNelle.Core.State
{
    /// <summary>
    /// One owned, persisted troop instance in the player's army. The saved twin of
    /// the static <c>TroopDef</c> (Village) — identifies WHICH troop type it is
    /// (<see cref="TroopDefId"/>) and carries its mutable per-instance state
    /// (veterancy, wounded/recovery). Never deleted on a loss (wounded-recovery
    /// model); held in <see cref="ArmyStorage.Owned"/>.
    /// </summary>
    [Serializable]
    public sealed class PlayerTroop
    {
        /// <summary>
        /// Max veterancy rank — caps the survivor-bonus ladder. +5% damage per rank,
        /// so rank 6 = +30% (the design cap). Rank starts at 0 (fresh-trained).
        /// </summary>
        public const int MaxVeterancyRank = 6;

        /// <summary>Per-rank damage bonus fraction (+5% per survived 3-star).</summary>
        public const float VeterancyDamagePerRank = 0.05f;

        /// <summary>
        /// Stable instance id — a simple deterministic counter string minted by
        /// <see cref="ArmyStorage"/> (e.g. "troop-7"), NOT a random/Date.now value,
        /// so saves round-trip deterministically. Unique within one army.
        /// </summary>
        [JsonProperty("id")] public string Id;

        /// <summary>The <c>TroopDef</c> id this instance is — e.g. "troop-footman".</summary>
        [JsonProperty("troopDefId")] public string TroopDefId;

        /// <summary>
        /// Veterancy rank 0..<see cref="MaxVeterancyRank"/>. Each survived 3-star raid
        /// adds a rank (<see cref="ArmyStorage.AddVeterancy"/>); drives
        /// <see cref="DamageMultiplier"/>. Fresh-trained = 0.
        /// </summary>
        [JsonProperty("veterancyRank")] public int VeterancyRank;

        /// <summary>
        /// True while this troop is recovering from being downed in a raid (the
        /// loss path — NEVER deletion). Not deployable while wounded.
        /// </summary>
        [JsonProperty("wounded")] public bool Wounded;

        /// <summary>
        /// Seconds of recovery remaining while <see cref="Wounded"/>. Ticked down by
        /// <see cref="ArmyStorage.TickRecovery"/>; on reaching 0 the troop recovers
        /// (Wounded cleared). A plain seconds countdown — matches the simple float/dt
        /// recovery convention (no wall-clock dependency, deterministic on replay).
        /// 0 when healthy.
        /// </summary>
        [JsonProperty("recoveryRemaining")] public float RecoveryRemaining;

        /// <summary>
        /// Damage multiplier from veterancy: 1 + 0.05 * rank, capped at
        /// <see cref="MaxVeterancyRank"/> (rank 6 → 1.30). Always &gt;= 1.
        /// </summary>
        [JsonIgnore]
        public float DamageMultiplier
        {
            get
            {
                int rank = VeterancyRank < 0 ? 0
                    : (VeterancyRank > MaxVeterancyRank ? MaxVeterancyRank : VeterancyRank);
                return 1f + VeterancyDamagePerRank * rank;
            }
        }

        /// <summary>True when the troop can be sent on a raid (healthy, not wounded).</summary>
        [JsonIgnore]
        public bool IsDeployable => !Wounded;

        public PlayerTroop() { }

        public PlayerTroop(string id, string troopDefId)
        {
            Id = id;
            TroopDefId = troopDefId;
            VeterancyRank = 0;
            Wounded = false;
            RecoveryRemaining = 0f;
        }
    }
}
