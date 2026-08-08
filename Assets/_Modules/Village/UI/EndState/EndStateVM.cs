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
using DeNelle.Core.Diagnostics;   // Guard / FlowTrace — §12: the banner never breaks the wave loop

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
        /// <param name="primaryRoute">FlowTrace route TAG only — never behaviour. Leave null to
        /// derive it from where the win happened (see <see cref="DefaultVictoryRoute"/>).</param>
        public static EndStateVM FromBattleVictory(int stars, float durationSeconds,
            int xp, int wisdom, int wood, int iron, string gearName,
            Action onContinue, float autoTimeoutSeconds = 20f, bool perfect = false,
            string primaryRoute = null)
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
                PrimaryRoute = primaryRoute ?? DefaultVictoryRoute(),
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
                    // NO ItemIconCatalog lookup here (owner F8 2026-08-05: "it's only some
                    // triangles" / the Iron row painted an opaque cream BOX). ForConsumable
                    // ("iron_ore_ingot","Iron Ore") keyword-matched into a JPEG sprite sheet —
                    // a .jpg carries NO alpha, so the sub-sprite rendered as a solid cream plate
                    // instead of an icon. Leave Icon null exactly like the raid loot rows below:
                    // EndStateView.BuildSpoilRow then resolves the CONCEPT icon from the label
                    // ("iron" -> currency/currency_iron, concept-icons.json:201-204), which is a
                    // real PNG with alpha — the same path "Wood" already renders through.
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

        /// <summary>
        /// The honest default for an arena win's <see cref="PrimaryRoute"/> TAG.
        ///
        /// F8 2026-08-05, triage cost: PrimaryRoute is documented on the field above as a
        /// FlowTrace tag and nothing else — yet <see cref="FromBattleVictory"/> hard-coded
        /// "return-home" for EVERY arena win, dungeons included. A dungeon win performs no
        /// return home and no scene load at all: BattleArena warps the hero back to the
        /// engagement spot IN THE SAME SCENE and DungeonController.SettleEncounter logs "hero
        /// resumes in place". The trace therefore asserted a route that path was never designed
        /// to take, and triage went hunting a scene load that does not exist. Make the tag say
        /// what actually happens.
        ///
        /// STRICTLY PRESENTATIONAL: this only picks the string that lands in the FlowTrace line.
        /// Primary / onContinue / the deferred return are untouched — the win behaves identically
        /// either way. Scene-name convention matches AudioService.cs:971.
        /// </summary>
        private static string DefaultVictoryRoute()
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? string.Empty;
            return scene.StartsWith("Dungeon", StringComparison.Ordinal)
                ? "resume-in-place"
                : "return-home";
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
        /// toast / prefab text): compact, non-blocking, auto-dismissing.
        ///
        /// OWNER FELT-TEST 2026-08-08 ("I'm not seeing rewards after waves. Shouldn't we have
        /// a rewards banner?"): this banner now leads with THE EARN BEAT — the resources the
        /// wave actually banked, read from <see cref="WaveManager.TryGetPayoutFor"/> and never
        /// re-derived. Wave rewards were paid all along (WaveManager.AwardWaveResources /
        /// AwardWaveCrystals) with no surface at all: every OnWaveCleared listener in the tree
        /// is persistence / quests / dialogue / tutorial / audio / pose, and the one
        /// presentation attempt (WaveManager.ShowRewardToast) reflected for
        /// "ShowBanner(string)" / "ShowToast(string)", neither of which VillageHudController
        /// declares — so it resolved null and no-opped silently, every wave, forever.
        ///
        /// Reward rows come FIRST (the beat the player is owed), then the damage report, both
        /// inside the ONE hard row budget (<see cref="CompactMaxSpoilRows"/>) this banner can
        /// seat without EndStateView.BuildBody having to compress every band.
        ///
        /// F8-45 (owner 2026-07-11): carries the DAMAGE REPORT — one spoils row per
        /// damaged/destroyed structure (worst-first; <see cref="WaveDamageReport.MaxRows"/>
        /// caps what is collected, the row budget caps what is SHOWN and the subtitle states
        /// the shortfall),
        /// with the IN-KIND MATERIALS repair cost (owner ruling 2026-07-11: damage
        /// fraction x the row's own catalog build cost; destroyed rows read "Rebuild"
        /// at the full build cost; crystals are never charged) where a
        /// WallRepairController exists to price it, and the production hit for damaged
        /// collectors (accrual scales with HP). State is carried by TEXT
        /// ("damaged 40%" / "DESTROYED" / a leading "+" on every earned amount), never color
        /// alone (colorblind law). A wave that paid nothing AND took no damage keeps today's
        /// 4s row-less banner, unchanged.</summary>
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
            // COLLECTED FIRST, BUILT SECOND: the row budget below has to know how many damage
            // entries exist before it decides how many reward rows it may spend.
            var damage = Guard.Try<List<WaveDamageReport.Entry>>(
                "EndState", "wave " + waveNumber + " damage report",
                () => WaveDamageReport.Collect(), null);
            int damageAvailable = damage != null ? damage.Count : 0;

            // ── THE EARN BEAT (owner felt-test 2026-08-08: "I'm not seeing rewards after
            //    waves. Shouldn't we have a rewards banner?") ─────────────────────────────
            // Rewards were ALWAYS being paid (WaveManager.AwardWaveResources /
            // AwardWaveCrystals) and NOTHING rendered them. These rows read the integers
            // WaveManager BANKED (WaveManager.TryGetPayoutFor — keyed on this wave's id so a
            // previous wave's spoils can never leak in); the payout is a random roll, so a
            // re-derivation here would print numbers the wallet never received.
            // Guarded (§12): a malformed payout must never take the wave loop down with it.
            int rewardRows = 0;
            Guard.Try("EndState", "wave " + waveNumber + " reward rows",
                () => { rewardRows = AppendWavePayoutRows(vm, waveNumber, damageAvailable); });

            // Damage rows fill whatever the reward rows left of the compact budget. When any
            // damage exists the reward side is capped at MaxRows-1, so this is always >= 1.
            int damageBudget = Mathf.Max(0, CompactMaxSpoilRows - rewardRows);
            int damageRows = Mathf.Min(damageAvailable, damageBudget);
            for (int i = 0; i < damageRows; i++)
            {
                var e = damage[i];
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

            // The subtitle is the ONE place the banner's overall verdict is stated, and it is
            // now keyed on DAMAGE ROWS, not on Spoils.Count — a reward-only banner previously
            // would have inherited "it took damage" simply because it had rows.
            // Never colour-only (colourblind law): the verdict is the words themselves.
            if (damageRows > 0)
            {
                vm.Subtitle = "The realm holds - but it took damage.";
                vm.AutoDismissSeconds = 8f;   // a report needs reading time
            }
            else if (rewardRows > 0)
            {
                vm.Subtitle = "The realm holds. Spoils claimed.";
                vm.AutoDismissSeconds = 6f;
            }

            // TRUNCATION IS STATED, NEVER SILENT. WaveDamageReport hands back up to MaxRows (8)
            // entries; the compact banner can legibly seat 4 rows TOTAL (see the budget note on
            // CompactMaxSpoilRows). Dropping the tail without saying so would read as "those
            // buildings are fine". An explicit '\n' segment is used because EndStateView's
            // SubtitleLines measures each segment independently — the panel solve therefore
            // BUDGETS this second line instead of discovering it after layout.
            if (damageAvailable > damageRows)
                vm.Subtitle += "\nShowing " + damageRows + " of " + damageAvailable + " damaged structures.";

            if (damageRows > 0)
            {
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

        // ── THE COMPACT BANNER'S ROW BUDGET (derived, not picked) ─────────────────────
        //
        // Canon issue #28 is real and it fires on THIS template: EndStateView.BuildBody
        // uniform-compresses every band when the content is taller than the body well, and
        // logs "body rows COMPRESSED to fit". A wave banner that quietly crushed itself would
        // be a worse defect than the missing rewards it is fixing, so the ceiling is computed
        // here rather than discovered on device.
        //
        // THE ARITHMETIC (all constants read at source, this session):
        //   Compact banner frame  = y 0.56 .. 0.86, grows DOWN, capped at y1-0.08 = 0.78
        //                           of screen height          (EndStateView.Show / the
        //                           compact extension block)
        //   Body well             = 0.075 .. 0.745 of the panel = 0.670 of it
        //                           (ElarionUiKit ZonesFor FrameCore z.body.y = 0.075, top
        //                           clamped to 0.745 by EndStateView's compact branch — with
        //                           the dead close band reclaimed, see EndStateView)
        //   Post-scale canvas H   = 965 ref px on BOTH the owner's 2670x1200 desktop and a
        //                           2400x1080 Seeker (CanvasScaler match 0.5 -> the same
        //                           1.24 / 1.12 scale factors land within 1px of each other)
        //   => body well AT THE CLAMP = 0.670 x 0.78 x 965 = ~504 ref px
        //
        //   Band costs (EndStateView): Emblem 64, Subtitle 60 PER WRAPPED LINE, Row 64,
        //   plus an 8px gap BETWEEN bands.
        //   Wave banner BANDS = emblem(1) + subtitle(1 band, L wrapped lines) + R rows,
        //   so n = R + 2 bands and (n-1) = R + 1 gaps:
        //       need = 64 + 60L + 64R + 8(R + 1)
        //   Solving need <= 504:
        //       L=1 -> R <= 5.3     L=2 -> R <= 4.4
        //   So FOUR rows is the largest count that survives BOTH a one-line and a two-line
        //   subtitle (worst case L=2, R=4: 64 + 120 + 256 + 40 = 480 <= 504, 24px spare).
        //
        // WHAT THIS REPLACED: WaveDamageReport hands back up to MaxRows = 8 entries and the
        // old code rendered every one. Eight rows demand 64 + 60 + 512 + 72 = 708 px into a
        // well that could not exceed 504 -> scale 0.71, i.e. the F8-35 class the extension
        // block itself was written to avoid. The cap is therefore a FIX to the pre-existing
        // damage path as well as the budget for the new reward rows, and the shortfall is
        // stated in the subtitle instead of silently dropped.
        /// <summary>Hard ceiling on spoils ROWS for the compact wave-clear banner. See the
        /// derivation above — four rows is what the banner can seat at its growth clamp
        /// without BuildBody having to compress every band below its own content size.</summary>
        private const int CompactMaxSpoilRows = 4;

        /// <summary>
        /// Appends the wave's EARNED-RESOURCE rows and returns how many it added.
        ///
        /// SOURCE OF TRUTH: <see cref="WaveManager.TryGetPayoutFor"/> — the integers the wave
        /// loop actually banked, keyed on <paramref name="waveNumber"/>. Returns 0 when this
        /// wave paid nothing (the staggered WO-361 intervals mean many waves legitimately
        /// don't), and the banner then reads exactly as it does today.
        ///
        /// ROW SHAPE: one row per resource while they fit — short label left, short "+N"
        /// right, which is the column split BuildSpoilRow is built for (label gets ~0.62 of
        /// the plate, the amount ~0.34). When the payout has MORE lines than the budget
        /// allows, the TAIL folds into a single combined row rather than being dropped: no
        /// earned resource ever goes unmentioned, and no cell ever carries a string long
        /// enough to be shrunk past the font floor.
        ///
        /// Icons are deliberately left NULL: EndStateView.BuildSpoilRow then resolves the
        /// CONCEPT icon from the row's own label ("wood"/"iron"/"food"/"crystals" are all
        /// keyed in concept-icons.json) — the same data-decides path FromBattleVictory's Iron
        /// row and FromRaidVictory's loot rows use, and the reason those rows render real
        /// currency PNGs instead of a placeholder square.
        /// </summary>
        private static int AppendWavePayoutRows(EndStateVM vm, int waveNumber, int damageAvailable)
        {
            if (vm == null) return 0;
            if (!WaveManager.TryGetPayoutFor(waveNumber, out var pay))
            {
                FlowTrace.Step("EndState",
                    $"wave {waveNumber} clear banner: no payout recorded for this wave " +
                    "(staggered intervals - nothing was due), reward rows omitted.");
                return 0;
            }

            var lines = new List<KeyValuePair<string, int>>(4);
            if (pay.Wood     > 0) lines.Add(new KeyValuePair<string, int>("Wood",     pay.Wood));
            if (pay.Iron     > 0) lines.Add(new KeyValuePair<string, int>("Iron",     pay.Iron));
            if (pay.Food     > 0) lines.Add(new KeyValuePair<string, int>("Food",     pay.Food));
            if (pay.Crystals > 0) lines.Add(new KeyValuePair<string, int>("Crystals", pay.Crystals));
            if (lines.Count == 0) return 0;

            // THE SPLIT: when the wave also took damage, the damage list keeps at least one
            // row, so the reward side may claim at most CompactMaxSpoilRows-1. A clean wave
            // gives the whole budget to the spoils.
            int budget = Mathf.Max(1, damageAvailable > 0 ? CompactMaxSpoilRows - 1 : CompactMaxSpoilRows);

            if (lines.Count <= budget)
            {
                foreach (var l in lines) vm.Spoils.Add(ResourceRow(l.Key, l.Value));
                FlowTrace.Step("EndState",
                    $"wave {waveNumber} clear banner: {lines.Count} reward row(s) from the BANKED payout " +
                    $"(wood={pay.Wood} iron={pay.Iron} food={pay.Food} crystals={pay.Crystals}), " +
                    $"budget={budget} of {CompactMaxSpoilRows}.");
                return lines.Count;
            }

            // Overflow (reachable: a live ServerConfig event pays crystals EVERY wave, so a
            // wave divisible by 2/3/4 with damage hits four resource lines against a budget of
            // three). Emit the first budget-1 rows individually, then ONE combined tail row.
            for (int i = 0; i < budget - 1; i++)
                vm.Spoils.Add(ResourceRow(lines[i].Key, lines[i].Value));

            var tailLabel = new System.Text.StringBuilder();
            var tailAmount = new System.Text.StringBuilder();
            for (int i = budget - 1; i < lines.Count; i++)
            {
                if (tailLabel.Length > 0) { tailLabel.Append(" + "); tailAmount.Append(", "); }
                tailLabel.Append(lines[i].Key);
                tailAmount.Append('+').Append(ElarionUi.CompactNumber(lines[i].Value));
            }
            vm.Spoils.Add(new SpoilRowVM
            {
                // No Icon: the combined label resolves no single concept, so BuildSpoilRow's
                // generic kit fallback (a chest) stands in — apt for "mixed spoils".
                Label = tailLabel.ToString(),
                Amount = tailAmount.ToString(),
            });
            FlowTrace.Step("EndState",
                $"wave {waveNumber} clear banner: {lines.Count} paid resources folded into {budget} row(s) " +
                $"(budget {budget} of {CompactMaxSpoilRows}, damage entries={damageAvailable}) - the tail is " +
                "COMBINED, never dropped.");
            return budget;
        }

        /// <summary>One earned-resource row. ASCII only, and the "+" prefix (not colour) is
        /// what marks it as a gain — the damage rows on the same banner carry no "+".</summary>
        private static SpoilRowVM ResourceRow(string label, int amount)
        {
            return new SpoilRowVM
            {
                // WO-697: every reward number renders through the ONE kit formatter.
                Label = label, Amount = "+" + ElarionUi.CompactNumber(amount),
            };
        }
    }
}
