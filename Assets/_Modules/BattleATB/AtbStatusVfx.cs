// =============================================================================
// AtbStatusVfx - owner-picked status/impact VFX presentation for the ATB battle.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.BattleATB (cannot reference DeNelle.Village - same constraint
// as AtbCombatantSwapper, so prefabs are reached via Resources.Load, the
// established BattleATB idiom).
//
// OWNER VFX PICKS (2026-08-16, verbatim - picks are canon, never substitute):
//   "Cast Haste"  -> Assets\Lana Studio\Casual RPG VFX\Prefabs\States\Aura_acceleration.prefab
//   "Cast Slow"   -> Assets\Lana Studio\Casual RPG VFX\Prefabs\States\Aura_slowdown.prefab
//   "Sleep Impact"-> Assets\Lana Studio\Casual RPG VFX\Prefabs\States\Character_status_sleep.prefab
//   "Heal Impact" -> Assets\Lana Studio\Casual RPG VFX\Prefabs\Backlight_resources\backlight_health_drop.prefab
//
// Haste/Slow are STATES: the aura spawns on StatusApply (the cast landing on the
// unit), lives while the status holds, and despawns on StatusExpire / death /
// battle end. Heal Impact is a ONE-SHOT on the healed target the moment an
// ability heal is credited. Sleep has NO StatusKind in the engine yet - the key
// and prefab path are registered below so the wiring is one switch-case away
// when the state lands; nothing speculative is spawned for it.
//
// Prefab resolution: Resources.Load at VFX/Status/<verbatim-prefab-name>. The
// Lana pack itself is NOT under a Resources folder, so the committer places
// Resources-reachable copies at Assets/Resources/VFX/Status/ (same pattern as
// the existing Assets/Resources/VFX/Aura/* prefabs). Until then every spawn
// degrades gracefully: FlowTrace.Warn, the cast proceeds without VFX (CLAUDE.md
// section 4 - missing prefab is a warning, never an error).
//
// Seam: BattleController.HandleTurnResolved feeds this the SAME new-entries log
// window its hit/death anim + floating damage drivers use (the cursor idiom) -
// StatusApply/StatusExpire/Death/heal entries are all in BattleState.Log, so no
// engine file changes and no second event channel. Cold path (a few entries per
// turn), so traces are plain Step/Warn - no throttling needed.
// =============================================================================

