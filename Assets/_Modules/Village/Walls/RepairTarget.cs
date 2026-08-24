// =============================================================================
// RepairTarget — a uniform "repairable structure" view over the three village
// structure types (Workstream B — player wall-repair mechanic).
// -----------------------------------------------------------------------------
// WallSegment, Gate and Building each already expose a Repair(amount) primitive
// and a damage / HP model — but the three models differ:
//
//   WallSegment : Damage 0..100   (100 = collapsed)        Repair() removes damage
//   Gate        : Hp / MaxHp      (force field tears <25%) Repair() restores Hp
//   Building    : Hp / MaxHp      (0 = destroyed)          Repair() restores Hp
//
// The player-facing repair loop should not care which it is. RepairTarget wraps
// one structure behind a single interface: a normalised 0..1 damage fraction, a
// "needs repair" flag, a renderer-bounds query (for the highlight ring), a
// display name and the verb to actually repair it. WallRepairController works
// only against RepairTarget instances.
//
// Module isolation (port spec Part 2): everything here lives in DeNelle.Village
// and touches only Village types + DeNelle.Core. No HUD / other-module coupling.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>Which of the three village structure kinds a repair target wraps.</summary>
    public enum RepairTargetKind
    {
        /// <summary>A wall-ring section.</summary>
        Wall = 0,
        /// <summary>A cardinal force-field gate.</summary>
        Gate = 1,
        /// <summary>A village building.</summary>
        Building = 2,
    }

    /// <summary>
    /// A uniform handle on one repairable village structure. Created by
    /// <see cref="TryWrap"/> from a <see cref="WallSegment"/>, <see cref="Gate"/>
    /// or <see cref="Building"/>; <see cref="WallRepairController"/> drives the
    /// player repair loop against this abstraction so it never branches on the
    /// concrete structure type.
    /// </summary>
    public sealed class RepairTarget
    {
        private readonly WallSegment _wall;
        private readonly Gate _gate;
        private readonly Building _building;

        /// <summary>Which structure kind this target wraps.</summary>
        public RepairTargetKind Kind { get; }

        /// <summary>The structure's root <see cref="GameObject"/> — never null for a live target.</summary>
        public GameObject GameObject { get; }

        /// <summary>The structure's <see cref="Transform"/>.</summary>
        public Transform Transform => GameObject != null ? GameObject.transform : null;

        private RepairTarget(RepairTargetKind kind, GameObject go,
            WallSegment wall, Gate gate, Building building)
        {
            Kind = kind;
            GameObject = go;
            _wall = wall;
            _gate = gate;
            _building = building;
        }

        /// <summary>
        /// Wraps the structure component found on or above <paramref name="hitCollider"/>
        /// as a <see cref="RepairTarget"/>. Returns null when the collider belongs
        /// to no village structure (a wall / gate / building). The same physical
        /// structure can be re-wrapped freely — a RepairTarget is a lightweight
        /// view, it holds no per-frame state.
        /// </summary>
        public static RepairTarget TryWrap(Component hitCollider)
        {
            if (hitCollider == null) return null;

            // DEF-226: never wrap a hero / player-controlled object as a repair
            // target. A repair highlight must never attach to or float over a hero
            // (a screenshot showed the marker near a hero's head). Heroes are tagged
            // Player (locomotion) / HeroTarget (enemy AI) per CLAUDE.md §7 — bail out
            // if the hit object or any ancestor carries either tag.
            if (IsHeroOrPlayer(hitCollider.transform)) return null;

            var wall = hitCollider.GetComponentInParent<WallSegment>();
            if (wall != null)
                return new RepairTarget(RepairTargetKind.Wall, wall.gameObject, wall, null, null);

            var gate = hitCollider.GetComponentInParent<Gate>();
            if (gate != null)
                return new RepairTarget(RepairTargetKind.Gate, gate.gameObject, null, gate, null);

            var building = hitCollider.GetComponentInParent<Building>();
            if (building != null)
                return new RepairTarget(RepairTargetKind.Building, building.gameObject, null, null, building);

            return null;
        }

        /// <summary>
        /// DEF-226: true when <paramref name="t"/> or any ancestor is the hero
        /// (CLAUDE.md §7: the hero tag is "Player"; a "HeroTarget" tag was NEVER
        /// declared — in a player build CompareTag on an undefined tag logs a native
        /// error line the flight recorder captures, so no fallback check exists here).
        /// Walks up the hierarchy so a hero's child collider is caught.
        /// Used to exclude heroes from ever becoming a repair target.
        /// </summary>
        private static bool IsHeroOrPlayer(Transform t)
        {
            for (var cur = t; cur != null; cur = cur.parent)
            {
                if (cur.CompareTag("Player"))
                    return true;
            }
            return false;
        }

        /// <summary>True while the wrapped structure component still exists.</summary>
        public bool IsValid => GameObject != null && Kind switch
        {
            RepairTargetKind.Wall => _wall != null,
            RepairTargetKind.Gate => _gate != null,
            RepairTargetKind.Building => _building != null,
            _ => false,
        };

        /// <summary>
        /// Normalised damage, 0 (pristine) .. 1 (fully destroyed / collapsed).
        /// Unifies WallSegment.Damage (0..100) and Gate/Building HP fractions.
        /// </summary>
        public float DamageFraction
        {
            get
            {
                switch (Kind)
                {
                    case RepairTargetKind.Wall:
                        return _wall != null ? Mathf.Clamp01(_wall.Damage / 100f) : 0f;
                    case RepairTargetKind.Gate:
                        return _gate != null ? 1f - Mathf.Clamp01(_gate.HpFraction) : 0f;
                    case RepairTargetKind.Building:
                        return _building != null ? 1f - Mathf.Clamp01(_building.HpFraction) : 0f;
                    default:
                        return 0f;
                }
            }
        }

        /// <summary>
        /// True when the structure has taken damage and a repair would change its
        /// state. A pristine structure returns false (nothing to repair).
        /// </summary>
        public bool NeedsRepair => DamageFraction > 0.0001f;

        /// <summary>
        /// A player-facing label for the structure, used in the repair prompt
        /// headline. LOCALIZE — see <see cref="WallRepairStrings"/>.
        /// </summary>
        public string DisplayName
        {
            get
            {
                switch (Kind)
                {
                    case RepairTargetKind.Wall:
                        return _wall != null && _wall.IsCorner
                            ? WallRepairStrings.WallCornerName
                            : WallRepairStrings.WallSegmentName;
                    case RepairTargetKind.Gate:
                        return _gate != null
                            ? string.Format(WallRepairStrings.GateNameFormat, _gate.Direction)
                            : WallRepairStrings.GateGenericName;
                    case RepairTargetKind.Building:
                        if (_building == null) return WallRepairStrings.BuildingGenericName;
                        // The build menu resolves canon names via VillageStrings;
                        // the designer-facing label is the safe non-empty fallback.
                        return !string.IsNullOrEmpty(_building.DisplayLabel)
                            ? _building.DisplayLabel
                            : WallRepairStrings.BuildingGenericName;
                    default:
                        return WallRepairStrings.StructureGenericName;
                }
            }
        }

        /// <summary>
        /// Applies an INCREMENTAL repair of <paramref name="amount"/> to the
        /// wrapped structure through its existing <c>Repair()</c> primitive. The
        /// amount is on each structure's native scale (0..100 damage points for
        /// walls/gates, HP for buildings). REP-1: a fixed 100 is NOT a full repair
        /// for a Building — buildings.json authors MaxHp 120..240, so Repair(100f)
        /// from 0 HP leaves HpFraction 0.42..0.83 and the damage visuals correctly
        /// stay on. The charged player-repair flow must use <see cref="RepairFull"/>.
        /// </summary>
        public void Repair(float amount)
        {
            float before = DamageFraction;
            switch (Kind)
            {
                case RepairTargetKind.Wall:
                    if (_wall != null) _wall.Repair(amount);
                    break;
                case RepairTargetKind.Gate:
                    if (_gate != null) _gate.Repair(amount);
                    break;
                case RepairTargetKind.Building:
                    if (_building != null) _building.Repair(amount);
                    break;
            }
            // REP-1 no-silent-failure net: an under-delivering repair is a logged
            // line (frac lands above 0.00 with needsRepair=True), never a silent
            // still-damaged building.
            FlowTrace.Step("Repair",
                $"RepairTarget.Repair '{DisplayName}' ({Kind}) amount={amount} " +
                $"frac {before:0.00}->{DamageFraction:0.00} needsRepair={NeedsRepair}");
        }

        /// <summary>
        /// Fully restores the wrapped structure BY CONTRACT (REP-1): resolves each
        /// kind's own full-restore magnitude instead of assuming a fixed 100-unit
        /// amount is "full". Walls clear their 0..100 damage track; gates and
        /// buildings top up by their own MaxHp (both primitives clamp at max, and
        /// both derive their broken/destroyed state from HP — no separate latch to
        /// clear). The charged repair paths (<see cref="WallRepairController"/>
        /// ConfirmRepair / RepairAll) call this.
        /// </summary>
        public void RepairFull()
        {
            float before = DamageFraction;
            switch (Kind)
            {
                case RepairTargetKind.Wall:
                    if (_wall != null) _wall.Repair(100f);            // damage track is 0..100 by contract
                    break;
                case RepairTargetKind.Gate:
                    if (_gate != null) _gate.Repair(_gate.MaxHp);     // additive, clamped at MaxHp
                    break;
                case RepairTargetKind.Building:
                    if (_building != null) _building.Repair(_building.MaxHp); // additive, clamped at MaxHp
                    break;
            }
            FlowTrace.Step("Repair",
                $"RepairTarget.RepairFull '{DisplayName}' ({Kind}) " +
                $"frac {before:0.00}->{DamageFraction:0.00} needsRepair={NeedsRepair}");
        }

        /// <summary>
        /// World-space centre of the structure's renderer bounds — the anchor for
        /// the selection-highlight ring. Falls back to the transform position
        /// when the structure has no renderers.
        /// </summary>
        public Vector3 WorldCenter
        {
            get
            {
                if (TryGetWorldBounds(out var b)) return b.center;
                return Transform != null ? Transform.position : Vector3.zero;
            }
        }

        /// <summary>
        /// Computes the combined world-space renderer bounds of the structure.
        /// Returns false when the structure has no renderers at all.
        /// </summary>
        public bool TryGetWorldBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            if (GameObject == null) return false;

            // ⛔ INCLUDE ONLY WHAT THE PLAYER CAN SEE (fixed 2026-08-24, owner felt-test: the repair
            // marker rendered as a ~20 m slab over a ~3 m hut). This used to encapsulate EVERY
            // renderer in the hierarchy, which pulled in two things that are not the building:
            //
            //   1. VFX CHILDREN. Measured on device: an aura child reports boundsSize 12.5 m
            //      ([Hovl_Cathedral_Aura], Logs/device/enemy-color.log). Extents 6.25 -> the
            //      caller's 6.25*1.35+0.6 = 9.04 pins its 9 m clamp, so the marker stops being
            //      fitted to anything and is simply always maximum size.
            //   2. THE HIDDEN BAKED MESH. HubStructureVisualInjector.SkinStorefront hides the baked
            //      model with `r.enabled = false`, NOT SetActive(false) — so the renderer is still
            //      RETURNED by GetComponentsInChildren and its bounds still inflate the box, even
            //      though nothing is drawn.
            //
            // ⚠ `enabled` is the exact property that distinguishes them, which is why filtering on
            // it is the fix rather than a name/tag heuristic: a disabled renderer draws nothing, so
            // it cannot be part of what the player sees us circling.
            //
            // ParticleSystemRenderer is excluded by TYPE: a particle system's bounds describe where
            // its particles may TRAVEL, not where the structure IS, so it is never a size input.
            var renderers = GameObject.GetComponentsInChildren<Renderer>();
            bool any = false;
            int skippedDisabled = 0, skippedParticles = 0;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (!r.enabled) { skippedDisabled++; continue; }
                if (r is ParticleSystemRenderer) { skippedParticles++; continue; }
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }

            // ⚠ FALL BACK RATHER THAN RETURN NOTHING. If every renderer was filtered out, the old
            // all-inclusive box is still a better answer than "no bounds" (which drops the caller to
            // a blind radius of 2). Say so — a silent widening is how this became invisible.
            if (!any && renderers.Length > 0)
            {
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    if (!any) { bounds = r.bounds; any = true; }
                    else bounds.Encapsulate(r.bounds);
                }
                if (any)
                    FlowTrace.Warn("Repair", $"TryGetWorldBounds('{DisplayName}'): every renderer was disabled or " +
                                             "a particle system - falling back to the UNFILTERED bounds, so " +
                                             "the marker may read oversized.");
            }

            if (any && (skippedDisabled > 0 || skippedParticles > 0))
                FlowTrace.Step("Repair", $"TryGetWorldBounds('{DisplayName}'): size={bounds.size.x:0.0}x{bounds.size.z:0.0}m " +
                                         $"from {renderers.Length - skippedDisabled - skippedParticles} visible renderer(s) " +
                                         $"(skipped {skippedDisabled} disabled, {skippedParticles} particle).");
            return any;
        }

        /// <summary>True when two targets wrap the same physical structure.</summary>
        public bool SameAs(RepairTarget other)
        {
            return other != null && other.GameObject == GameObject;
        }
    }
}
