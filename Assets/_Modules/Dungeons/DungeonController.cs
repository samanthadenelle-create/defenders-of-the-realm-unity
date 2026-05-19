// =============================================================================
// DungeonController — the Healer's Cottage scene orchestrator (Weeks 5-6).
// -----------------------------------------------------------------------------
// Port spec Part 3 row:
//   src/modules/dungeons/ -> _Modules/Dungeons/DungeonController.cs
// Port spec Part 5 Week 5: "scene manager that loads room layout, places hero
// at spawn, manages camera (Cinemachine follow rig, top-down isometric tilt)."
//
// One controller orchestrates the Dungeon_HealersCottage scene:
//   1. Loads the canonical layout JSON (StreamingAssets, via DungeonLayoutLoader).
//   2. Starts a run on the DungeonRuntimeState ScriptableObject.
//   3. Places the Keeper at the layout's spawn point.
//   4. Aims the Cinemachine follow camera at the hero (top-down isometric tilt).
//   5. Tracks which room the hero is in each frame; ticks the encounter clock.
//   6. Hands the layout to the dungeon's interactables (lore stones, checkpoints,
//      encounter triggers, Bryn, lantern) so each wires itself off shared data.
//
// CANON: the town is "Avalon", the world-tree is "Elarion" / "the Heart", the
// dungeon NPC is "Bryn" — all canon names. They are NOT typed inline in
// user-facing copy (port spec Part 4 routes them through canon-strings.json);
// this file uses them only in comments.
//
// All async flows return UniTask — never `async void` (port spec Part 3 mandate).
// =============================================================================

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DeNelle.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// Orchestrates the Healer's Cottage dungeon scene — loads the room layout,
    /// places the Keeper at spawn, drives the Cinemachine follow camera, and
    /// tracks the hero's current room. The KayKit Dungeon model pack is not yet
    /// imported, so this controller builds the runtime scaffolding (layout load,
    /// run lifecycle, camera, room tracking, interactable wiring) — the final
    /// mesh assembly lands once the pack is staged (port spec Part 7 Week 5).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonController : MonoBehaviour
    {
        [Header("Dungeon identity")]
        [Tooltip("Canonical dungeon id — keys the layout JSON under " +
                 "StreamingAssets/Data/Canonical/dungeons/. v2 foundation ships " +
                 "the Healer's Cottage only.")]
        [SerializeField] private string _dungeonId = "healers-cottage";

        [Header("State")]
        [Tooltip("The runtime-only ScriptableObject holding the active run " +
                 "(current room, checkpoints, lore-stones). Shared with the " +
                 "dungeon interactables.")]
        [SerializeField] private DungeonRuntimeState _runtimeState;

        [Header("Scene actors")]
        [Tooltip("The Keeper's hero rig — moved to the layout's spawn point on load.")]
        [SerializeField] private Transform _hero;

        [Tooltip("Cinemachine camera that follows the Keeper (top-down isometric tilt).")]
        [SerializeField] private CinemachineCamera _followCamera;

        [Tooltip("Hero-attached lantern — handed the layout's oil stones on load.")]
        [SerializeField] private Lantern _lantern;

        [Tooltip("Bryn the Wanderer — placed + configured from the layout's bryn block.")]
        [SerializeField] private Bryn _bryn;

        [Header("Interactable parents (wired by the scene builder)")]
        [Tooltip("Parent for the spawned lore-stone interactables.")]
        [SerializeField] private Transform _loreStoneRoot;

        [Tooltip("Parent for the spawned checkpoint shrines.")]
        [SerializeField] private Transform _checkpointRoot;

        [Tooltip("Parent for the spawned encounter triggers.")]
        [SerializeField] private Transform _encounterRoot;

        [Header("Camera framing (top-down isometric)")]
        [Tooltip("Camera offset from the hero, world units. The default gives the " +
                 "spec's top-down isometric tilt (high, pulled back, looking down).")]
        [SerializeField] private Vector3 _cameraOffset = new Vector3(0f, 13f, -9f);

        [Tooltip("Camera pitch in degrees — the isometric down-tilt.")]
        [SerializeField] private float _cameraPitch = 52f;

        [Header("Audio")]
        [Tooltip("Looping dungeon ambient BGM source — echoes-beneath-elarion.mp3 " +
                 "at the mix-spec volume (port spec Part 5 Week 5).")]
        [SerializeField] private AudioSource _ambientBgm;

        // ── Runtime ──────────────────────────────────────────────────────────

        /// <summary>The loaded layout, or null before <see cref="EnterDungeon"/> completes.</summary>
        public DungeonLayout Layout { get; private set; }

        /// <summary>True once the dungeon has finished loading and the run is live.</summary>
        public bool Ready { get; private set; }

        /// <summary>The runtime run state — current room, checkpoints, lore read.</summary>
        public DungeonRuntimeState RuntimeState => _runtimeState;

        /// <summary>The hero's last-known room id, cached to detect room crossings.</summary>
        private string _lastRoomId = string.Empty;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Start()
        {
            EnterDungeon().Forget();
        }

        private void OnDestroy()
        {
            // Tear the run down so a stale run never leaks into the next scene.
            if (_runtimeState != null && _runtimeState.RunActive)
                _runtimeState.EndRun();
        }

        /// <summary>
        /// Loads the canonical layout, starts the run, places the Keeper at the
        /// spawn point, aims the follow camera and wires up the interactables.
        /// Returns a <see cref="UniTask"/> — never <c>async void</c>.
        /// </summary>
        public async UniTask EnterDungeon()
        {
            Ready = false;

            Layout = await DungeonLayoutLoader.LoadAsync(_dungeonId);
            if (Layout == null)
            {
                Debug.LogError($"[DungeonController] Layout '{_dungeonId}' failed to load — " +
                               "dungeon cannot assemble.");
                return;
            }

            int seed = MakeRunSeed();
            Vector3 spawnPos = ResolveSpawnPosition();
            string entryRoomId = Layout.spawn?.roomId ?? Layout.entryRoomId;

            if (_runtimeState != null)
                _runtimeState.StartRun(Layout.id, entryRoomId, spawnPos, seed);

            PlaceHero(spawnPos);
            ConfigureCamera();
            ConfigureLantern();
            ConfigureBryn();
            StartAmbientAudio();

            _lastRoomId = entryRoomId;
            Ready = true;
        }

        /// <summary>
        /// Exits the dungeon — tears the run down and routes back to the village.
        /// Called by the Apothecary back-door once the mini-boss is defeated.
        /// </summary>
        public UniTask ExitToVillage()
        {
            if (_runtimeState != null && _runtimeState.RunActive)
                _runtimeState.EndRun();
            StopAmbientAudio();
            return SceneRouter.LoadSceneWithFade(SceneRouter.Village);
        }

        // ── Per-frame: room tracking + encounter clock ───────────────────────

        private void Update()
        {
            if (!Ready || _runtimeState == null || _hero == null) return;

            // Push the hero's live position into the run state — drives the
            // encounter engine and the proximity checks on every interactable.
            Vector3 heroPos = _hero.position;
            _runtimeState.SetHeroPosition(heroPos);

            // Detect a room crossing — the room whose footprint contains the
            // hero. Secret-room footprints are checked too, so brushing through
            // an illusory wall registers the discovery.
            DungeonRoom room = Layout.RoomAt(heroPos);
            if (room != null && room.id != _lastRoomId)
            {
                _lastRoomId = room.id;
                _runtimeState.SetCurrentRoom(room.id);
                if (room.secret) _runtimeState.MarkSecretRoomFound(room.id);
            }

            // Advance the encounter cooldown clock (no-op while in combat). v1
            // gates random encounters off (Layout.disableRandomEncounters) — the
            // clock still ticks so v1.1 can flip them on without a code change.
            _runtimeState.TickEncounterClock(Time.deltaTime);
        }

        // ── Hero placement ───────────────────────────────────────────────────

        /// <summary>The layout's spawn world position, falling back to the origin.</summary>
        private Vector3 ResolveSpawnPosition()
        {
            if (Layout?.spawn != null) return Layout.spawn.position.ToWorld();

            // No explicit spawn — drop the Keeper at the centre of the entry room.
            DungeonRoom entry = Layout?.FindRoom(Layout.entryRoomId);
            if (entry?.bounds != null) return entry.bounds.Center;
            return Vector3.zero;
        }

        /// <summary>Moves the Keeper to <paramref name="spawnPos"/> facing the layout's heading.</summary>
        private void PlaceHero(Vector3 spawnPos)
        {
            if (_hero == null) return;

            // The hero rig is a CharacterController (port spec Part 2) — disable
            // it across the teleport so the controller does not fight the move.
            var cc = _hero.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            _hero.position = spawnPos;
            float facingY = Layout?.spawn?.facingY ?? 0f;
            _hero.rotation = Quaternion.Euler(0f, facingY, 0f);

            if (cc != null) cc.enabled = true;
        }

        // ── Camera ───────────────────────────────────────────────────────────

        /// <summary>
        /// Aims the Cinemachine follow camera at the Keeper with the spec's
        /// top-down isometric tilt. Uses a position composer + the offset/pitch
        /// tuning fields; the camera follows the hero for the whole run.
        /// </summary>
        private void ConfigureCamera()
        {
            if (_followCamera == null || _hero == null) return;

            _followCamera.Follow = _hero;
            _followCamera.LookAt = _hero;

            // Seat the camera at the authored isometric offset behind + above
            // the hero, tilted down. The Cinemachine follow component eases it
            // along as the Keeper walks.
            var camTransform = _followCamera.transform;
            camTransform.position = _hero.position + _cameraOffset;
            camTransform.rotation = Quaternion.Euler(_cameraPitch, 0f, 0f);

            // A hard-locked follow offset gives the steady top-down framing the
            // spec asks for (no orbit, no free-look — that is the village rig).
            var follow = _followCamera.GetComponent<CinemachineFollow>();
            if (follow != null)
            {
                follow.FollowOffset = _cameraOffset;
                follow.TrackerSettings.PositionDamping = new Vector3(1.4f, 1.4f, 1.4f);
            }
        }

        // ── Interactable + actor wiring ──────────────────────────────────────

        /// <summary>Hands the lantern its oil-stone refill points from the layout.</summary>
        private void ConfigureLantern()
        {
            if (_lantern == null || Layout == null) return;
            _lantern.Configure(this, Layout.oilStones, _hero);
        }

        /// <summary>Places + configures Bryn from the layout's <c>bryn</c> block.</summary>
        private void ConfigureBryn()
        {
            if (_bryn == null || Layout?.bryn == null) return;
            _bryn.Configure(Layout.bryn, _runtimeState);
        }

        // ── Audio ────────────────────────────────────────────────────────────

        /// <summary>Starts the looping dungeon ambient BGM (echoes-beneath-elarion).</summary>
        private void StartAmbientAudio()
        {
            if (_ambientBgm == null) return;
            _ambientBgm.loop = true;
            if (!_ambientBgm.isPlaying) _ambientBgm.Play();
        }

        /// <summary>Stops the ambient BGM on dungeon exit.</summary>
        private void StopAmbientAudio()
        {
            if (_ambientBgm != null && _ambientBgm.isPlaying) _ambientBgm.Stop();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Produces the per-run seed for the deterministic encounter sequence.
        /// A fresh, non-zero seed each run; v1.1 can swap this for a save-derived
        /// value so a run is fully reproducible.
        /// </summary>
        private static int MakeRunSeed()
        {
            int seed = System.Environment.TickCount ^ (int)(Time.realtimeSinceStartup * 1000f);
            return seed == 0 ? 1 : seed;
        }

        /// <summary>The interactable parent transforms — read by the scene builder.</summary>
        public IReadOnlyList<Transform> InteractableRoots =>
            new[] { _loreStoneRoot, _checkpointRoot, _encounterRoot };
    }
}