using System.Collections.Generic;
using DeNelle.BattleATB.Engine;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.BattleATB
{
    /// <summary>
    /// Drives the owner-picked Lana status auras (Haste / Slow) and impact
    /// one-shots (Heal) on the 3D battle anchors. Owned by
    /// <see cref="BattleController"/>; fed the new-log window each TurnResolved.
    /// </summary>
    public sealed class AtbStatusVfx
    {
        // -- Owner-pick registry: key -> Resources path (prefab basenames VERBATIM) --
        /// <summary>"Cast Haste" (owner pick 2026-08-16): Lana Aura_acceleration.</summary>
        public const string HastePath = "VFX/Status/Aura_acceleration";
        /// <summary>"Cast Slow" (owner pick 2026-08-16): Lana Aura_slowdown.</summary>
        public const string SlowPath = "VFX/Status/Aura_slowdown";
        /// <summary>"Heal Impact" (owner pick 2026-08-16): Lana backlight_health_drop.</summary>
        public const string HealImpactPath = "VFX/Status/backlight_health_drop";
        /// <summary>"Ice Spell Impact" (owner pick 2026-08-16): Lana top_down_ice_circle.
        /// Keyed on the engine's first-class element concept (ElementType.Ice on the
        /// strike log entry), NOT per-ability hardcoding - Frost Lance, IceWolf pet
        /// abilities and any future ice spell all resolve through Strike with their
        /// AbilityDef.Element, so one element key covers them all. Scope = ABILITY
        /// strikes only (the owner's word was "Spell"); an ice unit's basic attack
        /// does not fire it.</summary>
        public const string IceImpactPath = "VFX/Status/top_down_ice_circle";
        /// <summary>"Fire Spell impact" (owner pick 2026-08-16): Unity ParticlePack
        /// BigExplosion. Exact sibling of the ice impact: keyed on the element
        /// concept (ElementType.Flame on the strike log entry - the engine's fire
        /// element is named Flame), ABILITY strikes only. Covers Flameblast (Mage W)
        /// and the Emberhead pet's Emberbite / Pyre Bond, plus any future fire spell.
        /// COMMITTER: the source prefab (Assets/UnityTechnologies/ParticlePack/
        /// EffectExamples/Fire &amp; Explosion Effects/Prefabs/BigExplosion.prefab) is
        /// GITIGNORED - it needs the EDITOR MIRROR treatment (AssetDatabase copy of
        /// prefab + material/texture dependencies), NOT a plain file copy.</summary>
        public const string FireImpactPath = "VFX/Status/BigExplosion";
        /// <summary>"Holy Impact" (owner pick 2026-08-16): Lana Hit_light.
        /// REGISTERED ONLY - the ATB engine has NO holy/light element yet
        /// (verified 2026-08-16: ElementType = Physical | Aether | Flame | Ice;
        /// heal-flavoured abilities like Mender's Light are typed Aether). Do NOT
        /// substitute Aether - picks are canon and Aether already reads as arcane.
        /// When a Holy/Light member lands in ElementType, wire it exactly like the
        /// Ice/Flame siblings: an else-if in the Ability case calling
        /// OnElementImpact(state, entry.TargetId, resolveAnchor, HolyImpactPath,
        /// "HolyImpact", groundPoint: false) - one-shot at the struck unit,
        /// spell-only. Strike already stamps Element generically, so no further
        /// engine edit will be needed.</summary>
        public const string HolyImpactPath = "VFX/Status/Hit_light";
        /// <summary>"Sleep Impact" (owner pick 2026-08-16): Lana Character_status_sleep.
        /// REGISTERED ONLY - the ATB engine has no StatusKind.Sleep yet (verified
        /// 2026-08-16: Types.cs StatusKind has no Sleep member, no Sleep mechanic
        /// anywhere in Assets/_Modules). When the state lands, add a
        /// StatusKind.Sleep case to <see cref="AuraPathFor"/> - the owner's word
        /// was "Impact", so the on-APPLY moment is the non-negotiable part; the
        /// prefab name (Character_status_sleep) reads as a status loop, so wire it
        /// like Haste/Slow (alive while asleep, despawn on wake) with the apply
        /// moment as its birth.</summary>
        public const string SleepImpactPath = "VFX/Status/Character_status_sleep";

        /// <summary>Live aura instances keyed by (unitId, status).</summary>
        private readonly Dictionary<(string unitId, StatusKind kind), GameObject> _live =
            new Dictionary<(string, StatusKind), GameObject>();

        /// <summary>Prefab cache; a path maps to null after a failed load so the
        /// miss is warned once per battle scene, not once per application.</summary>
        private readonly Dictionary<string, GameObject> _prefabs =
            new Dictionary<string, GameObject>();

        /// <summary>Resources path for a status aura, or null when the status has
        /// no owner-picked presentation (only owner tags add rows here - never
        /// pick creatively).</summary>
        private static string AuraPathFor(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Haste: return HastePath;
                case StatusKind.Slow:  return SlowPath;
                default:               return null;
            }
        }

        /// <summary>
        /// Scan log entries [fromIndex..end) - the same window BattleController's
        /// other presentation drivers use - and spawn/despawn status VFX.
        /// <paramref name="resolveAnchor"/> maps a unit to its 3D anchor
        /// (hero/enemy capsule); a null anchor skips with a Warn, never throws.
        /// </summary>
        public void ProcessLog(BattleState state, int fromIndex,
                               System.Func<BattleUnit, Transform> resolveAnchor)
        {
            if (state?.Log == null || resolveAnchor == null) return;

            for (int i = Mathf.Max(0, fromIndex); i < state.Log.Count; i++)
            {
                var entry = state.Log[i];
                switch (entry.Event)
                {
                    case BattleLogEvent.StatusApply:
                        if (entry.Status != null && entry.TargetId != null)
                            OnStatusApply(state, entry.TargetId, entry.Status.Value, resolveAnchor);
                        break;

                    case BattleLogEvent.StatusExpire:
                        if (entry.Status != null && entry.TargetId != null)
                            OnStatusExpire(entry.TargetId, entry.Status.Value);
                        break;

                    case BattleLogEvent.Death:
                        if (entry.TargetId != null)
                            ClearUnit(entry.TargetId);
                        break;

                    case BattleLogEvent.Ability:
                        // Heal landing: ability entries log heals as NEGATIVE Amount
                        // (see Actions.ResolveAbility "mends"/heal PushLog calls).
                        // Owner pick covers the ATB heal-landing moment ONLY -
                        // Item (potion) and StatusTick (regen) heals stay unwired
                        // until the owner tags them.
                        if ((entry.Amount ?? 0) < 0 && entry.TargetId != null)
                            OnHealImpact(state, entry.TargetId, resolveAnchor);
                        // Elemental spell landing: a damaging ABILITY strike stamped
                        // with its element (Strike logs the element as metadata).
                        // Ice -> top_down_ice_circle; Flame -> BigExplosion.
                        else if ((entry.Amount ?? 0) > 0 && entry.Element == ElementType.Ice
                                 && entry.TargetId != null)
                            OnElementImpact(state, entry.TargetId, resolveAnchor,
                                            IceImpactPath, "IceSpellImpact", groundPoint: true);
                        else if ((entry.Amount ?? 0) > 0 && entry.Element == ElementType.Flame
                                 && entry.TargetId != null)
                            OnElementImpact(state, entry.TargetId, resolveAnchor,
                                            FireImpactPath, "FireSpellImpact", groundPoint: false);
                        break;
                }
            }
        }

        /// <summary>Despawn everything (battle end / scene teardown / new battle).</summary>
        public void Clear()
        {
            foreach (var kv in _live)
            {
                if (kv.Value != null)
                {
                    FlowTrace.Step("AtbBattle",
                        $"StatusVfx: CLEAR aura status={kv.Key.kind} unit={kv.Key.unitId}");
                    Object.Destroy(kv.Value);
                }
            }
            _live.Clear();
        }

        // -- internals ---------------------------------------------------------

        private void OnStatusApply(BattleState state, string unitId, StatusKind kind,
                                   System.Func<BattleUnit, Transform> resolveAnchor)
        {
            string path = AuraPathFor(kind);
            if (path == null) return; // no owner pick for this status - nothing to play

            var key = (unitId, kind);
            if (_live.TryGetValue(key, out var existing) && existing != null)
            {
                // Re-apply refreshes the engine status turns; the loop is already
                // playing, keep it (no double-spawn).
                FlowTrace.Step("AtbBattle",
                    $"StatusVfx: REFRESH aura status={kind} unit={unitId} (already live)");
                return;
            }

            BattleUnit unit = FindUnit(state, unitId);
            Transform anchor = unit != null ? resolveAnchor(unit) : null;
            if (anchor == null)
            {
                FlowTrace.Warn("AtbBattle",
                    $"StatusVfx: no 3D anchor for unit={unitId} status={kind} - aura skipped (cast proceeds).");
                return;
            }

            GameObject prefab = LoadPrefab(path, kind.ToString());
            if (prefab == null) return; // LoadPrefab already warned - graceful, no VFX

            GameObject go = Object.Instantiate(prefab, anchor);
            go.name = $"StatusVfx_{kind}_{unitId}";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            _live[key] = go;
            FlowTrace.Step("AtbBattle",
                $"StatusVfx: SPAWN aura key={kind} prefab='Resources/{path}' anchor='{anchor.name}' unit={unitId}");
        }

        private void OnStatusExpire(string unitId, StatusKind kind)
        {
            var key = (unitId, kind);
            if (!_live.TryGetValue(key, out var go)) return;
            _live.Remove(key);
            if (go != null) Object.Destroy(go);
            FlowTrace.Step("AtbBattle",
                $"StatusVfx: DESPAWN aura status={kind} unit={unitId} (status expired)");
        }

        private void OnHealImpact(BattleState state, string unitId,
                                  System.Func<BattleUnit, Transform> resolveAnchor)
        {
            BattleUnit unit = FindUnit(state, unitId);
            Transform anchor = unit != null ? resolveAnchor(unit) : null;
            if (anchor == null)
            {
                FlowTrace.Warn("AtbBattle",
                    $"StatusVfx: no 3D anchor for healed unit={unitId} - heal impact skipped (heal already applied).");
                return;
            }

            GameObject prefab = LoadPrefab(HealImpactPath, "HealImpact");
            if (prefab == null) return;

            GameObject go = Object.Instantiate(prefab, anchor.position + Vector3.up * 1.0f,
                                               Quaternion.identity);
            go.name = $"HealImpact_{unitId}";
            Object.Destroy(go, OneShotLifetime(go));
            FlowTrace.Step("AtbBattle",
                $"StatusVfx: SPAWN one-shot key=HealImpact prefab='Resources/{HealImpactPath}' target='{anchor.name}' unit={unitId}");
        }

        /// <summary>One-shot elemental spell impact at the STRUCK unit (the target,
        /// never the caster). <paramref name="groundPoint"/> true = the prefab is a
        /// top-down ground decal (ice circle) anchored at the unit's ground point;
        /// false = a volumetric burst (fire explosion) centred at torso height.
        /// Prefab's own authored orientation is kept either way.</summary>
        private void OnElementImpact(BattleState state, string unitId,
                                     System.Func<BattleUnit, Transform> resolveAnchor,
                                     string path, string label, bool groundPoint)
        {
            BattleUnit unit = FindUnit(state, unitId);
            Transform anchor = unit != null ? resolveAnchor(unit) : null;
            if (anchor == null)
            {
                FlowTrace.Warn("AtbBattle",
                    $"StatusVfx: no 3D anchor for struck unit={unitId} key={label} - impact skipped (damage already applied).");
                return;
            }

            GameObject prefab = LoadPrefab(path, label);
            if (prefab == null) return;

            Vector3 impact = groundPoint ? anchor.position : anchor.position + Vector3.up * 1.0f;
            GameObject go = Object.Instantiate(prefab, impact, prefab.transform.rotation);
            go.name = $"{label}_{unitId}";
            Object.Destroy(go, OneShotLifetime(go));
            FlowTrace.Step("AtbBattle",
                $"StatusVfx: SPAWN one-shot key={label} prefab='Resources/{path}' " +
                $"impact=({impact.x:0.0},{impact.y:0.0},{impact.z:0.0}) target='{anchor.name}' unit={unitId}");
        }

        /// <summary>Drop every live aura riding a unit (death / KO).</summary>
        private void ClearUnit(string unitId)
        {
            List<(string, StatusKind)> gone = null;
            foreach (var kv in _live)
            {
                if (kv.Key.unitId != unitId) continue;
                (gone ??= new List<(string, StatusKind)>()).Add(kv.Key);
                if (kv.Value != null) Object.Destroy(kv.Value);
                FlowTrace.Step("AtbBattle",
                    $"StatusVfx: DESPAWN aura status={kv.Key.kind} unit={unitId} (unit down)");
            }
            if (gone != null)
                foreach (var k in gone) _live.Remove(k);
        }

        /// <summary>Cached Resources load; a miss warns ONCE per path (graceful
        /// fallback - the cast/heal proceeds without VFX, per CLAUDE.md section 4).</summary>
        private GameObject LoadPrefab(string path, string label)
        {
            if (_prefabs.TryGetValue(path, out var cached)) return cached;
            var prefab = Resources.Load<GameObject>(path);
            _prefabs[path] = prefab;
            if (prefab == null)
                FlowTrace.Warn("AtbBattle",
                    $"StatusVfx: prefab MISSING at Resources/{path} for key={label} - " +
                    "playing without VFX. Committer: place the Resources copy under Assets/Resources/VFX/Status/.");
            return prefab;
        }

        /// <summary>Auto-destroy horizon for a one-shot: longest particle
        /// duration + start lifetime among its systems, clamped 0.5-8 s
        /// (fallback 3 s when the prefab has no ParticleSystem).</summary>
        private static float OneShotLifetime(GameObject go)
        {
            float best = 0f;
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                float t = main.duration + main.startLifetime.constantMax;
                if (t > best) best = t;
            }
            return best > 0f ? Mathf.Clamp(best, 0.5f, 8f) : 3f;
        }

        private static BattleUnit FindUnit(BattleState state, string unitId)
        {
            if (state?.Units == null) return null;
            foreach (BattleUnit u in state.Units)
                if (u.Id == unitId) return u;
            return null;
        }
    }
}
