// =============================================================================
// EchoRosterCatalog -- the lightweight, data-driven Echo roster (owner felt-test
// 2026-07-17: "Echoes are portrait-card spirits, NOT 3D models... just let them
// live in the pet roster. When we unlock a new echo, they should start a dialogue").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY A CODE TABLE (not a Data/Canonical JSON): the roster is a FIXED, canonical
// 6-entry set with authored ASCII flavor -- there is nothing owner-tunable at
// runtime and no per-map variance. A static table is the LIGHTEST possible source
// (no JSON loader, no StreamingAssets read, no dual-copy/md5 dance, compile-time
// safe) and is still fully DATA-DRIVEN in the sense the owner asked for: the
// unlock dialogue + roster grid read WHICHEVER entry maps to the unlocked echo and
// fill portrait / name / element / flavor from it -- no per-echo hardcoded card.
// If the roster ever needs to grow owner-tunable, promote this to echoes.json under
// Data/Canonical then (dual-copy + md5) -- not before (LIGHTWEIGHT mandate).
//
// ORDER = the echo COUNT it corresponds to: echo #1 (Frosthowl, the starter, owned
// from EchoCount==1) .. echo #6 (Ember Phoenix). EchoService.EchoCount 1..MaxEchoes
// indexes straight into this (order == count). A wave-unlock that raises the count
// to N fires the dialogue for ByCount(N) = the newly earned spirit.
//
// Portraits: Assets/Resources/Echoes/Portraits/<PortraitName>.(png|jpg). Loaded as
// Texture2D + Sprite.Create at runtime (owner: "no importer settings needed; guard
// null") -- see LoadPortrait, Guard-wrapped so a missing image logs + skips.
// =============================================================================
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>The six canonical Echo elements (WO-738 identity axis). Distinct from the
    /// human-readable <see cref="EchoRosterEntry.Element"/> subtitle string, which stays for display.</summary>
    public enum ElementType
    {
        Nature,
        Shadow,
        Storm,
        Earth,
        Fire,
        Frost,
    }

    /// <summary>The functional Echo lanes (WO-738). Idle == unassigned. These are the AGENCY picks;
    /// the string lane vocabulary in <see cref="EchoAssignments"/> mirrors these tokens.</summary>
    public enum LaneType
    {
        Idle,
        Harvest,
        Crafting,
        Defense,
        Exploration,
    }

    /// <summary>One canonical Echo spirit's card identity (immutable data row).</summary>
    public sealed class EchoRosterEntry
    {
        /// <summary>Stable id ("echo-frosthowl").</summary>
        public string Id;
        /// <summary>1-based order == the EchoCount this spirit corresponds to.</summary>
        public int Order;
        /// <summary>Card name, e.g. "Frosthowl (Ice Echo)".</summary>
        public string DisplayName;
        /// <summary>Element subtitle shown under the portrait, e.g. "Ice Elemental".</summary>
        public string Element;
        /// <summary>Portrait file base name under Resources/Echoes/Portraits/.</summary>
        public string PortraitName;
        /// <summary>The awakening flavor line (mockup tone, ASCII).</summary>
        public string Flavor;
        /// <summary>Extended lore revealed by the dialogue's "Tell me more" button (ASCII).</summary>
        public string Lore;

        // ── WO-738 specialization identity (derived, non-tunable -- element identity,
        //    NOT a balance knob; the tunable numbers live in echoes-balance.json). ──
        /// <summary>This spirit's element (WO-738 identity axis).</summary>
        public ElementType ElementType;
        /// <summary>The lane this spirit is best at -- a match here earns the affinity bonus.</summary>
        public LaneType PreferredLane;
        /// <summary>For a Harvest-preferred spirit, the real resource it favors (the DumpSilos split
        /// weight). Null for a non-Harvest spirit (n/a). Maps to a real GameState wallet field.</summary>
        public ResourceType? HarvestResource;
    }

    /// <summary>
    /// The fixed 6-spirit Echo roster (order == echo count). Read by the unlock
    /// dialogue (<see cref="EchoUnlockDialogue"/>) and the roster grid
    /// (<see cref="EchoRosterView"/>). Portrait sprites are created on demand.
    /// </summary>
    public static class EchoRosterCatalog
    {
        // ASCII-only flavor + lore (colorblind owner reads TEXT, not hue; glyph-safe TMP).
        private static readonly EchoRosterEntry[] s_all =
        {
            new EchoRosterEntry {
                Id = "echo-frosthowl", Order = 1,
                DisplayName = "Frosthowl (Ice Echo)", Element = "Ice Elemental",
                PortraitName = "Frosthowl",
                Flavor = "The ancient spirit awakens, its icy breath whispering secrets of the frozen wastes...",
                Lore = "Frosthowl prowled the glacier reaches long before Elarion had a name. Bound to your cause, the cold works FOR you -- every harvest hastened by winter's patience.",
                ElementType = ElementType.Frost, PreferredLane = LaneType.Exploration, HarvestResource = null,
            },
            new EchoRosterEntry {
                Id = "echo-verdant-stag", Order = 2,
                DisplayName = "Verdant Stag (Nature Echo)", Element = "Verdant Elemental",
                PortraitName = "VerdantStag",
                Flavor = "Antlers of living wood break the loam, and the green spirit lifts its head to your call...",
                Lore = "The Verdant Stag remembers every seed the forest ever sowed. Where it walks the land gives freely -- growth answering to your command.",
                ElementType = ElementType.Nature, PreferredLane = LaneType.Harvest, HarvestResource = ResourceType.Wood,
            },
            new EchoRosterEntry {
                Id = "echo-voidwing-raven", Order = 3,
                DisplayName = "Voidwing Raven (Void Echo)", Element = "Void Elemental",
                PortraitName = "VoidwingRaven",
                Flavor = "A shadow with wings unfurls from nothing, its hollow eyes fixed upon your intent...",
                Lore = "The Voidwing Raven slipped between worlds when the first star guttered out. It gathers what others cannot reach, carrying spoils across the dark.",
                ElementType = ElementType.Shadow, PreferredLane = LaneType.Exploration, HarvestResource = null,
            },
            new EchoRosterEntry {
                Id = "echo-stormcoil-serpent", Order = 4,
                DisplayName = "Stormcoil Serpent (Storm Echo)", Element = "Storm Elemental",
                PortraitName = "StormcoilSerpent",
                Flavor = "Thunder coils and tightens, and a serpent of lightning tastes the charged air...",
                Lore = "The Stormcoil Serpent was born of a sky that would not stop raging. Its restless energy drives the whole workforce faster than any whip could.",
                ElementType = ElementType.Storm, PreferredLane = LaneType.Defense, HarvestResource = null,
            },
            new EchoRosterEntry {
                Id = "echo-stonewarden-bear", Order = 5,
                DisplayName = "Stonewarden Bear (Earth Echo)", Element = "Stone Elemental",
                PortraitName = "StonewardenBear",
                Flavor = "The mountain shifts, stands, and shakes the dust of ages from its granite shoulders...",
                Lore = "The Stonewarden Bear slept beneath the roots of the world. Tireless and unbreakable, it hauls the heaviest loads without complaint.",
                // Owner-final map says "Stone", but Stone is RETIRED (DEF-121) and NOT in ResourceType
                // {Iron,Wood,Food,AetherCrystal}; the WO's reconciled table maps this Earth spirit to
                // Iron ("hauls the heaviest loads" = ore). Real-resource-only, no invented type.
                ElementType = ElementType.Earth, PreferredLane = LaneType.Harvest, HarvestResource = ResourceType.Iron,
            },
            new EchoRosterEntry {
                Id = "echo-ember-phoenix", Order = 6,
                DisplayName = "Ember Phoenix (Fire Echo)", Element = "Ember Elemental",
                PortraitName = "EmberPhoenix",
                Flavor = "From a single spark the firebird rises, wings scattering embers like falling stars...",
                Lore = "The Ember Phoenix has burned and risen a thousand times. Its fervor sets the entire workforce alight -- fastest when the work is hardest.",
                ElementType = ElementType.Fire, PreferredLane = LaneType.Crafting, HarvestResource = null,
            },
        };

        /// <summary>The full roster in order (never null; length 6).</summary>
        public static EchoRosterEntry[] All => s_all;

        /// <summary>Total spirits in the canonical roster (6).</summary>
        public static int Count => s_all.Length;

        /// <summary>
        /// The entry for a given owned COUNT / order (1-based). Clamped: a count above
        /// the roster returns the last spirit, at/below 0 returns the first, so the
        /// unlock dialogue always has something to show (never null).
        /// </summary>
        public static EchoRosterEntry ByCount(int count)
        {
            if (s_all.Length == 0) return null;
            int idx = Mathf.Clamp(count - 1, 0, s_all.Length - 1);
            return s_all[idx];
        }

        /// <summary>The entry at 0-based index, or null if out of range.</summary>
        public static EchoRosterEntry ByIndex(int index)
        {
            if (index < 0 || index >= s_all.Length) return null;
            return s_all[index];
        }

        // -- portrait loader (Texture2D -> Sprite.Create, Guard-wrapped) -----------
        // Cache so re-opening the roster grid doesn't re-create six sprites each time.
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> s_spriteCache =
            new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>
        /// Load a spirit's portrait as a runtime <see cref="Sprite"/> (Resources
        /// Texture2D + Sprite.Create). Returns null (logged, never throws) when the
        /// image is absent -- callers show a text fallback so the card is never blank.
        /// </summary>
        public static Sprite LoadPortrait(string portraitName)
        {
            if (string.IsNullOrEmpty(portraitName)) return null;
            if (s_spriteCache.TryGetValue(portraitName, out var cached) && cached != null)
                return cached;

            var sprite = Guard.Try("Echo", "load echo portrait " + portraitName, () =>
            {
                var tex = Resources.Load<Texture2D>("Echoes/Portraits/" + portraitName);
                if (tex == null)
                {
                    FlowTrace.Warn("Echo", $"LoadPortrait: Resources/Echoes/Portraits/{portraitName} missing -- card shows a text fallback.");
                    return (Sprite)null;
                }
                return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                     new Vector2(0.5f, 0.5f), 100f);
            }, fallback: null);

            if (sprite != null) s_spriteCache[portraitName] = sprite;
            return sprite;
        }
    }
}
