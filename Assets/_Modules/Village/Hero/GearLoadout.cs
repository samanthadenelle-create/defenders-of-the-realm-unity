// =============================================================================
// GearLoadout — Gear v1 equip model on the hero (auto-equip best + manual shop/equip).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Attaches to the hero. Reads the hero's class (HeroAbilities.HeroClass) + level
// (HeroProgression.Level) and auto-equips the BEST weapon + armor the hero
// currently qualifies for from GearCatalog. Re-evaluates on every level-up.
//
// No equip UI yet (that's the art-gated layer) — v1 auto-equips so the power
// curve works immediately: level up -> qualify for a stronger weapon -> it
// equips -> every ability hits harder. Manual equip + loot drops layer on later.
//
// Exposes:
//   WeaponMult    — multiply the hero's outgoing damage (HeroAbilities reads it).
//   ArmorDefense  — fractional incoming-damage reduction (HeroHealth reads it).
//
// GRACEFUL: if no catalog / no eligible item, WeaponMult stays 1.0 and
// ArmorDefense stays 0 — existing combat is unchanged. HeroAbilities lazily
// adds this component, so it works on every hero with no builder/scene change.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;
// NOTE: DeNelle.Core.State is referenced FULLY QUALIFIED below (EquipPrefKeys) rather than
// imported, so pulling in that namespace can never shadow a DeNelle.Village type here.

namespace DeNelle.Village
{
    /// <summary>
    /// One class's authored STARTING kit — the two hand slots a brand-new hero of that
    /// class begins with. Pure data (ids into weapons.json); no behaviour.
    /// </summary>
    public sealed class StarterKit
    {
        /// <summary>MAIN-hand weapon id (weapons.json). Never null on an authored kit.</summary>
        public readonly string MainHand;
        /// <summary>OFF-hand / shield id (weapons.json), or null when the class starts one-handed-empty.</summary>
        public readonly string OffHand;
        /// <summary>
        /// The highest hero level at which the STARTER main hand still wins over
        /// <see cref="GearCatalog.BestWeapon"/>. Above it the auto-best power curve resumes,
        /// so levelling still upgrades the hero's weapon exactly as before.
        /// </summary>
        public readonly int MainHandUpToLevel;

        public StarterKit(string mainHand, string offHand, int mainHandUpToLevel = 1)
        {
            MainHand = mainHand;
            OffHand = offHand;
            MainHandUpToLevel = mainHandUpToLevel;
        }
    }

    /// <summary>
    /// WO-860 Part A2 — the per-class STARTER LOADOUT: the ONE source of truth for
    /// "what does a brand-new &lt;class&gt; hold in each hand".
    ///
    /// WHY A TABLE AND NOT AN "if (Knight)" BRANCH (review-mandated): before this, a new
    /// game's weapon came from <see cref="GearCatalog.BestWeapon"/>, i.e. the highest
    /// damageMult the class qualifies for at that level — which at level 1 is
    /// `knight_flameblade` (1.2), NOT the intended `knight_starter` "Squire's Blade" (1.0).
    /// The intended opening kit is a DESIGN statement, not a stat maximum, so it has to be
    /// authored. WO-861 then adds Ranger + Mage rows HERE and nothing else changes.
    ///
    /// SCOPE: this is the DEFAULT, not a choice. It is applied only when the player has
    /// never persisted an equip for that slot (see GearLoadout.Refresh); the moment they
    /// equip anything, their persisted pick wins forever after.
    ///
    /// WO-861 TODO (deliberately NOT pre-seeded): Ranger and Mage rows go here once their
    /// gear ids EXIST in weapons.json — `ranger_arrow_plain` + `tripo_dagger_a` for Sylas,
    /// a `mage_*` staff for Thrain (WO-861 Appendix A2/A1). Seeding ids that do not resolve
    /// would turn a hard failure into a silent Warn-and-no-weapon, so they land WITH the data.
    /// </summary>
    public static class StarterLoadout
    {
        private static readonly Dictionary<string, StarterKit> Kits =
            new Dictionary<string, StarterKit>(System.StringComparer.OrdinalIgnoreCase)
            {
                // Grom the Knight — Squire's Blade + Squire's Heater (the owner's
                // "on start should be a sword and shield"). Both ids verified present in
                // weapons.json; knight_starter is also the ONE weapon EquipmentController
                // maps natively to the sword_A prop, so the mesh attach is the proven path.
                { "knight", new StarterKit("knight_starter", "knight_shield_starter") },
            };

        /// <summary>The authored starter kit for a class key ("knight"), or null when unauthored.</summary>
        public static StarterKit For(string job)
        {
            if (string.IsNullOrEmpty(job)) return null;
            return Kits.TryGetValue(job.Trim(), out var kit) ? kit : null;
        }

