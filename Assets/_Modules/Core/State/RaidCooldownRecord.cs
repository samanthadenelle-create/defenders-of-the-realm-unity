// =============================================================================
// RaidCooldownRecord — the persisted PER-CAMP raid cooldown (WO-728 / WO-1134).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State
//
// ONE record per raid camp the player has cleared: when the cooldown started and
// how long it runs. The camp is raidable again once the window elapses.
//
// WHY IT LIVES IN CORE, NOT VILLAGE (the DefenseOutcomeRecord precedent):
//   It is a field on DeNelle.Core.State.SaveSchema.PersistedState. A record type
//   in DeNelle.Village could never be persisted there. Core also means the HUD
//   assembly (Core + Data only) could render "ready in 2h 15m" later without
//   breaching the one enforced cross-assembly invariant (CLAUDE.md §5).
//
// PURE DATA. NO UnityEngine TYPES ON A PERSISTED FIELD — the save wire stays
// human-inspectable JSON.
//
// ⛔ THE CLOCK IS NOT STORED HERE, AND THAT IS THE POINT.
//   StartedUnixMs is written by DeNelle.Village.World.Camps.RaidCooldownService
//   from TimeSource.NowUnixMs() — the SERVER-ANCHORED seam — never from
//   DateTime.UtcNow. A cooldown stamped off the device clock is rolled forward in
//   ten seconds; a monotonic-anchored one cannot be. This record is deliberately
//   dumb: it carries two numbers and judges nothing, so there is exactly ONE place
//   in the codebase that decides what "now" means for a raid cooldown.
//
// ⚠ DURATION IS PERSISTED, NOT RE-DERIVED, ON PURPOSE.
//   A running cooldown keeps the length it was STARTED with. If the balance table
//   is retuned in an update, an in-flight cooldown does not silently lengthen under
//   a player who already paid for the clear — the same reason WO-911 persists the
//   PAID BASKET on a queue job instead of recomputing the cost at cancel time.
//
// NO SCHEMA BUMP. Nullable on the wire per the .partial() convention; absent on an
// older save -> GameState's empty-list initializer, which is byte-identical to
// today's behaviour (no camp has a cooldown, every camp is raidable). A version
// bump on a LIVE published game is an OWNER decision and nothing here needs one:
// there is no field to rewrite and no old shape to reinterpret.
// =============================================================================

using System;
using Newtonsoft.Json;

namespace DeNelle.Core.State
{
    /// <summary>
    /// One camp's raid cooldown: the scene-config id, the instant the window opened
    /// (unix-ms, from the server-anchored clock seam), and how long it runs.
    /// </summary>
    [Serializable]
    public class RaidCooldownRecord
    {
        /// <summary>The scene-config id of the raid camp (e.g. "raider_camp_small").
        /// Matches SceneConfigDef.id and RaidClaimService's claim key.</summary>
        [JsonProperty("configId")] public string ConfigId;

        /// <summary>
        /// Unix-ms the cooldown window OPENED, stamped from
        /// <c>DeNelle.Village.TimeSource.NowUnixMs()</c> — server-anchored when a
        /// handshake has happened this process. NEVER DateTime.UtcNow.
        /// </summary>
        [JsonProperty("startedUnixMs")] public double StartedUnixMs;

        /// <summary>
        /// How long this window runs, in seconds, FROZEN at the moment it opened
        /// (see the file header: a retune must not lengthen a cooldown already
        /// running). &lt;= 0 means "no cooldown" and the record is inert.
        /// </summary>
        [JsonProperty("durationSeconds")] public double DurationSeconds;

        /// <summary>
        /// True when the clock was server-anchored at the moment the window opened.
        /// PURELY DIAGNOSTIC — nothing branches on it and nothing may start doing so.
        /// It exists so a capture (or a later server-side reconcile) can tell an
        /// honest offline clear from a suspicious one WITHOUT the client punishing
        /// anybody for a cold launch, which is always unanchored (the WO-1128
        /// "refuse server-side, never punish client-side" rule).
        /// </summary>
        [JsonProperty("serverAnchored")] public bool ServerAnchored;

        public RaidCooldownRecord() { }

        public RaidCooldownRecord(string configId, double startedUnixMs, double durationSeconds, bool serverAnchored)
        {
            ConfigId = configId;
            StartedUnixMs = startedUnixMs;
            DurationSeconds = durationSeconds;
            ServerAnchored = serverAnchored;
        }

        /// <summary>
        /// Repairs a record read off the wire so no reader can meet a null id, a NaN, or a
        /// negative duration from a partial/older/hand-edited save. Never throws; never
        /// returns null (a null input becomes an inert zero record, not a crash).
        /// </summary>
        public static RaidCooldownRecord Normalize(RaidCooldownRecord r)
        {
            if (r == null) r = new RaidCooldownRecord();
            if (r.ConfigId == null) r.ConfigId = string.Empty;
            if (double.IsNaN(r.StartedUnixMs) || double.IsInfinity(r.StartedUnixMs) || r.StartedUnixMs < 0d)
                r.StartedUnixMs = 0d;
            if (double.IsNaN(r.DurationSeconds) || double.IsInfinity(r.DurationSeconds) || r.DurationSeconds < 0d)
                r.DurationSeconds = 0d;
            return r;
        }
    }
}
