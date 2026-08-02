// =============================================================================
// HudActionBarModel — the COMMON applicability model for the bottom action bar
// (WO-835; owner architecture law 2026-08-02, HP B2B: "the applicability logic is
// managed in COMMON, not in the presentation class").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.HudModel
//
// THE PROBLEM THIS SOLVES: HudKitController.Update() held per-button gate reads
// (Talk dim via PostureSignals, Raids dim via RaidEntryGate.ArmyStatus, Map hide
// via GameStateService.Onboarded, Quests<->Upgrade relabel via HudBuildingFocus)
// and the bar was a FIXED /6 row — hiding a face left a visible HOLE. This model
// owns every predicate and publishes ONE ordered array of ACTIVE buttons; the
// View (HudKitController) subscribes ActiveButtonsChanged and just renders +
// centers the array it is passed. Zero predicates remain in the View.
//
// INPUTS (all Core-visible; Village mirrors in via the *HudBridge push seam):
//   TalkAvailable    - PostureSignals.TalkAvailable   (TalkHudBridge pushes)
//   RaidCapable      - PostureSignals.RaidCapable     (RaidCapabilityHudBridge:
//                      FeatureFlags.Raid AND barracks built AND >=1 deployable)
//   RaidArmyReady    - RaidEntryGate.ArmyStatus.Ready (BuildTimerService pushes;
//                      WO-820/823 dim gate — SEMANTICS PRESERVED: a capable-but-
//                      not-full army DIMS the visible Raids face, never disables,
//                      so the tap still reaches the drillmaster redirect)
//   MapUnlocked      - GameStateService.State.Onboarded (WO-825 R4 / WO-826)
//   BuildingFocused  - HudBuildingFocus.CurrentBuildingId non-empty
//   posture key      - forwarded by the View from PostureEvaluator (the View
//                      relays the notification it already receives; the town/
//                      explore set mapping lives HERE, not in the View)
//
// EDGE-TRIGGERED: Tick() polls the cheap sources every frame (the established
// HudBuildingFocus/ObsidianQueueGate poll precedent — no model event exists for
// most of these statics); ActiveButtonsChanged fires ONLY when the computed set
// actually changes, so the View re-lays out only on a real transition, never
// per-frame. The eventful signals (TalkChanged/RaidCapableChanged) are also
// subscribed by the Shared instance for same-frame response.
//
// TESTABILITY (the payoff of moving logic to Core): the ISource seam lets
// EditMode tests + HudActionBarRegression drive every signal combination through
// the REAL compute and assert the exact output array (impossible when the logic
// lived in the View's Update()). ServiceSource is the SOLE live resolution site.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.HudModel
{
    /// <summary>
    /// Identity of a bottom-action-bar button (WO-835). Enum ORDER IS THE BAR
    /// ORDER — the model emits actives sorted by this ordinal, left to right.
    /// Upgrade is the WO-835 split-out: its own context button (a focused
    /// building packs it IN), so Quests is never relabeled away again.
    /// </summary>
    public enum ActionBarButtonId
    {
        Build = 0,
        Talk = 1,
        Bag = 2,
        Raids = 3,
        Map = 4,
        Quests = 5,
        Upgrade = 6,
    }

    /// <summary>
    /// Computes the ordered set of APPLICABLE action-bar buttons from the Core
    /// context signals and raises <see cref="ActiveButtonsChanged"/> on a real
    /// set change (WO-835). The View renders exactly the array it is passed.
    /// </summary>
    public sealed class HudActionBarModel
    {
        /// <summary>Number of button identities (array sizing for the View).</summary>
        public const int ButtonCount = 7;

        // Posture keys the model maps to button sets (HudPostureKeys spellings —
        // the owner's hud-areas.json vocabulary; any other key => empty bar,
        // matching the occupancy rows that drop the bar in build/hostile/modal).
        public const string PostureTown = "calm(town)";
        public const string PostureExplore = "calm(explore)";

        /// <summary>Context-signal seam — tests inject a fake; live code uses
        /// <see cref="ServiceSource"/> (the sole live singleton-resolution site).</summary>
        public interface ISource
        {
            /// <summary>A talkable NPC is in range (PostureSignals.TalkAvailable).</summary>
            bool TalkAvailable { get; }
            /// <summary>Player CAN raid: FeatureFlags.Raid + barracks + >=1 deployable
            /// troop (PostureSignals.RaidCapable, Village-published).</summary>
            bool RaidCapable { get; }
            /// <summary>Army full (WO-820/823 dim gate, RaidEntryGate.ArmyStatus.Ready).</summary>
            bool RaidArmyReady { get; }
            /// <summary>Realm Map unlocked (GameState.Onboarded, WO-825 R4).</summary>
            bool MapUnlocked { get; }
            /// <summary>An upgradable building holds focus (HudBuildingFocus).</summary>
            bool BuildingFocused { get; }
        }

        /// <summary>The live source — reads the Core statics the retired View
        /// polls read, in one place.</summary>
        private sealed class ServiceSource : ISource
        {
            public bool TalkAvailable => PostureSignals.TalkAvailable;
            public bool RaidCapable => PostureSignals.RaidCapable;
            public bool RaidArmyReady => DeNelle.Core.UI.RaidEntryGate.ArmyStatus.Ready;
            public bool MapUnlocked
            {
                get
                {
                    // Explicit null checks — UnityEngine.Object never gets ?. (lint law).
                    var gs = DeNelle.Core.State.GameStateService.Instance;
                    return gs != null && gs.State != null && gs.State.Onboarded;
                }
            }
            public bool BuildingFocused =>
                !string.IsNullOrEmpty(DeNelle.Core.UI.HudBuildingFocus.CurrentBuildingId);
        }

        /// <summary>The one live model instance the HUD kit binds. Static lifetime
        /// (like the signal statics it composes); each kit instance subscribes on
        /// bind and unsubscribes on destroy, so scene swaps never go stale.</summary>
        public static HudActionBarModel Shared { get; } =
            new HudActionBarModel(new ServiceSource(), subscribeSignals: true);

        private readonly ISource _source;
        private readonly List<ActionBarButtonId> _active = new List<ActionBarButtonId>(ButtonCount);
        private int _activeMask = -1;        // -1 = never computed (first compute always publishes)
        private string _postureKey = "";
        private bool _raidsDimmed;
        private bool _raidsDimComputed;

        /// <summary>Raised when the ACTIVE set changed (edge-triggered — never per-frame).
        /// The View re-renders + re-centers exactly <see cref="Active"/>.</summary>
        public event Action ActiveButtonsChanged;

        /// <summary>Raised when the Raids dim state changed (WO-820 full-army gate —
        /// visual dim only; the View never reads the army status itself).</summary>
        public event Action RaidsDimmedChanged;

        /// <summary>The ordered active buttons (enum order, left to right).</summary>
        public IReadOnlyList<ActionBarButtonId> Active => _active;

        /// <summary>True while Raids is applicable but the army is not full — the View
        /// tints the face toward Disabled and keeps it INTERACTABLE (owner ruling:
        /// a dimmed tap still opens the drillmaster redirect).</summary>
        public bool RaidsDimmed => _raidsDimmed;

        /// <summary>The posture key last forwarded by the View (test/probe seam).</summary>
        public string PostureKey => _postureKey;

        /// <summary>Construct around a signal source. Tests pass a fake and leave
        /// <paramref name="subscribeSignals"/> false; the Shared live instance
        /// subscribes the eventful Core signals for same-frame edge response.</summary>
        public HudActionBarModel(ISource source, bool subscribeSignals = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _source = source;
            if (subscribeSignals)
            {
                // Static events + static-lifetime Shared instance — no teardown needed.
                PostureSignals.TalkChanged += Recompute;
                PostureSignals.RaidCapableChanged += Recompute;
            }
        }

        /// <summary>The View forwards each posture transition it receives from the
        /// evaluator (a relay, not a predicate — the key->set mapping lives here).</summary>
        public void SetPosture(string postureKey)
        {
            _postureKey = postureKey ?? "";
            Recompute();
        }

        /// <summary>Poll the poll-only sources (no model event exists for these Core
        /// statics — the HudBuildingFocus/ObsidianQueueGate precedent) and recompute.
        /// Cheap: five bool reads; the OUTPUT is edge-triggered.</summary>
        public void Tick() => Recompute();

        private void Recompute()
        {
            int mask = ComputeMask();
            bool dim = (mask & (1 << (int)ActionBarButtonId.Raids)) != 0 && !_source.RaidArmyReady;

            if (mask != _activeMask)
            {
                _activeMask = mask;
                _active.Clear();
                for (int i = 0; i < ButtonCount; i++)
                    if ((mask & (1 << i)) != 0) _active.Add((ActionBarButtonId)i);
                FlowTrace.Step("HudKit", "action bar set -> [" + DescribeActive() +
                               "] (posture '" + _postureKey + "')");
                ActiveButtonsChanged?.Invoke();
            }

            if (!_raidsDimComputed || dim != _raidsDimmed)
            {
                _raidsDimComputed = true;
                _raidsDimmed = dim;
                if (dim)
                    FlowTrace.Step("HudKit", "Raids face DIMMED (army not full - tap still redirects to drillmaster)");
                else if ((mask & (1 << (int)ActionBarButtonId.Raids)) != 0)
                    FlowTrace.Step("HudKit", "Raids face restored (army full)");
                RaidsDimmedChanged?.Invoke();
            }
        }

        // The WO-835 §3b predicate table, verbatim. Posture-first: only the two calm
        // postures show a bar at all (build/hostile/modal drop it via occupancy — the
        // model agrees by construction so the View never sees a stale set).
        private int ComputeMask()
        {
            if (_postureKey == PostureTown)
            {
                int mask = Bit(ActionBarButtonId.Build)      // always in town (posture gates)
                         | Bit(ActionBarButtonId.Bag)        // always applicable
                         | Bit(ActionBarButtonId.Quests);    // owner: "quests active more often"
                if (_source.TalkAvailable) mask |= Bit(ActionBarButtonId.Talk);
                if (_source.RaidCapable) mask |= Bit(ActionBarButtonId.Raids);      // WO-835 §3d hide default
                if (_source.MapUnlocked) mask |= Bit(ActionBarButtonId.Map);        // WO-825 R4 semantics
                if (_source.BuildingFocused) mask |= Bit(ActionBarButtonId.Upgrade); // §3c split-out
                return mask;
            }
            if (_postureKey == PostureExplore)
            {
                // The calm(explore) occupancy row carries only Talk + Bag — same set here.
                int mask = Bit(ActionBarButtonId.Bag);
                if (_source.TalkAvailable) mask |= Bit(ActionBarButtonId.Talk);
                return mask;
            }
            return 0;   // build / hostile / modal / unknown: the bar is down
        }

        private static int Bit(ActionBarButtonId id) => 1 << (int)id;

        private string DescribeActive()
        {
            var sb = new System.Text.StringBuilder(48);
            for (int i = 0; i < _active.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(_active[i]);
            }
            return sb.ToString();
        }
    }
}
