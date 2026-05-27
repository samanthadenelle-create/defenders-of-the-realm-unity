// =============================================================================
// CampaignManager — DEF-68 (Spire Chronicles Campaign). Runtime layer that sits
// on top of the wave loop: holds the active CampaignData + an in-memory
// CampaignProgressRecord, watches the WaveManager for wave clears, and advances
// mission progress when a mission's wave goal is met.
// -----------------------------------------------------------------------------
// PLACEMENT: namespace DeNelle.Village (it touches WaveManager, which lives
// here). DeNelle.Village already references DeNelle.Core, so the SO types in
// DeNelle.Core.Data are visible. No new asmdef, no scene edits, no UI.
//
// BRIDGE DECISION (documented per the work order): CampaignManager IS the bridge
// itself — it is a [DisallowMultipleComponent][RequireComponent(typeof(
// WaveManager))] component on the WaveManager GameObject, exactly like
// DailyQuestCombatBridge / WaveHudBridge. It caches the sibling WaveManager via
// Reset()=>GetComponent (editor) and Awake()=>GetComponent (runtime), subscribes
// WaveManager.OnWaveCleared in OnEnable, and unsubscribes in OnDisable. No
// CampaignWaveBridge forwarder is needed because the manager already lives on the
// WaveManager GameObject; a separate forwarder would only add indirection.
//   → VillageSceneBuilder attaches these bridges to the WaveManager GO; this
//     file does NOT wire scenes (same contract as DailyQuestCombatBridge).
//
// SINGLETON: follows the DailyQuestService.Instance shape (static Instance with
// the "destroy the duplicate" Awake guard, cleared in OnDestroy). It is NOT
// DontDestroyOnLoad and has NO RuntimeInitialize bootstrap — unlike
// DailyQuestService it is a SCENE-ATTACHED component (it must live beside the
// WaveManager), so it is created/destroyed with the Village scene. UI and other
// systems reach it via CampaignManager.Instance while the village is loaded.
//
// COMPLETION DETECTION (there is NO WaveManager.OnAllWavesCleared on this branch):
// the manager counts WaveManager.OnWaveCleared events against the active
// MissionData.waveGoal. Each OnWaveCleared bumps a per-mission cleared counter;
// when that counter reaches CurrentMission.waveGoal the mission is marked
// complete, the next mission auto-unlocks (currentMissionIndex advances, clamped),
// and the counter resets for the new mission. This reconciles the spec's
// "OnAllWavesCleared -> mark mission complete" intent to this branch's real API.
//
// ScriptableObjects stay PURE DATA: the manager never mutates CampaignData /
// MissionData; all run state lives in the in-memory CampaignProgressRecord.
// =============================================================================

