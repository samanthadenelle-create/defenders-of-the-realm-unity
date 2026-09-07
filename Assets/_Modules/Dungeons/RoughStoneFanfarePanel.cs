// =============================================================================
// RoughStoneFanfarePanel (WO-1596) - the VIEW half of the rough-stone fanfare.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Dungeons   Namespace: DeNelle.Dungeons
//
// THE DEFECT THIS CLOSES (device log, 2026-09-07 09:44:07):
//     [Flow:JewelPolish] run payout (composed exit): 1x 'ing_rough_stone' granted
//     [Flow:DungeonExit] taking RETURN exit -> SceneRouter.Castle
// Ten milliseconds apart, with nothing on screen in between. The first Rough Stone
// is guaranteed exactly once per player, it is the door to the Jeweler and the
// Rings of Power, and the player learned it from a log line she could not read.
// The treasure cache, a strictly smaller moment, already gets a modal.
//
// WHAT THIS IS
//   A FULL-SCREEN beat (safe-area inset only - owner ruling 2026-09-07 01:14,
//   "i expect these images to fill the screen, not 60% of it", which is why the
//   inset here is the SAME 0.02 ManageScreenPanel uses and not a modal margin).
//   Stone art large and centred, the kit's display face for the title, ONE line of
//   meaning, the polish score as stars AND as words, and exactly ONE verb.
//
// WHAT THIS IS NOT
//   * It is NOT a producer. DungeonController.GrantRunPayout banks the stone; by
//     the time this opens the player already owns it. This file contains no
//     inventory call of any kind and the regression lints the source for that.
//   * It does NOT decide whether the player earned anything. It renders a VM.
//
// KIT LAWS OBSERVED (same set DungeonTreasurePanel is held to)
//   * Built through ElarionUiKit only - UiObsidianConformanceRegression runs
//     HardFailOnNew, so a new file that hand-rolls raw uGUI FAILS the gate.
//   * ONE exit. No shared Close, no scrim dismiss: the single verb is the only way
//     out, because a linear reward beat with two exits reads as one choice offered
//     twice (owner F8 seq 628).
//   * ASCII-only source and copy - TMP renders anything else as tofu on device.
//   * Meaning never by colour: the stars print "2 of 3" as WORDS beside the glyph
//     row, so the grade survives a red/green colourblind read.
//
// BUILD vs SHOW - and why they are separate
//   Build(vm, onDismiss) constructs the screen with kit calls ONLY and returns the
//   canvas. Show(vm, onDismiss) is Build + the PanelManager arbiter + arming the
//   pending dismiss. The headless capture calls BUILD, so a frame can be shot in
//   edit mode without the arbiter (which can legitimately REJECT an open under the
//   WO-437 battle lock) deciding whether a screenshot exists.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Manage;   // ManageArt.LoadSprite - the shared Resources sprite seam
using DeNelle.Core.UI;

namespace DeNelle.Dungeons
{
    /// <summary>The full-screen "A ROUGH STONE" moment shown the instant a run pays one out.</summary>
    public static class RoughStoneFanfarePanel
    {
        private const string Sys = "JewelPolish";
        private const string PanelName = "RoughStoneFanfare";

        /// <summary>
        /// THE authored geometry, in ONE place, so the screen and its regression read the same
        /// numbers (a second copy is how the numbers drift - CLAUDE.md sec.5). Every band is
        /// (xMin, yMin, xMax, yMax) as FRACTIONS OF THE PANEL RECT, y bottom-to-top.
        /// </summary>
        public static class Layout
        {
            /// <summary>
            /// FULL BLEED. The 0.02 inset on every edge is the DEVICE SAFE AREA, not a margin -
            /// it keeps the obsidian frame's border off a rounded corner and out of a notch. It is
            /// the same constant ManageScreenPanel.ManagePanelInsetF uses, for the same reason and
            /// under the same owner ruling; the fanfare must not read as a smaller moment than the
            /// screen the player reaches it from.
            /// </summary>
            public const float SafeAreaInsetF = 0.02f;

            public static readonly Vector2 PanelMin = new Vector2(SafeAreaInsetF, SafeAreaInsetF);
            public static readonly Vector2 PanelMax = new Vector2(1f - SafeAreaInsetF, 1f - SafeAreaInsetF);

            /// <summary>Band 1 - the gold title. The KIT owns this rect: BuildObsidianPanel seats
            /// chrome.title inside FrameCore's header zone, so this constant MIRRORS that zone
            /// rather than competing with it (the WO-1228 collision-1 lesson).</summary>
            public static readonly Vector4 TitleBand = new Vector4(0.24f, 0.900f, 0.88f, 0.972f);

