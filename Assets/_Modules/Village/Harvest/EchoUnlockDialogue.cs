// =============================================================================
// EchoUnlockDialogue -- the "Echoes of Elarion" portrait unlock card (owner
// felt-test 2026-07-17 + mockup Screenshot 2026-07-17 062124.png) with the
// WO-831 2D EMERGENCE BEAT in front of it.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// When a new Echo is earned (EchoService.EchoUnlocked -> newCount), a spirit
// AWAKENS and speaks. WO-831 (owner: "leave the sprite as 2D -- would only be to
// introduce new sprite and advance dialog"): the flow is now a TWO-STATE beat --
//   STATE 1 (EMERGENCE): a 2D sprite of the spirit rising from the Heart-tree
//     (Resources/Echoes/Emergence/<PortraitName>_emerge.png, LFS) + the one-line
//     EmergeLine intro + a "Continue" advance. A soft fade/scale-in (CanvasGroup
//     coroutine -- no tween lib, no Timeline, no video) makes it "emerge".
//     Missing art DEGRADES (Guard + Warn): portrait stands in, then a text
//     placeholder -- the beat NEVER blocks the unlock. NO 3D (portrait-spirit canon).
//   STATE 2 (AWAKENING CARD): the existing full portrait dialogue card,
//     DATA-DRIVEN from EchoRosterCatalog.ByCount(newCount) -- portrait / name /
//     element / flavor, "I accept your power" / "Tell me more".
// Both unlock paths ride this (founding echo via AnnounceFoundingEcho, #2-6 via
// the wave bridge) -- they all land in Show().
//
// LAYOUT (card state -- matches the mockup, clean not ornate; reuses ElarionUiKit
// obsidian chrome, code-built uGUI, NO UXML per PIPELINE_STATE S8):
//   frame title "ECHOES OF ELARION"      (BuildObsidianModal header)
//   gold "Echo Leveled Up to N!" banner  (top strip)
//   LEFT : portrait (Sprite.Create) + essence subtitle ("Essence of a fallen keeper")
//   RIGHT: name ("Aldwin, the Ice Echo") + a tall flavor block + 2 card buttons:
//     "I accept your power" (primary, closes) / "Tell me more" (swaps flavor ->
//     extended lore). The SINGLE dismiss is the shared bottom-center obsidian Close
//     the kit seats on every panel -- the card no longer adds its own "Dismiss"
//     (that was a duplicate; owner F8 2026-07-19 "two ways to dismiss"). One Close,
//     game-wide-consistent, is canon (ElarionUiKit "one consistent Close" ruling).
//
// Colorblind-safe: identity reads from PORTRAIT + TEXT, never hue alone. Portrait
// load + both builds are Guard-wrapped (a missing image logs + shows a text
// fallback, never blanks; a faulted emergence falls straight through to the card).
// ASCII-only strings. FlowTrace on show/advance.
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
    /// <summary>Data-driven Echo-unlock beat: the WO-831 2D emergence intro, advancing on
    /// tap into the awakening portrait card. Built on the unlocked spirit's
    /// <see cref="EchoRosterEntry"/>. One on screen at a time; self-destroys on any
    /// close/accept/dismiss.</summary>
    [DisallowMultipleComponent]
    public sealed class EchoUnlockDialogue : MonoBehaviour
    {
        private static EchoUnlockDialogue s_active;   // single instance

        /// <summary>Legible mobile floor the flavor block auto-sizes DOWN to (never below).
        /// Kept in the owner's 28-34px legible band; the founding copy settles ~33px in the
        /// tall rect, so this floor is a safety net for the longest lore, not the usual size.</summary>
        private const float FlavorFontMin = 30f;

        /// <summary>Emergence fade/scale-in duration (unscaled seconds). Short and cheap --
        /// a CanvasGroup lerp, no tween lib (WO-831 polish scope).</summary>
        private const float EmergeFadeSeconds = 0.45f;

        /// <summary>TRUE while the unlock beat (emergence OR card state) is on screen. Read by
        /// EchoService.AnnounceFoundingEcho to confirm the founding beat actually rendered
        /// before persisting its one-shot flag.</summary>
        public static bool IsShowing => s_active != null;

        private GameObject _canvas;            // the awakening-card canvas (state 2)
        private GameObject _emergenceCanvas;   // the WO-831 emergence canvas (state 1)
        private EchoRosterEntry _entry;
        private int _newCount;
        private TextMeshProUGUI _flavorLabel;
        private Button _tellMoreBtn;
        private bool _showingLore;
        private PanelHandle _panelHandle;   // HUD-1: modal arbiter registration (one Echo modal at a time)
        private bool _open;

        /// <summary>Build + show the unlock beat for the spirit earned at
        /// <paramref name="newCount"/>: the WO-831 emergence intro first, advancing into the
        /// awakening card. Idempotent: replaces any beat on screen. Returns TRUE when the beat
        /// is on screen (used by the founding-echo teaching to persist its one-shot flag only
        /// after a confirmed render), FALSE on a null entry / build fault.</summary>
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

            // WO-831: emergence beat first, Guard-wrapped. A faulted emergence build must
            // NEVER block the unlock -- fall straight through to the awakening card (also
            // guarded). Only when BOTH states fault do we tear down and report false.
            bool ok = Guard.Try("Echo", "build echo emergence beat", () => dlg.BuildEmergence(entry));
            if (!ok)
            {
                FlowTrace.Warn("Echo", "emergence beat failed to build -- advancing straight to the awakening card (unlock never blocked).");
                ok = Guard.Try("Echo", "build echo unlock dialogue", () => dlg.Build(entry, newCount));
            }
            if (!ok)
            {
                if (dlg != null) Destroy(dlg.gameObject);
                s_active = null;
                return false;
            }

            // HUD-1: register with the single-modal arbiter and announce the open. The unlock
            // beat CLOSES any other Echo modal (roster/card/harvest) that was up -- one modal only.
            // Battle-lock (WO-437): a rejected open self-closes (SFX + pip still fire) -> return false
            // so the founding-echo teaching does not persist its one-shot on an un-rendered beat.
            dlg._open = true;
            dlg._panelHandle = PanelManager.Register("EchoUnlockDialogue", dlg.Close, () => dlg._open);
            if (!PanelManager.NotifyOpened(dlg._panelHandle))
            {
                FlowTrace.Warn("Echo", "unlock dialogue rejected by PanelManager (battle-lock) -- not shown.");
                return false;
            }
            FlowTrace.Step("Echo", $"unlock beat shown id={entry.Id} count={newCount} (state={(dlg._emergenceCanvas != null ? "emergence" : "card")})");
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

        /// <summary>WO-831: the emergence intro line for a spirit -- the authored EmergeLine,
        /// with a shared default when absent (never blank). Pure + public for headless asserts.</summary>
        public static string EmergeLineFor(EchoRosterEntry entry)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.EmergeLine)) return entry.EmergeLine;
            return "The Heart stirs -- a keeper wakes.";
        }

        // =====================================================================
        //  STATE 1 -- the WO-831 emergence beat (2D sprite + one line + advance)
        // =====================================================================

        private void BuildEmergence(EchoRosterEntry entry)
        {
            EnsureEventSystem();

            // Shared obsidian chrome. The scrim keeps Close as the tap-outside escape.
            var built = ElarionUiKit.BuildObsidianModal(
                "EchoEmergence", "ECHOES OF ELARION",
                new Vector2(0.14f, 0.20f), new Vector2(0.86f, 0.82f),
                onClose: Close, sortingOrder: 31020,   // same MODAL band as the card state
                frameName: RpgUiCatalog.FrameCore);
            _emergenceCanvas = built.canvas;
            var content = built.chrome.content.transform;

            // OWNER F8 seq 628 (2026-08-02): "only leave the continue button - its not needed
            // continue or close, both same answer". This beat is LINEAR, so two exits read as
            // one choice offered twice. Continue is the ONE exit; retire the shared Close for
            // THIS state only. Deliberately local (SetActive) rather than a kit change: the
            // kit's Close is canon for ~19 other panels (ElarionUiKit.ObsidianCloseButton) and
            // must not move for a single linear beat. The awakening card that Continue opens
            // KEEPS its Close - that state is the terminal one.
            if (built.chrome.close != null)
                built.chrome.close.gameObject.SetActive(false);

            // -- the emergence sprite (CENTER). Fallback chain (Guard-logged, never blank):
            //    Emergence art -> portrait -> text placeholder. Missing art never blocks.
            var sprite = EchoRosterCatalog.LoadEmergence(entry.PortraitName);
            bool usedFallback = sprite == null;
            if (sprite == null) sprite = EchoRosterCatalog.LoadPortrait(entry.PortraitName);
            if (sprite != null)
            {
                var pg = new GameObject("EmergenceSprite", typeof(Image));
                pg.transform.SetParent(content, false);
                // NON-OVERLAP BUDGET (content fractions, every band DISJOINT):
                //   sprite ......... y[0.42-0.86] x[0.30-0.70]  (center stage)
                //   intro line ..... y[0.28-0.40] x[0.08-0.92]
                //   bottom row ..... y[0.05-0.245]: shared Close center, Continue x[0.64-0.955]
                var prt = pg.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0.30f, 0.42f);
                prt.anchorMax = new Vector2(0.70f, 0.86f);
                prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                var pimg = pg.GetComponent<Image>();
                pimg.sprite = sprite;
                pimg.preserveAspect = true;
                pimg.raycastTarget = false;
                if (usedFallback)
                    FlowTrace.Warn("Echo", $"emergence beat: no emergence art for '{entry.PortraitName}' -- portrait stands in (degrade, never block).");
            }
            else
            {
                // Never blank: a text placeholder stands in for missing art entirely.
                ElarionUiKit.Label(content, "[ " + entry.Element + " ]", 0.56f, 0.72f,
                    ElarionUi.ParchmentDim, ElarionUi.FontHead, TextAlignmentOptions.Center,
                    0.20f, 0.80f, bold: true);
                FlowTrace.Warn("Echo", $"emergence beat: no emergence OR portrait art for '{entry.PortraitName}' -- text placeholder shown.");
            }

            // -- the one-line intro (under the sprite; ASCII, colorblind-safe TEXT) ---
            var line = ElarionUiKit.Label(content, EmergeLineFor(entry), 0.28f, 0.40f,
                ElarionUi.Parchment, ElarionUi.FontBody, TextAlignmentOptions.Center,
                0.08f, 0.92f, bold: false);
            ElarionUiKit.FitSingleLine(line);

            // -- the advance (bottom-CENTER). It used to sit bottom-RIGHT at x[0.64-0.955]
            //    because it FLANKED the shared bottom-centre Close; owner F8 seq 628 retired
            //    that Close, so a lone button hugging the right edge just read as misaligned
            //    (owner 2026-08-02: "move the continue button on pet screen to center width
            //    still on bottom of box"). SAME WIDTH (0.315 of the content) and the SAME
            //    bottom-row budget y[0.05-0.245] (~118px tall, above MinTouchPx=112) --
            //    only the x band is re-centred on 0.5, so nothing about the touch target or
            //    the intro line's clearance (0.27+) changes. ---
            ElarionUiKit.Button(content, "Continue", ElarionUiKit.ButtonKind.Confirm,
                new Vector2(0.3425f, 0.05f), new Vector2(0.6575f, 0.245f), OnEmergenceContinue);

            // -- WO-831 polish: soft fade/scale-in so the spirit "emerges" (CanvasGroup
            //    coroutine on unscaled time -- no tween lib, no Timeline, no video). ---
            var group = built.chrome.content.GetComponent<CanvasGroup>();
            if (group == null) group = built.chrome.content.AddComponent<CanvasGroup>();
            if (Application.isPlaying)
                StartCoroutine(EmergeIn(group, content));
            else
                group.alpha = 1f;   // edit-mode capture (RunCaptureHeadless): no coroutine tick -- render fully opaque
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

        /// <summary>The WO-831 advance: emergence -> the existing awakening card. Guarded --
        /// a faulted card build closes the beat cleanly (SFX + pip already fired; never a
        /// wedged half-card).</summary>
        private void OnEmergenceContinue()
        {
            FlowTrace.Step("Echo", $"emergence beat: Continue -> awakening card id={_entry?.Id}");
            if (_emergenceCanvas != null) { Retire(_emergenceCanvas); _emergenceCanvas = null; }
            bool ok = Guard.Try("Echo", "build echo unlock dialogue", () => Build(_entry, _newCount));
            if (!ok)
            {
                FlowTrace.Warn("Echo", "awakening card failed to build after emergence -- closing the beat (unlock itself already granted).");
                Close();
            }
        }

        // =====================================================================
        //  STATE 2 -- the awakening portrait card (pre-831 layout, unchanged)
        // =====================================================================

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

            // -- THE BLACK PLATE IS layout.body (owner F8 2026-08-05) -----------------
            // The visible black area of this card is the kit's Zone_Body ZoneBacking
            // (ElarionUiKit.cs:690 ZoneBacking(layout.body, ObsidianFill)) -- so the plate
            // rect IS chrome.layout.body, NOT chrome.content. This card historically laid
            // its copy on chrome.content with PANEL fractions, which do NOT track the body
            // zone's floor. That floor MOVES at runtime: the factory close-band reservation
            // (ElarionUiKit.cs:604-647) raises z.body.y to footer.w + 0.015 and resolves to
            // 0.4305 @2670x1200 / 0.4276 @2340x1080 / 0.4071 @1920x1080 -- so the element
            // caption authored at panel y[0.30-0.39] was 100% BELOW the plate at EVERY
            // resolution (it painted on the metal frame), and the flavor's tail with it.
            //
            // Reclaim + re-parent. This card uses NEITHER the FrameCore footer band NOR the
            // shared Close's exclusion band for copy (its own button row owns y[0.05-0.245]),
            // so ~0.14 of panel height was reserved for nothing. Drop the plate floor to just
            // above whichever is higher -- our button row or the shared Close box -- and make
            // the plate itself the parent of all copy. Replicating the kit's OWN close math
            // (rather than a magic number) means this self-corrects if the reservation changes.
            const float BtnRowTop = 0.245f;                       // matches the button row below
            var plate = built.chrome.layout != null ? built.chrome.layout.body : null;
            if (plate != null)
            {
                float panelH   = ElarionUiKit.PostScaleCanvasHeight(content) * (0.82f - 0.20f);
                float closeTop = 0.050f + ElarionUiKit.CanonCtaHeight / Mathf.Max(1f, panelH);
                float floorY   = Mathf.Max(BtnRowTop, closeTop) + 0.020f;
                plate.anchorMin = new Vector2(plate.anchorMin.x, floorY);
                plate.offsetMin = Vector2.zero;
            }
            // Every piece of copy parents HERE (plate-local fractions), never to content.
            Transform well = plate != null ? plate.transform : content;

            // -- "Echo Leveled Up to N!" banner (top strip: gold fill + ink text) ----
            //    DELIBERATELY on `content`, not the plate: the gold strip is chrome that sits
            //    in the frame's own header band ABOVE the plate (same as the buttons below).
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
                pg.transform.SetParent(well, false);
                var prt = pg.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0.02f, 0.36f);
                prt.anchorMax = new Vector2(0.42f, 1.00f);
                prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                var pimg = pg.GetComponent<Image>();
                pimg.sprite = sprite;
                pimg.preserveAspect = true;
                pimg.raycastTarget = false;
            }
            else
            {
                // Never blank: a text placeholder stands in for a missing portrait.
                ElarionUiKit.Label(well, "[ " + entry.Element + " ]", 0.40f, 0.70f,
                    ElarionUi.ParchmentDim, ElarionUi.FontHead, TextAlignmentOptions.Center,
                    0.02f, 0.42f, bold: true);
            }
            // ELEMENT CAPTION -- the owner's "keeper fell off the black" line. Plate-local,
            // and FitBlock rather than FitSingleLine: "Essence of a fallen keeper" measures
            // ~1082 units at FontLabel 40 but the widest plate-local column is only 541 units
            // (@1920x1080), so a single-line fit would bottom out on the 20pt HARD FLOOR and
            // still ellipsize ("...kee..."). FitBlock keeps it legible, lets the last word WRAP
            // to a second line INSIDE the plate, and its Truncate mode guarantees the glyphs can
            // never paint past the rect -- which is the actual defect reported.
            var elemLabel = ElarionUiKit.Label(well, entry.Element, 0.02f, 0.34f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TextAlignmentOptions.Center,
                0.00f, 0.44f, bold: true);
            ElarionUiKit.FitBlock(elemLabel, 26f, ElarionUi.FontLabel);

            // -- RIGHT: name + flavor ------------------------------------------------
            // NON-OVERLAP BUDGET. TWO coordinate spaces now, and that distinction is the
            // whole fix -- all COPY is PLATE-local (fractions of `well` = layout.body, whose
            // floor we lowered above), all CHROME is PANEL-local (fractions of `content`):
            //   PLATE-local (of `well`), top->bottom, every band DISJOINT:
            //     name ............ 0.84-1.00  x[0.46-1.00]  (single line, fit)
            //     flavor block .... 0.02-0.82  x[0.46-1.00]  (TALL -- the founding copy is
            //                                   ~6 lines / ~320 chars; shrink-to-fit)
            //     portrait ........ 0.36-1.00  x[0.02-0.42]
            //     element caption . 0.02-0.34  x[0.00-0.44]  (wraps, FitBlock)
            //   The left and right columns are disjoint in x (0.44 vs 0.46), so the caption
            //   and the flavor can never collide even at their tallest.
            //   PANEL-local (of `content`), OUTSIDE the plate by design:
            //     banner/header ... 0.885-0.98 (top strip, built above)
            //     BOTTOM ROW (y 0.05-0.245), three affordances SIDE BY SIDE, no stacking:
            //       Accept ........ x[0.045-0.36]   (primary, bottom-LEFT under portrait)
            //       [shared Close]  x~[0.382-0.618] (kit's fixed 360px box, bottom-CENTER)
            //       Tell me more .. x[0.64 -0.955]  (toggle, bottom-RIGHT under the flavor)
            // The plate floor is seated at max(0.245 button row, Close box top) + 0.020, so
            // the plate ENDS above the whole bottom row at every resolution -- copy can no
            // longer reach a button, and (the reported defect) can no longer reach the metal.
            var nameLabel = ElarionUiKit.Label(well, entry.DisplayName, 0.84f, 1.00f,
                ElarionUi.Gilt, ElarionUi.FontHead, TextAlignmentOptions.Left,
                0.46f, 1.00f, bold: true);
            ElarionUiKit.FitSingleLine(nameLabel);

            // FLAVOR: SHRINK-TO-FIT, NEVER TRUNCATE (owner F8 2026-07-19: the founding teach
            // line "...wood, iron, or grain -- and it is done" was CUT to "...and it is d").
            // The prior fix wrapped this in ElarionUiKit.FitBlock, which forces
            // TextOverflowModes.Truncate -- wrong for the founding card, whose full instruction
            // MUST read. We do NOT call FitBlock; instead the label WRAPS + AUTO-SIZES DOWN to
            // fit its rect (bounded [FlavorFontMin..FontBody]) with Overflow mode so a tail can
            // never be clipped. The rect is TALL and now PLATE-local (0.02-0.82 of the plate,
            // x[0.46-1.00] ~= 743x261 units at 2670x1200): the ~320-char founding flavor
            // auto-sizes into the 28-34 legible band and fits whole; the "Tell me more" lore
            // (shorter, ~290 chars) fits with headroom. Because the rect is now measured off
            // the plate, an Overflow tail spills into the plate's own margin -- never onto the
            // metal frame, which is where the last line was landing before.
            _flavorLabel = ElarionUiKit.Label(well, entry.Flavor, 0.02f, 0.82f,
                ElarionUi.Parchment, ElarionUi.FontBody, TextAlignmentOptions.TopLeft,
                0.46f, 1.00f, bold: false);
            _flavorLabel.textWrappingMode = TextWrappingModes.Normal;   // wrap, don't spill wide
            _flavorLabel.overflowMode = TextOverflowModes.Overflow;     // NEVER cut the tail
            _flavorLabel.enableAutoSizing = true;                       // shrink-to-fit the rect
            _flavorLabel.fontSizeMin = FlavorFontMin;                   // legible mobile floor
            _flavorLabel.fontSizeMax = ElarionUi.FontBody;              // never grow past body

            // -- RIGHT/BOTTOM: the two CARD buttons, side-by-side flanking the shared Close
            //    (the kit's one bottom-center Close is the single dismiss; no card "Dismiss").
            //    Each is 0.315w (~531px) x 0.195h (~118px, above MinTouchPx=112) with a clear
            //    x-gap to the 360px Close box on either side. Bottom row y[0.05-0.245] sits
            //    entirely below the plate floor -- text can never land on a button.
            //
            //    UNIFORM SHAPE (owner F8 2026-08-05: "one rounded, one square, Close square").
            //    ButtonKind is a back-compat shim (ElarionUiKit.cs:1343-1353) that maps
            //    Confirm->(Style2,Green) and Quiet->(Style1,Gray) -- and ObsidianButtonSpriteName
            //    resolves button2_* ROUNDED vs button1_* SQUARE. So Accept came out rounded while
            //    Tell me more AND the shared Close (which hardcodes Style1) came out square:
            //    three buttons, two shapes. Style carries no meaning on this card (the label
            //    does), so call the obsidian builder DIRECTLY with a uniform Style1 and let
            //    COLOUR alone carry emphasis. Same precedent as FoundingChoiceController.
            ElarionUiKit.BuildObsidianButton(content, "I accept your power",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Green,
                new Vector2(0.045f, 0.05f), new Vector2(0.36f, 0.245f), OnAccept);
            _tellMoreBtn = ElarionUiKit.BuildObsidianButton(content, "Tell me more",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.64f, 0.05f), new Vector2(0.955f, 0.245f), OnTellMore);
        }

        // -- button handlers --------------------------------------------------------
        private void OnAccept()
        {
            FlowTrace.Step("Echo", $"unlock dialogue: 'I accept your power' id={_entry?.Id}");
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
            if (_emergenceCanvas != null) { Retire(_emergenceCanvas); _emergenceCanvas = null; }
            if (_canvas != null) { Retire(_canvas); _canvas = null; }
            Retire(gameObject);
        }

        /// <summary>Destroy that is safe in BOTH modes: runtime Destroy in play mode,
        /// DestroyImmediate in edit mode (the headless UI-capture harness drives the
        /// emergence advance outside play mode, where runtime Destroy errors).</summary>
        private static void Retire(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
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
