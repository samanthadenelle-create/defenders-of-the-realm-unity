// =============================================================================
// WandererBubbleLegibilityRegression — pins WO-973 (Bryn's speech bubble was a
// giant world-space card covering ~60 % of the frame with the line cut off).
// -----------------------------------------------------------------------------
// WHY THIS SUITE EXISTS AT ALL, AND WHY IT DOES NOT PIN "THE AUTHORED VALUES".
//
// The obvious regression — "pin the bubble's world scale against the authored
// values" — would have pinned the BUG. The authored values WERE the defect. And
// they live in TWO places that can silently diverge:
//
//   1. the C# field initialisers in WandererBubble.cs, and
//   2. a serialised COPY inside Assets/Scenes/Dungeon_HealersCottage.unity,
//      written at bake time by DungeonSceneBuilder.
//
// That divergence is the real trap in this defect: correcting the code default
// alone ships with NO visible effect, because the baked scene still carries the
// old number and Unity deserialises over the initialiser. Nothing in the compile
// gate, the bake, or any screenshot suite can see that.
//
// So this suite pins THREE things:
//   Case 1 — the code defaults are the corrected (small) numbers, and the text
//            scale is a SERIALISED FIELD, not the hardcoded literal it used to
//            be (a literal cannot be tuned from the scene at all).
//   Case 2 — the scene-serialised copy EQUALS the code defaults. This is the
//            drift catcher, and it stays red until the dungeon is re-baked.
//   Case 3 — the bubble is not larger than the shipped village sibling
//            (DeNelle.Village.TownsfolkBubble), on panel size, wrap, and glyph
//            world size. Both sides are parsed from source, so neither side can
//            rot into a stale hardcoded copy.
//
// Case 3 is the one that actually generalises: it is a RATIO against a shipped,
// owner-accepted bubble, so it keeps holding even after someone re-tunes both.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class WandererBubbleLegibilityRegression
    {
        private const string WandererPath = "Assets/_Modules/Dungeons/Wanderer/WandererBubble.cs";
        private const string TownsfolkPath = "Assets/_Modules/Village/NPCs/TownsfolkBubble.cs";
        private const string ScenePath = "Assets/Scenes/Dungeon_HealersCottage.unity";
        private const string SceneClassId = "DeNelle.Dungeons::DeNelle.Dungeons.WandererBubble";

        /// <summary>Standalone batch entry — prints the WANDERER_BUBBLE_OK/_FAIL marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("WANDERER_BUBBLE_OK - " + reason);
            else Debug.LogError("WANDERER_BUBBLE_FAIL: " + reason);
        }

        /// <summary>Covenant contract for DataRegression.RunAll ([wanderer-bubble]). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            Case(failures, "code-defaults", () => Case1_CodeDefaultsAreTheCorrectedNumbers(failures, notes));
            Case(failures, "scene-matches-code", () => Case2_SceneCopyMatchesCodeDefaults(failures, notes));
            Case(failures, "not-bigger-than-sibling", () => Case3_NotBiggerThanShippedSibling(failures, notes));

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "WANDERER BUBBLE OK - 3/3 cases pass (code defaults are the corrected " +
                         "small numbers with a SERIALISED text scale, the baked scene copy equals " +
                         "them, and the bubble is no larger than the shipped TownsfolkBubble on " +
                         "panel size / wrap / glyph world size)" + noteStr;
                return true;
            }
            reason = "WANDERER BUBBLE FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        // =====================================================================
        //  CASE 1 — the code defaults, and the text scale is serialisable
        // =====================================================================
        private static void Case1_CodeDefaultsAreTheCorrectedNumbers(List<string> failures, List<string> notes)
        {
            string src = ReadText(WandererPath);
            if (src == null) { failures.Add($"[code-defaults] cannot read {WandererPath}"); return; }

            float? w = FloatField(src, "_panelWidth");
            float? h = FloatField(src, "_panelHeight");
            float? wrap = FloatField(src, "_wrapWidth");
            float? ts = FloatField(src, "_textScale");

            RequireAtMost(failures, "code-defaults", "_panelWidth", w, 2.2f);
            RequireAtMost(failures, "code-defaults", "_panelHeight", h, 0.9f);
            RequireAtMost(failures, "code-defaults", "_wrapWidth", wrap, 26f);
            RequireAtMost(failures, "code-defaults", "_textScale", ts, 0.09f);

            // The text scale MUST be a serialised field, not a literal in Build(). A literal
            // is invisible to the scene, so no bake can ever carry a corrected value and no
            // one can tune it without a code change + a re-bake.
            if (!Regex.IsMatch(src, @"\[SerializeField\]\s+private\s+float\s+_textScale"))
                failures.Add("[code-defaults] _textScale is not a [SerializeField] — the text size " +
                             "is un-tunable from the scene again (this was the WO-973 trap: a " +
                             "hardcoded Vector3.one * 0.16f in Build()).");
            if (Regex.IsMatch(src, @"localScale\s*=\s*Vector3\.one\s*\*\s*0?\.\d+f"))
                failures.Add("[code-defaults] Build() still assigns the text localScale from a " +
                             "hardcoded literal instead of _textScale.");

            notes.Add($"code defaults: panel={Fmt(w)}x{Fmt(h)} wrap={Fmt(wrap)} textScale={Fmt(ts)}");
        }

        // =====================================================================
        //  CASE 2 — the baked scene's serialised copy equals the code defaults
        // =====================================================================
        private static void Case2_SceneCopyMatchesCodeDefaults(List<string> failures, List<string> notes)
        {
            string src = ReadText(WandererPath);
            string scene = ReadText(ScenePath);
            if (src == null) { failures.Add($"[scene-matches-code] cannot read {WandererPath}"); return; }
            if (scene == null) { failures.Add($"[scene-matches-code] cannot read {ScenePath}"); return; }

            string block = ExtractSceneComponentBlock(scene, SceneClassId);
            if (block == null)
            {
                failures.Add($"[scene-matches-code] no WandererBubble component found in {ScenePath} " +
                             "— Bryn has no bubble baked, or the class moved.");
                return;
            }

            foreach (string field in new[] { "_panelWidth", "_panelHeight", "_wrapWidth", "_textScale" })
            {
                float? code = FloatField(src, field);
                float? baked = YamlFloat(block, field);
                if (code == null) { failures.Add($"[scene-matches-code] no code default parsed for {field}"); continue; }
                if (baked == null)
                {
                    failures.Add($"[scene-matches-code] {field} is NOT serialised in the baked scene " +
                                 $"(code default {Fmt(code)}) — the scene predates the field. " +
                                 "RE-BAKE the dungeon (isolated worktree) so the bake carries it.");
                    continue;
                }
                if (Mathf.Abs(code.Value - baked.Value) > 0.001f)
                    failures.Add($"[scene-matches-code] {field} DRIFT: code={Fmt(code)} but the baked " +
                                 $"scene carries {Fmt(baked)} — Unity deserialises the scene copy OVER " +
                                 "the initialiser, so the corrected code default has NO effect in play. " +
                                 "RE-BAKE the dungeon (isolated worktree).");
            }
        }

        // =====================================================================
        //  CASE 3 — no larger than the shipped village bubble
        // =====================================================================
        private static void Case3_NotBiggerThanShippedSibling(List<string> failures, List<string> notes)
        {
            string wander = ReadText(WandererPath);
            string town = ReadText(TownsfolkPath);
            if (wander == null) { failures.Add($"[not-bigger-than-sibling] cannot read {WandererPath}"); return; }
            if (town == null) { failures.Add($"[not-bigger-than-sibling] cannot read {TownsfolkPath}"); return; }

            CompareLE(failures, "_panelWidth", FloatField(wander, "_panelWidth"), FloatField(town, "_panelWidth"));
            CompareLE(failures, "_panelHeight", FloatField(wander, "_panelHeight"), FloatField(town, "_panelHeight"));
            CompareLE(failures, "_wrapWidth", FloatField(wander, "_wrapWidth"), FloatField(town, "_wrapWidth"));

            // Glyph world size is a PRODUCT of three numbers, and WO-973's bubble was
            // oversized by all three at once (0.16 * 0.5 * 64 = 5.12 vs 0.07 * 0.32 * 96
            // = 2.15). Shrinking one while growing another would sail past a per-field
            // check, so compare the product.
            float? wGlyph = GlyphWorldSize(wander, FloatField(wander, "_textScale"));
            float? tGlyph = GlyphWorldSize(town, FloatField(town, "_textScale"));
            if (wGlyph == null || tGlyph == null)
            {
                failures.Add("[not-bigger-than-sibling] could not parse fontSize/characterSize/textScale " +
                             $"from both bubbles (wanderer={Fmt(wGlyph)} townsfolk={Fmt(tGlyph)}) — " +
                             "this case is no longer covering the glyph size.");
                return;
            }
            if (wGlyph.Value > tGlyph.Value * 1.10f)
                failures.Add($"[not-bigger-than-sibling] glyph world size {wGlyph.Value:F2} exceeds the " +
                             $"shipped TownsfolkBubble's {tGlyph.Value:F2} (localScale * characterSize * " +
                             "fontSize) — Bryn's text is bigger than the owner-accepted village bubble again.");
            else
                notes.Add($"glyph world size: wanderer={wGlyph.Value:F2} townsfolk={tGlyph.Value:F2}");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static float? GlyphWorldSize(string src, float? textScale)
        {
            float? fontSize = FloatAssignment(src, "fontSize");
            float? charSize = FloatAssignment(src, "characterSize");
            if (textScale == null || fontSize == null || charSize == null) return null;
            return textScale.Value * fontSize.Value * charSize.Value;
        }

        private static void CompareLE(List<string> failures, string field, float? wanderer, float? townsfolk)
        {
            if (wanderer == null || townsfolk == null)
            {
                failures.Add($"[not-bigger-than-sibling] could not parse {field} from both bubbles " +
                             $"(wanderer={Fmt(wanderer)} townsfolk={Fmt(townsfolk)})");
                return;
            }
            if (wanderer.Value > townsfolk.Value * 1.10f)
                failures.Add($"[not-bigger-than-sibling] {field}={Fmt(wanderer)} exceeds the shipped " +
                             $"TownsfolkBubble's {Fmt(townsfolk)} — Bryn's card is oversized again (WO-973).");
        }

        private static void RequireAtMost(List<string> failures, string caseName, string field, float? value, float ceiling)
        {
            if (value == null) { failures.Add($"[{caseName}] could not parse {field} from {WandererPath}"); return; }
            if (value.Value > ceiling)
                failures.Add($"[{caseName}] {field}={Fmt(value)} exceeds the WO-973 ceiling of {ceiling} " +
                             "— the bubble has grown back toward the unreadable card.");
        }

        /// <summary>Parses a `[SerializeField] private &lt;num&gt; _name = VALUE;` initialiser.</summary>
        private static float? FloatField(string src, string field)
        {
            var m = Regex.Match(src, @"\b" + Regex.Escape(field) + @"\s*=\s*(-?[0-9]*\.?[0-9]+)f?\s*;");
            return m.Success ? ParseFloat(m.Groups[1].Value) : (float?)null;
        }

        /// <summary>Parses a `_text.fontSize = 96;` style assignment.</summary>
        private static float? FloatAssignment(string src, string member)
        {
            var m = Regex.Match(src, @"\." + Regex.Escape(member) + @"\s*=\s*(-?[0-9]*\.?[0-9]+)f?\s*;");
            return m.Success ? ParseFloat(m.Groups[1].Value) : (float?)null;
        }

        /// <summary>Parses `  _field: 1.8` out of a YAML component block.</summary>
        private static float? YamlFloat(string block, string field)
        {
            var m = Regex.Match(block, @"^\s*" + Regex.Escape(field) + @":\s*(-?[0-9]*\.?[0-9]+)\s*$",
                                RegexOptions.Multiline);
            return m.Success ? ParseFloat(m.Groups[1].Value) : (float?)null;
        }

        /// <summary>
        /// Returns the YAML lines of the MonoBehaviour whose m_EditorClassIdentifier is
        /// <paramref name="classId"/> — from that marker to the next document separator.
        /// </summary>
        private static string ExtractSceneComponentBlock(string scene, string classId)
        {
            int idx = scene.IndexOf("m_EditorClassIdentifier: " + classId, StringComparison.Ordinal);
            if (idx < 0) return null;
            int end = scene.IndexOf("\n--- !u!", idx, StringComparison.Ordinal);
            if (end < 0) end = scene.Length;
            return scene.Substring(idx, end - idx);
        }

        private static float? ParseFloat(string s)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                ? v : (float?)null;
        }

        private static string Fmt(float? v) => v == null ? "<unparsed>" : v.Value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string ReadText(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch (Exception) { return null; }
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }
    }
}
