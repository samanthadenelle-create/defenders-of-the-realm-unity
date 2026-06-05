// =============================================================================
// IntroCommandBridge — services the cinematic intro's Yarn commands.
// -----------------------------------------------------------------------------
// IntroSequence.yarn calls fade / audio / scene commands that have no handlers on
// their own (an unhandled command errors + stalls the runner). This registers them
// and delegates to engine services, mirroring DialogueCommandBridge for the FTUE:
//   fade_from_black / fade_to_white / fade_from_white  → screen fade overlay
//   play_sfx <name>                                    → Resources/Sfx/<name> (best-effort)
//   play_music <name>                                  → CoreServices.Audio.PlayMusic
//   transition_to <scene>                              → SceneRouter (→ hero select)
//
// MVP: fades are a simple full-screen CanvasGroup-free alpha on a runtime UI Toolkit
// element (no UGUI dependency); atmospheric SFX that don't exist yet no-op cleanly.
// Lives in DeNelle.DialogueUI so the ClassicRPG/Yarn dependency stays isolated.
// =============================================================================

using UnityEngine;
using UnityEngine.UIElements;
using Yarn.Unity;
using DeNelle.Core;
using DeNelle.Core.Audio;

namespace DeNelle.DialogueUI
{
    /// <summary>Registers + services the cinematic intro's Yarn commands.</summary>
    [DisallowMultipleComponent]
    public sealed class IntroCommandBridge : MonoBehaviour
    {
        private DialogueRunner _runner;
        private UIDocument _fadeDoc;
        private VisualElement _fade;
        private Coroutine _fadeRoutine;

        public void Install(DialogueRunner runner)
        {
            if (runner == null) return;
            _runner = runner;

            var reg = (IActionRegistration)_runner;
            reg.AddCommandHandler("fade_from_black", (System.Action<float>)(d => Fade(Color.black, 1f, 0f, d)));
            reg.AddCommandHandler("fade_to_black",   (System.Action<float>)(d => Fade(Color.black, 0f, 1f, d)));
            reg.AddCommandHandler("fade_to_white",   (System.Action<float>)(d => Fade(Color.white, 0f, 1f, d)));
            reg.AddCommandHandler("fade_from_white", (System.Action<float>)(d => Fade(Color.white, 1f, 0f, d)));
            reg.AddCommandHandler("play_sfx",        (System.Action<string>)CmdPlaySfx);
            reg.AddCommandHandler("play_music",      (System.Action<string>)CmdPlayMusic);
            reg.AddCommandHandler("transition_to",   (System.Action<string>)CmdTransitionTo);
        }

        // ── Audio ────────────────────────────────────────────────────────────

        private static void CmdPlaySfx(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var clip = Resources.Load<AudioClip>("Sfx/" + id);   // atmospheric clips may not exist yet
            if (clip != null) CoreServices.Audio?.PlaySfx(clip, 0.8f);
        }

        private static void CmdPlayMusic(string id)
        {
            // The only track the intro asks for today.
            if (id == "title_theme") CoreServices.Audio?.PlayMusic(MusicTrack.Title);
        }

        // ── Scene transition ──────────────────────────────────────────────────

        private void CmdTransitionTo(string target)
        {
            // The intro ends by handing off to hero select. Stop the runner first so it
            // doesn't keep ticking into the next scene.
            if (_runner != null && _runner.IsDialogueRunning) _runner.Stop();
            SceneRouter.GoHeroSelect();
        }

        // ── Fade overlay (runtime UI Toolkit, no UGUI dependency) ─────────────

        private void EnsureFade()
        {
            if (_fade != null) return;
            _fadeDoc = gameObject.GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
            if (_fadeDoc.panelSettings == null)
            {
                // Borrow a live panel so the overlay actually renders.
                var any = Object.FindObjectOfType<UIDocument>();
                if (any != null && any != _fadeDoc) _fadeDoc.panelSettings = any.panelSettings;
            }
            var root = _fadeDoc.rootVisualElement;
            if (root == null) return;
            _fade = new VisualElement { name = "intro-fade" };
            var s = _fade.style;
            s.position = Position.Absolute;
            s.top = 0f; s.left = 0f; s.right = 0f; s.bottom = 0f;
            _fade.pickingMode = PickingMode.Ignore;
            root.Add(_fade);
        }

        private void Fade(Color color, float fromA, float toA, float seconds)
        {
            EnsureFade();
            if (_fade == null) return;
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeRoutine(color, fromA, toA, Mathf.Max(0.01f, seconds)));
        }

        private System.Collections.IEnumerator FadeRoutine(Color color, float fromA, float toA, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(fromA, toA, t / seconds);
                _fade.style.backgroundColor = new Color(color.r, color.g, color.b, a);
                yield return null;
            }
            _fade.style.backgroundColor = new Color(color.r, color.g, color.b, toA);
            _fadeRoutine = null;
        }
    }
}
