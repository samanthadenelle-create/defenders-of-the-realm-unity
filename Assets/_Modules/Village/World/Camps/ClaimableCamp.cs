// =============================================================================
// ClaimableCamp - one outer-world enemy camp in the clear -> claim -> build loop.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// Lifecycle:
//   STAGE 1 CLEAR  - count enemy kills within CampRadius (subscribe to the real
//                    public Enemy.Died event). At KillsRequired -> Clear().
//   STAGE 2 CLAIM  - once cleared, when the hero is near, CampPromptUI shows a
//                    code-built world-space "Claim" prompt (tap / [E]). Tap claims.
//   STAGE 3 BUILD  - on claim, CampBuildMenuUI offers Watchtower / Lumber Outpost /
//                    Farm Outpost. Choosing one spawns an Outpost that auto-harvests
//                    a trickle into the wallet.
//
// ISOLATION: created/owned entirely by CampSystem at runtime. Subscribes to the
// existing PUBLIC Enemy.Died event (read-only) - it does NOT modify Enemy or any
// spawner. Code-built primitive visuals (LogWarning, never error, if optional art
// is absent). Persistence is PlayerPrefs only (claimed id + outpost type); the
// save SCHEMA is untouched (save-owner follow-up).
// Canon: the village is Elarion (never Avalon). ASCII-only runtime strings.
// =============================================================================
using System;
using UnityEngine;
using DeNelle.Core.World;

namespace DeNelle.Village.World.Camps
{
    /// <summary>The three lifecycle stages of a camp.</summary>
    public enum CampStage { Hostile, Cleared, Claimed }

    /// <summary>A bounded outer-world camp the player clears, claims, then builds on.</summary>
    [DisallowMultipleComponent]
    public sealed class ClaimableCamp : MonoBehaviour
    {
        // -- Config (set by CampSystem.Configure) -----------------------------
        public RegionId Region { get; private set; } = RegionId.Goldfields;
        public int ThreatLevel { get; private set; }
        public int KillsRequired { get; private set; } = CampSystem.DefaultKillsRequired;
        public float CampRadius { get; private set; } = CampSystem.DefaultCampRadius;

        /// <summary>Stable id (region-based) used as the PlayerPrefs persistence key.</summary>
        public string CampId { get; private set; }

        // -- Runtime state ----------------------------------------------------
        public CampStage Stage { get; private set; } = CampStage.Hostile;
        public int KillCount { get; private set; }

        /// <summary>True once enough kills landed inside the camp - claimable.</summary>
        public bool Cleared => Stage == CampStage.Cleared || Stage == CampStage.Claimed;

        /// <summary>True once the player has claimed and (optionally) built here.</summary>
        public bool Claimed => Stage == CampStage.Claimed;

        /// <summary>Raised when the camp transitions to Cleared. Arg = this camp.</summary>
        public event Action<ClaimableCamp> OnCleared;
        /// <summary>Raised when the camp is claimed. Arg = this camp.</summary>
        public event Action<ClaimableCamp> OnClaimed;

        private CampVisual _visual;
        private bool _subscribed;

        // -- Persistence (PlayerPrefs only - schema untouched) ----------------
        private const string PrefClearedKey = "dotr-camp-cleared-";   // +CampId -> "1"
        private const string PrefClaimedKey = "dotr-camp-claimed-";   // +CampId -> outpost type int

        /// <summary>Called by CampSystem immediately after AddComponent.</summary>
        public void Configure(RegionId region, int threat, int killsRequired, float radius)
        {
            Region = region;
            ThreatLevel = threat;
            KillsRequired = Mathf.Max(1, killsRequired);
            CampRadius = Mathf.Max(1f, radius);
            CampId = "camp_" + region;
        }

        private void Start()
        {
            // Build the simple code-built visual tell (campfire/banner primitives).
            _visual = gameObject.AddComponent<CampVisual>();
            _visual.Init(this);

            RestoreFromPrefs();

            if (Stage == CampStage.Hostile)
                SubscribeToKills();
        }

        private void OnDestroy() => UnsubscribeFromKills();

        // =====================================================================
        // STAGE 1 - CLEAR. Count kills inside the camp via the public Enemy.Died.
        // We find Enemy instances lazily (no spawner edit) and subscribe; the dead
        // enemy's world position is proximity-tested against the camp footprint.
        // =====================================================================

        private float _lastScan;

        private void Update()
        {
            if (Stage != CampStage.Hostile) return;

            // Re-scan for newly-spawned enemies every ~1s and subscribe to any we
            // haven't yet. RegionMobSpawner spawns enemies at runtime, so the set
            // grows; we never edit the spawner, just listen to each Enemy.Died.
            if (Time.time - _lastScan > 1f)
            {
                _lastScan = Time.time;
                SubscribeToKills();
            }
        }

