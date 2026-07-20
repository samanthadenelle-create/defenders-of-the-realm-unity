// =============================================================================
// EchoUnlockDialogue -- the "Echoes of Elarion" portrait unlock card (owner
// felt-test 2026-07-17 + mockup Screenshot 2026-07-17 062124.png).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// When a new Echo is earned (EchoService.EchoUnlocked -> newCount), a spirit
// AWAKENS and speaks. This is the full portrait dialogue card the owner asked for,
// DATA-DRIVEN from EchoRosterCatalog.ByCount(newCount): whichever spirit unlocks,
// its portrait / name / element / flavor fill the card. It REPLACES the old plain
// EchoUnlockToast center banner (EchoUnlockFeedback still fires the SFX + the
// persistent "Echoes N/6" pip -- no double banner; the toast class is retired).
//
// LAYOUT (matches the mockup, clean not ornate -- reuses ElarionUiKit obsidian
// chrome, code-built uGUI, NO UXML per PIPELINE_STATE S8):
//   frame title "ECHOES OF ELARION"      (BuildObsidianModal header)
//   gold "Echo Leveled Up to N!" banner  (top strip)
//   LEFT : portrait (Sprite.Create) + essence subtitle ("Essence of a fallen keeper")
//   RIGHT: name ("Aldwin, the Ice Echo") + flavor line + 3 buttons:
//     "I accept your power" (primary, closes) / "Tell me more" (swaps flavor ->
//     extended lore) / "Dismiss" (closes). The shared obsidian Close is canon extra.
//
// Colorblind-safe: identity reads from PORTRAIT + TEXT, never hue alone. Portrait
// load + card build are Guard-wrapped (a missing image logs + shows a text
// fallback, never blanks). ASCII-only strings. FlowTrace on show.
// =============================================================================
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>Data-driven Echo-unlock portrait dialogue. Built on the unlocked
    /// spirit's <see cref="EchoRosterEntry"/>. One on screen at a time; self-destroys
    /// on any close/accept/dismiss.</summary>
    [DisallowMultipleComponent]
    public sealed class EchoUnlockDialogue : MonoBehaviour
    {
        private static EchoUnlockDialogue s_active;   // single instance

        /// <summary>TRUE while an unlock card is on screen. Read by EchoService.AnnounceFoundingEcho
        /// to confirm the founding card actually rendered before persisting its one-shot flag.</summary>
        public static bool IsShowing => s_active != null;

        private GameObject _canvas;
        private EchoRosterEntry _entry;
        private TextMeshProUGUI _flavorLabel;
        private Button _tellMoreBtn;
        private bool _showingLore;
        private PanelHandle _panelHandle;   // HUD-1: modal arbiter registration (one Echo modal at a time)
        private bool _open;

        /// <summary>Build + show the unlock card for the spirit earned at
        /// <paramref name="newCount"/>. Idempotent: replaces any card on screen. Returns TRUE
        /// when the card is on screen (used by the founding-echo teaching to persist its
        /// one-shot flag only after a confirmed render), FALSE on a null entry / build fault.</summary>
        public static bool Show(EchoRosterEntry entry, int newCount)
        {
            if (entry == null)
            {
                FlowTrace.Warn("Echo", "EchoUnlockDialogue.Show: null roster entry -- card skipped (SFX + pip still fire).");
                return false;
            }
            // HUD-1: retire any card already up THROUGH its own Close so the arbiter is
            // notified (NotifyClosed) before we replace it -- never a raw orphan Destroy.
            if (s_active != null) { s_active.Close(); s_active = null; }

            var host = new GameObject("EchoUnlockDialogue");
            var dlg = host.AddComponent<EchoUnlockDialogue>();
            s_active = dlg;

            // Whole card build guarded -- a construction fault logs + tears down, never
            // wedges a half-card over gameplay (Sec.12 no-silent-failure).
            bool ok = Guard.Try("Echo", "build echo unlock dialogue", () => dlg.Build(entry, newCount));
            if (!ok)
            {
                if (dlg != null) Destroy(dlg.gameObject);
                s_active = null;
                return false;
            }

            // HUD-1: register with the single-modal arbiter and announce the open. The unlock
            // card CLOSES any other Echo modal (roster/card/harvest) that was up -- one modal only.
            // Battle-lock (WO-437): a rejected open self-closes (SFX + pip still fire) -> return false
            // so the founding-echo teaching does not persist its one-shot on an un-rendered card.
            dlg._open = true;
            dlg._panelHandle = PanelManager.Register("EchoUnlockDialogue", dlg.Close, () => dlg._open);
            if (!PanelManager.NotifyOpened(dlg._panelHandle))
            {
                FlowTrace.Warn("Echo", "unlock dialogue rejected by PanelManager (battle-lock) -- not shown.");
                return false;
            }
            FlowTrace.Step("Echo", $"unlock dialogue shown id={entry.Id} count={newCount}");
            return true;
        }

        /// <summary>FTUE-09: the gold banner header. newCount==1 is the FOUNDING AWAKEN
        /// -- there is no prior Echo to "level up" from, so "Echo Leveled Up to 1!" is
        /// nonsense; render an AWAKEN header instead (mirrors EchoRosterView's awaken
        /// wording). n>=2 is a genuine level-up. Pure + public so the copy is headlessly
        /// assertable (EchoCardCopyRegression).</summary>
        public static string HeaderFor(int newCount)
        {
            return newCount <= 1 ? "An Echo Awakens" : $"Echo Leveled Up to {newCount}!";
        }

        private void Build(EchoRosterEntry entry, int newCount)
        {
            _entry = entry;
            EnsureEventSystem();

            // Shared obsidian chrome + scrim (tap-outside closes) + ONE canon Close.
            // Wide landscape card (portrait left, text right). Sits ABOVE both the roster
            // (31000) and the echo card/picker (31010) in the canon MODAL band.
            var built = ElarionUiKit.BuildObsidianModal(
                "EchoesOfElarion", "ECHOES OF ELARION",
                new Vector2(0.14f, 0.20f), new Vector2(0.86f, 0.82f),
                onClose: Close, sortingOrder: 31020,   // above roster+card so unlock card is topmost (was 4700: under HUD)
                frameName: RpgUiCatalog.FrameCore);
            _canvas = built.canvas;
            var content = built.chrome.content.transform;

            // -- "Echo Leveled Up to N!" banner (top strip: gold fill + ink text) ----
            var bannerGo = new GameObject("LevelBanner", typeof(Image));
            bannerGo.transform.SetParent(content, false);
            var brt = bannerGo.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.06f, 0.885f);
            brt.anchorMax = new Vector2(0.94f, 0.98f);
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            var bimg = bannerGo.GetComponent<Image>();
            bimg.color = ElarionUi.Gold;
            bimg.raycastTarget = false;
            ElarionUiKit.Label(content, HeaderFor(newCount), 0.885f, 0.98f,
                ElarionUi.Ink, ElarionUi.FontBody, TextAlignmentOptions.Center,
                0.06f, 0.94f, bold: true);

            // -- LEFT: portrait (Sprite.Create) + element subtitle -------------------
            var sprite = EchoRosterCatalog.LoadPortrait(entry.PortraitName);
            if (sprite != null)
            {
                var pg = new GameObject("EchoPortrait", typeof(Image));
                pg.transform.SetParent(content, false);
                var prt = pg.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0.05f, 0.30f);
                prt.anchorMax = new Vector2(0.40f, 0.85f);
                prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                var pimg = pg.GetComponent<Image>();
                pimg.sprite = sprite;
                pimg.preserveAspect = true;
                pimg.raycastTarget = false;
            }
            else
            {
                // Never blank: a text placeholder stands in for a missing portrait.
                ElarionUiKit.Label(content, "[ " + entry.Element + " ]", 0.50f, 0.62f,
                    ElarionUi.ParchmentDim, ElarionUi.FontHead, TextAlignmentOptions.Center,
                    0.05f, 0.40f, bold: true);
            }
            ElarionUiKit.Label(content, entry.Element, 0.20f, 0.29f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TextAlignmentOptions.Center,
                0.05f, 0.40f, bold: true);

            // -- RIGHT: name + flavor ------------------------------------------------
            // NON-OVERLAP BUDGET (content fractions, top->bottom): name 0.75-0.87,
            // flavor 0.44-0.73, then a ~0.055 gap, then the button block 0.185-0.385
            // (which itself clears the shared bottom-center Close band that tops out
            // near y=0.161). Each band is disjoint by construction so nothing can stack.
            var nameLabel = ElarionUiKit.Label(content, entry.DisplayName, 0.75f, 0.87f,
                ElarionUi.Gilt, ElarionUi.FontHead, TextAlignmentOptions.Left,
                0.45f, 0.97f, bold: true);
            ElarionUiKit.FitSingleLine(nameLabel);

            // BUGFIX (owner F8 screenshot 2026-07-19: "button/text overlap"): the flavor
            // block used TMP's default Overflow mode, so the longer founding/essence copy
            // (and the even-longer Lore swapped in by "Tell me more") flowed top-down from
            // 0.72 and painted PAST its 0.44 rect bottom straight over the buttons whose
            // tops were at 0.435 (a mere ~0.005/~6px away). FitBlock bounds it to a legible
            // auto-size range [FontFloor..FontBody] and switches overflow to TRUNCATE, so the
            // text can NEVER paint below its own rect onto the buttons -- and its rect bottom
            // (0.44) now sits a full ~0.055 above the button block top (0.385). Guaranteed
            // no overlap at the LONGEST copy: worst case the tail truncates inside the band.
            _flavorLabel = ElarionUiKit.Label(content, entry.Flavor, 0.44f, 0.73f,
                ElarionUi.Parchment, ElarionUi.FontBody, TextAlignmentOptions.TopLeft,
                0.45f, 0.97f, bold: false);
            ElarionUiKit.FitBlock(_flavorLabel);

            // -- RIGHT: the three action buttons (block 0.185-0.385, below the flavor with a
            //    ~0.055 gap and clear of the shared Close band at the card's bottom-center) --
            ElarionUiKit.Button(content, "I accept your power", ElarionUiKit.ButtonKind.Confirm,
                new Vector2(0.45f, 0.285f), new Vector2(0.72f, 0.385f), OnAccept);
            _tellMoreBtn = ElarionUiKit.Button(content, "Tell me more", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.45f, 0.185f), new Vector2(0.72f, 0.275f), OnTellMore);
            ElarionUiKit.Button(content, "Dismiss", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.76f, 0.185f), new Vector2(0.97f, 0.385f), OnDismiss);
        }

        // -- button handlers --------------------------------------------------------
        private void OnAccept()
        {
            FlowTrace.Step("Echo", $"unlock dialogue: 'I accept your power' id={_entry?.Id}");
            Close();
        }

        private void OnDismiss()
        {
            FlowTrace.Step("Echo", $"unlock dialogue: 'Dismiss' id={_entry?.Id}");
            Close();
        }

        private void OnTellMore()
        {
            _showingLore = !_showingLore;
            if (_flavorLabel != null && _entry != null)
                _flavorLabel.text = _showingLore ? _entry.Lore : _entry.Flavor;
            if (_tellMoreBtn != null)
            {
                var t = _tellMoreBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (t != null) t.text = _showingLore ? "Show less" : "Tell me more";
            }
            FlowTrace.Step("Echo", $"unlock dialogue: 'Tell me more' -> {(_showingLore ? "lore" : "flavor")} id={_entry?.Id}");
        }

        private void Close()
        {
            // HUD-1: notify the arbiter BEFORE teardown; null the handle so a double Close
            // (self-replace + arbiter swap) never double-notifies. Guarded-safe on destroyed objects.
            _open = false;
            if (_panelHandle != null) { PanelManager.NotifyClosed(_panelHandle); _panelHandle = null; }
            if (_canvas != null) { Destroy(_canvas); _canvas = null; }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (s_active == this) s_active = null;
        }

        // Buttons need an EventSystem to receive clicks (mirrors EchoWorkforceHud).
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
