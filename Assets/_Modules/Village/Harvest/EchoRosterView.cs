// =============================================================================
// EchoRosterView -- the "pet box": an informative Echo roster grid (owner
// 2026-07-17: "add the pet box somewhere... show status of current pets, how long
// until the next echo, and each echo's perk / what it does").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Opened by the HUD "Pets" button (EchoUnlockFeedback.BuildPetBoxButton) via the
// static EchoRoster.Open(). A code-built Obsidian modal (ElarionUiKit -- NO UXML,
// PIPELINE_STATE S8) showing all 6 canonical spirits (EchoRosterCatalog) as
// portrait cards:
//   - OWNED spirits (index < EchoService.EchoCount): portrait lit + name + element
//     + live specialization readout (EchoBonusCalculator.ReadoutFor -> lane + level +
//     current bonus %, e.g. "Harvest - Lv 3 - +65%"). TAPPABLE -> EchoCard.Open(index)
//     opens the per-echo lane picker (WO-738 reachability + agency).
//   - LOCKED spirits: dimmed silhouette + "Locked -- wave X" (the real cadence,
//     (order-1) * WavesPerEcho). Non-interactive (only earned spirits are pickable).
// Header shows "Echoes N/6", the "Next Echo in M waves" ETA + a progress bar (real
// EchoService fields, not faked), and the HONEST shared perk: each Echo multiplies
// the WHOLE workforce's harvest speed (WO-709 quadratic), now xN.
//
// Colorblind-safe (portrait + TEXT status, never hue alone). Guard-wrapped card
// build (one bad card logs + skips, never blanks the grid). ASCII-only.
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
    /// the next-echo ETA are always current.</summary>
    [DisallowMultipleComponent]
    public sealed class EchoRosterView : MonoBehaviour
    {
        private GameObject _modal;
        private bool _open;

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

            bool ok = Guard.Try("Echo", "build echo roster", Build);
            if (!ok || _modal == null)
            {
                FlowTrace.Fail("Echo", "RosterOpen: roster failed to build -- not shown.");
                return;
            }
            _open = true;
            _modal.SetActive(true);
            FlowTrace.Step("Echo", $"Echo roster OPEN (owned {OwnedCount()}/{MaxEchoes()}).");
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

            var svc = EchoService.Instance;
            int owned = OwnedCount();
            int max = MaxEchoes();
            int nextWaves = svc != null ? svc.WavesUntilNextEcho : 0;
            float progress = svc != null ? svc.NextEchoProgress : 0f;
            double mult = svc != null ? svc.GlobalHarvestMultiplier : 1.0;
            int perEcho = svc != null ? Mathf.Max(1, svc.WavesPerEcho) : 5;

            // Empty or just-the-starter -> lead FRIENDLY (never a cold/blank/broken grid).
            bool firstRun = owned <= 1;
            // The cadence the invite copy names: the real "waves until next", but never 0
            // (fall back to the per-echo cadence when the service is absent so the invitation
            // always shows a real, honest number -- read from EchoService, not hardcoded).
            int wavesToNext = nextWaves > 0 ? nextWaves : perEcho;

            // Header count + next-echo ETA. First-run reads as an inviting GOAL, not a cold stat.
            string etaText;
            if (owned >= max)
                etaText = $"Echoes {owned}/{max}   -   Roster complete!";
            else if (firstRun)
                etaText = $"Echoes {owned}/{max}   -   {wavesToNext} more wave{(wavesToNext == 1 ? "" : "s")} to your next spirit";
            else
                etaText = $"Echoes {owned}/{max}   -   Next Echo in {nextWaves} wave{(nextWaves == 1 ? "" : "s")}";
            ElarionUiKit.Label(content, etaText, 0.905f, 0.965f,
                ElarionUi.Gilt, ElarionUi.FontBody, TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);

            // Progress bar toward the next unlock.
            var bar = ElarionUiKit.Bar(content, ElarionUiKit.BarKind.Castle,
                new Vector2(0.12f, 0.862f), new Vector2(0.88f, 0.895f), withValue: false);
            if (bar.fill != null) { bar.fill.color = LifeGreen; bar.fill.fillAmount = progress; }

            // Honest shared perk (WO-709 quadratic): each Echo speeds ALL harvest. Hidden in the
            // true-empty state (no live multiplier to boast yet -- the hero hint carries the promise).
            if (owned > 0)
            {
                ElarionUiKit.Label(content,
                    $"Each Echo speeds ALL harvest -- now x{mult:0.#} to every node's yield.",
                    0.805f, 0.855f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                    TextAlignmentOptions.Center, 0.04f, 0.96f, bold: false);
            }

            // TRUE-EMPTY branch (owned == 0). Defensive: EchoService.EchoCount floors at 1 TODAY so
            // this is unreachable now, but the owner may later let Echoes start at 0 -- when that
            // happens we must show a single centered, friendly awaken hint as the hero element, NOT
            // a bare grid of 6 locked cards that reads as broken.
            if (owned <= 0)
            {
                FlowTrace.Step("Echo", "Roster EMPTY (owned 0) -- showing centered awaken hint (no bare locked grid).");
                BuildEmptyHint(content, wavesToNext, perEcho);
                return;
            }

            // FIRST-RUN (owned == 1): lead with an inviting hint banner, then the grid below it.
            float gridTop = 0.775f;
            if (firstRun)
            {
                var starter = EchoRosterCatalog.ByIndex(owned - 1);
                string starterName = starter != null ? starter.DisplayName : "Your first spirit";
                FlowTrace.Step("Echo", $"Roster FIRST-RUN (owned {owned}) -- leading with awaken hint above the grid.");
                BuildFirstRunHint(content, starterName, wavesToNext);
                gridTop = 0.62f;   // slide the grid down into the free lower space to make room
            }

            // 3x2 grid of the 6 spirits (kept intact for when Echoes ARE owned).
            var roster = EchoRosterCatalog.All;
            Guard.TryEach("Echo", "build roster card", roster, entry =>
            {
                int index = entry.Order - 1;                 // 0-based
                int col = index % 3;
                int row = index / 3;
                float x0 = 0.05f + col * 0.315f;
                float x1 = x0 + 0.29f;
                float y1 = gridTop - row * 0.275f;            // row0 top
                float y0 = y1 - 0.245f;
                bool isOwned = index < owned;
                BuildCard(content, entry, index, isOwned, perEcho,
                          new Vector2(x0, y0), new Vector2(x1, y1));
            });
        }

        // -- friendly empty / first-run hints -----------------------------------

        /// <summary>First-run banner (owned == 1): warm, inviting copy that frames the "next
        /// Echo" as a goal, sitting ABOVE the compressed grid. State carried in TEXT, never hue
        /// (colorblind-safe). Cadence number is read live, never hardcoded.</summary>
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
        /// player HOW to earn their first Echo -- shown INSTEAD of a bare locked grid so the panel
        /// never reads as broken. Cadence numbers read live from EchoService, never hardcoded.</summary>
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

        private void BuildCard(Transform content, EchoRosterEntry entry, int index,
            bool owned, int wavesPerEcho, Vector2 min, Vector2 max)
        {
            // Card container (children use 0..1 of THIS rect).
            var cardGo = new GameObject($"EchoCard_{entry.Order}", typeof(Image));
            cardGo.transform.SetParent(content, false);
            var crt = cardGo.GetComponent<RectTransform>();
            crt.anchorMin = min; crt.anchorMax = max;
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var cbg = cardGo.GetComponent<Image>();
            cbg.color = owned ? OwnedGlass : LockedGlass;
            // OWNED cards are TAPPABLE -> open the per-echo lane picker for this index
            // (WO-738 reachability: the roster grid is now the picker's entry point).
            // LOCKED cards stay inert (no raycast, no handler) so only earned spirits are pickable.
            if (owned)
            {
                cbg.raycastTarget = true;
                var tapBtn = cardGo.AddComponent<UnityEngine.UI.Button>();
                tapBtn.targetGraphic = cbg;
                int tapIndex = index;   // capture for the closure
                tapBtn.onClick.AddListener(() =>
                {
                    FlowTrace.Step("Echo", $"Roster card tapped -> open picker for echo {tapIndex}.");
                    EchoCard.Open(tapIndex);
                });
            }
            else
            {
                cbg.raycastTarget = false;
            }
            var card = cardGo.transform;

            // Portrait (top ~55%). Locked -> dark silhouette tint.
            var sprite = EchoRosterCatalog.LoadPortrait(entry.PortraitName);
            if (sprite != null)
            {
                var pg = new GameObject("Portrait", typeof(Image));
                pg.transform.SetParent(card, false);
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
                ElarionUiKit.Label(card, owned ? entry.Element : "?", 0.60f, 0.92f,
                    ElarionUi.ParchmentDim, ElarionUi.FontHead, TextAlignmentOptions.Center,
                    0.05f, 0.95f, bold: true);
            }

            // Name (owned = gilt; locked = dim + "???" to preserve the reveal).
            var nameLabel = ElarionUiKit.Label(card,
                owned ? entry.DisplayName : "Locked Echo", 0.30f, 0.44f,
                owned ? ElarionUi.Gilt : ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                TextAlignmentOptions.Center, 0.03f, 0.97f, bold: true);
            ElarionUiKit.FitSingleLine(nameLabel);

            // Status line: owned -> element + lane/level/bonus readout; locked -> unlock wave.
            // Colorblind-safe: the lane, level, and bonus are carried in TEXT, never hue alone.
            string status;
            if (owned)
            {
                var ro = EchoBonusCalculator.ReadoutFor(index);
                string line2;
                if (ro.Lane == LaneType.Idle)
                {
                    line2 = "Idle -- tap to assign";
                }
                else
                {
                    string laneLabel = EchoAssignments.LabelFor(EchoAssignments.LaneOf(index));
                    line2 = laneLabel + " - Lv " + ro.Level + " - +" + Mathf.RoundToInt(ro.BonusPct) + "%";
                    if (ro.PreferredMatch) line2 += " (best)";
                }
                status = entry.Element + "\n" + line2;
            }
            else
            {
                int unlockWave = index * Mathf.Max(1, wavesPerEcho);   // order K (1-based, index K-1) at (K-1)*per
                status = "Locked\nUnlocks at wave " + unlockWave;
            }
            var statusLabel = ElarionUiKit.Label(card, status, 0.03f, 0.29f,
                owned ? LifeGreen : ElarionUi.Disabled, ElarionUi.FontLabel,
                TextAlignmentOptions.Center, 0.03f, 0.97f, bold: false);
            statusLabel.textWrappingMode = TextWrappingModes.Normal;
        }

        private void OnDestroy()
        {
            if (_modal != null) Destroy(_modal);
        }

        // -- helpers ------------------------------------------------------------
        private static int OwnedCount()
        {
            var svc = EchoService.Instance;
            // Clamp the LOW bound to 0 (not 1) so a future 0-owned state (the owner may later let
            // Echoes start at 0) flows into the friendly empty hint instead of a bare grid.
            // EchoService.EchoCount floors at 1 today, so this is byte-identical now -- purely
            // defensive per the empty-state contract. Null service still defaults to 1 (show starter).
            return svc != null ? Mathf.Clamp(svc.EchoCount, 0, MaxEchoes()) : 1;
        }

        private static int MaxEchoes()
        {
            var svc = EchoService.Instance;
            return svc != null ? svc.MaxEchoes : EchoRosterCatalog.Count;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                DontDestroyOnLoad(es);
            }
        }
    }
}
