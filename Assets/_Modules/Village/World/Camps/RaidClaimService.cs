// =============================================================================
// RaidClaimService — the persisted "which raid bases has the player CLAIMED?" set.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// THE GAP THIS CLOSES (core-loop payoff, WO-441 Phase A/C spine): clearing a raid
// garrison (RaidGarrisonSpawner.OnCleared) had NO subscriber and NO record of the
// win — so victory -> CLAIM -> "this base is now mine" was missing and a cleared
// raid soft-locked. This static persists the claimed-raid set so the win READS as
// the player's: the OuterWorld outpost / re-entry can reflect claimed state, and a
// claimed scene flips ownership ENEMY -> PLAYER (the inverse of
// RaidGarrisonSpawner's SceneOwnership.SetEnemyOwned(true)).
//
// PERSISTENCE: PlayerPrefs, exactly mirroring the established ClaimableCamp
// convention (dotr-camp-claimed-<id>) and the WO-441 spec's named key
// (dotr-raid-owner-<id>). This is the SAME additive pattern the camp loop already
// ships — NO SaveSchema migration (the versioned save layer is risk-gated; camp/
// raid ownership has always lived in PlayerPrefs). A later WO can fold the set into
// SaveSchema v24 (OwnedOutposts) for cloud sync; until then this is local-first and
// correct.
//
// configId is the scene-config id the raid base was generated from (the baked scene
// is RaidBase_<configId>; RaidGarrisonSpawner carries the STORED id). ASCII-only.
// Canon: the village is Elarion (never Avalon).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.World.Camps
{
    /// <summary>
    /// The persisted set of CLAIMED raid bases (PlayerPrefs <c>dotr-raid-owner-&lt;configId&gt;</c>).
    /// Written on raid victory by <see cref="RaidVictoryController"/>; read by re-entry /
    /// world-state so a cleared-and-claimed base reads as the player's, not the enemy's.
    /// </summary>
    public static class RaidClaimService
    {
        // +configId -> "1" once the player has cleared + claimed that raid base.
        private const string PrefOwnerKey = "dotr-raid-owner-";

        /// <summary>True once the player has claimed the raid base with this config id.</summary>
        public static bool IsClaimed(string configId)
        {
            if (string.IsNullOrEmpty(configId)) return false;
            return PlayerPrefs.GetString(PrefOwnerKey + configId, null) == "1";
        }

        /// <summary>
        /// Mark the raid base <paramref name="configId"/> CLAIMED (player-owned), persist,
        /// and report whether this was a NEW claim (true) or a no-op re-claim (false). The
        /// caller uses the "new" signal to decide whether to grant the one-time payoff
        /// (the next-companion unlock) so a re-cleared base never double-grants. Idempotent.
        /// </summary>
        public static bool MarkClaimed(string configId)
        {
            if (string.IsNullOrEmpty(configId))
            {
                FlowTrace.Warn("Raid", "RaidClaimService.MarkClaimed: empty configId — not claimed.");
                return false;
            }
            if (IsClaimed(configId))
            {
                FlowTrace.Step("Raid", $"RaidClaimService: '{configId}' already claimed — no-op (no re-grant).");
                return false;
            }
            PlayerPrefs.SetString(PrefOwnerKey + configId, "1");
            PlayerPrefs.Save();
            FlowTrace.Step("Raid", $"RaidClaimService: '{configId}' CLAIMED -> player-owned (persisted dotr-raid-owner-{configId}).");
            return true;
        }

        /// <summary>Test/dev hook: drop the claim on a raid base (so it can be re-raided).</summary>
        public static void ClearClaim(string configId)
        {
            if (string.IsNullOrEmpty(configId)) return;
            PlayerPrefs.DeleteKey(PrefOwnerKey + configId);
            PlayerPrefs.Save();
        }
    }
}
