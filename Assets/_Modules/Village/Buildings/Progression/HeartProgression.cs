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
        /// here; the ceiling lives on <see cref="VillageTierService.MaxTier"/> and
        /// ProgressionReachabilityRegression pins every authored gate against it.</summary>
        public static int MaxLevel => VillageTierService.MaxTier;

        /// <summary>The level a successful upgrade would reach (== <see cref="MaxLevel"/> at max).</summary>
        public static int NextLevel => IsMax ? MaxLevel : Level + 1;

        /// <summary>True once no further Heart Level can be bought.</summary>
        public static bool IsMax => VillageTierService.IsMax;

        /// <summary>Crystal price of the next Heart Level (0 at max). ⛔ Owner rules on balance —
        /// the ladder is <c>250 * next</c> and lives on VillageTierService.NextCost.</summary>
        public static int NextCost() => VillageTierService.NextCost();

        /// <summary>The player's crystal balance, read from the same wallet the spend charges.</summary>
        public static int Crystals => EconomyService.Instance != null ? EconomyService.Instance.Crystals : 0;

        /// <summary>True when the next Heart Level is affordable right now.</summary>
        public static bool CanAfford => !IsMax && Crystals >= NextCost();

        /// <summary>The one explicit action state of the Heart control (canon §7).</summary>
        public static HeartActionState State =>
            IsMax ? HeartActionState.Max
                  : CanAfford ? HeartActionState.Ready
                              : HeartActionState.MissingCrystals;

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
        /// <para>⚠ MEASURED 2026-09-06 — WHAT THE DATA DOES NOT CARRY. Across all of
        /// building-tiers.json the ONLY authored village gates are building tier rows and their
        /// perks (arcane-tower, armorer, barracks, forge, lumbermill, farm — tiers 2/3/4+, gates
        /// 1/2/3). NOTHING in the tree authors a Heart gate on a troop type, a defensive structure,
        /// a research school or a buildable reach/radius. Canon §6 says a Heart upgrade MAY unlock
        /// those; the content simply does not yet. They are therefore ABSENT from this preview
        /// rather than invented here — that is WO-2004's authoring job, and this method will report
        /// them with no code change once the data carries them.</para>
        /// </summary>
        public static IReadOnlyList<HeartUnlock> UnlocksAt(int level)
        {
            var list = new List<HeartUnlock>(12);
            if (level <= 0) return list;

            var all = BuildingTierCatalog.All;
            if (all == null) return list;

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
            return list;
        }

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
