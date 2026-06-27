// =============================================================================
// ScreenFader — the concrete full-screen fade overlay that finally WIRES the
// project's long-declared-but-never-assigned DeNelle.Core.ISceneFader hook.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// FINDING (encounter-feedback work, 2026-06-27): SceneRouter.Fader (the ISceneFader
// property LoadSceneWithFade + region crossings call) is DECLARED in Core but was
// NEVER assigned anywhere in the codebase — so every "fade" was a silent no-op and
// transitions hard-cut. This is the missing implementation: a DDOL ScreenSpaceOverlay
// canvas with a black full-screen Image driven by a CanvasGroup alpha. EnsureInstalled()
// lazily stands it up and assigns SceneRouter.Fader if still null, so:
//   * BattleArena masks its ~7km arena warps (drives the coroutine wrappers directly), and
//   * SceneRouter.LoadSceneWithFade / region crossings finally fade for real (bonus).
//
// Presentation-only (a screen overlay). Implements BOTH a coroutine API (FadeOutCo /
// FadeInCo — what BattleArena's IEnumerator stages consume) and the ISceneFader UniTask
// API (what SceneRouter awaits), sharing the same alpha-lerp. Unscaled time so a slow-mo
// death-cam timescale never stretches a fade. ASCII logs; null-guarded; never throws.
// =============================================================================

using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.UI
{
    /// <summary>Full-screen black fade overlay implementing <see cref="ISceneFader"/>.</summary>
    public sealed class ScreenFader : MonoBehaviour, ISceneFader
    {
        private static ScreenFader _instance;
        private CanvasGroup _group;

        /// <summary>The live fader (creates a persistent overlay on first access and installs it
        /// into <see cref="SceneRouter.Fader"/> if that hook is still unwired). Idempotent.</summary>
        public static ScreenFader EnsureInstalled()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("ScreenFader");
            if (Application.isPlaying) DontDestroyOnLoad(go);   // edit-mode/batchmode safe (mirrors BattleArena.Instance)
            _instance = go.AddComponent<ScreenFader>();
            _instance.Build();
            if (SceneRouter.Fader == null)
            {
                SceneRouter.Fader = _instance;
                FlowTrace.Step("ScreenFader", "installed -> SceneRouter.Fader wired (was null; transitions can mask now).");
            }
            return _instance;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;   // above BattleArenaHud (5000) and the 9-zone HUD

            var blackGo = new GameObject("Black");
            blackGo.transform.SetParent(transform, false);
            var black = blackGo.AddComponent<Image>();
            black.color = Color.black;
            black.raycastTarget = false;
            var rt = black.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        // ── coroutine API (BattleArena's IEnumerator stages consume these) ─────
        /// <summary>Fade the overlay to fully opaque (black) over <paramref name="seconds"/>.</summary>
        public IEnumerator FadeOutCo(float seconds) => FadeRoutine(1f, seconds);

        /// <summary>Fade the overlay back to fully transparent over <paramref name="seconds"/>.</summary>
        public IEnumerator FadeInCo(float seconds) => FadeRoutine(0f, seconds);

        private IEnumerator FadeRoutine(float target, float seconds)
        {
            if (_group == null) yield break;
            if (target > 0.5f) _group.blocksRaycasts = true;   // block stray input while opaque
            float start = _group.alpha;
            seconds = Mathf.Max(0.01f, seconds);
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(start, target, t / seconds);
                yield return null;
            }
            _group.alpha = target;
            if (target <= 0.001f) _group.blocksRaycasts = false;
        }

        // ── ISceneFader (UniTask) API (SceneRouter.LoadSceneWithFade awaits these) ──
        public async UniTask FadeOut(float seconds)
        {
            if (_group == null) return;
            await FadeAsync(1f, seconds);
        }

        public async UniTask FadeIn(float seconds)
        {
            if (_group == null) return;
            await FadeAsync(0f, seconds);
        }

        private async UniTask FadeAsync(float target, float seconds)
        {
            if (target > 0.5f) _group.blocksRaycasts = true;
            float start = _group.alpha;
            seconds = Mathf.Max(0.01f, seconds);
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(start, target, t / seconds);
                await UniTask.Yield();
            }
            _group.alpha = target;
            if (target <= 0.001f) _group.blocksRaycasts = false;
        }
    }
}
