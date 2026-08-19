// =============================================================================
// EchoUnlockDialogue -- the "Echoes of Elarion" awakening card. ONE SCREEN.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// OWNER RULING 2026-08-05 (verbatim): "the echo cards. It does not need three
// buttons. Two buttons is fine. I don't need one screen that tells me that I have
// an echo, and the next screen that shows me about the echo, it should just simply
// be one screen."
//
// WHAT THAT RETIRED. The beat used to be TWO modals:
//   STATE 1 (WO-831 EMERGENCE): a 2D sprite rising from the Heart-tree + the
//     one-line EmergeLine + a "Continue" advance.
//   STATE 2 (AWAKENING CARD): portrait / name / essence caption / flavor, with
//     "I accept your power", the kit's shared bottom-centre Close, and "Tell me
//     more" -- THREE buttons.
// Both states are now ONE card. Nothing was dropped: the announcement beat (its
// headline AND its EmergeLine arrival copy AND its emergence artwork AND its
// fade/scale-in) is folded into the top of the single card, so the player still
// gets "a soul has woken" -- it just no longer costs a separate tap.
//
// THE BUTTONS: 3 -> 2.
//   KEPT  "I accept your power"  (primary, dismisses -- the narrative assent)
//   KEPT  "Tell me more"         (the only NON-dismiss action; swaps flavor <-> lore)
//   GONE  the shared bottom-centre Close -- it is a DUPLICATE of Accept (same
//         outcome, one tap either way) and the 2026-08-05 Seeker review caught it
//         sitting BETWEEN the two positive actions. The owner already made exactly
//         this call for the emergence state (F8 seq 628: "its not needed continue
//         or close, both same answer"); merging makes the card terminal, so the same
//         ruling now applies here. Retired LOCALLY (SetActive false) -- the kit's
//         Close is canon for ~19 other panels and must not move for this one card.
//         Escapes remain: Accept, the scrim tap-outside (wired to Close), and the
//         PanelManager arbiter.
//   GONE  "Continue" -- it existed ONLY to advance screen 1 -> screen 2. The merge
//         deletes its reason to exist.
//
// ONE BUTTON TREATMENT (owner Seeker felt-test: "three mismatched button shapes").
// Both survivors are built by the SAME obsidian builder at the SAME Style1, then
// seated by SeatCta at ONE fixed pixel box (CtaWidthPx x CtaHeightPx) on ONE bottom
// baseline, mirrored about the panel centre. Identical shape / width / height /
// baseline; colour + LABEL carry emphasis, never shape (and never colour alone --
// the labels say what each does; the owner is red/green colourblind).
//
// NO TEXT OUTSIDE ITS PLATE (owner Seeker felt-test: "text spills outside the black
// panel"). Four independent guarantees, in order of strength:
//   1. EVERY piece of copy parents into the PLATE (chrome.layout.body -- the black
//      ZoneBacking rect IS the plate, NOT chrome.content). Only the frame's own
//      title label lives outside it.
//   2. The plate FLOOR is dropped to just above THIS card's button row using the
//      kit's OWN close-band math (CtaBottomFrac + CanonCtaHeight/panelPx + gap), so
//      the plate provably ends above the buttons at every resolution.
//   3. Every vertical band is a FIXED REFERENCE-PIXEL band derived from a kit
//      constant and pinned with PinBandFromTop/FromBottom -- never a fraction of the
//      parent. A fraction band under-heights the TMP line box (TMP then culls or
//      ellipsizes with no error) and, on a button, lets ClampMinTouch grow the rect
//      SYMMETRICALLY ABOUT ITS CENTRE up to MinTouchPx=112 -- which overlaps
//      neighbours and is the mechanical cause of the mismatched shapes. Both CTAs
//      are authored AT CanonCtaHeight(132) > MinTouchPx, so that guard is a no-op.
//   4. A RectMask2D on the plate: anything that would ever paint past the black
//      panel is clipped AT THE PANEL EDGE instead of landing on the metal frame.
//      The band budget below proves the copy fits, so nothing is actually clipped;
//      this is the backstop, not the plan.
//
// LAYOUT (single card; the treatment is borrowed from EchoRosterView, which the
// 2026-08-05 UI review named the best-composed screen in the build -- same panel
// rect, same fixed-pixel band discipline, same "all content in layout.body"):
//   frame title ..... "ECHOES OF ELARION"   (chrome, outside the plate)
//   PLATE, top-down, all fixed ref px:
//     headline ...... gold strip + Ink text  = HeaderFor(newCount)   [the announcement]
//     arrival ....... EmergeLineFor(entry)                           [the announcement]
//     LEFT  column .. emergence/portrait art, then the essence caption
//     RIGHT column .. name, then the flavor block (the flexible band)
//   BOTTOM ROW (panel-local, below the plate): the two CTAs.
//
// NARRATIVE (memory echo-is-essence-of-guarded-person, binding): an Echo is the
// awakened ESSENCE of one of the PEOPLE the Heart of Elarion guards -- not a monster,
// not loot. Three separate lines carry that on this one screen: the arrival line
// ("its first-kept soul rises to meet you"), the essence caption ("Essence of a
// fallen keeper") and the flavor. This is a narrative beat, not a stat card.
//
// Colorblind-safe: identity reads from PORTRAIT + TEXT, never hue alone. The build
// is Guard-wrapped (a missing image logs + shows a text fallback, never blanks).
// ASCII-only strings. FlowTrace on show/advance. Code-built uGUI, NO UXML
// (PIPELINE_STATE S8 -- UXML does not render in player builds).
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
    /// <summary>The data-driven Echo awakening card: announcement + spirit on ONE screen,
    /// built on the unlocked spirit's <see cref="EchoRosterEntry"/>. One on screen at a
    /// time; self-destroys on any close/accept.</summary>
    [DisallowMultipleComponent]
    public sealed class EchoUnlockDialogue : MonoBehaviour
    {
        private static EchoUnlockDialogue s_active;   // single instance

        // -- PANEL RECT --------------------------------------------------------
        // Borrowed VERBATIM from EchoRosterView (the review's reference screen) and
        // EchoCardView: all three Echo surfaces now open at ONE size. It is also the
        // rect that PAYS FOR THE MERGE -- at 2670x1200 the old 0.20-0.82 panel left a
        // ~326 ref-px plate, which could not hold both screens' copy; 0.05-0.95 leaves
        // ~533 px. The card takes every vertical pixel the frame will give it.
        private const float PanelXMin = 0.10f;
        private const float PanelXMax = 0.90f;
        private const float PanelYMin = 0.05f;
        private const float PanelYMax = 0.95f;

        // -- FIXED REF-PIXEL BAND CONSTANTS ------------------------------------
        // CANON_GROUND_TRUTH 2026-08-02 Sec.4: a text band is sized in FIXED reference
        // pixels >= the font's line box, NEVER as a fraction of a parent. Every value
        // below derives from a KIT constant so a kit change moves the card with it.
        // `public const` so an oracle can pin them without reflection tricks.

        /// <summary>One TMP line box at the kit's auto-size floor (ElarionUiKit.FontFloor=30).</summary>
        public const float FloorLinePx = ElarionUiKit.FontFloor * 1.25f + 2f;      // 39.5
        /// <summary>The gold announcement headline -- one FontBody line box.</summary>
        public const float HeadlineBandPx = ElarionUi.FontBody * 1.25f + 2f;       // 64.5
        /// <summary>The arrival line (the retired screen's EmergeLine) -- one FontLabel line box.
        /// The longest authored EmergeLine is 63 chars; at the FontFloor that measures ~945 ref
        /// units against a ~1280-unit band at the NARROWEST capture aspect, so it seats on one
        /// line with ~1.35x headroom and can never ellipsize into a fragment.</summary>
        public const float ArrivalBandPx = ElarionUi.FontLabel * 1.25f + 2f;       // 52
        /// <summary>The spirit's name -- one FontHead line box.</summary>
        public const float NameBandPx = ElarionUi.FontHead * 1.25f + 2f;           // 82
        /// <summary>The essence caption under the portrait. TWO floor line boxes: "Essence of a
        /// fallen keeper" seats on one line at FontLabel in the wide case and WRAPS to a second
        /// inside this band in the narrow one -- it must never be shrunk into a fragment
        /// (owner F8: "keeper" rendered entirely on the metal frame).</summary>
        public const float CaptionBandPx = 2f * FloorLinePx + 6f;                  // 85
        /// <summary>Gap between stacked fixed bands.</summary>
        public const float BandGapPx = 8f;
        /// <summary>Inset from the plate's top/bottom edge.</summary>
        public const float PlatePadPx = 6f;

        // -- THE ONE CTA BOX ---------------------------------------------------
        /// <summary>Both CTAs are EXACTLY the canonical CTA height (owner F8 x3: "Continue bar or
        /// Close button same size everywhere"). It is also 20 px ABOVE ElarionUiKit.MinTouchPx, so
        /// ClampMinTouch's centre-grow -- the mechanism behind the mismatched shapes -- is a
        /// guaranteed no-op on this row.</summary>
        public const float CtaHeightPx = ElarionUiKit.CanonCtaHeight;              // 132
        /// <summary>Both CTAs are EXACTLY this wide. Sized off the LONGEST label: "I accept your
        /// power" is 19 chars, ~380 ref units at ElarionUi.FontLabel(40); the obsidian button insets
        /// its label to 0.04-0.96, so it needs ~413 units of face. 480 seats it with ~16% headroom,
        /// which is why BOTH labels render at the SAME size instead of one auto-shrinking (the
        /// review's "three type sizes in one row").</summary>
        public const float CtaWidthPx = 480f;
        /// <summary>Gap between the two CTAs (and the fallback edge margin on a narrow pane).</summary>
        public const float CtaGapPx = 48f;
        /// <summary>The CTA row's bottom edge, as the kit's OWN close-band lower edge
        /// (ElarionUiKit DefaultCloseZone.y). Seated bottom-pivot so the fixed-pixel box grows
        /// UPWARD into the interior and can never dip into the frame's ornate bottom border --
        /// the same seat law as ElarionUiKit.SeatSharedCloseInside.</summary>
        private const float CtaBottomFrac = 0.050f;

        /// <summary>Legible mobile floor the flavor block auto-sizes DOWN to (never below) --
        /// the kit's own FontFloor, so a kit change moves this with it.</summary>
        private const float FlavorFontMin = ElarionUiKit.FontFloor;

        /// <summary>Awaken fade/scale-in duration (unscaled seconds). The WO-831 emergence polish,
        /// PRESERVED through the merge onto the single card -- a CanvasGroup lerp, no tween lib.</summary>
        private const float EmergeFadeSeconds = 0.45f;

        /// <summary>TRUE while the unlock card is on screen. Read by
        /// EchoService.AnnounceFoundingEcho to confirm the founding beat actually rendered
        /// before persisting its one-shot flag.</summary>
        public static bool IsShowing => s_active != null;

        private GameObject _canvas;            // the awakening-card canvas (the ONE screen)
        private EchoRosterEntry _entry;
        private int _newCount;
        private TextMeshProUGUI _flavorLabel;
        private Button _tellMoreBtn;
        private bool _showingLore;
        private PanelHandle _panelHandle;   // HUD-1: modal arbiter registration (one Echo modal at a time)
        private bool _open;

        /// <summary>Build + show the awakening card for the spirit earned at
        /// <paramref name="newCount"/> -- announcement and spirit on ONE screen (owner ruling
        /// 2026-08-05). Idempotent: replaces any card on screen. Returns TRUE when the card is on
        /// screen (used by the founding-echo teaching to persist its one-shot flag only after a
        /// confirmed render), FALSE on a null entry / build fault.</summary>
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
            dlg._entry = entry;
            dlg._newCount = newCount;

            // ONE build now (the emergence state is merged in), still Guard-wrapped: a faulted
            // build tears down cleanly and reports false -- the unlock itself is never blocked.
            bool ok = Guard.Try("Echo", "build echo awakening card", () => dlg.Build(entry, newCount));
            if (!ok)
            {
                FlowTrace.Warn("Echo", "awakening card failed to build -- beat skipped (unlock itself already granted).");
                if (dlg != null) Retire(dlg.gameObject);
                s_active = null;
                return false;
            }

            // HUD-1: register with the single-modal arbiter and announce the open. The unlock
            // card CLOSES any other Echo modal (roster/card/harvest) that was up -- one modal only.
            // Battle-lock (WO-437): a rejected open self-closes (SFX + pip still fire) -> return false
            // so the founding-echo teaching does not persist its one-shot on an un-rendered beat.
            dlg._open = true;
            dlg._panelHandle = PanelManager.Register("EchoUnlockDialogue", dlg.Close, () => dlg._open);
            if (!PanelManager.NotifyOpened(dlg._panelHandle))
            {
                // SELF-CLOSE on rejection, do not just return. The comment above has always said a
                // rejected open "self-closes", but the bare return left the built canvas on screen,
                // _open true, s_active set and the handle registered - a second way to strand the
                // arbiter. Close() tears the card down and releases the handle idempotently.
                FlowTrace.Warn("Echo", "unlock card rejected by PanelManager (battle-lock) -- self-closing.");
                dlg.Close();
                s_active = null;
                return false;
            }
            FlowTrace.Step("Echo", $"unlock card shown id={entry.Id} count={newCount} (one screen; announcement folded in)");
            return true;
        }

        /// <summary>FTUE-09: the gold announcement headline. newCount==1 is the FOUNDING AWAKEN
        /// -- there is no prior Echo to "level up" from, so "Echo Leveled Up to 1!" is
        /// nonsense; render an AWAKEN header instead (mirrors EchoRosterView's awaken
        /// wording). n>=2 is a genuine level-up. Pure + public so the copy is headlessly
        /// assertable (EchoCardCopyRegression).</summary>
        public static string HeaderFor(int newCount)
        {
            return newCount <= 1 ? "An Echo Awakens" : $"Echo Leveled Up to {newCount}!";
        }

        /// <summary>The ARRIVAL line for a spirit -- the authored EmergeLine, with a shared default
        /// when absent (never blank). This was the retired emergence screen's one-line intro; the
        /// merge keeps the copy and moves it to the top of the single card, so the "a soul has
        /// woken" beat survives without its own tap. Pure + public for headless asserts
        /// (EchoResourcePickerRegression).</summary>
        public static string EmergeLineFor(EchoRosterEntry entry)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.EmergeLine)) return entry.EmergeLine;
            return "The Heart stirs -- a keeper wakes.";
        }

        // =====================================================================
        //  FIXED-PIXEL BAND PINS (the WO-841 / WO-852 pattern, shared verbatim with
        //  EchoRosterView + EchoCardView)
        // ---------------------------------------------------------------------
        //  Re-hang a control on its parent's TOP or BOTTOM edge with a FIXED reference-pixel
        //  band. X anchors/offsets are preserved; only the vertical seat changes, so a band
        //  never scales with the pane and never under-heights its line box.
        // =====================================================================

        private static void PinBandFromTop(RectTransform rt, float topPx, float heightPx)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(rt.anchorMin.x, 1f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 1f);
            rt.offsetMin = new Vector2(rt.offsetMin.x, -(topPx + heightPx));
            rt.offsetMax = new Vector2(rt.offsetMax.x, -topPx);
        }

        private static void PinBandFromBottom(RectTransform rt, float bottomPx, float heightPx)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(rt.anchorMin.x, 0f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 0f);
            rt.offsetMin = new Vector2(rt.offsetMin.x, bottomPx);
            rt.offsetMax = new Vector2(rt.offsetMax.x, bottomPx + heightPx);
        }

        // =====================================================================
        //  THE ONE SCREEN
        // =====================================================================

        private void Build(EchoRosterEntry entry, int newCount)
        {
            _entry = entry;
            _newCount = newCount;
            EnsureEventSystem();

            // Shared obsidian chrome + scrim (tap-outside closes). Sits ABOVE both the roster
            // (31000) and the echo card/picker (31010) in the canon MODAL band.
            var built = ElarionUiKit.BuildObsidianModal(
                "EchoesOfElarion", "ECHOES OF ELARION",
                new Vector2(PanelXMin, PanelYMin), new Vector2(PanelXMax, PanelYMax),
                onClose: Close, sortingOrder: 31020,
                frameName: RpgUiCatalog.FrameCore);
            _canvas = built.canvas;
            var content = built.chrome.content.transform;

            // -- BUTTON #3 RETIRED (owner ruling 2026-08-05: "two buttons is fine") ---
            // The kit's shared bottom-centre Close is a DUPLICATE of "I accept your power"
            // (identical outcome), and the 2026-08-05 Seeker review caught it sitting BETWEEN
            // the two positive actions. Hidden LOCALLY for this card only -- the kit's Close is
            // canon for ~19 other panels and must not move. Tap-outside (the scrim) and the
            // PanelManager arbiter both still route to Close.
            if (built.chrome.close != null)
                built.chrome.close.gameObject.SetActive(false);

            // -- REFERENCE-PIXEL BUDGET FOR THIS BUILD -------------------------------
            // PostScaleCanvasHeight replicates the CanvasScaler's own math, so it is correct on
            // the creation frame (the live rect returns RAW SCREEN PIXELS until the scaler
            // applies). The canvas is ScreenSpaceOverlay, so its local rect carries the SCREEN's
            // aspect -- that is what gives us the width without a second kit helper.
            float canvasH = ElarionUiKit.PostScaleCanvasHeight(content);
            // SurfaceWidth/Height, not Screen.* — same value at runtime (no override), but this
            // is a BUILD-TIME layout read, so a capture must be able to drive it or the shot
            // reproduces the editor's 640x480 aspect (1.33) instead of the device's 2.23.
            float aspect = ElarionUiKit.SurfaceHeight > 0
                ? (float)ElarionUiKit.SurfaceWidth / ElarionUiKit.SurfaceHeight
                : 16f / 9f;
            float panelPx = Mathf.Max(1f, (PanelYMax - PanelYMin) * canvasH);
            float panelWpx = Mathf.Max(1f, (PanelXMax - PanelXMin) * canvasH * aspect);

            // -- THE BLACK PLATE IS layout.body (owner F8 2026-08-05) ----------------
            // The visible black area of this card is the kit's Zone_Body ZoneBacking, so the
            // plate rect IS chrome.layout.body -- NOT chrome.content. Copy authored in PANEL
            // fractions does not track the body zone's floor, and that floor MOVES at runtime
            // (the factory close-band reservation), which is how the element caption ended up
            // 100% BELOW the plate at every resolution, painting on the metal frame.
            //
            // Reclaim + re-parent. This card uses NEITHER the frame footer band NOR the shared
            // Close's exclusion band (the Close is retired above and its own CTA row owns the
            // bottom), so that height was reserved for nothing. Drop the plate floor to just
            // above THIS card's CTA row, replicating the kit's own close math rather than a
            // magic number, so it self-corrects if the reservation changes.
            var plate = built.chrome.layout != null ? built.chrome.layout.body : null;
            if (plate != null)
            {
                float floorY = CtaBottomFrac + CtaHeightPx / panelPx + 0.020f;
                // Never invert the plate on a pathologically short pane.
                floorY = Mathf.Min(floorY, plate.anchorMax.y - 0.05f);
                plate.anchorMin = new Vector2(plate.anchorMin.x, floorY);
                plate.offsetMin = Vector2.zero;

                // GUARANTEE 4: nothing parented to the plate may paint past it. A soft clip is
                // cheaper than a Mask and needs no stencil; it turns "text on the metal frame"
                // (the reported defect) into "text stops at the panel edge". The band budget
                // below proves the copy fits, so this should never actually clip anything.
                if (plate.GetComponent<RectMask2D>() == null)
                    plate.gameObject.AddComponent<RectMask2D>();
            }
            // Every piece of copy parents HERE (plate-local), never to content.
            Transform well = plate != null ? plate.transform : content;
            float plateHpx = plate != null
                ? (plate.anchorMax.y - plate.anchorMin.y) * panelPx
                : panelPx;

            // -- COLUMN X BANDS (fractions of the PLATE, disjoint) -------------------
            // X stays fractional on purpose: the columns resolve to ~430 and ~815 ref px at the
            // narrowest capture aspect, orders of magnitude above any floor, and the two bands
            // are disjoint with a ~55 px gutter -- so the caption and the flavor can never
            // collide even at their tallest. Only the VERTICAL axis is where a fraction bites.
            const float LeftX0 = 0.02f, LeftX1 = 0.34f;
            const float RightX0 = 0.38f, RightX1 = 0.98f;

            // =====================================================================
            //  PLATE STACK, top-down, every band FIXED reference pixels
            // =====================================================================
            float cursor = PlatePadPx;

            // -- (1) THE ANNOUNCEMENT HEADLINE -- folded in from the retired first screen.
            // A gold strip with Ink text, INSIDE the plate. It used to be a panel-local strip at
            // y 0.885-0.98, which overlapped the frame's own title band (FrameCore Zone_Header
            // 0.900-0.972) -- two titles in one plate. Inside the plate it collides with nothing
            // and the "no text outside its plate" law covers it like everything else.
            var strip = ElarionUiKit.AddImage(well, "HeadlineStrip",
                new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), ElarionUi.Gold, rounded: true);
            var stripImg = strip.GetComponent<Image>();
            if (stripImg != null) stripImg.raycastTarget = false;
            PinBandFromTop((RectTransform)strip.transform, cursor, HeadlineBandPx);

            var headline = ElarionUiKit.Label(well, HeaderFor(newCount), 0f, 1f,
                ElarionUi.Ink, ElarionUi.FontBody, TextAlignmentOptions.Center,
                0.04f, 0.96f, bold: true);
            ElarionUiKit.FitSingleLine(headline);
            PinBandFromTop(headline.rectTransform, cursor, HeadlineBandPx);
            cursor += HeadlineBandPx + BandGapPx;

            // -- (2) THE ARRIVAL LINE -- the retired emergence screen's own copy, verbatim.
            // This is the "you have earned an Echo" beat in words, and it is the line that
            // carries the canon (memory echo-is-essence-of-guarded-person): a soul the Heart
            // has been keeping is waking, not a monster being granted.
            var arrival = ElarionUiKit.Label(well, EmergeLineFor(entry), 0f, 1f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TextAlignmentOptions.Center,
                0.03f, 0.97f, bold: false);
            ElarionUiKit.FitSingleLine(arrival);
            PinBandFromTop(arrival.rectTransform, cursor, ArrivalBandPx);
            cursor += ArrivalBandPx + BandGapPx;

            // -- (3) THE TWO COLUMNS share whatever the plate has left ---------------
            // The FIXED bands are taken first; the PORTRAIT (an image) and the FLAVOR (auto-
            // sizing prose) absorb the remainder, so a shorter plate shrinks the flexible
            // elements and never the text line boxes.
            float columnsPx = Mathf.Max(FloorLinePx, plateHpx - cursor - PlatePadPx);
            float flavorPx = Mathf.Max(FloorLinePx, columnsPx - NameBandPx - BandGapPx);
            float portraitPx = Mathf.Max(FloorLinePx, columnsPx - CaptionBandPx - BandGapPx);
            FlowTrace.Step("Echo", $"unlock card layout: canvasH={canvasH:F0} panel={panelWpx:F0}x{panelPx:F0} "
                + $"plateH={plateHpx:F0} columns={columnsPx:F0} flavor={flavorPx:F0} portrait={portraitPx:F0} (ref px)");
            // The longest authored flavor is ~310 chars; at the FontFloor in the ~815 px right
            // column that is 6 wrapped lines == ~225 ref px. Below that this pane cannot seat the
            // founding copy and the plate needs a band shed -- say so rather than silently spilling.
            if (flavorPx < 6f * FloorLinePx)
                FlowTrace.Warn("Echo", $"unlock card: flavor well is {flavorPx:F0} ref px, under the "
                    + $"{6f * FloorLinePx:F0} px the longest founding copy needs at the font floor -- "
                    + "shed a band from the plate rather than shrinking the copy.");

            // LEFT COLUMN -- the spirit's image. EMERGENCE art first (that is the retired
            // screen's artwork, and the merge must not drop it), then the roster portrait, then
            // a text placeholder. Every step is Guard-logged; missing art NEVER blocks the unlock.
            var sprite = EchoRosterCatalog.LoadEmergence(entry.PortraitName);
            bool usedPortraitFallback = sprite == null;
            if (sprite == null) sprite = EchoRosterCatalog.LoadPortrait(entry.PortraitName);
            if (sprite != null)
            {
                var pg = new GameObject("EchoPortrait", typeof(Image));
                pg.transform.SetParent(well, false);
                var prt = pg.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(LeftX0, 1f);
                prt.anchorMax = new Vector2(LeftX1, 1f);
                prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                PinBandFromTop(prt, cursor, portraitPx);
                var pimg = pg.GetComponent<Image>();
                pimg.sprite = sprite;
                pimg.preserveAspect = true;
                pimg.raycastTarget = false;
                if (usedPortraitFallback)
                    FlowTrace.Warn("Echo", $"unlock card: no emergence art for '{entry.PortraitName}' -- the roster portrait stands in (degrade, never block).");
            }
            else
            {
                // Never blank: a text placeholder stands in for missing art entirely.
                var ph = ElarionUiKit.Label(well, "[ " + entry.Element + " ]", 0f, 1f,
                    ElarionUi.ParchmentDim, ElarionUi.FontHead, TextAlignmentOptions.Center,
                    LeftX0, LeftX1, bold: true);
                ElarionUiKit.FitBlock(ph, ElarionUiKit.FontFloor, ElarionUi.FontHead);
                PinBandFromTop(ph.rectTransform, cursor, portraitPx);
                FlowTrace.Warn("Echo", $"unlock card: no emergence OR portrait art for '{entry.PortraitName}' -- text placeholder shown.");
            }

            // ESSENCE CAPTION -- the owner's "keeper fell off the black" line, and one of the
            // three lines that carry the canon. FitBlock rather than FitSingleLine: the copy
            // measures wider than the left column at FontLabel on the narrow aspects, so a
            // single-line fit would bottom out on the hard floor and STILL ellipsize
            // ("...kee..."). FitBlock keeps it legible, lets it WRAP to a second line inside its
            // own two-line band, and its Truncate mode means the glyphs can never paint past the
            // rect -- which is the defect that was actually reported.
            var elemLabel = ElarionUiKit.Label(well, entry.Element, 0f, 1f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TextAlignmentOptions.Center,
                LeftX0, LeftX1, bold: true);
            ElarionUiKit.FitBlock(elemLabel, 26f, ElarionUi.FontLabel);
            PinBandFromTop(elemLabel.rectTransform, cursor + portraitPx + BandGapPx, CaptionBandPx);

            // RIGHT COLUMN -- name, then the flavor block.
            var nameLabel = ElarionUiKit.Label(well, entry.DisplayName, 0f, 1f,
                ElarionUi.Gilt, ElarionUi.FontHead, TextAlignmentOptions.Left,
                RightX0, RightX1, bold: true);
            ElarionUiKit.FitSingleLine(nameLabel);
            PinBandFromTop(nameLabel.rectTransform, cursor, NameBandPx);

            // FLAVOR: SHRINK-TO-FIT, NEVER TRUNCATE (owner F8 2026-07-19: the founding teach line
            // "...wood, iron, or grain -- and it is done" was CUT to "...and it is d"). So this is
            // deliberately NOT FitBlock (which forces Truncate): the label WRAPS and AUTO-SIZES
            // DOWN within [FlavorFontMin..FontBody] with Overflow mode, so a tail can never be cut.
            // The rect is plate-local and sized from the measured budget above (~815 x ~295 ref px
            // at the tightest landscape target), where the ~310-char founding flavor settles at
            // ~30-32 px over 6-7 lines -- inside the owner's 28-34 legible band, with a line of
            // slack. And because the plate now carries a RectMask2D, even an unbudgeted overflow
            // stops at the black panel's edge instead of landing on the metal frame.
            _flavorLabel = ElarionUiKit.Label(well, entry.Flavor, 0f, 1f,
                ElarionUi.Parchment, ElarionUi.FontBody, TextAlignmentOptions.TopLeft,
                RightX0, RightX1, bold: false);
            _flavorLabel.textWrappingMode = TextWrappingModes.Normal;   // wrap, don't spill wide
            _flavorLabel.overflowMode = TextOverflowModes.Overflow;     // NEVER cut the tail
            _flavorLabel.enableAutoSizing = true;                       // shrink-to-fit the rect
            _flavorLabel.fontSizeMin = FlavorFontMin;                   // legible mobile floor
            _flavorLabel.fontSizeMax = ElarionUi.FontBody;              // never grow past body
            PinBandFromTop(_flavorLabel.rectTransform, cursor + NameBandPx + BandGapPx, flavorPx);

            // =====================================================================
            //  THE BOTTOM ROW -- TWO buttons, ONE treatment (owner ruling 2026-08-05)
            // =====================================================================
            // Panel-local by design (they sit BELOW the plate, whose floor was dropped to clear
            // them). Both come from the SAME builder at the SAME Style1 -- ButtonKind is a
            // back-compat shim that maps Confirm->(Style2,Green) and Quiet->(Style1,Gray), and
            // ObsidianButtonSpriteName resolves button2_* ROUNDED vs button1_* SQUARE, which is
            // exactly how this card used to ship one rounded and one square face. Style carries
            // no meaning here (the LABEL does), so both are Style1 and colour alone never has to
            // be read (the owner is red/green colourblind).
            float ctaW = CtaWidthPx;
            if (2f * ctaW + CtaGapPx > panelWpx)
            {
                // Narrow pane (portrait phone): shrink BOTH equally rather than let one overhang.
                // Still far above the ~310 px the longest label needs at the font floor.
                ctaW = Mathf.Max(ElarionUiKit.MinTouchPx, (panelWpx - 3f * CtaGapPx) * 0.5f);
                FlowTrace.Warn("Echo", $"unlock card: panel is {panelWpx:F0} ref px wide -- CTA pair "
                    + $"narrowed from {CtaWidthPx:F0} to {ctaW:F0} px each (still one shared box).");
            }
            float halfStep = (ctaW + CtaGapPx) * 0.5f / panelWpx;

            var acceptBtn = ElarionUiKit.BuildObsidianButton(content, "I accept your power",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Green,
                new Vector2(0.5f - halfStep, CtaBottomFrac), new Vector2(0.5f - halfStep, CtaBottomFrac),
                OnAccept);
            SeatCta(acceptBtn, 0.5f - halfStep, ctaW);

            _tellMoreBtn = ElarionUiKit.BuildObsidianButton(content, "Tell me more",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.5f + halfStep, CtaBottomFrac), new Vector2(0.5f + halfStep, CtaBottomFrac),
                OnTellMore);
            SeatCta(_tellMoreBtn, 0.5f + halfStep, ctaW);

            // -- the WO-831 emergence polish, PRESERVED on the merged card: a soft fade/scale-in
            //    so the spirit still "emerges" instead of popping (CanvasGroup coroutine on
            //    unscaled time -- no tween lib, no Timeline, no video).
            var group = built.chrome.content.GetComponent<CanvasGroup>();
            if (group == null) group = built.chrome.content.AddComponent<CanvasGroup>();
            if (Application.isPlaying)
                StartCoroutine(EmergeIn(group, content));
            else
                group.alpha = 1f;   // edit-mode capture (RunCaptureHeadless): no coroutine tick -- render fully opaque
        }

        /// <summary>Seat one CTA at THE shared box: fixed <see cref="CtaWidthPx"/> x
        /// <see cref="CtaHeightPx"/> reference pixels, bottom-pivoted on the kit's own close band
        /// so both buttons share one baseline and grow UPWARD (never into the frame's ornate
        /// bottom border). Fixed pixels, never a fraction: the height is above
        /// ElarionUiKit.MinTouchPx, so the kit's ClampMinTouch centre-grow -- the mechanism that
        /// produced the mismatched shapes -- has nothing to grow. Position/size only; it does not
        /// restyle or re-wire the button.</summary>
        private static void SeatCta(Button btn, float centreX, float widthPx)
        {
            if (btn == null) return;
            var rt = btn.transform as RectTransform;
            if (rt == null) return;
            rt.anchorMin = new Vector2(centreX, CtaBottomFrac);
            rt.anchorMax = new Vector2(centreX, CtaBottomFrac);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(widthPx, CtaHeightPx);

            // ONE type size across the row (the review's "three type sizes in one row" came from
            // each button auto-shrinking its own label independently). The box is sized so the
            // LONGEST label fits at FontLabel, so both render at FontLabel; the floor is the
            // kit's, so a future longer label degrades legibly instead of clipping.
            var t = btn.GetComponentInChildren<TMP_Text>();
            if (t != null)
            {
                t.fontSize = ElarionUi.FontLabel;
                ElarionUiKit.FitSingleLine(t, ElarionUiKit.FontFloor, ElarionUi.FontLabel);
            }
        }

        private System.Collections.IEnumerator EmergeIn(CanvasGroup group, Transform contentRoot)
        {
            float t = 0f;
            var baseScale = contentRoot != null ? contentRoot.localScale : Vector3.one;
            while (t < EmergeFadeSeconds && group != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / EmergeFadeSeconds);
                group.alpha = k;
                if (contentRoot != null)
                    contentRoot.localScale = baseScale * Mathf.Lerp(0.9f, 1f, k);
                yield return null;
            }
            if (group != null) group.alpha = 1f;
            if (contentRoot != null) contentRoot.localScale = baseScale;
        }

        // -- button handlers --------------------------------------------------------
        private void OnAccept()
        {
            FlowTrace.Step("Echo", $"unlock card: 'I accept your power' id={_entry?.Id} count={_newCount}");
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
            FlowTrace.Step("Echo", $"unlock card: 'Tell me more' -> {(_showingLore ? "lore" : "flavor")} id={_entry?.Id}");
        }

        private void Close()
        {
            // HUD-1: notify the arbiter BEFORE teardown; null the handle so a double Close
            // (self-replace + arbiter swap) never double-notifies. Guarded-safe on destroyed objects.
            _open = false;
            if (_panelHandle != null) { PanelManager.NotifyClosed(_panelHandle); _panelHandle = null; }
            if (_canvas != null) { Retire(_canvas); _canvas = null; }
            Retire(gameObject);
        }

        /// <summary>Destroy that is safe in BOTH modes: runtime Destroy in play mode,
        /// DestroyImmediate in edit mode (the headless UI-capture harness builds this card
        /// outside play mode, where runtime Destroy errors).</summary>
        private static void Retire(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        private void OnDestroy()
        {
            if (s_active == this) s_active = null;

            // ⛔ RELEASE THE ARBITER HANDLE HERE TOO, NOT ONLY IN Close().
            // Close() is the tidy path and it does notify - but ANY other route to destruction
            // (scene unload, a parent torn down, a Retire() from elsewhere, the object dying with
            // the Echo body) skipped this and left PanelManager._open pointing at a panel that no
            // longer exists. There is no second owner to clear it: OpenPanelName reads _open.Name
            // directly, so the arbiter reports a modal open forever.
            //
            // CAPTURED, not theorised (owner F8, 2026-08-19, seq2536-2539):
            //   [Flow:UI] Dialogue suppressed - modal open ('EchoUnlockDialogue' at dialogue open,
            //             WO-795 truce); restored on modal close.
            // fired for FOUR consecutive tutorial dialogues over ~10 minutes while the card was
            // long gone from the owner's screen ("echo closes fine for me"). Each step then timed
            // out at its 120 s watchdog and was rescued as SKIPPED: founding_walk (the hero could
            // not walk, inputSuppressed, pos unchanged to the centimetre across all four captures),
            // founding_ack, founding_defense, founding_timers. The FTUE narrated nothing and
            // granted everything, and combat opened on a frozen hero.
            //
            // Idempotent: NotifyClosed no-ops unless _open IS this handle, and Close() nulls the
            // field, so the tidy path still notifies exactly once.
            if (_panelHandle != null)
            {
                FlowTrace.Warn("Echo", "unlock card DESTROYED while still holding the modal arbiter " +
                    "- releasing it here. Something retired this card without calling Close(); that " +
                    "path used to strand PanelManager._open and suppress every later dialogue.");
                PanelManager.NotifyClosed(_panelHandle);
                _panelHandle = null;
                _open = false;
            }
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
