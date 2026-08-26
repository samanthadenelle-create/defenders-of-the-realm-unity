// =============================================================================
// RewardCelebration / RewardFlightLayer — the acknowledgement that CANNOT be
// occluded by a modal (WO-1225).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// THE DEFECT THIS REPLACES (WO-1225, owner felt-test 2026-08-26 12:14):
//   WO-1213 shipped a toast acknowledging the daily-chest grant. It WORKED — the
//   device log proves the grant landed AND that ElarionUiKit.ShowToast was called
//   with the right sentence, three milliseconds before EchoUnlockDialogue opened.
//   The owner still saw nothing, because the kit toast renders at sortingOrder 720
//   and every modal in this project is built at 31000-32000 behind an alpha-0.85
//   full-screen Scrim (ElarionUiKit.Scrim). A toast rendered UNDER a modal is
//   still a silent grant — and it is WORSE than no trace at all, because the log
//   says success and steers the next reader away from the broken thing.
//
// THE OWNER'S RULING (2026-08-26), verbatim:
//   "can it show streamers and +1000 showing to gold? counting up animation?"
//
//   So the acknowledgement moves OFF the toast layer entirely. A "+1,000 Gold"
//   headline FLIES to the gold chip, a readout at the chip COUNTS UP to the new
//   balance, and a streamer burst marks the moment.
//
// ⭐ WHY THIS IS THE FIX AND NOT A SORTING-ORDER RACE
//   Winning a z-fight is a fix for ONE modal. This layer is a different surface
//   with a different job: it is decoration anchored to a persistent HUD chip, it
//   never blocks input (no GraphicRaycaster, no CanvasGroup interaction), and its
//   canvas sits ABOVE the whole modal band by construction (see SortingOrder).
//   Nothing in the game is authored above it, so a modal opening BESIDE the chip
//   cannot bury the acknowledgement.
//
// ⛔ POOLING IS PROJECT LAW (ARCHITECTURE_PRINCIPLES §2b.1/§2b.2 — the two-VFX-
//   stack scar). Every body here is built ONCE in Awake and cycled with
//   SetActive: HeadlinePoolSize flight labels, ONE readout (there is one gold
//   chip), StreamerPoolSize ribbons. A claim allocates nothing but the two label
//   strings. Same shape as CombatTextLayer (Core/UI/CombatTextLayer.cs), which is
//   this file's direct structural precedent.
//
// ⛔ THE COUNT-UP MUST NOT LIE. This layer is a DUMB RENDERER: it is handed a
//   `from` and a `to` and animates between them. It never reads a wallet, never
//   derives a delta and never infers an amount. The caller (HudKitController)
//   supplies the MEASURED pre- and post-grant balances off the economy model
//   push, so a grant that was clamped, refused or short-credited renders the
//   number that was actually banked. See HudKitController.NoteGoldGain.
//
// WHY NOT DOTween: docs/reference/DOTWEEN_SME.md §0 — DOTween ships as a classic
//   Assets/Plugins drop with `createASMDEF = 0`, so its types live in
//   Assembly-CSharp and DeNelle.Core (an asmdef assembly, references UniTask /
//   TextMeshPro / Addressables only) CANNOT reference it. Zero files under
//   Assets/_Modules/ `using DG.Tweening` for exactly this reason. The in-assembly
//   tween seams are UiKitTween (keyed value tweens, used by CurrencyChip's
//   count-tween) and the Update-driven animation CombatTextLayer uses for pooled
//   bodies. A pooled body is re-leased while a tween could still be live, so the
//   bodies here animate in Update from cached state — the CombatTextLayer idiom —
//   and only the CHIP's own count-tween (which owns a stable handle) uses
//   UiKitTween.
//
// COLOURBLIND LAW (CLAUDE.md §7): the owner is red/green colourblind. Every
//   surface here carries WORDS AND NUMERALS ("+1,000 Gold", "Gold 12,345"). The
//   tints and the streamers are a redundant decorative channel only — strip all
//   colour and the acknowledgement still reads. ASCII-only strings.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// The RAISE seam: a marquee grant asks for an acknowledgement, and whoever owns the
    /// resource chip renders it. Village raises (DailyChestController); DeNelle.HUD listens
    /// (HudKitController) — neither assembly references the other, so this Core static is the
    /// only legal meeting point.
    ///
    /// This carries NO amount that anyone can see. <see cref="Request.RequestedAmount"/> is
    /// the amount that was ASKED FOR and exists purely so the listener can compare it against
    /// the MEASURED wallet delta and warn on a shortfall — it is never rendered. That
    /// separation is the whole point: WO-1225 forbids animating to a number that was never
    /// banked.
    /// </summary>
    public static class RewardCelebration
    {
        /// <summary>One raised acknowledgement request.</summary>
        public struct Request
        {
            /// <summary>Resource word, e.g. "Gold". Rendered.</summary>
            public string Resource;
            /// <summary>What the grant path ASKED for. NEVER rendered — shortfall oracle only.</summary>
            public long RequestedAmount;
            /// <summary>Greppable origin ("daily.chest.rewarded_double").</summary>
            public string Reason;
            /// <summary>Screen point the headline flies FROM. Ignored unless <see cref="HasOrigin"/>.</summary>
            public Vector2 OriginScreen;
            public bool HasOrigin;
        }

        /// <summary>Raised requests, in order. Subscribers render; a request with no subscriber
        /// is a SILENT GRANT and is traced as one.</summary>
        public static event Action<Request> Requested;

        /// <summary>Oracle: how many requests have been raised this session.</summary>
        public static int RaiseCount { get; private set; }

        /// <summary>Oracle: the most recent request (EditMode suites read this).</summary>
        public static Request LastRequest { get; private set; }

        /// <summary>Oracle: how many listeners the last raise reached (0 = nothing rendered it).</summary>
        public static int LastListenerCount { get; private set; }

        /// <summary>Raise an acknowledgement for a grant that already landed, flying from the
        /// centre of the screen (where a claim modal lives).</summary>
        public static void Raise(string resource, long requestedAmount, string reason)
        {
            RaiseInternal(new Request
            {
                Resource = string.IsNullOrEmpty(resource) ? "Gold" : resource,
                RequestedAmount = requestedAmount,
                Reason = reason ?? "unknown",
                HasOrigin = false,
            });
        }

        /// <summary>As <see cref="Raise"/> but the headline flies from an explicit screen point.</summary>
        public static void RaiseFrom(string resource, long requestedAmount, string reason, Vector2 originScreen)
        {
            RaiseInternal(new Request
            {
                Resource = string.IsNullOrEmpty(resource) ? "Gold" : resource,
                RequestedAmount = requestedAmount,
                Reason = reason ?? "unknown",
                OriginScreen = originScreen,
                HasOrigin = true,
            });
        }

        private static void RaiseInternal(Request r)
        {
            RaiseCount++;
            LastRequest = r;

            var handler = Requested;
            LastListenerCount = handler == null ? 0 : handler.GetInvocationList().Length;

            // §12 permanent trace. The listener count is the load-bearing half: WO-1213's
            // failure was a call that provably fired into a surface nobody could see, so this
            // line records whether ANY renderer was attached at all.
            FlowTrace.Step("Reward",
                $"celebration raised resource={r.Resource} requested={r.RequestedAmount} " +
                $"reason={r.Reason} listeners={LastListenerCount}");

            if (handler == null)
            {
                // No HUD is bound (a raid/dungeon scene, a teardown frame, a headless run).
                // NOT silent: the grant still landed, but nothing on screen will say so.
                FlowTrace.Warn("Reward",
                    $"celebration for {r.Resource} +{r.RequestedAmount} ({r.Reason}) has NO LISTENER - " +
                    "the grant is real but NOTHING will acknowledge it on screen. If this fires in town, " +
                    "HudKitController never bound RewardCelebration.Requested.");
                return;
            }

            // Guarded: one throwing listener must never take down the claim path that raised it.
            Guard.Try("Reward", "dispatch celebration", () => handler(r));
        }
    }

    /// <summary>
    /// The pooled, always-on-top acknowledgement layer (see the file header for the contract).
    /// Lazily self-builds; no prefab, no scene wiring, no PanelSettings.
    /// </summary>
    public sealed class RewardFlightLayer : MonoBehaviour
    {
        // ── The invariant this whole file exists for ──────────────────────────
        //
        // ⛔ NEVER LOWER THIS. Modals in this project are authored across 31000-32000
        // (ElarionUiKit.Modal default 31000, ElarionUiKit.Confirm 32000, EchoUnlockDialogue
        // 31020, PauseController 31500, DungeonExitInteractable 34000 - the outlier), each
        // behind a full-screen alpha-0.85 Scrim. The kit toast sits at 720, which is how a
        // proven-correct grant went unseen. This layer is DECORATION and eats no input, so
        // sitting above the modal band costs nothing and is the only value at which the
        // acknowledgement is guaranteed legible while a modal is open.
        public const int SortingOrder = 34500;

        /// <summary>Modal band ceiling this layer must clear. Pinned so a regression can assert
        /// the relationship rather than re-deriving it from a scan.</summary>
        public const int ModalBandCeiling = 34000;

        private const int   HeadlinePoolSize = 3;      // hard cap; oldest recycled
        private const int   StreamerPoolSize = 24;     // one burst, reused forever
        private const float FlightSeconds    = 0.85f;  // headline: origin -> chip
        private const float CountSeconds     = 1.15f;  // readout: from -> to
        private const float HoldSeconds      = 1.10f;  // readout: settled, fully legible
        private const float FadeSeconds      = 0.55f;  // readout: out
        private const float StreamerSeconds  = 1.60f;  // ribbon life
        private const float HeadlineFontSize = 54f;
        private const float ReadoutFontSize  = 46f;

        // Decoration only (colourblind law: the words carry the meaning).
        private static readonly Color GoldTint    = new Color(1.00f, 0.86f, 0.36f, 1f);
        private static readonly Color ReadoutTint = new Color(1.00f, 0.95f, 0.80f, 1f);
        private static readonly Color[] StreamerTints =
        {
            new Color(1.00f, 0.86f, 0.36f, 1f),   // gilt
            new Color(0.98f, 0.98f, 0.94f, 1f),   // parchment white
            new Color(0.72f, 0.82f, 0.98f, 1f),   // cool steel
            new Color(0.86f, 0.72f, 0.98f, 1f),   // violet
        };

        // ── Oracles (EditMode / AutoPilot suites read these) ──────────────────

        /// <summary>How many flights have been shown this session.</summary>
        public static int FlightCount { get; private set; }
        /// <summary>The most recent headline string ("+1,000 Gold").</summary>
        public static string LastHeadline { get; private set; }
        /// <summary>The balance the last readout counted FROM (pre-grant, measured).</summary>
        public static long LastFromBalance { get; private set; }
        /// <summary>The balance the last readout counted TO (post-grant, measured).</summary>
        public static long LastToBalance { get; private set; }

        // ── Pooled bodies ─────────────────────────────────────────────────────

        private sealed class Headline
        {
            public GameObject go;
            public RectTransform rect;
            public TextMeshProUGUI label;
            public bool live;
            public float age;
            public Vector2 from, to;
        }

        private sealed class Streamer
        {
            public GameObject go;
            public RectTransform rect;
            public Image image;
            public bool live;
            public float age;
            public Vector2 origin, velocity;
            public float spin;
            public Color tint;
        }

        private static RewardFlightLayer _instance;

        private readonly Headline[] _headlines = new Headline[HeadlinePoolSize];
        private readonly Streamer[] _streamers = new Streamer[StreamerPoolSize];

        private RectTransform _canvasRect;
        private Canvas _canvas;

        // The single readout ("Gold 12,345") — one gold chip, one mirror.
        private GameObject _readoutGo;
        private RectTransform _readoutRect;
        private TextMeshProUGUI _readoutLabel;
        private bool _readoutLive;
        private float _readoutAge;
        private long _readoutFrom, _readoutTo, _readoutShown;
        private string _readoutWord = "Gold";

        /// <summary>The lazily-built singleton layer (null only outside play or when construction failed).</summary>
        public static RewardFlightLayer Instance
        {
            get
            {
                if (_instance == null && Application.isPlaying)
                {
                    _instance = Guard.Try("Reward", "build RewardFlightLayer", () =>
                    {
                        var go = new GameObject("RewardFlightLayer");
                        DontDestroyOnLoad(go);
                        return go.AddComponent<RewardFlightLayer>();
                    }, null);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // NO GraphicRaycaster — purely decorative; it must never eat a tap, least of all
            // the tap on the modal it is drawn over.
            _canvasRect = (RectTransform)transform;

            for (int i = 0; i < HeadlinePoolSize; i++)
            {
                var h = new Headline();
                h.go = new GameObject("RewardHeadline" + i, typeof(RectTransform), typeof(TextMeshProUGUI));
                h.go.transform.SetParent(transform, false);
                h.rect = (RectTransform)h.go.transform;
                h.rect.sizeDelta = new Vector2(560f, 96f);
                h.label = h.go.GetComponent<TextMeshProUGUI>();
                StyleLabel(h.label, HeadlineFontSize, GoldTint);
                h.go.SetActive(false);
                _headlines[i] = h;
            }

            _readoutGo = new GameObject("RewardReadout", typeof(RectTransform), typeof(TextMeshProUGUI));
            _readoutGo.transform.SetParent(transform, false);
            _readoutRect = (RectTransform)_readoutGo.transform;
            _readoutRect.sizeDelta = new Vector2(520f, 84f);
            _readoutLabel = _readoutGo.GetComponent<TextMeshProUGUI>();
            StyleLabel(_readoutLabel, ReadoutFontSize, ReadoutTint);
            _readoutGo.SetActive(false);

            for (int i = 0; i < StreamerPoolSize; i++)
            {
                var s = new Streamer();
                s.go = new GameObject("RewardStreamer" + i, typeof(RectTransform), typeof(Image));
                s.go.transform.SetParent(transform, false);
                s.rect = (RectTransform)s.go.transform;
                s.rect.sizeDelta = new Vector2(11f, 34f);
                s.image = s.go.GetComponent<Image>();
                s.image.raycastTarget = false;
                s.go.SetActive(false);
                _streamers[i] = s;
            }

            FlowTrace.Step("Reward",
                $"RewardFlightLayer built sortingOrder={SortingOrder} (modal band ceiling {ModalBandCeiling}) " +
                $"headlines={HeadlinePoolSize} streamers={StreamerPoolSize} raycaster=none");
        }

        private static void StyleLabel(TextMeshProUGUI label, float size, Color tint)
        {
            ElarionUiKit.EnsureFont(label, ElarionUiKit.FontRole.Stamp);   // Acme + fallback chain
            label.fontSize = size;
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.outlineColor = new Color32(8, 8, 12, 235);   // legible over ANY modal art
            label.outlineWidth = 0.24f;
            label.raycastTarget = false;
            label.color = tint;
        }

        // =====================================================================
        //  THE ONE ENTRY POINT
        // =====================================================================

        /// <summary>
        /// Show the acknowledgement: <paramref name="headline"/> flies from
        /// <paramref name="originScreen"/> to <paramref name="target"/>, then the readout counts
        /// <paramref name="fromBalance"/> -> <paramref name="toBalance"/> at the target while a
        /// streamer burst fires.
        ///
        /// ⛔ Both balances are the CALLER'S MEASURED values. This method does no arithmetic on
        /// them beyond formatting, and deliberately has no access to any wallet — it cannot
        /// invent a number, which is the only structural guarantee that the count-up is honest.
        /// </summary>
        /// <param name="headline">e.g. "+1,000 Gold" — words and numerals, never colour alone.</param>
        /// <param name="resourceWord">e.g. "Gold" — prefixes the readout so it is never a naked number.</param>
        /// <param name="target">The persistent HUD chip to fly to. Null -> upper-right fallback + Warn.</param>
        public void Fly(string headline, string resourceWord, Vector2 originScreen,
                        RectTransform target, long fromBalance, long toBalance)
        {
            if (string.IsNullOrEmpty(headline)) return;

            Vector2 targetScreen;
            if (target != null)
            {
                var cvs = target.GetComponentInParent<Canvas>();
                Camera cam = (cvs != null && cvs.renderMode != RenderMode.ScreenSpaceOverlay)
                    ? cvs.worldCamera : null;
                targetScreen = RectTransformUtility.WorldToScreenPoint(cam, target.position);
            }
            else
            {
                // The rail lives at the upper right. A missing chip is a real defect (the HUD
                // was not built, or the chip moved) — say so rather than silently dropping the
                // acknowledgement, which is the exact failure class this ticket is about.
                targetScreen = new Vector2(Screen.width * 0.82f, Screen.height * 0.88f);
                FlowTrace.Warn("Reward",
                    "Fly: no target chip rect - flying to the upper-right FALLBACK point. The " +
                    "acknowledgement still shows, but it is no longer anchored to the counter it " +
                    "is acknowledging.");
            }

            Vector2 fromLocal = ToLocal(originScreen);
            Vector2 toLocal   = ToLocal(targetScreen);

            // 1) Headline body — hard cap, oldest recycled (CombatTextLayer's §1.8 idiom).
            Headline take = null;
            float oldest = -1f;
            for (int i = 0; i < HeadlinePoolSize; i++)
            {
                var h = _headlines[i];
                if (!h.live) { take = h; break; }
                if (h.age > oldest) { oldest = h.age; take = h; }
            }
            if (take != null)
            {
                take.live = true;
                take.age = 0f;
                take.from = fromLocal;
                take.to = toLocal;
                take.label.text = headline;
                take.label.color = GoldTint;
                take.rect.anchoredPosition = fromLocal;
                take.rect.localScale = Vector3.one * 1.25f;
                take.go.SetActive(true);
            }

            // 2) Readout — armed now, revealed on landing (see Update).
            _readoutWord = string.IsNullOrEmpty(resourceWord) ? "Gold" : resourceWord;
            _readoutFrom = fromBalance;
            _readoutTo = toBalance;
            _readoutShown = long.MinValue;
            _readoutAge = 0f;
            _readoutLive = true;
            _readoutRect.anchoredPosition = toLocal + new Vector2(0f, -74f);   // just under the chip
            _readoutGo.SetActive(false);   // Update reveals it at touchdown

            // 3) Streamers — the burst originates at the chip, not at the claim, so the moment
            //    is marked where the number lands.
            Burst(toLocal);

            FlightCount++;
            LastHeadline = headline;
            LastFromBalance = fromBalance;
            LastToBalance = toBalance;

            // §12 permanent trace. Everything here is a MEASURED value handed in by the caller;
            // 'bodyLeased' records whether a pool body was actually available, so a capture can
            // separate "never asked" from "asked and nothing rendered".
            FlowTrace.Step("Reward",
                $"reward flight '{headline}' {_readoutWord} {fromBalance} -> {toBalance} " +
                $"origin=({originScreen.x:0},{originScreen.y:0}) target=({targetScreen.x:0},{targetScreen.y:0}) " +
                $"bodyLeased={(take != null)} sortingOrder={SortingOrder}");
        }

        private Vector2 ToLocal(Vector2 screenPoint)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPoint, null, out local);
            return local;
        }

        private void Burst(Vector2 originLocal)
        {
            for (int i = 0; i < StreamerPoolSize; i++)
            {
                var s = _streamers[i];
                s.live = true;
                s.age = 0f;
                s.origin = originLocal;
                // Fan upward and outward; deterministic-enough randomness, no allocation.
                float angle = UnityEngine.Random.Range(20f, 160f) * Mathf.Deg2Rad;
                float speed = UnityEngine.Random.Range(320f, 760f);
                s.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                s.spin = UnityEngine.Random.Range(-540f, 540f);
                s.tint = StreamerTints[i % StreamerTints.Length];
                s.image.color = s.tint;
                s.rect.anchoredPosition = originLocal;
                s.rect.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
                s.rect.localScale = Vector3.one;
                s.go.SetActive(true);
            }
        }

        // =====================================================================
        //  Animation — Update-driven from cached state (pooled-body idiom).
        //  Unscaled time throughout: a claim can land while the game is paused
        //  behind a modal, and an acknowledgement that freezes with timeScale is
        //  the same invisible grant by another route.
        // =====================================================================

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            AnimateHeadlines(dt);
            AnimateReadout(dt);
            AnimateStreamers(dt);
        }

        private void AnimateHeadlines(float dt)
        {
            for (int i = 0; i < HeadlinePoolSize; i++)
            {
                var h = _headlines[i];
                if (!h.live) continue;
                h.age += dt;
                float k = Mathf.Clamp01(h.age / FlightSeconds);
                float ease = 1f - (1f - k) * (1f - k) * (1f - k);     // cubic ease-out
                h.rect.anchoredPosition = Vector2.LerpUnclamped(h.from, h.to, ease);
                h.rect.localScale = Vector3.one * Mathf.Lerp(1.25f, 0.75f, ease);
                var c = h.label.color;
                c.a = k < 0.78f ? 1f : 1f - (k - 0.78f) / 0.22f;      // fade into the landing
                h.label.color = c;
                if (k >= 1f)
                {
                    h.live = false;
                    h.go.SetActive(false);
                }
            }
        }

        private void AnimateReadout(float dt)
        {
            if (!_readoutLive) return;
            _readoutAge += dt;

            // Reveal exactly at touchdown so the headline hands off to the counter.
            float t = _readoutAge - FlightSeconds;
            if (t < 0f) return;
            if (!_readoutGo.activeSelf) _readoutGo.SetActive(true);

            float k = Mathf.Clamp01(t / CountSeconds);
            float ease = 1f - (1f - k) * (1f - k);
            long shown = _readoutFrom + (long)Math.Round((_readoutTo - _readoutFrom) * (double)ease);
            if (shown != _readoutShown)
            {
                _readoutShown = shown;
                // Word + numeral: never a naked number, never colour alone. Grouped digits
                // (not the chip's CompactNumber) because a count-up through "1.2K" is
                // unreadable — the chip beneath keeps its compact resting form.
                _readoutLabel.text = _readoutWord + " " + shown.ToString("N0",
                    System.Globalization.CultureInfo.InvariantCulture);
            }

            float over = t - (CountSeconds + HoldSeconds);
            var c = _readoutLabel.color;
            if (over <= 0f)
            {
                c.a = 1f;
                // A small settle pop as the count lands, then still.
                float pop = k >= 1f ? 1f : 1f + 0.10f * Mathf.Sin(k * Mathf.PI);
                _readoutRect.localScale = Vector3.one * pop;
            }
            else if (over < FadeSeconds)
            {
                c.a = 1f - over / FadeSeconds;
            }
            else
            {
                c.a = 1f;
                _readoutLabel.color = c;
                _readoutRect.localScale = Vector3.one;
                _readoutGo.SetActive(false);
                _readoutLive = false;
                return;
            }
            _readoutLabel.color = c;
        }

        private void AnimateStreamers(float dt)
        {
            for (int i = 0; i < StreamerPoolSize; i++)
            {
                var s = _streamers[i];
                if (!s.live) continue;
                s.age += dt;
                float k = Mathf.Clamp01(s.age / StreamerSeconds);
                // Ballistic: launch, then fall. Reference px, so it reads the same at any aspect.
                float tt = s.age;
                Vector2 pos = s.origin + s.velocity * tt + new Vector2(0f, -900f * tt * tt * 0.5f);
                s.rect.anchoredPosition = pos;
                s.rect.localRotation = Quaternion.Euler(0f, 0f, s.spin * tt);
                var c = s.tint;
                c.a = k < 0.45f ? 1f : 1f - (k - 0.45f) / 0.55f;
                s.image.color = c;
                if (k >= 1f)
                {
                    s.live = false;
                    s.go.SetActive(false);
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
