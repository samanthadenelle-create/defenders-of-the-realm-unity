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

        // ── WO-672 Slice E: banner CTA (the ONE case the compact CTA seat returns) ──
        // Distinct from Primary on purpose: on a compact banner, tap-anywhere and
        // auto-dismiss BOTH funnel Primary — if Primary were "Repair All", a stray
        // tap or the timer would silently spend crystals. The CTA is the explicit
        // button-only action; firing it also dismisses the banner.

        /// <summary>Compact-banner CTA label (e.g. "Repair All - 40 wood, 12 iron"); null/empty = no CTA.</summary>
        public string CtaLabel;
        /// <summary>False renders the CTA disabled but still showing its cost (informative, not dead).</summary>
        public bool CtaEnabled = true;
        /// <summary>Route tag for the CTA FlowTrace line (e.g. "repair-all").</summary>
        public string CtaRoute = "cta";
        /// <summary>Invoked exactly once on CTA tap; the banner then dismisses via Primary.</summary>
        public Action Cta;

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
                    // WO-697: reward numbers render through the ONE kit formatter
                    // (ElarionUi.CompactNumber) — never verbatim six-digit strings.
                    Label = "Experience", Amount = "+" + ElarionUi.CompactNumber(xp),
                });
            if (wisdom > 0)
                vm.Spoils.Add(new SpoilRowVM
                {
                    Icon = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconTree),
                    Label = "Wisdom", Amount = "+" + ElarionUi.CompactNumber(wisdom),
                });
            if (wood > 0)
                vm.Spoils.Add(new SpoilRowVM
                {
                    // Item-catalog first (sprite-first, null-safe); no wood sheet art today
                    // -> renders as a label-only plate until the art lands.
                    Icon = ItemIconCatalog.ForConsumable("mat_wood", "Wood"),
                    Label = "Wood", Amount = "+" + ElarionUi.CompactNumber(wood),
                });
            if (iron > 0)
                vm.Spoils.Add(new SpoilRowVM
                {
                    Icon = ItemIconCatalog.ForConsumable("iron_ore_ingot", "Iron Ore"),
                    Label = "Iron", Amount = "+" + ElarionUi.CompactNumber(iron),
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

        /// <summary>
        /// HUB game-over (GameOverScreen routes here — audit §2e: its bespoke
        /// EventSystem-free manual hit-test overlay is retired). Heart-fell and
        /// hero-fell share this shape; the caller supplies the copy (the DEF-141 /
        /// WO-235 locked canon strings live in GameOverScreen). ONE way out (owner
        /// button law): Try Again — the old Leave-to-Title second exit is dropped
        /// because the template exposes exactly one primary action. NEVER
        /// auto-dismisses: an auto-fired Retry would reload the scene without
        /// player intent (the game is paused under this screen; the view's tween
        /// runs on unscaled time, so the pause is safe).
        /// </summary>
        public static EndStateVM FromGameOver(bool heartFell, string title, string body,
                                              Action onRetry)
        {
            return new EndStateVM
            {
                Kind = heartFell ? EndStateKind.Defeat : EndStateKind.HeroDeath,
                Title = title,
                Subtitle = body,
                Emblem = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconShield),
                PrimaryLabel = "Try Again",
                PrimaryRoute = "retry",
                Primary = onRetry,
                AutoDismissSeconds = 0f,   // deliberate: no softlock-guard here — Retry must be chosen
            };
        }

        /// <summary>
        /// RAID victory (baked RaidBase_* teleport scenes — replaces
        /// RaidVictoryController's bespoke "VICTORY" banner). The base is claimed and,
        /// on a new claim, the next companion joins; the ONE way out is
        /// <paramref name="onReturn"/> (SceneRouter.GoCastle) — the anti-soft-lock
        /// auto-return is the template's AutoDismissSeconds (fires the same route).
        /// </summary>
        public static EndStateVM FromRaidVictory(string joinedCompanionName,
            Action onReturn, float autoReturnSeconds = 20f,
            int stars = -1, int destructionPercent = -1, float elapsedSeconds = -1f,
            int lootCrystals = 0, int lootFood = 0)
        {
            // WO-771.6: the LOCKED-V1 scoring/loot now rides the raid victory screen —
            // stars (0-3), the %-destruction of the base, the clear time, and the loot
            // breakdown. All fields are opt-in (a caller with no scorer passes the old
            // three args and the screen renders exactly as before).
            string body = !string.IsNullOrEmpty(joinedCompanionName)
                ? "The base is CLAIMED - it is yours now.\n" + joinedCompanionName + " joins your party."
                : "The base is CLAIMED - it is yours now.";
            if (destructionPercent >= 0)
                body += "\n" + destructionPercent + "% razed.";

            var vm = new EndStateVM
            {
                Kind = EndStateKind.Victory,
                Title = "Victory!",
                Subtitle = body,
                Stars = stars >= 0 ? Mathf.Clamp(stars, 0, 3) : -1,
                TimeSeconds = elapsedSeconds >= 0f ? elapsedSeconds : -1f,
                Emblem = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconCombat),
                PrimaryLabel = "Return to Castle",
                PrimaryRoute = "return-home",
                Primary = onReturn,
                AutoDismissSeconds = Mathf.Max(2f, autoReturnSeconds),
            };

            // Loot breakdown (null Icon -> BuildSpoilRow resolves the concept icon from
            // the label, then a generic fallback — a reward row never blanks).
            if (lootCrystals > 0)
                vm.Spoils.Add(new SpoilRowVM
                {
                    Label = "Crystals", Amount = "+" + ElarionUi.CompactNumber(lootCrystals),
                });
            if (lootFood > 0)
                vm.Spoils.Add(new SpoilRowVM
                {
                    Label = "Food", Amount = "+" + ElarionUi.CompactNumber(lootFood),
                });
            return vm;
        }

        /// <summary>
        /// OUTPOST victory in the continuous-walk OuterWorld (replaces
        /// OutpostVictoryController's bespoke toast). Compact + non-blocking (no scrim):
        /// the hero KEEPS WALKING — there is no return/teleport (WO-449). The one action
        /// is a plain dismiss; AutoDismissSeconds clears it so it never lingers.
        /// </summary>
        public static EndStateVM FromOutpostVictory(string joinedCompanionName,
            bool newClaim, float autoDismissSeconds = 4f)
        {
            string body = !string.IsNullOrEmpty(joinedCompanionName)
                ? "The outpost is yours.\n" + joinedCompanionName + " joins your party."
                : (newClaim ? "The outpost is yours." : "Outpost already claimed.");

            return new EndStateVM
            {
                Kind = EndStateKind.Victory,
                Title = "Outpost Claimed",
                Subtitle = body,
                Emblem = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconCombat),
                // F8-43: no CTA on a compact banner — it auto-dismisses in seconds, so a
                // Continue button is a redundant control. Exit = auto-dismiss + tap-anywhere.
                PrimaryLabel = null,
                PrimaryRoute = "dismiss",
                AutoDismissSeconds = Mathf.Max(1f, autoDismissSeconds),
                Compact = true,
            };
        }

        /// <summary>Wave-clear RESULTS banner (replaces WaveCelebrationManager's IMGUI
        /// toast / prefab text): compact, non-blocking, auto-dismissing. F8-45 (owner
        /// 2026-07-11): carries the DAMAGE REPORT — one spoils row per damaged/destroyed
        /// structure (worst-first, capped by <see cref="WaveDamageReport.MaxRows"/>),
        /// with the IN-KIND MATERIALS repair cost (owner ruling 2026-07-11: damage
        /// fraction x the row's own catalog build cost; destroyed rows read "Rebuild"
        /// at the full build cost; crystals are never charged) where a
        /// WallRepairController exists to price it, and the production hit for damaged
        /// collectors (accrual scales with HP). State is carried by TEXT
        /// ("damaged 40%" / "DESTROYED"), never color alone (colorblind law). A clean
        /// wave keeps today's 4s row-less banner unchanged.</summary>
        public static EndStateVM FromWaveClear(int waveNumber)
        {
            var vm = new EndStateVM
            {
                Kind = EndStateKind.WaveResults,
                Title = $"Wave {waveNumber} Cleared!",
                Subtitle = "The realm holds. Ready for the next.",
                Emblem = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconCombat),
                // F8-43: no CTA on a compact banner — it auto-dismisses in seconds, so a
                // Continue button is a redundant control. Exit = auto-dismiss + tap-anywhere.
                PrimaryLabel = null,
                PrimaryRoute = "dismiss",
                AutoDismissSeconds = 4f,
                Compact = true,
            };

            // Damage report (model-side aggregation; this factory is the MVVM adapter).
            foreach (var e in WaveDamageReport.Collect())
            {
                if (e == null) continue;
                int pct = Mathf.Clamp(Mathf.RoundToInt(e.DamageFraction * 100f), 1, 100);
                string state;
                if (e.Destroyed)
                    state = e.IsCollector && e.LootStolen > 0
                        // WO-697: currency counts through the ONE kit formatter.
                        ? $"DESTROYED, looted {ElarionUi.CompactNumber(e.LootStolen)}"
                        : "DESTROYED";
                else
                    state = e.IsCollector
                        ? $"damaged {pct}%, production -{pct}%"  // economy hit = HP-scaled accrual
                        : $"damaged {pct}%";
                vm.Spoils.Add(new SpoilRowVM
                {
                    Icon = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconShield),
                    // Plain hyphen (not em-dash) in PLAYER-FACING copy: the build font has a
                    // tofu precedent (EndStateView.BuildStarRow) and canon strings use " - ".
                    Label = $"{e.Name} - {state}",
                    // In-kind materials cost only where the live controller priced it
                    // (no controller — omit rather than fake). Destroyed rows read
                    // "Rebuild" (full build cost); damaged rows read "Repair"
                    // (owner ruling 2026-07-11 — crystals never appear here).
                    Amount = e.HasCost && !WallRepairController.MaterialsZero(e.RepairCost)
                        ? (e.Destroyed ? "Rebuild " : "Repair ") +
                          WallRepairController.DescribeMaterials(e.RepairCost)
                        : string.Empty,
                });
            }

            if (vm.Spoils.Count > 0)
            {
                // The report needs reading time: 4s -> 8s, and the subtitle names the hit
                // so the banner never reads as an all-clear over a damage list.
                vm.Subtitle = "The realm holds - but it took damage.";
                vm.AutoDismissSeconds = 8f;

                // WO-672 Slice E (owner 2026-07-11: "i saw a damage report but could not
                // repair"): the CTA seat returns for THIS one case — "Repair All" wired to
                // WallRepairController.RepairAll (the one wallet/repair authority; this
                // factory is the model-side adapter, same seam as the icon resolution
                // above). Priced in IN-KIND MATERIALS summed per resource (owner ruling
                // 2026-07-11 — crystals never charged). Unaffordable renders
                // disabled-with-cost (informative, not dead). Copy uses plain hyphens
                // only: the build font has a tofu precedent (see the Label note above),
                // so no ◈/— glyphs in player copy.
                var repair = UnityEngine.Object.FindFirstObjectByType<WallRepairController>();
                var cost = repair != null
                    ? repair.RepairAllCost() : default(DeNelle.Core.Catalog.ResourceCost);
                if (repair != null && !WallRepairController.MaterialsZero(cost))
                {
                    vm.CtaLabel = "Repair All - " + WallRepairController.DescribeMaterials(cost);
                    vm.CtaEnabled = repair.CanAffordMaterials(cost);
                    vm.CtaRoute = "repair-all";
                    // RepairAll raises FeedbackShown (the existing HUD toast) with the
                    // repaired-summary; the banner itself dismisses after firing.
                    vm.Cta = () => { if (repair != null) repair.RepairAll(); };
                    // A decision now sits on the banner — extend the reading window.
                    vm.AutoDismissSeconds = 10f;
                }
            }
            return vm;
        }
    }
}
