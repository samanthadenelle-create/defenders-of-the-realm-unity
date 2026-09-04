#if UNITY_EDITOR
// =============================================================================
// EndStateBodyFitRegression — WO-952. THE `COMPRESSED`-ABSENCE ORACLE THAT WAS
// SPECCED IN AUGUST AND NEVER WRITTEN.
// -----------------------------------------------------------------------------
// ⛔ READ THIS BEFORE WEAKENING ANYTHING HERE. WO-952 §2 bullet 3 (2026-08-10)
// asked for an oracle asserting the ABSENCE of the `body rows COMPRESSED to fit`
// line. KEY_FACTS.md flagged the same day that it did not exist. The ticket was
// nevertheless flipped to DONE by an AUDIT on 2026-08-21 — an audit, not device
// evidence — and on 2026-09-04 `grep -rn "COMPRESSED" Assets/Editor/Regression/`
// still returned ZERO hits. So the bug walked back in and THE OWNER found it,
// on the production candidate, instead of a gate:
//
//   [Flow:EndState] body rows COMPRESSED to fit: need=578px well=540px scale=0.933
//     - the panel hit its screen-height clamp; every band is now below its own
//       content size
//   (F8 seq 4680, SM02G4061955851, 2026-09-04T14:35:49Z, arena victory with a
//    gear drop — 5 spoils rows, panel 907px = frac 0.94, pinned at MaxPanelHalf)
//
// ⭐ THE TRIGGER IS THE GEAR DROP. Four spoils rows fit at 496 = 496 px, scale
// 1.000. The fifth row cannot share a band at 2 columns, so the grid goes 2 bands
// -> 3, the need goes 496 -> 578 px, and the panel grows into its clamp where the
// well stops at 540 px. Every band then compresses to 0.933 — below its own
// content's fixed size, which is the exact class the fixed-px band law exists to
// prevent. The WAVE-CLEAR path is clean (340 = 340, scale 1) — the 2026-08-10 fix
// holds; this is arena-with-gear only, which is why Case 1 below is an ARENA case
// and a wave-clear case would have proven nothing.
//
// ⭐ HOW TO SEE IT RED (the WO-1138 rule: a test that has never failed proves
// nothing). Set EndStateView.MaxSpoilColumns to 2 — that is precisely the pre-fix
// engine — and Case 1 fails with `scale=0.933 need=578 well=540`, the captured
// numbers to the pixel. Case 2 documents the same arithmetic as data, so the RED
// state is legible without editing anything at all.
//
// ⛔ IT ASKS THE SHIPPED SOLVER, IT DOES NOT RE-DERIVE IT. Every number comes from
// EndStateView.ProbeFit, which runs SpoilColumns / RequiredBodyPxAt /
// PanelHalfHeight and Bind's own well derivation. A suite that recomputed the band
// budget would be duplicated state and would drift exactly the way this WO's first
// "DONE" did.
//
// ⚠ WHAT THIS SUITE CANNOT DO, said plainly. It proves the GEOMETRY SOLVE fits. It
// cannot prove the panel LOOKS right — glyph rendering, art seating, the staged
// reveal. That needs eyes: `UICaptureLaunch.CaptureEndStateWaveClear` +
// `UI_ENDSTATE_FIT_OK`, and the EndState case at the device's real 2670x1200 that
// WO-952 also asked for. This suite closes the half that a gate can hold.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DeNelle.Core.UI;
using DeNelle.Village.UI;

namespace DeNelle.Editor.Regression
{
    /// <summary>WO-952: no shipped end-state may compress its body bands below their own content
    /// size. The absence of the `COMPRESSED to fit` condition IS the acceptance signal.</summary>
    public static class EndStateBodyFitRegression
    {
        // The three surfaces the capture set already renders at, plus the one the owner plays on.
        private static readonly (int w, int h, string name)[] Surfaces =
        {
            (2670, 1200, "Seeker (her device)"),
            (2340, 1080, "common Android landscape"),
            (1920, 1080, "16:9 desktop"),
            (1080, 1920, "portrait"),
        };

        public static bool Run(out string result)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            try
            {
                TheCapturedArenaVictoryFits(failures, log);
                TheSpoilsLadderNeverCompresses(failures, log);
                TheGuardsThatMakeTheAboveMeanSomething(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add("[endstate-fit] the suite itself threw: " + ex);
            }
            finally
            {
                try { ElarionUiKit.ClearSurfaceOverride(); } catch { }
            }

            if (failures.Count > 0)
            {
                result = "EndState body fit BROKEN:\n  - " + string.Join("\n  - ", failures);
                return false;
            }
            result = "no end-state body compresses below its own content size on any shipped " +
                     "surface, including the gear-drop arena victory that reached the owner.\n" +
                     log.ToString().TrimEnd();
            return true;
        }

