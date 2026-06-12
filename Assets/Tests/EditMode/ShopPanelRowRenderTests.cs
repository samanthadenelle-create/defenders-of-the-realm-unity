using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace DeNelle.Tests.EditMode
{
    // =============================================================================
    // ShopPanelRowRenderTests (EditMode) — SOURCE guard that the vendor shop keeps
    // its layout-driven row rendering and never reverts to the fragile
    // normalized-anchor row positioning that hid all stock.
    // -----------------------------------------------------------------------------
    // THE BUG THIS GUARDS ("store shows no stock"). ShopPanel.BuildScrollContent used
    // to place each row by NORMALIZED ANCHOR fractions: a row's anchor was a fraction
    // of the CONTENT height while the slice was a fraction of the VIEWPORT height. Any
    // list longer than one viewport pushed later rows into NEGATIVE anchor space — off
    // the content, unreachable even by scrolling. The catalog loaded fine, but the panel
    // rendered empty. The FIX rebuilt the list around a VerticalLayoutGroup (top-down
    // stacking) + a ContentSizeFitter (content auto-grows to fit every row) + a
    // per-row LayoutElement height — robust for ANY row count.
    //
    // WHAT THIS LINT ASSERTS (over comment-stripped ShopPanel source):
    //   * "VerticalLayoutGroup" is present  -> rows are stacked by a layout group.
    //   * "ContentSizeFitter" is present    -> the content auto-sizes to all rows.
    //   * (corroborating) "LayoutElement" is present -> rows carry an explicit height.
    // If a future edit drops the layout group / fitter and re-introduces fraction
    // anchoring, the first two assertions go RED.
    //
    // WHY A SOURCE LINT. Reproducing the "rows off-content" failure needs a live Canvas
    // + ScrollRect layout pass (PlayMode), which is heavy + flaky. The structural cause
    // is the LAYOUT MECHANISM in source, so a comment-stripped token scan over
    // ShopPanel.cs is the fast, deterministic guard. Comments are stripped so the long
    // doc-comment in ShopPanel that NARRATES the old fraction approach can never satisfy
    // or trip the guard.
    //
    // KNOWN LIMITS (honest): presence-of-token, not a layout-graph proof. It confirms
    // the layout mechanism still LIVES in the file, not that it is wired to the exact
    // content RectTransform. That trade keeps it PlayMode-free; the goal is to make a
    // revert to normalized-anchor rows go RED.
    // =============================================================================
    public class ShopPanelRowRenderTests
    {
        private const string RelPath = "Assets/_Modules/Village/Hero/ShopPanel.cs";

        [Test]
        public void ShopPanel_UsesLayoutDrivenRows_NotNormalizedAnchorPositioning()
        {
            string code = StripComments(ReadSource(RelPath));

            Assert.IsTrue(code.Contains("VerticalLayoutGroup"),
                "ShopPanel no longer uses a VerticalLayoutGroup to stack its rows. It has likely " +
                "reverted to normalized-anchor row positioning, whose anchors push any list longer " +
                "than one viewport into NEGATIVE space — off the content, so the store shows NO STOCK " +
                "even though the catalog loaded. FIX: build the scroll content with a " +
                "VerticalLayoutGroup + ContentSizeFitter + per-row LayoutElement (see BuildScrollContent).");

            Assert.IsTrue(code.Contains("ContentSizeFitter"),
                "ShopPanel no longer uses a ContentSizeFitter to size its scroll content. Without it " +
                "the content cannot auto-grow to fit every row and longer lists render off-content " +
                "(the 'store shows no stock' regression). FIX: add a ContentSizeFitter " +
                "(verticalFit = PreferredSize) to the scroll content.");

            // Corroborating: rows must carry an explicit LayoutElement height for the
            // layout group to honor. This is what makes the content size deterministic.
            Assert.IsTrue(code.Contains("LayoutElement"),
                "ShopPanel rows no longer carry a LayoutElement height. The VerticalLayoutGroup + " +
                "ContentSizeFitter cannot size the content without per-row preferred heights, so rows " +
                "may collapse / overflow. FIX: give each row a LayoutElement with preferredHeight = RowHeightPx.");
        }

        // SELF-TEST: the detector must FIRE on the bad shape (fraction-anchored rows, no
        // layout group / fitter) and stay QUIET on the good shape (layout-driven). Proves
        // the same Contains-checks that pass the live file would have caught the revert.
        [Test]
        public void Detector_FiresOnFractionAnchorShape_QuietOnLayoutShape()
        {
            // BAD: the old fraction-anchor approach — no VerticalLayoutGroup, no ContentSizeFitter.
            const string bad = @"
                Transform BuildScrollContent(int rowCount) {
                    float rowSlice = 1f / rowCount;
                    for (int i = 0; i < rowCount; i++) {
                        var r = row.GetComponent<RectTransform>();
                        r.anchorMin = new Vector2(0f, 1f - (i + 1) * rowSlice);
                        r.anchorMax = new Vector2(1f, 1f - i * rowSlice);
                    }
                    return content;
                }";
            string badCode = StripComments(bad);
            Assert.IsFalse(badCode.Contains("VerticalLayoutGroup"),
                "self-test setup wrong: the fraction-anchor shape must NOT contain VerticalLayoutGroup.");
            Assert.IsFalse(badCode.Contains("ContentSizeFitter"),
                "self-test setup wrong: the fraction-anchor shape must NOT contain ContentSizeFitter.");

            // GOOD: the layout-driven fix.
            const string good = @"
                Transform BuildScrollContent(int rowCount) {
                    var vlg = content.AddComponent<VerticalLayoutGroup>();
                    var fitter = content.AddComponent<ContentSizeFitter>();
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                    var le = row.AddComponent<LayoutElement>(); le.preferredHeight = 86f;
                    return content;
                }";
            string goodCode = StripComments(good);
            Assert.IsTrue(goodCode.Contains("VerticalLayoutGroup"), "good shape must contain VerticalLayoutGroup.");
            Assert.IsTrue(goodCode.Contains("ContentSizeFitter"), "good shape must contain ContentSizeFitter.");
            Assert.IsTrue(goodCode.Contains("LayoutElement"), "good shape must contain LayoutElement.");
        }

        // SELF-TEST 2: a comment narrating the OLD fraction approach must NOT trip the
        // guard once stripped — proves the comment-strip is load-bearing. (ShopPanel's
        // real doc-comment literally describes "normalized-fraction placement".)
        [Test]
        public void CommentStrip_DropsNarrationOfTheOldApproach()
        {
            const string commentOnly =
                "// the old normalized-fraction placement set anchorMin/anchorMax by row slice\n" +
                "// it had no VerticalLayoutGroup and no ContentSizeFitter\nint x = 0;";
            string stripped = StripComments(commentOnly);
            Assert.IsFalse(stripped.Contains("VerticalLayoutGroup"),
                "comment-strip failed: a token inside a // comment survived. ShopPanel's doc-comment " +
                "mentions both the old + new mechanisms, so the strip MUST remove comment text or the " +
                "guard reads narration instead of live code.");
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        private static string ReadSource(string rel)
        {
            string full = Path.Combine(ProjectRoot(), rel.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(full),
                $"Expected ShopPanel source at {rel} but it was not found — the file moved or was " +
                "deleted. Update RelPath in ShopPanelRowRenderTests to the new location.");
            return File.ReadAllText(full);
        }

        // Strips // line comments and /* */ block comments (string-literal-aware).
        private static string StripComments(string src)
        {
            var sb = new StringBuilder(src.Length);
            bool inStr = false, inChar = false, inLine = false, inBlock = false, esc = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';
                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock) { if (c == '*' && n == '/') { inBlock = false; i++; } continue; }
                if (inStr)
                {
                    sb.Append(c);
                    if (esc) esc = false;
                    else if (c == '\\') esc = true;
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (inChar)
                {
                    sb.Append(c);
                    if (esc) esc = false;
                    else if (c == '\\') esc = true;
                    else if (c == '\'') inChar = false;
                    continue;
                }
                if (c == '/' && n == '/') { inLine = true; i++; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                if (c == '"') { inStr = true; sb.Append(c); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static string ProjectRoot()
        {
            string assets = UnityEngine.Application.dataPath;
            return Directory.GetParent(assets)?.FullName ?? assets;
        }
    }
}
