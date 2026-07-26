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
using DeNelle.Core.Diagnostics;
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

        [Header("Crafting (Workstream C)")]
        [Tooltip("The shared crafting-ingredient inventory ScriptableObject — wired " +
                 "to the ingredient pickups + the crafting pedestal on load.")]
        [SerializeField] private DungeonInventory _dungeonInventory;

        [Tooltip("The crafting-pedestal interactable — configured from " +
                 "crafting-recipes.json's pedestal block. Replaces the §5.4 " +
                 "placeholder shard pedestal in the Hidden Vault.")]
        [SerializeField] private CraftingPedestal _craftingPedestal;

        [Tooltip("Parent for the spawned ingredient-pickup motes — one child per " +
                 "crafting-recipes.json ingredientPlacements[] entry, in order.")]
        [SerializeField] private Transform _ingredientRoot;

        [Tooltip("The UI Toolkit crafting panel controller — subscribed to the " +
                 "crafting pedestal's open/close events on load.")]
        [SerializeField] private CraftingPanelController _craftingPanel;

        [Header("Dungeon HUD (Workstream C)")]
        [Tooltip("The dungeon HUD controller — fed the Lantern reference so its " +
                 "oil meter reads the lantern's public API each frame.")]
        [SerializeField] private DungeonHudController _dungeonHud;

        [Header("Interactable parents (wired by the scene builder)")]
        [Tooltip("Parent for the spawned lore-stone interactables.")]
        [SerializeField] private Transform _loreStoneRoot;

        [Tooltip("Parent for the spawned checkpoint shrines.")]
        [SerializeField] private Transform _checkpointRoot;

        [Tooltip("Parent for the spawned encounter triggers.")]
        [SerializeField] private Transform _encounterRoot;

        [Header("Hero vitals (placeholder — no dungeon hero-stat type yet)")]
        [Tooltip("Baseline hero HP seeded onto the run state at run start so the " +
                 "checkpoint heal + the ATB round-trip have live numbers to work " +
                 "from. The dungeon module owns no hero-stat type; when a real one " +
                 "lands it should drive SetHeroVitals each frame instead.")]
        [SerializeField] private float _heroBaselineHp = 120f;

        [Tooltip("Baseline hero mana seeded onto the run state at run start.")]
        [SerializeField] private float _heroBaselineMana = 60f;

        [Header("Camera framing (top-down isometric)")]
        [Tooltip("Camera offset from the hero, world units. Gives the spec's " +
                 "top-down isometric tilt (pulled back + up, looking down). Capped " +
                 "at bind time by CameraMaxHeightAboveHero so the rig sits just over " +
                 "the ~4u room ceiling, not far above it (owner felt-test 2026-07-16).")]
        [SerializeField] private Vector3 _cameraOffset = new Vector3(0f, 9f, -6.25f);

        [Tooltip("Camera pitch in degrees — the isometric down-tilt.")]
        [SerializeField] private float _cameraPitch = 52f;

        /// <summary>
        /// Hard cap on how far above the hero the inline follow camera may sit
        /// (world units). The rooms carry a ~4u ceiling (DungeonSceneBuilder.
        /// WallHeight); ~9u keeps the rig just over the roofline for a framed
        /// dungeon-iso look. Mirrors DungeonCameraRig._maxHeightAboveHero so both
        /// camera paths behave identically. Owner-tunable via that rig field.
        /// </summary>
        private const float CameraMaxHeightAboveHero = 9f;

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

        /// <summary>
        /// The canonical lore-fragment set (lore-fragments.json) — feeds Bryn's
        /// entrance line + each lore stone's reading text. Null when the file
        /// could not be loaded; the interactables then fall back to inline copy.
        /// </summary>
        private LoreFragmentSet _loreFragments;

        /// <summary>The hydrated scripted/boss encounter triggers, in layout order.</summary>
        private readonly List<EncounterTrigger> _encounterTriggers = new List<EncounterTrigger>();

        /// <summary>
        /// The canonical crafting data set (crafting-recipes.json) — feeds the
        /// ingredient pickups, the crafting pedestal and the crafting UI. Null
        /// when the file could not be loaded; the crafting layer then stays inert.
        /// </summary>
        private CraftingDataSet _craftingData;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Start()
        {
            EnterDungeon().Forget();
        }

        private void OnDestroy()
        {
            if (_runtimeState == null || !_runtimeState.RunActive) return;

            // CRITICAL (BUG-008 round-trip): when an encounter battle is pending,
            // this scene is being torn down to route into ATBBattle — NOT a
            // genuine dungeon exit. EndRun() would wipe the encounter handoff +
            // hero vitals the ScriptableObject is carrying across the round-trip,
            // so the dungeon could never resume. Leave the run intact; the
            // resume path on re-entry (or a real ExitToVillage) ends it.
            if (_runtimeState.HasPendingEncounter) return;

            // No battle pending — a real teardown (e.g. quit-to-menu, not the
            // Apothecary exit). WO-749: bank any gathered scatter to the persistent
            // larder before the per-run inventory dies with the scene. (The
            // Apothecary ExitToVillage path already deposited + set RunActive false,
            // so it returns above and never double-deposits here.)
            if (_dungeonInventory != null)
                DungeonLootGrant.DepositDungeonInventory(_dungeonInventory);
            _runtimeState.EndRun();
        }

        /// <summary>
        /// Loads the canonical layout, starts the run, places the Keeper at the
        /// spawn point, aims the follow camera and wires up the interactables.
        /// Returns a <see cref="UniTask"/> — never <c>async void</c>.
        /// </summary>
        public async UniTask EnterDungeon()
        {
            using var _flow = FlowTrace.Enter("Dungeon", $"EnterDungeon id='{_dungeonId}'");
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
                // HARD STOP: no layout means NO geometry, NO actors — the scene would
                // sit blank with the hero frozen (input was disabled above). Fail loud
                // AND hand input back so the Keeper is never stuck in a dead scene.
                FlowTrace.Fail("Dungeon",
                    $"EnterDungeon: Layout '{_dungeonId}' failed to load — dungeon cannot " +
                    "assemble. Re-enabling hero input so the Keeper is not frozen in a blank scene.");
                if (_heroController != null) _heroController.SetInputEnabled(true);
                return;
            }
            FlowTrace.Step("Dungeon",
                $"EnterDungeon: layout '{Layout.id}' loaded — rooms={Layout.rooms?.Length ?? 0}, " +
                $"loreStones={Layout.loreStones?.Length ?? 0}, checkpoints={Layout.checkpoints?.Length ?? 0}, " +
                $"scriptedEncounters={Layout.scriptedEncounters?.Length ?? 0}, miniBoss={(Layout.miniBoss != null ? "yes" : "no")}.");
            // VERIFY rooms hydrated: a zero-room layout builds no playable space.
            if ((Layout.rooms?.Length ?? 0) == 0)
                FlowTrace.Warn("Dungeon",
                    $"EnterDungeon: layout '{Layout.id}' has ZERO rooms — the dungeon will have no " +
                    "navigable space and room-tracking will never resolve a room.");

            // Load the canonical lore-fragment set — feeds Bryn's entrance line
            // and each lore stone's reading text. A null set is non-fatal: the
            // interactables fall back to the layout JSON's inline copy.
            _loreFragments = await LoreFragmentsLoader.LoadAsync();

            // Load the canonical crafting data (Workstream C) — feeds the
            // ingredient pickups, the crafting pedestal and the crafting UI. A
            // null set is non-fatal: the crafting layer simply stays inert.
            _craftingData = await CraftingDataLoader.LoadAsync();

            // An encounter battle just resolved if the run state still carries a
            // pending handoff — this scene LOAD is the ATB round-trip return.
            bool resuming = _runtimeState != null && _runtimeState.HasPendingEncounter;

            // A fresh run starts with an empty larder; a resume (the ATB
            // round-trip return) keeps whatever ingredients were gathered.
            if (!resuming && _dungeonInventory != null)
                _dungeonInventory.Clear();

            int seed = MakeRunSeed();
            Vector3 spawnPos = resuming
                ? _runtimeState.EncounterResumePosition
                : ResolveSpawnPosition();
            string entryRoomId = Layout.spawn?.roomId ?? Layout.entryRoomId;

            // StartRun deliberately preserves the encounter handoff + hero
            // vitals so they survive this reload (see DungeonRuntimeState).
            if (_runtimeState != null && !resuming)
                _runtimeState.StartRun(Layout.id, entryRoomId, spawnPos, seed);
            else if (_runtimeState != null && !_runtimeState.RunActive)
                // A resume after a process-fresh reload still needs a live run.
                _runtimeState.StartRun(Layout.id, entryRoomId, spawnPos, seed);

            // Seed the hero vitals on a fresh run so the checkpoint heal +
            // the ATB round-trip have numbers (Week-6 checklist item 7). On a
            // resume the vitals already rode the round-trip on the run state, so
            // they are left untouched.
            if (_runtimeState != null && !resuming && !_runtimeState.HasHeroVitals)
                _runtimeState.SetHeroVitals(
                    _heroBaselineHp, _heroBaselineHp, _heroBaselineMana, _heroBaselineMana);

            PlaceHero(spawnPos);
            ConfigureCamera();
            ConfigureLantern();
            ConfigureBryn();
            DressEntranceNpc();
            HydrateLoreStones();
            HydrateCheckpoints();
            HydrateEncounters();
            ConfigureCrafting();
            HydrateChests();
            HydrateExits();
            ConfigureDungeonHud();
            DressTraversalLinks();
            SweepPlaceholderCubes();
            StartAmbientAudio();

            // Settle any in-flight ATB encounter — the dungeon module's side of
            // the BUG-008 round-trip. The matching EncounterTrigger marks itself
            // fired + (on a boss victory) flags the boss defeated.
            if (resuming)
                ResolvePendingEncounter();

            DungeonRoom currentRoom = Layout.RoomAt(spawnPos);
            _lastRoomId = currentRoom?.id ?? entryRoomId;
            // RENDER-COMMIT: the scene is assembled + the Keeper framed. Ready flips the
            // per-frame loop on, so it self-reports the commit (and which room the hero
            // resolved into) — a blank/frozen run is then diagnosable from the trace.
            FlowTrace.Step("Dungeon",
                $"EnterDungeon: run live (Ready=true) — spawn={spawnPos}, entryRoom='{_lastRoomId}', " +
                $"resuming={resuming}.");
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
            // The crafting inventory is per-run — bank the gathered scatter to the
            // PERSISTENT larder (WO-749 gap 2 bridge) BEFORE clearing it, so delving
            // actually stocks the village crafting supply. Then clear so a stale run's
            // ingredients never leak into the next dungeon run.
            if (_dungeonInventory != null)
            {
                DungeonLootGrant.DepositDungeonInventory(_dungeonInventory);
                _dungeonInventory.Clear();
            }
            StopAmbientAudio();
            // Owner ruling 2026-07-13 ("map the dungeon in"): the exit goes HOME — the
            // merged overworld hub (SceneRouter.Castle -> Main_Castle_Overworld), not the
            // ABANDONED legacy Village scene this predated (canon: Village.unity retired).
            return SceneRouter.LoadSceneWithFade(SceneRouter.Castle);
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

            // WO-770.1: reveal the boss-gated back-door exit the instant the mini-boss falls
            // (it is spawned hidden at the Workshop). The always-open normal exit is separate.
            if (_bossBackDoor != null && !_bossBackDoor.gameObject.activeSelf && _runtimeState.BossDefeated)
            {
                _bossBackDoor.gameObject.SetActive(true);
                FlowTrace.Step("Dungeon", "WO-770.1: boss back-door exit revealed (BossDefeated).");
            }

            // Advance the encounter cooldown clock (no-op while in combat). v1
            // gates random encounters off (Layout.disableRandomEncounters) — the
            // clock still ticks so v1.1 can flip them on without a code change.
            _runtimeState.TickEncounterClock(Time.deltaTime);
        }

        // ── Hero placement ───────────────────────────────────────────────────

        // WO-770.1: the boss-gated back-door exit, spawned HIDDEN and revealed by Update once
        // the mini-boss falls (BossDefeated). Null when the layout has no "workshop" room.
        private DungeonExitInteractable _bossBackDoor;

        /// <summary>
        /// WO-770.1 (fixes the roach-motel D-finding): inject the dungeon's RETURN exits at
        /// runtime (no scene rebake). The rich Healer's Cottage previously only left via the
        /// post-boss Apothecary back-door — a hero who could not (or would not) beat the mini-boss
        /// was trapped. Two exits close that:
        ///   • NORMAL — always open, at the ENTRY room centre, so you can leave any time.
        ///   • BOSS BACK-DOOR — at the Workshop, spawned hidden, revealed once the mini-boss falls.
        /// Both mirror <see cref="DungeonExitInteractable"/>'s walk-in + Interact-button pattern and
        /// route through <see cref="ExitToVillage"/> (banks the run's crafting scatter, ends the run).
        /// Positions come straight off the layout's room bounds — no invented coordinates.
        /// </summary>
        private void HydrateExits()
        {
            if (Layout == null) { FlowTrace.Warn("Dungeon", "HydrateExits: no Layout — no exits placed."); return; }

            // NORMAL exit — the entry room the hero spawned into (spawn.roomId, else entryRoomId).
            DungeonRoom entry = Layout.FindRoom(Layout.spawn?.roomId ?? Layout.entryRoomId);
            if (entry?.bounds != null)
            {
                Vector3 pos = SeatExitOnFloor(entry.bounds.Center);
                DungeonExitInteractable.Spawn(pos, () => ExitToVillage().Forget(), "Leave Dungeon");
                FlowTrace.Step("Dungeon", $"HydrateExits: NORMAL exit at entry room '{entry.id}' centre {pos}.");
            }
            else
            {
                FlowTrace.Warn("Dungeon", "HydrateExits: entry room has no bounds — NORMAL exit NOT placed (run could be un-leavable!).");
            }

            // BOSS BACK-DOOR — the Workshop room (present in the Healer's Cottage layout). Spawned
            // hidden; Update reveals it on BossDefeated. Absent room = another dungeon id: skip it,
            // the normal exit still frees the run.
            DungeonRoom workshop = Layout.FindRoom("workshop");
            if (workshop?.bounds != null)
            {
                Vector3 pos = SeatExitOnFloor(workshop.bounds.Center);
                _bossBackDoor = DungeonExitInteractable.Spawn(pos, () => ExitToVillage().Forget(), "Secret Exit");
                bool alreadyBeaten = _runtimeState != null && _runtimeState.BossDefeated;
                _bossBackDoor.gameObject.SetActive(alreadyBeaten);
                FlowTrace.Step("Dungeon", $"HydrateExits: BOSS back-door at 'workshop' centre {pos} (active={alreadyBeaten}).");
            }
            else
            {
                FlowTrace.Step("Dungeon", "HydrateExits: no 'workshop' room in this layout — no boss back-door (normal exit still frees the run).");
            }
        }

        /// <summary>
        /// Seat an exit's origin on the room floor so its arch stands rather than floating at the
        /// room's mid-height bounds centre (a short ray down, the townsfolk y-band idiom). Falls
        /// back to the raw point when nothing is hit.
        /// </summary>
        private static Vector3 SeatExitOnFloor(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.05f;
            return p;
        }

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

            // Preferred path: the camera rig component self-configures (it owns
            // the ceiling-aware height cap — see DungeonCameraRig.EffectiveOffset).
            if (_cameraRig != null)
            {
                _cameraRig.Bind(_hero);
                FlowTrace.Step("DungeonCam",
                    $"ConfigureCamera: bound via DungeonCameraRig (target='{_hero.name}').");
            }
            else if (_followCamera != null)
            {
                _followCamera.Follow = _hero;
                // LookAt is left unset: with no Aim component the rig keeps the
                // authored fixed pitch — the steady isometric tilt the spec asks
                // for (no orbit, no free-look — that is the village rig).
                _followCamera.LookAt = null;

                // Cap the height so the camera never floats far above the ~4u room
                // ceiling ("camera overtop / too high", owner 2026-07-16). Scale the
                // whole offset uniformly so the iso ANGLE holds and only distance tightens.
                Vector3 off = _cameraOffset;
                if (off.y > CameraMaxHeightAboveHero && off.y > 0.01f)
                {
                    float scale = CameraMaxHeightAboveHero / off.y;
                    off = new Vector3(off.x * scale, CameraMaxHeightAboveHero, off.z * scale);
                }

                // Seat the camera at the (capped) isometric offset behind + above
                // the hero, tilted down. CinemachineFollow eases it along.
                var camTransform = _followCamera.transform;
                camTransform.position = _hero.position + off;
                camTransform.rotation = Quaternion.Euler(_cameraPitch, 0f, 0f);

                FlowTrace.Step("DungeonCam",
                    $"ConfigureCamera: inline follow — authored={_cameraOffset} effective={off} " +
                    $"(cap={CameraMaxHeightAboveHero}) pitch={_cameraPitch} target='{_hero.name}'.");

                var follow = _followCamera.GetComponent<CinemachineFollow>();
                if (follow != null)
                {
                    follow.FollowOffset = off;
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
            return Object.FindAnyObjectByType<Camera>();
        }

        // ── Interactable + actor wiring ──────────────────────────────────────

        /// <summary>Hands the lantern its oil-stone refill points from the layout.</summary>
        private void ConfigureLantern()
        {
            if (_lantern == null || Layout == null) return;
            _lantern.Configure(this, Layout.oilStones, _hero);
        }

        /// <summary>
        /// Places + configures Bryn from the layout's <c>bryn</c> block, hands
        /// her the hero transform she watches for the proximity check, and the
        /// lore-fragment set her entrance line is sourced from (Week-6 checklist
        /// item 2).
        /// </summary>
        private void ConfigureBryn()
        {
            if (_bryn == null || Layout?.bryn == null) return;
            _bryn.Configure(Layout.bryn, _runtimeState);
            _bryn.SetHero(_hero);
            if (_loreFragments != null)
                _bryn.SetLoreFragments(_loreFragments);
        }

        /// <summary>
        /// WO-711 item 1 (owner order 2026-07-13, verbatim: "SKIN THE PILL IN
        /// HEALERS COTTAGE AS A NPC"): dresses the entrance placeholder pill
        /// (Bryn's capsule stand-in from the scene builder) with a real
        /// People-pack body + a Talk teaching the torch/light need. Runs right
        /// after <see cref="ConfigureBryn"/> so Bryn's authored placement and
        /// rotation are final. Purely additive runtime dress — a failure logs
        /// and leaves the pill visible; never breaks the dungeon.
        /// </summary>
        private void DressEntranceNpc()
        {
            Guard.Try("Dungeon", "dress entrance NPC (torch warden)",
                () => TorchWardenDresser.Dress(_bryn, Layout, _hero));
        }

        /// <summary>
        /// Wires the crafting system (Workstream C) — the ingredient pickups,
        /// the crafting pedestal and the crafting UI panel — from the canonical
        /// crafting-recipes.json. Each ingredient pickup under
        /// <see cref="_ingredientRoot"/> pairs with one
        /// <c>ingredientPlacements[]</c> entry in file order; the pedestal takes
        /// the file's <c>pedestal</c> block. A null crafting data set leaves the
        /// whole layer inert (the file may not be imported yet).
        /// </summary>
        private void ConfigureCrafting()
        {
            if (_craftingData == null) return;

            // ── Ingredient pickups — child[i] pairs with placement[i]. ───────
            if (_ingredientRoot != null && _craftingData.IngredientPlacements != null)
            {
                var pickups = _ingredientRoot.GetComponentsInChildren<IngredientPickup>(true);
                int total = _craftingData.IngredientPlacements.Count;
                int n = Mathf.Min(pickups.Length, total);
                // WO-749: MORE placements than scene pickups is NORMAL now (the 12-ingredient
                // floor scatter is authored in data, not baked into the scene). Only warn when
                // the SCENE has extra pickups no placement feeds (a real drift).
                if (pickups.Length > total)
                {
                    FlowTrace.Warn("Dungeon",
                        $"ConfigureCrafting: {pickups.Length} scene ingredient-pickups but only {total} " +
                        $"placements in crafting data — hydrating the first {n}, {pickups.Length - total} left inert.");
                }
                for (int i = 0; i < n; i++)
                    pickups[i].Configure(
                        _craftingData.IngredientPlacements[i], _dungeonInventory, _hero);

                // WO-749 gap 2/4 — floor scatter without a scene bake: every placement
                // beyond the scene-authored pickups is runtime-authored as a tinted mote.
                // Collected ids ride the per-run DungeonInventory and bank to the larder on
                // exit (DungeonLootGrant.DepositDungeonInventory).
                for (int i = n; i < total; i++)
                {
                    var placement = _craftingData.IngredientPlacements[i];
                    if (placement == null) continue;
                    Color tint = TintForIngredient(placement.IngredientId);
                    IngredientPickup.CreateRuntime(_ingredientRoot, placement, _dungeonInventory, _hero, tint);
                }
                if (total > n)
                    FlowTrace.Step("DungeonLoot",
                        $"ConfigureCrafting: runtime-authored {total - n} scatter mote(s) beyond {n} scene pickup(s).");
            }

            // ── Crafting pedestal + its UI panel. ────────────────────────────
            if (_craftingPedestal != null && _craftingData.Pedestal != null)
            {
                _craftingPedestal.Configure(
                    _craftingData.Pedestal, _craftingData, _dungeonInventory, _hero);

                // Subscribe the UI panel to the pedestal's open/close events —
                // keeps the pedestal a pure scene actor and the panel a pure view.
                if (_craftingPanel != null)
                    _craftingPanel.BindPedestal(_craftingPedestal);
            }
        }

        /// <summary>
        /// The tint (from crafting-recipes.json <c>tint</c> hex) for a scatter mote,
        /// falling back to loot-gold when the ingredient has no authored tint.
        /// </summary>
        private Color TintForIngredient(string ingredientId)
        {
            var ing = _craftingData?.FindIngredient(ingredientId);
            if (ing != null && !string.IsNullOrEmpty(ing.Tint)
                && ColorUtility.TryParseHtmlString("#" + ing.Tint, out Color c))
                return c;
            return new Color(0.95f, 0.82f, 0.35f);   // loot-gold fallback
        }

        /// <summary>
        /// Wires the treasure chests (WO-749 gap 1) — attaches a
        /// <see cref="DungeonChestInteract"/> to each layout chest so its rewardKey
        /// resolves to a larder loot grant on open. The scene builder places chest
        /// VISUALS named <c>Chest_{id}</c> with no behaviour; this attaches the
        /// interact at runtime (NO scene bake), falling back to a runtime marker at
        /// the layout coords when the visual is absent (pack not imported). Idempotent
        /// — a chest already carrying the component is skipped.
        /// </summary>
        private void HydrateChests()
        {
            if (Layout?.chests == null || Layout.chests.Length == 0 || _runtimeState == null) return;

            using var _flow = FlowTrace.Enter("DungeonLoot", "HydrateChests");

            // Bucket every "Chest_*" transform once so each layout chest finds its visual.
            var byName = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || string.IsNullOrEmpty(t.name)) continue;
                if (t.name.StartsWith("Chest_", System.StringComparison.Ordinal)
                    && !byName.ContainsKey(t.name))
                    byName[t.name] = t;
            }

            int wired = 0;
            foreach (var chest in Layout.chests)
            {
                if (chest == null || string.IsNullOrEmpty(chest.id)) continue;
                var localChest = chest;   // capture for the closure
                Guard.Try("DungeonLoot", $"wire chest '{localChest.id}'", () =>
                {
                    GameObject host;
                    if (byName.TryGetValue($"Chest_{localChest.id}", out var visual) && visual != null)
                    {
                        host = visual.gameObject;
                    }
                    else
                    {
                        host = new GameObject($"Chest_{localChest.id}");
                        host.transform.SetParent(transform, false);
                        host.transform.position = localChest.position.ToWorld();
                        FlowTrace.Warn("DungeonLoot",
                            $"chest '{localChest.id}' has no scene visual — runtime marker placed at " +
                            $"{host.transform.position} (KayKit chest mesh not imported?).");
                    }

                    if (host.GetComponent<DungeonChestInteract>() == null)
                    {
                        host.AddComponent<DungeonChestInteract>()
                            .Configure(localChest, _runtimeState, _hero);
                        wired++;
                    }
                });
            }
            FlowTrace.Step("DungeonLoot",
                $"HydrateChests: wired {wired} chest interactable(s) of {Layout.chests.Length}.");
        }

        /// <summary>
        /// Feeds the dungeon HUD (Workstream C) its Lantern reference so the oil
        /// meter polls the lantern's public API each frame. The HUD is a passive
        /// display — it never mutates the lantern.
        /// </summary>
        private void ConfigureDungeonHud()
        {
            if (_dungeonHud != null && _lantern != null)
                _dungeonHud.SetLantern(_lantern);
        }

        /// <summary>
        /// Hydrates the lore stones placed under <see cref="_loreStoneRoot"/> from
        /// the layout's <c>loreStones</c> array (Week-6 checklist item 3). The
        /// scene builder places one <see cref="LoreStone"/> per layout entry, in
        /// layout order, so child[i] pairs with <c>loreStones[i]</c>. Each stone
        /// is also handed the lore-fragment set for its canon reading text.
        /// </summary>
        private void HydrateLoreStones()
        {
            if (_loreStoneRoot == null || Layout?.loreStones == null) return;

            var stones = _loreStoneRoot.GetComponentsInChildren<LoreStone>(true);
            int total = Layout.loreStones.Length;
            int n = Mathf.Min(stones.Length, total);
            // VERIFY: the layout authored lore stones but the scene placed NONE — Mathf.Min
            // would silently hydrate 0 and report "success". Assert >0 so a stripped/empty
            // _loreStoneRoot self-reports instead of leaving the room bare of readable lore.
            if (total > 0 && stones.Length == 0)
            {
                FlowTrace.Warn("Dungeon",
                    $"HydrateLoreStones: layout authored {total} lore stone(s) but the scene placed " +
                    "ZERO under _loreStoneRoot — no lore will be readable. Check the scene builder.");
            }
            else if (stones.Length != total)
            {
                FlowTrace.Warn("Dungeon",
                    $"HydrateLoreStones: count mismatch — {stones.Length} in scene, {total} in layout. " +
                    $"Hydrating the first {n}.");
            }

            for (int i = 0; i < n; i++)
            {
                int idx = i;   // capture for the closure
                FlowTrace.Try("Dungeon", $"configure lore stone[{idx}]", () =>
                {
                    stones[idx].Configure(Layout.loreStones[idx], _runtimeState, _hero, total);
                    if (_loreFragments != null)
                        stones[idx].SetLoreFragments(_loreFragments);
                    // WO-770.4 (fixes D6): the missing subscriber. A tap (via the stone's
                    // MobileInteractButton) raises ReadRequested; the code-built Obsidian
                    // LoreReadingModal renders the canon fragment. Closes the triple gap
                    // (input + subscriber + view) so lore is actually readable in gameplay.
                    stones[idx].ReadRequested.RemoveListener(LoreReadingModal.Show);
                    stones[idx].ReadRequested.AddListener(LoreReadingModal.Show);
                });
            }
            FlowTrace.Step("Dungeon", $"HydrateLoreStones: hydrated {n} of {total} lore stone(s).");
        }

        /// <summary>
        /// Hydrates the checkpoint shrines under <see cref="_checkpointRoot"/>
        /// from the layout's <c>checkpoints</c> array (Week-6 checklist item 4).
        /// Child[i] pairs with <c>checkpoints[i]</c>.
        /// </summary>
        private void HydrateCheckpoints()
        {
            if (_checkpointRoot == null || Layout?.checkpoints == null) return;

            var shrines = _checkpointRoot.GetComponentsInChildren<Checkpoint>(true);
            int total = Layout.checkpoints.Length;
            int n = Mathf.Min(shrines.Length, total);
            // VERIFY: layout authored checkpoints but the scene placed NONE — without a
            // checkpoint there is no heal/respawn anchor. Assert >0 so a stripped root
            // self-reports rather than silently shipping a save-less run.
            if (total > 0 && shrines.Length == 0)
            {
                FlowTrace.Warn("Dungeon",
                    $"HydrateCheckpoints: layout authored {total} checkpoint(s) but the scene placed " +
                    "ZERO under _checkpointRoot — no heal/respawn anchor exists. Check the scene builder.");
            }
            else if (shrines.Length != total)
            {
                FlowTrace.Warn("Dungeon",
                    $"HydrateCheckpoints: count mismatch — {shrines.Length} in scene, {total} in layout. " +
                    $"Hydrating the first {n}.");
            }

            for (int i = 0; i < n; i++)
            {
                int idx = i;
                FlowTrace.Try("Dungeon", $"configure checkpoint[{idx}]",
                    () => shrines[idx].Configure(Layout.checkpoints[idx], _runtimeState, _hero));
            }
            FlowTrace.Step("Dungeon", $"HydrateCheckpoints: hydrated {n} of {total} checkpoint(s).");
        }

        /// <summary>
        /// Hydrates the encounter triggers under <see cref="_encounterRoot"/>
        /// (Week-6 checklist item 5). The scene builder places one trigger per
        /// scripted encounter in layout order, then the mini-boss trigger LAST.
        /// So child[0..k-1] take <c>scriptedEncounters[i]</c> and the trailing
        /// child takes <c>miniBoss</c>.
        /// </summary>
        private void HydrateEncounters()
        {
            _encounterTriggers.Clear();
            if (_encounterRoot == null || Layout == null) return;

            var triggers = _encounterRoot.GetComponentsInChildren<EncounterTrigger>(true);
            DungeonScriptedEncounter[] scripted =
                Layout.scriptedEncounters ?? System.Array.Empty<DungeonScriptedEncounter>();
            bool hasBoss = Layout.miniBoss != null;
            int expected = scripted.Length + (hasBoss ? 1 : 0);

            // VERIFY: the layout expects encounters (scripted and/or a boss) but the scene
            // placed NONE — the dungeon would have no fights at all (and an unbeatable boss
            // gate). Assert >0 so a stripped _encounterRoot self-reports rather than shipping
            // a combat-less / un-clearable run.
            if (expected > 0 && triggers.Length == 0)
            {
                FlowTrace.Warn("Dungeon",
                    $"HydrateEncounters: layout expects {expected} encounter(s) ({scripted.Length} scripted + " +
                    $"{(hasBoss ? 1 : 0)} boss) but the scene placed ZERO triggers under _encounterRoot — " +
                    "no fights will fire and the boss gate can never clear. Check the scene builder.");
            }
            else if (triggers.Length != expected)
            {
                FlowTrace.Warn("Dungeon",
                    $"HydrateEncounters: count mismatch — {triggers.Length} in scene, {expected} expected " +
                    $"({scripted.Length} scripted + {(hasBoss ? 1 : 0)} boss). Hydrating what aligns.");
            }

            int scriptedCount = Mathf.Min(scripted.Length, triggers.Length);
            for (int i = 0; i < scriptedCount; i++)
            {
                int idx = i;
                FlowTrace.Try("Dungeon", $"configure scripted encounter[{idx}]",
                    () => triggers[idx].ConfigureScripted(this, scripted[idx], _runtimeState, _hero));
                _encounterTriggers.Add(triggers[i]);
            }

            // The trailing trigger is the mini-boss (the Apprentice of the
            // Apothecary — design §4 Beat 6), configured via ConfigureBoss.
            if (hasBoss && triggers.Length > scripted.Length)
            {
                EncounterTrigger bossTrigger = triggers[triggers.Length - 1];
                FlowTrace.Try("Dungeon", "configure boss encounter",
                    () => bossTrigger.ConfigureBoss(this, Layout.miniBoss, 4f, _runtimeState, _hero));
                _encounterTriggers.Add(bossTrigger);
            }
            else if (hasBoss)
            {
                // A boss is authored but no trigger slot remained for it — the boss fight
                // can never fire, so the dungeon's exit gate can never unlock.
                FlowTrace.Warn("Dungeon",
                    "HydrateEncounters: layout authored a mini-boss but no trailing trigger was " +
                    "available to host it — the boss fight cannot fire and the exit gate stays locked.");
            }
            FlowTrace.Step("Dungeon",
                $"HydrateEncounters: hydrated {_encounterTriggers.Count} trigger(s) ({scriptedCount} scripted" +
                $"{(hasBoss && triggers.Length > scripted.Length ? " + 1 boss" : string.Empty)}).");
        }

        /// <summary>
        /// Settles the in-flight ATB encounter on dungeon re-entry — the dungeon
        /// side of the BUG-008 round-trip (Week-6 checklist item 6). Reads the
        /// pending encounter id off the run state and calls the matching
        /// trigger's <see cref="EncounterTrigger.ResumePendingEncounter"/>; a
        /// boss victory flags the boss defeated.
        /// </summary>
        private void ResolvePendingEncounter()
        {
            if (_runtimeState == null || !_runtimeState.HasPendingEncounter) return;

            // WO-770.3 (fixes D4): the Core-level result carrier now exists — read the settled
            // outcome that BattleController stamped onto SceneRouter.PendingBattle before the
            // hand-back. Only a stamped Victory counts as a win; a Defeat OR a missing carrier
            // (dev/direct-play with no battle) is treated as a loss. NOTE: the real-time
            // BattleArena path settles IN-SCENE (warps the hero back, no scene round-trip) and
            // never reaches here — this governs the legacy ATB (ff.dungeonrealtime OFF) round-trip.
            var battleCarrier = SceneRouter.PendingBattle;
            bool victory = battleCarrier != null && battleCarrier.LastOutcome == BattleResultKind.Victory;

            // Capture the boss flag BEFORE the resume/exit clears the pending handoff below.
            bool wasBoss = _runtimeState.PendingEncounterIsBoss;

            // WO-770.3 LOCKED defeat behavior: a lost fight ENDS the run and returns to the
            // Village. Clear the encounter handoff + combat lock (so the run is not wedged and the
            // encounter is NOT re-armed for a free retry), grant NO loot, and do NOT mark the boss
            // defeated (the boss-gated back-door stays sealed). Then ExitToVillage — no resume-in-place.
            if (!victory)
            {
                FlowTrace.Warn("Dungeon",
                    $"ResolvePendingEncounter: DEFEAT on '{_runtimeState.PendingEncounterId}' " +
                    $"(carrier={(battleCarrier == null ? "none" : battleCarrier.LastOutcome.ToString())}) — " +
                    "ending the run + returning to Village (no loot, boss NOT credited, encounter not re-armed).");
                _runtimeState.ResumeAfterEncounter(false); // clear pending handoff + combat lock (records the loss)
                ExitToVillage().Forget();
                return;
            }

            // WO-749 gap 3: the ATB/cottage encounter path credited NO loot (only the
            // composed-chain live-Enemy path fed the larder). Grant a per-encounter
            // dungeon roll into the larder on a victory resume.
            DungeonLootGrant.GrantEncounter(wasBoss);

            string pendingId = _runtimeState.PendingEncounterId;
            bool settled = false;
            foreach (EncounterTrigger trigger in _encounterTriggers)
            {
                if (trigger == null) continue;
                if (trigger.ResumePendingEncounter(victory))
                {
                    settled = true;
                    break;
                }
            }

            // No trigger owned the pending id (a layout/scene drift) — clear the
            // handoff anyway so the run is not wedged in a permanent combat lock.
            if (!settled)
            {
                FlowTrace.Warn("Dungeon",
                    $"ResolvePendingEncounter: no encounter trigger matched pending id " +
                    $"'{pendingId}' on resume — clearing the handoff to unwedge the run (no combat-lock).");
                _runtimeState.ResumeAfterEncounter(victory);
            }
        }

        // ── Traversal ports (WO-711 items 3-4 — owner rulings, live walk) ────

        /// <summary>
        /// Dresses every door and staircase with a simple interact-to-port pair
        /// (WO-711: "anywhere with a door, use Door action — nav-link PORT from
        /// one side to the other"; "same with steps going up"; "we can cook
        /// later but for now simple"). Runtime-authored — never a scene edit.
        ///
        /// DOORS are keyed on the layout's <c>kind=="doorway"</c> wall segments
        /// (each carries <c>leadsTo</c>): one <see cref="DungeonPortLink"/> per
        /// side at the doorway midpoint, offset ~1.5u into each room, targeting
        /// the mate point across the wall. Illusory walls are SKIPPED (the
        /// secret walk-through is the design). STAIRS have no layout data —
        /// they are keyed on DungeonSceneBuilder's authored vertical connectors
        /// (BuildVerticalConnectors: stairs_long/-wood/-narrow + the cellar-entry
        /// placeholder), so the three pairs are table-driven for the Healer's
        /// Cottage only; another dungeon id logs a Warn instead of guessing.
        /// </summary>
        private void DressTraversalLinks()
        {
            if (Layout == null || _hero == null) return;

            using var _flow = FlowTrace.Enter("Dungeon", "DressTraversalLinks (WO-711)");

            var root = new GameObject("TraversalLinks");
            root.transform.SetParent(transform, false);

            // Room -> floor Y. The layout JSON carries NO level field (bounds are
            // XZ-only); the level assignment is the scene builder's (ground Y=0,
            // upper Y=6, underground Y=-6 — DungeonSceneBuilder.cs YUpper/YUnder
            // + each RoomDef.Level). Unknown rooms fall back to any floor-level
            // layout object in that room (checkpoint/encounter/chest y), else 0.
            var roomY = new System.Collections.Generic.Dictionary<string, float>
            {
                { "garden-approach", 0f }, { "entrance-room", 0f }, { "main-room", 0f },
                { "kitchen", 0f }, { "pantry-alcove", 0f }, { "workshop", 0f },
                { "loft-bedroom", 6f }, { "loft-study", 6f },
                { "root-cellar", -6f }, { "storage", -6f },
                { "crypt-sublevel", -6f }, { "hidden-vault", -6f },
            };

            float LevelYFor(DungeonRoom room)
            {
                if (room == null) return 0f;
                if (roomY.TryGetValue(room.id, out float y)) return y;
                foreach (var c in Layout.checkpoints)
                    if (c != null && c.roomId == room.id) return c.position.y;
                foreach (var e in Layout.scriptedEncounters)
                    if (e != null && e.roomId == room.id) return e.triggerPosition.y;
                foreach (var ch in Layout.chests)
                    if (ch != null && ch.roomId == room.id) return ch.position.y;
                FlowTrace.Warn("Dungeon",
                    $"DressTraversalLinks: no level Y known for room '{room.id}' — assuming 0.");
                return 0f;
            }

            // Seat a point on the floor band so a port never lands inside
            // geometry (the townsfolk y-band idiom): short ray down from just
            // above the authored point onto the room's floor collider.
            Vector3 SeatOnFloor(Vector3 p)
            {
                if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down,
                        out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore))
                    return hit.point + Vector3.up * 0.05f;
                return p;
            }

            float YawTo(Vector3 from, Vector3 to)
            {
                Vector3 d = to - from; d.y = 0f;
                if (d.sqrMagnitude < 0.0001f) return 0f;
                return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }

            int built = 0;

            void BuildPair(string pairName, string prompt,
                Vector3 posA, string labelA, Vector3 posB, string labelB)
            {
                Guard.Try("Dungeon", $"traversal pair '{pairName}'", () =>
                {
                    Vector3 a = SeatOnFloor(posA);
                    Vector3 b = SeatOnFloor(posB);

                    var goA = new GameObject($"PortLink_{pairName}_A");
                    goA.transform.SetParent(root.transform, false);
                    goA.transform.position = a;
                    goA.AddComponent<DungeonPortLink>().Configure(
                        prompt, b, YawTo(a, b), _hero, _heroController, labelA, labelB);

                    var goB = new GameObject($"PortLink_{pairName}_B");
                    goB.transform.SetParent(root.transform, false);
                    goB.transform.position = b;
                    goB.AddComponent<DungeonPortLink>().Configure(
                        prompt, a, YawTo(b, a), _hero, _heroController, labelB, labelA);

                    built++;
                    FlowTrace.Step("Dungeon",
                        $"DressTraversalLinks: pair '{pairName}' ('{prompt}') " +
                        $"{labelA}@{a} <-> {labelB}@{b}.");
                });
            }

            // ── DOORS — one pair per doorway wall segment (deduped: the same
            //    doorway is listed in BOTH rooms' wall arrays). ────────────────
            //
            // ALIGN FIX (owner F8 "open door not aligned with door location"):
            // the visible door GAP is cut by DungeonSceneBuilder from its OWN
            // hardcoded RoomDef door offsets, quantized to 4u wall segments —
            // which DRIFT from this layout JSON's doorway coords (garden<->
            // entrance: mesh gap ~z=2 vs JSON mid z=6 => a ~4u sideways offset,
            // so the 'Open Door' prompt fired well off the door mouth). We anchor
            // each side's port at that ROOM'S OWN built 'wall_doorway' mesh
            // (its transform IS the gap centre) instead of the JSON midpoint, so
            // the prompt sits where the Keeper actually sees the door. Runtime
            // only — no scene edit. Falls back to the JSON mid if a mesh is
            // missing (e.g. the pack was not imported -> placeholder box).
            var doorMeshByRoom = CollectDoorMeshes();

            var done = new System.Collections.Generic.HashSet<string>();
            foreach (var room in Layout.rooms)
            {
                if (room?.walls == null) continue;
                foreach (var wall in room.walls)
                {
                    if (wall == null || !wall.IsDoorway) continue;
                    if (string.IsNullOrEmpty(wall.leadsTo)) continue;

                    string key = string.CompareOrdinal(room.id, wall.leadsTo) < 0
                        ? room.id + "|" + wall.leadsTo
                        : wall.leadsTo + "|" + room.id;
                    if (!done.Add(key)) continue;

                    DungeonRoom other = Layout.FindRoom(wall.leadsTo);
                    if (other == null)
                    {
                        // e.g. the workshop doorway leadsTo "exit" — not a room;
                        // the dungeon exit is its own flow (ExitToVillage).
                        FlowTrace.Warn("Dungeon",
                            $"DressTraversalLinks: doorway in '{room.id}' leadsTo " +
                            $"'{wall.leadsTo}' which is not a room — no port authored (un-keyable).");
                        continue;
                    }

                    // Doorway midpoint + the wall's perpendicular, signed into
                    // each room (toward that room's footprint centre).
                    Vector3 s = wall.start.ToWorld(), e = wall.end.ToWorld();
                    Vector3 mid = (s + e) * 0.5f;
                    Vector3 dir = (e - s).normalized;
                    Vector3 n = new Vector3(dir.z, 0f, -dir.x);
                    Vector3 intoA = Vector3.Dot(n, room.bounds.Center - mid) >= 0f ? n : -n;

                    // Seat each side at that room's OWN door mesh (falls back to
                    // the JSON midpoint when no built doorway mesh is found).
                    Vector3 gapA = NearestDoorMeshXZ(room.id, mid, doorMeshByRoom, out bool haveA);
                    Vector3 gapB = NearestDoorMeshXZ(other.id, mid, doorMeshByRoom, out bool haveB);

                    // PROVE the offset (section 12): mesh gap vs the old JSON anchor.
                    FlowTrace.Step("Dungeon",
                        $"DoorAlign '{room.id}<->{other.id}': jsonMid={mid:F2} " +
                        $"gapA[{(haveA ? "mesh" : "fallback")}]={gapA:F2} " +
                        $"gapB[{(haveB ? "mesh" : "fallback")}]={gapB:F2} " +
                        $"deltaA={(gapA - mid).magnitude:F2} deltaB={(gapB - mid).magnitude:F2}.");

                    float yA = LevelYFor(room), yB = LevelYFor(other);
                    Vector3 posA = gapA + intoA * 1.5f + Vector3.up * yA;
                    Vector3 posB = gapB - intoA * 1.5f + Vector3.up * yB;

                    BuildPair($"Door_{room.id}__{other.id}", "Open Door",
                        posA, room.id, posB, other.id);
                }
            }

            // ── STAIRS — the builder's three authored vertical connectors
            //    (no layout data; Healer's Cottage table only). ────────────────
            if (Layout.id == "healers-cottage")
            {
                // stairs_long @ (-2,-6,-2): Main Room (ground) <-> Root Cellar;
                // the cellar-entry affordance is dressed at root-cellar (-18,·,0).
                BuildPair("Stairs_main-room__root-cellar", "Climb",
                    new Vector3(-2f, 0f, -2f), "main-room",
                    new Vector3(-18f, -6f, 0f), "root-cellar");

                // stairs_wood @ (0,0,-4): Main Room (ground) <-> Loft Bedroom (Y6).
                BuildPair("Stairs_main-room__loft-bedroom", "Climb",
                    new Vector3(0f, 0f, -4f), "main-room",
                    new Vector3(2f, 6f, -4f), "loft-bedroom");

                // stairs_narrow @ (-12,-6,5): Entrance Room trapdoor <-> Root Cellar.
                BuildPair("Stairs_entrance-room__root-cellar", "Climb",
                    new Vector3(-12f, 0f, 5f), "entrance-room",
                    new Vector3(-13f, -6f, 5f), "root-cellar");
            }
            else
            {
                FlowTrace.Warn("Dungeon",
                    $"DressTraversalLinks: no stair table for dungeon '{Layout.id}' — " +
                    "stairs get no ports until authored (doors still dressed from the layout).");
            }

            FlowTrace.Step("Dungeon",
                $"DressTraversalLinks: {built} traversal pair(s) authored ({built * 2} port links).");
        }

        /// <summary>
        /// Buckets every built <c>wall_doorway</c> mesh in the scene by the room
        /// it lives under (walking parents to the enclosing <c>Room_&lt;id&gt;</c>
        /// node). Used to seat each door port at the visible gap rather than the
        /// JSON midpoint (the two authorings drift — see DressTraversalLinks).
        /// </summary>
        private System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Transform>> CollectDoorMeshes()
        {
            var byRoom = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Transform>>();
            Guard.Try("Dungeon", "collect door meshes", () =>
            {
                foreach (var t in Object.FindObjectsByType<Transform>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (t == null) continue;
                    string n = t.name;
                    if (string.IsNullOrEmpty(n)) continue;
                    // The KayKit doorway piece instantiates as "wall_doorway";
                    // exclude illusory walls and our own PortLink markers.
                    if (n.IndexOf("doorway", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (n.StartsWith("[ILLUSORY]", System.StringComparison.Ordinal)) continue;
                    if (n.StartsWith("PortLink", System.StringComparison.Ordinal)) continue;

                    string rid = OwningRoomId(t);
                    if (rid == null) continue;
                    if (!byRoom.TryGetValue(rid, out var list))
                    {
                        list = new System.Collections.Generic.List<Transform>();
                        byRoom[rid] = list;
                    }
                    list.Add(t);
                }
            });
            return byRoom;
        }

        /// <summary>
        /// The XZ (floor-plane, y=0) of the door mesh in <paramref name="roomId"/>
        /// nearest <paramref name="near"/>; <paramref name="found"/> is false and
        /// the JSON point is returned when the room has no built doorway mesh.
        /// </summary>
        private Vector3 NearestDoorMeshXZ(string roomId, Vector3 near,
            System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Transform>> byRoom,
            out bool found)
        {
            found = false;
            Vector3 result = new Vector3(near.x, 0f, near.z);
            if (byRoom != null && byRoom.TryGetValue(roomId, out var list) && list != null)
            {
                float best = float.MaxValue;
                foreach (var t in list)
                {
                    if (t == null) continue;
                    Vector3 p = t.position;
                    float dx = p.x - near.x, dz = p.z - near.z;
                    float d = dx * dx + dz * dz;
                    if (d < best)
                    {
                        best = d;
                        result = new Vector3(p.x, 0f, p.z);
                        found = true;
                    }
                }
            }
            return result;
        }

        /// <summary>Walks up from <paramref name="t"/> to the enclosing
        /// <c>Room_&lt;id&gt;</c> node and returns its id, or null if none.</summary>
        private static string OwningRoomId(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                if (p.name != null && p.name.StartsWith("Room_", System.StringComparison.Ordinal))
                    return p.name.Substring(5);
            }
            return null;
        }

        /// <summary>
        /// Hides leftover WHITE placeholder primitive boxes — DungeonSceneBuilder's
        /// MakePlaceholderCube fallback for a KayKit prop whose mesh failed to load
        /// (owner F8: "white placeholder cube on the dungeon floor"). Only NEAR-WHITE
        /// primitive cubes named "[PLACEHOLDER] ..." are swept; the deliberately-
        /// tinted stand-ins (hearth/rug/water) render in colour and are LEFT ALONE.
        /// Runtime-only (no scene edit); idempotent (a re-imported mesh leaves no box).
        /// </summary>
        private void SweepPlaceholderCubes()
        {
            using var _flow = FlowTrace.Enter("Dungeon", "SweepPlaceholderCubes");
            int hidden = 0;
            Guard.Try("Dungeon", "sweep placeholder cubes", () =>
            {
                foreach (var t in Object.FindObjectsByType<Transform>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (t == null || !t.gameObject.activeSelf) continue;
                    if (!t.name.StartsWith("[PLACEHOLDER]", System.StringComparison.Ordinal)) continue;

                    // Only raw primitive boxes (built-in "Cube" mesh) — never a real FBX.
                    var mf = t.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null || mf.sharedMesh.name != "Cube") continue;

                    // Keep the tinted stand-ins; hide only the untinted white/magenta boxes.
                    var r = t.GetComponent<Renderer>();
                    Color c = Color.white;
                    if (r != null && r.sharedMaterial != null)
                    {
                        var m = r.sharedMaterial;
                        if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor");
                        else if (m.HasProperty("_Color")) c = m.color;
                    }
                    bool nearWhite = c.r > 0.85f && c.g > 0.85f && c.b > 0.85f;
                    if (!nearWhite) continue;

                    FlowTrace.Step("Dungeon",
                        $"SweepPlaceholderCubes: hiding white placeholder '{t.name}' at " +
                        $"{t.position:F2} (missing KayKit mesh -> default-material box).");
                    t.gameObject.SetActive(false);
                    hidden++;
                }
            });
            FlowTrace.Step("Dungeon",
                $"SweepPlaceholderCubes: {hidden} white placeholder box(es) hidden.");
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
                FlowTrace.Warn("Dungeon",
                    "StartAmbientAudio: ambient clip 'echoes-beneath-elarion' is not assigned — " +
                    "the MP3 is not yet imported under Assets/Audio/. Dungeon plays silently; " +
                    "wire the clip when the file lands.");
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
