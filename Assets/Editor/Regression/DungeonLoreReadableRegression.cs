// =============================================================================
// DungeonLoreReadableRegression [dungeon-lore] — locks WO-770.4 (fixes D6): the
// lore-stone triple gap (no input caller for Read(), no subscriber for
// ReadRequested, no view) must stay closed. Source-lint (edit-mode, no PlayMode),
// wired into DataRegression.RunAll. Never throws.
// -----------------------------------------------------------------------------
// WO-881 adds two more locks:
//   (4) SCROLL — long lore must scroll, not cull. The well's bands must be FIXED
//       PIXELS (the rumor board's fraction band resolved to -11px on 2026-08-02
//       and TMP culled the body), the mask + scrollbar + a text-encoded overflow
//       hint must exist, and the prose row must carry a px height floor.
//   (5) NAME — "Alduin" (the necromancer, canon-strings.json "alduin") and
//       "Aldwin" (Echo #1, EchoRosterCatalog) are DIFFERENT characters one letter
//       apart. WO-881 read the journal title as a typo; it is not. This pins the
//       journal to Alduin AT THE COPY SOURCE, pins the Echo to Aldwin, and forbids
//       the View from carrying either name (copy is authored in data, never in a
//       view — a View-side rename would hide the data and drift the two apart).
//   (5b) WO-1332 WIDENS THAT PIN to every canonical player-facing string file that
//       names either character, on BOTH twins. WO-1332 was minted on the premise
//       that "34 Alduin vs 30 Aldwin" was one misspelled name and asked for a
//       repo-wide normalise. It is not a misspelling: the sweep found ZERO
//       crossover — every Alduin is the necromancer, every Aldwin is Echo #1 —
//       and the normalise would have merged the two. The (5) pin only covered
//       lore-fragments + canon-strings + the roster, so en.json, enemies.json,
//       glossary.json, guide-content.json and healers-cottage.json were unguarded.
//       They are guarded now, which is why this WO could not be re-minted a third
//       time (WO-881 corrected the same premise in the OTHER direction, 2026-08-05).
// =============================================================================
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class DungeonLoreReadableRegression
    {
        public static bool Run(out string reason)
        {
            string root = Path.Combine(Application.dataPath, "_Modules/Dungeons");
            string lore  = Path.Combine(root, "LoreStone.cs");
            string ctrl  = Path.Combine(root, "DungeonController.cs");
            string modal = Path.Combine(root, "UI/LoreReadingModal.cs");

            var fails = new List<string>();

            // (1) INPUT — a tap must call LoreStone.Read via the shared interact button.
            if (!File.Exists(lore) || !File.ReadAllText(lore).Contains("MobileInteractButton.Request"))
                fails.Add("LoreStone has no input caller for Read() (MobileInteractButton.Request missing) — Read() is unreachable");

            // (2) SUBSCRIBER — DungeonController must wire ReadRequested to the modal.
            if (!File.Exists(ctrl) || !File.ReadAllText(ctrl).Contains("LoreReadingModal.Show"))
                fails.Add("DungeonController does not subscribe LoreStone.ReadRequested -> LoreReadingModal.Show");

            // (3) VIEW — a code-built (ElarionUiKit) reading modal must exist; NO uxml (CLAUDE.md §8).
            if (!File.Exists(modal))
                fails.Add("LoreReadingModal.cs (the reading view) does not exist");
            else if (!File.ReadAllText(modal).Contains("ElarionUiKit"))
                fails.Add("LoreReadingModal is not built via ElarionUiKit (code UI) — uxml does not work in builds");

            // ── (4) SCROLL — WO-881: long entries scroll, they never cull ────────────
            // Journal entry 4 is 4 paragraphs and the 2026-08-04 capture cut it mid-line
            // just above Close. The well must mask AND advertise itself, on fixed-px bands.
            string modalSrc = File.Exists(modal) ? File.ReadAllText(modal) : string.Empty;
            if (modalSrc.Length > 0)
            {
                if (!modalSrc.Contains("ScrollRect") || !modalSrc.Contains("RectMask2D"))
                    fails.Add("LoreReadingModal has no masked ScrollRect well — a long journal entry would be clipped by Close (WO-881)");
                if (!modalSrc.Contains("verticalScrollbar"))
                    fails.Add("LoreReadingModal's scroll well has no scrollbar affordance — masked prose reads as a rendering bug (WO-881)");
                if (!modalSrc.Contains("LayoutElement") || !modalSrc.Contains("minHeight"))
                    fails.Add("LoreReadingModal's prose row has no fixed-px minHeight floor — a zero-measure frame would collapse the column (WO-841/852 class)");
                // Fixed-pixel bands, never fractions of the parent. The retired code used
                // anchor fractions 0.06/0.94 on the viewport; those literals must not return.
                if (modalSrc.Contains("0.06f, 0.06f") || modalSrc.Contains("0.94f, 0.94f"))
                    fails.Add("LoreReadingModal sizes its scroll well by FRACTION of parent — bands must be fixed px (WO-841/852: a fraction band resolved to -11px and TMP culled the text)");
                foreach (string band in new[] { "WellPadX", "HintBandPx", "BodyLinePx" })
                    if (!modalSrc.Contains(band))
                        fails.Add("LoreReadingModal is missing the fixed-pixel band constant '" + band + "' (WO-881)");
                // Text-encoded overflow state (never colour alone).
                if (!modalSrc.Contains("MORE BELOW"))
                    fails.Add("LoreReadingModal has no TEXT-encoded overflow hint — scroll state must be words, not colour alone (WO-881)");
            }

            // ── (5) NAME — Alduin (necromancer) vs Aldwin (Echo #1) ─────────────────
            // Established at source, this session: canon-strings.json "alduin" =
            // "Alduin the Mournful"; EchoRosterCatalog #1 = "Aldwin, the Ice Echo";
            // docs/DUNGEON_DESIGNS.md D2 — "the journal she's been reading is Alduin's".
            // Both spellings are authored canon for DIFFERENT people. Pin both ends so
            // neither a "typo fix" nor a View-side rename can collapse them together.
            string data = Path.Combine(Application.dataPath, "Resources/Data/Canonical");
            string stream = Path.Combine(Application.dataPath, "StreamingAssets/Data/Canonical");
            foreach (string copySource in new[]
                     {
                         Path.Combine(data, "lore-fragments.json"),
                         Path.Combine(stream, "lore-fragments.json"),
                     })
            {
                if (!File.Exists(copySource)) { fails.Add("lore copy source missing: " + copySource); continue; }
                string txt = File.ReadAllText(copySource);
                if (!txt.Contains("Alduin's journal"))
                    fails.Add("lore copy source lost the canon journal owner 'Alduin's journal' (" + Path.GetFileName(copySource) + ") — Alduin the Mournful, NOT the Ice Echo Aldwin (WO-881)");
                if (txt.Contains("Aldwin"))
                    fails.Add("lore copy source names 'Aldwin' (" + Path.GetFileName(copySource) + ") — the journal is Alduin the Mournful's; Aldwin is Echo #1, a different character (WO-881)");
            }
            string strings = Path.Combine(data, "canon-strings.json");
            if (!File.Exists(strings) || !File.ReadAllText(strings).Contains("Alduin the Mournful"))
                fails.Add("canon-strings.json no longer carries 'Alduin the Mournful' — the name authority for the journal owner (WO-881)");
            string roster = Path.Combine(Application.dataPath, "_Modules/Village/Harvest/EchoRosterCatalog.cs");
            if (!File.Exists(roster) || !File.ReadAllText(roster).Contains("Aldwin, the Ice Echo"))
                fails.Add("EchoRosterCatalog no longer carries 'Aldwin, the Ice Echo' — the other half of the Alduin/Aldwin pin (WO-881)");

            // ── (5b) WO-1332: the pin widened to EVERY player-facing canonical string ──
            // WO-881 pinned lore-fragments + canon-strings + the roster. WO-1332 was then
            // minted on the premise that "34 Alduin vs 30 Aldwin" was ONE name spelled two
            // ways and asked for a repo-wide normalise — which would have attributed a
            // necromancer's journal to the player's founding companion. The files below are
            // the ones such a sweep would have rewritten, and none of them were pinned. They
            // are now, on BOTH canonical twins (the Resources copy wins at load, so a
            // one-sided pin is a one-sided pin).
            //   SINGLE-SIDED FILES: the whole file belongs to one character.
            var oneSided = new (string Rel, string Must, string Forbidden, string Who)[]
            {
                ("enemies.json",                  "Alduin",           "Aldwin", "Alduin the Mournful's necromancer entries"),
                ("dungeons/healers-cottage.json", "Alduin's journal", "Aldwin", "Alduin the Mournful's journal lore stones"),
                ("canon-strings.json",            "Alduin the Mournful", "Aldwin", "the boss name authority"),
                ("glossary.json",                 "Aldwin",           "Alduin", "the six-Echo roster"),
                ("guide-content.json",            "Aldwin",           "Alduin", "the six-Echo roster"),
            };
            foreach (var side in oneSided)
            {
                foreach (string baseDir in new[] { data, stream })
                {
                    string path = Path.Combine(baseDir, side.Rel);
                    if (!File.Exists(path)) { fails.Add("canonical string file missing: " + path); continue; }
                    string txt = File.ReadAllText(path);
                    if (!txt.Contains(side.Must))
                        fails.Add(side.Rel + " (" + Path.GetFileName(baseDir) + ") lost '" + side.Must + "' — it authors " + side.Who + " (WO-1332)");
                    if (txt.Contains(side.Forbidden))
                        fails.Add(side.Rel + " (" + Path.GetFileName(baseDir) + ") now names '" + side.Forbidden +
                                  "' — that is the OTHER character. Alduin the Mournful (necromancer boss) and Aldwin, the Ice Echo (#1) are two authored people one letter apart; " +
                                  "this file authors " + side.Who + ". Do not normalise one into the other (WO-1332 / WO-881)");
                }
            }
            //   TWO-SIDED FILE: en.json carries BOTH, keyed. Pin them per line by key.
            foreach (string baseDir in new[] { data, stream })
            {
                string enPath = Path.Combine(baseDir, "en.json");
                if (!File.Exists(enPath)) { fails.Add("canonical string file missing: " + enPath); continue; }
                bool sawBoss = false, sawEcho = false;
                foreach (string raw in File.ReadAllLines(enPath))
                {
                    bool hasAlduin = raw.Contains("Alduin");
                    bool hasAldwin = raw.Contains("Aldwin");
                    if (!hasAlduin && !hasAldwin) continue;
                    bool bossKey = raw.Contains("boss") || raw.Contains("victory") || raw.Contains("milestone");
                    bool echoKey = raw.Contains("iceWolf") || raw.Contains("keeperAmbient");
                    if (hasAlduin && bossKey) sawBoss = true;
                    if (hasAldwin && echoKey) sawEcho = true;
                    if (hasAlduin && echoKey)
                        fails.Add("en.json (" + Path.GetFileName(baseDir) + ") names 'Alduin' on an ECHO/pet line ('" + raw.Trim() +
                                  "') — the ice wolf is Aldwin, Echo #1; Alduin the Mournful is the necromancer boss (WO-1332)");
                    if (hasAldwin && bossKey)
                        fails.Add("en.json (" + Path.GetFileName(baseDir) + ") names 'Aldwin' on a BOSS line ('" + raw.Trim() +
                                  "') — the boss is Alduin the Mournful; Aldwin is Echo #1, the founding companion (WO-1332)");
                }
                if (!sawBoss)
                    fails.Add("en.json (" + Path.GetFileName(baseDir) + ") no longer names Alduin on any boss/victory/milestone line — the boss-side half of the pin is gone (WO-1332)");
                if (!sawEcho)
                    fails.Add("en.json (" + Path.GetFileName(baseDir) + ") no longer names Aldwin on any ice-wolf/keeper line — the Echo-side half of the pin is gone (WO-1332)");
            }
            // The View renders copy; it must never author or correct it (MVVM law). Comments
            // are allowed to explain the pair — only CODE lines would be a View-side edit, so
            // strip comment lines before looking for either name.
            foreach (string raw in modalSrc.Split('\n'))
            {
                string line = raw.TrimStart();
                if (line.StartsWith("//") || line.StartsWith("*") || line.StartsWith("/*")) continue;
                if (line.Contains("Aldwin") || line.Contains("Alduin"))
                {
                    fails.Add("LoreReadingModal has an Alduin/Aldwin name in CODE ('" + line.Trim() +
                              "') — copy is authored in lore-fragments.json and rendered verbatim, never patched in the View (WO-881)");
                    break;
                }
            }

            if (fails.Count == 0)
            {
                Debug.Log("DUNGEON_LORE_OK");
                reason = "DUNGEON LORE OK — lore stones readable: input (MobileInteractButton) + subscriber + code Obsidian modal; " +
                         "WO-881: fixed-px scroll bands + mask + scrollbar + text-encoded overflow hint; " +
                         "Alduin (necromancer) / Aldwin (Echo #1) pinned at the copy source and absent from the View; " +
                         "WO-1332: that pin widened to en.json, enemies.json, glossary.json, guide-content.json and " +
                         "healers-cottage.json on BOTH canonical twins — neither name can be normalised into the other";
                return true;
            }
            reason = "dungeon-lore: " + string.Join("; ", fails);
            Debug.LogError("DUNGEON_LORE_FAIL: " + reason);
            return false;
        }
    }
}
