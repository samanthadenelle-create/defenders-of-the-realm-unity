// =============================================================================
// EchoRosterView -- the "pet box": an informative Echo roster grid. DUMB SKIN.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Opened by the HUD "Pets" button (EchoUnlockFeedback.BuildPetBoxButton) via the
// static EchoRoster.Open(). A code-built Obsidian modal (ElarionUiKit -- NO UXML,
// PIPELINE_STATE S8) showing all 6 canonical spirits as portrait cards.
//
// MVVM (Silo F): the View reads NO service. Every card's identity / owned-locked
// state / portrait / per-echo lane-level-bonus readout, the header ETA + progress,
// the shared-perk line, and the first-run / empty framing all come from
// EchoRosterVM. OWNED cards tap through to the per-echo lane picker via the VM's
// OpenCard command (WO-738 reachability). Rebuilt fresh each open (VM re-created)
// so owned/locked + the ETA are always current. Colorblind-safe (portrait + TEXT
// status). Guard-wrapped card build (one bad card logs + skips). ASCII-only.
// =============================================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>Static opener for the Echo roster ("pet box"). Lazily creates the
    /// singleton view host; safe to call from any Village code (or the HUD Pets button).</summary>
    public static class EchoRoster
    {
        private static EchoRosterView s_view;

        /// <summary>Open (or refresh) the Echo roster grid.</summary>
        public static void Open()
        {
            if (s_view == null)
            {
                var go = new GameObject("EchoRoster");
                Object.DontDestroyOnLoad(go);
                s_view = go.AddComponent<EchoRosterView>();
            }
            s_view.OpenPanel();
        }
    }

    /// <summary>The Echo roster grid view. Rebuilt each open so owned/locked state +
    /// the next-echo ETA are always current. Binds <see cref="EchoRosterVM"/>.</summary>
    [DisallowMultipleComponent]
    public sealed class EchoRosterView : MonoBehaviour
    {
        private GameObject _modal;
        private bool _open;
        private EchoRosterVM _vm;

        private static readonly Color OwnedGlass  = new Color(0.09f, 0.10f, 0.13f, 0.95f);
        private static readonly Color LockedGlass = new Color(0.05f, 0.05f, 0.06f, 0.95f);
        private static readonly Color LifeGreen   = new Color(0.40f, 0.78f, 0.45f, 1f);

        /// <summary>Open + (re)build the grid to the current workforce state.</summary>
        public void OpenPanel()
        {
            using var _t = FlowTrace.Enter("Echo", "RosterOpen");
            EnsureEventSystem();

            // Rebuild fresh each open (cheap; keeps owned/locked + ETA current).
            if (_modal != null) { Destroy(_modal); _modal = null; }
            if (_vm != null) { _vm.Dispose(); _vm = null; }
            _vm = EchoRosterVM.CreateDefault(Close);

            bool ok = Guard.Try("Echo", "build echo roster", Build);
            if (!ok || _modal == null)
            {
                FlowTrace.Fail("Echo", "RosterOpen: roster failed to build -- not shown.");
                return;
            }
            _open = true;
            _modal.SetActive(true);
            FlowTrace.Step("Echo", $"Echo roster OPEN (owned {_vm.Owned}/{_vm.MaxEchoes}).");
        }

        private void Close()
        {
            _open = false;
            if (_modal != null) _modal.SetActive(false);
            FlowTrace.Step("Echo", "Echo roster CLOSED.");
        }

        // -- build --------------------------------------------------------------
        private void Build()
        {
            var built = ElarionUiKit.BuildObsidianModal(
                "EchoRoster", "ECHOES OF ELARION",
                new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.90f),
                onClose: Close, sortingOrder: 4650,
                frameName: RpgUiCatalog.FrameCore);
            _modal = built.canvas;
            var content = built.chrome.content.transform;

            int owned = _vm.Owned;
            double mult = _vm.GlobalHarvestMultiplier;
            int perEcho = _vm.PerEcho;
            bool firstRun = _vm.FirstRun;
            int wavesToNext = _vm.WavesToNext;

            // Header count + next-echo ETA (composed by the VM).
            ElarionUiKit.Label(content, _vm.RosterEtaText, 0.905f, 0.965f,
                ElarionUi.Gilt, ElarionUi.FontBody, TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);

            // Progress bar toward the next unlock.
            var bar = ElarionUiKit.Bar(content, ElarionUiKit.BarKind.Castle,
                new Vector2(0.12f, 0.862f), new Vector2(0.88f, 0.895f), withValue: false);
            if (bar.fill != null) { bar.fill.color = LifeGreen; bar.fill.fillAmount = _vm.NextEchoProgress; }

            // Honest shared perk (WO-709 quadratic): each Echo speeds ALL harvest. Hidden true-empty.
            if (_vm.HarvestPerkLine != null)
            {
                ElarionUiKit.Label(content, _vm.HarvestPerkLine,
                    0.805f, 0.855f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                    TextAlignmentOptions.Center, 0.04f, 0.96f, bold: false);
            }

            // TRUE-EMPTY branch (owned == 0): a single centered awaken hint, NOT a bare locked grid.
            if (_vm.Empty)
            {
                FlowTrace.Step("Echo", "Roster EMPTY (owned 0) -- showing centered awaken hint (no bare locked grid).");
                BuildEmptyHint(content, wavesToNext, perEcho);
                return;
            }

            // FIRST-RUN (owned == 1): inviting hint banner, then the grid below it.
            float gridTop = 0.775f;
            if (firstRun)
            {
                FlowTrace.Step("Echo", $"Roster FIRST-RUN (owned {owned}) -- leading with awaken hint above the grid.");
                BuildFirstRunHint(content, _vm.StarterName, wavesToNext);
                gridTop = 0.62f;   // slide the grid down into the free lower space to make room
            }

            // 3x2 grid of the 6 spirits (from the VM's card projection).
            Guard.TryEach("Echo", "build roster card", _vm.Cards, card =>
            {
                int index = card.Order - 1;                   // 0-based grid placement
                int col = index % 3;
                int row = index / 3;
                float x0 = 0.05f + col * 0.315f;
                float x1 = x0 + 0.29f;
                float y1 = gridTop - row * 0.275f;            // row0 top
                float y0 = y1 - 0.245f;
                BuildCard(content, card, new Vector2(x0, y0), new Vector2(x1, y1));
            });
        }

        // -- friendly empty / first-run hints -----------------------------------

        /// <summary>First-run banner (owned == 1): warm, inviting copy that frames the "next
        /// Echo" as a goal, sitting ABOVE the compressed grid. State carried in TEXT, never hue.</summary>
        private void BuildFirstRunHint(Transform content, string starterName, int wavesToNext)
        {
            var panel = ElarionUiKit.Panel(content,
                new Vector2(0.07f, 0.635f), new Vector2(0.93f, 0.80f), deep: false, innerRim: true);
            var t = panel.transform;

            ElarionUiKit.Label(t, starterName + " has answered your call.",
                0.58f, 0.94f, ElarionUi.Gilt, ElarionUi.FontBody,
                TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);

            string body = "It gathers for you now. Hold the line at Elarion -- clear "
                        + wavesToNext + " more wave" + (wavesToNext == 1 ? "" : "s")
                        + " and the Heart will awaken your next Echo to speed every harvest.";
            var b = ElarionUiKit.Label(t, body, 0.06f, 0.56f,
                ElarionUi.Parchment, ElarionUi.FontLabel,
                TextAlignmentOptions.Center, 0.06f, 0.94f, bold: false);
            b.textWrappingMode = TextWrappingModes.Normal;
        }

        /// <summary>True-empty hero hint (owned == 0): one centered, inviting card telling the
        /// player HOW to earn their first Echo -- shown INSTEAD of a bare locked grid.</summary>
        private void BuildEmptyHint(Transform content, int wavesToNext, int perEcho)
        {
            var panel = ElarionUiKit.Panel(content,
                new Vector2(0.14f, 0.30f), new Vector2(0.86f, 0.74f), deep: true, innerRim: true);
            var t = panel.transform;

            ElarionUiKit.Label(t, "The Tree sleeps.",
                0.74f, 0.93f, ElarionUi.Gilt, ElarionUi.FontHead,
                TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);

            string body = "Defend Elarion's waves and the Heart will awaken a spirit to gather for you. "
                        + "Clear " + wavesToNext + " wave" + (wavesToNext == 1 ? "" : "s")
                        + " to call your first Echo.";
            var b = ElarionUiKit.Label(t, body, 0.24f, 0.70f,
                ElarionUi.Parchment, ElarionUi.FontBody,
                TextAlignmentOptions.Center, 0.08f, 0.92f, bold: false);
            b.textWrappingMode = TextWrappingModes.Normal;

            var faint = ElarionUiKit.Label(t,
                "Six spirits wait beyond the veil -- one awakens for every " + perEcho + " waves you hold.",
                0.06f, 0.22f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                TextAlignmentOptions.Center, 0.06f, 0.94f, bold: false);
            faint.textWrappingMode = TextWrappingModes.Normal;

            FlowTrace.Step("Echo", $"Empty-hint built (call first Echo in {wavesToNext} waves; cadence {perEcho}).");
        }

        private void BuildCard(Transform content, EchoRosterCardVM card, Vector2 min, Vector2 max)
        {
            bool owned = card.Owned;

            // Card container (children use 0..1 of THIS rect).
            var cardGo = new GameObject($"EchoCard_{card.Order}", typeof(Image));
            cardGo.transform.SetParent(content, false);
            var crt = cardGo.GetComponent<RectTransform>();
            crt.anchorMin = min; crt.anchorMax = max;
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var cbg = cardGo.GetComponent<Image>();
            cbg.color = owned ? OwnedGlass : LockedGlass;
            // OWNED cards are TAPPABLE -> open the per-echo lane picker (via the VM command).
            // LOCKED cards stay inert (no raycast, no handler) so only earned spirits are pickable.
            if (owned)
            {
                cbg.raycastTarget = true;
                var tapBtn = cardGo.AddComponent<UnityEngine.UI.Button>();
                tapBtn.targetGraphic = cbg;
                int tapIndex = card.Index;   // capture for the closure
                tapBtn.onClick.AddListener(() =>
                {
                    FlowTrace.Step("Echo", $"Roster card tapped -> open picker for echo {tapIndex}.");
                    if (_vm != null) _vm.OpenCard(tapIndex);
                });
            }
            else
            {
                cbg.raycastTarget = false;
            }
            var cardT = cardGo.transform;

            // Portrait (top ~55%). Locked -> dark silhouette tint.
            var sprite = card.Portrait;
            if (sprite != null)
            {
                var pg = new GameObject("Portrait", typeof(Image));
                pg.transform.SetParent(cardT, false);
                var prt = pg.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0.10f, 0.44f);
                prt.anchorMax = new Vector2(0.90f, 0.95f);
                prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                var pimg = pg.GetComponent<Image>();
                pimg.sprite = sprite;
                pimg.preserveAspect = true;
                pimg.raycastTarget = false;
                pimg.color = owned ? Color.white : new Color(0.12f, 0.12f, 0.14f, 0.95f); // silhouette
            }
            else
            {
                ElarionUiKit.Label(cardT, card.PortraitFallback, 0.60f, 0.92f,
                    ElarionUi.ParchmentDim, ElarionUi.FontHead, TextAlignmentOptions.Center,
                    0.05f, 0.95f, bold: true);
            }

            // Name (owned = gilt; locked = dim).
            var nameLabel = ElarionUiKit.Label(cardT,
                card.DisplayName, 0.30f, 0.44f,
                owned ? ElarionUi.Gilt : ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                TextAlignmentOptions.Center, 0.03f, 0.97f, bold: true);
            ElarionUiKit.FitSingleLine(nameLabel);

            // Status line (owned -> element + lane/level/bonus; locked -> unlock wave) -- from the VM.
            var statusLabel = ElarionUiKit.Label(cardT, card.StatusText, 0.03f, 0.29f,
                owned ? LifeGreen : ElarionUi.Disabled, ElarionUi.FontLabel,
                TextAlignmentOptions.Center, 0.03f, 0.97f, bold: false);
            statusLabel.textWrappingMode = TextWrappingModes.Normal;
        }

        private void OnDestroy()
        {
            if (_vm != null) { _vm.Dispose(); _vm = null; }
            if (_modal != null) Destroy(_modal);
        }

        // -- helpers ------------------------------------------------------------
        private static void EnsureEventSystem()
        {
            // EventSystem.current is a plain static (NOT a scene query) — no banned FindAnyObjectByType.
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }
    }
}