        // ---------------------------------------------------------------------
        //  CASE 1 — ⭐ THE CAPTURE, TO THE PIXEL. The exact VM shape behind
        //  F8 seq 4680, on her device's surface.
        // ---------------------------------------------------------------------
        private static void TheCapturedArenaVictoryFits(List<string> failures, StringBuilder log)
        {
            // Post-scale canvas height MEASURED off the capture: panel 907px at frac 0.94 means
            // 907 / (2 * 0.47) = 965 ref px. Pinned, so this is the number the clamp produced.
            const float CanvasH = 965.4f;

            ElarionUiKit.SetSurfaceOverride(2670, 1200);
            try
            {
                var vm = ArenaVictoryFixture(spoils: 5);
                var fit = EndStateView.ProbeFit(vm, CanvasH);

                if (fit.Scale < EndStateView.CompressFailBelowFrac)
                    failures.Add($"[arena-gear] ⛔ THE WO-952 DEFECT IS BACK: a 5-row arena victory " +
                                 $"(gear drop) compresses to scale={fit.Scale:0.###} " +
                                 $"(need={fit.NeedPx:0}px well={fit.WellPx:0}px, panel {fit.PanelPx:0}px " +
                                 $"= frac {fit.PanelFrac:0.###}, {fit.Columns} column(s), " +
                                 $"{fit.SpoilBands} spoils band(s)). Every band is now BELOW its own " +
                                 "content's fixed size - the subtitle under its line box, the stars " +
                                 "under their bbox, the rows under their icon. The owner saw this on " +
                                 "the production candidate. ⛔ The fix is to REFLOW (more spoils " +
                                 "columns), never to shrink, and never to raise MaxPanelHalf - at " +
                                 "0.47 against a 0.50 centre the panel already spans the documented " +
                                 "0.03..0.97 and any more clips the top edge (WO-894).");
                else
                    log.AppendLine($"  [arena-gear] 5-row arena victory at 2670x1200: need={fit.NeedPx:0}px " +
                                   $"well={fit.WellPx:0}px scale={fit.Scale:0.###} " +
                                   $"({fit.Columns} columns, {fit.SpoilBands} bands, panel " +
                                   $"frac {fit.PanelFrac:0.###})");

                // The 4-row victory that ALREADY worked must be untouched: same 2-column layout,
                // same 638px panel, scale 1. A "fix" that reflows everything is a different screen.
                var four = EndStateView.ProbeFit(ArenaVictoryFixture(spoils: 4), CanvasH);
                if (four.Columns != 2)
                    failures.Add($"[arena-4row] the 4-row arena victory now solves at {four.Columns} " +
                                 "columns. It fitted at 2 (496 = 496px, scale 1.000, panel frac 0.661) " +
                                 "on 2026-09-04 and its layout is the WO-894 ruling - nothing may " +
                                 "reflow unless it does not fit.");
                if (four.Scale < EndStateView.CompressFailBelowFrac)
                    failures.Add($"[arena-4row] the 4-row victory regressed into compression " +
                                 $"(scale={four.Scale:0.###}).");
                else
                    log.AppendLine($"  [arena-4row] unchanged: {four.Columns} columns, " +
                                   $"need={four.NeedPx:0}px well={four.WellPx:0}px scale={four.Scale:0.###}");
            }
            finally { ElarionUiKit.ClearSurfaceOverride(); }
        }

        // ---------------------------------------------------------------------
        //  CASE 2 — the whole spoils ladder, on every shipped surface. The
        //  capture was ONE row count on ONE device; the defect class is
        //  "content taller than the clamp", so sweep it.
        // ---------------------------------------------------------------------
        private static void TheSpoilsLadderNeverCompresses(List<string> failures, StringBuilder log)
        {
            foreach (var s in Surfaces)
            {
                ElarionUiKit.SetSurfaceOverride(s.w, s.h);
                try
                {
                    // The canvas the kit resolves for this surface, in the same reference space
                    // PanelHalfHeight works in. 965.4 is the measured Seeker value; the others
                    // scale with the short axis exactly as the ScaleWithScreenSize scaler does.
                    float canvasH = 965.4f * (Mathf.Min(s.w, s.h) / 1200f);
                    if (s.h > s.w) canvasH = 1920f;   // portrait: the kit's own reference height

                    for (int rows = 1; rows <= 6; rows++)
                    {
                        var fit = EndStateView.ProbeFit(ArenaVictoryFixture(rows), canvasH);
                        if (fit.Scale < EndStateView.CompressFailBelowFrac)
                            failures.Add($"[ladder/{s.name}] {rows} spoils row(s) compress to " +
                                         $"scale={fit.Scale:0.###} (need={fit.NeedPx:0}px " +
                                         $"well={fit.WellPx:0}px, panel frac {fit.PanelFrac:0.###}, " +
                                         $"{fit.Columns} column(s)). " +
                                         (fit.PinnedAtClamp
                                            ? "The panel is PINNED at MaxPanelHalf, so the well cannot " +
                                              "grow to meet the need - reflow the grid or cut content; " +
                                              "do NOT raise the clamp and do NOT let the bands shrink."
                                            : "The panel is NOT at its clamp, so this is a solve bug " +
                                              "rather than a content-too-tall case - the panel should " +
                                              "have grown to fit."));
                    }
                }
                finally { ElarionUiKit.ClearSurfaceOverride(); }
            }
            log.AppendLine("  [ladder] 1-6 spoils rows x " + Surfaces.Length +
                           " shipped surfaces: no case compresses");
        }

