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

            // No battle pending — a real teardown. End the run so a stale run
            // never leaks into the next scene.
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
            HydrateLoreStones();
            HydrateCheckpoints();
            HydrateEncounters();
            ConfigureCrafting();
            ConfigureDungeonHud();
            StartAmbientAudio();

            // Settle any in-flight ATB encounter — the dungeon module's side of
            // the BUG-008 round-trip. The matching EncounterTrigger marks itself
            // fired + (on a boss victory) flags the boss defeated.
            if (resuming)
                ResolvePendingEncounter();

            DungeonRoom currentRoom = Layout.RoomAt(spawnPos);
            _lastRoomId = currentRoom?.id ?? entryRoomId;
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
            // The crafting inventory is per-run — clear it on a real exit so a
            // stale run's ingredients never leak into the next dungeon run.
            if (_dungeonInventory != null)
                _dungeonInventory.Clear();
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
                if (pickups.Length != total)
                {
                    Debug.LogWarning(
                        $"[DungeonController] Ingredient-pickup count mismatch — {pickups.Length} " +
                        $"in scene, {total} in crafting data. Hydrating the first {n}.");
                }
                for (int i = 0; i < n; i++)
                    pickups[i].Configure(
                        _craftingData.IngredientPlacements[i], _dungeonInventory, _hero);
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
            if (stones.Length != total)
            {
                Debug.LogWarning(
                    $"[DungeonController] Lore-stone count mismatch — {stones.Length} in scene, " +
                    $"{total} in layout. Hydrating the first {n}.");
            }

            for (int i = 0; i < n; i++)
            {
                stones[i].Configure(Layout.loreStones[i], _runtimeState, _hero, total);
                if (_loreFragments != null)
                    stones[i].SetLoreFragments(_loreFragments);
            }
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
            int n = Mathf.Min(shrines.Length, Layout.checkpoints.Length);
            if (shrines.Length != Layout.checkpoints.Length)
            {
                Debug.LogWarning(
                    $"[DungeonController] Checkpoint count mismatch — {shrines.Length} in scene, " +
                    $"{Layout.checkpoints.Length} in layout. Hydrating the first {n}.");
            }

            for (int i = 0; i < n; i++)
                shrines[i].Configure(Layout.checkpoints[i], _runtimeState, _hero);
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

            if (triggers.Length != expected)
            {
                Debug.LogWarning(
                    $"[DungeonController] Encounter-trigger count mismatch — {triggers.Length} " +
                    $"in scene, {expected} expected ({scripted.Length} scripted + " +
                    $"{(hasBoss ? 1 : 0)} boss). Hydrating what aligns.");
            }

            int scriptedCount = Mathf.Min(scripted.Length, triggers.Length);
            for (int i = 0; i < scriptedCount; i++)
            {
                triggers[i].ConfigureScripted(this, scripted[i], _runtimeState, _hero);
                _encounterTriggers.Add(triggers[i]);
            }

            // The trailing trigger is the mini-boss (the Apprentice of the
            // Apothecary — design §4 Beat 6), configured via ConfigureBoss.
            if (hasBoss && triggers.Length > scripted.Length)
            {
                EncounterTrigger bossTrigger = triggers[triggers.Length - 1];
                bossTrigger.ConfigureBoss(this, Layout.miniBoss, 4f, _runtimeState, _hero);
                _encounterTriggers.Add(bossTrigger);
            }
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

            // v1's ATB engine fully restores HP/MP after every fight, so a clean
            // resume is treated as a victory unless a battle-result carrier says
            // otherwise. The ATB outcome lands on the battle runtime state; the
            // dungeon module references DeNelle.Core only, so until a Core-level
            // result carrier exists the resume assumes victory (the cleared
            // encounter never re-fires either way — ResumePendingEncounter
            // re-arms _hasFired).
            bool victory = true;

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
                Debug.LogWarning(
                    $"[DungeonController] No encounter trigger matched pending id " +
                    $"'{pendingId}' on resume — clearing the handoff to unwedge the run.");
                _runtimeState.ResumeAfterEncounter(victory);
            }
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
