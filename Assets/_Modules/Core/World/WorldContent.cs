// =============================================================================
// WorldContent — pure-data records for the open-world territory loop
// (WO-159 node settlements + WO-160 wandering tribes). Lives in DeNelle.Core so
// Village/World runtime can persist them through GameState without a Village ref.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.World
//
// These are the two halves of ONE loop (WO-159 ⇄ WO-160):
//   • SettlementState — the player's claimed harvesting outpost on a node. Auto-
//     harvests the node's finite reserve into the wallet and DEFENDS it.
//   • TribeState      — a persistent roaming raider band. Materialises into live
//     enemies when the player is near (radius gate) and writes its remaining/
//     cleared state back when they leave. The THREAT that razes an undefended
//     settlement.
//
// PERSISTENCE (mirrors the ZoneState precedent in GameState.Zones): all records
// are JsonUtility-safe — public fields, no dictionaries, enums serialise as int —
// so they round-trip inside GameState lists. They are APPENDED to GameState (see
// GameState.Tribes / GameState.Settlements) and are NOT yet wired into
// SaveSchema/SaveMigrator: the save owner adds the (de)serialisation + bumps the
// schema version, exactly as the comment on GameState.Zones documents. Until then
// they live correctly in-memory and the world systems function within a session.
//
// All "richness / raid-size / threat" numbers are DATA here — never hard-coded at
// a call site — so designers tune the danger⇄reward dial (shared with WO-144/155/
// ZoneManager.ThreatLevel) without touching runtime code.
// =============================================================================
using System;
using System.Collections.Generic;

namespace DeNelle.Core.World
{
    // =========================================================================
    //  WO-160 — Wandering Tribes
    // =========================================================================

    /// <summary>
    /// Authoring template for one wandering tribe (WO-160). Immutable design data:
    /// where it anchors, how big it rolls, how it scales. The mutable per-player
    /// record is <see cref="TribeState"/>. JsonUtility-safe.
    /// </summary>
    [Serializable]
    public class TribeDef
    {
        /// <summary>Stable tribe id (e.g. "ashwood-cult-1"). The TribeState key.</summary>
        public string Id = "";
        /// <summary>Region this tribe belongs to — the <see cref="RegionId"/> enum NAME.</summary>
        public string RegionKey = "";
        /// <summary>World-space anchor (camp centre) the tribe roams around / spawns at.</summary>
        public WorldPoint Anchor = new WorldPoint();
        /// <summary>Player distance (m) that flips the tribe ACTIVE (members spawn).</summary>
        public float ActivationRadius = 45f;
        /// <summary>
        /// Player distance (m) beyond which the tribe goes DORMANT (members despawn).
        /// Must be &gt; <see cref="ActivationRadius"/> — the gap is the hysteresis band
        /// that stops the tribe thrashing on/off at the edge.
        /// </summary>
        public float DeactivationRadius = 60f;

        // ── Raid-size band (WO-160: randomised WITHIN the region's threat band) ──
        /// <summary>Minimum members a spawn/raid rolls (the "easy" end of the swing).</summary>
        public int MinMembers = 3;
        /// <summary>Maximum members a spawn/raid rolls (the "brutal" end of the swing).</summary>
        public int MaxMembers = 6;

        /// <summary>
        /// Region enemy-roster ids this tribe is composed of (WO-155). Empty ⇒ the
        /// spawner falls back to a region-appropriate generic grunt. Each spawned
        /// member picks from this list; ThreatLevel scales its stats.
        /// </summary>
        public List<string> RosterIds = new List<string>();

        public TribeDef() { }

        public TribeDef(string id, RegionId region, WorldPoint anchor,
                        int minMembers, int maxMembers)
        {
            Id = id;
            RegionKey = region.ToString();
            Anchor = anchor;
            MinMembers = minMembers;
            MaxMembers = maxMembers;
        }
    }

    /// <summary>
    /// The persisted, per-player record for a tribe (WO-160). Carries everything the
    /// radius-activation + state-saving mechanics need so a half-cleared tribe stays
    /// half-cleared and a wiped one returns smaller. Seeded from a <see cref="TribeDef"/>.
    /// JsonUtility-safe — rides directly inside <c>GameState.Tribes</c>.
    /// </summary>
    [Serializable]
    public class TribeState
    {
        /// <summary>Matches <see cref="TribeDef.Id"/>.</summary>
        public string Id = "";
        /// <summary>Region key — the <see cref="RegionId"/> enum NAME (survives enum renumber).</summary>
        public string RegionKey = "";
        /// <summary>Camp anchor (mirrors the def, persisted so the record is self-contained).</summary>
        public WorldPoint Anchor = new WorldPoint();