            /// <summary>Band 2 - the stone itself, large and centred. This is the moment.</summary>
            public static readonly Vector4 ArtBand = new Vector4(0.30f, 0.480f, 0.70f, 0.860f);

            /// <summary>Band 3 - the stone's own name, under the art.</summary>
            public static readonly Vector4 NameBand = new Vector4(0.08f, 0.410f, 0.92f, 0.468f);

            /// <summary>Band 4 - ONE line: what it is and why it matters.</summary>
            public static readonly Vector4 MeaningBand = new Vector4(0.08f, 0.300f, 0.92f, 0.400f);

            /// <summary>Band 5 - the polish grade, as a glyph row AND as words.</summary>
            public static readonly Vector4 StarsBand = new Vector4(0.20f, 0.200f, 0.80f, 0.285f);

            /// <summary>Band 6 - the ONE verb. Authored 0.130 of the panel tall so the
            /// MinTouchPx floor is met BY CONSTRUCTION and ClampMinTouch never has to grow it
            /// into band 5 (the hero-select failure).</summary>
            public static readonly Vector4 CtaBand = new Vector4(0.18f, 0.045f, 0.82f, 0.175f);

            /// <summary>Panel height in POST-SCALE canvas units.</summary>
            public static float PanelHeightPx(float canvasHeightPx)
            {
                return (PanelMax.y - PanelMin.y) * canvasHeightPx;
            }

            /// <summary>CTA height in canvas units - compare against ElarionUiKit.MinTouchPx.</summary>
            public static float CtaHeightPx(float canvasHeightPx)
            {
                return (CtaBand.w - CtaBand.y) * PanelHeightPx(canvasHeightPx);
            }

            /// <summary>The six named bands, in draw order. The regression asserts they are
            /// pairwise NON-intersecting.</summary>
            public static Vector4[] Bands()
            {
                return new[] { TitleBand, ArtBand, NameBand, MeaningBand, StarsBand, CtaBand };
            }

            /// <summary>Names parallel to <see cref="Bands"/> (for failure messages).</summary>
            public static string[] BandNames()
            {
                return new[] { "title", "art", "name", "meaning", "stars", "cta" };
            }

            /// <summary>Half-open rect intersection on (xMin,yMin,xMax,yMax) fractions.</summary>
            public static bool Intersect(Vector4 a, Vector4 b)
            {
                return a.x < b.z && b.x < a.z && a.y < b.w && b.y < a.w;
            }
        }

        private static GameObject s_canvas;
        private static PanelHandle s_handle;

        // The pending dismiss, held only while the screen is live. EVERY way this beat can
        // end - the verb, or PanelManager swapping us out - must run it exactly once, or the
        // exit route it carries never fires and the player is stranded in the dungeon.
        private static Action s_onDismiss;

        /// <summary>True while the fanfare is on screen.</summary>
        public static bool IsOpen { get { return s_canvas != null; } }

        /// <summary>The star row, as ASCII. Filled '*', empty '-', so the grade reads without hue.</summary>
        public static string StarRow(int stars, int max)
        {
            if (max <= 0) return "";
            if (stars < 0) stars = 0;
            if (stars > max) stars = max;
            var sb = new System.Text.StringBuilder(max * 2);
            for (int i = 0; i < max; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(i < stars ? '*' : '-');
            }
            return sb.ToString();
        }

        /// <summary>The same grade IN WORDS - the colourblind-safe half of band 5.</summary>
        public static string StarWords(int stars, int max)
        {
            if (stars < 0) stars = 0;
            if (stars > max) stars = max;
            return "Polish " + stars + " of " + max;
        }