        /// <summary>The authored starter OFF-HAND id for a class, or null. Used by the body seed.</summary>
        public static string OffHandFor(string job)
        {
            var kit = For(job);
            return kit != null ? kit.OffHand : null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class GearLoadout : MonoBehaviour
    {
        /// <summary>Outgoing-damage multiplier from the equipped weapon (1.0 = none).</summary>
        public float WeaponMult { get; private set; } = 1f;

        /// <summary>Fractional incoming-damage reduction from equipped armor (0 = none).</summary>
        public float ArmorDefense { get; private set; } = 0f;

        /// <summary>
        /// THE armor damage-reduction ceiling. OWNER-LOCKED at 0.90 (2026-08-02): "the cap is
        /// 0.90, and display and engine must agree on it."
        ///
        /// WHY THIS CONSTANT EXISTS AT ALL. Before it there were FOURTEEN copies of the literal
        /// and they had drifted into two different numbers: <see cref="ApplyStats"/> clamped the
        /// APPLIED value at 0.70 while every shop / equip / inventory readout clamped its display
        /// at 0.90. The store advertised "+85% def" and the damage chain granted 70% - a lie the
        /// player pays gold and resources for, and one that no compiler could ever catch because
        /// both sides were self-consistent literals. There is now exactly ONE definition; every
        /// engine clamp and every display clamp reads THIS symbol. Never re-inline the number.
        ///
        /// SCOPE - what this is NOT:
        ///   * NOT the talent damage-reduction cap. HeroTalentModifiers' reduction is clamped at
        ///     0.95 inside HeroHealth (HeroHealth.cs ~:543). That is a SEPARATE mitigation stage
        ///     applied after this one and is deliberately a different number; it is not a
        ///     fifteenth copy of this cap and must not be folded into it.
        ///   * NOT a per-item bound. GearStatResolver.Effective* clamps each RESOLVED piece here
        ///     too, so a single fat-fingered 1.3 in a json row can never alone grant immunity.
        ///
        /// The value is deliberately below 1.0: at 0.90 the hero still takes a tenth of every
        /// blow, so no stack of gear can ever make an encounter unloseable.
        /// </summary>
        public const float MaxArmorDefense = 0.90f;

        /// <summary>The currently-equipped pieces (null when nothing qualifies). For UI/debug.</summary>
        public WeaponDef EquippedWeapon { get; private set; }   // MAIN hand
        public ArmorDef  EquippedArmor  { get; private set; }

        // ── OFF-HAND slot (owner 2026-06-18, docs/STORE_EQUIP_SPEC.md "Equip-slot rules") ──
        // The hero has two hand slots: EquippedWeapon = MAIN hand, EquippedOffHand = OFF hand.
        // EquippedOffHand holds a shield / off-hand item (WeaponDef with IsOffHandItem true, or a
        // 1h off-hand). The two slots are mutually constrained:
        //   • a 1H main-hand weapon + an off-hand may BOTH be equipped;
        //   • a 2H weapon occupies BOTH hands → it clears the off-hand, and equipping an off-hand
        //     while a 2H is held clears the 2H (falling the main hand back to a 1H or empty).
        // Enforced in EnforceHandSlots, never set raw.
        /// <summary>The currently-equipped OFF-HAND item (shield / off-hand), or null. For UI/debug/attach.</summary>
        public WeaponDef EquippedOffHand { get; private set; }

        // ── ACCESSORY slots (WO-543) — rings + amulets, pure stat modifiers (no mesh) ──
        /// <summary>The currently-equipped RING accessory, or null. Pure stat modifier (no mesh).</summary>
        public AccessoryDef EquippedRing { get; private set; }
        /// <summary>The currently-equipped AMULET accessory, or null. Pure stat modifier (no mesh).</summary>
        public AccessoryDef EquippedAmulet { get; private set; }

        /// <summary>Flat HP bonus from equipped armor + accessories (WO-543). HeroHealth folds this into max HP.</summary>
        public int GearHpBonus { get; private set; }

        /// <summary>Fired after any manual or auto equip change (shop/equip UI can subscribe to refresh lists or HUD).</summary>
        public event System.Action OnGearChanged;

        // ── WO-295: Legendary "Aegis of Elarion" set bonus ───────────────────────
        // The full Aegis set = an Aegis WEAPON (per-class) + the Aegis ARMOR, both
        // carrying setId "aegis". A full set grants:
        //   • the "Oathweld" ward — a portion of damage the hero takes is refunded
        //     as HP to the Heart (mechanically ties survival to the win condition).
        //     Driven by the lazily-attached AegisSetEffect component.
        //   • a per-class weapon perk, expressed as an extra damage multiplier folded
        //     into WeaponMult (so it flows through the existing damage chain without
        //     forking combat), plus a small bonus armour defense.
        // All gated on AegisSetEffect.SetActive; ordinary gear is untouched.

        /// <summary>True when a full Aegis set (Aegis weapon + Aegis armor) is equipped.</summary>
        public bool AegisSetActive =>
            EquippedWeapon != null && EquippedWeapon.IsAegis &&
            EquippedArmor  != null && EquippedArmor.IsAegis;

        /// <summary>Fraction of damage taken that the Oathweld ward refunds to the Heart (0 when no set).</summary>
        public float WardRefundFraction => AegisSetActive ? 0.25f : 0f;

        // Per-class Aegis weapon perk as a flat extra outgoing-damage multiplier,
        // folded into WeaponMult when the full set is active. Knight (shock combo) and
        // Mage (cost-down → leans on raw power up close) get the larger bump; Archer
        // (pierce/mark) and Cleric (heal-also-wards) a steadier one. Data-tunable later.
        private static float AegisWeaponPerkMult(string job)
        {
            switch ((job ?? string.Empty).ToLowerInvariant())
            {
                case "knight": return 1.15f;  // Emberbrand — stored-aether shock finisher
                case "mage":   return 1.15f;  // Aetherstaff — spells hit harder up close
                case "ranger": return 1.10f;  // Heartwood Longbow — pierce + mark
                case "cleric": return 1.10f;  // Hallowed Censer — heal-also-wards
                default:       return 1.0f;
            }
        }

        // Extra flat defense the Oathweld plating adds on top of the armor's own value
        // when the full set is worn (clamped with the base in Refresh/Equip*).
        private const float AegisSetDefenseBonus = 0.05f;

        private HeroAbilities   _abilities;
        private HeroProgression _progression;

        // Per-class PERSISTED equip (owner 2026-06-16): a manual equip from the equip UI
        // is saved under the wearer's class so it sticks across loads — for the hero AND
        // each companion. Keyed by class name so every party member keeps its own loadout.
        // WO-860 Part A1: the literals moved to DeNelle.Core.State.EquipPrefKeys so the
        // WRITER (here) and the New-Game ERASER (GameStateService.ClearEquipPrefs) can never
        // drift apart — a reset that misses a prefix is exactly how the stale axe survived.
        private const string PrefWeaponKey  = DeNelle.Core.State.EquipPrefKeys.Weapon;    // + <class>  (main hand)
        private const string PrefArmorKey   = DeNelle.Core.State.EquipPrefKeys.Armor;     // + <class>
        private const string PrefOffHandKey = DeNelle.Core.State.EquipPrefKeys.OffHand;   // + <class>  (off hand / shield)
        private const string PrefRingKey    = DeNelle.Core.State.EquipPrefKeys.Ring;      // + <class>  (WO-543 accessory)
        private const string PrefAmuletKey  = DeNelle.Core.State.EquipPrefKeys.Amulet;    // + <class>  (WO-543 accessory)

        // WO-434: explicit "player removed this slot" sentinel. Written to the SAME per-class
        // PlayerPrefs key the Equip methods use, so a later Refresh/level-up honours the empty
        // choice and does NOT silently auto-re-equip the best piece the player just took off.
        // Distinct from "key absent" (= never chosen → auto-best is fine, legacy behaviour).
        private const string PrefNoneSentinel = "__none__";

        // Set on a COMPANION loadout (which has no HeroAbilities). When non-null it is the
        // authoritative class for this loadout (BindOwnerClass). Null on the player hero,
        // which reads its class from HeroAbilities as before.
        private string _ownerClassOverride;

        /// <summary>
        /// Bind this loadout to a specific class (used for companion bodies, which carry no
        /// HeroAbilities). Sets the authoritative class, then re-resolves gear so the wearer
        /// auto-equips its best — or its persisted manual choice — for that class immediately.
        /// </summary>
        public void BindOwnerClass(string job)
        {
            _ownerClassOverride = string.IsNullOrEmpty(job) ? null : job;
            Refresh();
        }

        private void Awake()
        {
            _abilities   = GetComponent<HeroAbilities>();
            _progression = GetComponent<HeroProgression>();
        }

        private void OnEnable()
        {
            if (_progression != null) _progression.OnLevelUp += OnLevelUp;
            Refresh();
        }

        private void OnDisable()
        {
            if (_progression != null) _progression.OnLevelUp -= OnLevelUp;
        }

        private void OnLevelUp(int newLevel) => Refresh();

        /// <summary>Re-resolve class + level and equip the best eligible weapon + armor.</summary>
        public void Refresh()
        {
            if (_abilities == null)   _abilities   = GetComponent<HeroAbilities>();
            if (_progression == null) _progression = GetComponent<HeroProgression>();

            string job = CurrentJob();
            int level  = _progression != null ? _progression.Level : 1;

            // WO-860 Part A2 — the class STARTER wins over auto-best on a FRESH hero.
            // Precedence (highest first): persisted player choice > authored starter > auto-best.
            // The starter only applies while the hero is at/below its authored level AND the
            // player has never persisted a main-hand choice for this class, so:
            //   * a new game opens on Squire's Blade, not Flameblade (auto-best's damageMult pick);
            //   * levelling still auto-upgrades exactly as before (the starter stops applying);
            //   * anything the player equips or explicitly un-equips still wins (ApplyPersistedEquip).
            // §12 PROVENANCE: which of the precedence tiers actually produced the main hand.
            // Without this the capture line below reads the same whether the hero opened on
            // the authored starter, on auto-best, or on a weapon a PREVIOUS SESSION persisted
            // — and those have three different fixes.
            string mainHandSource;
            var starter = ResolveStarterMainHand(job, level);
            if (starter != null)
            {
                EquippedWeapon = starter;
                mainHandSource = "authored-starter";
            }
            else
            {
                // OWNERSHIP-GATED auto-best (2026-08-08). Never a bare GearCatalog.BestWeapon
                // any more: that ranked the WHOLE CATALOG by damageMult and handed a level-2
                // knight the purchasable `knight_flameblade` for free. See ResolveAutoBestMainHand
                // — every one of its return paths lands on a real, class-legal weapon.
                EquippedWeapon = ResolveAutoBestMainHand(job, level, out mainHandSource);
            }

            // ARMOR IS DELIBERATELY STILL CATALOG-WIDE. The same free-shop-item leak exists on
            // this line (armor.json rows are Armorer stock too), but there is NO authored starter
            // ARMOR in StarterLoadout to fall back to, so gating it would resolve to null on a
            // fresh save and drop the hero to ArmorDefense 0 — trading an economy leak for a
            // silent difficulty spike. Author a starter armor row in StarterKit FIRST, then this
            // line takes the same treatment. Flagged rather than half-done on purpose.
            EquippedArmor  = GearCatalog.BestArmor(job, level);

            // A PERSISTED manual choice (from the equip UI) wins over auto-best, so gear
            // assigned to this member sticks across loads. Only applied when it still fits
            // the class (a light-armor wearer never restores a heavy piece).
            if (ApplyPersistedEquip(job)) mainHandSource = "persisted-playerprefs";

            // Resolve the main-hand / off-hand pair to a legal state after the picks above
            // (auto-best may pick a 2H while a persisted off-hand restored, etc.). Mutually
            // exclusive 2H↔off-hand enforced here, with the 1H fallback / armed-hero guard.
            EnforceHandSlots(job, level);

            // WO-425 one-shot diagnostic: definitively shows null-weapon (#1 data) vs has-weapon
            // (#2 missing mesh art) on the next playtest. Refresh is NOT hot (OnEnable + OnLevelUp
            // only), so a single Step per call is correct — no Once/Throttle guard needed.
            FlowTrace.Step("Gear", $"Refresh: job='{job}' level={level} " +
                $"bestWeapon='{EquippedWeapon?.id ?? "<null>"}' source={mainHandSource} " +
                $"offHand='{EquippedOffHand?.id ?? "<null>"}' " +
                $"bestArmor='{EquippedArmor?.id ?? "<null>"}'");

            ApplyStats(job);
            OnGearChanged?.Invoke();
        }

        /// <summary>
        /// WO-860 Part A2 — the authored STARTER main hand for this class, or null when it
        /// does not apply (no authored kit / hero has out-levelled it / the player already
        /// has a persisted choice / the id does not resolve or does not fit the class).
        ///
        /// The "no persisted choice" test is <see cref="PlayerPrefs.HasKey"/>, NOT a
        /// non-empty value: an explicit un-equip persists the "__none__" sentinel, and a
        /// player who deliberately fought bare-handed must not have a sword handed back.
        /// </summary>
        private static WeaponDef ResolveStarterMainHand(string job, int level)
        {
            var kit = StarterLoadout.For(job);
            if (kit == null || string.IsNullOrEmpty(kit.MainHand)) return null;
            if (level > kit.MainHandUpToLevel)
            {
                // §12 INSTRUMENT — THE branch behind the owner's 2026-08-08 report ("when I
                // started I started with the flame blade ... left over from the dev build").
                // PROVEN, not inferred, from the captured Player.log of 2026-08-08:
                //     [Flow:Gear] Refresh: job='knight' level=2 bestWeapon='knight_flameblade'
                //                 offHand='<null>' bestArmor='armor_knight_common'
                // The hero was NOT level 1, so the authored starter stopped applying here and
                // GearCatalog.BestWeapon took over — and BestWeapon ranks purely by damageMult
                // (GearCatalog.cs:281), so it hands over `knight_flameblade` (1.2) instead of
                // `knight_starter` (1.0). Both are req.level 1, so this fires the instant the
                // hero is level 2, and the Flameblade is a 40-wood/120-iron Forge item the
                // player never bought.
                //
                // This bail emitted NOTHING, which is why the capture looked like a caching
                // bug: the trace showed the RESULT (flameblade) with no hint that a LEVEL gate,
                // not a stale equip pref, had opened the door. One line per Refresh only
                // (OnEnable + OnLevelUp), so no throttle is warranted.
                FlowTrace.Step("Gear",
                    $"StarterLoadout SKIPPED for class '{job}': hero level {level} is ABOVE the kit's " +
                    $"MainHandUpToLevel={kit.MainHandUpToLevel}, so the authored starter " +
                    $"('{kit.MainHand}') no longer applies and ResolveAutoBestMainHand picks the main " +
                    "hand instead. SINCE 2026-08-08 that pick is restricted to gear the wearer OWNS " +
                    "(see the [Flow:Gear] AutoBest: line that follows) - it used to be the highest " +
                    "damageMult in the WHOLE catalog, which is how a level-2 knight was handed the " +
                    "purchasable knight_flameblade for free. If this fires on what is supposed to be a " +
                    "FIRST-TIME player, the hero level came from a PREVIOUS session's save - a fresh " +
                    "game is level 1.");
                return null;                                            // auto-best power curve resumes
            }

            string key = (job ?? string.Empty).ToLowerInvariant();
            string prefKey = PrefWeaponKey + key;
            if (PlayerPrefs.HasKey(prefKey))
            {
                // §12 INSTRUMENT (owner report 2026-08-08: "I started with the flame blade …
                // that was left over from the dev build"). THIS is the branch that decides
                // "authored starter" vs "whatever some earlier session persisted", and until
                // now it returned null in SILENCE — every other bail in this method Warns, so
                // a capture showing `bestWeapon='knight_flameblade'` could not distinguish a
                // stale pref from a broken starter id. It is not a hot path (Refresh runs on
                // OnEnable + OnLevelUp only), so one line per call is correct.
                //
                // WHY A DEV BUILD CAN POISON A RELEASE BUILD: PlayerPrefs is keyed off
                // companyName + productName (ProjectSettings.asset:15-16, "DeNelle" /
                // "Defenders of the Realm"), which are IDENTICAL for a Development and a
                // Release player of this project. Both therefore read and write the SAME
                // store, so a weapon equipped in a dev session is still sitting under this
                // key when the release build boots. On a genuinely fresh install the key
                // does not exist and the authored starter applies normally.
                FlowTrace.Warn("Gear",
                    $"StarterLoadout SKIPPED for class '{job}' (level {level}): PlayerPrefs key " +
                    $"'{prefKey}' ALREADY EXISTS (value='{PlayerPrefs.GetString(prefKey, "<unset>")}'), " +
                    "so the PERSISTED equip wins over the authored starter " +
                    $"('{kit.MainHand}'). A brand-new player must NOT have this key — if this line " +
                    "fires on what is supposed to be a first launch, the save/prefs carry a PREVIOUS " +
                    "session's equip. New Game (TitleController.OnStartNew -> " +
                    "GameStateService.ResetToNewGame -> ClearEquipPrefs) is what erases it.");
                return null;                                             // the player has chosen — never override
            }

            var w = GearCatalog.FindWeapon(kit.MainHand);
            if (w == null)
            {
                FlowTrace.Warn("Gear",
                    $"StarterLoadout['{job}'].MainHand = '{kit.MainHand}' is NOT in weapons.json - " +
                    "falling back to auto-best. Author the row or fix the id.");
                return null;
            }
            if (!GearCatalog.WeaponFitsClass(w, job))
            {
                FlowTrace.Warn("Gear",
                    $"StarterLoadout['{job}'].MainHand = '{kit.MainHand}' does not fit class '{job}' " +
                    "(job gate) - falling back to auto-best.");
                return null;
            }
            FlowTrace.Step("Gear",
                $"StarterLoadout applied: class='{job}' level={level} mainHand='{w.id}' " +
                "(authored starter beats auto-best on a fresh hero).");
            return w;
        }

        // =====================================================================
        //  OWNERSHIP-GATED AUTO-BEST  (owner-felt defect, 2026-08-08)
        // ---------------------------------------------------------------------
        //  PROVEN, from the captured Player.log of 2026-08-08:
        //      [Flow:Gear] Refresh: job='knight' level=2 bestWeapon='knight_flameblade'
        //                  source=auto-best offHand='<null>' bestArmor='armor_knight_common'
        //  The hero was level 2, so the authored starter kit stopped applying (StarterKit.
        //  MainHandUpToLevel == 1) and GearCatalog.BestWeapon took over. That ranked by
        //  damageMult across the WHOLE CATALOG and returned `knight_flameblade` (1.2) over
        //  `knight_starter` (1.0) - and knight_flameblade is a PURCHASABLE Forge item. Every
        //  knight who levelled once without ever shopping was handed a paid weapon for free.
        //
        //  THE RULING: auto-upgrade-on-level-up STAYS (it is WO-860's intended feature). What
        //  changes is the CANDIDATE SET - auto-best may only rank gear the player OWNS.
        //  Ranking unowned shop stock is the bug; upgrading among owned gear is the feature.
        //
        //  THE HARD SAFETY REQUIREMENT: the hero must NEVER end up weaponless. Every path
        //  below either returns a real, class-legal WeaponDef or falls through to the SAME
        //  catalog-wide query that shipped before this change - so the worst case is exactly
        //  today's behaviour, never a null hand. A null weapon on a fresh save would be a far
        //  worse bug than the free flameblade.
        // =====================================================================

        /// <summary>
        /// The set of gear ids the wearer OWNS, or an UNRESOLVED marker.
        ///
        /// <c>Resolved</c> is the load-bearing distinction: "the player owns nothing"
        /// and "we cannot currently tell what the player owns" must never collapse into the
        /// same empty set, because the second one happens on every boot before the save is up
        /// (VillageInventory.EnsureLoaded bails while GameStateService.Instance.State is null,
        /// leaving Counts a PRE-LOAD empty that looks identical to a genuinely empty bag).
        /// Treating that as "owns nothing" would strip a levelled hero's weapon at every launch.
        /// </summary>
        private readonly struct OwnedGearSet
        {
            /// <summary>Owned ids (case-insensitive), or null when ownership could not be resolved.</summary>
            public readonly HashSet<string> Ids;
            /// <summary>Per-source counts, for the §12 capture line.</summary>
            public readonly string Source;

            public OwnedGearSet(HashSet<string> ids, string source) { Ids = ids; Source = source; }

            /// <summary>False when no ownership authority was available at all — callers MUST
            /// then fall back to the authored starter, never to a catalog-wide damageMult pick.</summary>
            public bool Resolved => Ids != null;

            /// <summary>The predicate handed to <see cref="GearCatalog.PickBestWeapon"/>.</summary>
            public bool Owns(string id) => Ids != null && !string.IsNullOrEmpty(id) && Ids.Contains(id);
        }

        /// <summary>
        /// Resolve what this wearer owns. FOUR sources are unioned, and every one of them is an
        /// EXISTING record — nothing here invents an ownership concept:
        ///
        ///   1. <c>GameState.GearInventory</c> — THE persisted source of truth (SaveSchema.cs:262,
        ///      "gearInventory", save v20; GameState.cs:135). Shop purchases land here via
        ///      ShopVM.TryBuyWeapon -> VillageInventory.Add -> SyncToState.
        ///   2. <c>VillageInventory.Instance.Counts</c> — the runtime cache over (1). Read as well
        ///      as (1) so a grant made THIS session before a save flush still counts.
        ///   3. The authored STARTER KIT ids for this class. The kit is GRANTED, not purchased, so
        ///      it is never written to VillageInventory — but the player unquestionably owns it,
        ///      and it is the fallback the safety chain leans on. Omitting it here would let the
        ///      starter be filtered out of its own fallback, which is the one way this fix could
        ///      leave a hero unarmed.
        ///   4. The per-class PERSISTED equip ids (weapon + off-hand). A id only ever reaches those
        ///      keys because something EXPLICITLY equipped it — a shop/EquipmentPanel equip of an
        ///      inventory item, or an arena/outpost loot grant (EnemyOutpost/BattleArena call
        ///      GearLoadout.EquipWeaponById directly and never touch VillageInventory). Those are
        ///      legitimately earned, so they stay ownable. Note this does NOT re-admit the
        ///      flameblade: an AUTO-equipped weapon never writes a pref key (only Equip*ById does),
        ///      which is exactly why the 2026-08-08 capture reported source=auto-best.
        /// </summary>
        private static OwnedGearSet ResolveOwnedGear(string job)
        {
            var svc = DeNelle.Core.State.GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            var inv = DeNelle.Village.Crafting.VillageInventory.Instance;

            bool haveState = state != null;
            bool haveRuntime = inv != null && inv.Counts != null && inv.Counts.Count > 0;

            // NO authority at all: the save service is down AND the runtime bag is empty/absent.
            // That is indistinguishable from a pre-load boot frame, so we refuse to answer.
            if (!haveState && !haveRuntime) return default;

            var ids = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            int fromSave = 0, fromRuntime = 0, fromKit = 0, fromEquipped = 0;

            if (haveState && state.GearInventory != null)
                foreach (var kv in state.GearInventory)
                    if (kv.Value > 0 && !string.IsNullOrEmpty(kv.Key) && ids.Add(kv.Key)) fromSave++;

            if (inv != null && inv.Counts != null)
                foreach (var kv in inv.Counts)
                    if (kv.Value > 0 && !string.IsNullOrEmpty(kv.Key) && ids.Add(kv.Key)) fromRuntime++;

            var kit = StarterLoadout.For(job);
            if (kit != null)
            {
                if (!string.IsNullOrEmpty(kit.MainHand) && ids.Add(kit.MainHand)) fromKit++;
                if (!string.IsNullOrEmpty(kit.OffHand)  && ids.Add(kit.OffHand))  fromKit++;
            }

            string key = (job ?? string.Empty).ToLowerInvariant();
            string equippedWeapon = PlayerPrefs.GetString(PrefWeaponKey + key, null);
            if (!string.IsNullOrEmpty(equippedWeapon) && equippedWeapon != PrefNoneSentinel &&
                ids.Add(equippedWeapon)) fromEquipped++;
            string equippedOffHand = PlayerPrefs.GetString(PrefOffHandKey + key, null);
            if (!string.IsNullOrEmpty(equippedOffHand) && equippedOffHand != PrefNoneSentinel &&
                ids.Add(equippedOffHand)) fromEquipped++;

            return new OwnedGearSet(ids,
                $"save={fromSave} runtime={fromRuntime} kit={fromKit} equipped={fromEquipped} total={ids.Count}");
        }

        /// <summary>
        /// The auto-best MAIN HAND, restricted to owned gear, with a floor that can never be a
        /// null hand. <paramref name="branch"/> names which return path fired (§12).
        ///
        /// RETURN PATHS — all four are proven non-weaponless:
        ///   A. ownership UNRESOLVED  -> <see cref="StarterOrCatalogFloor"/> (authored starter).
        ///   B. owned pick found      -> that weapon (class + level + main-hand gated by PickBestWeapon).
        ///   C. owned set had none    -> <see cref="StarterOrCatalogFloor"/> (authored starter).
        ///   D. inside the floor, if the class has no authored kit (Ranger/Mage today) it falls
        ///      through to the SAME catalog-wide GearCatalog.BestWeapon this method replaced —
        ///      i.e. unchanged pre-fix behaviour, so it cannot be null unless it was already null,
        ///      which ArmedHeroInvariantRegression [armed-hero-levels] already fails on.
        /// </summary>
        private static WeaponDef ResolveAutoBestMainHand(string job, int level, out string branch)
        {
            var owned = ResolveOwnedGear(job);

            if (!owned.Resolved)
            {
                var safe = StarterOrCatalogFloor(job, level, out branch, "ownership-unresolved");
                FlowTrace.Warn("Gear",
                    $"AutoBest: job='{job}' level={level} eligible=n/a owned=n/a ownershipFiltered=FALSE " +
                    $"ownedSource=<unresolved> branch={branch} weapon='{safe?.id ?? "<null>"}' - no ownership " +
                    "authority was available (GameStateService.State is null AND VillageInventory is empty/absent), " +
                    "so the owned set could not be told apart from a pre-load empty bag. Fell back to the AUTHORED " +
                    "STARTER rather than the catalog-wide damageMult pick - handing over a shop item is never the " +
                    "safe default. If this fires after the save is up, the boot order changed.");
                return safe;
            }

            var pick = GearCatalog.PickBestWeapon(job, level, owned.Owns);
            if (pick.Weapon != null)
            {
                branch = "owned-auto-best";
                FlowTrace.Step("Gear",
                    $"AutoBest: job='{job}' level={level} eligible={pick.Eligible} owned={pick.Owned} " +
                    $"ownershipFiltered=TRUE ownedSource=({owned.Source}) branch={branch} weapon='{pick.Weapon.id}' " +
                    $"(dmgMult={pick.Weapon.damageMult:0.00}) - the highest-damageMult weapon the wearer OWNS. " +
                    $"{pick.Eligible - pick.Owned} class-eligible catalog row(s) were excluded as unowned shop stock.");
                return pick.Weapon;
            }

            var floor = StarterOrCatalogFloor(job, level, out branch, "owned-set-empty");
            FlowTrace.Warn("Gear",
                $"AutoBest: job='{job}' level={level} eligible={pick.Eligible} owned=0 ownershipFiltered=TRUE " +
                $"ownedSource=({owned.Source}) branch={branch} weapon='{floor?.id ?? "<null>"}' - the wearer owns " +
                "NONE of the class-eligible main-hand rows, so the authored starter floor armed them. Not an error " +
                "on a fresh save; it IS an error if the wearer just bought a weapon (check it reached " +
                "VillageInventory.Add -> GameState.GearInventory).");
            return floor;
        }

        /// <summary>
        /// THE never-weaponless floor: the class's authored starter main hand, else the pre-fix
        /// catalog-wide pick.
        ///
        /// It deliberately IGNORES <see cref="StarterKit.MainHandUpToLevel"/>. That field decides
        /// when the starter stops being the *intended opening kit*; this is not that question, it
        /// is "the hero must be holding something real". A level-20 knight who somehow owns nothing
        /// gets a Squire's Blade, which is weak but valid — and strictly better than empty hands.
        /// </summary>
        private static WeaponDef StarterOrCatalogFloor(string job, int level, out string branch, string why)
        {
            var kit = StarterLoadout.For(job);
            if (kit != null && !string.IsNullOrEmpty(kit.MainHand))
            {
                var w = GearCatalog.FindWeapon(kit.MainHand);
                if (w != null && !w.IsOffHandItem && GearCatalog.WeaponFitsClass(w, job))
                {
                    branch = "starter-floor:" + why;
                    return w;
                }
            }

            // No authored kit for this class (Ranger / Mage today), or its id no longer resolves.
            // Fall through to EXACTLY the query this method replaced, so behaviour here is
            // byte-for-byte the pre-fix behaviour and can never be worse.
            branch = "catalog-wide-floor:" + why;
            var any = GearCatalog.BestWeapon(job, level);
            if (any == null)
                FlowTrace.Fail("Gear",
                    $"AutoBest floor EXHAUSTED for class '{job}' at level {level} ({why}): the class has no " +
                    "authored starter kit AND the catalog-wide main-hand query returned null, so the hero has NO " +
                    "main hand. This is a CATALOG gap, not an ownership gap - weapons.json serves this class " +
                    "nothing at this level, and it would have been unarmed before the ownership gate existed too. " +
                    "ArmedHeroInvariantRegression [armed-hero-levels] pins exactly this.");
            return any;
        }

        /// <summary>
        /// The one-handed MAIN-HAND refill EnforceHandSlots uses when it evicts a shield from the
        /// main slot or drops a 2H that conflicts with an off-hand. Same ownership gate and the
        /// same never-weaponless floor as <see cref="ResolveAutoBestMainHand"/> — otherwise this
        /// would be a second, unguarded door onto the whole paid catalog.
        /// </summary>
        private static WeaponDef ResolveOwnedOneHandedRefill(string job, int level)
        {
            var owned = ResolveOwnedGear(job);
            string filtered = owned.Resolved ? "TRUE" : "FALSE";

            if (owned.Resolved)
            {
                var pick = GearCatalog.PickBestWeapon(job, level, owned.Owns, oneHandedOnly: true);
                if (pick.Weapon != null)
                {
                    FlowTrace.Step("Gear",
                        $"AutoBest(refill-1h): job='{job}' level={level} eligible={pick.Eligible} owned={pick.Owned} " +
                        $"ownershipFiltered=TRUE ownedSource=({owned.Source}) branch=owned-auto-best " +
                        $"weapon='{pick.Weapon.id}'");
                    return pick.Weapon;
                }
            }

            var kit = StarterLoadout.For(job);
            var starter = kit != null && !string.IsNullOrEmpty(kit.MainHand)
                ? GearCatalog.FindWeapon(kit.MainHand) : null;
            if (starter != null && starter.IsOneHandedMain && GearCatalog.WeaponFitsClass(starter, job))
            {
                FlowTrace.Step("Gear",
                    $"AutoBest(refill-1h): job='{job}' level={level} eligible=n/a owned=0 " +
                    $"ownershipFiltered={filtered} branch=starter-floor weapon='{starter.id}'");
                return starter;
            }

            var any = GearCatalog.BestOneHandedWeapon(job, level);
            FlowTrace.Warn("Gear",
                $"AutoBest(refill-1h): job='{job}' level={level} eligible=n/a owned=0 ownershipFiltered={filtered} " +
                $"branch=catalog-wide-floor weapon='{any?.id ?? "<null>"}' - no owned 1H and no authored 1H starter " +
                "for this class, so the pre-fix catalog-wide pick armed the hand (or, if null, EnforceHandSlots " +
                "reports the hero UNARMED on the next line).");
            return any;
        }

        // Restore a per-class persisted manual equip over the auto-best pick. Validated:
        // the id must still exist in the catalog AND still be legal for the class (weapon
        // job-match / armor weight-class), or the auto-best stands.
        //
        // RETURNS true when the MAIN HAND was replaced by a persisted value (either a real
        // weapon or the explicit "unequipped" sentinel) — Refresh reports that as the
        // main-hand's provenance so a capture names WHICH tier armed the hero (§12).
        private bool ApplyPersistedEquip(string job)
        {
            bool mainHandFromPrefs = false;
            string key = (job ?? string.Empty).ToLowerInvariant();   // case-safe key (hero vs companion)
            string wId = PlayerPrefs.GetString(PrefWeaponKey + key, null);
            if (wId == PrefNoneSentinel)
            {
                // WO-434: player explicitly unequipped — respect the empty choice (no auto-best).
                EquippedWeapon = null;
                mainHandFromPrefs = true;
                FlowTrace.Step("Gear",
                    $"ApplyPersistedEquip('{job}'): main hand restored from PlayerPrefs " +
                    $"'{PrefWeaponKey + key}' = the explicit UNEQUIP sentinel — hero stays bare-handed.");
            }
            else if (!string.IsNullOrEmpty(wId))
            {
                var w = GearCatalog.FindWeapon(wId);
                if (w != null && GearCatalog.WeaponFitsClass(w, job))
                {
                    EquippedWeapon = w;
                    mainHandFromPrefs = true;
                    FlowTrace.Step("Gear",
                        $"ApplyPersistedEquip('{job}'): main hand OVERRIDDEN by PlayerPrefs " +
                        $"'{PrefWeaponKey + key}' = '{wId}' — this is a PREVIOUS session's equip, " +
                        "not the authored starter.");
                }
                else
                {
                    // The stored id is stale/illegal: the auto-best pick stands. Loud, because
                    // the hero is now holding whatever has the highest damageMult rather than
                    // either the player's choice OR the authored starter.
                    FlowTrace.Warn("Gear",
                        $"ApplyPersistedEquip('{job}'): PlayerPrefs '{PrefWeaponKey + key}' = '{wId}' " +
                        $"is {(w == null ? "NOT in the catalog" : "not legal for this class")} — the " +
                        "persisted choice is DROPPED and the auto-best pick stands. Note the mere " +
                        "EXISTENCE of this key already suppressed the authored starter.");
                }
            }
            // OFF-HAND restore (must run BEFORE EnforceHandSlots so a persisted shield + a 2H
            // auto-best resolve to the shield-wins rule). Only restored when it's still a valid
            // off-hand item the class may carry. Absent key => leave whatever was set (none).
            string oId = PlayerPrefs.GetString(PrefOffHandKey + key, null);
            if (oId == PrefNoneSentinel)
            {
                EquippedOffHand = null;
            }
            else if (!string.IsNullOrEmpty(oId))
            {
                var o = GearCatalog.FindWeapon(oId);
                if (o != null && o.IsOffHandItem && GearCatalog.WeaponFitsClass(o, job)) EquippedOffHand = o;
            }

            string aId = PlayerPrefs.GetString(PrefArmorKey + key, null);
            if (aId == PrefNoneSentinel)
            {
                EquippedArmor = null;
            }
            else if (!string.IsNullOrEmpty(aId))
            {
                var a = GearCatalog.FindArmor(aId);
                if (a != null && GearCatalog.ArmorFitsClass(a, job)) EquippedArmor = a;
            }

            // WO-543: restore persisted accessories (rings/amulets are job "any" — slot match only).
            string rId = PlayerPrefs.GetString(PrefRingKey + key, null);
            if (rId == PrefNoneSentinel) EquippedRing = null;
            else if (!string.IsNullOrEmpty(rId))
            {
                var r = GearCatalog.FindAccessory(rId);
                if (r != null && r.IsRing) EquippedRing = r;
            }

            string mId = PlayerPrefs.GetString(PrefAmuletKey + key, null);
            if (mId == PrefNoneSentinel) EquippedAmulet = null;
            else if (!string.IsNullOrEmpty(mId))
            {
                var m = GearCatalog.FindAccessory(mId);
                if (m != null && m.IsAmulet) EquippedAmulet = m;
            }

            return mainHandFromPrefs;
        }

        // Lower-cased persistence key for the wearer's class — the hero (HeroAbilities) and a
        // companion (BindOwnerClass) can report different casing for the same class, so both
        // read/write the SAME PlayerPrefs slot.
        private string PrefJobKey() => CurrentJob().ToLowerInvariant();

        /// <summary>
        /// Recomputes WeaponMult + ArmorDefense from the equipped pieces, folding in the
        /// WO-295 Aegis full-set bonus (per-class weapon perk multiplier + bonus defense)
        /// when both an Aegis weapon and Aegis armor are equipped. Also (de)activates the
        /// Oathweld ward via the lazily-attached AegisSetEffect. Ordinary gear is unchanged.
        /// </summary>
        private void ApplyStats(string job)
        {
            // WO-808 Option A: the published scalars carry the owned instance's LEVEL via the
            // pure resolver (level 1 / unauthored band == the authored values exactly, so
            // pre-808 behaviour is byte-identical). This is the single choke point every
            // combat consumer reads — no other apply site exists.
            var gs = DeNelle.Core.State.GameStateService.Instance != null
                ? DeNelle.Core.State.GameStateService.Instance.State : null;
            float weapon = EquippedWeapon != null
                ? GearStatResolver.EffectiveDamageMult(EquippedWeapon, GearProgression.GearLevelOf(gs, EquippedWeapon.id))
                : 1f;
            float armor  = EquippedArmor != null
                ? GearStatResolver.EffectiveDefense(EquippedArmor, GearProgression.GearLevelOf(gs, EquippedArmor.id))
                : 0f;

            if (AegisSetActive)
            {
                weapon *= AegisWeaponPerkMult(job);
                armor   = Mathf.Clamp(armor + AegisSetDefenseBonus, 0f, MaxArmorDefense);
            }

            // WO-543: accessory (ring + amulet) bonuses stack ADDITIVELY on top of weapon + armor.
            //   • damage chain: weaponMult × (1 + ringDmg + amuletDmg)
            //   • defense:      armor.defense + ring.defense + amulet.defense + offHand.defense
            //                   (CAP MaxArmorDefense - never immune)
            //   • max HP:       armor.hpBonus + ring.hpBonus + amulet.hpBonus  (folded by HeroHealth)
            // Accessories carry NO gear level of their own (no rarity band is authored for them),
            // so they are read flat, exactly as before.
            float accDamage  = (EquippedRing != null ? EquippedRing.damageMult : 0f)
                             + (EquippedAmulet != null ? EquippedAmulet.damageMult : 0f);
            float accDefense = (EquippedRing != null ? EquippedRing.defense : 0f)
                             + (EquippedAmulet != null ? EquippedAmulet.defense : 0f);
            int accHp        = (EquippedRing != null ? EquippedRing.hpBonus : 0)
                             + (EquippedAmulet != null ? EquippedAmulet.hpBonus : 0);

            weapon *= (1f + Mathf.Max(0f, accDamage));

            // SHIELDS WERE INERT (owner 2026-08-02): every category "shield" row carried no
            // defense value AND the equipped off-hand was never summed here - decoration on both
            // halves, so a Squire's Heater did exactly nothing. The off-hand now contributes
            // exactly like armor: additive, floor-guarded (a negative authored value can never
            // HEAL through the mitigation formula), and under the SAME ceiling, so a shield can
            // never make the hero immune.
            //
            // AND IT IS LEVEL-SCALED, through the SAME GearStatResolver the weapon (line ~393)
            // and the armor (line ~396) already go through - NOT a raw `.defense` read.
            //
            // THE BUG THIS CLOSES (owner-called Tier 0, 2026-08-02): shields live in weapons.json,
            // so GearProgression.Improve ACCEPTS them - it charges real wood + iron off the
            // ResourceLedger, writes GameState.GearLevels[shieldId] and reports "improved to Lv 5"
            // in the UI. But this line read EquippedOffHand.defense RAW, so the level it just sold
            // you never reached ArmorDefense, which is the one scalar HeroHealth.TakeDamage
            // consumes. An epic shield taken to L5 cost thousands of resources and delivered
            // EXACTLY ZERO mitigation - silent, repeatable resource theft, with a congratulatory
            // toast on top. Routing it through the resolver is the whole fix; there is deliberately
            // no second resolver for off-hands, because two resolvers is how this drifts again.
            float offHandDefense = EquippedOffHand != null
                ? GearStatResolver.EffectiveDefense(EquippedOffHand, GearProgression.GearLevelOf(gs, EquippedOffHand.id))
                : 0f;

            WeaponMult   = weapon;
            // OWNER-LOCKED CEILING (2026-08-02): MaxArmorDefense, the SAME symbol every display
            // site clamps to. This clamp used to read 0.70 while the shop showed 0.90 - see the
            // MaxArmorDefense doc comment. Do not re-inline the number.
            ArmorDefense = Mathf.Clamp(armor + Mathf.Max(0f, accDefense) + Mathf.Max(0f, offHandDefense),
                                       0f, MaxArmorDefense);
            GearHpBonus  = Mathf.Max(0, Mathf.RoundToInt(EquippedArmor != null ? EquippedArmor.hpBonus : 0f) + accHp);

            // Drive the body's armor visual tier off the equipped piece. EquipmentController
            // owns the asset-attach layer (the canonical instance); GearLoadout simply tells
            // it WHICH tier is worn. NOTE: SetArmorTier is presently a recorded NO-OP on the
            // visual side (armor body art not yet authored) — this wire makes the tier flow
            // so the body-attach lights up automatically the moment that art lands, with no
            // further plumbing. Weapon mesh attach is already driven via OnGearChanged.
            PushArmorTierToBody();

            // Activate / refresh the Oathweld ward driver (lazily attached, self-guards).
            EnsureSetEffect().Refresh();

            // WO-543: drive the rarity rim-light glow off the dominant equipped rarity
            // (armor/ring/amulet). Lazily attached, fully guarded (no-op without a hero mesh).
            EnsureRimLight().Refresh();

            // WO-888: the item-granted persistent AURAS (elemental weapon smoulder + relic
            // restoration column). Lazily attached, mirrors EnsureRimLight exactly. This is the
            // reliable driver: WO-888 named GearVisualApplier.Apply as the seam, but that method
            // returns early unless the retired primitive-gear switch is on, and HeroBodySwapper
            // skips it entirely on the package / KnightV3 body paths - so the aura is also
            // ensured + refreshed HERE, on the equip path every slot change already runs through.
            EnsureGearAura().Refresh();

            // COMPANION gear: a companion body has no HeroAbilities damage chain, so push the
            // equipped weapon multiplier straight onto its StoryCompanion driver (no-op on the
            // player hero, which has no StoryCompanion). Its attacks then scale with gear.
            var companion = GetComponent<StoryCompanion>();
            if (companion != null) companion.SetGearWeaponMult(WeaponMult);
        }

        /// <summary>
        /// Re-publish the combat scalars from the CURRENT equipment without changing it —
        /// WO-808: called after a gear-level Improve so the new level is felt immediately
        /// (WeaponMult/ArmorDefense re-resolve through GearStatResolver at the new level).
        /// </summary>
        public void RefreshStats() => ApplyStats(CurrentJob());

        private EquipmentController _equipment;

        // Map the equipped armor to a coarse visual tier (0 = none) and hand it to the
        // EquipmentController. Tier is derived from the existing rarity ladder so it needs
        // no new data: common=1 … legendary=5. Graceful: no armor or no controller = tier 0
        // / no-op (combat + existing visuals unchanged).
        private void PushArmorTierToBody()
        {
            if (_equipment == null) _equipment = GetComponent<EquipmentController>();
            if (_equipment == null) return;   // controller not attached on this hero — skip
            _equipment.SetArmorTier(ArmorVisualTier(EquippedArmor));
        }

        /// <summary>Maps an armor piece to its coarse visual tier (0 none … 5 legendary) off the
        /// rarity ladder. Public so the Gear Preview (EquipmentPanel) drives the SAME tint tier the
        /// world hero uses — ONE mapping, no divergence.</summary>
        public static int ArmorVisualTier(ArmorDef a)
        {
            if (a == null) return 0;
            switch ((a.rarity ?? "common").ToLowerInvariant())
            {
                case "uncommon":  return 2;
                case "rare":      return 3;
                case "epic":      return 4;
                case "legendary": return 5;
                default:          return 1;   // common starter (e.g. Wanderer's Cloth)
            }
        }

        private AegisSetEffect _setEffect;

        /// <summary>Lazily attaches the Oathweld-ward driver so every hero gets it with no builder change.</summary>
        private AegisSetEffect EnsureSetEffect()
        {
            if (_setEffect == null)
            {
                if (!TryGetComponent(out _setEffect)) _setEffect = gameObject.AddComponent<AegisSetEffect>();
            }
            return _setEffect;
        }

        private HeroArmorRimLight _rimLight;

        /// <summary>Lazily attaches the WO-543 armor/accessory rim-light applier (mirrors EnsureSetEffect).</summary>
        private HeroArmorRimLight EnsureRimLight()
        {
            if (_rimLight == null)
            {
                if (!TryGetComponent(out _rimLight)) _rimLight = gameObject.AddComponent<HeroArmorRimLight>();
            }
            return _rimLight;
        }

        private GearAura _gearAura;

        /// <summary>Lazily attaches the WO-888 item-aura holder (mirrors EnsureRimLight). It owns
        /// its own loop handles and stops them on unequip / disable / destroy / scene unload.</summary>
        private GearAura EnsureGearAura()
        {
            if (_gearAura == null)
            {
                if (!TryGetComponent(out _gearAura)) _gearAura = gameObject.AddComponent<GearAura>();
            }
            return _gearAura;
        }

        /// <summary>
        /// WO-543: manually equip a ring/amulet accessory. Routes by the def's slot, persists per
        /// class so it sticks across loads, recomputes stats (the rim-light + bonuses re-apply via
        /// ApplyStats), and fires OnGearChanged. No-op (with a Warn) when the id isn't an accessory.
        /// </summary>
        public void EquipAccessoryById(string id)
        {
            var ac = GearCatalog.FindAccessory(id);
            if (ac == null)
            {
                FlowTrace.Warn("Gear", $"EquipAccessoryById('{id}') — no AccessoryDef in catalog; equip skipped.");
                return;
            }

            if (ac.IsRing)
            {
                EquippedRing = ac;
                PlayerPrefs.SetString(PrefRingKey + PrefJobKey(), id);
            }
            else if (ac.IsAmulet)
            {
                EquippedAmulet = ac;
                PlayerPrefs.SetString(PrefAmuletKey + PrefJobKey(), id);
            }
            else
            {
                FlowTrace.Warn("Gear", $"EquipAccessoryById('{id}') — slot '{ac.slot}' is not ring/amulet; equip skipped.");
                return;
            }

            PlayerPrefs.Save();
            ApplyStats(CurrentJob());
            OnGearChanged?.Invoke();
            FlowTrace.Step("Gear", $"EquipAccessoryById('{id}') applied — ring='{EquippedRing?.id ?? "<null>"}' " +
                $"amulet='{EquippedAmulet?.id ?? "<null>"}' hpBonus={GearHpBonus}");
        }

        /// <summary>
        /// WO-543: remove the accessory in <paramref name="slot"/> ("ring"/"amulet"). Persists the
        /// "none" sentinel so a later Refresh honours the empty choice, recomputes stats, fires OnGearChanged.
        /// </summary>
        public void UnequipAccessory(string slot)
        {
            string s = (slot ?? string.Empty).Trim().ToLowerInvariant();
            if (s == "ring")
            {
                EquippedRing = null;
                PlayerPrefs.SetString(PrefRingKey + PrefJobKey(), PrefNoneSentinel);
            }
            else if (s == "amulet")
            {
                EquippedAmulet = null;
                PlayerPrefs.SetString(PrefAmuletKey + PrefJobKey(), PrefNoneSentinel);
            }
            else
            {
                FlowTrace.Warn("Gear", $"UnequipAccessory('{slot}') — unknown accessory slot; skipped.");
                return;
            }

            PlayerPrefs.Save();
            ApplyStats(CurrentJob());
            OnGearChanged?.Invoke();
            FlowTrace.Step("Gear", $"UnequipAccessory('{s}') applied — ring='{EquippedRing?.id ?? "<null>"}' " +
                $"amulet='{EquippedAmulet?.id ?? "<null>"}'");
        }

        // ── HAND-SLOT ENFORCEMENT (docs/STORE_EQUIP_SPEC.md "Equip-slot rules") ──────
        // The single point that keeps the main-hand / off-hand pair legal after ANY equip.
        // Called by every path that sets EquippedWeapon or EquippedOffHand (manual equip,
        // persisted-restore, auto-best Refresh). Each enforcement is FlowTrace.Step'd — no
        // silent state change (§12). Returns nothing; mutates EquippedWeapon/EquippedOffHand.
        //
        // Rules:
        //   • main-hand is 2H  -> off-hand MUST be empty (2H takes both hands).
        //   • off-hand present + main-hand is 2H -> the off-hand WINS the off slot, the 2H is
        //     removed, and the main hand falls back to the best 1H for the class (or empty —
        //     but never left unarmed when a 1H starter exists; armed-hero guard).
        //   • 1H main + off-hand -> both kept (the allowed combo).
        // A shield can never sit in the MAIN slot (IsOffHandItem) — guarded here too.
        private void EnforceHandSlots(string job, int level)
        {
            // A shield/off-hand item must never occupy the MAIN hand — move it to the off slot.
            if (EquippedWeapon != null && EquippedWeapon.IsOffHandItem)
            {
                FlowTrace.Step("Gear", $"EnforceHandSlots: '{EquippedWeapon.id}' is an off-hand item in the main slot -> moved to off-hand.");
                if (EquippedOffHand == null) EquippedOffHand = EquippedWeapon;
                EquippedWeapon = null;

                // ARMED-HERO INVARIANT: evicting a shield must not LEAVE the main hand empty.
                // The 2H branch below already refills; this branch did not, so a class whose
                // best pick resolved to a shield spawned with nothing in the main hand at all
                // (the level-1 Mage, 2026-08-02). Refill from the best 1H the class can hold.
                // OWNED-gated (2026-08-08): a catalog-wide refill here would be a second door
                // onto the paid Forge shelf. ResolveOwnedOneHandedRefill keeps the same
                // never-weaponless floor, so this branch is no more likely to leave a null hand.
                var refill = ResolveOwnedOneHandedRefill(job, level);
                if (refill != null)
                {
                    EquippedWeapon = refill;
                    FlowTrace.Step("Gear", $"EnforceHandSlots: main-hand refilled with 1H '{refill.id}' after the shield moved off.");
                }
                else
                {
                    FlowTrace.Warn("Gear", $"EnforceHandSlots: no 1H main-hand exists for class '{job}' at level {level} — hero is UNARMED (off-hand retained).");
                }
            }

            bool mainIs2H = EquippedWeapon != null && EquippedWeapon.IsTwoHanded;
            bool haveOff  = EquippedOffHand != null;

            if (mainIs2H && haveOff)
            {
                // Both can't hold: the most RECENT intent decides. We resolve by removing the 2H
                // (the off-hand was the thing just added in the shield-while-2H path, and the 2H
                // path itself clears the off-hand BEFORE calling this). Fall the main hand back to
                // a 1H so the hero is never unarmed.
                FlowTrace.Step("Gear", $"EnforceHandSlots: 2H main '{EquippedWeapon.id}' conflicts with off-hand '{EquippedOffHand.id}' -> 2H removed, main falls back to a 1H.");
                var fallback = ResolveOwnedOneHandedRefill(job, level);   // OWNED-gated; same floor
                EquippedWeapon = fallback;   // may be null only if no 1H exists for this class AT ALL
                if (fallback != null)
                    FlowTrace.Step("Gear", $"EnforceHandSlots: main-hand fell back to 1H '{fallback.id}'.");
                else
                    FlowTrace.Warn("Gear", "EnforceHandSlots: no 1H fallback for class — main hand left empty (off-hand retained).");
            }
            else if (mainIs2H && !haveOff)
            {
                // Healthy 2H state — nothing to do, off-hand already empty.
            }
        }

        // Apply a NEW main-hand weapon and enforce the slot rules. A 2H clears the off-hand
        // FIRST (it takes both hands); a 1H keeps any existing off-hand. Centralised so manual
        // equip, persisted-restore and Refresh share one rule set.
        private void SetMainHand(WeaponDef w, string job, int level)
        {
            if (w != null && w.IsOffHandItem)
            {
                // A shield passed to the main slot routes to the off slot instead.
                SetOffHand(w, job, level);
                return;
            }
            EquippedWeapon = w;
            if (w != null && w.IsTwoHanded && EquippedOffHand != null)
            {
                FlowTrace.Step("Gear", $"equip 2H '{w.id}' -> off-hand '{EquippedOffHand.id}' cleared (2H takes both hands).");
                EquippedOffHand = null;
            }
            EnforceHandSlots(job, level);
        }

        // Apply a NEW off-hand/shield and enforce the slot rules. If the current main hand is 2H,
        // the off-hand wins and the 2H is removed (main falls back to a 1H via EnforceHandSlots).
        private void SetOffHand(WeaponDef offHand, string job, int level)
        {
            EquippedOffHand = offHand;
            if (offHand != null && EquippedWeapon != null && EquippedWeapon.IsTwoHanded)
            {
                FlowTrace.Step("Gear", $"equip off-hand '{offHand.id}' while 2H '{EquippedWeapon.id}' held -> 2H removed (falls back to a 1H).");
            }
            EnforceHandSlots(job, level);
        }

        /// <summary>
        /// Manual equip support for shop / EquipmentPanel flows. Forces a specific piece
        /// the player has purchased (or crafted). Updates stats immediately and triggers
        /// visuals re-apply (sword on hand, bow on back, armor accents, knight shield).
        /// A shield/off-hand id routes to the off-hand slot automatically (and clears a 2H).
        /// </summary>
        public void EquipWeaponById(string id)
        {
            var w = GearCatalog.FindWeapon(id);
            if (w == null)
            {
                // Equip is a no-op when the id isn't in the catalog — surface it so a
                // playtest doesn't silently think gear took when the def is missing.
                FlowTrace.Warn("Gear", $"EquipWeaponById('{id}') — no WeaponDef in catalog; equip skipped.");
                return;
            }

            // A shield/off-hand goes to the off slot (and persists there).
            if (w.IsOffHandItem)
            {
                EquipOffHandById(id);
                return;
            }

            int level = _progression != null ? _progression.Level : 1;
            SetMainHand(w, CurrentJob(), level);
            PlayerPrefs.SetString(PrefWeaponKey + PrefJobKey(), id);   // persist per class
            // A 2H equip removed the off-hand — persist the empty off slot so it doesn't restore.
            if (w.IsTwoHanded) PlayerPrefs.SetString(PrefOffHandKey + PrefJobKey(), PrefNoneSentinel);
            PlayerPrefs.Save();
            ApplyStats(CurrentJob());          // recomputes WeaponMult from EquippedWeapon.damageMult
            OnGearChanged?.Invoke();
            TryReapplyVisuals();
            // VERIFY equip-applies-stats: WeaponMult is now live in the damage chain
            // (HeroAbilities.cs:365 + PlayerAttackController.cs:408 both read it). This
            // Step lets a playtest confirm a MANUAL equip took (Refresh's auto-equip Step
            // at line ~120 only covers the level-up auto path, not shop/EquipmentPanel).
            FlowTrace.Step("Gear", $"EquipWeaponById('{id}') applied — WeaponMult={WeaponMult:0.00}");
        }

        /// <summary>
        /// Manually equip a shield / off-hand item into the OFF hand. If the current main hand is a
        /// 2H weapon it is REMOVED (a 2H takes both hands) and the main hand falls back to the best
        /// 1H for the class (never left unarmed when a 1H starter exists). Persists per class so the
        /// off-hand sticks across loads. No-op (with a Warn) when the id isn't in the catalog or
        /// isn't an off-hand item.
        /// </summary>
        public void EquipOffHandById(string id)
        {
            var w = GearCatalog.FindWeapon(id);
            if (w == null)
            {
                FlowTrace.Warn("Gear", $"EquipOffHandById('{id}') — no WeaponDef in catalog; equip skipped.");
                return;
            }
            if (!w.IsOffHandItem)
            {
                FlowTrace.Warn("Gear", $"EquipOffHandById('{id}') — '{id}' is not an off-hand/shield item; equip skipped.");
                return;
            }

            int level = _progression != null ? _progression.Level : 1;
            bool clearedTwoHander = EquippedWeapon != null && EquippedWeapon.IsTwoHanded;
            SetOffHand(w, CurrentJob(), level);

            PlayerPrefs.SetString(PrefOffHandKey + PrefJobKey(), id);   // persist the off-hand per class
            // If a 2H was just removed, persist the new main-hand choice (the 1H fallback or "none")
            // so a later Refresh honours the swap instead of restoring the 2H.
            if (clearedTwoHander)
            {
                PlayerPrefs.SetString(PrefWeaponKey + PrefJobKey(),
                    EquippedWeapon != null ? EquippedWeapon.id : PrefNoneSentinel);
            }
            PlayerPrefs.Save();
            ApplyStats(CurrentJob());
            OnGearChanged?.Invoke();
            TryReapplyVisuals();
            FlowTrace.Step("Gear", $"EquipOffHandById('{id}') applied — off='{EquippedOffHand?.id ?? "<null>"}' main='{EquippedWeapon?.id ?? "<null>"}'");
        }

        /// <summary>
        /// Manually remove the equipped off-hand / shield. Clears the off slot, persists the "none"
        /// sentinel so a later Refresh doesn't auto-restore it, recomputes + re-applies visuals.
        /// The main hand is untouched.
        /// </summary>
        public void UnequipOffHand()
        {
            EquippedOffHand = null;
            PlayerPrefs.SetString(PrefOffHandKey + PrefJobKey(), PrefNoneSentinel);
            PlayerPrefs.Save();
            ApplyStats(CurrentJob());
            OnGearChanged?.Invoke();
            TryReapplyVisuals();
            FlowTrace.Step("Gear", "UnequipOffHand applied — off-hand cleared.");
        }

        public void EquipArmorById(string id)
        {
            var a = GearCatalog.FindArmor(id);
            if (a == null)
            {
                FlowTrace.Warn("Gear", $"EquipArmorById('{id}') — no ArmorDef in catalog; equip skipped.");
                return;
            }
            EquippedArmor = a;
            PlayerPrefs.SetString(PrefArmorKey + PrefJobKey(), id);   // persist per class
            PlayerPrefs.Save();
            ApplyStats(CurrentJob());          // recomputes ArmorDefense from EquippedArmor.defense
            OnGearChanged?.Invoke();
            TryReapplyVisuals();
            // VERIFY equip-applies-stats: ArmorDefense is now live in HeroHealth's
            // mitigation (HeroHealth.cs reads GearLoadout). Step confirms a manual equip took.
            FlowTrace.Step("Gear", $"EquipArmorById('{id}') applied — ArmorDefense={ArmorDefense:0.00}");
        }

        /// <summary>
        /// WO-434: manually remove the equipped weapon. Clears the slot, recomputes stats,
        /// fires OnGearChanged, and persists a "none" sentinel under the wearer's class so a
        /// later Refresh()/level-up does NOT silently auto-re-equip the best piece the player
        /// just took off. Additive — existing equip callers are unaffected.
        /// </summary>
        public void UnequipWeapon()
        {
            EquippedWeapon = null;
            PlayerPrefs.SetString(PrefWeaponKey + PrefJobKey(), PrefNoneSentinel);   // persist the empty choice
            PlayerPrefs.Save();
            ApplyStats(CurrentJob());          // WeaponMult falls back to 1.0
            OnGearChanged?.Invoke();
            TryReapplyVisuals();
            FlowTrace.Step("Gear", $"UnequipWeapon applied — WeaponMult={WeaponMult:0.00}");
        }

        /// <summary>
        /// WO-434: manually remove the equipped armor. Mirrors <see cref="UnequipWeapon"/> —
        /// clears the slot, recomputes stats (ArmorDefense → 0), fires OnGearChanged, and
        /// persists the "none" sentinel so Refresh honours the empty choice.
        /// </summary>
        public void UnequipArmor()
        {
            EquippedArmor = null;
            PlayerPrefs.SetString(PrefArmorKey + PrefJobKey(), PrefNoneSentinel);   // persist the empty choice
            PlayerPrefs.Save();
            ApplyStats(CurrentJob());          // ArmorDefense falls back to 0
            OnGearChanged?.Invoke();
            TryReapplyVisuals();
            FlowTrace.Step("Gear", $"UnequipArmor applied — ArmorDefense={ArmorDefense:0.00}");
        }

        /// <summary>The wearer's current class id (for catalog queries, persistence keys, and
        /// the Aegis per-class weapon perk). Precedence:
        ///   1. a companion loadout's BindOwnerClass override (authoritative for that body);
        ///   2. the hero's live HeroAbilities class;
        ///   3. the PERSISTED player class from GameState - the SAME source
        ///      HeroBodySwapper.ResolveHeroClass trusts to build the BODY;
        ///   4. AbilityCatalog.DefaultClass, and only with a FlowTrace.Warn naming the object.
        ///
        /// WHY STEP 3 EXISTS (F8 seq-642, PROVEN from Player.log, not inferred). A composed
        /// dungeon hero carries NO HeroAbilities: DungeonBaker.PopulateForPlay attaches only
        /// HeroLocomotion + HeroBodySwapper, and HeroControlEnsurer's emergency wiring is gated
        /// by IsVillageScene, which every dg_* scene fails. So step 2 was null and the old code
        /// fell straight to AbilityCatalog.DefaultClass - which is the literal string "mage"
        /// (AbilityCatalog.cs:207, pinned by Assets/Data/Tests/AbilityCatalogTest.cs:52). The
        /// capture reads, one line after BuildKnightV3Body:
        ///     [Flow:Gear] Refresh: job='mage' level=1 bestWeapon='mage_oak' offHand='&lt;null&gt;'
        ///                 bestArmor='armor_cloth'
        /// i.e. HeroBodySwapper built the KNIGHT body while this component armed him as a MAGE:
        /// an Oakheart STAFF and cloth robes ("in starter loop i get a staff?").
        ///
        /// AND IT CORRUPTED A SAVE SLOT THE PLAYER NEVER PLAYED: PrefJobKey() routes through
        /// here, so every persisted equip in that dungeon was written under `...-mage` - the
        /// same capture shows EquipOffHandById('knight_shield_starter') landing in the MAGE's
        /// off-hand slot - while the knight's own persisted sword was never read back.
        ///
        /// A SILENT wrong-class default is exactly the no-silent-failure violation CLAUDE.md
        /// section 12 forbids: the wrong class flowed through catalog queries, persistence keys
        /// and the damage chain for an entire dungeon run without emitting one line.
        /// </summary>
        private string CurrentJob()
        {
            if (!string.IsNullOrEmpty(_ownerClassOverride)) return _ownerClassOverride;
            if (_abilities == null) _abilities = GetComponent<HeroAbilities>();
            if (_abilities != null && !string.IsNullOrEmpty(_abilities.HeroClass)) return _abilities.HeroClass;

            string persisted = PersistedPlayerJob();
            if (!string.IsNullOrEmpty(persisted))
            {
                // Not a Warn: this is the CORRECT answer for any hero built without a
                // HeroAbilities component (every composed dungeon). Once-per-key so a
                // dungeon run logs it exactly once instead of on every equip/ApplyStats.
                FlowTrace.Once("Gear", "job-from-gamestate-" + persisted,
                    $"CurrentJob: no HeroAbilities on '{gameObject.name}' - resolved class '{persisted}' " +
                    "from the PERSISTED GameState.HeroClass (the same source HeroBodySwapper builds " +
                    "the body from), NOT the catalog default.");
                return persisted;
            }

            FlowTrace.Warn("Gear",
                $"CurrentJob: '{gameObject.name}' has NO HeroAbilities, no BindOwnerClass override AND no " +
                $"persisted GameState.HeroClass - falling back to AbilityCatalog.DefaultClass " +
                $"('{AbilityCatalog.DefaultClass}'). Gear, the per-class PlayerPrefs slots and the damage " +
                "chain will all key off that class; if this wearer is not a " + AbilityCatalog.DefaultClass +
                " the equipped gear AND the save slot are WRONG. Fix the SOURCE (persist the hero class, " +
                "or BindOwnerClass on a companion) - do not treat this line as normal.");
            return AbilityCatalog.DefaultClass;
        }

        /// <summary>
        /// The lowercase job key for the PERSISTED player class, or null when no class has been
        /// chosen / no save service is up. Reads GameStateService.State.HeroClass - byte-identical
        /// to HeroBodySwapper.ResolveHeroClass's source - and maps it through
        /// DeNelle.Core.State.PlayableHeroes.JobKey, the registry documented as "the SAME key
        /// weapons.json `job`, armor weight-class lookup and the per-class PlayerPrefs slots use".
        /// HeroBodySwapper already picks the STARTER kit through that exact accessor
        /// (PlayableHeroes.JobKey(cls)), so the kit and this loadout can never key off different
        /// strings.
        ///
        /// FULLY QUALIFIED ON PURPOSE (including the extension method, called statically): the
        /// file header forbids importing DeNelle.Core.State here, because that namespace's
        /// HeroClass type would shadow the DeNelle.Village names used throughout this file.
        ///
        /// CLERIC NOTE: JobKey maps Cleric -> "cleric", while HeroAbilities aliases her ability
        /// loadout to "mage". "cleric" is the correct GEAR key (GearCatalog.ClassWeight and the
        /// Aegis perk table both have real "cleric" rows; keying her as "mage" would gate her to
        /// LIGHT armor). She is not in PlayableHeroes.Roster, so this is inert today.
        /// </summary>
        private static string PersistedPlayerJob()
        {
            var svc = DeNelle.Core.State.GameStateService.Instance;
            if (svc == null || svc.State == null) return null;
            var opt = DeNelle.Core.State.HeroClassOptExtensions.ToNullable(svc.State.HeroClass);
            return opt.HasValue ? DeNelle.Core.State.PlayableHeroes.JobKey(opt.Value) : null;
        }

        private void TryReapplyVisuals()
        {
            var body = transform.Find("HeroBody");
            if (body != null)
                GearVisualApplier.Apply(body, this);
        }
    }
}
