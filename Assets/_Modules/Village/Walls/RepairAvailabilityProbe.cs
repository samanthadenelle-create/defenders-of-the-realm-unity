// =============================================================================
// RepairAvailabilityProbe - INSTRUMENTATION ONLY (owner F8 seq=2153: "building is
// on fire but there is no option to repair").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS EXISTS (CLAUDE.md section 12 - instrument first, never inference-fix):
// the F8 capture for that report carried NO repair-path lines (a per-frame logger
// had flooded the harvest window), so there is no data proving WHY the repair
// option was missing. This probe makes the NEXT capture answer the question
// outright. It READS ONLY - it never repairs, never spawns UI, never mutates a
// structure - so it can ship enabled while the defect is being chased and be
// deleted (or FlowTrace-disabled) once the cause is proven.
//
// "ON FIRE" IS A VFX, NOT A STATE. The fire the owner saw is
// StructureDamageVisuals' Damage_Fire loop, which arms at hp <= the
// damage-states.json 'fire' threshold (0.25 by default) and covers SEVEN
// structure surfaces: wall / building / gate / collector / tower / defensetower
// / arcanetower / harvestsite. The repair SURFACES do not cover the same set,
// and that mismatch is the leading candidate this probe is built to prove or
// kill. So every line below reports the VFX-side state and the REPAIR-side
// availability for the SAME object, on one line, with no interpretation.
//
// THE FOUR QUESTIONS IT ANSWERS (one line per burning structure, on change):
//   1. WHICH structure and WHAT STATE - concrete component type, live hp
//      fraction, broken/destroyed flag. Separates "damaged" from "destroyed"
//      from "burning but healthy-ish".
//   2. OFFERED-BUT-NOT-SURFACED vs NEVER-OFFERED - `inRepairAllSet` says whether
//      the repair BACKEND is already pricing this structure. If it is in the set
//      and the player saw no button, the gap is UI. If it is absent from the set,
//      the gap is logic.
//   3. PLAYER-BUILT vs BAKED - `placed` reports whether a PlacedStructure parent
//      exists (the build-mode placement marker). A baked map structure carries
//      none. NOTE: the baked-registration lane is owned by another work order;
//      this probe only REPORTS the distinction, it never registers anything.
//   4. DESTROYED-IS-CORRECT - `broken=True` means "no repair option" is the
//      CORRECT behaviour (project rule: a destroyed item is rebuilt at full cost,
//      there is no repair discount) and the real defect is the fire VFX still
//      showing. The line says so explicitly so nobody "fixes" correct behaviour.
//
// It also reports the SCENE-LEVEL repair surfaces once per change, because a
// missing surface is invisible from the structure side:
//   * WallRepairController present? ENABLED? (a hub-installed one is
//     deliberately enabled=false, which disables tap-to-select entirely)
//   * HubRepairAffordance present? what is its button showing?
// If BOTH read absent while a structure burns, the player genuinely has no
// repair affordance anywhere and the trace proves it in one line.
//
// Instrumented [Flow:RepairProbe]. Null-safe + Guard-wrapped throughout; a throw
// in a diagnostic must never break a play session.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.Buildings.Progression;

namespace DeNelle.Village
{
    /// <summary>
    /// Read-only diagnostic that pairs each BURNING structure's actual state with
    /// the repair options actually available to the player, so an F8 capture
    /// pinpoints whether "no option to repair" is a UI gap, a logic gap, or
    /// correct destroyed-item behaviour. Never mutates anything.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RepairAvailabilityProbe : MonoBehaviour
    {
        /// <summary>Seconds between polls. Slow on purpose - this is a diagnostic, not a system.</summary>
        private const float PollInterval = 2.5f;

        /// <summary>
        /// Safety margin above the data 'fire' threshold. A structure just above the
        /// line is reported too, so a capture shows the approach to the fire state
        /// rather than only its arrival (the owner reports the moment she SEES fire,
        /// which may be a frame either side of the threshold).
        /// </summary>
        private const float ReportMargin = 0.05f;

        private float _timer;

        // Last-reported line per structure, so the log carries TRANSITIONS and not a
        // poll-rate firehose. The flooded seq=2153 harvest is exactly what this avoids.
        private readonly Dictionary<Object, string> _lastLine = new Dictionary<Object, string>();
        private string _lastSurfaceLine;

        // ── Self-install (mirrors StructureDamageVisuals / HubRepairAffordance) ──

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TrySpawn();
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode mode) => TrySpawn();

