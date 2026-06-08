// =============================================================================
// ElaraWaveThreeJoin — the "Elara joins on wave 3" party-fill beat (WO: party-of-4).
// -----------------------------------------------------------------------------
// Canon join order (companion roster): Sylas (beat-1) → Elara (wave 3) → Grom
// (first OuterWorld return). This component delivers the SECOND join: the Cleric
// companion, Elara, arrives once the player has held the line through wave 3.
//
// It is a near-twin of SylasFirstMeeting (the proven first-meeting → join
// pattern): self-bootstrapping, no scene edit, drives a few scripted lines
// through the joining companion's own bubble, suppresses ambient chatter while it
// scripts, persists a one-shot SeenTutorials flag, and — the actual JOIN — calls
// GameStateService.AddToParty(id). AddToParty fires PlayerChanged, which the
// StoryCompanionInjector listens to and re-spawns the roster against — so the new
// companion appears, follows, fights the shared TargetManager's hostiles, and the
// party frame grows to 3 (PartyHudBridge reflects PartySize).
//
// TRIGGER: WaveManager.OnWaveCleared(int) — when the cleared wave id reaches
// JoinWave (3) and the beat hasn't run, Elara joins. Idempotent: a once-only
// SeenTutorials flag + a session guard, so it never replays.
//
// CANON (companion class ≠ player class, no duplicate roster class): Elara is the
// Cleric by default. If the player IS the Cleric (or the Cleric is already in the
// party), we substitute a DIFFERENT free class via the SAME CompanionSpawner
// mapping the FTUE / Sylas beat use — so the party never holds two of one class
// and the joining companion is never a mirror of the hero.
// =============================================================================