using System;
using DeNelle.Core.Data;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Campaign progression layer over the wave loop. Singleton (like
    /// DailyQuestService) AND the WaveManager bridge (like DailyQuestCombatBridge)
    /// in one scene-attached component.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WaveManager))]
    public sealed class CampaignManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        public static CampaignManager Instance { get; private set; }

        // ── Inspector wiring ──────────────────────────────────────────────────

        [Header("Campaign")]
        [Tooltip("The active campaign authoring asset. May be left null and supplied " +
                 "later via SetCampaign(...).")]
        [SerializeField] private CampaignData _campaign;

        [Tooltip("Cached sibling WaveManager (auto-filled by Reset/Awake).")]
        [SerializeField] private WaveManager _wave;

        // ── Events (for UI / reward resolution later) ─────────────────────────

        /// <summary>
        /// Fires when a mission is completed. Arg = the 0-based index of the
        /// completed mission. UI / reward systems subscribe to react.
        /// </summary>
        public event Action<int> OnMissionCompleted;

        // ── Runtime state (in-memory only) ────────────────────────────────────

        private CampaignProgressRecord _progress;

        /// <summary>Waves cleared since the current mission began (reset on advance).</summary>
        private int _wavesClearedThisMission;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>The active campaign asset (null until set).</summary>
        public CampaignData Campaign => _campaign;

        /// <summary>In-memory progress through the active campaign (null until a campaign is set).</summary>
        public CampaignProgressRecord Progress => _progress;

        /// <summary>
        /// The MissionData currently in play, or null if there is no campaign /
        /// the campaign is exhausted (currentMissionIndex past the last mission).
        /// </summary>
        public MissionData CurrentMission
        {
            get
            {
                if (_campaign == null || _campaign.missions == null || _progress == null)
                    return null;
                int i = _progress.currentMissionIndex;
                if (i < 0 || i >= _campaign.missions.Length) return null;
                return _campaign.missions[i];
            }
        }

        /// <summary>True once every mission in the active campaign is complete.</summary>
        public bool IsCampaignComplete
        {
            get
            {
                if (_campaign == null || _campaign.missions == null) return false;
                return _progress != null &&
                       _progress.currentMissionIndex >= _campaign.missions.Length;
            }
        }

        /// <summary>
        /// Swaps in a campaign and resets in-memory progress to its first mission.
        /// Safe to call at runtime. Passing null clears the active campaign.
        /// </summary>
        public void SetCampaign(CampaignData campaign)
        {
            _campaign = campaign;
            int missionCount = (campaign != null && campaign.missions != null)
                ? campaign.missions.Length
                : 0;
            _progress = new CampaignProgressRecord(missionCount);
            _wavesClearedThisMission = 0;

            Debug.Log(campaign != null
                ? $"[CampaignManager] Campaign set — {missionCount} mission(s)."
                : "[CampaignManager] Campaign cleared (set to null).");
        }

        /// <summary>
        /// Whether the mission at <paramref name="index"/> has been completed.
        /// Out-of-range / no-progress returns false.
        /// </summary>
        public bool IsMissionComplete(int index)
        {
            if (_progress == null || _progress.missionCompleted == null) return false;
            if (index < 0 || index >= _progress.missionCompleted.Length) return false;
            return _progress.missionCompleted[index];
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Reset() => _wave = GetComponent<WaveManager>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (_wave == null) _wave = GetComponent<WaveManager>();

            // If a campaign was wired in the inspector, initialise progress now so
            // CurrentMission is valid before the first wave clears.
            if (_progress == null && _campaign != null)
                SetCampaign(_campaign);
        }

        private void OnEnable()
        {
            if (_wave == null) _wave = GetComponent<WaveManager>();
            if (_wave != null)
                _wave.OnWaveCleared.AddListener(HandleWaveCleared);
        }

        private void OnDisable()
        {
            if (_wave != null)
                _wave.OnWaveCleared.RemoveListener(HandleWaveCleared);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Wave-clear -> mission progression ─────────────────────────────────

        /// <summary>
        /// Forwarded from WaveManager.OnWaveCleared(int waveId). Counts cleared
        /// waves against the active mission's waveGoal; on reaching it, marks the
        /// mission complete and advances (auto-unlocking the next).
        /// </summary>
        private void HandleWaveCleared(int waveId)
        {
            // No campaign in play, or the campaign is already finished: nothing to do.
            MissionData mission = CurrentMission;
            if (mission == null || IsCampaignComplete) return;

            _wavesClearedThisMission++;

            int goal = Mathf.Max(1, mission.waveGoal);
            if (_wavesClearedThisMission < goal) return;

            CompleteCurrentMission();
        }

        /// <summary>
        /// Marks the current mission complete, fires OnMissionCompleted, then
        /// advances currentMissionIndex (clamped past the last mission) and resets
        /// the per-mission cleared counter for the next mission.
        /// </summary>
        private void CompleteCurrentMission()
        {
            int completedIndex = _progress.currentMissionIndex;

            if (_progress.missionCompleted != null &&
                completedIndex >= 0 &&
                completedIndex < _progress.missionCompleted.Length)
            {
                _progress.missionCompleted[completedIndex] = true;
            }

            // Auto-unlock the next mission. Clamp at missions.Length so an exhausted
            // campaign reports IsCampaignComplete rather than indexing out of range.
            int missionCount = (_campaign != null && _campaign.missions != null)
                ? _campaign.missions.Length
                : 0;
            _progress.currentMissionIndex = Mathf.Min(completedIndex + 1, missionCount);
            _wavesClearedThisMission = 0;

            string completedName = (_campaign != null && _campaign.missions != null &&
                                    completedIndex >= 0 && completedIndex < _campaign.missions.Length &&
                                    _campaign.missions[completedIndex] != null)
                ? _campaign.missions[completedIndex].missionName
                : completedIndex.ToString();

            Debug.Log(IsCampaignComplete
                ? $"[CampaignManager] Mission '{completedName}' complete — campaign finished."
                : $"[CampaignManager] Mission '{completedName}' complete — next mission unlocked " +
                  $"(index {_progress.currentMissionIndex}).");

            // Notify UI / reward resolution. Event last so listeners see final state.
            OnMissionCompleted?.Invoke(completedIndex);
        }
    }
}