        private void SubscribeToKills()
        {
            // Subscribe to every Enemy currently alive (idempotent: -= then +=).
            var enemies = UnityEngine.Object.FindObjectsByType<Enemy>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var e in enemies)
            {
                if (e == null) continue;
                e.Died -= HandleEnemyDied;
                e.Died += HandleEnemyDied;
            }
            _subscribed = true;
        }

        private void UnsubscribeFromKills()
        {
            if (!_subscribed) return;
            var enemies = UnityEngine.Object.FindObjectsByType<Enemy>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var e in enemies)
            {
                if (e != null) e.Died -= HandleEnemyDied;
            }
            _subscribed = false;
        }

        private void HandleEnemyDied(Enemy enemy)
        {
            if (Stage != CampStage.Hostile || enemy == null) return;

            // Proximity gate: only kills INSIDE this camp footprint count (do NOT
            // edit RegionMobSpawner - just test where the kill happened).
            float sqr = (enemy.transform.position - transform.position).sqrMagnitude;
            if (sqr > CampRadius * CampRadius) return;

            KillCount++;
            if (KillCount >= KillsRequired)
                Clear();
        }

        /// <summary>Transition Hostile -> Cleared (visual tell + persistence).</summary>
        public void Clear()
        {
            if (Stage != CampStage.Hostile) return;
            Stage = CampStage.Cleared;
            UnsubscribeFromKills();
            _visual?.SetStage(CampStage.Cleared);
            PlayerPrefs.SetString(PrefClearedKey + CampId, "1");
            PlayerPrefs.Save();
            Debug.Log($"[ClaimableCamp] {CampId} CLEARED ({KillCount}/{KillsRequired} kills). Approach to claim.");
            OnCleared?.Invoke(this);
        }

        // =====================================================================
        // STAGE 2 - CLAIM. Driven by CampPromptUI (hero proximity + tap/[E]).
        // =====================================================================

        /// <summary>Claim a cleared camp. No-op unless Cleared. Idempotent.</summary>
        public void Claim()
        {
            if (Stage != CampStage.Cleared) return;
            Stage = CampStage.Claimed;
            _visual?.SetStage(CampStage.Claimed);
            PlayerPrefs.SetString(PrefClaimedKey + CampId, ((int)OutpostType.None).ToString());
            PlayerPrefs.Save();
            Debug.Log($"[ClaimableCamp] {CampId} CLAIMED. Choose an outpost to build.");
            OnClaimed?.Invoke(this);
        }

        // =====================================================================
        // STAGE 3 - BUILD. Spawn the chosen outpost (auto-harvest faucet).
        // =====================================================================

        private Outpost _outpost;

        /// <summary>True once an outpost has been built on this claimed camp.</summary>
        public bool HasOutpost => _outpost != null;

        /// <summary>Build (or replace) the outpost of the given type on this camp.</summary>
        public Outpost BuildOutpost(OutpostType type)
        {
            if (Stage != CampStage.Claimed) return null;
            if (type == OutpostType.None) return null;

            if (_outpost != null)
            {
                Destroy(_outpost.gameObject);
                _outpost = null;
            }

            var go = new GameObject($"Outpost_{type}_{Region}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            _outpost = go.AddComponent<Outpost>();
            _outpost.Init(type, Region, ThreatLevel);

            PlayerPrefs.SetString(PrefClaimedKey + CampId, ((int)type).ToString());
            PlayerPrefs.Save();

            Debug.Log($"[ClaimableCamp] {CampId} built a {type} outpost - auto-harvest online.");
            return _outpost;
        }

        // =====================================================================
        // Persistence restore (PlayerPrefs only).
        // =====================================================================

        private void RestoreFromPrefs()
        {
            // Claimed (and possibly built) takes precedence over merely cleared.
            string claimedRaw = PlayerPrefs.GetString(PrefClaimedKey + CampId, null);
            if (!string.IsNullOrEmpty(claimedRaw))
            {
                Stage = CampStage.Claimed;
                _visual?.SetStage(CampStage.Claimed);
                if (int.TryParse(claimedRaw, out int t) && t != (int)OutpostType.None)
                {
                    var type = (OutpostType)t;
                    var go = new GameObject($"Outpost_{type}_{Region}");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = Vector3.zero;
                    _outpost = go.AddComponent<Outpost>();
                    _outpost.Init(type, Region, ThreatLevel);
                }
                return;
            }

            if (PlayerPrefs.GetString(PrefClearedKey + CampId, null) == "1")
            {
                Stage = CampStage.Cleared;
                _visual?.SetStage(CampStage.Cleared);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Stage == CampStage.Hostile
                ? new Color(0.9f, 0.2f, 0.2f, 0.35f)
                : new Color(0.2f, 0.9f, 0.4f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, CampRadius);
        }
#endif
    }
}