        /// <summary>
        /// Installs UNCONDITIONALLY, unlike HubRepairAffordance which gates on
        /// SceneHasRepairables(). That is deliberate: whether that gate is what
        /// suppressed the repair option is one of the things being measured, so the
        /// probe must not inherit the gate it is measuring.
        /// </summary>
        private static void TrySpawn()
        {
            if (FindAnyObjectByType<RepairAvailabilityProbe>() != null) return;
            var go = new GameObject("RepairAvailabilityProbe");
            go.AddComponent<RepairAvailabilityProbe>();
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = PollInterval;
            Guard.Try("RepairProbe", "poll burning structures", Poll);
        }

        // ── Poll ────────────────────────────────────────────────────────────────

        private void Poll()
        {
            var burning = new List<Row>();
            CollectBurning(burning);
            if (burning.Count == 0)
            {
                // Nothing is on fire - stay silent so the log is not padded, but drop
                // the transition memory so the NEXT fire re-reports in full.
                if (_lastLine.Count > 0) _lastLine.Clear();
                _lastSurfaceLine = null;
                return;
            }

            ReportSurfaces();

            var repair = FindAnyObjectByType<WallRepairController>();
            string setLine = repair != null
                ? Guard.Try("RepairProbe", "describe repair-all set", () => repair.DescribeRepairAllSet(), "<threw>")
                : "<no WallRepairController - the repair backend is not present in this scene>";

            foreach (var row in burning)
            {
                bool inSet = repair != null && setLine != null &&
                             setLine.IndexOf(row.Name, System.StringComparison.OrdinalIgnoreCase) >= 0;

                string line =
                    $"BURNING '{row.Name}' type={row.TypeName} hp={row.Hp:0.00} broken={row.Broken} " +
                    $"placed={row.Placed} tapRepairable={row.TapRepairable} inRepairAllSet={inSet}";

                if (_lastLine.TryGetValue(row.Key, out string prev) && prev == line) continue;
                _lastLine[row.Key] = line;

                if (row.Broken)
                {
                    // Question 4, answered before anyone can mis-read the line: a destroyed
                    // structure having no repair option is the DESIGNED behaviour. If this
                    // line is what the capture shows, the defect is the FIRE VFX on a ruin,
                    // not the missing repair option - do not "fix" the repair gate.
                    FlowTrace.Step("RepairProbe",
                        $"{line} -> DESTROYED. No repair option is CORRECT here (destroyed items " +
                        "rebuild at full cost, no repair discount). If a FIRE loop is showing on " +
                        "this shell, the defect is the VFX state, not the repair gate.");
                }
                else if (inSet && !row.TapRepairable)
                {
                    FlowTrace.Warn("RepairProbe",
                        $"{line} -> the backend PRICES this structure (it is in the Repair-All set) " +
                        "but RepairTarget cannot wrap it, so tap-to-select can never reach it. " +
                        "Repair-All is the only surface that can fix this one.");
                }
                else if (!inSet)
                {
                    FlowTrace.Warn("RepairProbe",
                        $"{line} -> NEVER OFFERED: the structure is damaged and burning but the " +
                        "Repair-All set does not contain it. This is a LOGIC gap, not a UI gap.");
                }
                else
                {
                    FlowTrace.Step("RepairProbe",
                        $"{line} -> repair IS offered by the backend. If the player saw no option, " +
                        "the gap is in the SURFACES line above (button hidden / affordance absent).");
                }
            }
        }

        /// <summary>
        /// Report the scene-level repair surfaces on change. A missing surface cannot be
        /// seen from any single structure, and "no option to repair" is most often a
        /// surface that never installed rather than a structure that refused.
        /// </summary>
        private void ReportSurfaces()
        {
            var repair = FindAnyObjectByType<WallRepairController>();
            var hub = FindAnyObjectByType<HubRepairAffordance>();
            var wave = FindAnyObjectByType<WaveManager>();

            string line =
                $"SURFACES scene='{SceneManager.GetActiveScene().name}' " +
                $"WallRepairController={(repair == null ? "ABSENT" : (repair.enabled ? "present+ENABLED" : "present+DISABLED(no tap-to-select)"))} " +
                $"HubRepairAffordance={(hub == null ? "ABSENT" : "present:" + hub.DiagnosticState)} " +
                $"WaveManager={(wave == null ? "none(pure hub)" : wave.Phase.ToString())}";

            if (line == _lastSurfaceLine) return;
            _lastSurfaceLine = line;

            if (repair == null && hub == null)
                FlowTrace.Fail("RepairProbe",
                    $"{line} -> NO repair surface exists in this scene at all while a structure burns. " +
                    "The player has no way to repair anything here.");
            else
                FlowTrace.Step("RepairProbe", line);
        }

