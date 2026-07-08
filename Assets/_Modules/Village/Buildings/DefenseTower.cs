// =============================================================================
// DefenseTower — the auto-firing defensive structure (defensive-catalog v0 test).
// -----------------------------------------------------------------------------
// Proves "placement = role": an Archer tower on the GROUND (CanHitAir = false,
// short range) can't touch a flying dragon; a Wizard tower on the WALL-WALK
// (CanHitAir = true, long range, elevated) can. Targeting is by ROLE PRIORITY —
// the owner's "scamper to the DPS and healers" — squishy backline first.
//
// Reuses: IDamageable (DeNelle.Core.Combat) for find+damage, EnemyBrain.Role for
// priority, ProjectileMover for the visual bolt. All data-tunable.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Who a tower fights. <see cref="PlayerOwned"/> is the default and the legacy
    /// behaviour — every player-built / arena defender tower shoots Hostile enemies
    /// exactly as before. <see cref="EnemyOwned"/> flips the allegiance: a garrison
    /// turret targets the PLAYER PARTY (hero + companions) instead, damaging them
    /// through the same <see cref="IDamageableStructure"/> seam the enemy
    /// contact/ranged attacks already use on the hero.
    /// </summary>
    public enum TowerAllegiance
    {
        /// <summary>Default — shoots <see cref="CombatFaction.Hostile"/> enemies (legacy).</summary>
        PlayerOwned = 0,
        /// <summary>Garrison turret — shoots the player party (hero + companions).</summary>
        EnemyOwned = 1,
    }

    public sealed class DefenseTower : MonoBehaviour
    {
        public float Range       = 14f;
        public float Damage      = 8f;
        public float FireRate    = 1.2f;   // shots per second
        public bool  CanHitAir   = false;  // ground archers: false · wall wizards: true
        public float AirThreshold = 3.5f;  // target above this Y counts as "flying"
        public Color BoltColor   = Color.white;
        public DamageElement Element = DamageElement.None;

        // TOWER IDENTITY (owner 2026-07-08 "ballista shoots arrows not round pellet" /
        // "arcane casts spells"): per-catalog-entry projectile VISUAL style. Set by
        // StructureFactory from RepoProps.projectileStyle ("pellet"|"bolt"|"spell";
        // null/empty/unknown -> pellet, the legacy sphere). Visual only — damage,
        // targeting and travel logic are untouched.
        public string ProjectileStyle = null;

        /// <summary>Resolved projectile visual archetype (see <see cref="ProjectileStyle"/>).</summary>
        private enum BoltStyle { Pellet, Bolt, Spell }
        private BoltStyle _style;
        private bool _styleResolved;

        // Allegiance — PlayerOwned (default) preserves every existing tower's
        // behaviour byte-for-byte; EnemyOwned garrison turrets target the player
        // party. Set by the spawner (GarrisonController) for garrison towers.
        public TowerAllegiance Allegiance = TowerAllegiance.PlayerOwned;

        // Elevation perk (wall-mounted defense): a tower seated on a wall-walk TOP gets the
        // high-ground range/LOS advantage. 1 = ground (no bonus); BaseLayoutLoader.Spawn sets
        // it (e.g. 1.25) when the structure is wall-mounted (PlacedStructureData.wallMounted).
        // A MULTIPLIER on EffectiveRange, so it survives tier upgrades (ApplyTierStats recomputes
        // the base Range from the catalog, never touching this factor). Bounded by the spawner.
        public float ElevationRangeMult = 1f;

        private float _cd;
        private float _scan;
        private readonly List<IDamageable> _hostiles = new List<IDamageable>();

        // EnemyOwned target list — the player party (hero HeroHealth + companions
        // StoryCompanion), both IDamageableStructure (the seam enemies already hit
        // the hero through). Only populated/used in EnemyOwned mode.
        private readonly List<IDamageableStructure> _partyTargets = new List<IDamageableStructure>();

        // ── Targeting indicator (cheap persistent LineRenderer aim-beam) ──────
        // A single, reused LineRenderer drawn from the muzzle to the locked
        // target while one is acquired. Created lazily, never per-shot — towers
        // fire often, so this stays allocation-free in the hot loop. Hidden
        // (disabled) whenever there is no valid target.
        private LineRenderer _aimBeam;
        private Material      _aimBeamMat;

        // WO-430 — PLAYER-owned towers get the Arcane Tower tier perks (towerDamageMult /
        // towerRangeMult). LIVE-READ (cheap, no current-HP problem) so the tier-4 Arcane
        // Overload temp-empower can buff dynamically. Enemy garrison turrets use base stats.
        private float EffectiveDamage => Allegiance == TowerAllegiance.PlayerOwned
            ? Damage * DeNelle.Core.State.ModifierService.Active.TowerDamageMult : Damage;
        private float EffectiveRange => (Allegiance == TowerAllegiance.PlayerOwned
            ? Range * DeNelle.Core.State.ModifierService.Active.TowerRangeMult : Range) * ElevationRangeMult;

        private void Update()
        {
            // EnemyOwned garrison turret — target the player party instead of
            // Hostile enemies. Fully separate path so PlayerOwned stays identical.
            if (Allegiance == TowerAllegiance.EnemyOwned)
            {
                UpdateEnemyOwned();
                return;
            }

            _scan -= Time.deltaTime;
            if (_scan <= 0f) { Rescan(); _scan = 0.4f; }   // refresh target list a few times/sec

            // Acquire once per frame so the aim-beam tracks the current target
            // even between shots (the indicator reads "this tower is locked on").
            var target = Acquire();
            UpdateAimBeam(target);

            _cd -= Time.deltaTime;
            if (_cd > 0f) return;
            if (target == null) return;
            _cd = 1f / Mathf.Max(0.1f, FireRate);
            Fire(target);
        }

        // ─────────────────────────────────────────────────────────────────────
        // EnemyOwned (garrison turret) — mirror of the PlayerOwned tick but the
        // target set is the PLAYER PARTY (hero + companions) and damage routes
        // through IDamageableStructure.ApplyContactDamage — the SAME seam the
        // enemy melee/ranged attacks use on the hero (Enemy.RangedAttack →
        // structure.ApplyContactDamage). No new damage API.
        // ─────────────────────────────────────────────────────────────────────
        private void UpdateEnemyOwned()
        {
            _scan -= Time.deltaTime;
            if (_scan <= 0f) { RescanParty(); _scan = 0.4f; }

            IDamageableStructure target = AcquireParty(out Vector3 targetPos);
            UpdateAimBeamAt(targetPos, target != null);

            _cd -= Time.deltaTime;
            if (_cd > 0f) return;
            if (target == null) return;
            _cd = 1f / Mathf.Max(0.1f, FireRate);
            FireAtParty(target, targetPos);
        }

        /// <summary>Rebuilds the player-party target list (hero + companions).</summary>
        private void RescanParty()
        {
            _partyTargets.Clear();
            var hero = HeroHealth.Instance;
            if (hero != null) _partyTargets.Add(hero);
            foreach (var c in FindObjectsByType<StoryCompanion>())
                if (c != null) _partyTargets.Add(c);
        }

        /// <summary>
        /// Nearest in-range, alive party member. Reuses the same range + air gate
        /// as the player path; party members are ground units so the air gate is
        /// effectively a no-op for them (a downed hero is skipped via IsAlive).
        /// </summary>
        private IDamageableStructure AcquireParty(out Vector3 bestPos)
        {
            IDamageableStructure best = null;
            bestPos = default;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _partyTargets.Count; i++)
            {
                var d = _partyTargets[i];
                var mb = d as MonoBehaviour;
                if (mb == null || d == null || !d.IsAlive) continue;
                Vector3 p = mb.transform.position;
                float sqr = (p - transform.position).sqrMagnitude;
                if (sqr > Range * Range) continue;
                if (p.y > AirThreshold && !CanHitAir) continue;
                if (sqr < bestSqr) { bestSqr = sqr; best = d; bestPos = p; }
            }
            return best;
        }

        /// <summary>
        /// Fire on a party member: identical bolt visual + muzzle flash as the
        /// player path, but damage lands via ApplyContactDamage (the hero/companion
        /// IDamageableStructure seam).
        /// </summary>
        private void FireAtParty(IDamageableStructure target, Vector3 targetPos)
        {
            using var _ = FlowTrace.Enter("DefenseTower", "FireAtParty (EnemyOwned)");
            Vector3 muzzle = transform.position + Vector3.up * 2f;

            // GUARD the bolt spawn: a thrown CreatePrimitive/material step must NOT abort the
            // shot (and orphan a half-built bolt). On failure we still apply damage below so the
            // turret stays functional — never silently dead.
            GameObject bolt = SpawnProjectileVisual(muzzle, targetPos + Vector3.up * 1f, "party");
            VerifyBoltRenders(bolt, "party");

            PlayFireVfx(muzzle, targetPos + Vector3.up * 1f);

            target?.ApplyContactDamage(Damage);   // same seam the enemy attacks use on the hero
        }

        private void OnDisable()
        {
            // Don't leave a stale beam pointing at a dead target when the tower
            // is disabled (pooled, swapped, destroyed-soon).
            if (_aimBeam != null) _aimBeam.enabled = false;
        }

        private void Rescan()
        {
            // PERF (overworld 1fps fix): the old scan was FindObjectsByType<MonoBehaviour>,
            // which enumerates EVERY MonoBehaviour in ALL loaded scenes (the additive
            // overworld terrain/props/NPCs = tens of thousands) on every 0.4s tick, per
            // tower — hundreds of ms/frame regardless of enemy count. Scan only the two
            // CONCRETE hostile IDamageable implementors instead (engine-filtered, returns
            // just the live enemy bodies + the dragon). Identical target set, no full-scene
            // enumeration. Same Faction==Hostile gate preserved.
            _hostiles.Clear();
            foreach (var d in FindObjectsByType<EnemyDamageable>())
            {
                if (d == null || d.Faction != CombatFaction.Hostile) continue;
                // TOWERS DEFEND THE TOWN AUTONOMOUSLY (owner 2026-06-28): roaming overworld
                // encounter reps (RepEngageWatcher) used to be SKIPPED here as "un-killable
                // Hp=9999 hooks". That is no longer true — reps are now killable (Hp=150,
                // OverworldEncounterSpawner) and tower damage does NOT trigger the arena
                // (RepEngageWatcher.RangedHitsEngage=false; only near-CONTACT with the HERO
                // pops the battle scene). So towers SHOULD target + damage + kill reps in
                // range: that IS the automated town defense (player focuses on leveling/
                // foraging; the town defends itself). The arena still fires only when a rep
                // engages the hero. Every hostile faction in range is acquired — no skip.
                _hostiles.Add(d);
            }
            foreach (var d in FindObjectsByType<DragonBoss>())
                if (d != null && d.Faction == CombatFaction.Hostile)
                    _hostiles.Add(d);
        }

        private IDamageable Acquire()
        {
            IDamageable best = null;
            int   bestPri = int.MaxValue;
            float bestSqr = float.MaxValue;
            float range = EffectiveRange;   // WO-430 — Arcane Tower range perk (player towers)
            foreach (var d in _hostiles)
            {
                if (d == null || !d.IsAlive) continue;
                Vector3 p = d.WorldPosition;
                float sqr = (p - transform.position).sqrMagnitude;
                if (sqr > range * range) continue;
                if (p.y > AirThreshold && !CanHitAir) continue;   // ground tower can't reach a flier
                int pri = Priority(d);
                if (pri < bestPri || (pri == bestPri && sqr < bestSqr))
                {
                    bestPri = pri; bestSqr = sqr; best = d;
                }
            }
            return best;
        }

        // "Scamper to the DPS and healers" — squishy backline first, tanks last.
        private static int Priority(IDamageable d)
        {
            var mb = d as MonoBehaviour;
            var brain = mb != null ? mb.GetComponent<EnemyBrain>() : null;
            if (brain == null) return 2;   // bosses / unknown — middling
            switch (brain.Role)
            {
                case EnemyRole.Healer:   return 0;   // kill the healer first
                case EnemyRole.Ranged:   return 1;
                case EnemyRole.DPS:      return 1;
                case EnemyRole.MiniBoss: return 2;
                case EnemyRole.Tank:     return 3;   // tanks last
                default:                 return 2;
            }
        }

        private void Fire(IDamageable target)
        {
            // Hot loop (towers fire often): Throttle the entry trace to ~1/sec so it pinpoints
            // a live-firing tower without flooding the break-log.
            FlowTrace.Throttle("DefenseTower", $"fire:{GetInstanceID()}", 1f,
                $"Fire on '{(target as MonoBehaviour)?.name ?? "<target>"}' (dmg={EffectiveDamage:0.#}, element={Element}).");
            Vector3 muzzle = transform.position + Vector3.up * 2f;

            // GUARD the bolt spawn: a thrown CreatePrimitive/material step must NOT abort the
            // shot (and orphan a half-built bolt). Damage still lands below — the tower never
            // silently stops firing because one bolt's visual threw.
            GameObject bolt = SpawnProjectileVisual(muzzle, target.WorldPosition + Vector3.up * 1f, "hostile");
            VerifyBoltRenders(bolt, "hostile");

            // ── Muzzle flash / cast ───────────────────────────────────────────
            // Brief pooled burst at the fire point each shot. Reuses the shared
            // VFXManager (object-pooled, quality-gated) — element-tinted; a Spell-
            // style tower plays a caster wind-up + an impact burst at the target
            // instead, so its shot reads as a CAST, not a turret muzzle flash.
            // Null-safe via the static API: a no-op if VFXManager isn't booted.
            PlayFireVfx(muzzle, target.WorldPosition + Vector3.up * 1f);

            target.TakeDamage(EffectiveDamage, Element);   // hitscan damage on fire (WO-430: + Arcane Tower damage perk)
        }

        // RENDER-VERIFY (TGVRU "V"; mirrors Tower.VerifyVisualRendersNow): a spawned bolt MUST
        // carry >=1 ENABLED Renderer with a sharedMesh, else it's an invisible projectile (the
        // "tower fires but nothing visible" symptom). Throttled (hot loop) + Fail-loud on a miss
        // so a capture self-reports a dud bolt; the shot's damage already landed regardless.
        private void VerifyBoltRenders(GameObject bolt, string kind)
        {
            if (bolt == null)
            {
                FlowTrace.Fail("DefenseTower", $"VerifyBolt ({kind}): bolt is null — spawn threw; no visible projectile (damage still applied).");
                return;
            }
            int enabled = 0, withMesh = 0;
            foreach (var r in bolt.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (r.enabled) enabled++;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) withMesh++;
            }
            if (enabled == 0 || withMesh == 0)
            {
                FlowTrace.Fail("DefenseTower",
                    $"VerifyBolt ({kind}) FAILED on '{bolt.name}': enabledRenderers={enabled} withMesh={withMesh} — invisible bolt.");
                return;
            }
            FlowTrace.Throttle("DefenseTower", $"boltok:{GetInstanceID()}", 1f,
                $"VerifyBolt ({kind}) ok: enabledRenderers={enabled} withMesh={withMesh}.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Projectile VISUAL styles (owner 2026-07-08 tower-identity pass).
        // Data-driven off the catalog row's optional "projectileStyle" string;
        // pure presentation — travel (ProjectileMover), damage and targeting
        // are byte-identical across styles.
        //   Pellet — legacy emissive sphere (default / fallback).
        //   Bolt   — elongated shaft + tip, oriented along velocity (ballista
        //            arrow; ProjectileMover already faces travel each frame).
        //   Spell  — glowing arcane orb; PlayFireVfx adds cast + impact bursts.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Resolve the catalog style string once (Step on resolution, Warn +
        /// pellet fallback on an unknown value — per docs/INSTRUMENTATION_STANDARD.md).</summary>
        private BoltStyle ResolveStyle()
        {
            if (_styleResolved) return _style;
            _styleResolved = true;

            string s = ProjectileStyle != null ? ProjectileStyle.Trim().ToLowerInvariant() : string.Empty;
            switch (s)
            {
                case "":
                case "pellet": _style = BoltStyle.Pellet; break;
                case "bolt":   _style = BoltStyle.Bolt;   break;
                case "spell":  _style = BoltStyle.Spell;  break;
                default:
                    FlowTrace.Warn("DefenseTower",
                        $"'{name}': unknown projectileStyle '{ProjectileStyle}' — falling back to pellet (valid: pellet|bolt|spell).");
                    _style = BoltStyle.Pellet;
                    return _style;
            }
            FlowTrace.Step("DefenseTower",
                $"'{name}': projectile style resolved -> {_style} (catalog projectileStyle='{ProjectileStyle ?? "<null>"}', element={Element}).");
            return _style;
        }

        /// <summary>
        /// Build + launch the per-shot projectile visual for the resolved style.
        /// Guarded: a thrown CreatePrimitive/material step logs + returns whatever was
        /// built (possibly null) — the caller's damage still lands, and VerifyBoltRenders
        /// self-reports an invisible shot. Same non-pooled ProjectileMover path as ever.
        /// </summary>
        private GameObject SpawnProjectileVisual(Vector3 muzzle, Vector3 targetPos, string kind)
        {
            GameObject bolt = null;
            Guard.Try("DefenseTower", $"spawn {kind} projectile", () =>
            {
                switch (ResolveStyle())
                {
                    case BoltStyle.Bolt:  bolt = BuildBoltVisual();  break;
                    case BoltStyle.Spell: bolt = BuildSpellVisual(); break;
                    default:              bolt = BuildPelletVisual(); break;
                }
                bolt.transform.position = muzzle;
                // Face the target immediately so the first rendered frame of an elongated
                // bolt already lies along the flight line (ProjectileMover re-faces per frame).
                Vector3 dir = targetPos - muzzle;
                if (dir.sqrMagnitude > 0.0001f) bolt.transform.rotation = Quaternion.LookRotation(dir);
                bolt.AddComponent<ProjectileMover>().Launch(targetPos, 40f, CanHitAir ? 0.1f : 0.35f);
            });
            return bolt;
        }

        /// <summary>Legacy pellet — the emissive 0.4 m sphere (default style).</summary>
        private GameObject BuildPelletVisual()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Bolt";
            go.transform.localScale = Vector3.one * 0.4f;
            StripCollider(go);
            ApplyBoltMaterial(go, BoltColor, 2f);
            return go;
        }

        /// <summary>
        /// Ballista/archer BOLT — a thin elongated shaft (cylinder laid along +Z) with a
        /// short steel tip cube at the nose. Code-built primitives, one shared parent the
        /// mover rotates along velocity. Reads as an arrow, not a pellet.
        /// </summary>
        private GameObject BuildBoltVisual()
        {
            var root = new GameObject("Bolt");

            // Shaft: cylinder's long axis is local Y — rotate X+90 so it lies along the
            // parent's forward (+Z), the direction ProjectileMover faces.
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Shaft";
            shaft.transform.SetParent(root.transform, false);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shaft.transform.localScale    = new Vector3(0.08f, 0.55f, 0.08f);   // 1.1 m long, thin
            StripCollider(shaft);
            // Wooden-dark shaft: the tower's BoltColor darkened so element tint still reads.
            ApplyBoltMaterial(shaft, BoltColor * 0.55f, 0.6f);

            // Tip: a small cube at the nose, yaw/pitched 45° so its corner leads — a
            // cheap diamond arrowhead silhouette.
            var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tip.name = "Tip";
            tip.transform.SetParent(root.transform, false);
            tip.transform.localPosition = new Vector3(0f, 0f, 0.62f);
            tip.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
            tip.transform.localScale    = Vector3.one * 0.12f;
            StripCollider(tip);
            ApplyBoltMaterial(tip, new Color(0.75f, 0.78f, 0.82f), 1.2f);   // steel glint

            return root;
        }

        /// <summary>
        /// Arcane SPELL orb — a larger, strongly emissive sphere that reads as a glowing
        /// projectile (the cast/impact VFX around it come from PlayFireVfx).
        /// </summary>
        private GameObject BuildSpellVisual()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SpellOrb";
            go.transform.localScale = Vector3.one * 0.5f;
            StripCollider(go);
            // Hot emissive violet-leaning glow; BoltColor still tints per tower.
            ApplyBoltMaterial(go, BoltColor, 4f);
            return go;
        }

        private static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
        }

        /// <summary>Shared URP/Lit emissive material apply (the legacy pellet look, tunable).</summary>
        private static void ApplyBoltMaterial(GameObject go, Color color, float emission)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) return;
            var m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * emission); }
            var r = go.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = m;
        }

        /// <summary>
        /// Per-shot fire VFX. Pellet/Bolt keep the legacy element-tinted muzzle burst;
        /// Spell style reads as a CAST — wind-up at the muzzle + an element impact burst
        /// at the target point. All through the pooled, null-safe VFXManager static API.
        /// </summary>
        private void PlayFireVfx(Vector3 muzzle, Vector3 targetPos)
        {
            if (ResolveStyle() == BoltStyle.Spell)
            {
                VFXManager.Play(VFXType.Cast_MageCharge, muzzle);
                VFXManager.Play(ImpactVfxFor(Element), targetPos);
                return;
            }
            VFXManager.Play(MuzzleVfxFor(Element), muzzle);
        }

        /// <summary>Maps the tower's damage element to a point-impact VFXType (spell style).</summary>
        private static VFXType ImpactVfxFor(DamageElement element)
        {
            switch (element)
            {
                case DamageElement.Flame: return VFXType.Impact_Flame;
                case DamageElement.Ice:   return VFXType.Impact_Ice;
                case DamageElement.None:  return VFXType.Impact_Physical;
                default:                  return VFXType.Impact_Aether;   // Aether
            }
        }

        /// <summary>Maps the tower's damage element to its tower-bolt muzzle VFXType.</summary>
        private static VFXType MuzzleVfxFor(DamageElement element)
        {
            switch (element)
            {
                case DamageElement.Flame: return VFXType.Projectile_TowerFire;
                case DamageElement.Ice:   return VFXType.Projectile_TowerIce;
                default:                  return VFXType.Projectile_TowerArcane;   // None / Aether
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Targeting indicator — a thin aim-beam from the muzzle to the locked
        // target. Cheap (one reused LineRenderer + one emissive material), and
        // readable (faint, element-tinted, slightly transparent so it doesn't
        // clutter the screen when many towers are firing).
        // ─────────────────────────────────────────────────────────────────────
        private void UpdateAimBeam(IDamageable target)
        {
            if (target == null || !target.IsAlive)
            {
                if (_aimBeam != null) _aimBeam.enabled = false;
                return;
            }

            EnsureAimBeam();
            if (_aimBeam == null) return;   // (defensive — EnsureAimBeam always builds it)

            Vector3 muzzle = transform.position + Vector3.up * 2f;
            Vector3 hit    = target.WorldPosition + Vector3.up * 1f;
            _aimBeam.enabled = true;
            _aimBeam.SetPosition(0, muzzle);
            _aimBeam.SetPosition(1, hit);
        }

        /// <summary>
        /// Position-based aim-beam update for the EnemyOwned party-target path
        /// (the party uses IDamageableStructure, which has no WorldPosition).
        /// Mirrors <see cref="UpdateAimBeam"/> exactly but takes an explicit point.
        /// </summary>
        private void UpdateAimBeamAt(Vector3 targetWorldPos, bool hasTarget)
        {
            if (!hasTarget)
            {
                if (_aimBeam != null) _aimBeam.enabled = false;
                return;
            }

            EnsureAimBeam();
            if (_aimBeam == null) return;

            Vector3 muzzle = transform.position + Vector3.up * 2f;
            Vector3 hit    = targetWorldPos + Vector3.up * 1f;
            _aimBeam.enabled = true;
            _aimBeam.SetPosition(0, muzzle);
            _aimBeam.SetPosition(1, hit);
        }

        private void EnsureAimBeam()
        {
            if (_aimBeam != null) return;

            using var _ = FlowTrace.Enter("DefenseTower", "EnsureAimBeam (build lock-on LineRenderer)");
            var beamGo = new GameObject("AimBeam");
            beamGo.transform.SetParent(transform, false);
            _aimBeam = beamGo.AddComponent<LineRenderer>();
            _aimBeam.useWorldSpace   = true;
            _aimBeam.positionCount   = 2;
            _aimBeam.numCapVertices  = 0;
            _aimBeam.alignment       = LineAlignment.View;
            _aimBeam.textureMode     = LineTextureMode.Stretch;
            _aimBeam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _aimBeam.receiveShadows  = false;

            // Thin, tapered, subtle — a hint of a lock-on laser, not a fat beam.
            _aimBeam.startWidth = 0.05f;
            _aimBeam.endWidth   = 0.02f;

            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh != null)
            {
                _aimBeamMat = new Material(sh);
                Color tint = BoltColor; tint.a = 0.35f;   // faint, see-through
                if (_aimBeamMat.HasProperty("_BaseColor")) _aimBeamMat.SetColor("_BaseColor", tint);
                if (_aimBeamMat.HasProperty("_Color"))     _aimBeamMat.SetColor("_Color", tint);
                _aimBeam.sharedMaterial = _aimBeamMat;
            }

            Color c = BoltColor; c.a = 0.45f;
            _aimBeam.startColor = c;
            Color e = BoltColor; e.a = 0.15f;   // fades toward the target end
            _aimBeam.endColor = e;

            _aimBeam.enabled = false;   // off until a target is locked
        }

        private void OnDestroy()
        {
            if (_aimBeamMat != null) Destroy(_aimBeamMat);
        }
    }
}
