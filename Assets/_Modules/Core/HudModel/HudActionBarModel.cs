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

        /// <summary>
        /// ⚠ RETIRED FROM THE BAR (WO-911, owner ruling Q10+Q13, 2026-08-06) — Map moved INTO Bag
        /// as a tab. The enum VALUE is kept DORMANT on purpose, not deleted: the ordinal is the bar
        /// order AND the index into the View's face arrays, so renumbering would silently re-point
        /// every other face. Nothing ever sets this bit any more (see <c>ComputeMask</c>), so it
        /// never renders. Do not reuse the value for a new face.
        /// </summary>
        Map = 4,

        Quests = 5,

        /// <summary>
        /// RE-POINTED, NOT ADDED (WO-911, ruling Q10+Q13). This face was the context-sensitive
        /// "Upgrade" button; it is now the single door to the unified MANAGE / QUEUES screen
        /// (<c>PanelId.Manage</c>) and is always applicable in town rather than gated on a focused
        /// building. Keeping the VALUE at 6 is what dissolves the 8th-face problem entirely — no
        /// enum extension, no <see cref="HudActionBarModel.ButtonCount"/> increase, no new
        /// hud-areas.json widget id (the row stays "upgradeButton").
        /// </summary>
        Upgrade = 6,
    }

    /// <summary>
    /// Computes the ordered set of APPLICABLE action-bar buttons from the Core
    /// context signals and raises <see cref="ActiveButtonsChanged"/> on a real
    /// set change (WO-835). The View renders exactly the array it is passed.
    /// </summary>
    public sealed class HudActionBarModel
    {
        /// <summary>
        /// Number of button IDENTITIES — the array-sizing / mask-iteration bound for the View.
        /// -------------------------------------------------------------------------------------
        /// ⚠ THIS IS NOT THE NUMBER OF FACES THAT RENDER. The rendered list is
        /// <see cref="Active"/>, whose length varies 0..<see cref="MaxVisibleFaces"/> per posture;
        /// the View lays out <c>Active.Count</c> faces and centres them, so a shorter set can never
        /// leave a dead trailing slot.
        ///
        /// WO-911 (ruling Q10+Q13) settled this at source, because the ruling's "no ButtonCount
        /// change is needed" is exact for the Upgrade->Manage RE-POINT but Map's REMOVAL is a
        /// separate count question: this stays 7 because <see cref="ActionBarButtonId.Map"/> is kept
        /// DORMANT rather than renumbered, and every face array is indexed by the enum ORDINAL
        /// (Upgrade = 6). Dropping this to 6 would put Upgrade out of bounds. The count that
        /// actually changed 7 -> 6 is <see cref="MaxVisibleFaces"/>, which is what the View's slot
        /// geometry must derive from.
        /// </summary>
        public const int ButtonCount = 7;

        /// <summary>
        /// WO-911 — the MAXIMUM number of faces that can render at once (the widest posture set).
        /// The calm(town) bar is Build, Talk, Bag, Raids, Quests, Manage = SIX since Map moved into
        /// Bag as a tab. This is the number the View must size a slot from; <see cref="ButtonCount"/>
        /// is the enum-identity bound and is deliberately larger (Map stays dormant at ordinal 4).
        /// </summary>
        public const int MaxVisibleFaces = 6;

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
                // WO-911 (ruling Q10+Q13, 2026-08-06): the bar goes 7 -> 6 faces.
                //  • Map is GONE from the bar — it is a tab inside Bag now. The
                //    ActionBarButtonId.Map bit is never set here again; _source.MapUnlocked stays on
                //    the ISource seam because it is still the Onboarded gate other code reads, but
                //    it no longer packs a face.
                //  • Upgrade is RE-POINTED to the unified Manage/Queues screen and is therefore
                //    ALWAYS applicable in town — it is the single door to the three production
                //    lines, so gating it on _source.BuildingFocused (the old WO-835 §3c split-out)
                //    would make the queues reachable only while standing next to a building, which
                //    is the exact undiscoverability this WO exists to remove.
                int mask = Bit(ActionBarButtonId.Build)      // always in town (posture gates)
                         | Bit(ActionBarButtonId.Bag)        // always applicable
                         | Bit(ActionBarButtonId.Quests)     // owner: "quests active more often"
                         | Bit(ActionBarButtonId.Upgrade);   // WO-911: the Manage/Queues door
                if (_source.TalkAvailable) mask |= Bit(ActionBarButtonId.Talk);
                if (_source.RaidCapable) mask |= Bit(ActionBarButtonId.Raids);      // WO-835 §3d hide default
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
