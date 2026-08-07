// =============================================================================
// TownSuspension - the ONE authority for "the town is standing still because the
// player is elsewhere in their own game" (owner ruling 2026-08-07).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core
//
// THE OWNER'S RULING, VERBATIM:
//   "everything pauses except harvesting, while player is active"
//   "part of that is deliberate that they need to make sure town is strong
//    before leaving it unattended."
//
// THE AXIS IS **ACTIVE vs OFFLINE**, NOT dungeon-vs-town. Getting this backwards
// inverts the whole design, so it is stated once, here, and every gate reads it
// from this file:
//
//   PLAYER ACTIVE, ELSEWHERE (a dungeon, a raid, an arena fight)
//       -> the town PAUSES. The player is PRESENT, just not standing in the town.
//          Suspended: wave countdown, enemy spawns, structure damage, HEART
//          damage. Still running: HARVESTING, always.
//
//   PLAYER OFFLINE (app closed / backgrounded)
//       -> the town is EXPOSED. DELIBERATELY. That is where the defensive
//          pressure lives - "make sure town is strong before leaving it
//          unattended" - and it must NOT be softened. Offline accrual is a
//          SEPARATE path (OfflineHarvestService, 10h cap) and NOTHING in this
//          file is ever consulted for it. Do not conflate the two.
//
// WHY NOT Time.timeScale: because the player is STANDING IN the other scene.
// Freezing the clock would freeze the dungeon they are playing. So the town's
// systems are suspended DELIBERATELY, one by one, each checking this authority
// at its own tick site.
//
// THE SCENE RULE THAT MAKES THAT SAFE (SuspendedFor): a system suspends only if
// its own GameObject does NOT live in the currently ACTIVE scene. Anything in the
// scene the player is actually standing in is NEVER suspended, no matter what
// this authority says. That single test is what keeps a global town pause from
// ever touching the dungeon - it is not possible to freeze the room the player is
// in through this API.
//
// HARVESTING IS NOT REPRESENTED HERE AT ALL. There is deliberately no
// HarvestSuspended flag: a flag that exists is a flag someone eventually reads,
// and the ruling is that harvesting never stops. Its absence is the enforcement.
//
// PAUSE-SEAM PRECEDENT: this mirrors RepEngageWatcher.PauseAll/ResumeAll (a
// static gate honoured at the top of each Update plus at the action entry point)
// and adds that seam's hardest-won lesson - EVERY teardown path must resume, or
// the freeze leaks for the rest of the session. See ResumeIfStale below.
//
// Instrumented [Flow:TownSuspend]. No silent transitions.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core
{
    /// <summary>
    /// What to do with a wave that is ALREADY IN PROGRESS when the player leaves
    /// the town. OPEN DESIGN QUESTION - the owner has not ruled. Both behaviours
    /// are implemented so the ruling is a one-line change and never a rewrite.
    /// </summary>
    public enum InProgressWavePolicy
    {
        /// <summary>
        /// DEFAULT. The live wave is frozen where it stands and resumes (after the
        /// return grace) exactly as it was. Non-destructive: nothing the player
        /// already fought is thrown away, and nothing is handed to them for free.
        /// Chosen as the default only because it DISCARDS NOTHING - not because it
        /// is known to be the ruling.
        /// </summary>
        SuspendAndResume = 0,

        /// <summary>
        /// The live wave is abandoned on exit and the town returns to its calm
        /// posture. Kinder on return (the player never walks back into a fight they
        /// left mid-swing) but it erases progress through that wave and can be
        /// farmed - enter a dungeon to wipe a wave going badly.
        /// </summary>
        CancelOnEntry = 1,
    }

    /// <summary>
    /// Single authority for suspending the town's systems while the player is
    /// ACTIVE but elsewhere. Read-only for nearly every consumer: call
    /// <see cref="SuspendedFor"/> at a tick site and obey it.
    /// </summary>
    public static class TownSuspension
    {
        /// <summary>
        /// Seconds the town stays suspended AFTER the player returns. The return must
        /// NOT dump a held wave on a player who is still loading in / re-orienting -
        /// the same courtesy the post-loss path gives (RepEngageWatcher's 3.5s
        /// PostLossGraceSeconds, matched deliberately so the two windows feel alike).
        /// </summary>
        public const float DefaultReturnGraceSeconds = 3.5f;

        /// <summary>
        /// OPEN QUESTION - see <see cref="InProgressWavePolicy"/>. Settable so the
        /// owner's ruling lands as one assignment. Nothing in this file assumes which
        /// way it goes; both paths are honoured by the wave-side gate.
        /// </summary>
        public static InProgressWavePolicy WavePolicy { get; set; } = InProgressWavePolicy.SuspendAndResume;

        private static bool _suspended;
        private static string _reason = "none";
        private static float _graceUntil = -1f;
        private static string _lastActiveScene;

        /// <summary>True while the town is held still because the player is elsewhere.</summary>
        public static bool IsSuspended => _suspended;

        /// <summary>Why the town is suspended (scene name / caller tag) - for traces only.</summary>
        public static string Reason => _reason;

        /// <summary>
        /// True while the post-return grace is still running. Town systems stay held
        /// during this window even though <see cref="IsSuspended"/> has gone false.
        /// </summary>
        public static bool ReturnGraceActive => Time.time < _graceUntil;

        /// <summary>Seconds left in the post-return grace (0 when not in one).</summary>
        public static float ReturnGraceRemaining => Mathf.Max(0f, _graceUntil - Time.time);

        /// <summary>
        /// The combined gate: suspended outright, OR still inside the return grace.
        /// This is what tick sites should consult, via <see cref="SuspendedFor"/>.
        /// </summary>
        public static bool Held => _suspended || ReturnGraceActive;

        // ── The scene-safe gate every consumer should call ──────────────────────

        /// <summary>
        /// THE gate. True when <paramref name="owner"/> is a town-side object that
        /// must hold still right now.
        ///
        /// The load-bearing clause is the ACTIVE-SCENE EXEMPTION: an object living in
        /// the scene the player is currently standing in is NEVER suspended. That is
        /// what makes a town-wide pause safe to ship - a dungeon's own enemies, its own
        /// damageable props and its own spawners all sit in the active scene, so this
        /// returns false for them even at the height of a town suspension. It is
        /// structurally impossible to freeze the room the player is in through this API.
        ///
        /// A null owner, or an object with no valid scene (DontDestroyOnLoad singletons
        /// such as the region roamer spawner), is treated as TOWN-SIDE and suspended.
        /// That direction is deliberate: the failure mode of wrongly suspending a
        /// persistent town service is a paused town, which is the intent anyway; the
        /// failure mode of wrongly running one is damage the absent player cannot see.
        /// </summary>
        public static bool SuspendedFor(GameObject owner)
        {
            if (!Held) return false;
            if (owner == null) return true;

            var scene = owner.scene;
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.name)) return true;   // DDOL / unowned

            // The player is standing here - never hold it still.
            if (scene.handle == SceneManager.GetActiveScene().handle) return false;

            return true;
        }

        /// <summary>
        /// Convenience for a MonoBehaviour tick site: <c>if (TownSuspension.SuspendedFor(this)) return;</c>
        /// </summary>
        public static bool SuspendedFor(Component owner)
            => SuspendedFor(owner != null ? owner.gameObject : null);

        // ── Transitions ─────────────────────────────────────────────────────────

        /// <summary>
        /// Hold the town still. Idempotent - a second call while already suspended
        /// only refreshes the reason, it never stacks (the leak that would need a
        /// matching count of Resumes to clear).
        /// </summary>
        public static void Suspend(string reason)
        {
            string r = string.IsNullOrEmpty(reason) ? "unspecified" : reason;
            if (_suspended)
            {
                if (r != _reason)
                {
                    _reason = r;
                    FlowTrace.Step("TownSuspend", $"town already suspended - reason updated to '{r}'.");
                }
                return;
            }
            _suspended = true;
            _reason = r;
            _graceUntil = -1f;
            FlowTrace.Step("TownSuspend",
                $"town SUSPENDED ({r}). Held: wave countdown, enemy spawns, structure damage, heart damage. " +
                $"Still running: HARVESTING (never suspended). Wave policy={WavePolicy}.");
        }

        /// <summary>
        /// Release the town, then hold it for <paramref name="graceSeconds"/> more so the
        /// return does not dump a suspended wave on a player who has just loaded in.
        /// Idempotent and safe to call when not suspended (that is the point - every
        /// teardown path can call it unconditionally, which is how the RepEngageWatcher
        /// seam avoids leaking a permanent freeze).
        /// </summary>
        public static void Resume(string reason, float graceSeconds = DefaultReturnGraceSeconds)
        {
            if (!_suspended)
            {
                // Not an error. Teardown paths call this unconditionally on purpose.
                return;
            }
            _suspended = false;
            float g = Mathf.Max(0f, graceSeconds);
            _graceUntil = Time.time + g;
            FlowTrace.Step("TownSuspend",
                $"town RESUMED ({(string.IsNullOrEmpty(reason) ? "unspecified" : reason)}) after '{_reason}' - " +
                $"holding {g:0.0}s return grace so no held wave lands the instant the player is back.");
            _reason = "none";
        }

        // ── Automatic scene-driven drive ────────────────────────────────────────

        /// <summary>
        /// Reset the statics on a fresh play session. Domain reload can be OFF in this
        /// project, so a static left true would carry a permanent town freeze into the
        /// next Play - the exact leak class the arena seam's watchdogs exist to prevent.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _suspended = false;
            _reason = "none";
            _graceUntil = -1f;
            _lastActiveScene = null;
            WavePolicy = InProgressWavePolicy.SuspendAndResume;
        }

        /// <summary>
        /// Drive the suspension off the ACTIVE SCENE. Deliberately expressed as "not a
        /// hub" rather than "is a dungeon": the ruling is about the player being ACTIVE
        /// SOMEWHERE ELSE, and dungeons are only one of the places that can be. Raids,
        /// ATB battles and any future elsewhere are covered by the same sentence, and no
        /// new scene has to be added to a whitelist to be safe. There is no IsDungeon()
        /// helper in the project, and this design means one is not needed.
        ///
        /// The ARENA is NOT covered by this hook - it is staged 7 km away in the SAME
        /// scene, so no scene change occurs. BattleArena drives it explicitly at the two
        /// points it already drives RepEngageWatcher.PauseAll/ResumeAll.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallSceneHook()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EvaluateActiveScene("startup");
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode mode) => EvaluateActiveScene("sceneLoaded:" + s.name);

        private static void OnActiveSceneChanged(Scene from, Scene to) => EvaluateActiveScene("activeSceneChanged:" + to.name);

        private static void EvaluateActiveScene(string trigger)
        {
            var active = SceneManager.GetActiveScene();
            string name = active.name;
            if (string.IsNullOrEmpty(name)) return;
            if (name == _lastActiveScene) return;
            _lastActiveScene = name;

            // Menus / front-end are not "the player is off adventuring" - there is no town
            // loaded to protect and suspending there would only muddy the trace. Only a
            // real gameplay scene that is not a hub counts as being elsewhere.
            bool isHub = HubScenes.IsHub(name);
            bool isFrontEnd = name == SceneRouter.Title || name.StartsWith("HeroSelect", System.StringComparison.OrdinalIgnoreCase);

            if (isHub || isFrontEnd)
            {
                Resume("active scene '" + name + "' is " + (isHub ? "a hub" : "front-end") + " (" + trigger + ")");
                return;
            }

            Suspend("player active in '" + name + "' (" + trigger + ")");
        }
    }
}
