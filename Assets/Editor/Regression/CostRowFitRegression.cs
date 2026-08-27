// =====================================================================
//  CostRowFitRegression -- WO-1060. THE COST ROW MUST STAY INSIDE ITS BAND.
// ---------------------------------------------------------------------
//  WHAT IT PINS, AND WHY IT EXISTS.
//
//  ElarionUiKit.CostRow authors a LayoutElement.preferredWidth for every child
//  (22 for the icon, max(28, len*8) for the text). A HorizontalLayoutGroup only
//  READS those numbers when childControlWidth is true. With it false the group
//  lays every child out at its RAW sizeDelta instead -- 100 for a default Image,
//  200 for a default TextMeshProUGUI -- so a three-part row measures ~920 ref px
//  inside the 228.8 px band a 260 px build card gives it, and spills onto both
//  neighbouring cards.
//
//  That is not hypothetical: 0c65af9b0 (WO-1195 cost formatting) moved
//  BuildPaletteUI from a single anchored cost label to this shared CostRow, and
//  the very next capture went from zero BuildPaletteDock findings to 33
//  BUTTON OVER TEXT ones (Builds/ship-ui-capture.log, 2026-08-25 20:47), each a
//  build card's Button sitting over the NEIGHBOURING card's cost text.
//
//  The capture harness can only see panels that are in the capture enumeration,
//  and BuildPaletteDock is the only CostRow consumer that is. This suite pins the
//  KIT METHOD instead, so the guarantee holds for every future caller whether or
//  not anyone remembers to add it to the screenshot set.
//
//  RED-FIRST (PROD-008 / WO-1138). Case RED reproduces the exact regression by
//  setting childControlWidth back to false on the built row, and FAILS IF THE
//  CHILDREN STAY INSIDE. A containment check that cannot go red is not evidence,
//  and this one is measured against the same numbers the green case uses.
//
//  MEASUREMENT. Two landscape aspects, on a WORLD-SPACE canvas sized to the
//  reference-px extent the kit's ScaleWithScreenSize + MatchWidthOrHeight scaler
//  resolves for that target -- a ScreenSpace canvas in an edit-mode batchmode
//  call reports the editor's own 640x480 and every number is fiction (F8-5).
//
//  The row is built through the PRODUCTION method, not a hand-rolled copy: a
//  second construction path would be a second thing to keep in step, and the
//  point is to pin what BuildPaletteUI actually gets. Icons may or may not
//  resolve headless; the assertion is containment, which holds either way.
// =====================================================================

