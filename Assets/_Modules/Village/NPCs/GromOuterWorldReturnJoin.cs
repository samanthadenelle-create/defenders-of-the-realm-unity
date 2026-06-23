// =============================================================================
// GromOuterWorldReturnJoin — "Grom joins on first OuterWorld return" (WO: party-of-4).
// -----------------------------------------------------------------------------
// Canon join order: Sylas (beat-1) → Elara (wave 3) → Grom (first OuterWorld
// return). This component delivers the THIRD join, filling the party to four
// (hero + Sylas + Elara + Grom): the Knight veteran, Grom, joins the first time
// the player ventures OUT into the surrounding world and comes back to Elarion.
//
// Near-twin of ElaraWaveThreeJoin / SylasFirstMeeting (the proven join pattern):
// self-bootstrapping, no scene edit, scripted bubble lines, one-shot SeenTutorials
// flag, and the actual JOIN = GameStateService.AddToParty(id). AddToParty fires
// PlayerChanged → StoryCompanionInjector re-spawns the roster → Grom appears,
// follows, fights the shared TargetManager's hostiles, and the party frame grows
// to 4 (PartyHudBridge reflects PartySize).
//
// TRIGGER (story-progression marker, robust to the additive OuterWorld model):
// OuterWorld loads ADDITIVELY over Village (one continuous space — there is no
// scene swap to hook), so we detect the round-trip GEOMETRICALLY. Elarion sits at
// the origin inside a ~21u wall ring; the four OuterWorld regions lie well beyond.
// We latch "ventured out" once the hero passes FarRadius from origin (clearly in
// OuterWorld), then fire the join the next time the hero comes back within
// HomeRadius (back inside the town). Idempotent: a once-only SeenTutorials flag +
// a session guard, so it never replays.
//
// CANON (companion class ≠ player, no duplicate roster class): Grom is the Knight
// by default; if the player IS the Knight (or the Knight is already in the party)
// we substitute the next FREE class via the same mapping the FTUE uses, so the
// party never holds two of a class and Grom is never a mirror of the hero.
// =============================================================================