        /// <summary>
        /// Members still alive in the tribe. -1 = "not yet rolled" (the next activation
        /// rolls a fresh raid size within the def band). 0 = wiped this cycle. &gt;0 = a
        /// damaged tribe returns at this reduced count.
        /// </summary>
        public int MembersRemaining = -1;

        /// <summary>True the instant the player wipes every member (drives reduced respawn).</summary>
        public bool Cleared;

        /// <summary>
        /// How many times the player has wiped this tribe. Each clear scales the next
        /// respawn DOWN (owner: reduced, not full-strength, not permanently gone) until
        /// it floors out / vanishes after enough clears. Tuned by the manager curve.
        /// </summary>
        public int ClearCount;

        /// <summary>Unix-ms the tribe was last de-activated (last-seen). 0 = never.</summary>
        public double LastSeenAtMs;

        public TribeState() { }

        public TribeState(TribeDef def)
        {
            if (def == null) return;
            Id = def.Id;
            RegionKey = def.RegionKey;
            Anchor = def.Anchor;
            MembersRemaining = -1;   // unrolled — first activation rolls the band
            Cleared = false;
            ClearCount = 0;
        }
    }

    // =========================================================================
    //  WO-159 — Node Settlements
    // =========================================================================

    /// <summary>
    /// The lifecycle phase of a claimed node settlement (WO-159). Stable enum —
    /// append, never renumber (persisted as int inside <see cref="SettlementState"/>).
    /// </summary>
    public enum SettlementPhase
    {
        /// <summary>Standing + harvesting the node's reserve.</summary>
        Active = 0,
        /// <summary>Node mined empty — the settlement REMAINS as a plain held outpost.</summary>
        Outpost = 1,
        /// <summary>Overrun and destroyed — the site is razed + locked (see RazedUntilDay).</summary>
        Razed = 2,
    }

    /// <summary>
    /// The persisted record for a player-built node settlement (WO-159). One per
    /// claimed node site. Tracks the claim, harvest drain, defence HP, and the
    /// razed-site lockout. JsonUtility-safe — rides inside <c>GameState.Settlements</c>.
    /// </summary>
    [Serializable]
    public class SettlementState
    {
        /// <summary>Stable site id — derived from the claimed node (the node's id / position key).</summary>
        public string SiteId = "";
        /// <summary>Region key — the <see cref="RegionId"/> enum NAME.</summary>
        public string RegionKey = "";
        /// <summary>World-space position of the settlement (= the claimed node site).</summary>
        public WorldPoint Position = new WorldPoint();

        /// <summary>Lifecycle phase.</summary>
        public SettlementPhase Phase = SettlementPhase.Active;

        // ── Defence (the "protect" half) ─────────────────────────────────────
        /// <summary>Current settlement HP. Drops under raid; 0 ⇒ destroyed/razed.</summary>
        public float Hp = 100f;
        /// <summary>Max settlement HP (garrison + structures the player invested).</summary>
        public float MaxHp = 100f;

        // ── Razed-site lockout (owner-locked: 3 game days) ───────────────────
        /// <summary>
        /// Game-day the razed site clears and becomes re-claimable. 0 = not razed.
        /// Set on destruction to <c>currentGameDay + LockoutDays</c>; build-mode rejects
        /// placement here until the game-day clock passes it. Reads the EXISTING
        /// game-day clock — no parallel time system.
        /// </summary>
        public int RazedUntilDay;

        public SettlementState() { }

        public SettlementState(string siteId, RegionId region, WorldPoint pos, float maxHp)
        {
            SiteId = siteId;
            RegionKey = region.ToString();
            Position = pos;
            MaxHp = maxHp;
            Hp = maxHp;
            Phase = SettlementPhase.Active;
        }

        /// <summary>True while the settlement is standing and actively harvesting.</summary>
        public bool IsActive => Phase == SettlementPhase.Active;

        /// <summary>True when the site is razed and still inside its game-day lockout.</summary>
        public bool IsLockedAt(int currentGameDay) =>
            Phase == SettlementPhase.Razed && currentGameDay < RazedUntilDay;
    }

    // =========================================================================
    //  Shared — a JsonUtility-safe world point (Vector3 serialises fine, but a
    //  plain x/y/z struct keeps these records engine-neutral + headless-testable).
    // =========================================================================

    /// <summary>A plain x/y/z world point — JsonUtility/engine-neutral, headless-safe.</summary>
    [Serializable]
    public struct WorldPoint
    {
        public float x;
        public float y;
        public float z;

        public WorldPoint(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
}