        // ── Collection ──────────────────────────────────────────────────────────

        /// <summary>One burning structure's read-only snapshot.</summary>
        private struct Row
        {
            public Object Key;
            public string Name;
            public string TypeName;
            public float Hp;
            public bool Broken;
            public bool Placed;
            public bool TapRepairable;
        }

        /// <summary>
        /// Collect every structure at or near the data-driven 'fire' threshold, across
        /// the SAME surfaces StructureDamageVisuals scans. Deliberately mirrors that
        /// scan rather than sharing code with it: if the two ever diverge, the probe
        /// reporting a structure the visuals ignore (or vice versa) is itself a finding.
        /// </summary>
        private void CollectBurning(List<Row> into)
        {
            AddIfBurning<WallSegment>(into, "wall", c => HpOf(c), c => BrokenOf(c));
            AddIfBurning<Building>(into, "building",
                c => c is Building b ? b.HpFraction : 1f,
                c => c is Building b && b.IsDestroyed);
            AddIfBurning<Gate>(into, "gate", c => HpOf(c), c => BrokenOf(c));
            AddIfBurning<Tower>(into, "tower",
                c => c is Tower t ? t.HpFraction : 1f,
                c => c is Tower t && t.IsBroken);
            AddIfBurning<DefenseTower>(into, "defensetower",
                c => c is DefenseTower t ? t.HpFraction : 1f,
                c => c is DefenseTower t && t.IsBroken);
            AddIfBurning<ArcaneTower>(into, "arcanetower",
                c => c is ArcaneTower t ? t.HpFraction : 1f,
                c => c is ArcaneTower t && t.IsBroken);
            AddIfBurning<DeNelle.Village.World.HarvestSite>(into, "harvestsite",
                c => c is DeNelle.Village.World.HarvestSite h ? h.HpFraction : 1f,
                c => c is DeNelle.Village.World.HarvestSite h && h.IsBroken);

            // Collectors come from the registry (no scene scan), matching StructureDamageVisuals.
            float collectorLine = DamageStatesCatalog.Fire("collector") + ReportMargin;
            foreach (var c in ResourceCollectorRegistry.All)
            {
                if (c == null) continue;
                float hp = c.HpFraction;
                if (!c.IsBroken && hp > collectorLine) continue;
                into.Add(BuildRow(c, "ResourceCollector", c.BuildingId, hp, c.IsBroken));
            }
        }

        private void AddIfBurning<T>(List<Row> into, string typeKey,
            System.Func<Component, float> hp, System.Func<Component, bool> broken) where T : Component
        {
            float line = DamageStatesCatalog.Fire(typeKey) + ReportMargin;
            foreach (var c in FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (c == null) continue;
                bool isBroken = broken(c);
                float f = hp(c);
                if (!isBroken && f > line) continue;
                into.Add(BuildRow(c, typeof(T).Name, c.gameObject.name, f, isBroken));
            }
        }

        private static Row BuildRow(Component c, string typeName, string name, float hp, bool broken)
        {
            // PlacedStructure is the build-mode placement marker: present => the player
            // built it, absent => it is a baked map structure. Reported, never acted on.
            bool placed = c.GetComponentInParent<PlacedStructure>() != null;
            // Tap-to-select only reaches what RepairTarget can wrap (wall / gate /
            // building). Towers, harvest sites and collectors wrap to null, so a tap on
            // one is silently ignored no matter how damaged it is.
            bool tappable = RepairTarget.TryWrap(c) != null;
            return new Row
            {
                Key = c,
                Name = string.IsNullOrEmpty(name) ? typeName : name,
                TypeName = typeName,
                Hp = hp,
                Broken = broken,
                Placed = placed,
                TapRepairable = tappable,
            };
        }

        /// <summary>HP fraction via the uniform RepairTarget view (walls / gates / buildings).</summary>
        private static float HpOf(Component c)
        {
            var t = RepairTarget.TryWrap(c);
            return t != null && t.IsValid ? 1f - t.DamageFraction : 1f;
        }

        /// <summary>Destroyed test via the uniform RepairTarget view.</summary>
        private static bool BrokenOf(Component c)
        {
            var t = RepairTarget.TryWrap(c);
            return t != null && t.IsValid && t.DamageFraction >= 0.999f;
        }
    }
}
