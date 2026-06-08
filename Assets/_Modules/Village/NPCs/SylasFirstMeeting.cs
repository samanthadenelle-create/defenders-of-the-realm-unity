// =============================================================================
// SylasFirstMeeting — the scripted "you meet Sylas, and he JOINS" beat (WO-238 / DEF-180).
// -----------------------------------------------------------------------------
// The companion roster (Thrain/Mage, Grom/Knight, Sylas/Ranger, Elara/Cleric) joins
// the Keeper one at a time. This component delivers the FIRST-MEETING beat for the
// Ranger companion, Sylas ("Scout of the Reach"): he appears at the Keeper's shoulder,
// speaks a short scripted greeting through his world-space bubble, and then JOINS the
// party — following the hero and fighting beside them. The "join" is not a new system:
// the StoryCompanion that the village already spawns ALREADY follows + fights once it
// exists, so the meeting beat is the scripted hand-off that makes that companion read
// as a recruited party member rather than ambient scenery.
//
// ── What it REUSES (no new companion / dialogue / combat system) ─────────────────────
//   • Spawn / follow / fight — StoryCompanionInjector + StoryCompanion. The injector
//     already spawns ONE companion near the hero who trails them and engages the shared
//     TargetManager's hostiles. We do not spawn a second body; we make the existing
//     companion BE Sylas (via the injector's class override) and frame the meeting.
//   • Dialogue delivery      — TutorialDialogue (the same tap-to-advance queue the FTUE
//     uses) driving the companion's own TownsfolkBubble, so the lines read as Sylas
//     speaking. We suppress the companion's ambient auto-chatter only while we script it.
//   • Once-only gate         — GameState.SeenTutorials (markTutorialSeen), the same
//     one-shot flag store the project already persists; keyed "sylas_first_meeting".
//
// ── Different-class-if-the-player-IS-Sylas (mirrors the FTUE rule) ───────────────────
// A companion is never a mirror of the player. If the player picked Ranger (so the
// HERO is Sylas), the first companion to meet + join is a DIFFERENT class, chosen via
// the SAME CompanionSpawner.CompanionClassFor mapping the tutorial uses (Ranger →
// Cleric/Elara). The dialogue, name, and title all follow the chosen companion class,
// so the beat is coherent whichever hero the player runs.
//
// ── Non-collision with the FTUE (TutorialDirector) ───────────────────────────────────
// The first-time tutorial ALSO spawns a companion and runs its own arrival meeting and
// owns the bubble while it plays. To avoid two meetings fighting over the one bubble,
// this beat DEFERS entirely while the FTUE is eligible to run (a brand-new save, i.e.
// Onboarded == false) — the tutorial introduces the companion in that path. This
// standalone Sylas meeting therefore fires for the returning-but-hasn't-met-Sylas
// player (Onboarded == true, flag not yet set), exactly once, and then never replays.
//
// Self-bootstrapping DontDestroyOnLoad-free runner (one per Village load), no scene
// edit, no Village.unity / VillageSceneBuilder change (both off-limits). async UniTask
// throughout — never async void. Every singleton / cross-module call is null-guarded;
// off the village scene it simply no-ops.
// =============================================================================