using System;
using Cysharp.Threading.Tasks;
using DeNelle.Core;
using DeNelle.Core.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>
    /// Plays the one-shot Grom first-OuterWorld-return → party-join beat, reusing the
    /// existing <see cref="StoryCompanion"/> and the <see cref="GameStateService"/>
    /// party roster. Self-bootstraps; gated on the <c>grom_world_return_join</c>
    /// one-shot flag and triggered by the hero's first venture-out-and-return.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GromOuterWorldReturnJoin : MonoBehaviour
    {
        /// <summary>The one-shot SeenTutorials key — once set the beat never replays.</summary>
        private const string SeenKey = "grom_world_return_join";

        // Geometry (origin = the Heart of Elarion at scene centre). The wall ring is
        // ~21u; the village footprint reaches ~45u; OuterWorld regions lie beyond.
        private const float FarRadius  = 70f;   // past this = clearly out in OuterWorld
        private const float HomeRadius = 40f;   // back within this = returned to the town

        /// <summary>Dev override — replay the beat even if the save says it's seen.</summary>
        public static bool ForceRun;

        // Belt-and-braces: one run per session regardless of save timing.
        private static bool s_ranThisSession;

        private Transform _hero;
        private float _resolveTimer;
        private bool _venturedOut;
        private bool _running;
        private HeroClass _companionClass = HeroClass.Knight;
        private TutorialDialogue _dialogue;

        // =====================================================================
        //  Bootstrap (no scene edit)
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<GromOuterWorldReturnJoin>() != null) return;
            new GameObject("GromOuterWorldReturnJoin").AddComponent<GromOuterWorldReturnJoin>();
        }

        private void Awake()
        {
            // Widened from an exact "Village2" match to the canonical hub list so the
            // first-return join can bootstrap in the castle hub (MainCastle_Hall) too. The
            // venture-out/return detection is GEOMETRIC (FarRadius/HomeRadius from origin),
            // so it still only fires after a real OuterWorld round-trip — this only lets the
            // watcher live in the castle hub instead of self-destructing off Village2.
            if (!HubScenes.IsHub(SceneManager.GetActiveScene().name)) { Destroy(gameObject); return; }
        }

        private void Update()
        {
            if (_running) return;
            if (!ShouldStillTry) { Destroy(gameObject); return; }

            ResolveHeroIfNeeded();
            if (_hero == null) return;

            // Distance from the Heart on the ground plane.
            Vector3 d = _hero.position; d.y = 0f;
            float distSqr = d.sqrMagnitude;

            if (!_venturedOut)
            {
                if (distSqr >= FarRadius * FarRadius) _venturedOut = true;
                return;
            }

            // Ventured out, now back inside the town → Grom joins.
            if (distSqr <= HomeRadius * HomeRadius)
                Run().Forget();
        }

        /// <summary>Whether it's still worth watching (not yet seen / run).</summary>
        private bool ShouldStillTry
        {
            get
            {
                if (s_ranThisSession) return false;

                // SINGLE-HERO (ff.singlehero, default ON): NO companions — the first-return
                // join must NOT recruit Grom. Stand down (the watcher self-destructs). Flag
                // OFF restores the party-of-4 join.
                if (FeatureFlags.SingleHero) return false;

                if (ForceRun) return true;
                var svc = GameStateService.Instance;
                if (svc == null || svc.State == null) return true; // wait for Core
                return !HasSeen(svc.State);
            }
        }

        private static bool HasSeen(GameState state) =>
            state != null && state.SeenTutorials != null
            && state.SeenTutorials.TryGetValue(SeenKey, out bool seen) && seen;

        private void ResolveHeroIfNeeded()
        {
            if (_hero != null) return;
            _resolveTimer -= Time.deltaTime;
            if (_resolveTimer > 0f) return;
            _resolveTimer = 0.75f;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t != null && t.name.StartsWith("Hero")) { _hero = t; return; }
        }

        // =====================================================================
        //  The join beat (mirrors ElaraWaveThreeJoin.Run)
        // =====================================================================

        private async UniTask Run()
        {
            if (_running || s_ranThisSession) return;
            s_ranThisSession = true;
            _running = true;

            try
            {
                _companionClass = ResolveCompanionClass();

                // THE JOIN: enrol into the persisted party roster. The injector spawns
                // the body (follow + fight); PartyHudBridge grows the frame to 4.
                GameStateService.Instance?.AddToParty(_companionClass.ToString());

                await UniTask.Delay(TimeSpan.FromSeconds(1.0f));

                StoryCompanion companion = ResolveCompanion(_companionClass);
                _dialogue = gameObject.AddComponent<TutorialDialogue>();
                if (companion != null)
                {
                    companion.SetSpeechSuppressed(true);
                    _dialogue.SetBubble(companion.Bubble);
                }

                string speaker = CompanionDialogue.NameFor(_companionClass);
                foreach (string line in JoinLines(_companionClass))
                    _dialogue.Say(speaker, line);

                await WaitForDialogue();

                companion?.SetSpeechSuppressed(false);
                MarkSeen();

                Debug.Log($"[GromOuterWorldReturnJoin] {speaker} joined the party on the first return to Elarion.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GromOuterWorldReturnJoin] Join beat error — finishing anyway. {ex.Message}");
                ResolveCompanion(_companionClass)?.SetSpeechSuppressed(false);
                MarkSeen();
            }
            finally
            {
                _running = false;
                Destroy(gameObject, 0.5f);
            }
        }

        // =====================================================================
        //  Companion class resolution (canon: ≠ player, ≠ already-in-party)
        // =====================================================================

        private static HeroClass ResolveCompanionClass()
        {
            HeroClass player = ResolvePlayerClass();
            HeroClass pick = HeroClass.Knight;   // Grom

            if (pick == player || IsInParty(pick))
            {
                pick = CompanionSpawner.CompanionClassFor(player);
                foreach (HeroClass cls in AllClasses())
                {
                    if (cls == player || IsInParty(cls)) continue;
                    pick = cls;
                    break;
                }
            }
            return pick;
        }

        private static HeroClass[] AllClasses() => new[]
        {
            HeroClass.Knight, HeroClass.Ranger, HeroClass.Cleric, HeroClass.Mage,
        };

        private static bool IsInParty(HeroClass cls)
        {
            var svc = GameStateService.Instance;
            return svc != null && svc.IsInParty(cls.ToString());
        }

        private static HeroClass ResolvePlayerClass()
        {
            var svc = GameStateService.Instance;
            if (svc != null && svc.State != null)
                return svc.State.HeroClass.ToNullable() ?? HeroClass.Knight;
            return HeroClass.Knight;
        }

        private static StoryCompanion ResolveCompanion(HeroClass cls)
        {
            foreach (var c in FindObjectsByType<StoryCompanion>(FindObjectsSortMode.None))
                if (c != null && c.Hero == cls) return c;
            var injector = StoryCompanionInjector.Instance;
            return injector != null ? injector.Companion : null;
        }

        private async UniTask WaitForDialogue()
        {
            if (_dialogue == null) return;
            await UniTask.Yield();
            await UniTask.WaitUntil(() => _dialogue.IsIdle);
        }

        private static void MarkSeen() => GameStateService.Instance?.MarkTutorialSeen(SeenKey);

        // =====================================================================
        //  Scripted join lines (canon tone — grounded, warm)
        // =====================================================================

        private static string[] JoinLines(HeroClass companion)
        {
            switch (companion)
            {
                case HeroClass.Knight: // Grom — the canonical return arrival
                    return new[]
                    {
                        "Back through the gate in one piece. Good. The wild eats the careless out there.",
                        "Grom. Veteran of the Wall. I've buried good soldiers — I'd sooner train one well.",
                        "You've walked the dark side of this land and come home. That's more than most.",
                        "Stay close, Keeper. I'll teach you the rest, and I'll bleed beside you doing it.",
                    };
                case HeroClass.Ranger: // Sylas stand-in
                    return new[]
                    {
                        "You made it back. I had eyes on you the whole way through the treeline.",
                        "Sylas. Scout of the Reach. I put an arrow in whatever finds us first.",
                        "I'll walk where you walk now, Keeper — in or out of these walls.",
                    };
                case HeroClass.Cleric: // Elara stand-in
                    return new[]
                    {
                        "You're back, and walking — thank the Heart. The road out there is unkind.",
                        "Elara. I tend the wounded and keep the candles lit when the nights run long.",
                        "I'll walk it with you — mend what I can, and stand where you stand.",
                    };
                default: // Thrain stand-in
                    return new[]
                    {
                        "You returned, and the light steadied. The Heart noticed — so did I.",
                        "Thrain, Keeper of the Light. I'll ward what the books can't.",
                        "Walk with me, Keeper. There's far more out there to learn, and little time.",
                    };
            }
        }
    }
}