        /// <summary>
        /// CONSTRUCT the screen with kit calls only and return its canvas. No arbiter, no
        /// static state - so the headless capture can shoot a frame in edit mode and a
        /// battle-lock rejection can never be the reason a screenshot does not exist.
        /// Returns null (loudly) when the kit produces no usable chrome.
        /// </summary>
        public static GameObject Build(RoughStoneFanfareVM vm, Action onDismiss)
        {
            if (vm == null)
            {
                FlowTrace.Fail(Sys, "ROUGH STONE FANFARE build refused - null VM (nothing to render).");
                return null;
            }

            var modal = ElarionUiKit.BuildObsidianModal(
                PanelName, vm.Title,
                Layout.PanelMin, Layout.PanelMax,
                onClose: null, sortingOrder: 34100,
                frameName: RpgUiCatalog.FrameCore);
            if (modal == null || modal.canvas == null || modal.chrome == null || modal.chrome.content == null)
            {
                FlowTrace.Fail(Sys, "BuildObsidianModal returned no usable chrome - ROUGH STONE FANFARE NOT shown.");
                if (modal != null && modal.canvas != null) UnityEngine.Object.Destroy(modal.canvas);
                return null;
            }

            var content = modal.chrome.content.transform;

            // ONE exit: retire the shared Close so the verb is the only way out.
            if (modal.chrome.close != null) modal.chrome.close.gameObject.SetActive(false);

            // Geometry comes from the POST-SCALE canvas height, never rect.height: on a canvas's
            // creation frame the CanvasScaler has not applied and rect.height returns RAW SCREEN
            // pixels (the F8-5 DlgLayout capture, 1351 vs the real 1047).
            float canvasH = ElarionUiKit.PostScaleCanvasHeight(content);

            // A full-bleed obsidian field behind everything, so the world does not show through
            // FrameCore's transparent centre during the beat (the Manage body-fill precedent).
            var fill = ElarionUiKit.AddImage(content, "FanfareFill",
                Vector2.zero, Vector2.one, ElarionUiKit.ObsidianFill, rounded: false);
            if (fill != null)
            {
                var fillImage = fill.GetComponent<Image>();
                if (fillImage != null) fillImage.raycastTarget = false;
                fill.transform.SetAsFirstSibling();
            }

            // -- BAND 2: the stone, large and centred --------------------------------
            BuildStoneArt(content, vm);

            // -- BAND 3: the stone's own name ----------------------------------------
            var nameLabel = ElarionUiKit.Label(content, vm.StoneName,
                Layout.NameBand.y, Layout.NameBand.w,
                ElarionUi.Gilt, ElarionUi.FontHead, TextAlignmentOptions.Center,
                Layout.NameBand.x, Layout.NameBand.z, bold: true);
            ElarionUiKit.FitSingleLine(nameLabel);

            // -- BAND 4: ONE line of meaning -----------------------------------------
            ElarionUiKit.Label(content, vm.Meaning,
                Layout.MeaningBand.y, Layout.MeaningBand.w,
                ElarionUi.Parchment, ElarionUi.FontBody, TextAlignmentOptions.Center,
                Layout.MeaningBand.x, Layout.MeaningBand.z);

            // -- BAND 5: the grade, as glyphs AND as words ---------------------------
            // Two readings of one fact, deliberately: the row is the felt one, the words are
            // the one that survives a colourblind read and a small screen.
            var starRow = ElarionUiKit.Label(content, StarRow(vm.Stars, vm.MaxStars),
                Layout.StarsBand.y + 0.5f * (Layout.StarsBand.w - Layout.StarsBand.y), Layout.StarsBand.w,
                ElarionUi.Gold, ElarionUi.FontHead, TextAlignmentOptions.Center,
                Layout.StarsBand.x, Layout.StarsBand.z, spacing: 12f, bold: true);
            ElarionUiKit.FitSingleLine(starRow);

            var starWords = ElarionUiKit.Label(content, StarWords(vm.Stars, vm.MaxStars),
                Layout.StarsBand.y, Layout.StarsBand.y + 0.5f * (Layout.StarsBand.w - Layout.StarsBand.y),
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TextAlignmentOptions.Center,
                Layout.StarsBand.x, Layout.StarsBand.z);
            ElarionUiKit.FitSingleLine(starWords);

            // -- BAND 6: the ONE verb -------------------------------------------------
            ElarionUiKit.Button(content, vm.CtaLabel, ElarionUiKit.ButtonKind.Gold,
                new Vector2(Layout.CtaBand.x, Layout.CtaBand.y),
                new Vector2(Layout.CtaBand.z, Layout.CtaBand.w),
                CloseAndDismiss);

            WarnIfBandsCannotSeatContent(canvasH);

            // TODO (WO-1596 follow-up, needs an owner tag - memory `vfx-map-owner-tags-no-creative-pick`):
            //   AUDIO. The celebration sting that fits is GameSfx.PlayLevelUp ("Sfx/LevelUp"), but
            //   GameSfx is `internal static` inside DeNelle.Village and DeNelle.Dungeons cannot
            //   reach it. Closing this is a one-line PUBLIC seam on the Village side, which is
            //   outside this WO's stated file scope - it is NOT an invented id. The kit's own
            //   button already plays IAudioService.PlayUiClick on the verb, so the beat is not
            //   silent at the tap; it is silent at the OPEN.
            //   VFX. No owner-tagged celebration/marquee hook exists in the tree (searched
            //   2026-09-07). Nothing is invented here; the hook is an owner tag, not a CLI pick.

            // ⚠ THIS SAYS **BUILT**, NOT "shown", AND THE DIFFERENCE IS THE ACCEPTANCE CRITERION.
            // WO-1596 is closed by a device log reading shown -> dismissed -> scene route, in that
            // order. Build runs in the headless capture and it runs a moment BEFORE the arbiter
            // may still REJECT the open - so if "shown" were emitted here, an edit-mode screenshot
            // or a battle-locked rejection would forge the first token of the proof. "shown" is
            // emitted by Show, after NotifyOpened accepts, and nowhere else.
            FlowTrace.Step(Sys, "ROUGH STONE FANFARE built " + vm.TraceSummary
                + " canvasH=" + canvasH.ToString("0")
                + " panelH=" + Layout.PanelHeightPx(canvasH).ToString("0")
                + " ctaH=" + Layout.CtaHeightPx(canvasH).ToString("0.0")
                + " (minTouch=" + ElarionUiKit.MinTouchPx.ToString("0") + ")");

            s_canvas = modal.canvas;
            s_onDismiss = onDismiss;   // Build's caller (the capture) passes null and never arms a route
            return modal.canvas;
        }

