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
using DeNelle.Core.State;

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
        /// <summary>Raised when the post-build counterattack is repelled (camp secured).</summary>
        public event Action<ClaimableCamp> OnDefended;
        /// <summary>Raised when the post-build counterattack razes the outpost (defense lost).</summary>
        public event Action<ClaimableCamp> OnDefenseLost;

        /// <summary>True once the post-build counterattack has been repelled.</summary>
        public bool Secured { get; private set; }
        /// <summary>True while the post-build counterattack is in progress.</summary>
        public bool UnderAttack => _defense != null && !_defense.Resolved;

        private CampVisual _visual;
        private bool _subscribed;
        private CampGuards _guards;
        private CampDefenseWave _defense;

        // -- Clear reward (WO-216 / DEF-187) ----------------------------------
        // Granted ONCE when the camp transitions Hostile -> Cleared. Crystals bank
        // straight into GameState (the existing economy path); XP feeds the hero via
        // HeroProgression. Scaled by threat tier so deadlier camps pay better.
        /// <summary>Aether Crystals banked on clear (before the threat-tier bonus).</summary>
        public int BaseClearCrystals { get; private set; } = 15;
        /// <summary>Hero XP granted on clear (before the threat-tier bonus).</summary>
        public int BaseClearXp { get; private set; } = 40;
        /// <summary>True once the clear reward has been paid (prevents double-grant).</summary>
        private bool _rewardPaid;

        // -- Persistence (PlayerPrefs only - schema untouched) ----------------
        private const string PrefClearedKey = "dotr-camp-cleared-";   // +CampId -> "1"
        private const string PrefClaimedKey = "dotr-camp-claimed-";   // +CampId -> outpost type int
        private const string PrefSecuredKey = "dotr-camp-secured-";   // +CampId -> "1" (defended)

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
            {
                // Primary clear path (WO-216): spawn a dedicated guard pack ON the camp.
                // When ALL guards die the camp clears. The legacy proximity-kill counter
                // below stays as a secondary path (e.g. a roaming mob killed inside the
                // footprint also counts), so a camp can never get stuck un-clearable.
                _guards = gameObject.AddComponent<CampGuards>();
                _guards.AllCleared += Clear;
                _guards.Spawn(Region, ThreatLevel);

                SubscribeToKills();
            }
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
            if (_guards != null) _guards.AllCleared -= Clear;
            _visual?.SetStage(CampStage.Cleared);
            PlayerPrefs.SetString(PrefClearedKey + CampId, "1");
            PlayerPrefs.Save();

            GrantClearReward();

            Debug.Log($"[ClaimableCamp] {CampId} CLEARED. Approach to claim.");
            OnCleared?.Invoke(this);
        }

        /// <summary>
        /// Pay the one-time clear reward (WO-216 / DEF-187): Aether Crystals banked
        /// into GameState (the existing economy wallet path) + hero XP via
        /// HeroProgression. Both scale with the camp's threat tier so deadlier camps
        /// pay better. Idempotent - the reward is paid at most once per camp/session.
        /// Restored-as-already-cleared camps do NOT re-pay (RestoreFromPrefs marks it).
        /// </summary>
        private void GrantClearReward()
        {
            if (_rewardPaid) return;
            _rewardPaid = true;

            int crystals = BaseClearCrystals + ThreatLevel * 5;
            int xp = BaseClearXp + ThreatLevel * 10;

            // Crystals -> GameState wallet (null-conditional on the cross-module singleton).
            var state = GameStateService.Instance?.State;
            if (state != null)
            {
                state.AetherCrystals += crystals;
                GameStateService.Instance?.ResourcesChanged?.Invoke();
            }
            else
            {
                Debug.LogWarning("[ClaimableCamp] GameState null - clear crystals not banked.");
            }

            // XP -> hero progression (same flat-XP path Enemy.Die uses on kill).
            HeroProgression.Instance?.AddXp(xp);

            Debug.Log($"[ClaimableCamp] {CampId} clear reward: +{crystals} crystals, +{xp} XP.");
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

            // STAGE 4 DEFEND: the displaced faction counterattacks the new outpost.
            // (A camp already SECURED in a prior session resolves instantly inside Begin.)
            StartDefense();
            return _outpost;
        }

        // =====================================================================
        // STAGE 4 - DEFEND. After a build, spawn one counterattack on the outpost.
        // Survive it -> camp SECURED (one-time reward + persisted). Outpost razed
        // -> defense lost, camp stays claimed and is rebuildable.
        // =====================================================================

        private void StartDefense()
        {
            if (_outpost == null) return;
            if (Secured) return;                       // already held - no re-raid
            if (_defense != null && !_defense.Resolved) return;  // a raid is already live

            _defense = gameObject.AddComponent<CampDefenseWave>();
            _defense.OnDefended += HandleDefended;
            _defense.OnLost += HandleDefenseLost;
            _defense.Begin(Region, ThreatLevel, _outpost, CampId);
        }

        private void HandleDefended(CampDefenseWave wave)
        {
            if (wave != null) wave.OnDefended -= HandleDefended;
            Secured = true;
            PlayerPrefs.SetString(PrefSecuredKey + CampId, "1");
            PlayerPrefs.Save();
            Debug.Log($"[ClaimableCamp] {CampId} SECURED - counterattack repelled.");
            OnDefended?.Invoke(this);
        }

        private void HandleDefenseLost(CampDefenseWave wave)
        {
            if (wave != null) wave.OnLost -= HandleDefenseLost;
            // Outpost was razed (it Destroys itself at 0 HP). Camp stays claimed;
            // building a fresh outpost re-triggers the counterattack.
            _outpost = null;
            Debug.LogWarning($"[ClaimableCamp] {CampId} defense lost - outpost razed, rebuild to retry.");
            OnDefenseLost?.Invoke(this);
        }

        // =====================================================================
        // Persistence restore (PlayerPrefs only).
        // =====================================================================

        private void RestoreFromPrefs()
        {
            // A previously SECURED camp stays peaceful (no re-raid on reload).
            Secured = PlayerPrefs.GetString(PrefSecuredKey + CampId, null) == "1";

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

                    // Restored with an outpost but NOT yet secured -> the counterattack
                    // is still pending; re-arm it so the loop can complete after reload.
                    if (!Secured) StartDefense();
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
