// =============================================================================
// DungeonRuntimeState — runtime-only ScriptableObject for the active dungeon run.
// -----------------------------------------------------------------------------
// Port spec Part 3 row:
//   src/store/dungeonRuntimeStore.ts -> _Modules/Dungeons/State/DungeonRuntimeState.cs
//
// C# port of the React 3D dungeon runtime store (src/store/dungeon3dRuntimeStore.ts
// — the live 3D successor to the dormant 2D dungeonRuntimeStore.ts the port-table
// row names). A non-persisted ScriptableObject that mirrors the Zustand store:
// it holds the live run surface (current room, hero position, checkpoints reached,
// lore-stones read, encounter counters) and raises UnityEvents on mutation.
//
// Zustand parallel (v2 port-spec Part 3 "ScriptableObject + UnityEvent for
// state"): the ScriptableObject holds the data; UnityEvents fire on mutation;
// UI / scene MonoBehaviours subscribe in OnEnable, unsubscribe in OnDisable.
//
// A dungeon run is meant to be finished in one sitting — NOTHING here is
// serialised to PlayerPrefs. Everything can be rebuilt from the dungeon layout
// JSON (StreamingAssets/Data/Canonical/dungeons/healers-cottage.json) plus the
// persisted save. This SO carries [CreateAssetMenu] only so a designer can drop
// a shared instance into the DungeonController in the inspector.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// Runtime-only ScriptableObject tracking the active Healer's Cottage run —
    /// the current room, the Keeper's floor position, which checkpoints have
    /// healed/saved, which lore-stones have been read, and the encounter clock.
    /// Mirrors the React <c>dungeon3dRuntimeStore</c>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DungeonRuntimeState",
        menuName = "DeNelle/Dungeons/Dungeon Runtime State")]
    public sealed class DungeonRuntimeState : ScriptableObject
    {
        // ── Live run surface ─────────────────────────────────────────────────

        [Header("Run (runtime — not serialised to disk)")]
        [Tooltip("The dungeon id of the active run, or empty before a run starts.")]
        [SerializeField] private string _dungeonId = string.Empty;

        [Tooltip("Id of the room the Keeper is currently inside.")]
        [SerializeField] private string _currentRoomId = string.Empty;

        [Tooltip("The Keeper's floor-plane position (driven by the scene loop).")]
        [SerializeField] private Vector3 _heroPosition;

        [Tooltip("Per-run seed — threads the deterministic random-encounter sequence.")]
        [SerializeField] private int _dungeonRunSeed;

        [Tooltip("How many random encounters have fired this run.")]
        [SerializeField] private int _randomEncounterCount;

        [Tooltip("Seconds elapsed since the last encounter (random or scripted).")]
        [SerializeField] private float _secondsSinceLastEncounter;

        [Tooltip("Checkpoint shrine ids reached this run (heal + save points).")]
        [SerializeField] private List<string> _checkpointsReached = new List<string>();

        [Tooltip("Lore-stone ids read this run — all four advances the questline.")]
        [SerializeField] private List<string> _loreStonesRead = new List<string>();

        [Tooltip("Scripted-encounter ids that have already fired this run.")]
        [SerializeField] private List<string> _scriptedEncountersFired = new List<string>();

        [Tooltip("Treasure-chest ids opened this run.")]
        [SerializeField] private List<string> _chestsOpened = new List<string>();

        [Tooltip("Secret-room ids the Keeper has discovered this run.")]
        [SerializeField] private List<string> _secretRoomsFound = new List<string>();

        [Header("Flags (runtime)")]
        [Tooltip("True while an encounter battle (random or mini-boss) is being resolved.")]
        [SerializeField] private bool _inCombat;

        [Tooltip("True once the mini-boss is down this run — gates the Apothecary exit.")]
        [SerializeField] private bool _bossDefeated;

        [Tooltip("True once a run has been started (StartRun called).")]
        [SerializeField] private bool _runActive;

        [Header("Encounter handoff (survives the ATB scene round-trip)")]
        [Tooltip("Id of the encounter that launched the in-flight ATB battle, or " +
                 "empty. A ScriptableObject survives SceneManager.LoadScene, so this " +
                 "carries the encounter id across into the ATBBattle scene and back.")]
        [SerializeField] private string _pendingEncounterId = string.Empty;

        [Tooltip("True for a mini-boss handoff — on a victory resume the run marks " +
                 "the boss defeated and unlocks the Apothecary exit.")]
        [SerializeField] private bool _pendingEncounterIsBoss;

        [Tooltip("The Keeper's floor position when the encounter fired — the resume " +
                 "point the hero is replaced at when the dungeon scene reloads.")]
        [SerializeField] private Vector3 _encounterResumePosition;

        [Header("Hero vitals (preserved across the ATB round-trip)")]
        [Tooltip("The Keeper's HP carried into / out of an encounter. v1's ATB " +
                 "engine fully restores party HP/MP after every fight, so a clean " +
                 "resume reads full; a future non-reset build still round-trips here.")]
        [SerializeField] private float _heroHp = -1f;

        [Tooltip("The Keeper's max HP — the ceiling a checkpoint heal restores to.")]
        [SerializeField] private float _heroMaxHp = -1f;

        [Tooltip("The Keeper's mana, carried across the ATB round-trip.")]
        [SerializeField] private float _heroMana = -1f;

        [Tooltip("The Keeper's max mana — the ceiling a checkpoint heal restores to.")]
        [SerializeField] private float _heroMaxMana = -1f;

        // ── Mutation events (Zustand 'subscribe' parallel) ───────────────────

        /// <summary>Fired whenever any field of the run state changes.</summary>
        [Header("Events")]
        public UnityEvent RunStateChanged = new UnityEvent();

        /// <summary>Fired with the new room id when the Keeper crosses into a room.</summary>
        public UnityEvent<string> RoomChanged = new UnityEvent<string>();

        /// <summary>Fired with the checkpoint id the FIRST time a checkpoint is reached.</summary>
        public UnityEvent<string> CheckpointReached = new UnityEvent<string>();

        /// <summary>Fired with the lore-stone id the FIRST time a stone is read.</summary>
        public UnityEvent<string> LoreStoneRead = new UnityEvent<string>();

        /// <summary>Fired when all lore stones of the dungeon have been read.</summary>
        public UnityEvent QuestlineBeatComplete = new UnityEvent();

        // ── Read-only accessors ──────────────────────────────────────────────

        /// <summary>The dungeon id of the active run, or empty before a run starts.</summary>
        public string DungeonId => _dungeonId;

        /// <summary>The id of the room the Keeper is currently inside.</summary>
        public string CurrentRoomId => _currentRoomId;

        /// <summary>The Keeper's current floor-plane world position.</summary>
        public Vector3 HeroPosition => _heroPosition;

        /// <summary>The per-run seed for the deterministic encounter sequence.</summary>
        public int DungeonRunSeed => _dungeonRunSeed;

        /// <summary>How many random encounters have fired this run.</summary>
        public int RandomEncounterCount => _randomEncounterCount;

        /// <summary>Seconds since the last encounter (the cooldown clock).</summary>
        public float SecondsSinceLastEncounter => _secondsSinceLastEncounter;

        /// <summary>Checkpoint shrine ids reached this run.</summary>
        public IReadOnlyList<string> CheckpointsReached => _checkpointsReached;

        /// <summary>Lore-stone ids read this run.</summary>
        public IReadOnlyList<string> LoreStonesRead => _loreStonesRead;

        /// <summary>Treasure-chest ids opened this run.</summary>
        public IReadOnlyList<string> ChestsOpened => _chestsOpened;

        /// <summary>Secret-room ids discovered this run.</summary>
        public IReadOnlyList<string> SecretRoomsFound => _secretRoomsFound;

        /// <summary>True while an encounter battle is being resolved.</summary>
        public bool InCombat => _inCombat;

        /// <summary>True once the mini-boss is down — unlocks the Apothecary exit.</summary>
        public bool BossDefeated => _bossDefeated;

        /// <summary>True once a run has been started and not yet ended.</summary>
        public bool RunActive => _runActive;

        /// <summary>
        /// The id of the encounter whose ATB battle is in flight, or empty. Set
        /// when an encounter launches the battle scene; read on dungeon re-entry
        /// to resume — the carrying SO survives the scene round-trip.
        /// </summary>
        public string PendingEncounterId => _pendingEncounterId;

        /// <summary>True while an encounter battle is pending its resume.</summary>
        public bool HasPendingEncounter => !string.IsNullOrEmpty(_pendingEncounterId);

        /// <summary>True when the in-flight encounter is the dungeon's mini-boss.</summary>
        public bool PendingEncounterIsBoss => _pendingEncounterIsBoss;

        /// <summary>The floor position the Keeper resumes at after an encounter.</summary>
        public Vector3 EncounterResumePosition => _encounterResumePosition;

        /// <summary>The Keeper's last-snapshotted HP, or &lt; 0 when never set.</summary>
        public float HeroHp => _heroHp;

        /// <summary>The Keeper's max HP, or &lt; 0 when never set.</summary>
        public float HeroMaxHp => _heroMaxHp;

        /// <summary>The Keeper's last-snapshotted mana, or &lt; 0 when never set.</summary>
        public float HeroMana => _heroMana;

        /// <summary>The Keeper's max mana, or &lt; 0 when never set.</summary>
        public float HeroMaxMana => _heroMaxMana;

        /// <summary>True once the hero's vitals have been snapshotted at least once.</summary>
        public bool HasHeroVitals => _heroMaxHp > 0f;

        /// <summary>True when a lore stone with the given id has been read.</summary>
        public bool HasReadLore(string loreStoneId) => _loreStonesRead.Contains(loreStoneId);

        /// <summary>True when a checkpoint with the given id has been reached.</summary>
        public bool HasReachedCheckpoint(string checkpointId) =>
            _checkpointsReached.Contains(checkpointId);

        /// <summary>True when a scripted encounter with the given id has fired.</summary>
        public bool HasFiredScriptedEncounter(string encounterId) =>
            _scriptedEncountersFired.Contains(encounterId);

        /// <summary>True when a treasure chest with the given id has been opened.</summary>
        public bool HasOpenedChest(string chestId) => _chestsOpened.Contains(chestId);

        // ── Lifecycle ────────────────────────────────────────────────────────

        /// <summary>
        /// Begin a dungeon run — clears every counter and list, seeds the run,
        /// and places the Keeper in the entry room. Mirrors <c>startRun</c> in
        /// <c>dungeon3dRuntimeStore.ts</c>.
        /// </summary>
        public void StartRun(string dungeonId, string entryRoomId, Vector3 heroPosition, int seed)
        {
            _dungeonId = dungeonId ?? string.Empty;
            _currentRoomId = entryRoomId ?? string.Empty;
            _heroPosition = heroPosition;
            _dungeonRunSeed = seed;
            _randomEncounterCount = 0;
            _secondsSinceLastEncounter = 0f;
            _checkpointsReached.Clear();
            _loreStonesRead.Clear();
            _scriptedEncountersFired.Clear();
            _chestsOpened.Clear();
            _secretRoomsFound.Clear();
            _inCombat = false;
            _bossDefeated = false;
            _runActive = true;
            // NOTE: StartRun deliberately does NOT touch the encounter handoff or
            // the hero-vitals snapshot. Both must survive the ATB scene
            // round-trip — when the dungeon scene reloads after a battle, the
            // controller inspects HasPendingEncounter to decide resume-vs-fresh.
            // EndRun and ClearPendingEncounter own those resets.
            RunStateChanged.Invoke();
        }

        /// <summary>
        /// Tear down the run — clears all runtime state. Called on dungeon exit.
        /// Mirrors <c>endRun</c> in <c>dungeon3dRuntimeStore.ts</c>.
        /// </summary>
        public void EndRun()
        {
            _dungeonId = string.Empty;
            _currentRoomId = string.Empty;
            _heroPosition = Vector3.zero;
            _dungeonRunSeed = 0;
            _randomEncounterCount = 0;
            _secondsSinceLastEncounter = 0f;
            _checkpointsReached.Clear();
            _loreStonesRead.Clear();
            _scriptedEncountersFired.Clear();
            _chestsOpened.Clear();
            _secretRoomsFound.Clear();
            _inCombat = false;
            _bossDefeated = false;
            _runActive = false;
            _pendingEncounterId = string.Empty;
            _pendingEncounterIsBoss = false;
            _encounterResumePosition = Vector3.zero;
            _heroHp = -1f;
            _heroMaxHp = -1f;
            _heroMana = -1f;
            _heroMaxMana = -1f;
            RunStateChanged.Invoke();
        }

        // ── Hero movement / room tracking ────────────────────────────────────

        /// <summary>Update the Keeper's floor-plane position (driven by the scene loop).</summary>
        public void SetHeroPosition(Vector3 position)
        {
            _heroPosition = position;
            // High-frequency caller — does NOT raise RunStateChanged every frame.
        }

        /// <summary>
        /// Record that the Keeper has crossed into a new room. No-op when the id
        /// is unchanged so frame-rate callers do not churn subscribers.
        /// </summary>
        public void SetCurrentRoom(string roomId)
        {
            if (!_runActive || string.IsNullOrEmpty(roomId) || _currentRoomId == roomId) return;
            _currentRoomId = roomId;
            RoomChanged.Invoke(roomId);
            RunStateChanged.Invoke();
        }

        /// <summary>Record a secret room as discovered. Returns true on first discovery.</summary>
        public bool MarkSecretRoomFound(string roomId)
        {
            if (!_runActive || string.IsNullOrEmpty(roomId)) return false;
            if (!AddUnique(_secretRoomsFound, roomId)) return false;
            RunStateChanged.Invoke();
            return true;
        }

        // ── Encounter cadence ────────────────────────────────────────────────

        /// <summary>
        /// Advance the encounter cooldown clock by <paramref name="dtSeconds"/>.
        /// Called from the scene loop. No-op while in combat or with no run.
        /// </summary>
        public void TickEncounterClock(float dtSeconds)
        {
            if (!_runActive || _inCombat || dtSeconds <= 0f) return;
            _secondsSinceLastEncounter += dtSeconds;
        }

        /// <summary>
        /// Register that a random encounter has fired — increments the count,
        /// resets the cooldown clock, and sets <see cref="InCombat"/>.
        /// </summary>
        public void RegisterRandomEncounter()
        {
            if (!_runActive) return;
            _randomEncounterCount += 1;
            _secondsSinceLastEncounter = 0f;
            _inCombat = true;
            RunStateChanged.Invoke();
        }

        /// <summary>
        /// Register a SCRIPTED encounter (or the mini-boss): resets the cooldown
        /// clock and sets <see cref="InCombat"/>, but does NOT increment the
        /// random-encounter count (only random encounters count toward the cap).
        /// </summary>
        public void RegisterScriptedEncounter(string encounterId)
        {
            if (!_runActive) return;
            if (!string.IsNullOrEmpty(encounterId))
                AddUnique(_scriptedEncountersFired, encounterId);
            _secondsSinceLastEncounter = 0f;
            _inCombat = true;
            RunStateChanged.Invoke();
        }

        // ── ATB encounter handoff (survives the battle-scene round-trip) ─────

        /// <summary>
        /// Record an encounter as the in-flight ATB battle just before the scene
        /// routes to <c>ATBBattle</c>. This SO survives <c>SceneManager.LoadScene</c>,
        /// so the encounter id, the boss flag and the Keeper's resume position
        /// cross into the battle scene and back. <paramref name="resumePosition"/>
        /// is where the dungeon places the hero when its scene reloads.
        /// </summary>
        public void BeginEncounterHandoff(string encounterId, bool isBoss, Vector3 resumePosition)
        {
            _pendingEncounterId = encounterId ?? string.Empty;
            _pendingEncounterIsBoss = isBoss;
            _encounterResumePosition = resumePosition;
            _inCombat = true;
            RunStateChanged.Invoke();
        }

        /// <summary>
        /// Resolve the in-flight encounter on dungeon re-entry. Clears the combat
        /// lock + the cooldown clock, marks the boss defeated on a boss-encounter
        /// <paramref name="victory"/>, and clears the pending handoff. Idempotent
        /// — a no-op when there is no pending encounter. Returns true when an
        /// encounter was actually resumed.
        /// </summary>
        public bool ResumeAfterEncounter(bool victory)
        {
            if (!HasPendingEncounter) return false;
            if (victory && _pendingEncounterIsBoss && _runActive && !_bossDefeated)
            {
                _bossDefeated = true;
            }
            _pendingEncounterId = string.Empty;
            _pendingEncounterIsBoss = false;
            _encounterResumePosition = Vector3.zero;
            _secondsSinceLastEncounter = 0f;
            _inCombat = false;
            RunStateChanged.Invoke();
            return true;
        }

        /// <summary>Clears the pending encounter handoff without resolving it (teardown).</summary>
        public void ClearPendingEncounter()
        {
            if (!HasPendingEncounter) return;
            _pendingEncounterId = string.Empty;
            _pendingEncounterIsBoss = false;
            _encounterResumePosition = Vector3.zero;
            RunStateChanged.Invoke();
        }

        // ── Hero vitals (preserved across the ATB round-trip + checkpoint heal) ─

        /// <summary>
        /// Snapshot the Keeper's HP / mana into the run state. Called by the
        /// encounter trigger just before the battle scene loads (so the vitals
        /// survive the round-trip) and by the scene loop so a checkpoint heal
        /// always works from current numbers.
        /// </summary>
        public void SetHeroVitals(float hp, float maxHp, float mana, float maxMana)
        {
            _heroHp = hp;
            _heroMaxHp = Mathf.Max(0f, maxHp);
            _heroMana = mana;
            _heroMaxMana = Mathf.Max(0f, maxMana);
            // High-frequency-safe — no RunStateChanged churn (mirrors SetHeroPosition).
        }

        /// <summary>
        /// Restore the Keeper to full HP and mana — the checkpoint-shrine heal.
        /// Returns true when vitals were available to heal. The actual hero
        /// component re-reads <see cref="HeroHp"/> / <see cref="HeroMana"/> after
        /// this call (the dungeon module owns no hero-stat type).
        /// </summary>
        public bool HealHeroToFull()
        {
            if (!HasHeroVitals) return false;
            _heroHp = _heroMaxHp;
            _heroMana = _heroMaxMana;
            RunStateChanged.Invoke();
            return true;
        }

        /// <summary>
        /// Resolve the in-flight encounter battle — clears <see cref="InCombat"/>
        /// and resets the cooldown clock so the next encounter cannot fire
        /// immediately. Called once the ATB battle's resume completes.
        /// </summary>
        public void ResolveEncounter()
        {
            if (!_runActive) { _inCombat = false; return; }
            _secondsSinceLastEncounter = 0f;
            _inCombat = false;
            RunStateChanged.Invoke();
        }

        /// <summary>
        /// Record that the dungeon's mini-boss has been defeated this run.
        /// Idempotent — no-op once set. Unlocks the Apothecary back-door exit.
        /// </summary>
        public void MarkBossDefeated()
        {
            if (!_runActive || _bossDefeated) return;
            _bossDefeated = true;
            RunStateChanged.Invoke();
        }

        // ── Checkpoints ──────────────────────────────────────────────────────

        /// <summary>
        /// Record a checkpoint shrine as reached this run. No-op when the shrine
        /// is already in the set (a re-visited shrine re-heals nothing). Returns
        /// true when the shrine was newly recorded (its first activation).
        /// </summary>
        public bool ReachCheckpoint(string checkpointId)
        {
            if (!_runActive || string.IsNullOrEmpty(checkpointId)) return false;
            if (!AddUnique(_checkpointsReached, checkpointId)) return false;
            CheckpointReached.Invoke(checkpointId);
            RunStateChanged.Invoke();
            return true;
        }

        // ── Lore stones ──────────────────────────────────────────────────────

        /// <summary>
        /// Mark a lore stone read. Returns true on the first read. When
        /// <paramref name="totalLoreCount"/> stones have all been read, fires
        /// <see cref="QuestlineBeatComplete"/> once (advances the Healer's
        /// Garden questline — storyline §4.1 beat 3 "read all lore").
        /// </summary>
        public bool ReadLoreStone(string loreStoneId, int totalLoreCount)
        {
            if (!_runActive || string.IsNullOrEmpty(loreStoneId)) return false;
            if (!AddUnique(_loreStonesRead, loreStoneId)) return false;
            LoreStoneRead.Invoke(loreStoneId);
            RunStateChanged.Invoke();
            if (totalLoreCount > 0 && _loreStonesRead.Count >= totalLoreCount)
                QuestlineBeatComplete.Invoke();
            return true;
        }

        // ── Treasure chests ──────────────────────────────────────────────────

        /// <summary>Record a treasure chest as opened. Returns true on first open.</summary>
        public bool OpenChest(string chestId)
        {
            if (!_runActive || string.IsNullOrEmpty(chestId)) return false;
            if (!AddUnique(_chestsOpened, chestId)) return false;
            RunStateChanged.Invoke();
            return true;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Append a value to a list if not already present. Returns true if added.</summary>
        private static bool AddUnique(List<string> list, string value)
        {
            if (list.Contains(value)) return false;
            list.Add(value);
            return true;
        }

        // ScriptableObject assets persist edits between play sessions in the
        // editor; a runtime-only SO must reset itself on enable so a stale run
        // never leaks into a fresh session. OnEnable fires on SO load/domain reload
        // (NOT per scene load), so clearing the pending encounter here is safe — the
        // battle round-trip keeps the SO loaded and does not re-fire this.
        // WO-770.9 (fixes D11): also clear the run IDENTITY (_dungeonId/_currentRoomId)
        // and the five progress lists — previously only the flags reset, so a fresh
        // session could read the PRIOR run's dungeon id, room, and read/cleared lists
        // in the window before StartRun overwrites them.
        private void OnEnable()
        {
            _runActive = false;
            _inCombat = false;
            _bossDefeated = false;
            _pendingEncounterId = string.Empty;
            _pendingEncounterIsBoss = false;
            _encounterResumePosition = Vector3.zero;
            _heroHp = -1f;
            _heroMaxHp = -1f;
            _heroMana = -1f;
            _heroMaxMana = -1f;

            // WO-770.9: run identity + progress lists (the stale-read window, D11).
            _dungeonId = string.Empty;
            _currentRoomId = string.Empty;
            _checkpointsReached.Clear();
            _loreStonesRead.Clear();
            _scriptedEncountersFired.Clear();
            _chestsOpened.Clear();
            _secretRoomsFound.Clear();
        }
    }
}
