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

            InstallOilHud();

            // WO-1001 slice 6: darkness ambush director (higher odds when oil critical).
            ComposedKeyBag.Clear();
            var ambush = gameObject.GetComponent<ComposedAmbushDirector>();
            if (ambush == null) ambush = gameObject.AddComponent<ComposedAmbushDirector>();
            ambush.Configure(_lantern, heroGo.transform, _state, tier: 1);
            FlowTrace.Step(Sys, "ComposedAmbushDirector armed (slice 6 darkness ambush)");

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
