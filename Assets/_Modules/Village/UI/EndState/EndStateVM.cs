// =============================================================================
// EndStateVM — the ONE view-model behind every end-state screen (WO-B, UI
// conformance audit 2026-07-02 §3.2): battle victory, battle defeat, hero death
// (non-hub), and wave-clear results all construct THIS and render through
// EndStateView. Pure data + adapter factories; the View binds it and never
// reads game state (MVVM strict, presentation-does-no-service).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// Icon resolution happens HERE (in the factories, the model side of the seam),
// sprite-first with null fallback per the RpgUiCatalog / ItemIconCatalog
// contract — a null Icon renders as a label-only slot plate, never blanks.
// The outcome EMBLEM deliberately does NOT reuse the fringed AI crown asset
// (Resources/RpgUi/crown/*): it uses the committed bronze RPG icon pack —
// icon_combat (crossed sword+axe) for wins, icon_shield for defeats.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.UI;

namespace DeNelle.Village.UI
{
    /// <summary>Which end-state this screen represents (drives copy + trace tag).</summary>
    public enum EndStateKind
    {
        Victory,
        Defeat,
        HeroDeath,
        WaveResults,
    }

    /// <summary>One spoils row: icon (null = label-only plate) + label + amount.</summary>
    public sealed class SpoilRowVM
    {
        public Sprite Icon;
        public string Label;
        public string Amount;
        /// <summary>Rarity index for the kit slot plate frame (1 = common).</summary>
        public int Rarity = 1;
    }

    /// <summary>
    /// The end-state screen model. Construct via the static factories (they adapt
    /// each outcome's data + resolve icons); EndStateView renders it.
    /// </summary>
    public sealed class EndStateVM
    {
        public EndStateKind Kind;
        public string Title;
        public string Subtitle;

        /// <summary>Battle duration in seconds; &lt; 0 hides the time row.</summary>
        public float TimeSeconds = -1f;
        /// <summary>Earned stars 0..3; &lt; 0 hides the rating row.</summary>
        public int Stars = -1;
        /// <summary>Flawless win (perfect-tier); no signal is threaded yet — defaults false.</summary>
        public bool Perfect;

        /// <summary>Outcome emblem for the medallion socket (bronze icon pack, never the crown).</summary>
        public Sprite Emblem;

        public readonly List<SpoilRowVM> Spoils = new List<SpoilRowVM>();

        /// <summary>The ONE primary action (owner button law: an end-state has exactly one way out).</summary>
        public string PrimaryLabel = "Continue";
        /// <summary>Route tag for the FlowTrace line (e.g. "return-home", "respawn").</summary>
        public string PrimaryRoute = "close";
        /// <summary>Invoked exactly once (button tap OR auto-dismiss), then the view tears down.</summary>
        public Action Primary;

        /// <summary>&gt; 0 = fire Primary automatically after this many real seconds (softlock guard).</summary>
        public float AutoDismissSeconds;

        /// <summary>Compact banner mode (wave-clear): small top panel, no scrim/backdrop, non-blocking.</summary>
        public bool Compact;

        // ── Factories (the adapters — one per outcome) ────────────────────────

        /// <summary>
        /// Battle-arena WIN summary (replaces BattleArenaHud.ShowVictorySummary).
        /// <paramref name="onContinue"/> = the deferred home-return; the view latches it once.
        /// </summary>
        public static EndStateVM FromBattleVictory(int stars, float durationSeconds,
            int xp, int wisdom, int wood, int iron, string gearName,
            Action onContinue, float autoTimeoutSeconds = 20f, bool perfect = false)
        {
            var vm = new EndStateVM
            {
                Kind = EndStateKind.Victory,
                Title = "Victory!",
                Subtitle = perfect ? "Flawless! The realm is safer because of you!"
                                   : "The realm is safer because of you!",
                Stars = Mathf.Clamp(stars, 0, 3),
                Perfect = perfect,
                TimeSeconds = Mathf.Max(0f, durationSeconds),
                Emblem = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconCombat),
                PrimaryLabel = "Continue",
                PrimaryRoute = "return-home",
                Primary = onContinue,
                AutoDismissSeconds = Mathf.Max(1f, autoTimeoutSeconds),
            };

