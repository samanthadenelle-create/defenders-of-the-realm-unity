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
        private const string PrefabResourcePath = "Dialogue/DialogueSystem";

        // Dev escape hatch — pass this on the command line to fire on every village load.
        private const string AlwaysFlag = "-yarnAlways";

        // Guard so a single play session never double-hosts within one process,
        // even if the village scene is loaded more than once.
        private static bool _hostedThisSession;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            TryHostForActiveScene();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsVillageScene(scene.name)) TryHost();
        }

        private static void TryHostForActiveScene()
        {
            if (IsVillageScene(SceneManager.GetActiveScene().name)) TryHost();
        }

        private static bool IsVillageScene(string sceneName) =>
            !string.IsNullOrEmpty(sceneName) &&
            sceneName.IndexOf("Village", StringComparison.OrdinalIgnoreCase) >= 0;

        private static void TryHost()
        {
            // Dev builds (BuildOptions.Development) ALWAYS replay so the owner can step
            // through the Yarn dialogue on EVERY village entry — the -yarnAlways flag is
            // desktop-only (no command line on WebGL). Release builds keep the
            // once-per-save gate so players see it a single time.
            bool always = HasAlwaysFlag() || Debug.isDebugBuild;

            // Once per process, regardless of gate (avoid double-hosting on re-load).
            if (_hostedThisSession && !always) return;

            // First-village-entry-per-save gate (skipped when the dev flag is set).
            if (!always && PlayerPrefs.GetInt(SeenKey, 0) != 0) return;

            var prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"[CompanionMeetingTrigger] Resources/{PrefabResourcePath} " +
                                 "not found — CompanionMeeting dialogue cannot play. " +
                                 "(The dialogue prefab/pack may be missing from this build.)");
                return;
            }

            UnityEngine.Object.Instantiate(prefab).name = "DialogueSystem (CompanionMeeting)";
            _hostedThisSession = true;

            // Mark the save so it doesn't replay. NOTE: when -yarnAlways is set we still
            // record it (harmless), but the flag short-circuits the gate on every load.
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();

            Debug.Log("[CompanionMeetingTrigger] Hosted Resources/" + PrefabResourcePath +
                      " — CompanionMeeting Yarn dialogue should now play. " +
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