        /// <summary>
        /// Present the fanfare and take ownership of what happens next.
        /// <paramref name="onDismiss"/> runs EXACTLY ONCE, when the beat ends (the verb, or an
        /// arbiter-forced close - this screen has no other dismiss).
        /// <para>Returns TRUE when the screen is live and therefore owns the continuation;
        /// FALSE when it refused to open (duplicate Show, unusable chrome, arbiter rejection),
        /// in which case THE CALLER STILL OWNS CONTINUING. That contract is what keeps a failed
        /// fanfare from becoming a dead exit.</para>
        /// </summary>
        public static bool Show(RoughStoneFanfareVM vm, Action onDismiss)
        {
            if (s_canvas != null)
            {
                FlowTrace.Warn(Sys, "ROUGH STONE FANFARE already open - ignoring duplicate Show.");
                return false;
            }

            // Armed only AFTER the arbiter accepts: NotifyOpened can REJECT (WO-437 battle-lock)
            // and invokes the handle's Close on its way out, so arming first would run the
            // continuation inside the rejection AND leave the caller running it again on our false.
            s_onDismiss = null;
            if (Build(vm, null) == null) return false;
            s_onDismiss = null;

            if (s_handle == null) s_handle = PanelManager.Register(PanelName, CloseAndDismiss, () => IsOpen);
            if (!PanelManager.NotifyOpened(s_handle))
            {
                FlowTrace.Warn(Sys, "PanelManager rejected the ROUGH STONE FANFARE (battle-lock) - " +
                                    "the caller must continue directly; the stone is already banked.");
                Teardown();
                return false;
            }
            s_onDismiss = onDismiss;

            // THE FIRST TOKEN OF THE ACCEPTANCE PROOF. Emitted here and only here: the screen is
            // built, the arbiter has accepted it, and the continuation is armed - so a device log
            // carrying this line really did put the moment in front of the player.
            FlowTrace.Step(Sys, "ROUGH STONE FANFARE shown " + vm.TraceSummary);
            return true;
        }

        /// <summary>The ONE way this beat ends: tear it down, then run the pending continuation.
        /// Wired to both the verb and the arbiter's forced close.</summary>
        private static void CloseAndDismiss()
        {
            var pending = s_onDismiss;
            s_onDismiss = null;                 // consume FIRST - a re-entrant close cannot route twice
            Teardown();
            FlowTrace.Step(Sys, "ROUGH STONE FANFARE dismissed - continuing the exit"
                                + (pending == null ? " (no continuation was armed)" : ""));
            // Continue AFTER teardown so a throwing continuation can never leave the screen wedged.
            if (pending != null) Guard.Try(Sys, "rough stone fanfare dismiss continuation", () => pending());
        }

        /// <summary>Destroy the screen and release the arbiter. Never continues; idempotent.</summary>
        private static void Teardown()
        {
            if (s_canvas != null)
            {
                UnityEngine.Object.Destroy(s_canvas);
                s_canvas = null;
            }
            if (s_handle != null) PanelManager.NotifyClosed(s_handle);
        }

