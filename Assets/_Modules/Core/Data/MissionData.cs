// =============================================================================
// MissionData — DEF-68 (Spire Chronicles Campaign). ScriptableObject defining
// one campaign mission: its name, the number of waves to clear, an optional
// "no-damage" tower-integrity constraint, and an optional special-modifier key.
// -----------------------------------------------------------------------------
// Lives in DeNelle.Core.Data (the namespace inside the existing DeNelle.Core
// asmdef — no new asmdef; DeNelle.Village already references DeNelle.Core).
//
// RECONCILIATION TO THIS BRANCH (vs the DEF-68 Linear description):
//   • The Linear spec sketched a richer authoring shape (missionId/missionTitle/
//     briefingText/difficultyMultiplier/hasBossWave/MissionReward[]). This branch
//     deliberately ships the LEAN field set the WO-48 work order pins down, so
//     CampaignManager can run against THIS branch's real WaveManager today; the
//     richer reward/lore authoring is deferred to its follow-up tickets.
//   • specialModifier stays a string key (per the Linear note) — a future
//     MissionModifierResolver switches on it; avoids a per-modifier enum churn.
//
// PURE DATA: this SO is never mutated at runtime. Per-playthrough state lives in
// CampaignProgressRecord (in-memory). CreateAssetMenu under "Defenders/".
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.Data
{
    /// <summary>Authoring data for one Spire Chronicles campaign mission.</summary>
    [CreateAssetMenu(menuName = "Defenders/MissionData", fileName = "MissionData")]
    public class MissionData : ScriptableObject
    {
        [Tooltip("Display name for this mission (e.g. \"The First Watch\").")]
        public string missionName = "New Mission";

        [Tooltip("How many waves must be cleared for this mission to count as complete. " +
                 "CampaignManager counts WaveManager.OnWaveCleared events against this.")]
        [Min(1)] public int waveGoal = 1;

        [Tooltip("Optional integrity constraint, 0..1, expressed as the fraction of the " +
                 "tower's max HP that may be lost and still earn the clean-clear (e.g. 0 = " +
                 "take no damage, 1 = damage irrelevant). Read by reward/scoring follow-ups; " +
                 "CampaignManager itself does not gate completion on it yet.")]
        [Range(0f, 1f)] public float maxTowerDamageAllowed = 1f;

        [Tooltip("String key for an optional special modifier (e.g. \"EnemySpeedBoost\", " +
                 "\"NightOnly\"). Intentionally a string — a future MissionModifierResolver " +
                 "switches on it. Empty/None means a standard mission.")]
        public string specialModifier = "";
    }
}
