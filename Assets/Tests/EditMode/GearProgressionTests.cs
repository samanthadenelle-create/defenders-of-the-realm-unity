// =============================================================================
// GearProgressionTests — WO-808 Option A oracles (headless EditMode, no scene).
// Pins: level clamp, cost monotonicity, resolver math (level 1 == authored
// baseline exactly), ApplyImprove write behaviour, and the defense safety clamp.
// =============================================================================

using NUnit.Framework;
using DeNelle.Core.State;
using DeNelle.Village;

public class GearProgressionTests
{
    // ── Level state ──────────────────────────────────────────────────────────

    [Test]
    public void GearLevelOf_NullState_IsBaseline()
    {
        Assert.AreEqual(1, GearProgression.GearLevelOf(null, "weapon_x"));
    }

    [Test]
    public void GearLevelOf_AbsentId_IsBaseline()
    {
        var s = new GameState();
        Assert.AreEqual(1, GearProgression.GearLevelOf(s, "weapon_x"));
    }

    [Test]
    public void ApplyImprove_BumpsOneLevel_AndClampsAtMax()
    {
        var s = new GameState();
        int max = GearProgression.MaxLevelFor("common");
        Assert.GreaterOrEqual(max, 2, "common band must author at least 2 levels");

        for (int i = 0; i < max + 3; i++)
            GearProgression.ApplyImprove(s, "weapon_x", "common");

        Assert.AreEqual(max, GearProgression.GearLevelOf(s, "weapon_x"),
            "level must clamp at the band max no matter how many applies run");
    }

    [Test]
    public void HasNextLevel_FalseAtMax()
    {
        int max = GearProgression.MaxLevelFor("rare");
        Assert.IsTrue(GearProgression.HasNextLevel("rare", 1));
        Assert.IsFalse(GearProgression.HasNextLevel("rare", max));
    }

    // ── Costs ────────────────────────────────────────────────────────────────

    [Test]
    public void ImproveCost_Level1_IsFree_AndMonotonicAfter()
    {
        foreach (var rarity in new[] { "common", "uncommon", "rare", "epic", "legendary" })
        {
            var first = GearProgression.ImproveCost(rarity, 1);
            Assert.AreEqual(0, first.Wood + first.Iron, rarity + " L1 must be free (owned baseline)");

            int max = GearProgression.MaxLevelFor(rarity);
            int prev = 0;
            for (int lvl = 2; lvl <= max; lvl++)
            {
                var c = GearProgression.ImproveCost(rarity, lvl);
                int total = c.Wood + c.Iron;
                Assert.Greater(total, prev, rarity + " cost must strictly increase per level (L" + lvl + ")");
                prev = total;
            }
        }
    }

    // ── Resolver ─────────────────────────────────────────────────────────────

    [Test]
    public void Resolver_Level1_EqualsAuthoredBaseline()
    {
        var w = new WeaponDef { id = "t_w", rarity = "common", damageMult = 1.4f };
        var a = new ArmorDef { id = "t_a", rarity = "common", defense = 0.2f };
        Assert.AreEqual(1.4f, GearStatResolver.EffectiveDamageMult(w, 1), 1e-4f);
        Assert.AreEqual(0.2f, GearStatResolver.EffectiveDefense(a, 1), 1e-4f);
    }

    [Test]
    public void Resolver_HigherLevel_IsStrictlyStronger()
    {
        var w = new WeaponDef { id = "t_w", rarity = "epic", damageMult = 1.4f };
        var a = new ArmorDef { id = "t_a", rarity = "epic", defense = 0.2f };
        int max = GearProgression.MaxLevelFor("epic");
        for (int lvl = 2; lvl <= max; lvl++)
        {
            Assert.Greater(GearStatResolver.EffectiveDamageMult(w, lvl),
                           GearStatResolver.EffectiveDamageMult(w, lvl - 1),
                           "damage must climb per level");
            Assert.Greater(GearStatResolver.EffectiveDefense(a, lvl),
                           GearStatResolver.EffectiveDefense(a, lvl - 1),
                           "defense must climb per level");
        }
    }

    [Test]
    public void Resolver_Defense_NeverExceedsSafetyClamp()
    {
        // A pathological 0.9-defense legendary at max level must stay inside 0..0.9 —
        // the same window ApplyStats always enforced (never approach immunity).
        var a = new ArmorDef { id = "t_a", rarity = "legendary", defense = 0.9f };
        int max = GearProgression.MaxLevelFor("legendary");
        Assert.LessOrEqual(GearStatResolver.EffectiveDefense(a, max), 0.9f + 1e-4f);
    }

    [Test]
    public void Resolver_UnknownRarity_IsBaselineAtAnyLevel()
    {
        var w = new WeaponDef { id = "t_w", rarity = "mythic-unauthored", damageMult = 1.4f };
        Assert.AreEqual(1.4f, GearStatResolver.EffectiveDamageMult(w, 5), 1e-4f,
            "an unauthored band must never scale (baseline at every level)");
    }

    // ── Save round-trip shape ────────────────────────────────────────────────

    [Test]
    public void GearLevels_SurvivesDictionaryCopy_RoundTrip()
    {
        // The persistence path copies the dict in/out (GameStateService dehydrate/hydrate);
        // pin the shape so a rename/retype breaks loudly here.
        var s = new GameState();
        GearProgression.ApplyImprove(s, "weapon_x", "common");
        GearProgression.ApplyImprove(s, "weapon_x", "common");
        var copy = new System.Collections.Generic.Dictionary<string, int>(s.GearLevels);
        var restored = new GameState { GearLevels = copy };
        Assert.AreEqual(GearProgression.GearLevelOf(s, "weapon_x"),
                        GearProgression.GearLevelOf(restored, "weapon_x"));
    }
}