        /// <summary>
        /// Band 2. Walks the VM's art candidates through the shared Resources sprite seam and
        /// paints the first that resolves. When NONE resolves it draws a procedural kit disc with
        /// the material's own ASCII glyph centred in it.
        /// <para>It deliberately does NOT borrow another item's icon: painting a crystal under
        /// "A ROUGH STONE" would tell the player she earned a different thing. The honest miss is
        /// a glyph, and the ART ASK is named in the VM.</para>
        /// </summary>
        private static void BuildStoneArt(Transform content, RoughStoneFanfareVM vm)
        {
            var well = ElarionUiKit.AddImage(content, "StoneArtWell",
                new Vector2(Layout.ArtBand.x, Layout.ArtBand.y),
                new Vector2(Layout.ArtBand.z, Layout.ArtBand.w),
                new Color(0f, 0f, 0f, 0f), rounded: false);
            if (well == null) return;

            Sprite art = null;
            string resolvedKey = null;
            if (vm.ArtKeys != null)
            {
                for (int i = 0; i < vm.ArtKeys.Count && art == null; i++)
                {
                    string key = vm.ArtKeys[i];
                    if (string.IsNullOrEmpty(key)) continue;
                    string captured = key;
                    Guard.Try(Sys, "load rough stone art '" + captured + "'", () =>
                    {
                        var s = ManageArt.LoadSprite(captured);
                        if (s != null) { art = s; resolvedKey = captured; }
                    });
                }
            }

            if (art != null)
            {
                var plate = ElarionUiKit.AddImage(well.transform, "StoneArt",
                    Vector2.zero, Vector2.one, Color.white, rounded: false);
                var img = plate != null ? plate.GetComponent<Image>() : null;
                if (img != null)
                {
                    img.sprite = art;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                }
                FlowTrace.Step(Sys, "ROUGH STONE FANFARE art resolved from '" + resolvedKey + "'.");
                return;
            }

            // NO SILENT FALLBACK (CLAUDE.md sec.12): say out loud that the art is missing, name the
            // exact asset that would fill it, and draw something honest in the meantime.
            FlowTrace.Warn(Sys, "ROUGH STONE FANFARE has NO art for '" + vm.StoneId + "' - tried "
                + (vm.ArtKeys != null ? vm.ArtKeys.Count : 0) + " key(s). ART ASK: author "
                + "Assets/Resources/" + RoughStoneFanfareVM.PreferredArtKey + ".png. Drawing the "
                + "procedural disc + the material glyph '" + vm.Glyph + "' instead - deliberately NOT "
                + "another item's icon.");

            var disc = ElarionUiKit.AddImage(well.transform, "StoneGlyphDisc",
                Vector2.zero, Vector2.one, ElarionUi.AetherDim, rounded: false);
            var discImg = disc != null ? disc.GetComponent<Image>() : null;
            if (discImg != null)
            {
                discImg.sprite = ElarionUiKit.CircleSprite;
                discImg.preserveAspect = true;
                discImg.raycastTarget = false;
            }

            var glyph = ElarionUiKit.Label(well.transform, vm.Glyph, 0.18f, 0.82f,
                ElarionUi.Gilt, ElarionUi.FontTitle, TextAlignmentOptions.Center,
                0.10f, 0.90f, bold: true);
            if (glyph != null) glyph.enableAutoSizing = false;
        }

        /// <summary>
        /// NO SILENT FAILURES. The bands are authored disjoint and pinned by regression, but a
        /// canvas short enough to need ClampMinTouch's rescue on the verb is still a LAYOUT bug
        /// and must announce itself rather than shipping as a squint.
        /// </summary>
        private static void WarnIfBandsCannotSeatContent(float canvasH)
        {
            if (canvasH <= 1f) return;   // no meaningful canvas yet; the stacking is correct either way

            float ctaH = Layout.CtaHeightPx(canvasH);
            if (ctaH < ElarionUiKit.MinTouchPx)
            {
                FlowTrace.Warn(Sys, "the fanfare verb is " + ctaH.ToString("0.0") + "px tall, under the "
                    + ElarionUiKit.MinTouchPx.ToString("0") + "px touch floor - ClampMinTouch will GROW it, "
                    + "which is how a CTA walks into the band above it. Re-author CtaBand rather than "
                    + "relying on the rescue.");
            }
        }
    }
}