using System.Collections.Generic;
using System.Text;
using DeNelle.Core.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Editor.Regression
{
    public static class CostRowFitRegression
    {
        /// <summary>Landscape targets: 16:9 and the Seeker's tall-landscape.</summary>
        private static readonly Vector2Int[] Aspects =
        {
            new Vector2Int(1920, 1080),
            new Vector2Int(2340, 1080),
        };

        // BuildPaletteUI's real geometry -- a 260 px card with the cost row anchored
        // 0.06..0.94, i.e. a 228.8 px band. Mirrored (not shared) on purpose: if the
        // palette ever narrows its cards, this suite still pins the kit's contract at
        // the width the defect was measured at.
        private const float CardWidthPx = 260f;
        private const float CardHeightPx = 160f;
        private const float RowMinX = 0.06f, RowMaxX = 0.94f;
        private const float BandWidthPx = CardWidthPx * (RowMaxX - RowMinX);   // 228.8

        // The kit's scaler settings, mirrored so the reference-px extent can be computed
        // without a live CanvasScaler.Update (which does not run in a synchronous call).
        private const float RefW = 1080f, RefH = 1920f, Match = 0.5f;

        private const float Epsilon = 0.5f;

        [MenuItem("Tools/Regression/UI/Cost Row Fit (WO-1060)")]
        public static void RunMenu()
        {
            bool ok = Run(out string reason);
            if (ok) Debug.Log(reason); else Debug.LogError(reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            int casesRun = 0;

            foreach (var a in Aspects)
            {
                casesRun += 3;
                CaseFitsPlain(a.x, a.y, failures, log);
                CaseFitsWithNeedPrefix(a.x, a.y, failures, log);
                CaseRedWhenWidthUncontrolled(a.x, a.y, failures, log);
            }

            if (failures.Count > 0)
            {
                var sb = new StringBuilder("COST_ROW_FIT_FAIL x" + failures.Count + " over " + casesRun +
                    " cases -- ElarionUiKit.CostRow is laying children outside the band it was given. " +
                    "Every panel that shows a price inherits this; BuildPaletteDock is only the one the " +
                    "capture harness can see.\n");
                for (int i = 0; i < failures.Count; i++) sb.AppendLine("  - " + failures[i]);
                sb.Append(log);
                reason = sb.ToString();
                return false;
            }

            reason = "COST_ROW_FIT_OK " + casesRun + "/" + casesRun + " cases -- ElarionUiKit.CostRow keeps " +
                     "every child inside its " + BandWidthPx.ToString("0.#") + " ref px band (with and without " +
                     "the NEED prefix), and the same measurement goes RED when childControlWidth is turned " +
                     "back off -- at " + Aspects.Length + " landscape aspects.\n" + log;
            return true;
        }

        // ---------------------------------------------------------------------
        //  GREEN -- an ordinary three-resource price stays inside the band.
        // ---------------------------------------------------------------------
        private static void CaseFitsPlain(int w, int h, List<string> failures, StringBuilder log)
        {
            RunFitCase(w, h, null, "plain", failures, log);
        }

        // ---------------------------------------------------------------------
        //  GREEN -- the unaffordable variant. BuildPaletteUI passes a "NEED" prefix,
        //  which is a FOURTH child in the same band; if anything overflows it is this.
        // ---------------------------------------------------------------------
        private static void CaseFitsWithNeedPrefix(int w, int h, List<string> failures, StringBuilder log)
        {
            RunFitCase(w, h, "NEED", "NEED-prefixed", failures, log);
        }

        private static void RunFitCase(int w, int h, string prefix, string label,
                                       List<string> failures, StringBuilder log)
        {
            string at = w + "x" + h;
            GameObject canvas = null;
            try
            {
                canvas = BuildCanvas(w, h);
                var card = Card(canvas.transform);
                var row = BuildCostRow(card, prefix);
                Settle(canvas);

                int children = row.childCount;
                if (children == 0)
                {
                    failures.Add("FIT(" + label + ") @" + at + ": the CostRow was built with no children at " +
                                 "all, so this case proves nothing. A vacuous containment check is worse than " +
                                 "no check -- it reads green forever.");
                    return;
                }

                float bandHalf = row.rect.width * 0.5f;
                float worstOver = 0f;
                string worstName = null;
                for (int i = 0; i < children; i++)
                {
                    var child = (RectTransform)row.GetChild(i);
                    float cx = child.localPosition.x;
                    float over = Mathf.Max(-(cx + child.rect.xMin) - bandHalf, (cx + child.rect.xMax) - bandHalf);
                    if (over > worstOver) { worstOver = over; worstName = child.name; }
                }

                if (worstOver > Epsilon)
                {
                    failures.Add("FIT(" + label + ") @" + at + ": '" + worstName + "' escapes the CostRow band by " +
                                 worstOver.ToString("0.#") + " ref px (band is " + row.rect.width.ToString("0.#") +
                                 " px wide, " + children + " children). On a build card that lands on the " +
                                 "NEIGHBOURING card and the oracle reports it as BUTTON OVER TEXT. Check that " +
                                 "HorizontalLayoutGroup.childControlWidth is still true in ElarionUiKit.CostRow -- " +
                                 "with it off the group ignores every LayoutElement.preferredWidth authored there.");
                    return;
                }

                log.AppendLine("  [fit-" + label + " @" + at + "] " + children + " children inside a " +
                               row.rect.width.ToString("0.#") + " px band, worst edge " +
                               worstOver.ToString("0.##") + " px over.");
            }
            finally { Kill(canvas); }
        }

        // ---------------------------------------------------------------------
        //  RED -- the regression itself. Turning childControlWidth back off MUST be
        //  caught by the very measurement the green cases use. If this case does not
        //  see an escape, the green cases are not evidence of anything.
        // ---------------------------------------------------------------------
        private static void CaseRedWhenWidthUncontrolled(int w, int h, List<string> failures, StringBuilder log)
        {
            string at = w + "x" + h;
            GameObject canvas = null;
            try
            {
                canvas = BuildCanvas(w, h);
                var card = Card(canvas.transform);
                var row = BuildCostRow(card, "NEED");

                // The exact pre-fix state of ElarionUiKit.CostRow (before WO-1060), reproduced
                // on a row built by the production method rather than described in a comment.
                var group = row.GetComponent<HorizontalLayoutGroup>();
                if (group == null)
                {
                    failures.Add("RED @" + at + ": the CostRow has no HorizontalLayoutGroup, so the regression " +
                                 "this suite pins cannot be reproduced and the green cases prove nothing.");
                    return;
                }
                group.childControlWidth = false;
                Settle(canvas);

                float bandHalf = row.rect.width * 0.5f;
                float worstOver = 0f;
                string worstName = null;
                for (int i = 0; i < row.childCount; i++)
                {
                    var child = (RectTransform)row.GetChild(i);
                    float cx = child.localPosition.x;
                    float over = Mathf.Max(-(cx + child.rect.xMin) - bandHalf, (cx + child.rect.xMax) - bandHalf);
                    if (over > worstOver) { worstOver = over; worstName = child.name; }
                }

                if (worstOver <= Epsilon)
                {
                    failures.Add("RED @" + at + ": childControlWidth was turned OFF and every child still " +
                                 "measured inside the band. The containment check cannot go red, so it is not " +
                                 "evidence -- fix the measurement before trusting any COST_ROW_FIT_OK.");
                    return;
                }

                log.AppendLine("  [red @" + at + "] childControlWidth=false -> '" + worstName + "' escapes by " +
                               worstOver.ToString("0.#") + " ref px. The measurement can see the regression.");
            }
            finally { Kill(canvas); }
        }

        // ---------------------------------------------------------------------
        //  Fixture plumbing.
        // ---------------------------------------------------------------------

        /// <summary>The production kit method, with BuildPaletteUI's own anchors and a
        /// three-resource price. Never a hand-rolled copy -- a second construction path is a
        /// second thing to keep in step with the kit.</summary>
        private static RectTransform BuildCostRow(Transform card, string prefix)
        {
            var parts = CostFormat.Parts(new[]
            {
                ("wood", "Wood", 120),
                ("iron", "Iron", 80),
                ("crystals", "Crystals", 40),
            });
            return ElarionUiKit.CostRow(card, parts,
                new Vector2(RowMinX, 0.03f), new Vector2(RowMaxX, 0.24f),
                ElarionUi.Parchment, prefix);
        }

        /// <summary>A build card at BuildPaletteUI's authored width.</summary>
        private static Transform Card(Transform parent)
        {
            var go = new GameObject("Card_fixture", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(CardWidthPx, CardHeightPx);
            rt.anchoredPosition = Vector2.zero;
            return go.transform;
        }

        /// <summary>A world-space canvas whose rect IS the reference-px extent the kit's
        /// ScaleWithScreenSize/MatchWidthOrHeight scaler resolves for this target.</summary>
        private static GameObject BuildCanvas(int w, int h)
        {
            float sf = ScaleFactor(w, h);
            var go = new GameObject("~CostRowFitProbe", typeof(RectTransform), typeof(Canvas));
            go.hideFlags = HideFlags.HideAndDontSave;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;   // NOT overlay: an overlay canvas in an
                                                         // edit-mode call reports the editor's own
                                                         // 640x480 and every measurement is fiction.
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w / sf, h / sf);
            rt.position = Vector3.zero;
            rt.localScale = Vector3.one;
            return go;
        }

        /// <summary>Mirrors CanvasScaler's ScaleWithScreenSize + MatchWidthOrHeight math.</summary>
        private static float ScaleFactor(int w, int h)
        {
            float logW = Mathf.Log(w / RefW, 2f);
            float logH = Mathf.Log(h / RefH, 2f);
            float sf = Mathf.Pow(2f, Mathf.Lerp(logW, logH, Match));
            return (sf > 0f && !float.IsNaN(sf) && !float.IsInfinity(sf)) ? sf : 1f;
        }

        /// <summary>Force a full synchronous layout pass. Twice, matching the capture harness:
        /// one pass is not always enough for nested rebuilds to settle.</summary>
        private static void Settle(GameObject canvas)
        {
            var rt = canvas.GetComponent<RectTransform>();
            for (int pass = 0; pass < 2; pass++)
            {
                Canvas.ForceUpdateCanvases();
                if (rt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
        }

        private static void Kill(GameObject go)
        {
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}
