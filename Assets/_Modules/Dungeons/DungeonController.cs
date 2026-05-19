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

        [Tooltip("The Keeper's dungeon-walk controller — input is held off across " +
                 "the spawn teleport, then enabled once the run is live. Optional: " +
                 "auto-found on the hero rig when left unset.")]
        [SerializeField] private DungeonHero _heroController;

        [Tooltip("Cinemachine camera that follows the Keeper (top-down isometric tilt).")]
        [SerializeField] private CinemachineCamera _followCamera;

        [Tooltip("Optional isometric camera rig component. When set, it owns the " +
                 "framing maths; when null the controller applies the framing " +
                 "inline from the offset/pitch fields below.")]
        [SerializeField] private DungeonCameraRig _cameraRig;

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

        [Tooltip("The dungeon ambient clip (echoes-beneath-elarion). MAY BE NULL " +
                 "until the audio file is imported under Assets/Audio/ — the code " +
                 "path is guarded and logs a warning rather than erroring. The " +
                 "AudioSource's own clip is used as a fallback when this is unset.")]
        [SerializeField] private AudioClip _ambientBgmClip;

        [Tooltip("Dungeon BGM volume — audio-mix-spec.md §2 fixes the 'dungeon' " +
                 "track at 0.25 (very soft, ambient only). Master volume scales " +
                 "this multiplicatively once the MusicDirector lands.")]
        [SerializeField, Range(0f, 1f)] private float _ambientBgmVolume = 0.25f;

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

            // Resolve the hero's walk controller up-front so input can be held
            // off across the spawn teleport (the Keeper must not drift while the
            // layout loads / the camera snaps into frame).
            if (_heroController == null && _hero != null)
                _heroController = _hero.GetComponent<DungeonHero>();
            if (_heroController != null)
                _heroController.SetInputEnabled(false);

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

            // The run is live and the Keeper is framed — hand movement back.
            if (_heroController != null)
                _heroController.SetInputEnabled(true);
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

            float facingY = Layout?.spawn?.facingY ?? 0f;

            // The DungeonHero owns the safe teleport (it disables its own
            // CharacterController across the move and clears any tap target).
            if (_heroController != null)
            {
                _heroController.Teleport(spawnPos, facingY);
                return;
            }

            // No DungeonHero present — fall back to a raw transform move,
            // disabling any CharacterController so it does not fight the teleport.
            var cc = _hero.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            _hero.position = spawnPos;
            _hero.rotation = Quaternion.Euler(0f, facingY, 0f);

            if (cc != null) cc.enabled = true;
        }

        // ── Camera ───────────────────────────────────────────────────────────

        /// <summary>
        /// Aims the Cinemachine follow camera at the Keeper with the spec's
        /// top-down isometric tilt. Prefers the dedicated <see cref="DungeonCameraRig"/>
        /// (it owns the framing maths); falls back to an inline offset/pitch
        /// setup for a hand-wired camera. The camera follows the hero — and feeds
        /// the hero its yaw so WASD stays screen-relative under the tilt.
        /// </summary>
        private void ConfigureCamera()
        {
            if (_hero == null) return;

            // Preferred path: the camera rig component self-configures.
            if (_cameraRig != null)
            {
                _cameraRig.Bind(_hero);
            }
            else if (_followCamera != null)
            {
                _followCamera.Follow = _hero;
                // LookAt is left unset: with no Aim component the rig keeps the
                // authored fixed pitch — the steady isometric tilt the spec asks
                // for (no orbit, no free-look — that is the village rig).
                _followCamera.LookAt = null;

                // Seat the camera at the authored isometric offset behind + above
                // the hero, tilted down. CinemachineFollow eases it along.
                var camTransform = _followCamera.transform;
                camTransform.position = _hero.position + _cameraOffset;
                camTransform.rotation = Quaternion.Euler(_cameraPitch, 0f, 0f);

                var follow = _followCamera.GetComponent<CinemachineFollow>();
                if (follow != null)
                {
                    follow.FollowOffset = _cameraOffset;
                    var settings = follow.TrackerSettings;
                    settings.PositionDamping = new Vector3(1.4f, 1.4f, 1.4f);
                    follow.TrackerSettings = settings;
                }
            }

            // Tell the hero which camera its WASD vector is relative to so
            // screen-up always maps to the camera's forward under the tilt.
            if (_heroController != null)
            {
                Camera unityCam = ResolveUnityCamera();
                if (unityCam != null) _heroController.SetCamera(unityCam);
            }
        }

        /// <summary>The Unity camera the Cinemachine brain drives — for hero input framing.</summary>
        private Camera ResolveUnityCamera()
        {
            // The CinemachineCamera is a controller, not a renderer — the real
            // Camera is the one with the CinemachineBrain (usually Camera.main).
            if (Camera.main != null) return Camera.main;
            return Object.FindFirstObjectByType<Camera>();
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

        /// <summary>
        /// Starts the looping dungeon ambient BGM (echoes-beneath-elarion) at the
        /// audio-mix-spec §2 volume (0.25). Guards a missing clip: the MP3 may not
        /// be imported yet — when no clip is present this logs a warning and the
        /// dungeon plays silently rather than erroring (port spec Week 5 note).
        /// </summary>
        private void StartAmbientAudio()
        {
            if (_ambientBgm == null) return;

            _ambientBgm.loop = true;
            _ambientBgm.playOnAwake = false;
            _ambientBgm.volume = _ambientBgmVolume;

            // Prefer the explicitly-assigned clip; otherwise fall back to any
            // clip already on the AudioSource (the scene builder may wire it
            // there directly once the MP3 is imported).
            if (_ambientBgmClip != null)
                _ambientBgm.clip = _ambientBgmClip;

            if (_ambientBgm.clip == null)
            {
                Debug.LogWarning(
                    "[DungeonController] Dungeon ambient clip 'echoes-beneath-elarion' " +
                    "is not assigned — the MP3 is not yet imported under Assets/Audio/. " +
                    "Dungeon will play silently; wire the clip when the file lands.");
                return;
            }

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
