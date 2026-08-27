// =============================================================================
// DefenseReportPanel — the re-openable record of attacks on YOUR town (WO-1026).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// A DUMB SKIN over DefenseReportLedger. Master-detail on the FrameQuest grammar,
// exactly like GameGuidePanel: the dark left well carries the report LIST, the
// parchment right well carries the SELECTED report's detail.
//
// CONSTRUCTION LAW (non-negotiable here as everywhere):
//   • UXML DOES NOT WORK IN BUILDS — code-built uGUI via ElarionUiKit only.
//   • ASCII ONLY in every TMP string. LiberationSans-SDF tofus anything else, so:
//     "->" not an arrow, "..." not an ellipsis, "40%" not a fancy percent.
//   • ⛔ NEVER CONVEY MEANING BY COLOUR ALONE — the owner is red/green colourblind.
//     Every state on this screen is a SENTENCE: "OVERRUN - the Heart fell",
//     "DESTROYED", "damaged 40%", "Nothing was taken." Tints are decoration on top
//     of text that already says it. A greyscale screenshot must lose no information.
//   • Fixed-pixel row bands via LayoutElement — the kit scroll column does NOT
//     control child height (the documented PartyShop collapse).
//
// ⛔ IT NEVER READS WaveDamageReport, AND THAT IS LOAD-BEARING.
//    The panel renders the PERSISTED RECORD and nothing else. A panel that re-scanned
//    the live town could never render a report from a week ago (the town has changed)
//    and could never render a model-(c) ghost's report at all — which would quietly
//    turn the (c) source swap back into a rewrite. SiegeSpawnAuthorityRegression fails
//    the gate if a WaveDamageReport reference appears in this file.
//
// THE DOOR IS DELIBERATELY NOT MINTED HERE. CLAUDE.md §7 caps the calm(town) action
// bar at SIX visible faces and spends paragraphs on why; adding a seventh to reach
// this screen would silently undo that ruling. The panel ships REGISTERED and openable
// (PanelRouter.Open(PanelId.DefenseReport) + the DevPanel). Picking the town door — a
// badge on the Heart interaction, or a Manage-screen tab — is an owner call, recorded
// in the WO-1026 result.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Defense;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village.UI
{
    /// <summary>Lists the retained defence reports and renders the selected one.</summary>
    [DisallowMultipleComponent]
    public sealed class DefenseReportPanel : MonoBehaviour
    {
        private const float ListRowPx = 132f;   // >= MinTouchPx (112) with room for two text lines
        private const float MapPlatePx = 420f;  // fixed band for the diagram (the scroll column
                                                // does not control child height -- §1.14 kit rule)

        private GameObject _ui;
        private RectTransform _listContent;
        private RectTransform _detailContent;
        private PanelHandle _panelHandle;
        private bool _onParchment;

        private List<DefenseOutcomeRecord> _rows = new List<DefenseOutcomeRecord>();
        private string _selectedId;

        /// <summary>The built map plate, held ONLY so its path segments can be re-solved after
        /// the layout pass gives the plate a real pixel size (see DefenseMapPlate.Plate.Relayout
        /// -- without this the polyline renders as a row of 2px stubs).</summary>
        private DefenseMapPlate.Plate _plate;

        /// <summary>True while the screen is up (built on open, destroyed on close).</summary>
        public bool IsOpen => _ui != null;

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Defense Report", Close, () => IsOpen);
            PanelRouter.Register(PanelId.DefenseReport, Open);
        }

        private void OnDestroy()
        {
            if (_ui != null) Destroy(_ui);
            _ui = null;
            PanelRouter.Unregister(PanelId.DefenseReport, Open);
        }

        // ── Open / Close ─────────────────────────────────────────────────────────

        /// <summary>Opens the screen on the newest report.</summary>
        public void Open()
        {
            Close();

            _rows = DefenseReportLedger.NewestFirst();
            if (_rows.Count > 0 && string.IsNullOrEmpty(_selectedId))
                _selectedId = _rows[0].Id;

            BuildChrome();
            Render();

            if (!PanelManager.NotifyOpened(_panelHandle))
                return;   // rejected (e.g. mid-battle) — NotifyOpened already invoked Close

            FlowTrace.Step("Siege",
                $"report panel opened id={_selectedId} reports={_rows.Count} unread={DefenseReportLedger.UnreadCount()}.");
        }

        private void Close()
        {
            _listContent = null;
            _detailContent = null;
            if (_ui != null) Destroy(_ui);
            _ui = null;
            PanelManager.NotifyClosed(_panelHandle);
        }

        // ── Chrome (presentation only — the frame IS the chrome) ─────────────────

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("DefenseReportPanelUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "Attacks On Your Town",
                new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f), Close,
                frameName: RpgUiCatalog.FrameQuest, medallionIcon: "quest");

            var layout = chrome.layout;
            Transform listZone = layout != null && layout.bodyLeft != null
                ? (Transform)layout.bodyLeft
                : FallbackZone(chrome.content.transform, "ListWell",
                    new Vector2(0.035f, 0.22f), new Vector2(0.295f, 0.885f));
            Transform detailZone = layout != null && layout.bodyRight != null
                ? (Transform)layout.bodyRight
                : FallbackZone(chrome.content.transform, "DetailWell",
                    new Vector2(0.320f, 0.22f), new Vector2(0.965f, 0.885f));
            _onParchment = layout != null && layout.bodyRight != null;

            _listContent = ElarionUiKit.MakeScrollZone(listZone, spacing: 8f, padding: 8).content;
            _detailContent = ElarionUiKit.MakeScrollZone(detailZone, spacing: 12f, padding: 16).content;
        }

        // ── Render ───────────────────────────────────────────────────────────────

        private void Render()
        {
            RebuildList();
            RebuildDetail();

            Canvas.ForceUpdateCanvases();
            if (_listContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(_listContent);
            if (_detailContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(_detailContent);

            // The plate's rect only becomes real after the rebuild above, so the path geometry
            // is solved HERE rather than at build time.
            _plate?.Relayout();
        }

        private void RebuildList()
        {
            ClearChildren(_listContent);
            if (_listContent == null) return;

            if (_rows.Count == 0)
            {
                Paragraph(_listContent, "No attacks recorded.", ElarionUi.FontLabel, ElarionUi.ParchmentDim, false);
                return;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                if (r == null) continue;
                string id = r.Id;
                bool selected = id == _selectedId;

                // The row label carries EVERY state in words: verdict, who, when, unread.
                // A greyscale capture of this list loses nothing.
                string label = OutcomeWord(r.Outcome) + "\n" + Safe(r.Attacker.DisplayName, "Unknown force")
                             + "  -  " + RelativeTime(r.EndedAtUnixMs)
                             + (r.Read ? string.Empty : "  [NEW]");

                var host = new GameObject("ReportRow", typeof(RectTransform), typeof(LayoutElement));
                host.transform.SetParent(_listContent, false);
                var le = host.GetComponent<LayoutElement>();
                le.preferredHeight = ListRowPx;
                le.minHeight = ListRowPx;

                ElarionUiKit.BuildObsidianButton(host.transform, label,
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    selected ? ElarionUiKit.ObsidianButtonColor.Yellow
                             : ElarionUiKit.ObsidianButtonColor.Gray,
                    Vector2.zero, Vector2.one, () => Select(id));
            }
        }

        private void Select(string reportId)
        {
            _selectedId = reportId;
            DefenseReportLedger.MarkRead(reportId);
            _rows = DefenseReportLedger.NewestFirst();
            Render();
        }

        private void RebuildDetail()
        {
            _plate = null;   // destroyed with the cleared children below
            ClearChildren(_detailContent);
            if (_detailContent == null) return;

            Color inkTitle = _onParchment ? ElarionUiKit.ParchmentInk : ElarionUi.Gilt;
            Color inkBody = _onParchment ? ElarionUiKit.ParchmentInk : ElarionUi.Parchment;
            Color inkDim = _onParchment ? ElarionUiKit.ParchmentInkDim : ElarionUi.ParchmentDim;

            var r = DefenseReportLedger.TryGet(_selectedId);
            if (r == null)
            {
                Paragraph(_detailContent,
                    "Your town has not been attacked yet. When it is, the report lands here: who came, " +
                    "where they broke through, and what it cost you.",
                    ElarionUi.FontBody, inkDim, false);
                return;
            }

            // ── Verdict (a full sentence, never a colour) ───────────────────────
            Paragraph(_detailContent, OutcomeWord(r.Outcome), ElarionUi.FontHead, inkTitle, true);
            Paragraph(_detailContent, OutcomeSentence(r.Outcome), ElarionUi.FontLabel, inkDim, false);
            // The score is a LABEL. It prints only when it was actually derived -- a declined
            // score shows NOTHING rather than a placeholder, the same rule as an unmeasured
            // hold time. Number AND word, so it survives greyscale.
            if (r.HasDefenseScore)
                Paragraph(_detailContent,
                    "Defence score " + r.DefenseScore + "/100  -  "
                    + DefenseReportBuilder.DefenseScoreWord(r.DefenseScore),
                    ElarionUi.FontLabel, inkDim, false);

            // ── Attacker. The panel renders THESE STRINGS. It never composes a name
            //    from the Source enum — that is what keeps model (c) a source swap. The
            //    single sanctioned Source read is the small chip below: a LABEL LOOKUP.
            Paragraph(_detailContent, "ATTACKER", ElarionUi.FontLabel, inkDim, true);
            Paragraph(_detailContent,
                Safe(r.Attacker.DisplayName, "Unknown force") + "   (" + SourceChip(r.Attacker.Source) + ")",
                ElarionUi.FontBody, inkBody, false);
            Paragraph(_detailContent,
                "Strength " + r.Attacker.PowerRating + "   -   wave " + r.WaveId
                + "   -   lasted " + Mathf.RoundToInt(r.DurationSeconds) + "s",
                ElarionUi.FontLabel, inkDim, false);
            if (r.Attacker.Units.Count == 0)
                Paragraph(_detailContent, "-  (no roster recorded)", ElarionUi.FontLabel, inkDim, false);
            for (int i = 0; i < r.Attacker.Units.Count; i++)
            {
                var u = r.Attacker.Units[i];
                if (u == null) continue;
                Paragraph(_detailContent, "-  x" + u.Count + "  " + Safe(u.DefId, "unknown")
                    + "  (level " + Mathf.Max(1, u.Level) + ")", ElarionUi.FontLabel, inkBody, false);
            }

            // ── Your base at the time ──────────────────────────────────────────
            Paragraph(_detailContent, "YOUR BASE AT THE TIME", ElarionUi.FontLabel, inkDim, true);
            Paragraph(_detailContent,
                r.Defender.StructureCount + " structures  -  " + r.Defender.WallCount + " wall sections  -  "
                + r.Defender.TowerCount + " towers  -  hero "
                + (r.Defender.HeroPresent ? "present" : "absent"),
                ElarionUi.FontLabel, inkBody, false);
            Paragraph(_detailContent, "Layout " + Safe(r.Defender.LayoutHash, "unknown")
                + "  (this changes when you move a structure)", ElarionUi.FontLabel, inkDim, false);

            // ── ⭐ THE DIAGNOSIS. The single most important line on the screen, placed
            //    ABOVE the diagram and above the lists, because it is the sentence the whole
            //    feature exists to produce: not "what did I lose" but "what do I move".
            Paragraph(_detailContent, "WHAT WENT WRONG", ElarionUi.FontLabel, inkDim, true);
            var diagnosis = Diagnose(r);
            for (int i = 0; i < diagnosis.Count; i++)
                Paragraph(_detailContent, diagnosis[i], ElarionUi.FontBody, inkBody, i == 0);

            // ── The plate. DECORATIVE BY DESIGN: every fact on it is also stated in words
            //    above and below, so a reader who cannot parse the diagram (or for whom it
            //    fails to build) loses nothing. ──────────────────────────────────
            BuildMapPlate(_detailContent, r, inkDim);

            // ── Breaches — THE REDESIGN SIGNAL ─────────────────────────────────
            Paragraph(_detailContent, "WHERE THEY GOT IN", ElarionUi.FontLabel, inkDim, true);
            if (r.Breaches.Count == 0)
            {
                Paragraph(_detailContent, "Nothing crossed your inner ring. The line held.",
                    ElarionUi.FontBody, inkBody, false);
            }
            else
            {
                for (int i = 0; i < r.Breaches.Count; i++)
                {
                    var b = r.Breaches[i];
                    if (b == null) continue;
                    // The FIRST breach is called out in words ("1st") -- the ordinal is never
                    // implied by colour or position alone.
                    string ord = i == 0 ? "1st" : (i + 1) + (i == 1 ? "nd" : i == 2 ? "rd" : "th");
                    Paragraph(_detailContent,
                        "-  " + ord + ": " + Safe(b.DisplayName, "Open ground")
                        + "  at " + Mathf.RoundToInt(b.AtSeconds) + "s"
                        + "  by " + Safe(b.AttackerDefId, "unknown")
                        + "   (" + DefenseMapPlate.Compass(b.WorldX - r.Defender.CoreX,
                                                           b.WorldZ - r.Defender.CoreZ)
                        + " of the Heart)",
                        ElarionUi.FontLabel, inkBody, i == 0);
                }
            }

            // ── Rows, GROUPED BY LINE. Grouping is the cheap half of the diagnosis:
            //    "my whole front line fell and nothing behind it was touched" is a thought a
            //    flat list cannot produce. ────────────────────────────────────────
            Paragraph(_detailContent, "WHAT BROKE", ElarionUi.FontLabel, inkDim, true);
            if (r.Rows.Count == 0)
            {
                Paragraph(_detailContent, "Nothing was damaged.", ElarionUi.FontBody, inkBody, false);
            }
            else
            {
                RenderBandGroup(r, DefenseBand.Front, "FRONT LINE (they meet this first)", inkBody, inkDim);
                RenderBandGroup(r, DefenseBand.Second, "SECOND LINE", inkBody, inkDim);
                RenderBandGroup(r, DefenseBand.Core, "CORE (the Heart's ring)", inkBody, inkDim);
            }

            // ── ResourcesLost — an EXPLICIT statement, never a blank ───────────────────
            Paragraph(_detailContent, "WHAT IT COST YOU", ElarionUi.FontLabel, inkDim, true);
            Paragraph(_detailContent, StakesLine(r.ResourcesLost), ElarionUi.FontBody, inkBody, false);
        }

        // ── ⭐ THE LEGIBILITY LAYER ──────────────────────────────────────────────

        /// <summary>
        /// The plate, plus its legend and its text twin. The plate is DECORATION over facts
        /// already stated in words: <see cref="DefenseMapPlate.DescribeMarks"/> prints the
        /// headline marks as sentences, and the legend spells every glyph out. If the plate
        /// fails to build, the screen is still complete.
        /// </summary>
        private void BuildMapPlate(RectTransform host, DefenseOutcomeRecord r, Color inkDim)
        {
            // Text twin FIRST, so it is present regardless of what the plate does.
            var described = DefenseMapPlate.DescribeMarks(r);
            for (int i = 0; i < described.Count; i++)
                Paragraph(host, described[i], ElarionUi.FontLabel, inkDim, false);

            // Fixed-pixel band: the kit scroll column does NOT control child height, so the
            // plate carries its own LayoutElement (the documented PartyShop-collapse rule).
            var band = new GameObject("MapPlateBand", typeof(RectTransform), typeof(LayoutElement));
            band.transform.SetParent(host, false);
            var le = band.GetComponent<LayoutElement>();
            le.preferredHeight = MapPlatePx;
            le.minHeight = MapPlatePx;

            _plate = DefenseMapPlate.Build(band.transform, r);
            if (_plate == null)
            {
                Paragraph(host, "(map unavailable -- the positions above still describe it)",
                    ElarionUi.FontLabel, inkDim, false);
                return;
            }

            for (int i = 0; i < DefenseMapPlate.Legend.Length; i++)
                Paragraph(host, DefenseMapPlate.Legend[i], ElarionUi.FontLabel, inkDim, false);
        }

        /// <summary>
        /// Renders one FRONT / SECOND / CORE band, or nothing when it is empty. An empty band is
        /// deliberately silent rather than printed as "none": three headers with two "none"s
        /// under them buries the one band that actually matters.
        /// </summary>
        private void RenderBandGroup(DefenseOutcomeRecord r, DefenseBand band, string header,
            Color inkBody, Color inkDim)
        {
            var rows = new List<StructureOutcome>();
            for (int i = 0; i < r.Rows.Count; i++)
                if (r.Rows[i] != null && r.Rows[i].Band == band) rows.Add(r.Rows[i]);
            if (rows.Count == 0) return;

            Paragraph(_detailContent, header, ElarionUi.FontLabel, inkDim, true);
            for (int i = 0; i < rows.Count; i++)
            {
                var l = rows[i];
                // State in WORDS: "DESTROYED" / "damaged 40%" — never a coloured bar alone.
                string state = l.Destroyed
                    ? "DESTROYED"
                    : "damaged " + Mathf.RoundToInt(l.DamageFraction * 100f) + "%";
                string text = "-  " + Safe(l.DisplayName, "Structure") + "  -  " + state;
                // THE row that matters: they came through HERE, versus a row that merely took
                // splash damage. Identical-looking in a flat list, opposite instructions.
                if (l.BreachOrdinal == 1) text += "  -  THEY CAME THROUGH HERE";
                else if (l.BreachOrdinal > 1) text += "  -  breach #" + l.BreachOrdinal;
                if (l.LootStolen > 0) text += "  -  " + l.LootStolen + " carried off";
                Paragraph(_detailContent, text, ElarionUi.FontLabel, inkBody, false);

                string hold = HoldLine(l);
                if (!string.IsNullOrEmpty(hold))
                    Paragraph(_detailContent, "      " + hold, ElarionUi.FontLabel, inkDim, false);
                // Cost is OMITTED, never faked — HasCost carries that, same as the live banner.
                if (l.HasCost)
                    Paragraph(_detailContent, "      repair: " + CostLine(l), ElarionUi.FontLabel, inkDim, false);
            }
        }

        /// <summary>
        /// ⭐ THE HOLD-TIME SENTENCE — the highest-signal line in the report.
        /// "held 40s" and "fell in 4s" are the same row with opposite instructions.
        ///
        /// <para>⛔ An UNKNOWN hold time prints NOTHING. It must never render as "fell in 0s":
        /// a fabricated duration would point the player at the wrong structure, which is
        /// strictly worse than telling them nothing. Pre-existing damage says so explicitly,
        /// because that row's timing belongs to an earlier fight.</para>
        /// </summary>
        private static string HoldLine(StructureOutcome l)
        {
            if (l.WasAlreadyDamaged)
                return "was already damaged before this attack -- hold time is from an earlier fight";
            if (!l.HasHoldTime) return string.Empty;

            int s = Mathf.RoundToInt(l.HoldTimeSeconds);
            if (l.Destroyed)
                return s <= 5
                    ? "fell in " + s + "s -- it barely slowed them"
                    : "held " + s + "s before it fell";
            return "held " + s + "s and survived";
        }

        /// <summary>
        /// The report's headline, in sentences the player can act on. Built ONLY from recorded
        /// fields — it never re-scans the town, so it reads identically for an old report or
        /// (later) a model-(c) ghost's.
        /// <para>Every claim here is one the data actually supports. Where the data is thin the
        /// diagnosis says less rather than guessing: an invented cause is exactly the thing that
        /// makes a player move the wrong tower and conclude the report lies.</para>
        /// </summary>
        private static List<string> Diagnose(DefenseOutcomeRecord r)
        {
            var outLines = new List<string>();

            // 1. The approach + the first breach — where to look.
            var first = r.Breaches.Count > 0 ? r.Breaches[0] : null;
            if (first != null)
            {
                outLines.Add("They got in "
                    + DefenseMapPlate.Compass(first.WorldX - r.Defender.CoreX,
                                              first.WorldZ - r.Defender.CoreZ)
                    + " of the Heart, " + Mathf.RoundToInt(first.AtSeconds) + "s in"
                    + (string.IsNullOrEmpty(first.DisplayName) || first.DisplayName == "Open ground"
                        ? ", across open ground." : ", past " + first.DisplayName + "."));
            }
            else if (r.Outcome == DefenseOutcome.Overrun)
            {
                outLines.Add("The Heart fell without a recorded ring crossing.");
            }
            else
            {
                outLines.Add("Your ring held -- nothing got inside it.");
            }

            // 2. The weakest structure BY TIME. This is the "what do I move" line, and it is
            //    only offered when a real measurement exists.
            StructureOutcome weakest = null;
            for (int i = 0; i < r.Rows.Count; i++)
            {
                var l = r.Rows[i];
                if (l == null || !l.Destroyed || !l.HasHoldTime) continue;
                if (weakest == null || l.HoldTimeSeconds < weakest.HoldTimeSeconds) weakest = l;
            }
            if (weakest != null)
                outLines.Add("Weakest point: " + Safe(weakest.DisplayName, "a structure")
                    + " fell in " + Mathf.RoundToInt(weakest.HoldTimeSeconds) + "s ("
                    + LineWord(weakest.Band) + ").");

            // 3. Did the front line do its job? Only stated when there IS a front line.
            int frontLost = 0, frontTotal = 0, behindLost = 0;
            for (int i = 0; i < r.Rows.Count; i++)
            {
                var l = r.Rows[i];
                if (l == null) continue;
                if (l.Band == DefenseBand.Front) { frontTotal++; if (l.Destroyed) frontLost++; }
                else if (l.Destroyed) behindLost++;
            }
            if (r.Defender.FrontRadius <= 0f)
                outLines.Add("You have no wall ring, so nothing meets them before your buildings do.");
            else if (frontLost > 0 && behindLost == 0)
                outLines.Add("Your front line absorbed all of it -- " + frontLost
                    + " lost there and nothing behind it was touched.");
            else if (frontTotal == 0 && behindLost > 0)
                outLines.Add("They reached past your front line without breaking it -- check for a gap, not a weak wall.");

            return outLines;
        }

        private static string LineWord(DefenseBand l)
        {
            switch (l)
            {
                case DefenseBand.Front: return "front line";
                case DefenseBand.Core: return "core";
                default: return "second line";
            }
        }

        // ── Copy helpers (every state is a SENTENCE — colourblind law) ───────────

        private static string OutcomeWord(DefenseOutcome o)
        {
            switch (o)
            {
                case DefenseOutcome.Overrun: return "OVERRUN";
                case DefenseOutcome.Breached: return "BREACHED";
                default: return "HELD";
            }
        }

        private static string OutcomeSentence(DefenseOutcome o)
        {
            switch (o)
            {
                case DefenseOutcome.Overrun: return "The Heart fell. They took the town.";
                case DefenseOutcome.Breached: return "You won, but they got inside your ring.";
                default: return "They never reached your inner ring.";
            }
        }

        /// <summary>The ONE sanctioned read of AttackerSource in presentation: a LABEL LOOKUP,
        /// not a branch in the layout. Everything else on this screen renders the record's own
        /// strings, which is what keeps model (c) a source swap.</summary>
        private static string SourceChip(AttackerSource s)
        {
            switch (s)
            {
                case AttackerSource.GhostSnapshot: return "echo of a rival town";
                case AttackerSource.LivePvp: return "live rival";
                default: return "raiders";
            }
        }

        private static string CostLine(StructureOutcome l)
        {
            var parts = new List<string>();
            if (l.RepairWood > 0) parts.Add(l.RepairWood + " wood");
            if (l.RepairIron > 0) parts.Add(l.RepairIron + " iron");
            if (l.RepairFood > 0) parts.Add(l.RepairFood + " stone");
            if (l.RepairCrystals > 0) parts.Add(l.RepairCrystals + " crystals");
            return parts.Count == 0 ? "free" : string.Join(", ", parts);
        }

        /// <summary>
        /// WHAT THE ATTACK TOOK -- an EXPLICIT statement either way, never a blank the player
        /// reads as a bug.
        ///
        /// <para>* THIS IS THE ONLY PLACE A THEFT IS EVER ANNOUNCED, and it renders the ledger
        /// VERBATIM. The ledger IS the debit (DefenseReportBuilder.ApplyStakes spends exactly these
        /// buckets), so this screen cannot tell the player a different number than the wallet lost
        /// -- there is nothing to re-derive here. An unexplained shrinking number is the resented
        /// version of this mechanic; a report that names it is the loop working.</para>
        ///
        /// <para>! THE COPY MUST TEACH THE RULE, because the rule is what turns the loss into
        /// "damn, I should improve my defenses" instead of "the game erased something I paid for".
        /// It has three jobs: name what was taken, name that a RESERVE was protected and a CAP
        /// held, and name what can NEVER be touched. Crystals and purchases are called out BY NAME
        /// -- a player who is told her crystals are safe does not go looking to see whether they
        /// are.</para>
        ///
        /// <para>Owner ruling 2026-08-27: LOOTABLE = wood, iron, stone, gold. UNTOUCHABLE =
        /// crystals, SKR, purchased goods, equipped gear. "Stone" is the balance internally named
        /// Food, and it is rendered with the player-facing word.</para>
        ///
        /// <para>Every state is carried by TEXT. The owner is colourblind: this must read the same
        /// in greyscale, so nothing here depends on a tint.</para>
        /// </summary>
        private static string StakesLine(StakesLedger s)
        {
            if (s == null || s.IsEmpty)
                return "Nothing was taken.\n(Your reserve held -- raiders can never dig below it, " +
                       "and crystals, purchases and equipped gear are never at risk.)";

            var parts = new List<string>();
            if (s.Wood > 0) parts.Add(s.Wood + " wood");
            if (s.Iron > 0) parts.Add(s.Iron + " iron");
            if (s.Food > 0) parts.Add(s.Food + " stone");
            if (s.Coins > 0) parts.Add(s.Coins + " gold");
            // Crystals/Magic can NEVER be taken. They are listed only so that if one ever appeared
            // it would be VISIBLE on screen rather than silently hidden by a renderer that "knows"
            // it cannot happen.
            if (s.Crystals > 0) parts.Add(s.Crystals + " crystals");
            if (s.Magic > 0) parts.Add(s.Magic + " magic");

            return "They carried off " + string.Join(", ", parts) +
                   ".\n(A protected reserve was left untouched and one attack can never take more " +
                   "than its cap. Crystals, purchases and equipped gear are never at risk -- " +
                   "stronger defences are what keep the rest.)";
        }

        private static string RelativeTime(double whenUnixMs)
        {
            if (whenUnixMs <= 0) return "recently";
            double deltaMs = TimeSource.NowUnixMs() - whenUnixMs;
            if (deltaMs < 0) deltaMs = 0;
            double mins = deltaMs / 60000.0;
            if (mins < 1) return "just now";
            if (mins < 60) return Mathf.RoundToInt((float)mins) + " min ago";
            double hours = mins / 60.0;
            if (hours < 24) return Mathf.RoundToInt((float)hours) + "h ago";
            return Mathf.RoundToInt((float)(hours / 24.0)) + "d ago";
        }

        private static string Safe(string s, string fallback)
            => string.IsNullOrEmpty(s) ? fallback : s;

        // ── Builders (layout plumbing only — chrome comes from the kit) ──────────

        private static void Paragraph(Transform parent, string text, int size, Color color, bool bold)
        {
            var go = new GameObject("Para", typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = text ?? string.Empty;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TMPro.TextWrappingModes.Normal;
            t.raycastTarget = false;
            if (bold) t.fontStyle = FontStyles.Bold;
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        private static Transform FallbackZone(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go.transform;
        }

        private static void ClearChildren(RectTransform host)
        {
            if (host == null) return;
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                var c = host.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }
        }
    }
}
