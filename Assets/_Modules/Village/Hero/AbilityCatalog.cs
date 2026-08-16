// =============================================================================
// AbilityCatalog — typed model + loader for the canonical abilities.json.
// -----------------------------------------------------------------------------
// Port spec Part 3 row: src/content/abilities + the React MAGE_ABILITIES table
// (src/modules/village/hero/abilities/mage.ts) -> abilities.json + AbilityDef.
//
// The four hero abilities are CONTENT, not code: AbilityCatalog reads
// Assets/StreamingAssets/Data/Canonical/abilities.json at load time and
// hydrates typed AbilityDef records. Mirrors the PackCatalog.cs pattern exactly
// (canonical JSON under StreamingAssets, read via Application.streamingAssetsPath,
// parsed by Newtonsoft.Json — see unity-decisions.md, 2026-05-18 / 05-22).
//
// v2 foundation reads the "mage" class only (Blaise); Knight / Ranger ship in
// v2.1 (port spec Part 3). HeroAbilities.cs reads AbilityDefs from here; it
// never types cooldowns / damage / mana inline (port spec Part 4 — game tuning
// flows from JSON).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// The four hero ability slots. Q is the primary; W/E/R the cooldown kit.
    /// Mirrors <c>AbilityKind</c> in the React village types.
    /// </summary>
    public enum AbilitySlot
    {
        /// <summary>Primary — low cooldown, no mana (Arcane Bolt).</summary>
        Q = 0,
        /// <summary>Crowd-control (Frost Nova).</summary>
        W = 1,
        /// <summary>Utility / support (Healing Beacon).</summary>
        E = 2,
        /// <summary>Ultimate (Meteor Strike).</summary>
        R = 3,
    }

    /// <summary>
    /// The gameplay shape an ability resolves to — verbatim port of the React
    /// <c>AbilityEffect</c> union (village/types.ts).
    /// </summary>
    public enum AbilityEffect
    {
        /// <summary>Single-target hit on the nearest enemy in range.</summary>
        Strike = 0,
        /// <summary>Single-target hit + a slow.</summary>
        Snare = 1,
        /// <summary>Area blast centred on the caster.</summary>
        Aoe = 2,
        /// <summary>Area blast centred on the caster (alias of aoe — heavier swing).</summary>
        Cleave = 3,
        /// <summary>Heals the Heart — no enemy damage.</summary>
        Heal = 4,
        /// <summary>Area blast centred on the nearest enemy cluster.</summary>
        Meteor = 5,
    }

    /// <summary>
    /// One hero ability's static definition — the C# port of React's
    /// <c>AbilityDef</c> (village/types.ts). Hydrated from abilities.json; never
    /// constructed inline by game code.
    /// </summary>
    [Serializable]
    public sealed class AbilityDef
    {
        /// <summary>Which Q/W/E/R slot this ability fills.</summary>
        [JsonProperty("slot")] public string Slot;
        /// <summary>The on-screen hotkey label (e.g. "Q", "F").</summary>
        [JsonProperty("key")] public string Key;
        /// <summary>
        /// Stable unique id for the ability (e.g. "knight.shield-bash"). Optional —
        /// the per-class Q/W/E/R defs need no id; skill-tree abilities carry one so a
        /// HeroLoadout can equip them by id via <see cref="AbilityCatalog.FindById"/>.
        /// </summary>
        [JsonProperty("id")] public string Id;
        /// <summary>Canon ability name — verbatim, never paraphrased.</summary>
        [JsonProperty("name")] public string Name;
        /// <summary>HUD glyph.</summary>
        [JsonProperty("icon")] public string Icon;
        /// <summary>Accent / VFX colour as a hex string (e.g. "#b388ff").</summary>
        [JsonProperty("color")] public string Color;
        /// <summary>The gameplay effect — strike / snare / aoe / cleave / heal / meteor.</summary>
        [JsonProperty("effect")] public string Effect;
        /// <summary>Seconds before the ability can fire again.</summary>
        [JsonProperty("cooldown")] public float Cooldown;
        /// <summary>Mana spent on cast.</summary>
        [JsonProperty("manaCost")] public float ManaCost;
        /// <summary>Damage (heal: the heal amount) on the 0–100 HP scale.</summary>
        [JsonProperty("damage")] public float Damage;
        /// <summary>strike/snare: acquire range. aoe/cleave/meteor: blast radius. heal: ring radius.</summary>
        [JsonProperty("range")] public float Range;
        /// <summary>aoe only — seconds the blast freezes the enemies it catches.</summary>
        [JsonProperty("freeze")] public float Freeze;

        /// <summary>
        /// Interruptible cast WIND-UP, in seconds (F8 "movement interrupts casting"). 0 = instant,
        /// uninterruptible (basic/melee snap-cast, backward-compatible). >0 opens a wind-up window in
        /// <see cref="DeNelle.Village.HeroAbilities.TryCast"/>: mana/cooldown are charged up front, but
        /// the effect only commits after the wind-up completes — moving during it cancels + refunds.
        /// </summary>
        [JsonProperty("castSeconds")] public float CastSeconds;

        /// <summary>
        /// OPTIONAL per-ability cast-ANIMATION key (fix: the cast animation was keyed by the pressed
        /// Q/W/E/R SLOT, so an ability swapped into a slot played that slot's stock clip). Names the
        /// canonical cast-anim keyword this ability should ALWAYS play, independent of the slot it is
        /// equipped in — one of the shared ActionKeywords cast/skill vocabulary ("cast", "skill1",
        /// "skill2", "castHeal") or a q/w/e/r slot letter. Empty (the default) = derive the animation
        /// from the effect SHAPE (see <see cref="DeNelle.Village.HeroAbilities"/>.ResolveAnimVariant).
        /// Additive — an absent field leaves the effect-shape fallback in charge.
        /// </summary>
        [JsonProperty("castAnim")] public string CastAnim;

        // ── WO-614 extra tuning fields (default 0 when absent — backward-compatible) ──
        /// <summary>dot effect — burn damage-per-second applied after the initial strike.</summary>
        [JsonProperty("dotDamage")] public float DotDamage;
        /// <summary>dot effect — seconds the burn ticks.</summary>
        [JsonProperty("dotSeconds")] public float DotSeconds;
        /// <summary>healOverTime effect — seconds the HP drip runs; invuln effect — seconds of immunity.</summary>
        [JsonProperty("seconds")] public float Seconds;

        // ── WO-VFX-003 Hovl VFX string keys (VFXManager.PlayKey). Empty = no Hovl fx for that beat. ──
        // Data-driven so a new ability maps its VFX in abilities.json with NO code change: HeroAbilities
        // reads these keys and calls VFXManager.PlayKey at cast / launch / impact / residual. Keys resolve
        // through HovlVfxCatalog (author via Defenders/VFX/Generate Hovl VFX Catalog); an unknown/empty key
        // simply no-ops (throttled log) so this is safe before the catalog rows are authored.
        /// <summary>Hovl cast/windup VFX key, played at the hero on cast (e.g. "Fireball_Cast").</summary>
        [JsonProperty("vfxCast")] public string VfxCast;
        /// <summary>Hovl projectile VFX key (loop) that travels muzzle→target on a ranged throw (e.g. "Thunderbolt_Projectile").</summary>
        [JsonProperty("vfxProjectile")] public string VfxProjectile;
        /// <summary>Hovl impact VFX key, played at the hit / blast-centre point (e.g. "Fireball_Impact").</summary>
        [JsonProperty("vfxImpact")] public string VfxImpact;
        /// <summary>Hovl residual DoT/HoT/aura/shield LOOP key on the target/hero for the effect's duration (e.g. "Ember_Burn").</summary>
        [JsonProperty("vfxResidual")] public string VfxResidual;

        /// <summary>The slot parsed to the <see cref="AbilitySlot"/> enum.</summary>
        public AbilitySlot SlotEnum
        {
            get
            {
                switch ((Slot ?? string.Empty).Trim().ToLowerInvariant())
                {
                    case "q": return AbilitySlot.Q;
                    case "w": return AbilitySlot.W;
                    case "e": return AbilitySlot.E;
                    case "r": return AbilitySlot.R;
                    default: return AbilitySlot.Q;
                }
            }
        }

        /// <summary>The effect parsed to the <see cref="AbilityEffect"/> enum.</summary>
        public AbilityEffect EffectEnum
        {
            get
            {
                switch ((Effect ?? string.Empty).Trim().ToLowerInvariant())
                {
                    case "strike": return AbilityEffect.Strike;
                    case "snare": return AbilityEffect.Snare;
                    case "aoe": return AbilityEffect.Aoe;
                    case "cleave": return AbilityEffect.Cleave;
                    case "heal": return AbilityEffect.Heal;
                    case "meteor": return AbilityEffect.Meteor;
                    default: return AbilityEffect.Strike;
                }
            }
        }

        /// <summary>The accent colour parsed to a Unity <see cref="UnityEngine.Color"/>.</summary>
        public Color UnityColor =>
            ColorUtility.TryParseHtmlString(Color ?? "#ffffff", out var c) ? c : UnityEngine.Color.white;
    }

    /// <summary>
    /// WO-997: one class's resource ECONOMY — the base pool/regen HeroAbilities seeds on
    /// class resolve (modifiers such as the Cathedral keys still fold on top), plus the
    /// class identity bits: the HUD display name (Mana / Vigor / Focus) and the ranger's
    /// on-hit restore. Optional in abilities.json — an absent block leaves the serialized
    /// HeroAbilities defaults untouched (legacy behaviour).
    /// </summary>
    [Serializable]
    public sealed class ClassResourceDef
    {
        /// <summary>Player-facing resource name (Mana / Vigor / Focus).</summary>
        [JsonProperty("displayName")] public string DisplayName;
        /// <summary>Base pool size (before structural/talent modifiers).</summary>
        [JsonProperty("max")] public float Max;
        /// <summary>Base passive regen per second (before multipliers).</summary>
        [JsonProperty("regenPerSecond")] public float RegenPerSecond;
        /// <summary>Resource restored per landed BASIC-attack hit (ranger Focus); 0 = none.</summary>
        [JsonProperty("onHitRestore")] public float OnHitRestore;
    }

    /// <summary>One hero class's full Q/W/E/R loadout.</summary>
    [Serializable]
    public sealed class AbilityClassDef
    {
        /// <summary>Display name of the class (Mage / Knight / Ranger).</summary>
        [JsonProperty("displayName")] public string DisplayName;
        /// <summary>WO-997: the class's resource economy block. Null when not authored.</summary>
        [JsonProperty("resource")] public ClassResourceDef Resource;
        /// <summary>Q/W/E/R abilities keyed by the lowercase slot letter.</summary>
        [JsonProperty("abilities")] public Dictionary<string, AbilityDef> Abilities = new Dictionary<string, AbilityDef>();
    }

    /// <summary>The parsed abilities.json root.</summary>
    [Serializable]
    public sealed class AbilityCatalogData
    {
        [JsonProperty("version")] public int Version;
        /// <summary>Per-class loadouts keyed by the lowercase class id.</summary>
        [JsonProperty("classes")] public Dictionary<string, AbilityClassDef> Classes = new Dictionary<string, AbilityClassDef>();
    }

    /// <summary>
    /// Static surface over the canonical abilities.json — loads + caches the
    /// per-class ability loadouts, exposes per-class / per-slot lookups. The
    /// PackCatalog.cs loading pattern (port spec Part 4).
    /// </summary>
    public static class AbilityCatalog
    {
        /// <summary>The default v2-foundation hero class — Blaise (port spec Part 3).</summary>
        public const string DefaultClass = "mage";

        private const string StreamingRelativePath = "Data/Canonical/abilities.json";

        private static AbilityCatalogData _data;

        /// <summary>Forces a re-read of abilities.json (used by tests / the Monday sync).</summary>
        public static void Reload()
        {
            _data = null;
            EnsureLoaded();
        }

        /// <summary>
        /// All Q/W/E/R abilities for a class, ordered Q,W,E,R. Returns an empty
        /// list when the class id is unknown.
        /// </summary>
        public static IReadOnlyList<AbilityDef> GetLoadout(string heroClass)
        {
            EnsureLoaded();
            var ordered = new List<AbilityDef>(4);
            if (_data.Classes != null &&
                _data.Classes.TryGetValue(NormalizeClass(heroClass), out var cls) &&
                cls?.Abilities != null)
            {
                foreach (var slot in new[] { "q", "w", "e", "r" })
                    if (cls.Abilities.TryGetValue(slot, out var def) && def != null)
                        ordered.Add(def);
            }
            return ordered;
        }

        /// <summary>
        /// Looks up one ability by class + slot. Returns null when not found.
        /// </summary>
        public static AbilityDef Find(string heroClass, AbilitySlot slot)
        {
            EnsureLoaded();
            if (_data.Classes != null &&
                _data.Classes.TryGetValue(NormalizeClass(heroClass), out var cls) &&
                cls?.Abilities != null &&
                cls.Abilities.TryGetValue(SlotKey(slot), out var def))
            {
                return def;
            }
            return null;
        }

        /// <summary>
        /// Looks up one ability by its unique <see cref="AbilityDef.Id"/> across ALL
        /// class loadouts (a flat id index). Returns null for a null/empty/unknown id.
        /// Used by HeroLoadout to resolve an equipped skill-tree ability into the live
        /// AbilityDef the cast path consumes. Comparison is case-insensitive on the
        /// trimmed id to match the lenient slot/class normalisation elsewhere here.
        /// </summary>
        public static AbilityDef FindById(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return null;
            EnsureLoaded();
            if (_data.Classes == null) return null;
            string want = abilityId.Trim().ToLowerInvariant();
            foreach (var cls in _data.Classes.Values)
            {
                if (cls?.Abilities == null) continue;
                foreach (var def in cls.Abilities.Values)
                {
                    if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                    if (def.Id.Trim().ToLowerInvariant() == want) return def;
                }
            }
            return null;
        }

        /// <summary>
        /// WO-997: the class's resource-economy block from abilities.json, or null when the
        /// class is unknown or authors no block (callers keep their serialized defaults then).
        /// The Cleric aliases onto the mage loadout upstream (HeroAbilities.Awake maps it to
        /// "mage" before any lookup), so no alias is needed here.
        /// </summary>
        public static ClassResourceDef ResourceFor(string heroClass)
        {
            EnsureLoaded();
            return _data.Classes != null &&
                   _data.Classes.TryGetValue(NormalizeClass(heroClass), out var cls)
                ? cls?.Resource : null;
        }

        private static string NormalizeClass(string heroClass)
        {
            return string.IsNullOrEmpty(heroClass)
                ? DefaultClass
                : heroClass.Trim().ToLowerInvariant();
        }

        // =====================================================================
        //  WO-1019 Part A — CLASS OWNERSHIP of an ability id
        // -----------------------------------------------------------------------------
        //  Both persisted ability bars (HeroLoadout = the W/E/R rail, AssignableSkillBar
        //  = the hot-swap rail) store ability IDS and restore them on load. Neither could
        //  ask the question "does this id even belong to the hero wearing it?", so a bar
        //  restored from another hero's session rendered that hero's kit — the owner's
        //  "he inherits the hotswap from previous character" on Thrain.
        //
        //  The answer is DATA, not a string convention: an ability's owning class is the
        //  abilities.json CLASS KEY it is authored under, with the "-skills" pool suffix
        //  stripped ("mage-skills" -> "mage"). The id PREFIX agrees today (mage.fireball)
        //  but it is decoration — a renamed id must not silently change who may equip it.
        //
        //  "universal-skills" -> "universal", usable by EVERY class (that is what the pool
        //  is FOR: universal.arcane-bolt / universal.mend / universal.dash).
        // =====================================================================

        /// <summary>The pool class key whose abilities every hero may equip.</summary>
        public const string UniversalPoolClass = "universal";

        private const string SkillsPoolSuffix = "-skills";

        /// <summary>
        /// The class that OWNS <paramref name="abilityId"/> — the abilities.json class key it is
        /// authored under, with the "-skills" pool suffix stripped ("mage-skills" -> "mage").
        /// Returns <see cref="UniversalPoolClass"/> for the shared pool and null for a
        /// null/empty/unknown id (an id no longer in the catalog belongs to nobody).
        /// </summary>
        public static string OwningClassOf(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return null;
            EnsureLoaded();
            if (_data.Classes == null) return null;
            string want = abilityId.Trim().ToLowerInvariant();
            foreach (var kvp in _data.Classes)
            {
                var cls = kvp.Value;
                if (cls?.Abilities == null) continue;
                foreach (var def in cls.Abilities.Values)
                {
                    if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                    if (def.Id.Trim().ToLowerInvariant() != want) continue;
                    string key = (kvp.Key ?? string.Empty).Trim().ToLowerInvariant();
                    return key.EndsWith(SkillsPoolSuffix, StringComparison.Ordinal)
                        ? key.Substring(0, key.Length - SkillsPoolSuffix.Length)
                        : key;
                }
            }
            return null;
        }

        /// <summary>
        /// True when a hero of <paramref name="heroClass"/> may have <paramref name="abilityId"/>
        /// bound to a bar slot: the id is authored under that class (kit or "-skills" pool) or
        /// under the universal pool. An UNKNOWN id returns false — it cannot render or cast, so
        /// keeping it bound only preserves a lie on the bar.
        ///
        /// CLERIC ALIAS: HeroAbilities.Awake and HeroLoadout.ResolveClass both map Cleric onto the
        /// "mage" loadout (WO-226) and there is no "cleric" block in abilities.json, so the same
        /// alias is applied here — otherwise this predicate would drop a Cleric's ENTIRE bar.
        /// </summary>
        public static bool IsUsableByClass(string abilityId, string heroClass)
        {
            string owner = OwningClassOf(abilityId);
            if (string.IsNullOrEmpty(owner)) return false;
            if (owner == UniversalPoolClass) return true;
            string cls = NormalizeClass(heroClass);
            if (cls == "cleric") cls = "mage";
            return owner == cls;
        }

        private static string SlotKey(AbilitySlot slot)
        {
            switch (slot)
            {
                case AbilitySlot.Q: return "q";
                case AbilitySlot.W: return "w";
                case AbilitySlot.E: return "e";
                case AbilitySlot.R: return "r";
                default: return "q";
            }
        }

        // =====================================================================
        //  Loading — identical strategy to PackCatalog.LoadCatalog().
        // =====================================================================

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            _data = LoadCatalog();
        }

        private static AbilityCatalogData LoadCatalog()
        {
            // WebGL-safe load via CanonicalJson: Resources.Load first (works in a browser,
            // where File.ReadAllText(streamingAssetsPath) THROWS), then a StreamingAssets
            // fallback on desktop/editor. abilities.json is mirrored into
            // Assets/Resources/Data/Canonical/ so the Resources path resolves.
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<AbilityCatalogData>(json);
                    if (parsed != null && parsed.Classes != null && parsed.Classes.Count > 0)
                        return parsed;
                    Debug.LogError("[AbilityCatalog] abilities.json parsed empty.");
                }
                else
                {
                    Debug.LogError("[AbilityCatalog] abilities.json not found (Resources or StreamingAssets).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AbilityCatalog] Failed to read abilities.json: {ex.Message}");
            }

            Debug.LogError("[AbilityCatalog] abilities.json could not be loaded — using an empty catalog.");
            return new AbilityCatalogData { Classes = new Dictionary<string, AbilityClassDef>() };
        }
    }
}
