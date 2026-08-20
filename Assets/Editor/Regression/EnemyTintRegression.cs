// =============================================================================
// EnemyTintRegression [enemy-tint]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// Pins the 2026-08-20 owner report: "enemies not having coloring."
//
// WHAT ACTUALLY WENT WRONG, from the captured device session
// (logs/device/enemy-color.log, pid 6783 — the ONLY session in that file that matters):
//
//   14:07:55.492  [Flow:TripoMatFix] NO ALBEDO on 'Orc_Shaman(Clone)' renderer
//                 'tripo_node_79fc0b70' slot 0: material='Orc_Shaman (URP)'
//                 shader='Universal Render Pipeline/Lit' tint=(0.45,0.30,0.20)
//                 - "the URP rebuild took but bound NO base map, so this mesh renders
//                    as flat tint. A SHADER-ONLY VERIFY CALLS THIS OK."
//
// That last clause is the whole defect class. EnemyFactory.VerifyVisualRenders proves a
// body has a MESH; nothing proved it has a COLOUR. And the colour code that does exist
// (the WO-790 basecolor/tint binds) is reachable only from three rig branches —
// OrcHumanoid, OrcWarband and the AccuRigIntake set. In the same capture, 16 of the 20
// enemy spawns were Skeleton_Minion (rig HumanoidMedium) or Skeleton_Rogue
// (SkeletonHumanoid): rigs with NO colour code and NO colour trace whatsoever. A body
// that renders white or grey produced not one line of evidence.
//
// ⚠ ONE HYPOTHESIS THIS FILE DELIBERATELY DOES NOT ENCODE, because the data killed it:
// "the late re-skin bypasses the recolour". It does NOT. EnemyLateSkinner re-runs
// EnemyFactory.TrySkinBody — the same method, same recipe — and the capture proves the
// binds fire on that path:
//   14:07:51.289 garrison albedo 'Enemies/TripoTex/Troll_basecolor' bound to 'Troll'
//   14:07:51.291 LATE RE-SKIN OK: 'Troll' ... 2.2s after spawn
// Both paths are covered here for the same reason: case [arm-covers-late-skin] pins that
// they stay the same ONE path, so a future edit cannot quietly fork them.
//
// CASES
//   1 [textured-passes]      A slot with a base map is Textured — never repainted. If this
//                            direction broke, the guard would paint over authored art.
//   2 [unpainted-fails]      A slot with NO base map and a WHITE albedo is Unpainted. This is
//                            the FAIL direction the owner reported: a body that skipped the
//                            recolour MUST be flagged, not waved through.
//   3 [grey-default-fails]   TripoMaterialFixer's own unpainted default is a flat 0.5 grey
//                            (TripoMaterialFixer.cs:121), not white. A brightness-based rule
//                            would have missed it; the chroma rule catches it. Pinned so
//                            nobody "simplifies" the classifier back into a whiteness test.
//   4 [painted-passes]       A deliberate family tint is Painted — a second repaint would
//                            multiply the colour and darken every enemy on every audit.
//   5 [emissive-exempt]      A glow slot is Emissive and left alone (skeleton eyes etc.).
//   6 [family-tints-chromatic] EVERY colour EnemyFactory.FamilyFallbackTint can return must
//                            itself classify as Painted. An achromatic "fix" is a no-op that
//                            the very next audit would re-flag — the repair must be visible.
//   7 [arm-covers-late-skin] EnemyFactory arms the guard inside TrySkinBody, and
//                            EnemyLateSkinner reaches the body only through TrySkinBody. That
//                            is what makes the spawn path and the re-skin path provably equal.
//   8 [miss-tint-floor]      The OrcHumanoid arm binds a miss-tint. It used to have no else at
//                            all: a model with no atlas got no texture, no tint, and no trace.
//
// Markers: ENEMY_TINT_OK / ENEMY_TINT_FAIL.
// Standalone: run-unity-method DeNelle.Editor.EnemyTintRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    /// <summary>Oracle for the enemy body-colour guard and the family tint authority.</summary>
    public static class EnemyTintRegression
    {
        private const string FactorySrc    = "Assets/_Modules/Village/Enemies/EnemyFactory.cs";
        private const string LateSkinSrc   = "Assets/_Modules/Village/Enemies/EnemyLateSkinner.cs";

        /// <summary>Batchmode entry: writes the OK/FAIL marker, exits 1 on failure.</summary>
        public static void RunAll()
        {
            bool ok;
            string reason;
            try
            {
                ok = Run(out reason);
            }
            catch (Exception ex)
            {
                ok = false;
                reason = "threw " + ex.GetType().Name + ": " + ex.Message;
            }

            Debug.Log((ok ? "ENEMY_TINT_OK " : "ENEMY_TINT_FAIL ") + reason);
            if (!ok && Application.isBatchMode) EditorApplication.Exit(1);
        }

        /// <summary>
        /// DataRegression-shaped contract. True when every case passes; <paramref name="reason"/>
        /// always carries a human-readable summary. Never throws — an unexpected exception is
        /// folded into a failure by RunAll.
        /// </summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();

            // ── 1 [textured-passes] ──────────────────────────────────────────
            var textured = EnemyBodyColorGuard.Classify(hasAlbedo: true, baseColor: Color.white, emissive: false);
            if (textured != EnemySlotColor.Textured)
                failures.Add($"[textured-passes] a slot WITH a base map classified {textured}, expected Textured — " +
                             "the guard would repaint authored art");
            else log.Append("[textured-passes] ok; ");

            // ── 2 [unpainted-fails] — THE OWNER'S SYMPTOM ────────────────────
            var white = EnemyBodyColorGuard.Classify(hasAlbedo: false, baseColor: Color.white, emissive: false);
            if (white != EnemySlotColor.Unpainted)
                failures.Add($"[unpainted-fails] a textureless WHITE slot classified {white}, expected Unpainted — " +
                             "a body that skipped the recolour would ship white and say nothing, which is the " +
                             "2026-08-20 report verbatim");
            else log.Append("[unpainted-fails] ok; ");

            // ── 3 [grey-default-fails] ───────────────────────────────────────
            var grey = EnemyBodyColorGuard.Classify(false, new Color(0.5f, 0.5f, 0.5f, 1f), false);
            if (grey != EnemySlotColor.Unpainted)
                failures.Add($"[grey-default-fails] TripoMaterialFixer's own 0.5 grey default classified {grey}, " +
                             "expected Unpainted — the classifier must key on CHROMA, not brightness, or the most " +
                             "common unpainted body in the project walks straight through it");
            else log.Append("[grey-default-fails] ok; ");

            // ── 4 [painted-passes] ───────────────────────────────────────────
            var umber = EnemyBodyColorGuard.Classify(false, HostilePalette.PlaceholderBodyTint, false);
            if (umber != EnemySlotColor.Painted)
                failures.Add($"[painted-passes] the hostile placeholder tint classified {umber}, expected Painted — " +
                             "a repainted body would be repainted again on the next audit and darken every pass");
            else log.Append("[painted-passes] ok; ");

            // ── 5 [emissive-exempt] ──────────────────────────────────────────
            var glow = EnemyBodyColorGuard.Classify(false, Color.white, emissive: true);
            if (glow != EnemySlotColor.Emissive)
                failures.Add($"[emissive-exempt] an emissive slot classified {glow}, expected Emissive — the guard " +
                             "would flatten glow/eye slots that legitimately carry no albedo");
            else log.Append("[emissive-exempt] ok; ");

            // ── 6 [family-tints-chromatic] ───────────────────────────────────
            // Every branch of the tint authority, by the id/model that reaches it. Achromatic
            // here means the "repair" repaints grey with grey — a silent no-op.
            var tintCases = new (string id, string model)[]
            {
                ("orc-berserker",  "Orc_Berserker"),
                ("troll",          "Troll"),
                ("caveman",        "Orc_Berserker"),
                ("ogre",           "Orc_Berserker"),
                ("ogre-mage",      "OgreMage"),
                ("orc-warlord",    "Orc_Necromancer"),
                ("orc-necromancer","Orc_Necromancer"),
                ("orc-shaman",     "Orc_Shaman"),
            };
            int chromatic = 0;
            foreach (var (id, model) in tintCases)
            {
                Color tint = EnemyFactory.FamilyFallbackTint(new EnemyDef { Id = id }, model);
                var verdict = EnemyBodyColorGuard.Classify(false, tint, false);
                if (verdict != EnemySlotColor.Painted)
                    failures.Add($"[family-tints-chromatic] FamilyFallbackTint('{id}','{model}') returned {tint}, " +
                                 $"which classifies {verdict} — a fallback the guard itself calls unpainted repairs " +
                                 "nothing and re-flags forever");
                else chromatic++;
            }
            log.Append($"[family-tints-chromatic] {chromatic}/{tintCases.Length} chromatic; ");

            // ── 7 [arm-covers-late-skin] ─────────────────────────────────────
            // Source-read, deliberately: the equality of the two paths is a STRUCTURAL fact
            // (one method, two callers), and only reading the source can prove it stayed that way.
            string factory = ReadSource(FactorySrc, failures);
            string lateSkin = ReadSource(LateSkinSrc, failures);
            if (factory != null && lateSkin != null)
            {
                bool armed = factory.Contains("EnemyBodyColorGuard.Arm(");
                bool lateSkinGoesThroughTrySkin = lateSkin.Contains("EnemyFactory.TrySkinBody(");
                bool lateSkinSkinsItself =
                    lateSkin.Contains("VisualFactory.Skin(") || lateSkin.Contains("LoadEnemyPrefab(");

                if (!armed)
                    failures.Add("[arm-covers-late-skin] EnemyFactory.cs no longer arms EnemyBodyColorGuard — " +
                                 "nothing colour-verifies a skinned body and the 2026-08-20 report can return silently");
                if (!lateSkinGoesThroughTrySkin)
                    failures.Add("[arm-covers-late-skin] EnemyLateSkinner no longer rebuilds through " +
                                 "EnemyFactory.TrySkinBody — the re-skinned body is no longer provably the same " +
                                 "body the spawn path builds, so the colour guard may not reach it");
                if (lateSkinSkinsItself)
                    failures.Add("[arm-covers-late-skin] EnemyLateSkinner skins a body ITSELF (a VisualFactory.Skin / " +
                                 "LoadEnemyPrefab call of its own) — that is a SECOND body-building path, and the " +
                                 "recolour on it will drift from the spawn path's exactly as the 08-18 seam did");
                if (armed && lateSkinGoesThroughTrySkin && !lateSkinSkinsItself)
                    log.Append("[arm-covers-late-skin] ok; ");
            }

            // ── 8 [miss-tint-floor] ──────────────────────────────────────────
            if (factory != null)
            {
                if (!factory.Contains("SetMissTint(FamilyFallbackTint("))
                    failures.Add("[miss-tint-floor] EnemyFactory no longer binds a family MISS-TINT on the enemy " +
                                 "TripoMaterialFixer — a slot whose own map is missing falls back to the fixer's " +
                                 "0.5 grey, which is the flat-grey enemy this suite exists to stop");
                else log.Append("[miss-tint-floor] ok; ");
            }

            if (failures.Count > 0)
            {
                reason = $"{failures.Count} failure(s): " + string.Join(" | ", failures);
                return false;
            }

            reason = "8/8 cases pass — " + log.ToString().TrimEnd(' ', ';');
            return true;
        }

        /// <summary>Read a source file, recording a failure (and returning null) if it is gone.</summary>
        private static string ReadSource(string path, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add($"[source-missing] '{path}' does not exist — this suite cannot prove the enemy colour " +
                             "path is wired, and a suite that cannot fail is worse than no suite");
                return null;
            }
            return File.ReadAllText(path);
        }
    }
}
