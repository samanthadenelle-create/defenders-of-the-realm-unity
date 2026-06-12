// =============================================================================
// CompanionMeetingTrigger — self-bootstrapping host for the CompanionMeeting Yarn
// dialogue, decoupled from TutorialDirector (DEF-265).
// -----------------------------------------------------------------------------
// BUG (owner playtest 2026-06-04, DEF-265): on first village entry the player
// expected the companion dialogue and got nothing. Player.log:
//   "[OnboardingIntegrator] No OnboardingFlow in the scene."
// DEF-251 (commit bc49673) wired Yarn so that simply INSTANTIATING
// Resources/Dialogue/DialogueSystem.prefab (a ClassicRPG dialogue prefab whose
// DialogueRunner autoStarts the "CompanionMeeting" node from the compiled
// YarnProject) plays the dialogue. The intended host —
// TutorialDirector.OnVillageProgressionStart() — instantiates that prefab, BUT
// TutorialDirector isn't present in Village2 (component-by-component build gap)
// AND it's first-run-gated, so the hook never fires.
//
// FIX (global, code-only, no scene edits): mirror EventSystemEnsurer /
// HeroControlEnsurer with a single [RuntimeInitializeOnLoadMethod] trigger that,
// on village scene load (scene.name CONTAINS "Village" — covers Village and
// Village2), instantiates Resources.Load<GameObject>("Dialogue/DialogueSystem")
// ONCE. The prefab self-runs CompanionMeeting; we deliberately do NOT reference
// any Yarn types here — just Resources.Load + Instantiate.
//
// GATING: fires once per save via a PlayerPrefs key so it doesn't replay on every
// village (re)load. Re-test escape hatches:
//   * launch with "-yarnAlways" on the command line (System.Environment.
//     GetCommandLineArgs()) — fires on EVERY village entry, or
//   * clear/delete the PlayerPrefs key "yarn.companionMeeting.seen".
// LogWarning-safe (not error) if the prefab is missing — the pack may be absent.
// =============================================================================

using System;
using DeNelle.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>
    /// Hosts the CompanionMeeting Yarn dialogue in the village scene without
    /// requiring TutorialDirector to be present (DEF-265). Instantiates the
    /// self-running ClassicRPG dialogue prefab once per save on village entry;
    /// re-testable via the "-yarnAlways" launch flag or by clearing the
    /// PlayerPrefs gate key.
    /// </summary>
    public static class CompanionMeetingTrigger
    {
        // PlayerPrefs gate — set after the dialogue is hosted so it plays once per save.
        // Clear this key (PlayerPrefs.DeleteKey) to re-test from a fresh-village state.
        private const string SeenKey = "yarn.companionMeeting.seen";

        // Resources path (no extension) to the wired ClassicRPG dialogue prefab.
        private const string CompanionMeetingNode = "CompanionMeeting";

        // Dev escape hatch — pass this on the command line to fire on every village load.
        private const string AlwaysFlag = "-yarnAlways";

        // Guard so a single play session never double-hosts within one process,
        // even if the village scene is loaded more than once.
        private static bool _hostedThisSession;

        /// <summary>True once the FTUE dialogue has been hosted this session. Read by
        /// TutorialDirector to defer its wave-loop handoff to the Yarn narrative.</summary>
        public static bool Hosted { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            TryHostForActiveScene();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsVillageScene(scene.name)) SafeTryHost();
        }

        private static void TryHostForActiveScene()
        {
            if (IsVillageScene(SceneManager.GetActiveScene().name)) SafeTryHost();
        }

        // WEBGL CRASH-GUARD (WO-331): both call sites run from engine callbacks
        // (RuntimeInitializeOnLoadMethod / SceneManager.sceneLoaded). An uncaught
        // throw out of one of those halts the WebGL player on Village load. Hosting
        // the FTUE dialogue is non-essential to the village loading, so any failure
        // must degrade (log) — never take the scene down.
        private static void SafeTryHost()
        {
            try
            {
                TryHost();
            }
            catch (Exception ex)
            {
                Debug.LogError("[CompanionMeetingTrigger] TryHost threw — FTUE dialogue skipped so " +
                               "the village still loads (WebGL crash-guard). " + ex);
            }
        }

        // Widened from a "Village"-contains check to the canonical hub list so the FTUE
        // companion meeting also fires in the castle hub (MainCastle_Hall), not just the
        // abandoned Village2 — the companion-never-joins gate. HubScenes.IsHub covers
        // Village2 AND the castle, so the village flow is preserved.
        private static bool IsVillageScene(string sceneName) => HubScenes.IsHub(sceneName);

        private static void TryHost()
        {
            // Once-per-save gate applies in ALL builds now (owner: the FTUE must NOT
            // re-trigger on retry). Only the explicit -yarnAlways desktop flag forces a
            // replay for testing — dev builds no longer auto-replay every entry.
            bool always = HasAlwaysFlag();

            // FAST PATH (default "Start New") — owner: fast into battle. The full Yarn
            // CompanionMeeting is the deep, blocking FTUE; it must ONLY play when the
            // player opted into the full tutorial ("Play Intro"). On the fast path the
            // brief hook lives in TutorialDirector.RunFastPath instead, so we DON'T host
            // the meeting here. The -yarnAlways dev flag still forces it for testing.
            if (!always && OnboardingMode.FastPath)
            {
                Debug.Log("[CompanionMeetingTrigger] Fast-path onboarding — skipping the full " +
                          "CompanionMeeting Yarn FTUE (the brief hook runs in TutorialDirector).");
                return;
            }

            // Once per process, regardless of gate (avoid double-hosting on re-load).
            if (_hostedThisSession && !always) return;

            // First-village-entry-per-save gate (skipped when the dev flag is set).
            if (!always && PlayerPrefs.GetInt(SeenKey, 0) != 0) return;

            // If a dialogue is already hosted (e.g. a redundant caller), don't double up.
            if (DialogueService.Current != null)
            {
                _hostedThisSession = true;
                Hosted = true;
                return;
            }

            // Single launch path for ALL dialogue (host-or-reuse + command-bridge
            // install + node validation) — see DialogueService. "CompanionMeeting"
            // is the FTUE opener.
            bool started = DialogueService.Play(CompanionMeetingNode);
            if (!started)
            {
                // Prefab/project/node missing — DialogueService already logged why.
                // Do NOT mark the save seen so the FTUE can retry on a later entry.
                return;
            }

            _hostedThisSession = true;
            Hosted = true;

            // Mark the save so it doesn't replay. NOTE: when -yarnAlways is set we still
            // record it (harmless), but the flag short-circuits the gate on every load.
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();

            Debug.Log("[CompanionMeetingTrigger] Started '" + CompanionMeetingNode +
                      "' via DialogueService. " +
                      (always ? "(-yarnAlways set: fires every village entry.)"
                              : "(Gated once per save via PlayerPrefs '" + SeenKey + "'.)"));
        }

        private static bool HasAlwaysFlag()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                if (args != null)
                {
                    for (int i = 0; i < args.Length; i++)
                        if (string.Equals(args[i], AlwaysFlag, StringComparison.OrdinalIgnoreCase))
                            return true;
                }
            }
            catch (Exception e)
            {
                // GetCommandLineArgs can be restricted on some platforms (e.g. WebGL) —
                // never let the re-test hatch crash the gate.
                Debug.LogWarning("[CompanionMeetingTrigger] Could not read command line args " +
                                 "for the -yarnAlways flag: " + e.Message);
            }
            return false;
        }
    }
}
