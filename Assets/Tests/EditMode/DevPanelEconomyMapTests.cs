using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace DeNelle.Tests.EditMode
{
    // =============================================================================
    // DevPanelEconomyMapTests (EditMode) — SOURCE guard that the DEV "Add resources"
    // grant keeps routing through the EconomyService.GrantSpendable seam AND keeps
    // pushing the on-screen resource bar.
    // -----------------------------------------------------------------------------
    // THE BUG THIS GUARDS. The dev panel's resource grant used to write GameState
    // directly. That filled only the persisted upgrade wallet — the on-screen HUD
    // resource bar (and the in-session EconomyService pool the shop spends from) saw
    // NOTHING, so "Add resources" appeared to do nothing. The fix routes the grant
    // through EconomyService.GrantSpendable (lands Wood/Iron in BOTH wallets) and then
    // PingHud() pushes the wallet straight into VillageHudController.SetResources /
    // SetCrystals so the bar populates the moment a grant lands.
    //
    // COMPLEMENT, NOT DUPLICATE of DevGrantSpendableTests: that fixture runtime-tests
    // EconomyService.GrantSpendable's wallet-convergence BEHAVIOUR. This test pins the
    // DEV-PANEL CALL SITE — that DevPanelController still CALLS GrantSpendable and still
    // refreshes the bar. The runtime test would stay green even if someone reverted the
    // dev panel to a direct GameState write; this source guard goes red on that revert.
    //
    // WHY A SOURCE LINT. DevPanelController lives in the #if-gated DeNelle.DevTools
    // assembly (compiled out of release, NOT referenced by the test asmdef), so it
    // cannot be type-referenced from EditMode. We read its source as text instead.
    // Comments are stripped first so a doc-comment mentioning these tokens can neither
    // satisfy nor trip the guard — only live code counts.
    //
    // WHAT IT ASSERTS (over comment-stripped source):
    //   * "GrantSpendable" is present       -> the grant path uses the convergent seam.
    //   * "PingHud" is present              -> the bar-refresh helper is wired.
    //   * at least one of "SetResources" / "SetCrystals" is present -> PingHud actually
    //                                          pushes the wallet into the HUD bar.
    //
    // KNOWN LIMITS (honest): presence-of-token, not data-flow. It proves the seam +
    // refresh tokens still LIVE in the file, not that they sit on the exact grant path.
    // That keeps it fast + robust; the goal is to make a revert-to-direct-GameState-write
    // (which drops GrantSpendable) or a dropped bar-refresh go RED.
    // =============================================================================
    public class DevPanelEconomyMapTests
    {
        private const string RelPath = "Assets/_Modules/DevTools/DevPanelController.cs";

        [Test]
        public void DevPanelGrant_RoutesThroughGrantSpendable_AndRefreshesTheBar()
        {
            string code = StripComments(ReadSource(RelPath));

            Assert.IsTrue(code.Contains("GrantSpendable"),
                "DevPanelController no longer references EconomyService.GrantSpendable. The dev " +
                "'Add resources' grant has likely reverted to a direct GameState write, which " +
                "fills only the upgrade wallet — the on-screen resource bar + the shop's in-session " +
                "pool stay empty (the 'Add resources does nothing' regression). FIX: route the grant " +
                "through EconomyService.Instance.GrantSpendable(...).");

            Assert.IsTrue(code.Contains("PingHud"),
                "DevPanelController no longer calls PingHud(). After a dev grant the on-screen " +
                "resource bar will not refresh, so the grant appears to do nothing. FIX: call " +
                "PingHud() after the grant to push the wallet into VillageHudController.");

            Assert.IsTrue(code.Contains("SetResources") || code.Contains("SetCrystals"),
                "DevPanelController's bar refresh no longer pushes the wallet into the HUD " +
                "(neither VillageHudController.SetResources nor SetCrystals is referenced). The " +
                "resource bar will not populate after a dev grant. FIX: have PingHud() call " +
                "hud.SetResources(...) / hud.SetCrystals(...).");
        }

        // SELF-TEST: the detector logic must FIRE on the bad shape (a direct GameState
        // write with no seam + no bar refresh) and stay QUIET on the good shape. Proves
        // the same Contains-checks that pass the live file would have caught the revert.
        [Test]
        public void Detector_FiresOnRevertedShape_QuietOnFixedShape()
        {
            // BAD: the reverted shape — writes GameState directly, no GrantSpendable / PingHud.
            const string bad = @"
                void GiveCrystals(int amount) {
                    var r = state.Resources; r.Crystals += amount; state.Resources = r;
                    state.Save();
                }";
            string badCode = StripComments(bad);
            Assert.IsFalse(badCode.Contains("GrantSpendable"),
                "self-test setup wrong: the bad shape should NOT contain GrantSpendable.");
            Assert.IsFalse(badCode.Contains("PingHud"),
                "self-test setup wrong: the bad shape should NOT contain PingHud.");

            // GOOD: the fixed shape — routes through the seam AND refreshes the bar.
            const string good = @"
                void GiveCrystals(int amount) {
                    EconomyService.Instance.GrantSpendable(crystals: amount);
                    PingHud();
                }
                static void PingHud() {
                    hud.SetResources(w, i, f, c); hud.SetCrystals(c);
                }";
            string goodCode = StripComments(good);
            Assert.IsTrue(goodCode.Contains("GrantSpendable"), "good shape must contain GrantSpendable.");
            Assert.IsTrue(goodCode.Contains("PingHud"), "good shape must contain PingHud.");
            Assert.IsTrue(goodCode.Contains("SetResources") || goodCode.Contains("SetCrystals"),
                "good shape must push the wallet into the HUD bar.");
        }

        // SELF-TEST 2: a comment mentioning the tokens must NOT satisfy the guard once
        // stripped — proves the comment-strip is load-bearing (a doc-comment can't fake it).
        [Test]
        public void CommentStrip_DropsTokensInComments()
        {
            const string commentOnly = "// GrantSpendable PingHud SetResources are mentioned only in this comment\nint x = 0;";
            string stripped = StripComments(commentOnly);
            Assert.IsFalse(stripped.Contains("GrantSpendable"),
                "comment-strip failed: a token inside a // comment survived, so a doc-comment " +
                "could falsely satisfy this guard. The strip must remove comment text.");
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        private static string ReadSource(string rel)
        {
            string full = Path.Combine(ProjectRoot(), rel.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(full),
                $"Expected DevPanel source at {rel} but it was not found — the file moved or was " +
                "deleted. Update RelPath in DevPanelEconomyMapTests to the new location.");
            return File.ReadAllText(full);
        }

        // Strips // line comments and /* */ block comments (string-literal-aware) so the
        // token scan sees live CODE only — mirrors the sibling lints' StripComments.
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

        // <project> root, from Application.dataPath (== <project>/Assets).
        private static string ProjectRoot()
        {
            string assets = UnityEngine.Application.dataPath;
            return Directory.GetParent(assets)?.FullName ?? assets;
        }
    }
}
