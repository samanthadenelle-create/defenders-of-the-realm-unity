// =============================================================================
// EndStateView — the ONE shared Obsidian end-state screen (WO-B, UI conformance
// audit 2026-07-02 §3.2). Victory / defeat / hero-death / wave-results all render
// through THIS view from an EndStateVM. Replaces the divergent implementations:
// BattleArenaHud.ShowVictorySummary + ShowLossPanel (retired in that file) and
// WaveCelebrationManager's IMGUI toast / prefab text (retired there).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// Canon (docs/UI_BLINK_TEMPLATE_CANON.md + owner addenda 2026-07-02):
//   • Master factory only: ElarionUiKit.BuildObsidianModal / BuildObsidianPanel
//     with frameName = RpgUiCatalog.FrameCore; content DROPS into the returned
//     drop-zones (header / body / footer). No per-screen chrome.
//   • ONE way out (owner button law): a single primary kit Button in the footer.
//     The factory's shared Close chip is HIDDEN here — an end-state must not
//     offer a second, redundant exit. (Kit change reported: a `withClose:false`
//     parameter on BuildObsidianPanel would make this first-class.)
//   • Sized to content: the panel rect is computed from what the VM carries —
//     no cavernous empty space (the owner's F8 "THis looks bad" Victory modal).
//   • SMOOTH (owner directive): fade+scale in ~250ms ease-out (unscaled time),
//     spoils rows stagger-reveal ~50ms apart, the primary button lands last.
//     No pre-existing shared UI tween helper exists in the codebase (searched:
//     only ad-hoc coroutines — BattleArenaHud.PopCrown, VillageHudController.
//     FadeInHud), so the tween lives here. KIT-PROMOTION CANDIDATE: lift
//     RevealRoutine into ElarionUiKit once a second screen needs it.
//   • MVVM strict: this view binds the EndStateVM and reads NO game state.
//   • Never pauses time — the hero-death variant narrates HeroHealth's respawn
//     coroutine, which runs on scaled time.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.UI
{
    /// <summary>The shared end-state screen. Build one via <see cref="Show"/>.</summary>
    public sealed class EndStateView : MonoBehaviour
    {
        private static EndStateView _open;

        private EndStateVM _vm;
        private bool _fired;                      // primary-action latch (fires exactly once)
        private readonly List<Reveal> _reveals = new List<Reveal>();

        private struct Reveal
        {
            public CanvasGroup Group;
            public RectTransform Rect;
            public float Delay;
            public float FromScale;
        }

        // ── entry point ───────────────────────────────────────────────────────

        /// <summary>Show the end-state screen for <paramref name="vm"/> (replaces any open one).</summary>
        public static EndStateView Show(EndStateVM vm)
        {
            if (vm == null) return null;
            if (_open != null) { Destroy(_open.gameObject); _open = null; }

            // REAL EventSystem buttons (audit §2e: GameOverScreen's manual Input hit-test
            // existed because builds lacked an EventSystem — ensure one, don't hand-roll).
            EnsureEventSystem();

            GameObject canvas;
            ElarionUiKit.PanelChrome chrome;

            if (vm.Compact)
            {
                // Wave-results banner: small top-of-screen panel, NO scrim/backdrop, non-blocking.
                canvas = ElarionUiKit.BuildModalCanvas("EndState", 31000);
                var c = canvas.GetComponent<Canvas>();
                if (c != null) c.overrideSorting = true;
                chrome = ElarionUiKit.BuildObsidianPanel(canvas.transform, vm.Title,
                    new Vector2(0.15f, 0.64f), new Vector2(0.85f, 0.86f),
                    onClose: null, withBackdrop: false, frameName: RpgUiCatalog.FrameCore,
                    medallionIcon: "crest");   // explicit: the socket seats the crest family, never blank
            }
            else
            {
                // Full end-state modal, sized to the VM's content (no cavernous empty space).
                float half = PanelHalfHeight(vm);
                var modal = ElarionUiKit.BuildObsidianModal("EndState", vm.Title,
                    new Vector2(0.08f, 0.53f - half), new Vector2(0.92f, 0.53f + half),
                    onClose: null,   // scrim stays a pure raycast-blocker — no second way out
                    frameName: RpgUiCatalog.FrameCore,
                    medallionIcon: "crest");   // explicit: the socket seats the crest family, never blank
                canvas = modal.canvas;
                chrome = modal.chrome;
            }

            // Owner button law: an end-state has exactly ONE way out (the primary button).
            // Hide the factory's shared Close chip. KIT CHANGE REPORTED: BuildObsidianPanel
            // withClose:false would make this first-class instead of hide-after-build.
            if (chrome.close != null) chrome.close.gameObject.SetActive(false);

            var view = canvas.AddComponent<EndStateView>();
            view.Bind(vm, chrome);
            _open = view;
            // P23 (HUD_OBSIDIAN A4.6): the end-state is the DECISION NODE — while it is
            // up the posture is hostile(postbattle) and the HUD kit stands down.
            DeNelle.Core.HudModel.PostureSignals.SetEndState(true);
            return view;
        }

        /// <summary>Content-sized panel half-height (fraction of screen) from the VM.</summary>
        private static float PanelHalfHeight(EndStateVM vm)
        {
            float units = 0.6f;                            // header/footer breathing room
            if (vm.Emblem != null) units += 2.4f;
            if (!string.IsNullOrEmpty(vm.Subtitle)) units += 1.1f;
            if (vm.Stars >= 0) units += 1.0f;
            if (vm.TimeSeconds >= 0f) units += 0.8f;
            units += vm.Spoils.Count * 1.0f;
            return Mathf.Clamp(0.055f + units * 0.021f, 0.12f, 0.33f);
        }

        // ── binding ───────────────────────────────────────────────────────────

        private void Bind(EndStateVM vm, ElarionUiKit.PanelChrome chrome)
        {
            _vm = vm;

            // Drop-zones (sprite-first contract: layout is null on the procedural
            // fallback panel — mirror the default zone fractions on the content).
            RectTransform body   = chrome.layout != null ? chrome.layout.body
                                 : MakeZone(chrome.content.transform, "Zone_Body",   0.06f, 0.10f, 0.94f, 0.875f);
            RectTransform footer = chrome.layout != null && chrome.layout.footer != null ? chrome.layout.footer
                                 : MakeZone(chrome.content.transform, "Zone_Footer", 0.08f, 0.030f, 0.92f, 0.095f);

            BuildBody(vm, body);

            // ONE primary action (Continue / Rise again / ...) — lands LAST in the reveal.
            var btn = ElarionUiKit.Button(footer, vm.PrimaryLabel, ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.24f, 0.02f), new Vector2(0.76f, 0.98f), FirePrimary);
            Track(btn.gameObject, 0.25f + vm.Spoils.Count * 0.05f + 0.08f, 0.92f);

            // Smooth in: whole panel fades+scales, then the staggered content.
            var rootGroup = chrome.root.GetComponent<CanvasGroup>();
            if (rootGroup == null) rootGroup = chrome.root.AddComponent<CanvasGroup>();
            rootGroup.alpha = 0f;
            StartCoroutine(RevealRoutine(rootGroup, (RectTransform)chrome.root.transform, 0f, 0.25f, 0.94f));
            foreach (var r in _reveals)
                StartCoroutine(RevealRoutine(r.Group, r.Rect, r.Delay, 0.20f, r.FromScale));

            if (vm.AutoDismissSeconds > 0f)
                StartCoroutine(AutoDismissAfter(vm.AutoDismissSeconds));

            SceneManager.sceneLoaded += OnSceneLoaded;

            FlowTrace.Step("EndState",
                $"{vm.Kind} shown: spoils={vm.Spoils.Count} action={vm.PrimaryRoute}");
        }

        /// <summary>Stack the VM's content top-down inside the body zone, each band sized
        /// by weight so the panel is exactly as tall as its content demands.</summary>
        private void BuildBody(EndStateVM vm, RectTransform body)
        {
            // (weight, builder) bands, top to bottom.
            var bands = new List<(float w, Action<RectTransform> build)>();

            if (vm.Emblem != null)
                bands.Add((2.4f, host =>
                {
                    var go = new GameObject("Emblem", typeof(Image));
                    go.transform.SetParent(host, false);
                    var img = go.GetComponent<Image>();
                    img.sprite = vm.Emblem;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    var rt = img.rectTransform;
                    rt.anchorMin = new Vector2(0.38f, 0.04f);
                    rt.anchorMax = new Vector2(0.62f, 0.96f);
                    rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                    Track(go, 0.10f, 0.7f);   // emblem pops from smaller — the hero beat
                }));

            if (!string.IsNullOrEmpty(vm.Subtitle))
                bands.Add((1.1f, host =>
                {
                    var l = ElarionUiKit.Label(host, vm.Subtitle, 0f, 1f, ElarionUi.Parchment,
                        ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f);
                    l.raycastTarget = false;
                    Track(l.gameObject, 0.14f, 1f);
                }));

            if (vm.Stars >= 0)
                bands.Add((1.0f, host => BuildStarRow(host, vm.Stars)));

            if (vm.TimeSeconds >= 0f)
                bands.Add((0.8f, host =>
                {
                    var l = ElarionUiKit.Label(host, "Time  " + FormatTime(vm.TimeSeconds), 0f, 1f,
                        ElarionUi.Gilt, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center,
                        0.06f, 0.94f, bold: true);
                    l.raycastTarget = false;
                    Track(l.gameObject, 0.20f, 1f);
                }));

            for (int i = 0; i < vm.Spoils.Count; i++)
            {
                int idx = i;
                bands.Add((1.0f, host =>
                    Guard.Try("EndState", "spoils row " + idx,
                        () => BuildSpoilRow(host, vm.Spoils[idx], 0.25f + idx * 0.05f))));
            }

            // Lay the bands out by cumulative weight (small fixed gap between bands).
            float total = 0f;
            foreach (var b in bands) total += b.w;
            if (total <= 0f) return;
            const float gap = 0.012f;
            float cursor = 1f;
            foreach (var (w, build) in bands)
            {
                float h = (w / total) * (1f - gap * (bands.Count - 1));
                var host = MakeZone(body, "Band", 0f, cursor - h, 1f, cursor);
                cursor -= h + gap;
                build(host);
            }
        }

        /// <summary>One spoils row: kit slot plate + icon (null-safe) + label + amount.</summary>
        private void BuildSpoilRow(RectTransform host, SpoilRowVM row, float revealDelay)
        {
            if (row == null) return;
            var plate = ElarionUiKit.Slot(host, row.Rarity, new Vector2(0.06f, 0.04f),
                                          new Vector2(0.94f, 0.96f));
            if (row.Icon != null)
            {
                var go = new GameObject("Icon", typeof(Image));
                go.transform.SetParent(plate.transform, false);
                var img = go.GetComponent<Image>();
                img.sprite = row.Icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0.025f, 0.12f);
                rt.anchorMax = new Vector2(0.135f, 0.88f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            }
            var label = ElarionUiKit.Label(plate.transform, row.Label ?? "", 0f, 1f,
                ElarionUi.Parchment, ElarionUi.FontBody, TMPro.TextAlignmentOptions.MidlineLeft,
                row.Icon != null ? 0.17f : 0.06f, 0.68f);
            label.raycastTarget = false;
            var amount = ElarionUiKit.Label(plate.transform, row.Amount ?? "", 0f, 1f,
                ElarionUi.Gilt, ElarionUi.FontBody, TMPro.TextAlignmentOptions.MidlineRight,
                0.68f, 0.95f, bold: true);
            amount.raycastTarget = false;
            Track(plate, revealDelay, 0.96f);
        }

        /// <summary>Rating row: three procedural gold diamonds (filled/dim). Deliberately
        /// sprite-free — the TMP star glyphs tofu'd on the build font and the crown art
        /// carries a white fringe (owner F8), so the rating can never blank or fringe.</summary>
        private void BuildStarRow(RectTransform host, int stars)
        {
            var rowGo = new GameObject("Stars", typeof(RectTransform));
            rowGo.transform.SetParent(host, false);
            var rowRt = (RectTransform)rowGo.transform;
            rowRt.anchorMin = Vector2.zero; rowRt.anchorMax = Vector2.one;
            rowRt.offsetMin = Vector2.zero; rowRt.offsetMax = Vector2.zero;

            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Star" + i, typeof(Image));
                go.transform.SetParent(rowRt, false);
                var img = go.GetComponent<Image>();
                img.color = i < stars ? ElarionUiKit.ObsidianTrim : new Color(1f, 1f, 1f, 0.14f);
                img.raycastTarget = false;
                var rt = img.rectTransform;
                float cx = 0.5f + (i - 1) * 0.13f;
                rt.anchorMin = new Vector2(cx, 0.5f);
                rt.anchorMax = new Vector2(cx, 0.5f);
                rt.sizeDelta = new Vector2(26f, 26f);
                rt.localRotation = Quaternion.Euler(0f, 0f, 45f);   // diamond
            }
            Track(rowGo, 0.18f, 1f);
        }

        // ── actions / lifecycle ───────────────────────────────────────────────

        /// <summary>Fire the VM's primary action exactly once, then tear down.</summary>
        private void FirePrimary()
        {
            if (_fired) return;
            _fired = true;
            FlowTrace.Step("EndState", $"{_vm.Kind} primary fired: action={_vm.PrimaryRoute}");
            var act = _vm.Primary;
            _vm.Primary = null;
            act?.Invoke();
            Destroy(gameObject);
        }

        private IEnumerator AutoDismissAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, seconds));
            FirePrimary();
        }

        private void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            // The world moved on underneath us (e.g. raid-death evac loaded the hub):
            // tear down silently WITHOUT firing the primary route.
            _fired = true;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_open == this)
            {
                _open = null;
                // P23 (A4.6): the decision node closed — the posture arc moves on.
                DeNelle.Core.HudModel.PostureSignals.SetEndState(false);
            }
        }

        // ── smooth-in tween (KIT-PROMOTION CANDIDATE) ─────────────────────────

        /// <summary>Register a GameObject for the staggered reveal (alpha 0 until its turn).</summary>
        private void Track(GameObject go, float delay, float fromScale)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            _reveals.Add(new Reveal
            {
                Group = cg,
                Rect = go.transform as RectTransform,
                Delay = delay,
                FromScale = fromScale,
            });
        }

        /// <summary>Ease-out cubic fade+scale on UNSCALED time (plays through slow-mo /
        /// any pause). Mirrors the proven BattleArenaHud.PopCrown pattern, generalized.</summary>
        private static IEnumerator RevealRoutine(CanvasGroup cg, RectTransform rt,
                                                 float delay, float duration, float fromScale)
        {
            if (cg == null) yield break;
            if (rt != null && fromScale < 1f) rt.localScale = Vector3.one * fromScale;

            float t = 0f;
            while (t < delay)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - u, 3f);   // ease-out cubic
                if (cg == null) yield break;                 // torn down mid-tween
                cg.alpha = eased;
                if (rt != null && fromScale < 1f)
                    rt.localScale = Vector3.one * Mathf.Lerp(fromScale, 1f, eased);
                yield return null;
            }
            if (cg != null) cg.alpha = 1f;
            if (rt != null) rt.localScale = Vector3.one;
        }

        // ── tiny helpers ──────────────────────────────────────────────────────

        /// <summary>Kit buttons need an EventSystem; builds don't always have one
        /// (the reason GameOverScreen hand-rolled hit-testing). Same proven pattern
        /// as BattleArenaHud.EnsureEventSystem.</summary>
        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }

        private static RectTransform MakeZone(Transform parent, string name,
                                              float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
