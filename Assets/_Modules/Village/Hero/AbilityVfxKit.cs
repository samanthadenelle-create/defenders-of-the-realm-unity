// =============================================================================
// AbilityVfxKit — procedural, asset-free ability VFX (owner WO-35: replace the
// "random dots" placeholder with real per-ability effects).
// -----------------------------------------------------------------------------
// Designed by the creative VFX pass. One reusable entry point, SpawnAbilityVfx,
// dispatches to a distinct treatment per effect kind:
//   Strike  → fast bright tracer toward the foe + impact spark
//   Snare   → strike + a lingering ground ring at the foe's feet
//   Aoe     → expanding flat ground ring + upward shard burst + freeze flash
//   Cleave  → same nova shape, heavier/warmer
//   Heal    → rising warm column + soft pulse ring (sustained)
//   Meteor  → downward fiery streak (trail) → ground shockwave + ember scatter
// plus a short URP point-light flash per cast.
//
// URP-safe: every system uses the URP unlit particle shader + a generated soft
// round glow texture (no binary art assets, fresh-clone safe). Colour-over-
// lifetime gives the hot-core → hue → dark-edge gradient that reads as "good
// VFX" instead of flat dots.
//
// CURVE-MODE NOTE: this project has hit "Particle Velocity curves must all be in
// the same mode" — so any velocityOverLifetime here sets x/y/z all in the SAME
// (Constant) mode. Most motion is shape-direction + startSpeed to sidestep it.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    public static class AbilityVfxKit
    {
        private static Texture2D s_softDot;

        // ── VFXManager bridge ─────────────────────────────────────────────────
        // When VFXManager is live and has a prefab wired for the requested type,
        // it handles pooling + art assets. The procedural builders below are the
        // fallback — they are ALWAYS available even with no art packages installed.

        /// <summary>
        /// Play a hero ability VFX. Tries VFXManager first (prefab pool), then
        /// falls through to the procedural SpawnAbilityVfxForClass builders.
        /// Use this instead of calling SpawnAbilityVfxForClass directly so future
        /// art swaps require no code changes.
        /// </summary>
        public static void PlayHeroAbility(AbilityEffect kind, Color color, Vector3 position,
                                           float radius, Vector3 targetHint, string heroClass)
        {
            // Map hero class + ability kind to a VFXType so VFXManager can route
            // to the correct prefab in VFXCatalog.
            var type = ResolveVFXType(kind, heroClass);

            // Try prefab path first.
            if (type != VFXType.None && VFXManager.Instance != null)
            {
                VFXManager.Play(type, position);
                return;
            }

            // Procedural fallback.
            SpawnAbilityVfxForClass(kind, color, position, radius, targetHint, heroClass);
        }

        private static VFXType ResolveVFXType(AbilityEffect kind, string heroClass)
        {
            switch ((heroClass ?? string.Empty).ToLowerInvariant())
            {
                // WO-226: the Cleric is a caster — it shares the Mage's arcane VFX set.
                case "cleric":
                case "mage":
                    return kind switch
                    {
                        AbilityEffect.Strike => VFXType.Projectile_ArcaneBolt,
                        AbilityEffect.Aoe    => VFXType.Impact_ExplosionAether,
                        AbilityEffect.Cleave => VFXType.Impact_Aether,
                        AbilityEffect.Heal   => VFXType.Impact_Heal,
                        AbilityEffect.Meteor => VFXType.Impact_ExplosionFire,
                        _                    => VFXType.Impact_Aether,
                    };
                case "ranger":
                    return kind switch
                    {
                        AbilityEffect.Strike => VFXType.Projectile_Arrow,
                        AbilityEffect.Snare  => VFXType.Projectile_FlameArrow,
                        _                    => VFXType.Projectile_Arrow,
                    };
                case "knight":
                    return kind switch
                    {
                        AbilityEffect.Strike => VFXType.Impact_Physical,
                        AbilityEffect.Aoe    => VFXType.Impact_ShockwaveRing,
                        AbilityEffect.Cleave => VFXType.Impact_ShockwaveRing,
                        _                    => VFXType.Impact_Physical,
                    };
                default:
                    return VFXType.None;
            }
        }

        /// <summary>Spawns the VFX for an ability effect. position = effect centre;
        /// targetHint = the foe / impact point (for tracers + meteor fall).</summary>
        public static void SpawnAbilityVfx(AbilityEffect kind, Color color, Vector3 position,
                                           float radius, Vector3 targetHint)
        {
            var host = new GameObject("AbilityVFX_" + kind);
            host.transform.position = position;

            Color core = Color.Lerp(color, Color.white, 0.6f);
            Color body = new Color(color.r, color.g, color.b, 1f);
            Color edge = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 1f);
            float r = Mathf.Max(0.6f, radius);

            switch (kind)
            {
                case AbilityEffect.Strike:
                    BuildStrike(host, core, body, edge, position, targetHint);
                    FlashLight(host, body, targetHint, 6f, 3f, 0.12f);
                    break;
                case AbilityEffect.Snare:
                    BuildStrike(host, core, body, edge, position, targetHint);
                    BuildGroundRing(host, core, body, targetHint, 0.7f, 1.0f, 24, 0f);
                    FlashLight(host, body, targetHint, 3f, 3.5f, 0.5f);
                    break;
                case AbilityEffect.Aoe:
                    BuildNova(host, core, body, edge, position, r, 1.2f);
                    FlashLight(host, core, position, 8f, r + 2f, 0.3f);
                    break;
                case AbilityEffect.Cleave:
                    BuildNova(host, core, body, edge, position, r, 0.8f);
                    FlashLight(host, body, position, 7f, r + 2f, 0.2f);
                    break;
                case AbilityEffect.Heal:
                    BuildHeal(host, core, body, position, r);
                    FlashLight(host, body, position + Vector3.up, 4f, r + 1f, 1.0f);
                    break;
                case AbilityEffect.Meteor:
                    BuildMeteor(host, core, body, edge, position, r);
                    FlashLight(host, body, position, 12f, r + 4f, 0.4f);
                    break;
                default:
                    BuildNova(host, core, body, edge, position, r, 1.0f);
                    FlashLight(host, body, position, 6f, r + 2f, 0.3f);
                    break;
            }

            foreach (var ps in host.GetComponentsInChildren<ParticleSystem>()) ps.Play();
            Object.Destroy(host, 2.6f);
        }

        /// <summary>
        /// WO-37: class-flavoured dispatch. Each hero class now gets a real,
        /// distinct SHAPE — not just a recolour — so the VFX reads as the class's
        /// own attack:
        ///   Knight → heavy MELEE: ground impact/shockwave ring + bright sparks +
        ///            a short flash, steel-gold. NEVER the long arcane tracer.
        ///   Ranger → tight ARROW: a fast thin green streak to the foe + a small
        ///            leaf-green impact burst, leaf-green palette.
        ///   Mage / default → the existing arcane <see cref="SpawnAbilityVfx"/>.
        /// Deliberately a SEPARATE method from <see cref="SpawnAbilityVfx"/> so
        /// PetAttackVfxBridge's 5-arg reflection bind on "SpawnAbilityVfx" stays
        /// unambiguous (adding an overload there would break it).
        /// </summary>
        public static void SpawnAbilityVfxForClass(AbilityEffect kind, Color color, Vector3 position,
                                                   float radius, Vector3 targetHint, string heroClass)
        {
            switch ((heroClass ?? string.Empty).ToLowerInvariant())
            {
                case "knight":
                    SpawnKnightVfx(kind, color, position, radius, targetHint);
                    break;
                case "ranger":
                    SpawnRangerVfx(kind, color, position, radius, targetHint);
                    break;
                default: // mage = the ability's element colour, full arcane treatment
                    SpawnAbilityVfx(kind, color, position, radius, targetHint);
                    break;
            }
        }

        // ── Knight — heavy melee, no arcane tracer ─────────────────────────────
        private static void SpawnKnightVfx(AbilityEffect kind, Color color, Vector3 position,
                                           float radius, Vector3 targetHint)
        {
            // Steel-gold palette regardless of the incoming element colour.
            Color steel = new Color(0.92f, 0.86f, 0.70f);
            Color core = Color.Lerp(steel, Color.white, 0.6f);
            Color body = new Color(steel.r, steel.g, steel.b, 1f);
            Color edge = new Color(steel.r * 0.5f, steel.g * 0.46f, steel.b * 0.38f, 1f); // dark steel
            float r = Mathf.Max(0.6f, radius);

            var host = new GameObject("AbilityVFX_Knight_" + kind);
            host.transform.position = position;

            switch (kind)
            {
                // Single-target blows → impact shockwave ring at the foe + sparks.
                case AbilityEffect.Strike:
                case AbilityEffect.Snare:
                {
                    Vector3 hit = (targetHint - position).sqrMagnitude > 0.04f ? targetHint : position;
                    BuildKnightImpact(host, core, body, edge, hit);
                    if (kind == AbilityEffect.Snare)
                        BuildGroundRing(host, core, body, hit, 0.7f, 1.0f, 24, 0f);
                    FlashLight(host, body, hit, 7f, 4f, 0.18f);
                    break;
                }
                // Sweeping blows → a heavier nova (more shards, lower arc, gold).
                case AbilityEffect.Aoe:
                    BuildNova(host, core, body, edge, position, r, 1.4f);
                    BuildKnightImpact(host, core, body, edge, position);
                    FlashLight(host, core, position, 9f, r + 2f, 0.3f);
                    break;
                case AbilityEffect.Cleave:
                    BuildNova(host, core, body, edge, position, r, 1.2f);
                    BuildKnightImpact(host, core, body, edge, position);
                    FlashLight(host, body, position, 8f, r + 2f, 0.22f);
                    break;
                // Utility kinds have no melee analogue → keep the base shape, gold.
                case AbilityEffect.Heal:
                    BuildHeal(host, core, body, position, r);
                    FlashLight(host, body, position + Vector3.up, 4f, r + 1f, 1.0f);
                    break;
                case AbilityEffect.Meteor:
                    BuildMeteor(host, core, body, edge, position, r);
                    FlashLight(host, body, position, 12f, r + 4f, 0.4f);
                    break;
                default:
                    BuildKnightImpact(host, core, body, edge, position);
                    FlashLight(host, body, position, 7f, r + 2f, 0.25f);
                    break;
            }

            foreach (var ps in host.GetComponentsInChildren<ParticleSystem>()) ps.Play();
            Object.Destroy(host, 2.6f);
        }

        /// <summary>A grounded shockwave ring + a bright spark fan — a steel blow
        /// landing, no long tracer.</summary>
        private static void BuildKnightImpact(GameObject host, Color core, Color body, Color edge, Vector3 at)
        {
            // Flat ground shockwave ring expanding from the impact point.
            var ring = NewPS(host, "KnightShock", at + Vector3.up * 0.05f);
            var rm = ring.main; rm.startLifetime = 0.4f; rm.startSpeed = 6f; rm.startSize = 0.3f;
            var rsh = ring.shape; rsh.enabled = true; rsh.shapeType = ParticleSystemShapeType.Circle;
            rsh.radius = 0.3f; rsh.radiusThickness = 0f; rsh.rotation = new Vector3(90f, 0f, 0f);
            Burst(ring, 36);
            SizeOverLife(ring, 1f, 0.2f);
            ApplyCOL(ring, Color.white, body, edge);

            // Bright spark fan kicked up by the blow (gravity-pulled).
            var spark = NewPS(host, "KnightSparks", at + Vector3.up * 0.1f);
            var sm = spark.main;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            sm.gravityModifier = 1.2f;
            var ssh = spark.shape; ssh.enabled = true; ssh.shapeType = ParticleSystemShapeType.Hemisphere; ssh.radius = 0.25f;
            Burst(spark, 22);
            Stretch(spark, 0.06f, 2.2f); // little flying sparks read as streaks
            ApplyCOL(spark, core, body, edge);
        }

        // ── Ranger — a tight, fast arrow ───────────────────────────────────────
        private static void SpawnRangerVfx(AbilityEffect kind, Color color, Vector3 position,
                                           float radius, Vector3 targetHint)
        {
            // Leaf-green palette regardless of the incoming element colour.
            Color leaf = new Color(0.48f, 0.95f, 0.55f);
            Color core = Color.Lerp(leaf, Color.white, 0.6f);
            Color body = new Color(leaf.r, leaf.g, leaf.b, 1f);
            Color edge = new Color(leaf.r * 0.45f, leaf.g * 0.55f, leaf.b * 0.42f, 1f);
            float r = Mathf.Max(0.6f, radius);

            var host = new GameObject("AbilityVFX_Ranger_" + kind);
            host.transform.position = position;

            switch (kind)
            {
                // Aimed shots → a thin fast streak + a small leaf burst at impact.
                case AbilityEffect.Strike:
                case AbilityEffect.Snare:
                {
                    BuildRangerArrow(host, core, body, edge, position, targetHint);
                    if (kind == AbilityEffect.Snare)
                    {
                        Vector3 hit = (targetHint - position).sqrMagnitude > 0.04f ? targetHint : position;
                        BuildGroundRing(host, core, body, hit, 0.7f, 1.0f, 24, 0f);
                    }
                    FlashLight(host, body, targetHint, 5f, 3f, 0.14f);
                    break;
                }
                // Volleys → arrow plus the base nova, leaf-green.
                case AbilityEffect.Aoe:
                    BuildRangerArrow(host, core, body, edge, position, targetHint);
                    BuildNova(host, core, body, edge, position, r, 1.1f);
                    FlashLight(host, core, position, 7f, r + 2f, 0.3f);
                    break;
                case AbilityEffect.Cleave:
                    BuildRangerArrow(host, core, body, edge, position, targetHint);
                    BuildNova(host, core, body, edge, position, r, 0.9f);
                    FlashLight(host, body, position, 6f, r + 2f, 0.22f);
                    break;
                case AbilityEffect.Heal:
                    BuildHeal(host, core, body, position, r);
                    FlashLight(host, body, position + Vector3.up, 4f, r + 1f, 1.0f);
                    break;
                case AbilityEffect.Meteor:
                    BuildMeteor(host, core, body, edge, position, r);
                    FlashLight(host, body, position, 12f, r + 4f, 0.4f);
                    break;
                default:
                    BuildRangerArrow(host, core, body, edge, position, targetHint);
                    FlashLight(host, body, targetHint, 5f, r + 2f, 0.18f);
                    break;
            }

            foreach (var ps in host.GetComponentsInChildren<ParticleSystem>()) ps.Play();
            Object.Destroy(host, 2.6f);
        }

        /// <summary>A single tight thin arrow streak to the foe + a small green
        /// leaf-scatter at the impact point.</summary>
        private static void BuildRangerArrow(GameObject host, Color core, Color body, Color edge,
                                             Vector3 origin, Vector3 target)
        {
            Vector3 dir = target - origin; dir.y = 0f;
            bool hasFoe = dir.sqrMagnitude > 0.04f;
            if (!hasFoe) dir = Vector3.forward;
            dir.Normalize();

            // Thin fast streak — narrower cone, smaller size, longer stretch than
            // the Mage tracer so it reads as a single arrow, not a spray.
            var arrow = NewPS(host, "ArrowStreak", origin + Vector3.up * 0.6f);
            arrow.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            var am = arrow.main;
            am.startLifetime = 0.14f;
            am.startSpeed = new ParticleSystem.MinMaxCurve(24f, 28f);
            am.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.1f); // thinner than Strike's 0.12-0.22
            var ash = arrow.shape; ash.enabled = true; ash.shapeType = ParticleSystemShapeType.Cone;
            ash.angle = 0.6f; ash.radius = 0.02f; // very tight
            Burst(arrow, 5);
            Stretch(arrow, 0.07f, 3.4f); // long, lean streak
            ApplyCOL(arrow, core, body, edge);

            // Small leaf-green burst where the arrow lands.
            Vector3 hit = hasFoe ? target : origin + dir * 1.5f;
            var leaves = NewPS(host, "LeafBurst", hit + Vector3.up * 0.4f);
            var lm = leaves.main;
            lm.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            lm.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            lm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
            lm.gravityModifier = 0.5f;
            var lsh = leaves.shape; lsh.enabled = true; lsh.shapeType = ParticleSystemShapeType.Sphere; lsh.radius = 0.12f;
            Burst(leaves, 12);
            SizeOverLife(leaves, 1f, 0f);
            ApplyCOL(leaves, core, body, edge);
        }

        // ── shared builders ────────────────────────────────────────────────────

        private static void BuildStrike(GameObject host, Color core, Color body, Color edge,
                                        Vector3 origin, Vector3 target)
        {
            Vector3 dir = target - origin; dir.y = 0f;
            bool hasFoe = dir.sqrMagnitude > 0.04f;
            if (!hasFoe) dir = Vector3.forward;
            dir.Normalize();

            // Tracer — a fast stretched streak from the caster toward the foe.
            var tracer = NewPS(host, "Tracer", origin);
            tracer.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            var tm = tracer.main;
            tm.startLifetime = 0.12f;
            tm.startSpeed = new ParticleSystem.MinMaxCurve(20f, 24f);
            tm.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            var tsh = tracer.shape; tsh.enabled = true; tsh.shapeType = ParticleSystemShapeType.Cone;
            tsh.angle = 2f; tsh.radius = 0.05f;
            Burst(tracer, 10);
            Stretch(tracer, 0.08f, 2.6f);
            ApplyCOL(tracer, core, body, edge);

            // Impact spark at the hit point.
            Vector3 hit = hasFoe ? target : origin + dir * 1.5f;
            var spark = NewPS(host, "Spark", hit);
            var sm = spark.main;
            sm.startLifetime = 0.25f;
            sm.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            sm.startSize = 0.12f;
            sm.gravityModifier = 0.3f;
            var ssh = spark.shape; ssh.enabled = true; ssh.shapeType = ParticleSystemShapeType.Sphere; ssh.radius = 0.1f;
            Burst(spark, 14);
            SizeOverLife(spark, 1f, 0f);
            ApplyCOL(spark, core, body, edge);
        }

        private static void BuildGroundRing(GameObject host, Color core, Color body, Vector3 at,
                                            float ringRadius, float life, int count, float speed)
        {
            var ring = NewPS(host, "GroundRing", at + Vector3.up * 0.05f);
            var m = ring.main; m.startLifetime = life; m.startSpeed = speed; m.startSize = 0.22f;
            var sh = ring.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = ringRadius; sh.radiusThickness = 0f; sh.rotation = new Vector3(90f, 0f, 0f);
            Burst(ring, count);
            SizeOverLife(ring, 0.8f, 0.0f);
            ApplyCOL(ring, core, body, body);
        }

        private static void BuildNova(GameObject host, Color core, Color body, Color edge,
                                      Vector3 at, float r, float shardGravity)
        {
            // Expanding flat shockwave ring on the ground.
            var ring = NewPS(host, "NovaRing", at + Vector3.up * 0.05f);
            var rm = ring.main; rm.startLifetime = 0.5f; rm.startSpeed = Mathf.Max(4f, r * 2f); rm.startSize = 0.3f;
            var rsh = ring.shape; rsh.enabled = true; rsh.shapeType = ParticleSystemShapeType.Circle;
            rsh.radius = 0.4f; rsh.radiusThickness = 0f; rsh.rotation = new Vector3(90f, 0f, 0f);
            Burst(ring, 40);
            ApplyCOL(ring, Color.white, body, edge);

            // Upward shard / arrow burst at the centre.
            var shards = NewPS(host, "Shards", at);
            var shm = shards.main; shm.startLifetime = 0.6f;
            shm.startSpeed = new ParticleSystem.MinMaxCurve(5f, 8f);
            shm.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            shm.gravityModifier = shardGravity;
            var ssh = shards.shape; ssh.enabled = true; ssh.shapeType = ParticleSystemShapeType.Cone;
            ssh.angle = 25f; ssh.radius = 0.3f; ssh.rotation = new Vector3(-90f, 0f, 0f);
            Burst(shards, 20);
            Stretch(shards, 0.05f, 1.8f);
            ApplyCOL(shards, core, body, edge);

            // Brief centre flash.
            var flash = NewPS(host, "Flash", at + Vector3.up * 0.4f);
            var fm = flash.main; fm.startLifetime = 0.18f; fm.startSpeed = 0f; fm.startSize = r * 0.9f;
            var fsh = flash.shape; fsh.enabled = true; fsh.shapeType = ParticleSystemShapeType.Sphere; fsh.radius = 0.05f;
            Burst(flash, 6);
            ApplyCOLWhite(flash, core, 0.5f);
        }

        private static void BuildHeal(GameObject host, Color core, Color body, Vector3 at, float r)
        {
            // Rising warm column — a gentle sustained fountain.
            var col = NewPS(host, "HealColumn", at);
            var cm = col.main; cm.duration = 1.2f; cm.startLifetime = 1.0f;
            cm.startSpeed = 0f; cm.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            cm.gravityModifier = -0.05f;
            var em = col.emission; em.rateOverTime = 30f;
            var sh = col.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.5f;
            // upward drift — all 3 axes same (Constant) mode (curve-mode rule).
            var vel = col.velocityOverLifetime; vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f);
            vel.y = new ParticleSystem.MinMaxCurve(2.5f);
            vel.z = new ParticleSystem.MinMaxCurve(0f);
            ApplyCOL(col, core, body, body);

            // Soft slow pulse ring marking the heal radius.
            BuildGroundRing(host, core, body, at, r, 0.8f, 20, Mathf.Max(2f, r * 1.2f));
        }

        private static void BuildMeteor(GameObject host, Color core, Color body, Color edge, Vector3 at, float r)
        {
            // Beat 1 — fiery falling streak (spawns above, falls to the target).
            var fall = NewPS(host, "MeteorFall", at + Vector3.up * 6f);
            var fm = fall.main; fm.startLifetime = 0.3f; fm.startSpeed = 18f;
            fm.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.6f);
            var fsh = fall.shape; fsh.enabled = true; fsh.shapeType = ParticleSystemShapeType.Cone;
            fsh.angle = 5f; fsh.radius = 0.1f; fsh.rotation = new Vector3(90f, 0f, 0f); // point DOWN
            Burst(fall, 16);
            Stretch(fall, 0.08f, 3f);
            var trail = fall.trails; trail.enabled = true; trail.lifetime = 0.2f;
            var fr = fall.GetComponent<ParticleSystemRenderer>(); if (fr != null) fr.trailMaterial = fr.material;
            ApplyCOL(fall, Color.white, body, edge);

            // Beat 2 — ground shockwave ring at impact (fires after the fall).
            var ring = NewPS(host, "MeteorRing", at + Vector3.up * 0.05f);
            var rm = ring.main; rm.startLifetime = 0.45f; rm.startSpeed = Mathf.Max(6f, r * 2.2f); rm.startSize = 0.35f;
            var rsh = ring.shape; rsh.enabled = true; rsh.shapeType = ParticleSystemShapeType.Circle;
            rsh.radius = 0.4f; rsh.radiusThickness = 0f; rsh.rotation = new Vector3(90f, 0f, 0f);
            ring.emission.SetBursts(new[] { new ParticleSystem.Burst(0.25f, 40) });
            ApplyCOL(ring, Color.white, body, edge);

            // Ember scatter (after impact).
            var ember = NewPS(host, "Embers", at);
            var emm = ember.main; emm.startLifetime = 0.9f;
            emm.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
            emm.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            emm.gravityModifier = 1.5f;
            var esh = ember.shape; esh.enabled = true; esh.shapeType = ParticleSystemShapeType.Hemisphere; esh.radius = 0.4f;
            ember.emission.SetBursts(new[] { new ParticleSystem.Burst(0.25f, 40) });
            ApplyCOL(ember, core, body, edge);
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        private static ParticleSystem NewPS(GameObject host, string name, Vector3 worldPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(host.transform, false);
            go.transform.position = worldPos;
            var ps = go.AddComponent<ParticleSystem>();
            // AddComponent starts the system playing this frame REGARDLESS of the
            // playOnAwake flag we set below, and MainModule.duration cannot change
            // on a playing system ("Setting the duration while system is still
            // playing is not supported"). Stop it first so every property below —
            // and any duration set by the caller after NewPS returns (e.g. Heal) —
            // is applied while stopped. The system is (re)played later in bulk via
            // GetComponentsInChildren<ParticleSystem>().Play().
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.6f;
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.None;
            var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
            var sh = ps.shape; sh.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (r != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                if (s != null) { var m = new Material(s); m.mainTexture = SoftDot(); r.material = m; }
                r.renderMode = ParticleSystemRenderMode.Billboard;
            }
            return ps;
        }

        private static void Burst(ParticleSystem ps, int count)
            => ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        private static void Stretch(ParticleSystem ps, float velScale, float lenScale)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r == null) return;
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = velScale;
            r.lengthScale = lenScale;
        }

        private static void SizeOverLife(ParticleSystem ps, float from, float to)
        {
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
        }

        private static void ApplyCOL(ParticleSystem ps, Color core, Color body, Color edge)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(core, 0f), new GradientColorKey(body, 0.4f), new GradientColorKey(edge, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        private static void ApplyCOLWhite(ParticleSystem ps, Color tint, float peakAlpha)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(tint, 1f) },
                new[] { new GradientAlphaKey(peakAlpha, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        private static void FlashLight(GameObject host, Color color, Vector3 at,
                                       float intensity, float range, float fadeTime)
        {
            var go = new GameObject("AbilityFlash");
            go.transform.SetParent(host.transform, false);
            go.transform.position = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = color; l.intensity = intensity;
            l.range = range; l.shadows = LightShadows.None;
            go.AddComponent<VfxLightFade>().FadeTime = fadeTime;
        }

        private static Texture2D SoftDot()
        {
            if (s_softDot != null) return s_softDot;
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            float c = (N - 1) / 2f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - d); a *= a;
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            s_softDot = tex;
            return tex;
        }
    }

    /// <summary>Fades a point-light's intensity to zero then self-destroys.</summary>
    [DisallowMultipleComponent]
    internal sealed class VfxLightFade : MonoBehaviour
    {
        public float FadeTime = 0.3f;
        private Light _light;
        private float _start;
        private float _t;

        private void Awake() { _light = GetComponent<Light>(); _start = _light != null ? _light.intensity : 0f; }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(_t / Mathf.Max(0.01f, FadeTime));
            if (_light != null) _light.intensity = _start * k;
            if (_t >= FadeTime) Destroy(gameObject);
        }
    }
}