using System;
using Cysharp.Threading.Tasks;
using DeNelle.Core.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>
    /// Plays the one-shot Elara wave-3 → party-join beat, reusing the existing
    /// <see cref="StoryCompanion"/> (follow + fight) and the
    /// <see cref="GameStateService"/> party roster. Self-bootstraps; gated on the
    /// <c>elara_wave3_join</c> one-shot flag and triggered by
    /// <see cref="WaveManager.OnWaveCleared"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElaraWaveThreeJoin : MonoBehaviour
    {
        private const string TargetScene = "Village2";

        /// <summary>The one-shot SeenTutorials key — once set the beat never replays.</summary>
        private const string SeenKey = "elara_wave3_join";

        /// <summary>The wave whose clear summons Elara (canon: ~wave 3).</summary>
        private const int JoinWave = 3;

        /// <summary>Dev override — replay the beat even if the save says it's seen.</summary>
        public static bool ForceRun;

        // Belt-and-braces: one run per session regardless of save / event timing.
        private static bool s_ranThisSession;

        private WaveManager _wave;
        private bool _hooked;
        private bool _running;
        private HeroClass _companionClass = HeroClass.Cleric;
        private TutorialDialogue _dialogue;

        // =====================================================================
        //  Bootstrap (no scene edit) + wire the wave-clear trigger
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<ElaraWaveThreeJoin>() != null) return;
            new GameObject("ElaraWaveThreeJoin").AddComponent<ElaraWaveThreeJoin>();
        }

        private void Awake()
        {
            if (SceneManager.GetActiveScene().name != TargetScene) { Destroy(gameObject); return; }
        }

        private void Update()
        {
            // Lazily resolve + hook the WaveManager once it exists in the scene (it
            // may spawn after us). Cheap: stops scanning the moment we're hooked.
            if (_hooked) return;
            if (!ShouldStillTry) { Destroy(gameObject); return; }

            if (_wave == null) _wave = FindObjectOfType<WaveManager>();
            if (_wave != null)
            {
                _wave.OnWaveCleared.AddListener(OnWaveCleared);
                _hooked = true;
            }
        }

        private void OnDestroy()
        {
            if (_wave != null && _hooked) _wave.OnWaveCleared.RemoveListener(OnWaveCleared);
        }

        /// <summary>Whether it's still worth keeping the watcher alive (not yet seen).</summary>
        private bool ShouldStillTry
        {
            get
            {
                if (s_ranThisSession) return false;
                if (ForceRun) return true;
                var svc = GameStateService.Instance;
                if (svc == null || svc.State == null) return true; // wait for Core
                return !HasSeen(svc.State);
            }
        }

        // =====================================================================
        //  Trigger — wave cleared
        // =====================================================================

        private void OnWaveCleared(int waveId)
        {
            if (_running || s_ranThisSession) return;
            if (!ForceRun && waveId < JoinWave) return;

            var svc = GameStateService.Instance;
            if (!ForceRun && svc != null && svc.State != null && HasSeen(svc.State)) return;

            Run().Forget();
        }

        private static bool HasSeen(GameState state) =>
            state != null && state.SeenTutorials != null
            && state.SeenTutorials.TryGetValue(SeenKey, out bool seen) && seen;

        // =====================================================================
        //  The join beat (mirrors SylasFirstMeeting.Run)
        // =====================================================================

        private async UniTask Run()
        {
            s_ranThisSession = true;
            _running = true;

            try
            {
                _companionClass = ResolveCompanionClass();

                // THE JOIN: enrol the companion into the persisted party roster. The
                // injector listens to PlayerChanged (fired by AddToParty) and spawns a
                // body for the new roster member that follows + fights; PartyHudBridge
                // grows the party frame to match PartySize. Idempotent in AddToParty.
                GameStateService.Instance?.AddToParty(_companionClass.ToString());

                // Let the body spawn + settle, then frame a short arrival via its bubble.
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

                Debug.Log($"[ElaraWaveThreeJoin] {speaker} joined the party after wave {JoinWave}.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ElaraWaveThreeJoin] Join beat error — finishing anyway. {ex.Message}");
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

        /// <summary>
        /// Elara is the Cleric by default. If the player IS the Cleric, or the Cleric
        /// is already in the party, pick the next free class so the party never holds
        /// two of a class and the companion is never a mirror of the hero. Uses the
        /// same <see cref="CompanionSpawner.CompanionClassFor"/> mapping as the FTUE.
        /// </summary>
        private static HeroClass ResolveCompanionClass()
        {
            HeroClass player = ResolvePlayerClass();
            HeroClass pick = HeroClass.Cleric;   // Elara

            // If that clashes with the player or an already-joined class, walk to a free one.
            if (pick == player || IsInParty(pick))
            {
                pick = CompanionSpawner.CompanionClassFor(player);   // a different class by canon
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
            HeroClass.Cleric, HeroClass.Ranger, HeroClass.Knight, HeroClass.Mage,
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
            // Fall back to the injector's representative companion.
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
                case HeroClass.Cleric: // Elara — the canonical wave-3 arrival
                    return new[]
                    {
                        "Three waves, and the Heart still beats. You held — but you're bleeding for it.",
                        "Elara. I tend the wounded and keep the candles lit when the nights run long.",
                        "You shouldn't carry this road alone. So I'll mend what I can, and stand where you stand.",
                        "Lead on, Keeper. I've stitched soldiers back together on worse ground than this.",
                    };
                case HeroClass.Mage: // Thrain stand-in
                    return new[]
                    {
                        "The light steadied when you held the line — I felt it from the stacks.",
                        "Thrain, Keeper of the Light. The Heart still teaches me each dawn, and now it sends me to you.",
                        "Walk with me. I'll ward what the books can't, and light the way through the dark side of this land.",
                    };
                case HeroClass.Ranger: // Sylas stand-in (rare)
                    return new[]
                    {
                        "You've earned three dawns. Most don't earn one. I've been watching from the treeline.",
                        "Sylas. Scout of the Reach. I put an arrow in whatever finds us first.",
                        "I'll walk where you walk, Keeper. Let's see what the wild's been pushing toward Elarion.",
                    };
                default: // Grom stand-in
                    return new[]
                    {
                        "Three waves held. A wall holds better with two — and a town better with four.",
                        "Grom. Veteran of the Wall. I'll teach you the rest, and bleed beside you doing it.",
                        "Stay close, Keeper. The Heart holds the town; we hold the Heart.",
                    };
            }
        }
    }
}