            if (xp > 0)
                vm.Spoils.Add(new SpoilRowVM
                {
                    Icon = RpgUiCatalog.Get(RpgUiCatalog.RoleBadge, RpgUiCatalog.BadgeLevel),
                    Label = "Experience", Amount = "+" + xp,
                });
            if (wisdom > 0)
                vm.Spoils.Add(new SpoilRowVM
                {
                    Icon = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconTree),
                    Label = "Wisdom", Amount = "+" + wisdom,
                });
            if (wood > 0)
                vm.Spoils.Add(new SpoilRowVM
                {
                    // Item-catalog first (sprite-first, null-safe); no wood sheet art today
                    // -> renders as a label-only plate until the art lands.
                    Icon = ItemIconCatalog.ForConsumable("mat_wood", "Wood"),
                    Label = "Wood", Amount = "+" + wood,
                });
            if (iron > 0)
                vm.Spoils.Add(new SpoilRowVM
                {
                    Icon = ItemIconCatalog.ForConsumable("iron_ore_ingot", "Iron Ore"),
                    Label = "Iron", Amount = "+" + iron,
                });
            if (!string.IsNullOrEmpty(gearName))
                vm.Spoils.Add(new SpoilRowVM
                {
                    // Gear drops arrive as a DISPLAY NAME only (BattleArena.TryGrantArenaGear)
                    // — no id to resolve through GearCatalog/ItemIconCatalog, so the bronze
                    // sword icon stands in for "a piece of gear".
                    Icon = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconSword),
                    Label = gearName, Amount = "Equipped",
                    Rarity = 2,
                });
            return vm;
        }

        /// <summary>Battle-arena LOSS sting (replaces BattleArenaHud.ShowLossPanel). The
        /// controller returns the hero home immediately, so this auto-dismisses fast.</summary>
        public static EndStateVM FromBattleDefeat()
        {
            return new EndStateVM
            {
                Kind = EndStateKind.Defeat,
                Title = "Defeat",
                Subtitle = "Fall back and regroup, hero.",
                Emblem = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconShield),
                PrimaryLabel = "Continue",
                PrimaryRoute = "close",
                AutoDismissSeconds = 2.5f,
            };
        }

        /// <summary>
        /// HERO DEATH in a NON-hub scene (closes the audit MISSING: HeroHealth silently
        /// respawns/evacuates outside hubs with no defeat sting). The respawn/evac is
        /// already automatic (HeroHealth.HandleDeath) — "Rise again" simply dismisses
        /// the sting; the existing respawn IS the route. Never pauses time (a pause
        /// would freeze the respawn coroutine this screen narrates).
        /// </summary>
        public static EndStateVM FromHeroDeath(bool enemyOwnedScene)
        {
            return new EndStateVM
            {
                Kind = EndStateKind.HeroDeath,
                Title = "YOU HAVE FALLEN",
                Subtitle = enemyOwnedScene
                    ? "The raid is lost. You retreat to the castle to fight another day."
                    : "The dark takes you, but Elarion still needs its defender.",
                Emblem = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconShield),
                PrimaryLabel = "Rise again",
                PrimaryRoute = "respawn",
                AutoDismissSeconds = 6f,
            };
        }

        /// <summary>Wave-clear RESULTS banner (replaces WaveCelebrationManager's IMGUI
        /// toast / prefab text): compact, non-blocking, auto-dismissing.</summary>
        public static EndStateVM FromWaveClear(int waveNumber)
        {
            return new EndStateVM
            {
                Kind = EndStateKind.WaveResults,
                Title = $"Wave {waveNumber} Cleared!",
                Subtitle = "The realm holds. Ready for the next.",
                Emblem = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconCombat),
                PrimaryLabel = "Continue",
                PrimaryRoute = "dismiss",
                AutoDismissSeconds = 4f,
                Compact = true,
            };
        }
    }
}
