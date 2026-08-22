// =============================================================================
// ComposedDungeonHost -- the live owner of one composed (Pipeline A) dungeon run.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Dungeons   Namespace: DeNelle.Dungeons
//
// ComposedDungeonBootstrap INSTALLS; this component OWNS. It holds the run state
// for the scene's lifetime so the two things that need it after load -- the lantern
// HUD and the exit's payout -- have somewhere to read it from. Before WO-1112 the
// bootstrap created a DungeonRuntimeState in a local variable and let it fall out of
// scope, which is why the composed exit had nothing to pay a run out from.
//
// ⚠ WHY THE HERO PILLARS ARM ONE FRAME LATE, AND WHY THAT IS THE POINT (WO-1112):
// SceneRouter.GoDungeonScene now CARRIES the town hero into a composed dungeon,
// because the baked Keeper has no HeroAbilities (Q/W/E/R were dead, silently). That
// means TWO Player-tagged heroes exist on the frame the scene loads -- the carried
// one and the baker's own -- and HeroControlEnsurer.DedupeHeroes destroys the baked
// one in the carried hero's favour. Unity's Destroy is deferred to END OF FRAME, so
// a GameObject.FindGameObjectWithTag("Player") on the load frame can hand back the
// DOOMED rig. Arming the lantern on that object would attach the light, the HUD feed
// and the ambush director to something that ceases to exist moments later -- and it
// would do so SILENTLY, which is this whole pipeline's signature failure. Waiting a
// frame costs nothing and makes the hero resolution unambiguous. The run state and
// StartRun still happen immediately; only the hero-dependent half waits.
//
// Instrumented per CLAUDE.md sec.12 -- [Flow:ComposedDungeon] on every step and
// branch, including the ones that used to fail with no trace at all.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Dungeons.RoomForge;
using DeNelle.Village;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Dungeons
{
    /// <summary>Runtime owner of a composed dungeon's run state, lantern, HUD and ambush.</summary>
    [DisallowMultipleComponent]
    public sealed class ComposedDungeonHost : MonoBehaviour
    {
        private const string Sys = "ComposedDungeon";

        /// <summary>
        /// The host for the composed dungeon currently loaded, or null. Single-scene by
        /// construction (one DungeonCompose_* root per scene, and the bootstrap is idempotent).
        /// </summary>
        public static ComposedDungeonHost Current { get; private set; }

        private DungeonRuntimeState _state;
        private Transform _composeRoot;
        private Lantern _lantern;
        private DungeonHudController _hud;
        private DungeonComposeLayout _layout;
        private OutpostEnemyGroupSpawner _bossSpawner;
        private readonly List<BreakableContainer> _bossHoard = new List<BreakableContainer>();

        /// <summary>The live run record for this dungeon. Null only if StartRun never ran.</summary>
        public DungeonRuntimeState RunState => _state;

        /// <summary>The Keeper's lantern once armed (null during the first frame).</summary>
        public Lantern ActiveLantern => _lantern;

        /// <summary>Installer entry point — see the header for why the hero half is deferred.</summary>
        public void Install(Transform composeRoot, DungeonRuntimeState state)
        {
            _composeRoot = composeRoot;
            _state = state;
            Current = this;
            StartCoroutine(ArmHeroPillarsNextFrame());
        }

        private void OnDestroy()
        {
            if (_bossSpawner != null) _bossSpawner.BossCleared -= HandleBossCleared;
            if (Current == this) Current = null;
        }

        private IEnumerator ArmHeroPillarsNextFrame()
        {
            // One frame: long enough for HeroControlEnsurer's sceneLoaded pass to run and for
            // the destroy of the losing duplicate hero to actually resolve.
            yield return null;
            Guard.Try(Sys, "arm composed hero pillars", ArmHeroPillars);
        }

        private void ArmHeroPillars()
        {
            using var _scope = FlowTrace.Enter(Sys, $"arm hero pillars on '{gameObject.scene.name}'");

            var heroGo = GameObject.FindGameObjectWithTag("Player");
            if (heroGo == null)
            {
                FlowTrace.Warn(Sys, "no Player-tagged hero one frame after load - lantern, oil meter and ambush are NOT armed for this run.");
                return;
            }
            FlowTrace.Step(Sys, $"hero resolved as '{heroGo.name}' (scene='{heroGo.scene.name}') - carried hero wins the dedupe when GoDungeonScene armed the WO-1112 carry.");
            DungeonCandleVfxInstaller.Rebind(gameObject.scene, heroGo.transform);

            // Collect baked oil stones (planar refill, same contract as cottage).
            var stones = CollectOilStones();

            // Ensure a lantern light follows the Keeper.
            _lantern = heroGo.GetComponentInChildren<Lantern>(true);
            if (_lantern == null)
            {
                var lightGo = new GameObject("Lantern");
                lightGo.transform.SetParent(heroGo.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                lightGo.AddComponent<Light>();
                _lantern = lightGo.AddComponent<Lantern>();
                FlowTrace.Step(Sys, "created Lantern under Player (composed bake had none)");
            }

            _lantern.ConfigureStandalone(stones, heroGo.transform);
            FlowTrace.Step(Sys,
                $"lantern armed standalone: stones={stones.Count} hero='{heroGo.name}' " +
                $"burn={_lantern.EstimatedSecondsRemaining:F0}s at full oil (WO-1112 tripled via dungeon-balance.json)");

            // Every authored cache is also a one-use emergency still. It spends real persisted
            // crafting materials for a partial refill; the free cache itself is independently
            // one-use in Lantern, so neither path can become an infinite fountain.
            if (_composeRoot != null)
            {
                foreach (var marker in _composeRoot.GetComponentsInChildren<ComposedOilStone>(true))
                {
                    if (marker == null) continue;
                    var still = marker.GetComponent<ComposedOilStill>();
                    if (still == null) still = marker.gameObject.AddComponent<ComposedOilStill>();
                    still.Configure(heroGo.transform, _lantern);
                }
            }

            InstallOilHud();

            _layout = LoadLayout();

            // WO-1001 slice 6: darkness ambush director (higher odds when oil critical).
            ComposedKeyBag.Clear();
            var ambush = gameObject.GetComponent<ComposedAmbushDirector>();
            if (ambush == null) ambush = gameObject.AddComponent<ComposedAmbushDirector>();
            int tier = _layout != null ? Mathf.Max(1, _layout.tier) : 1;
            ambush.Configure(_lantern, heroGo.transform, _state, tier);
            FlowTrace.Step(Sys, $"ComposedAmbushDirector armed (slice 6 darkness ambush, tier={tier})");

            InstallBossContract();

            // WO-1001 1b/7: count what the bake actually left in the scene. These are the pillars
            // whose bake-time Configure used to be discarded by SaveScene, so a zero here on a
            // dungeon that authored them is the exact signature of that class of defect returning.
            if (_composeRoot != null)
            {
                int ports = _composeRoot.GetComponentsInChildren<DungeonPortLink>(true).Length;
                int locks = _composeRoot.GetComponentsInChildren<ComposedLockedPort>(true).Length;
                int keys = _composeRoot.GetComponentsInChildren<ComposedKeyPickup>(true).Length;
                int traps = _composeRoot.GetComponentsInChildren<ComposedTrapHazard>(true).Length;
                FlowTrace.Step(Sys,
                    $"pillars present in '{gameObject.scene.name}': stairPorts={ports} lockedPorts={locks} " +
                    $"keys={keys} traps={traps} oilStones={stones.Count}");
            }
        }

        private DungeonComposeLayout LoadLayout()
        {
            if (_composeRoot == null) return null;
            const string prefix = "DungeonCompose_";
            string id = _composeRoot.name.StartsWith(prefix, System.StringComparison.Ordinal)
                ? _composeRoot.name.Substring(prefix.Length)
                : gameObject.scene.name;
            var text = Resources.Load<TextAsset>("Data/Canonical/dungeon-layouts/" + id);
            if (text == null)
            {
                FlowTrace.Warn(Sys, $"difficulty/boss contract: layout '{id}' not found");
                return null;
            }
            return Guard.Try(Sys, $"parse layout '{id}' for difficulty/boss contract",
                () => JsonConvert.DeserializeObject<DungeonComposeLayout>(text.text), null);
        }

        private void InstallBossContract()
        {
            if (_composeRoot == null || _state == null || _layout == null) return;

            var spawners = FindObjectsByType<OutpostEnemyGroupSpawner>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < spawners.Length; i++)
            {
                var candidate = spawners[i];
                if (candidate != null && candidate.gameObject.scene == gameObject.scene && candidate.IsBossGroup)
                {
                    _bossSpawner = candidate;
                    break;
                }
            }
            if (_bossSpawner == null)
            {
                FlowTrace.Step(Sys, "no authored boss spawner - boss gate contract not needed");
                return;
            }

            _bossSpawner.BossCleared += HandleBossCleared;
            if (_bossSpawner.IsBossCleared) HandleBossCleared(_bossSpawner);

            Transform bossRoom = _composeRoot.Find(_bossSpawner.RoomId);
            if (bossRoom == null)
            {
                FlowTrace.Warn(Sys, $"boss room '{_bossSpawner.RoomId}' not found - exits cannot be gated by room");
                return;
            }
            Bounds roomBounds = DungeonRoomBounds.Compute(bossRoom.gameObject);
            var containers = FindObjectsByType<BreakableContainer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < containers.Length; i++)
            {
                var chest = containers[i];
                if (chest == null || chest.gameObject.scene != gameObject.scene) continue;
                if (DungeonRoomBounds.SqrDistanceXZ(roomBounds, chest.transform.position) > 0.25f) continue;
                _bossHoard.Add(chest);
                chest.gameObject.SetActive(_state.BossDefeated);
            }
            var exits = FindObjectsByType<DungeonExitInteractable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int gated = 0;
            for (int i = 0; i < exits.Length; i++)
            {
                var exit = exits[i];
                if (exit == null || exit.gameObject.scene != gameObject.scene) continue;
                if (DungeonRoomBounds.SqrDistanceXZ(roomBounds, exit.transform.position) > 0.25f) continue;
                exit.SetBossGate(_state);
                gated++;
            }
            FlowTrace.Step(Sys, $"boss contract armed: room='{_bossSpawner.RoomId}' gatedExits={gated} " +
                $"sealedHoard={_bossHoard.Count}");
        }

        private void HandleBossCleared(OutpostEnemyGroupSpawner source)
        {
            if (_state == null || _state.BossDefeated) return;
            _state.MarkBossDefeated();
            for (int i = 0; i < _bossHoard.Count; i++)
                if (_bossHoard[i] != null) _bossHoard[i].gameObject.SetActive(true);
            FlowTrace.Step(Sys, $"boss clear recorded once for '{gameObject.scene.name}' - boss-room exits unlocked");
        }

        private List<DungeonOilStone> CollectOilStones()
        {
            var stones = new List<DungeonOilStone>();
            if (_composeRoot == null) return stones;
            var markers = _composeRoot.GetComponentsInChildren<ComposedOilStone>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m == null) continue;
                Vector3 p = m.transform.position;
                stones.Add(new DungeonOilStone
                {
                    id = m.Id,
                    roomId = "",
                    position = new DungeonPoint { x = p.x, y = p.y, z = p.z },
                    radius = m.Radius,
                });
            }
            return stones;
        }

        /// <summary>
        /// WO-1112 (A7): installs the SAME oil meter the cottage pipeline uses.
        /// <para>
        /// THE DEFECT: DungeonHudController.SetLantern had exactly ONE production caller —
        /// DungeonController — and DungeonController is in no dg_* scene, while DungeonBaker
        /// never places the HUD. So the composed player watched an invisible flask drain to
        /// empty and then played the rest of the run in the dark, with the ambush multiplier
        /// on, with no meter and no idea why.
        /// </para>
        /// <para>
        /// ⚠ REUSED, NOT REBUILT, AND EXPLICITLY NOT A UXML PATH. CLAUDE.md sec.8: UXML does not
        /// render in player builds. DungeonHudController was rewritten code-first (WO-1005) for
        /// exactly that reason and builds its own uGUI canvas in OnEnable; it needs nothing from
        /// a scene but a live GameObject and a lantern pushed in through the existing SetLantern
        /// seam. Its serialized _document field stays null here, which its own HideLegacyUxmlHud
        /// treats as the expected code-built case.
        /// </para>
        /// </summary>
        private void InstallOilHud()
        {
            _hud = GetComponentInChildren<DungeonHudController>(true);
            if (_hud == null)
            {
                var hudGo = new GameObject("ComposedDungeonHud");
                hudGo.transform.SetParent(transform, false);
                _hud = hudGo.AddComponent<DungeonHudController>();
                FlowTrace.Step(Sys, "DungeonHudController installed on the composed host (code-built uGUI oil meter; composed bake places none)");
            }
            _hud.SetLantern(_lantern);
            FlowTrace.Step(Sys,
                $"oil meter bound to lantern '{(_lantern != null ? _lantern.name : "<null>")}' - " +
                "the composed run finally has a readable flask (WO-1112 A7).");
        }

        /// <summary>Ends the run record, if one is still active. Idempotent.</summary>
        public void EndRun()
        {
            if (_state != null && _state.RunActive)
            {
                _state.EndRun();
                FlowTrace.Step(Sys, "run ended (composed exit).");
            }
        }
    }
}
