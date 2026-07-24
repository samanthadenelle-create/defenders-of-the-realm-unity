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
        private PanelHandle _panelHandle;   // HUD-1: modal arbiter registration (one Echo modal at a time)

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

            // HUD-1: register with the single-modal arbiter and announce the open. Opening the
            // roster CLOSES any other Echo modal (card/picker, harvest, unlock dialogue) that was
            // up -- no more stacked modals. Battle-lock (WO-437): a rejected open self-closes.
            if (_panelHandle == null)
                _panelHandle = PanelManager.Register("EchoRoster", Close, () => _open);
            if (!PanelManager.NotifyOpened(_panelHandle))
            {
                FlowTrace.Warn("Echo", "RosterOpen rejected by PanelManager (battle-lock) -- not shown.");
                return;
            }
            FlowTrace.Step("Echo", $"Echo roster OPEN (owned {_vm.Owned}/{_vm.MaxEchoes}).");
        }

        private void Close()
        {
            _open = false;
            if (_modal != null) _modal.SetActive(false);
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
            FlowTrace.Step("Echo", "Echo roster CLOSED.");
        }

        // -- build --------------------------------------------------------------
        private void Build()
        {
            // Owner F8 2026-07-24: EVERY overlap on the pet screen came from parenting into
            // chrome.content (full panel 0..1) so labels painted ON the frame title + Close.
            // Kit law: drop chrome-less content into layout.body (header/title/close reserved).
            var built = ElarionUiKit.BuildObsidianModal(
                "EchoRoster", "ECHOES OF ELARION",
                new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.94f),
                onClose: Close, sortingOrder: 31000,
                frameName: RpgUiCatalog.FrameCore);
            _modal = built.canvas;

            // Chrome title stays the product name ONLY (never "Echoes 1/6 - ..." which collided).
            if (built.chrome.title != null)
            {
                built.chrome.title.text = "ECHOES OF ELARION";
                ElarionUiKit.FitSingleLine(built.chrome.title);
            }

            // BODY well is the only safe parent (above Close band, below header plate).
            Transform body = built.chrome.layout != null && built.chrome.layout.body != null
                ? built.chrome.layout.body
                : built.chrome.content.transform;

            int owned = _vm.Owned;
            int perEcho = _vm.PerEcho;
            bool firstRun = _vm.FirstRun;
            int wavesToNext = _vm.WavesToNext;

            // ── Strict top-down bands inside BODY (0..1 of body, not panel) ──
            // y 0.92-1.00  ETA (Echoes N/M - next spirit)     single line
            // y 0.86-0.90  progress bar
            // y 0.78-0.85  harvest perk                        single line
            // y 0.58-0.76  first-run banner (only owned==1)    two non-overlap lines
            // y 0.02-0.56  3x2 card grid                       2 rows, footer clear
            var eta = ElarionUiKit.Label(body, _vm.RosterEtaText, 0.92f, 0.995f,
                ElarionUi.Gilt, ElarionUi.FontBody, TextAlignmentOptions.Center, 0.03f, 0.97f, bold: true);
            ElarionUiKit.FitSingleLine(eta);

            var bar = ElarionUiKit.Bar(body, ElarionUiKit.BarKind.Castle,
                new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.905f), withValue: false);
            if (bar.fill != null) { bar.fill.color = LifeGreen; bar.fill.fillAmount = _vm.NextEchoProgress; }

            if (_vm.HarvestPerkLine != null)
            {
                var perk = ElarionUiKit.Label(body, _vm.HarvestPerkLine,
                    0.785f, 0.85f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                    TextAlignmentOptions.Center, 0.03f, 0.97f, bold: false);
                ElarionUiKit.FitSingleLine(perk);
            }

            if (_vm.Empty)
            {
                FlowTrace.Step("Echo", "Roster EMPTY (owned 0) -- showing centered awaken hint (no bare locked grid).");
                BuildEmptyHint(body, wavesToNext, perEcho);
                return;
            }

            // Grid occupies the lower body. First-run steals a banner band above it.
            float gridTop = 0.76f;
            float gridBot = 0.02f;
            if (firstRun)
            {
                FlowTrace.Step("Echo", $"Roster FIRST-RUN (owned {owned}) -- awaken hint above grid (no text stack).");
                BuildFirstRunHint(body, _vm.StarterName, wavesToNext);
                gridTop = 0.56f;
            }

            // 3x2 grid -- equal cells inside [gridBot, gridTop], no overlap with Close (body already reserved).
            const int cols = 3, rows = 2;
            float padX = 0.02f, gapX = 0.02f, gapY = 0.02f;
            float cellW = (1f - 2f * padX - (cols - 1) * gapX) / cols;
            float cellH = (gridTop - gridBot - (rows - 1) * gapY) / rows;

            Guard.TryEach("Echo", "build roster card", _vm.Cards, card =>
            {
                int index = card.Order - 1;
                int col = index % cols;
                int row = index / cols;
                float x0 = padX + col * (cellW + gapX);
                float x1 = x0 + cellW;
                float y1 = gridTop - row * (cellH + gapY);
                float y0 = y1 - cellH;
                BuildCard(body, card, new Vector2(x0, y0), new Vector2(x1, y1));
            });
        }

        // -- friendly empty / first-run hints -----------------------------------

        /// <summary>First-run banner: TWO non-overlapping lines (owner F8: gold title was
        /// painted through the parchment body). Sits in body y 0.58-0.76 only.</summary>
        private void BuildFirstRunHint(Transform body, string starterName, int wavesToNext)
        {
            var panel = ElarionUiKit.Panel(body,
                new Vector2(0.04f, 0.58f), new Vector2(0.96f, 0.76f), deep: false, innerRim: true);
            var t = panel.transform;

            // Top half of banner -- title only, forced single line.
            var title = ElarionUiKit.Label(t, starterName + " has answered your call.",
                0.52f, 0.92f, ElarionUi.Gilt, ElarionUi.FontBody,
                TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            ElarionUiKit.FitSingleLine(title);

            // Bottom half -- short body, never crosses the title band.
            string bodyTxt = "It gathers for you now. Clear " + wavesToNext
                           + " more wave" + (wavesToNext == 1 ? "" : "s")
                           + " for your next Echo.";
            var b = ElarionUiKit.Label(t, bodyTxt, 0.08f, 0.46f,
                ElarionUi.Parchment, ElarionUi.FontLabel,
                TextAlignmentOptions.Center, 0.04f, 0.96f, bold: false);
            b.textWrappingMode = TextWrappingModes.Normal;
        }

        /// <summary>True-empty hero hint (owned == 0): one centered card in the body well.</summary>
        private void BuildEmptyHint(Transform body, int wavesToNext, int perEcho)
        {
            var panel = ElarionUiKit.Panel(body,
                new Vector2(0.10f, 0.18f), new Vector2(0.90f, 0.72f), deep: true, innerRim: true);
            var t = panel.transform;

            var head = ElarionUiKit.Label(t, "The Tree sleeps.",
                0.72f, 0.92f, ElarionUi.Gilt, ElarionUi.FontHead,
                TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
            ElarionUiKit.FitSingleLine(head);

            string bodyTxt = "Defend Elarion's waves and the Heart will awaken a spirit. "
                           + "Clear " + wavesToNext + " wave" + (wavesToNext == 1 ? "" : "s")
                           + " to call your first Echo.";
            var b = ElarionUiKit.Label(t, bodyTxt, 0.30f, 0.66f,
                ElarionUi.Parchment, ElarionUi.FontBody,
                TextAlignmentOptions.Center, 0.06f, 0.94f, bold: false);
            b.textWrappingMode = TextWrappingModes.Normal;

            var faint = ElarionUiKit.Label(t,
                "Six spirits wait -- one awakens every " + perEcho + " waves.",
                0.08f, 0.26f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                TextAlignmentOptions.Center, 0.05f, 0.94f, bold: false);
            ElarionUiKit.FitSingleLine(faint);

            FlowTrace.Step("Echo", $"Empty-hint built (call first Echo in {wavesToNext} waves; cadence {perEcho}).");
        }

        private void BuildCard(Transform body, EchoRosterCardVM card, Vector2 min, Vector2 max)
        {
            bool owned = card.Owned;

            var cardGo = new GameObject($"EchoCard_{card.Order}", typeof(Image));
            cardGo.transform.SetParent(body, false);
            var crt = cardGo.GetComponent<RectTransform>();
            crt.anchorMin = min; crt.anchorMax = max;
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var cbg = cardGo.GetComponent<Image>();
            cbg.color = owned ? OwnedGlass : LockedGlass;
            if (owned)
            {
                cbg.raycastTarget = true;
                var tapBtn = cardGo.AddComponent<UnityEngine.UI.Button>();
                tapBtn.targetGraphic = cbg;
                int tapIndex = card.Index;
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

            // Card-internal bands (no cross-stack):
            //   portrait  0.42-0.96
            //   name      0.22-0.40   single line
            //   status    0.04-0.20   single line (lane only -- not long Element lore)
            var sprite = card.Portrait;
            if (sprite != null)
            {
                var pg = new GameObject("Portrait", typeof(Image));
                pg.transform.SetParent(cardT, false);
                var prt = pg.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0.14f, 0.42f);
                prt.anchorMax = new Vector2(0.86f, 0.96f);
                prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                var pimg = pg.GetComponent<Image>();
                pimg.sprite = sprite;
                pimg.preserveAspect = true;
                pimg.raycastTarget = false;
                pimg.color = owned ? Color.white : new Color(0.12f, 0.12f, 0.14f, 0.95f);
            }
            else
            {
                ElarionUiKit.Label(cardT, card.PortraitFallback, 0.55f, 0.90f,
                    ElarionUi.ParchmentDim, ElarionUi.FontHead, TextAlignmentOptions.Center,
                    0.05f, 0.95f, bold: true);
            }

            var nameLabel = ElarionUiKit.Label(cardT,
                card.DisplayName, 0.22f, 0.40f,
                owned ? ElarionUi.Gilt : ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                TextAlignmentOptions.Center, 0.03f, 0.97f, bold: true);
            ElarionUiKit.FitSingleLine(nameLabel);

            var statusLabel = ElarionUiKit.Label(cardT, card.StatusText, 0.04f, 0.20f,
                owned ? LifeGreen : ElarionUi.Disabled, ElarionUi.FontLabel,
                TextAlignmentOptions.Center, 0.03f, 0.97f, bold: false);
            ElarionUiKit.FitSingleLine(statusLabel);
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
