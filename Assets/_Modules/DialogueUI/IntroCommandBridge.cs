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
            // IMPORTANT (re-entrancy guard): every handler is registered as a YarnTask-returning
            // delegate that yields ONE frame (YieldFrame) before returning. The intro screens fire
            // several commands back-to-back (e.g. <<fade_from_black>> then <<play_sfx>>). Under
            // YarnSpinner v3, when a command handler completes SYNCHRONOUSLY the runner calls
            // Dialogue.SignalContentComplete() then Dialogue.Continue() inline — and Continue() pumps
            // the VM straight into the NEXT command on the same stack frame. That nested command, also
            // synchronous, would call SignalContentComplete() again while the outer Continue() is still
            // unwinding → "SignalContentComplete can only be called when a command is being dispatched".
            // Yielding a frame makes each handler's task NOT complete synchronously, so the runner takes
            // its await branch (await dispatchResult.Task) instead of re-entering — the signal still
            // fires, just not nested. (Mirrors the village bridge's IEnumerator-blocking model.)
            reg.AddCommandHandler("fade_from_black", (System.Func<float, YarnTask>)(d => CmdFade(Color.black, 1f, 0f, d)));
            reg.AddCommandHandler("fade_to_black",   (System.Func<float, YarnTask>)(d => CmdFade(Color.black, 0f, 1f, d)));
            reg.AddCommandHandler("fade_to_white",   (System.Func<float, YarnTask>)(d => CmdFade(Color.white, 0f, 1f, d)));
            reg.AddCommandHandler("fade_from_white", (System.Func<float, YarnTask>)(d => CmdFade(Color.white, 1f, 0f, d)));
            reg.AddCommandHandler("play_sfx",        (System.Func<string, YarnTask>)CmdPlaySfx);
            reg.AddCommandHandler("play_music",      (System.Func<string, YarnTask>)CmdPlayMusic);
            reg.AddCommandHandler("transition_to",   (System.Func<string, YarnTask>)CmdTransitionTo);
        }

        // A handler must NOT complete synchronously, or the runner re-enters the next command inline
        // and double-fires SignalContentComplete (see Install). Yielding one frame defers completion.
        private static YarnTask YieldFrame() => YarnTask.Yield();

        // ── Audio ────────────────────────────────────────────────────────────

        private static async YarnTask CmdPlaySfx(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                var clip = Resources.Load<AudioClip>("Sfx/" + id);   // atmospheric clips may not exist yet
                if (clip != null) CoreServices.Audio?.PlaySfx(clip, 0.8f);
            }
            await YieldFrame();
        }

        private static async YarnTask CmdPlayMusic(string id)
        {
            // The only track the intro asks for today.
            if (id == "title_theme") CoreServices.Audio?.PlayMusic(MusicTrack.Title);
            await YieldFrame();
        }

        // ── Scene transition ──────────────────────────────────────────────────

        private async YarnTask CmdTransitionTo(string target)
        {
            // The intro ends by handing off to hero select. Do NOT call _runner.Stop() here:
            // Yarn's DialogueRunner runs Dialogue.Continue() AFTER a command handler returns, so a
            // mid-command Stop() tears down the selected node and the post-command Continue() throws
            // "No node has been selected" (ticket #5, RCA 2026-06-21; canonical anti-pattern, memory
            // yarn-no-node-stop-after-panel-command). The script ends the runner cleanly with <<stop>>
            // right after <<transition_to>>, so the runner completes via onDialogueComplete — no orphaned
            // Continue(). The handler only performs the scene route.
            await YieldFrame();
            SceneRouter.GoHeroSelect();
        }

        // ── Fade overlay (runtime UI Toolkit, no UGUI dependency) ─────────────

        // Command wrapper: start the fade (a coroutine animates it), then yield a frame so the
        // handler never completes synchronously (see Install — re-entrancy guard). The fade visual
        // keeps running on its own coroutine; we don't block the narrative on the full fade duration.
        private async YarnTask CmdFade(Color color, float fromA, float toA, float seconds)
        {
            Fade(color, fromA, toA, seconds);
            await YieldFrame();
        }

        private void EnsureFade()
        {
            if (_fade != null) return;
            // Unity overloads == against fake-null, so `?? AddComponent` returns a fake-null and never
            // falls through. Use TryGetComponent and add only when genuinely absent.
            if (!gameObject.TryGetComponent(out _fadeDoc)) _fadeDoc = gameObject.AddComponent<UIDocument>();
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
