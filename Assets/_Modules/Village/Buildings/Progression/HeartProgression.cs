// =============================================================================
// HeartProgression — the MODEL behind the player-facing HEART LEVEL (WO-2003).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// ⛔ THE DEFECT THIS EXISTS FOR (owner, 2026-09-06: "wire the heart"): the ONLY
// runtime writer of the realm-progression gate is VillageTierService.TryUpgrade,
// and until today its ONLY caller was BuildingUpgradeVM.Select(VillageTierRowId)
// (BuildingUpgradeVM.cs:1027), reachable ONLY from the action band in
// BuildingUpgradePanelMvvm.cs:1322-1338 — which is painted ONLY while the player
// happens to be looking at a building whose NEXT tier is village-gated. So the
// control that gates nearly all content had no direct route at all. The owner
// could not find it, which is exactly the "a thing built with no door" species
// CLI_DRIVING_PLAN §1 names.
//
// THE SHAPE (canon §6 + owner ruling 11): the STORED field stays GameState
// .VillageTier and the SOLE WRITER stays VillageTierService.TryUpgrade — save
// compatibility is explicitly allowed to keep the old name (WO-2003 "Save
// compatibility"). What changes is that every PLAYER-VISIBLE word becomes
// HEART LEVEL, and that the model — not a view — owns level / cost / affordability
// / state / the unlock preview.
//
// ⚠ DERIVED, NEVER TYPED. UnlocksAt() walks BuildingTierCatalog and reports the
// rows whose authored requiresVillageTier equals the level being previewed. It
// hardcodes NOTHING. If content later authors a village gate on troops, defenses
// or buildable reach, this method reports them the moment the DATA carries them —
// see the NOTE on UnlocksAt for what the data does NOT carry today.
//
// ⚠ WO-2004 (2026-09-06) CLOSED THE OTHER HALF. Two things changed and neither is
// a re-balance: (1) the LADDER left the code — VillageTierService's `const MaxTier
// = 3` and `250 * next` now live in heart-progression.json behind
// HeartProgressionCatalog, the single authoritative progression table WO-2004's
// acceptance criteria demand; (2) UnlocksAt learned to derive TRANSITIVELY, so a
// Heart Level that opens a Barracks rung now also reports the TROOPS that rung
// unlocks (owner ruling 21), plus the population cap and the Echo workforce slots
// the level satisfies. Still zero authored unlock lists — the derivation is the
// authority, and a second table is the thing being prevented.
//
// ⚠ THERE IS NO DURATION AND NO QUEUE FOR A HEART UPGRADE. VillageTierService
// .TryUpgrade is INSTANT (spend crystals -> tier+1 -> Save -> Recompute). WO-2003
// asks for "upgrade duration" and an "active/in-queue state"; NEITHER EXISTS in
// the live service and this file deliberately does not invent them. That is a
// recorded gap for the owner to rule on, not something papered over here.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.State;

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>What kind of thing a Heart level opens. Presentation picks an icon from this;
    /// it never parses the text.</summary>
    public enum HeartUnlockKind
    {
        /// <summary>A building ladder rung becomes upgradable.</summary>
        BuildingLevel = 0,
        /// <summary>A research perk becomes buyable.</summary>
        Research = 1,
        /// <summary>A troop type becomes reachable, via a Barracks rung this level opens (WO-2004).</summary>
        Troop = 2,
        /// <summary>The village population ceiling rises (WO-2004).</summary>
        PopulationCap = 3,
        /// <summary>An Echo workforce slot's Heart-Level condition is satisfied (WO-2004).</summary>
        EchoSlot = 4,
    }

    /// <summary>One line of the "what this Heart level opens" preview. Model-authored text.</summary>
    public readonly struct HeartUnlock
    {
        public readonly HeartUnlockKind Kind;
        /// <summary>Player-facing line, already composed ("Armorer Level 2").</summary>
        public readonly string Text;
        /// <summary>The building id the line belongs to (routing/art), never parsed for meaning.</summary>
        public readonly string BuildingId;

        public HeartUnlock(HeartUnlockKind kind, string text, string buildingId)
        {
            Kind = kind; Text = text; BuildingId = buildingId;
        }
    }

    /// <summary>The action state of the Heart's ONE upgrade control. Explicit and model-owned
    /// (canon §7): a view must never infer it from button interactability.</summary>
    public enum HeartActionState
    {
        /// <summary>Affordable right now — the CTA is live.</summary>
        Ready = 0,
        /// <summary>Short of crystals. The CTA is inert and the sentence says by how much.</summary>
        MissingCrystals = 1,
        /// <summary>Already at the highest authored Heart Level — no CTA at all.</summary>
        Max = 2,
        /// <summary>
        /// Crystals are not the blocker: an authored prerequisite for the next level is unmet, or the
        /// next level has no authored row at all (WO-2004 requirements lane, 2026-09-07).
        /// <para>⚠ ADDED AS A NEW VALUE RATHER THAN FOLDED INTO <see cref="MissingCrystals"/>, which
        /// would have made the Heart say "you need N crystals" to a player who already has them. The
        /// two live consumers were read at source before adding it and both are safe: the Heart panel
        /// tests <c>== Max</c> / <c>== Ready</c> by equality (HeartPanel.cs:266/276/298/311/321), and
        /// <c>ManageScreenVM.HeartUpgradeAvailable</c> is <c>!= Max</c> (ManageScreenVM.cs:583-585) —
        /// neither is a switch that could throw and neither indexes an array by the enum.</para>
        /// <para>⚠ UNREACHABLE ON SHIPPED DATA TODAY, on purpose: every row in
        /// heart-progression.json authors an EMPTY requirement list, so this is the shape working,
        /// not a gate anyone will feel. The owner rules on whether to fill one.</para>
        /// </summary>
        MissingPrerequisite = 3,
    }

    /// <summary>
    /// One authored prerequisite on a Heart Level, resolved against what the player actually holds.
    /// Model-composed text (canon §9) — the view binds <see cref="Text"/> and never re-derives it.
    /// </summary>
    public readonly struct HeartRequirement
    {
        /// <summary>The building ladder id the requirement names (routing/art; never parsed for meaning).</summary>
        public readonly string BuildingId;
        /// <summary>The building level that ladder must have reached.</summary>
        public readonly int RequiredLevel;
        /// <summary>The level that ladder stands at right now.</summary>
        public readonly int CurrentLevel;
        /// <summary>True when <see cref="CurrentLevel"/> already meets <see cref="RequiredLevel"/>.</summary>
        public readonly bool Satisfied;
        /// <summary>Player-facing line, already composed ("Barracks Level 2 (you have Level 1)").</summary>
        public readonly string Text;

        public HeartRequirement(string buildingId, int requiredLevel, int currentLevel, string text)
        {
            BuildingId = buildingId;
            RequiredLevel = requiredLevel;
            CurrentLevel = currentLevel;
            Satisfied = currentLevel >= requiredLevel;
            Text = text;
        }
    }

    /// <summary>
    /// Everything one Heart Level is, resolved in ONE pass: whether it is authored at all, what it
    /// costs, what it demands, and what it opens.
    ///
    /// <para>⛔ THE POINT OF BUNDLING THEM. Before 2026-09-07 a caller wanting the whole picture had
    /// to make four separate static calls in the right order and know that an unauthored level
    /// returned <c>cost 0</c> / <c>requirements empty</c> / <c>unlocks empty</c> — three answers that
    /// each read like "this level is free and unconditional" and were in fact "this level does not
    /// exist". <see cref="HeartProgression.ResolveBundle"/> answers once, carries
    /// <see cref="IsAuthored"/> explicitly, and traces the resolve, so a data hole cannot be mistaken
    /// for a design (CLAUDE.md §12: no silent failure).</para>
    /// </summary>
    public readonly struct HeartLevelBundle
    {
        /// <summary>The Heart Level this bundle describes.</summary>
        public readonly int Level;
        /// <summary>FALSE when heart-progression.json has no row for <see cref="Level"/>. ⛔ Check this
        /// before believing the other three fields — an unauthored level yields empty everything.</summary>
        public readonly bool IsAuthored;
        /// <summary>Crystals to reach <see cref="Level"/> (0 when unauthored). ⛔ Owner rules on balance.</summary>
        public readonly int CostCrystal;
        /// <summary>Authored prerequisites, resolved against the player's ladders. Never null.</summary>
        public readonly IReadOnlyList<HeartRequirement> Requirements;
        /// <summary>What reaching the level opens, DERIVED (see <see cref="HeartProgression.UnlocksAt"/>).
        /// Never null.</summary>
        public readonly IReadOnlyList<HeartUnlock> Unlocks;

        public HeartLevelBundle(int level, bool isAuthored, int costCrystal,
                                IReadOnlyList<HeartRequirement> requirements,
                                IReadOnlyList<HeartUnlock> unlocks)
        {
            Level = level;
            IsAuthored = isAuthored;
            CostCrystal = costCrystal;
            Requirements = requirements ?? System.Array.Empty<HeartRequirement>();
            Unlocks = unlocks ?? System.Array.Empty<HeartUnlock>();
        }

        /// <summary>The first unmet prerequisite, or null when every one is satisfied.</summary>
        public string FirstUnmet()
        {
            for (int i = 0; i < Requirements.Count; i++)
                if (!Requirements[i].Satisfied) return Requirements[i].Text;
            return null;
        }
    }

    /// <summary>
    /// Read/act surface over the realm-progression spine, in PLAYER words. Every number here is
    /// derived from <see cref="VillageTierService"/> or from authored catalog data; nothing is
    /// stored twice and nothing is typed by hand.
    /// </summary>
    public static class HeartProgression
    {
        /// <summary>The player-facing name of the spine. One place, so the three retired
        /// vocabularies ("Village Tier" / "Village Level" / "village tier") cannot come back
        /// piecemeal.</summary>
        public const string LevelWord = "Heart Level";

        /// <summary>The Heart Level the player holds (0 on a fresh save).</summary>
        public static int Level => VillageTierService.Current;

        /// <summary>Highest authored Heart Level. ⛔ Owner rules on balance — read, never raise
        /// here.
        /// <para>⚠ CORRECTED BY WO-2004: the ceiling is no longer a C# constant. It is authored in
        /// <c>heart-progression.json</c> (<c>maxLevel</c>), read by
        /// <see cref="DeNelle.Core.State.HeartProgressionCatalog.MaxLevel"/>, and projected by
        /// <see cref="VillageTierService.MaxTier"/> — which this reads so the chain has exactly one
        /// source. ProgressionReachabilityRegression pins every authored gate against it.</para>
        /// <para>⛔ Not to be confused with <c>RepoProps.MaxStructureLevel</c> (6) — a different
        /// axis; never re-hardcode either.</para></summary>
        public static int MaxLevel => VillageTierService.MaxTier;

        /// <summary>The level a successful upgrade would reach (== <see cref="MaxLevel"/> at max).</summary>
        public static int NextLevel => IsMax ? MaxLevel : Level + 1;

        /// <summary>True once no further Heart Level can be bought.</summary>
        public static bool IsMax => VillageTierService.IsMax;

        /// <summary>Crystal price of the next Heart Level (0 at max). ⛔ Owner rules on balance.
        /// <para>⚠ CORRECTED BY WO-2004: the ladder was the code literal <c>250 * next</c>; it is
        /// now AUTHORED in <c>heart-progression.json</c> (250 / 500 / 750 — the same numbers the
        /// formula produced, so this was a de-hardcoding and not a re-balance) and reached through
        /// <see cref="VillageTierService.NextCost"/>. Do not restate the curve in a comment or a
        /// constant anywhere; read the file.</para></summary>
        public static int NextCost() => VillageTierService.NextCost();

        /// <summary>The player's crystal balance, read from the same wallet the spend charges.</summary>
        public static int Crystals => EconomyService.Instance != null ? EconomyService.Instance.Crystals : 0;

        /// <summary>True when the next Heart Level is affordable right now.</summary>
        public static bool CanAfford => !IsMax && Crystals >= NextCost();

        /// <summary>The one explicit action state of the Heart control (canon §7).
        /// <para>⚠ ORDER IS LOAD-BEARING. A prerequisite is checked BEFORE affordability, because a
        /// player who holds the crystals and is still blocked must not be told to go and get
        /// crystals. Max still wins over everything.</para></summary>
        public static HeartActionState State
        {
            get
            {
                if (IsMax) return HeartActionState.Max;
                if (BlockedReason(NextLevel) != null) return HeartActionState.MissingPrerequisite;
                return CanAfford ? HeartActionState.Ready : HeartActionState.MissingCrystals;
            }
        }

        /// <summary>
        /// The player-facing reason <paramref name="level"/> cannot be bought for reasons OTHER than
        /// crystals — an unmet authored prerequisite, or a data hole. Null when nothing blocks it.
        ///
        /// <para>⛔ THE DATA-HOLE BRANCH IS THE ONE THAT MATTERS. Until 2026-09-07 a level inside the
        /// authored ceiling with no row cost 0, and <c>VillageTierService.TryUpgrade</c> SKIPS THE
        /// SPEND when the cost is 0 — so the missing row was traced as a Fail and then the Heart was
        /// raised FOR FREE. A named failure that still grants the thing is not a refusal, and this is
        /// exactly the "no silent failure" shape §12 exists for. Both this and
        /// <c>HeartProgressionCatalog.HasAuthoredRow</c> must stay, because this one produces the
        /// WORDS and that one is what the writer enforces.</para>
        ///
        /// <para>⚠ Guarded: a broken building catalog must not make the Heart permanently
        /// unraisable. A throw degrades to "nothing blocks it", which is the pre-2026-09-07
        /// behaviour, and the throw itself is logged by <c>Guard</c>.</para>
        /// </summary>
        public static string BlockedReason(int level)
        {
            return DeNelle.Core.Diagnostics.Guard.Try<string>("Heart",
                "prerequisite check for Heart Level " + level,
                () =>
                {
                    if (level <= 0) return null;
                    if (!HeartProgressionCatalog.HasAuthoredRow(level))
                        return "Heart Level " + level + " is not authored in heart-progression.json — "
                             + "it cannot be raised until the data carries it.";

                    var reqs = RequirementsFor(level);
                    for (int i = 0; i < reqs.Count; i++)
                        if (!reqs[i].Satisfied) return "Requires " + reqs[i].Text + ".";
                    return null;
                },
                null);
        }

        /// <summary>
        /// The authored prerequisites for <paramref name="level"/>, resolved against the ladders the
        /// player actually holds. THE UNLOCK-REQUIREMENT READER — the one place authored
        /// <c>requiresBuildings</c> rows become player words and a satisfied/unsatisfied verdict.
        ///
        /// <para>⛔ THE LEVEL COMPARED IS THE BUILDING LADDER, via
        /// <c>ModifierService.TierOf(id)</c> — the same number the Manage BUILD grid upgrades and the
        /// same one owner ruling 21 made the troop gate. It is NOT <c>GameState.BarracksLevel</c> (a
        /// dead field whose only writer was unreachable) and NOT the Heart ladder. Three integer
        /// scales; picking the wrong one is the WO-1423 dead end.</para>
        ///
        /// <para>⚠ Empty on every shipped row today, deliberately — see the file's
        /// <c>_authoringNotes</c>. This method exists so that filling one array is a DATA edit.</para>
        /// </summary>
        public static IReadOnlyList<HeartRequirement> RequirementsFor(int level)
        {
            var authored = HeartProgressionCatalog.RequirementsFor(level);
            if (authored == null || authored.Count == 0)
                return System.Array.Empty<HeartRequirement>();

            var list = new List<HeartRequirement>(authored.Count);
            for (int i = 0; i < authored.Count; i++)
            {
                var r = authored[i];
                if (r == null || string.IsNullOrEmpty(r.Id))
                {
                    DeNelle.Core.Diagnostics.FlowTrace.Fail("Heart",
                        "heart-progression.json level " + level + " authors a requiresBuildings entry with "
                        + "NO id — it can never be satisfied and can never be explained to the player. "
                        + "Skipped; fix the row.");
                    continue;
                }

                int have = ModifierService.TierOf(r.Id);
                string name = DisplayNameForBuilding(r.Id);
                string text = name + " Level " + r.Level
                            + (have >= r.Level ? "" : " (you have Level " + have + ")");
                list.Add(new HeartRequirement(r.Id, r.Level, have, text));
            }
            return list;
        }

        /// <summary>The catalog display name for a building id, falling back to the id itself so a
        /// requirement naming an unknown ladder still reads as something rather than blank.</summary>
        private static string DisplayNameForBuilding(string buildingId)
        {
            var all = BuildingTierCatalog.All;
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    var b = all[i];
                    if (b == null || b.Id != buildingId) continue;
                    return !string.IsNullOrEmpty(b.DisplayName) ? b.DisplayName : b.Id;
                }
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Heart",
                    "heart-progression.json names building id '" + buildingId + "' in a Heart prerequisite "
                    + "but building-tiers.json has no such ladder — the requirement will read as the raw id "
                    + "and ModifierService.TierOf will report 0 forever.");
            }
            return buildingId;
        }

        /// <summary>
        /// Resolve EVERYTHING about one Heart Level in a single traced pass: authored-or-not, cost,
        /// requirements and unlocks. The instrumented seam (§12) — one line names what the level
        /// resolved to, so a preview that comes back empty can be told apart from a level that opens
        /// nothing and from a level that does not exist.
        /// </summary>
        public static HeartLevelBundle ResolveBundle(int level)
        {
            bool authored = HeartProgressionCatalog.HasAuthoredRow(level);
            if (!authored)
            {
                // CostToReach / RequirementsFor already name the hole when it is inside the ceiling;
                // this line names the RESOLVE so the caller's own path is visible in the trace.
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Heart",
                    "bundle resolve for Heart Level " + level + " found NO authored row (ceiling is "
                    + MaxLevel + "). Returning an UNAUTHORED bundle — cost 0, no requirements, no "
                    + "unlocks. That is a data hole, not a free level: the raise is refused.");
                return new HeartLevelBundle(level, false, 0, null, null);
            }

            int cost = HeartProgressionCatalog.CostToReach(level);
            var reqs = RequirementsFor(level);
            var unlocks = UnlocksAt(level);

            int unmet = 0;
            for (int i = 0; i < reqs.Count; i++) if (!reqs[i].Satisfied) unmet++;

            DeNelle.Core.Diagnostics.FlowTrace.Step("Heart",
                "bundle resolve: Heart Level " + level + " costs " + cost + " Crystals, "
                + reqs.Count + " prerequisite(s) (" + unmet + " unmet), " + unlocks.Count
                + " unlock line(s) derived from building-tiers.json / troops.json / "
                + "population-milestones.json.");

            return new HeartLevelBundle(level, true, cost, reqs, unlocks);
        }

        /// <summary>Short realm-progression description (WO-2017 "short realm-progression
        /// description"). Code literal by design: it is not authored in canon-strings.json —
        /// verified 2026-09-06, that file carries no Heart-Level key.</summary>
        public const string Blurb =
            "The Heart is the realm's spine. Raising it opens higher building levels and new research across Elarion.";

        /// <summary>
        /// The one player-facing sentence for the current state. Model-owned so the panel binds
        /// text it did not compose (canon §9).
        /// </summary>
        public static string StateSentence()
        {
            switch (State)
            {
                case HeartActionState.Max:
                    return "The Heart is fully raised. Nothing further is gated on it.";
                case HeartActionState.MissingPrerequisite:
                    // BlockedReason already composed the words, including WHICH ladder and where it
                    // stands. Re-deriving them here would be a second author for one sentence.
                    return BlockedReason(NextLevel)
                           ?? "The Heart cannot be raised yet.";
                case HeartActionState.MissingCrystals:
                    return "Need " + DeNelle.Core.UI.ElarionUi.CompactNumber(NextCost())
                         + " Crystals to raise the Heart (you have "
                         + DeNelle.Core.UI.ElarionUi.CompactNumber(Crystals) + ").";
                default:
                    return "Ready to raise the Heart to Level " + NextLevel + ".";
            }
        }

        /// <summary>The CTA face for the current state ("" at max — no CTA is drawn).</summary>
        public static string CtaLabel() => IsMax ? "" : "RAISE HEART TO LEVEL " + NextLevel;

        /// <summary>Costs of the next level as the shared cost-chip parts (empty at max).</summary>
        public static IReadOnlyList<DeNelle.Core.UI.CostPart> NextCostParts()
        {
            if (IsMax) return System.Array.Empty<DeNelle.Core.UI.CostPart>();
            return DeNelle.Core.UI.CostFormat.Parts(new[] { ("crystal", "Crystals", NextCost()) });
        }

        /// <summary>
        /// What reaching <paramref name="level"/> opens, DERIVED from building-tiers.json.
        ///
        /// <para>A ladder rung whose authored <c>requiresVillageTier</c> equals
        /// <paramref name="level"/> becomes upgradable at that level (the gate the live code reads
        /// is <c>BuildingUpgradeService.cs:53-59</c>), and every research perk sitting on that same
        /// row becomes buyable with it (<c>BuildingTierCatalog.PerkRequiredVillageTier</c> — the
        /// perk gate is its OWN row's field, NOT the row's tier number; conflating the two was the
        /// WO-1423 dead end).</para>
        ///
        /// <para>⚠ WO-2004 (2026-09-06) EXTENDED THIS, AND THE WAY IT EXTENDED IT IS THE POINT.
        /// The 2026-09-06 note below was RIGHT that nothing authors a Heart gate DIRECTLY on a
        /// troop — and it stayed right; no such field was added. What was added is TRANSITIVE
        /// DERIVATION. The Heart opens a Barracks rung (<c>requiresVillageTier</c>), and
        /// troops.json already authors <c>unlockBarracksTier</c> against that same rung (owner
        /// ruling 21: the barracks BUILDING tier gates troops). Composing the two hops reports
        /// "Outrider" at the Heart Level that opens Barracks Level 4 with NO second table — which
        /// is exactly what WO-2004's "no duplicated Heart-level unlock tables / one authoritative
        /// progression table" requires. Authoring a troop list into heart-progression.json would
        /// have been the failure, not the feature. Population cap and Echo workforce slots ride
        /// the same principle (PopulationService.CapAtVillageTier /
        /// population-milestones.json <c>villageLevel</c>).</para>
        ///
        /// <para>⚠ MEASURED 2026-09-06 — WHAT THE DATA STILL DOES NOT CARRY, verified again by
        /// WO-2004. Across all of building-tiers.json the ONLY authored village gates are building
        /// tier rows and their perks (arcane-tower, armorer, barracks, forge, lumbermill, farm —
        /// tiers 2/3/4+, gates 1/2/3). structures-catalog.json authors NO Heart gate at all
        /// (grepped for requires/unlock/minTier keys: zero hits), so no DEFENSIVE STRUCTURE is
        /// Heart-gated. There is NO buildable-reach or influence-radius system anywhere under
        /// Assets/_Modules/Village/BuildMode (zero hits), so canon §6's reach unlock and owner
        /// ruling 12's "value must be data-driven" describe a feature that does not exist yet —
        /// a NEW FEATURE for the owner, not a missing number. No reward/message grant is wired to
        /// a Heart level either. All four stay ABSENT from this preview rather than invented, and
        /// each is recorded in the WO-2004 gate audit. This method reports them the moment the
        /// data or the system carries them.</para>
        ///
        /// <para>⚠ ⛔ THIS PREVIEW SHOWS WHAT OPENS, NEVER WHAT IT COSTS. If a caller ever wants a
        /// cost on one of these lines it must read <c>BuildingTierChargeLane</c>, NOT the row's
        /// authored currency key: <c>BuildingUpgradeService.TierCost</c> picks the lane by tier
        /// INDEX (T1 Wood, T2 Stone, T3+ Iron) and ignores what the JSON says, so every tier-2 row
        /// in the game is charged Stone regardless of its authoring (owner ruling 22 / 24).</para>
        /// </summary>
        public static IReadOnlyList<HeartUnlock> UnlocksAt(int level)
        {
            var list = new List<HeartUnlock>(16);
            if (level <= 0) return list;

            // Each source is Guarded independently: one broken catalog must degrade the preview by
            // its own lines, never blank the whole list (§12 — no silent failure, no total failure).
            DeNelle.Core.Diagnostics.Guard.Try("Heart", "unlock preview: building rungs, perks and troops",
                () => AppendBuildingAndTroopUnlocks(level, list));
            DeNelle.Core.Diagnostics.Guard.Try("Heart", "unlock preview: population cap",
                () => AppendPopulationCapUnlock(level, list));
            DeNelle.Core.Diagnostics.Guard.Try("Heart", "unlock preview: echo workforce slots",
                () => AppendEchoSlotUnlocks(level, list));
            return list;
        }

        /// <summary>Building rungs gated on <paramref name="level"/>, the research perks on those
        /// rungs, and — for the Barracks specifically — the troops those rungs make reachable.</summary>
        private static void AppendBuildingAndTroopUnlocks(int level, List<HeartUnlock> list)
        {
            var all = BuildingTierCatalog.All;
            if (all == null)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Heart",
                    "BuildingTierCatalog.All is NULL — the Heart Level " + level
                    + " preview can report no building rungs, perks or troops.");
                return;
            }

            for (int i = 0; i < all.Count; i++)
            {
                var b = all[i];
                if (b == null || b.Tiers == null) continue;
                string name = !string.IsNullOrEmpty(b.DisplayName) ? b.DisplayName : b.Id;
                for (int j = 0; j < b.Tiers.Count; j++)
                {
                    var t = b.Tiers[j];
                    if (t == null || t.RequiresVillageTier != level) continue;

                    list.Add(new HeartUnlock(HeartUnlockKind.BuildingLevel,
                        name + " Level " + t.Tier, b.Id));

                    AppendTroopsForBuildingRung(b.Id, t.Tier, name, list);

                    if (t.Perks == null) continue;
                    for (int k = 0; k < t.Perks.Count; k++)
                    {
                        var p = t.Perks[k];
                        if (p == null) continue;
                        string perkName = !string.IsNullOrEmpty(p.Name) ? p.Name : p.Id;
                        list.Add(new HeartUnlock(HeartUnlockKind.Research,
                            perkName + " (research)", b.Id));
                    }
                }
            }
        }

        /// <summary>
        /// The troops that a newly-opened BARRACKS rung makes reachable — the second hop of the
        /// derivation described on <see cref="UnlocksAt"/>.
        ///
        /// <para>⛔ THE ID IS <c>BarracksBuildingId</c> AND THE GATE IS THE BUILDING TIER, per owner
        /// ruling 21 (2026-09-06, verbatim: "Merge them - the building tier gates troops"). The
        /// retired <c>GameState.BarracksLevel</c> field is NOT consulted here and must not be: it
        /// sat at its founding value of 1 forever because its only writer was unreachable, and
        /// ANDing it against the building tier is what left seven of nine troops unreachable.</para>
        ///
        /// <para>⚠ WORDED AS A PATH, NOT A GRANT. Reaching this Heart Level does not hand the
        /// player the troop — it makes the Barracks rung UPGRADABLE, and the troop follows when
        /// that upgrade is bought. The line says so ("via Barracks Level 4") so the preview never
        /// promises something the level alone does not deliver.</para>
        /// </summary>
        private static void AppendTroopsForBuildingRung(string buildingId, int tier, string buildingName,
                                                        List<HeartUnlock> list)
        {
            if (buildingId != BarracksBuildingId) return;

            var troops = TroopCatalog.All;
            if (troops == null)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Heart",
                    "TroopCatalog.All is NULL — Barracks Level " + tier
                    + " opens at this Heart Level but no troop line can be reported for it.");
                return;
            }

            for (int i = 0; i < troops.Count; i++)
            {
                var d = troops[i];
                if (d == null || d.UnlockBarracksTier != tier) continue;
                string troopName = !string.IsNullOrEmpty(d.DisplayName) ? d.DisplayName : d.Id;
                list.Add(new HeartUnlock(HeartUnlockKind.Troop,
                    troopName + " (via " + buildingName + " Level " + tier + ")", buildingId));
            }
        }

        /// <summary>
        /// The population ceiling this level raises, reported only when it actually rises.
        /// Reads <see cref="DeNelle.Village.Population.PopulationService.CapAtVillageTier"/> — the
        /// same ladder <c>PopulationCap</c> serves — rather than re-listing the numbers here.
        /// </summary>
        private static void AppendPopulationCapUnlock(int level, List<HeartUnlock> list)
        {
            int before = DeNelle.Village.Population.PopulationService.CapAtVillageTier(level - 1);
            int after = DeNelle.Village.Population.PopulationService.CapAtVillageTier(level);
            if (after <= before) return;
            list.Add(new HeartUnlock(HeartUnlockKind.PopulationCap,
                "Population cap " + before + " -> " + after, null));
        }

        /// <summary>
        /// Echo workforce slots whose Heart-Level condition lands on <paramref name="level"/>.
        ///
        /// <para>⚠ A COMPOUND GATE, AND THE COPY SAYS SO. A milestone's <c>villageLevel</c> sits
        /// inside an ALL block that may also demand quests / outposts / waves / XP
        /// (population-milestones.json: slot 4 = villageLevel 2 AND 35 quests). Reaching the Heart
        /// Level is therefore NECESSARY, not sufficient, and the line is suffixed accordingly.
        /// Claiming the slot outright would be the preview lying about a gate — the exact species
        /// of defect this program keeps finding.</para>
        /// <para>Only the ALL block is read: a <c>villageLevel</c> inside an ANY block would make
        /// the Heart one of several ALTERNATIVE routes, which is not an unlock caused by this level
        /// and must not be advertised as one. Measured 2026-09-06: no milestone authors
        /// villageLevel in an ANY block, so this branch is a guard against future authoring, not a
        /// live case.</para>
        /// </summary>
        private static void AppendEchoSlotUnlocks(int level, List<HeartUnlock> list)
        {
            var milestones = DeNelle.Village.Population.PopulationMilestonesCatalog.Milestones;
            if (milestones == null) return;

            for (int i = 0; i < milestones.Count; i++)
            {
                var m = milestones[i];
                if (m == null || m.All == null || m.All.VillageLevel != level) continue;

                bool alsoNeedsMore =
                    m.All.Xp > 0 || m.All.QuestsCompleted > 0 || m.All.OutpostsCleared > 0 ||
                    m.All.WavesCleared > 0 || (m.Any != null && !m.Any.IsEmpty);

                list.Add(new HeartUnlock(HeartUnlockKind.EchoSlot,
                    "Echo workforce slot " + m.EchoSlot
                    + (alsoNeedsMore ? " (also needs other milestones)" : ""), null));
            }
        }

        /// <summary>The catalog id of the Barracks ladder. One place, so the troop derivation and
        /// any future reader cannot drift onto a different spelling.</summary>
        private const string BarracksBuildingId = "barracks";

        /// <summary>
        /// Raise the Heart by one level. Routes through <see cref="VillageTierService.TryUpgrade"/>,
        /// which stays the SOLE writer of the stored field — this method adds no second write path.
        /// <paramref name="status"/> always carries a player-facing sentence, on success and on
        /// every refusal (§12: no silent no-op).
        /// </summary>
        public static bool TryRaise(out string status)
        {
            if (IsMax)
            {
                status = "The Heart is already at its highest level.";
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Heart",
                    "raise REFUSED: already at max Heart Level " + Level + ".");
                return false;
            }

            // Prerequisites (and the unauthored-row data hole) are checked BEFORE crystals, so a
            // player holding the price is never told to go and find crystals they already have.
            // ⚠ This is the SECOND of two checks by design: VillageTierService.TryUpgrade enforces the
            // same rule at the SOLE WRITER, which is what covers the other door into the raise
            // (BuildingUpgradeVM.Select -> VillageTierService.TryUpgrade, BuildingUpgradeVM.cs:1045).
            // This one exists to produce the player SENTENCE; that one exists to make it true.
            string blocked = BlockedReason(NextLevel);
            if (blocked != null)
            {
                status = blocked;
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Heart",
                    "raise REFUSED for Heart Level " + NextLevel + ": " + blocked);
                return false;
            }

            int cost = NextCost();
            int have = Crystals;
            if (have < cost)
            {
                status = StateSentence();
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Heart",
                    "raise REFUSED: crystals " + have + " < cost " + cost + " for Heart Level " + NextLevel + ".");
                return false;
            }

            if (!VillageTierService.TryUpgrade())
            {
                status = "Could not raise the Heart right now.";
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Heart",
                    "VillageTierService.TryUpgrade returned FALSE with crystals " + have
                    + " >= cost " + cost + " — the spend or the state was refused downstream.");
                return false;
            }

            int now = Level;
            status = "Heart raised to Level " + now + ".";
            DeNelle.Core.Diagnostics.FlowTrace.Step("Heart",
                "Heart raised to Level " + now + " for " + cost + " Crystals; "
                + UnlocksAt(now).Count + " authored rows open at this level.");
            return true;
        }
    }
}