using System;
using Cysharp.Threading.Tasks;
using DeNelle.Core.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>
    /// Plays the one-shot Sylas first-meeting → party-join beat in the Village scene,
    /// reusing the existing <see cref="StoryCompanion"/> (follow + fight) and
    /// <see cref="TutorialDialogue"/> (scripted bubble lines). Self-bootstraps; gated on
    /// the <c>sylas_first_meeting</c> one-shot flag and deferred while the FTUE runs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SylasFirstMeeting : MonoBehaviour
    {
        // ── Tuning ───────────────────────────────────────────────────────────
        private const string TargetScene = "Village2";

        /// <summary>The one-shot SeenTutorials key — once set the beat never replays.</summary>
        private const string SeenKey = "sylas_first_meeting";

        // Let the village settle (hero rig, NavMesh, HUD, the companion injector's
        // own AfterSceneLoad spawn) before staging the meeting — the same settle the
        // FTUE waits out so the companion exists to drive.
        private const float SettleSeconds = 1.5f;

        // The meeting is a "you encounter him" beat: wait until the hero and the
        // companion are both present and within sight of each other before speaking,
        // so the lines land when Sylas is actually beside the Keeper. Generous, so it
        // fires promptly even though the companion already trails close.
        private const float MeetRange = 14f;
        // Failsafe: if proximity never resolves (no hero found, odd scene), proceed
        // after this long so the beat can't silently wedge.
        private const float ProximityTimeoutSeconds = 8f;

        /// <summary>Dev override — replay the beat even if the save says it's seen.</summary>
        public static bool ForceRun;

        // Belt-and-braces: one run per session regardless of save timing.
        private static bool s_ranThisSession;

        // ── Sub-component (added at runtime, same as the FTUE) ────────────────
        private TutorialDialogue _dialogue;

        private HeroClass _companionClass = HeroClass.Ranger;
        private bool _running;

        // =====================================================================
        //  Bootstrap (no scene edit)
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<SylasFirstMeeting>() != null) return;
            new GameObject("SylasFirstMeeting").AddComponent<SylasFirstMeeting>();
        }

        private void Awake()
        {
            // Only ever needed in the Village scene; clean up elsewhere.
            if (SceneManager.GetActiveScene().name != TargetScene)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (!ShouldRun)
            {
                Destroy(gameObject);
                return;
            }

            Run().Forget();
        }

        /// <summary>True when the Sylas meeting should play this session.</summary>
        private bool ShouldRun
        {
            get
            {
                if (s_ranThisSession) return false;
                if (ForceRun) return true;

                var svc = GameStateService.Instance;
                if (svc == null || svc.State == null) return false;

                // Already met Sylas (or his stand-in) → never again.
                if (HasSeen(svc.State)) return false;

                // Defer to the FTUE on a brand-new save: TutorialDirector spawns a
                // companion + runs its own arrival meeting in that path, so this
                // standalone beat would double up. It fires once the FTUE is behind
                // the player (Onboarded == true) and the flag is still unset.
                if (!svc.State.Onboarded) return false;

                return true;
            }
        }

        private static bool HasSeen(GameState state)
        {
            return state != null
                && state.SeenTutorials != null
                && state.SeenTutorials.TryGetValue(SeenKey, out bool seen)
                && seen;
        }

        // =====================================================================
        //  The meeting → join beat
        // =====================================================================

        private async UniTask Run()
        {
            s_ranThisSession = true;
            _running = true;

            try
            {
                // Let the scene + the companion injector's own spawn settle first.
                await UniTask.Delay(TimeSpan.FromSeconds(SettleSeconds));

                // WO-238 — prefer the authored Yarn first-meeting node when it's compiled
                // into the project. The Yarn node IS the recruit (it fires StartQuest +
                // RecruitCompanion "Ranger" + CompleteQuest through the command bridge, so
                // the injector still spawns + joins Sylas), and it plays through the shared
                // tap-to-advance DialogueService runner. We only take this path for the
                // canonical Sylas (Ranger) meeting; the rare different-class substitution
                // (player IS the Ranger) keeps the bubble lines below. Mark the one-shot
                // flag so this standalone beat never replays once Yarn has the meeting.
                if (ResolveCompanionClass() == HeroClass.Ranger &&
                    DialogueService.NodeExists("SylasFirstMeeting"))
                {
                    DialogueService.Play("SylasFirstMeeting");
                    MarkSeen();
                    Debug.Log("[SylasFirstMeeting] Delegated to the authored Yarn node 'SylasFirstMeeting' (recruit fires via the command bridge).");
                    return;
                }

                // Make the village's single companion BE Sylas — or, if the player IS
                // Sylas (Ranger hero), a DIFFERENT companion via the FTUE mapping. The
                // injector spawns exactly one body that already follows + fights, so
                // this is the JOIN: from here the companion trails + fights as a party
                // member. We only repurpose the existing companion; we never spawn a
                // second one.
                _companionClass = ResolveCompanionClass();
                StoryCompanion companion = ResolveAndConfigureCompanion();

                // Drive the scripted greeting through the companion's own bubble so it
                // reads as the companion speaking. Suppress its ambient auto-chatter
                // while we own the lines (follow + fight are unaffected).
                _dialogue = gameObject.AddComponent<TutorialDialogue>();
                if (companion != null)
                {
                    companion.SetSpeechSuppressed(true);
                    _dialogue.SetBubble(companion.Bubble);
                }

                // Wait until Sylas is actually beside the Keeper (he trails close, so
                // this resolves quickly) before the meeting lines land. Failsafe-timed.
                await WaitUntilBeside(companion);

                // The scripted first meeting (3–5 lines, canon tone — grounded, warm,
                // no fake-archaic speech; the Hollow Ones read as grief, not villains).
                string speaker = CompanionDialogue.NameFor(_companionClass);
                foreach (string line in MeetingLines(_companionClass))
                    _dialogue.Say(speaker, line);

                await WaitForDialogue();

                // Restore the companion's ambient voice — the meeting is over, Sylas
                // has joined, and from here he chats + follows + fights on his own.
                companion?.SetSpeechSuppressed(false);

                // Persist the one-shot flag so the meeting never replays (idempotent).
                MarkSeen();

                Debug.Log($"[SylasFirstMeeting] {speaker} met the Keeper and joined the party.");
            }
            catch (Exception ex)
            {
                // Never wedge the game on a narrative hiccup — still mark seen so a
                // broken beat can't loop, and restore any suppressed companion voice.
                Debug.LogWarning($"[SylasFirstMeeting] Meeting beat error — finishing anyway. {ex.Message}");
                ResolveCompanion()?.SetSpeechSuppressed(false);
                MarkSeen();
            }
            finally
            {
                _running = false;
                Destroy(gameObject, 0.5f);
            }
        }

        // =====================================================================
        //  Companion resolution (reuse the existing StoryCompanion = the JOIN)
        // =====================================================================

        /// <summary>
        /// The companion class for this meeting. Normally Sylas (Ranger); if the player
        /// chose Ranger (the HERO is Sylas) the first companion is a DIFFERENT class via
        /// the same mapping the FTUE uses (Ranger → Cleric/Elara), so we never meet a
        /// mirror of the player.
        /// </summary>
        private static HeroClass ResolveCompanionClass()
        {
            HeroClass player = ResolvePlayerClass();
            // Default beat: meet Sylas (Ranger). Only when the player IS the Ranger do
            // we swap to a different companion so it isn't a duplicate of the player.
            if (player == HeroClass.Ranger)
                return CompanionSpawner.CompanionClassFor(player); // Ranger → Cleric (Elara)
            return HeroClass.Ranger;                               // meet Sylas
        }

        private static HeroClass ResolvePlayerClass()
        {
            var svc = GameStateService.Instance;
            if (svc != null && svc.State != null)
                return svc.State.HeroClass.ToNullable() ?? HeroClass.Knight;
            return HeroClass.Knight;
        }

        /// <summary>
        /// Forces the village's single companion to be the meeting's class (so it IS
        /// Sylas / the chosen stand-in) and returns it. The companion already follows +
        /// fights, so this hand-off is the party JOIN — no second body is created.
        /// </summary>
        private StoryCompanion ResolveAndConfigureCompanion()
        {
            var injector = StoryCompanionInjector.Instance;
            if (injector == null) return null;

            // Re-spawn the one companion as the meeting class (Sylas, unless the player
            // is the Ranger). Idempotent: the injector replaces rather than duplicates.
            injector.SetHeroClassOverride(_companionClass);
            return injector.Companion;
        }

        private static StoryCompanion ResolveCompanion()
        {
            var injector = StoryCompanionInjector.Instance;
            return injector != null ? injector.Companion : null;
        }

        // =====================================================================
        //  Staging — wait until the companion is beside the hero
        // =====================================================================

        /// <summary>
        /// Resolves once the companion is within <see cref="MeetRange"/> of the hero (so
        /// the meeting lines land with Sylas actually present), or after a failsafe
        /// timeout so a missing hero can never wedge the beat.
        /// </summary>
        private async UniTask WaitUntilBeside(StoryCompanion companion)
        {
            float deadline = Time.unscaledTime + ProximityTimeoutSeconds;
            Transform hero = ResolveHero();

            while (Time.unscaledTime < deadline)
            {
                if (hero == null) hero = ResolveHero();
                Transform comp = companion != null ? companion.transform : null;

                if (hero != null && comp != null)
                {
                    Vector3 d = comp.position - hero.position; d.y = 0f;
                    if (d.sqrMagnitude <= MeetRange * MeetRange) return;
                }
                await UniTask.Yield();
            }
            // Timed out — proceed regardless (the companion trails close in practice).
        }

        // Name-based Keeper lookup (matches StoryCompanion / VillageNpcInjector): the
        // village hero rig is named "Hero (...)"; the project defines no "Player" tag.
        private static Transform ResolveHero()
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t != null && t.name.StartsWith("Hero")) return t;
            return null;
        }

        private async UniTask WaitForDialogue()
        {
            if (_dialogue == null) return;
            // A frame so the just-enqueued lines register, then wait for the queue to
            // drain (tap-to-advance / auto-failsafe — same as the FTUE).
            await UniTask.Yield();
            await UniTask.WaitUntil(() => _dialogue.IsIdle);
        }

        // =====================================================================
        //  Persistence (one-shot)
        // =====================================================================

        private static void MarkSeen()
        {
            GameStateService.Instance?.MarkTutorialSeen(SeenKey);
        }

        // =====================================================================
        //  Scripted meeting lines (canon tone — narrative bible §8)
        // =====================================================================

        /// <summary>
        /// The 3–5 first-meeting lines for the joining companion. Sylas (the default)
        /// gets a scout's-greeting beat that ends in him committing to walk with the
        /// Keeper; the rare different-class stand-ins reuse their established voice from
        /// <see cref="CompanionDialogue"/> so every path stays coherent.
        /// </summary>
        // LOCALIZE: kept here as constants, the same convention CompanionDialogue uses;
        // a future localization pass lifts these wholesale.
        private static string[] MeetingLines(HeroClass companion)
        {
            switch (companion)
            {
                case HeroClass.Ranger: // Sylas, Scout of the Reach — the canonical meeting
                    return new[]
                    {
                        "Hold — easy. I've had eyes on you since the treeline. You're the new Keeper.",
                        "Sylas. Scout of the Reach. I've read these woods longer than the Hollow Ones have walked them.",
                        "The wild's been restless. Something's pushing them toward Elarion, and I'd rather not watch it alone.",
                        "So here's my offer, Keeper — I walk where you walk, and I put an arrow in whatever finds us first.",
                        "Lead on. Let's see what the dark side of this land is hiding.",
                    };

                case HeroClass.Cleric: // Elara stand-in (player chose Ranger / is Sylas)
                    return new[]
                    {
                        "You're awake, and walking — thank the Heart. I'd half given up waiting at the gate.",
                        "Elara. I tend the wounded and keep the candles lit when the nights run long.",
                        "You shouldn't carry this road alone. No guardian should.",
                        "So I'll walk it with you — mend what I can, and stand where you stand.",
                    };

                case HeroClass.Knight: // Grom stand-in
                    return new[]
                    {
                        "There you are. I've held this wall through worse mornings, but a wall holds better with two.",
                        "Grom. Veteran of the Wall. I've buried good soldiers — I'd sooner train one well.",
                        "The Heart holds the town; we hold the Heart. Simple, and just as heavy.",
                        "Stay close, Keeper. I'll teach you the rest, and I'll bleed beside you doing it.",
                    };

                case HeroClass.Mage: // Thrain stand-in
                    return new[]
                    {
                        "Ah — there you are. The light steadied the moment you stirred. Curious.",
                        "Thrain, Keeper of the Light. I've read every page in Elarion's stacks, and the Heart still teaches me each dawn.",
                        "There is a great deal the Heart wishes you to learn, and little time to learn it.",
                        "Walk with me, then. I'll light the way, and ward what the books can't.",
                    };

                default:
                    return new[]
                    {
                        "There you are, Keeper. The road's long, and better walked with company.",
                        "I'll walk it with you — and fight beside you when it turns dark.",
                    };
            }
        }
    }
}