        // ---------------------------------------------------------------------
        //  CASE 3 — ⛔ SILENCE MUST NOT PASS, AND THE FIX MUST NOT BE A CHEAT.
        //  This suite is only worth its runtime if the probe is really wired to
        //  the shipped solver and the escape hatch really has a floor.
        // ---------------------------------------------------------------------
        private static void TheGuardsThatMakeTheAboveMeanSomething(List<string> failures, StringBuilder log)
        {
            // A probe that answers zeros would make every case above pass vacuously.
            ElarionUiKit.SetSurfaceOverride(2670, 1200);
            try
            {
                var fit = EndStateView.ProbeFit(ArenaVictoryFixture(5), 965.4f);
                if (fit.NeedPx <= 1f || fit.WellPx <= 1f || fit.PanelPx <= 1f || fit.SpoilBands < 1)
                    failures.Add("[guard] EndStateView.ProbeFit returned a degenerate solve " +
                                 $"(need={fit.NeedPx:0} well={fit.WellPx:0} panel={fit.PanelPx:0} " +
                                 $"bands={fit.SpoilBands}). Zeros make every assertion above pass " +
                                 "without measuring anything - that is the hollow-green class this " +
                                 "suite exists to end.");

                // The reflow must stay inside the LEGIBILITY floor, or it has simply moved the
                // defect from "too short" to "too narrow" (MinSpoilColumnPx is derived: the
                // longest stock label, 'Experience', needs a 407px cell to stay off the font floor).
                if (fit.Columns > 3)
                    failures.Add($"[guard] the spoils grid reflowed to {fit.Columns} columns. Past 3 a " +
                                 "reward plate stops reading as a line item and becomes a tile grid - " +
                                 "and the WO-894 ruling that made two columns legal was about " +
                                 "READABILITY, not about winning height at any price.");

                // Portrait must stay single-column, as ruled: at 1080x1920 a column is ~269px,
                // under the 420px floor.
                ElarionUiKit.ClearSurfaceOverride();
                ElarionUiKit.SetSurfaceOverride(1080, 1920);
                var portrait = EndStateView.ProbeFit(ArenaVictoryFixture(5), 1920f);
                if (portrait.Columns != 1)
                    failures.Add($"[guard] PORTRAIT solved to {portrait.Columns} spoils columns. A portrait " +
                                 "column is ~269 ref px against a 420px legibility floor, so multi-column " +
                                 "portrait was ruled out in WO-894 and the width floor is what enforces it.");
                else
                    log.AppendLine("  [guard] portrait stays single-column; the reflow is width-gated, " +
                                   "not height-driven");
            }
            finally { ElarionUiKit.ClearSurfaceOverride(); }
        }

        // ---------------------------------------------------------------------
        //  THE FIXTURE — the arena victory VM, built by hand.
        // ---------------------------------------------------------------------
        //  ⚠ NOT EndStateVM.FromBattleVictory, and the reason matters: that factory
        //  resolves its emblem and row icons through RpgUiCatalog / ItemIconCatalog,
        //  which can return null in a headless editor run. A null Emblem silently
        //  drops a 64px band from the budget, so the suite would measure a DIFFERENT
        //  panel from the one that shipped and would pass while the screen failed.
        //  UICaptureLaunch's EndState cases build fixtures for exactly this reason.
        //
        //  The shape is the capture's: emblem + one-line subtitle + stars + time +
        //  N spoils rows, which reproduces need=578px at 2 columns to the pixel.
        // ---------------------------------------------------------------------
        private static EndStateVM ArenaVictoryFixture(int spoils)
        {
            var vm = new EndStateVM
            {
                Kind = EndStateKind.Victory,
                Title = "Victory!",
                Subtitle = "The realm is safer because of you!",
                Stars = 3,
                TimeSeconds = 41f,
                Emblem = OnePixelSprite(),
                PrimaryLabel = "Continue",
                PrimaryRoute = "return-home",
                Compact = false,
                HoldWorld = true,
            };
            // Descending importance, exactly as FromBattleVictory builds them; the 5th is the
            // GEAR DROP that tipped the panel past its clamp.
            var rows = new (string label, string amount)[]
            {
                ("Experience", "+60"),
                ("Gold",       "+120"),
                ("Wood",       "+33"),
                ("Iron",       "+15"),
                ("Emberglass Staff", "Equipped"),
                ("Wisdom",     "+4"),
            };
            for (int i = 0; i < Mathf.Clamp(spoils, 0, rows.Length); i++)
                vm.Spoils.Add(new SpoilRowVM { Label = rows[i].label, Amount = rows[i].amount });
            return vm;
        }

        private static Sprite _emblem;

        /// <summary>A real, non-null Sprite so the emblem band is BUDGETED. Its pixels are never
        /// drawn — only `vm.Emblem != null` is read by the solve.</summary>
        private static Sprite OnePixelSprite()
        {
            if (_emblem != null) return _emblem;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.Apply();
            _emblem = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            _emblem.hideFlags = HideFlags.HideAndDontSave;
            return _emblem;
        }
    }
}
#endif
