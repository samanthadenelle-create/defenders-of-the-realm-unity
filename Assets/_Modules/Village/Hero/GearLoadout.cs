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

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class GearLoadout : MonoBehaviour
    {
        /// <summary>Outgoing-damage multiplier from the equipped weapon (1.0 = none).</summary>
        public float WeaponMult { get; private set; } = 1f;

        /// <summary>Fractional incoming-damage reduction from equipped armor (0 = none).</summary>
        public float ArmorDefense { get; private set; } = 0f;

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
        private const string PrefWeaponKey  = "dotr-equip-weapon-";   // + <class>  (main hand)
        private const string PrefArmorKey   = "dotr-equip-armor-";    // + <class>
        private const string PrefOffHandKey = "dotr-equip-offhand-";  // + <class>  (off hand / shield)
        private const string PrefRingKey    = "dotr-equip-ring-";     // + <class>  (WO-543 accessory)
        private const string PrefAmuletKey  = "dotr-equip-amulet-";   // + <class>  (WO-543 accessory)

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

            EquippedWeapon = GearCatalog.BestWeapon(job, level);
            EquippedArmor  = GearCatalog.BestArmor(job, level);

            // A PERSISTED manual choice (from the equip UI) wins over auto-best, so gear
            // assigned to this member sticks across loads. Only applied when it still fits
            // the class (a light-armor wearer never restores a heavy piece).
            ApplyPersistedEquip(job);

            // Resolve the main-hand / off-hand pair to a legal state after the picks above
            // (auto-best may pick a 2H while a persisted off-hand restored, etc.). Mutually
            // exclusive 2H↔off-hand enforced here, with the 1H fallback / armed-hero guard.
            EnforceHandSlots(job, level);

            // WO-425 one-shot diagnostic: definitively shows null-weapon (#1 data) vs has-weapon
            // (#2 missing mesh art) on the next playtest. Refresh is NOT hot (OnEnable + OnLevelUp
            // only), so a single Step per call is correct — no Once/Throttle guard needed.
            FlowTrace.Step("Gear", $"Refresh: job='{job}' level={level} " +
                $"bestWeapon='{EquippedWeapon?.id ?? "<null>"}' offHand='{EquippedOffHand?.id ?? "<null>"}' " +
                $"bestArmor='{EquippedArmor?.id ?? "<null>"}'");

            ApplyStats(job);
            OnGearChanged?.Invoke();
        }

        // Restore a per-class persisted manual equip over the auto-best pick. Validated:
        // the id must still exist in the catalog AND still be legal for the class (weapon
        // job-match / armor weight-class), or the auto-best stands.
        private void ApplyPersistedEquip(string job)
        {
            string key = (job ?? string.Empty).ToLowerInvariant();   // case-safe key (hero vs companion)
            string wId = PlayerPrefs.GetString(PrefWeaponKey + key, null);
            if (wId == PrefNoneSentinel)
            {
                // WO-434: player explicitly unequipped — respect the empty choice (no auto-best).
                EquippedWeapon = null;
            }
            else if (!string.IsNullOrEmpty(wId))
            {
                var w = GearCatalog.FindWeapon(wId);
                if (w != null && GearCatalog.WeaponFitsClass(w, job)) EquippedWeapon = w;
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
            float weapon = EquippedWeapon != null ? Mathf.Max(0.1f, EquippedWeapon.damageMult) : 1f;
            float armor  = EquippedArmor  != null ? Mathf.Clamp(EquippedArmor.defense, 0f, 0.9f) : 0f;

            if (AegisSetActive)
            {
                weapon *= AegisWeaponPerkMult(job);
                armor   = Mathf.Clamp(armor + AegisSetDefenseBonus, 0f, 0.9f);
            }

            // WO-543: accessory (ring + amulet) bonuses stack ADDITIVELY on top of weapon + armor.
            //   • damage chain: weaponMult × (1 + ringDmg + amuletDmg)
            //   • defense:      armor.defense + ring.defense + amulet.defense  (CAP 0.70 — never immune)
            //   • max HP:       armor.hpBonus + ring.hpBonus + amulet.hpBonus  (folded by HeroHealth)
            float accDamage  = (EquippedRing != null ? EquippedRing.damageMult : 0f)
                             + (EquippedAmulet != null ? EquippedAmulet.damageMult : 0f);
            float accDefense = (EquippedRing != null ? EquippedRing.defense : 0f)
                             + (EquippedAmulet != null ? EquippedAmulet.defense : 0f);
            int accHp        = (EquippedRing != null ? EquippedRing.hpBonus : 0)
                             + (EquippedAmulet != null ? EquippedAmulet.hpBonus : 0);

            weapon *= (1f + Mathf.Max(0f, accDamage));

            WeaponMult   = weapon;
            ArmorDefense = Mathf.Clamp(armor + Mathf.Max(0f, accDefense), 0f, 0.70f);
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

            // COMPANION gear: a companion body has no HeroAbilities damage chain, so push the
            // equipped weapon multiplier straight onto its StoryCompanion driver (no-op on the
            // player hero, which has no StoryCompanion). Its attacks then scale with gear.
            var companion = GetComponent<StoryCompanion>();
            if (companion != null) companion.SetGearWeaponMult(WeaponMult);
        }

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

        private static int ArmorVisualTier(ArmorDef a)
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
                var fallback = GearCatalog.BestOneHandedWeapon(job, level);
                EquippedWeapon = fallback;   // may be null if no 1H exists for this class
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
        /// the Aegis per-class weapon perk). A companion loadout's BindOwnerClass override wins;
        /// otherwise the hero's HeroAbilities class.</summary>
        private string CurrentJob()
        {
            if (!string.IsNullOrEmpty(_ownerClassOverride)) return _ownerClassOverride;
            if (_abilities == null) _abilities = GetComponent<HeroAbilities>();
            return _abilities != null ? _abilities.HeroClass : AbilityCatalog.DefaultClass;
        }

        private void TryReapplyVisuals()
        {
            var body = transform.Find("HeroBody");
            if (body != null)
                GearVisualApplier.Apply(body, this);
        }
    }
}
