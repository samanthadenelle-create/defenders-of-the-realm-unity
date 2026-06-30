// =============================================================================
// BreakableContainer — a destructible loot prop (crate / barrel / chest) for the
// Phase-2 outpost/dungeon chain. The hero melee/ability OverlapSphere already
// sweeps the "Enemy" layer for DeNelle.Core.Combat.IDamageable targets and calls
// TakeDamage(...); a container is just a STATIC IDamageable that, on death, rolls
// a loot table (the SAME drops -> materials -> crafting lane the enemies feed) and
// either spawns a world pickup mote or deposits straight to the larder.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// REUSES (does NOT reinvent, CLAUDE.md §9):
//   - DeNelle.Village.Items.ItemDropSystem.RollLines / RollAndDeposit (loot roll)
//   - DeNelle.Village.Items.ItemPickupSpawner.Spawn (world drop mote)
//   - DeNelle.Core.Combat.IDamageable / IDamageableStructure (the hit seams)
//
// It implements BOTH IDamageable (the hero's OverlapSphere -> TakeDamage path) and
// IDamageableStructure (the contact-damage / structure path) so any existing damage
// source can break it. SHIPS behind ItemDropSystem.Enabled via the spawner no-op.
//
// RUNTIME-SAFE: Create() builds a tinted PRIMITIVE cube + a BoxCollider on the
// "Enemy" layer (so the hero's enemy-mask sweep catches it). No AssetDatabase, no
// prefab hard-dep. ASCII strings only. Canon: the village is Elarion.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Combat;        // IDamageable / IDamageableStructure / DamageElement
using DeNelle.Core.Diagnostics;   // FlowTrace (TGVRU, CLAUDE.md §12)
using DeNelle.Village.Items;      // ItemDropSystem / ItemPickupSpawner (same assembly)

namespace DeNelle.Village
{
    /// <summary>
    /// A destructible loot prop. The hero attacks it like an enemy (it is an
    /// <see cref="IDamageable"/> on the Enemy layer); when its HP reaches zero it
    /// rolls <see cref="LootTableId"/> and drops survival materials.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BreakableContainer : MonoBehaviour, IDamageable, IDamageableStructure
    {
        private const string Sys = "Loot";

        [Tooltip("Hit points before the container breaks open.")]
        [SerializeField] private float maxHp = 30f;

        [Tooltip("Loot table rolled on break (loot-tables.json id, e.g. crate-common / barrel-common / chest-rare).")]
        [SerializeField] private string lootTableId = "crate-common";

        private float _hp = -1f;        // lazily initialised to maxHp on first access
        private bool _broken;

        /// <summary>Loot table id rolled when this container breaks.</summary>
        public string LootTableId
        {
            get => lootTableId;
            set => lootTableId = value;
        }

        private void Awake()
        {
            if (_hp < 0f) _hp = Mathf.Max(1f, maxHp);
        }

        // ── IDamageable (the hero melee / ability OverlapSphere seam) ───────────

        /// <summary>Containers read as Hostile so the hero's enemy-mask sweep hits them.</summary>
        public CombatFaction Faction => CombatFaction.Hostile;

        /// <summary>World position — used for range / nearest-target queries.</summary>
        public Vector3 WorldPosition => transform.position;

        /// <summary>Current hit points.</summary>
        public float Hp => _hp < 0f ? Mathf.Max(1f, maxHp) : _hp;

        /// <summary>True while the container still stands and can be struck.</summary>
        public bool IsAlive => !_broken && Hp > 0f;

        /// <summary>Hero / ability damage entry point. Element is ignored (a crate has no resists).</summary>
        public void TakeDamage(float amount, DamageElement element) => ApplyDamage(amount);

        /// <summary>Status effects are a no-op on inert props.</summary>
        public void ApplyStatus(StatusEffect effect, float seconds) { /* props ignore CC */ }

        // ── IDamageableStructure (the contact-damage seam) ─────────────────────

        /// <summary>Contact-damage entry point (e.g. an enemy bumping the prop).</summary>
        public void ApplyContactDamage(float amount) => ApplyDamage(amount);

        // ── Damage + break ─────────────────────────────────────────────────────

        private void ApplyDamage(float amount)
        {
            if (_broken) return;
            if (_hp < 0f) _hp = Mathf.Max(1f, maxHp);
            if (amount <= 0f) return;

            _hp -= amount;
            if (_hp <= 0f) Break();
        }

        private void Break()
        {
            if (_broken) return;
            _broken = true;

            string name = gameObject != null ? gameObject.name : "container";
            Vector3 at = transform != null ? transform.position : Vector3.zero;

            // Roll the loot. Prefer a WORLD pickup mote (walk-over to collect); if the roll
            // produced nothing OR pickups are disabled, fall back to a direct larder deposit
            // so the kill is still credited. ItemPickupSpawner.Spawn no-ops when the lane is off.
            var lines = ItemDropSystem.RollLines(lootTableId);
            if (ItemDropSystem.UseWorldPickups && lines != null && lines.Count > 0)
            {
                ItemPickupSpawner.Spawn(at, lines);
                FlowTrace.Step(Sys, $"{name} broke -> dropped {lines.Count} loot line(s) as a world mote (table '{lootTableId}')");
            }
            else
            {
                ItemDropSystem.RollAndDeposit(lootTableId);
                FlowTrace.Step(Sys, $"{name} broke -> deposited loot to larder (table '{lootTableId}')");
            }

            // Remove the visual. Destroy the whole prop (the mote, if any, is a separate object).
            Destroy(gameObject);
        }

        // ── Runtime factory ─────────────────────────────────────────────────────

        /// <summary>
        /// Build a runtime-safe breakable prop: a tinted primitive cube + a solid
        /// BoxCollider on the "Enemy" layer (so the hero's enemy-mask OverlapSphere
        /// catches it) carrying a configured <see cref="BreakableContainer"/>.
        /// <paramref name="visualToken"/> ("crate" / "barrel" / "chest") only tints it.
        /// </summary>
        public static BreakableContainer Create(Transform parent, Vector3 pos, string lootTableId, string visualToken)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Breakable_{(string.IsNullOrEmpty(visualToken) ? "crate" : visualToken)}";
            go.transform.SetParent(parent, false);
            go.transform.position = pos + Vector3.up * 0.5f;   // sit the cube on the floor (top at ~1m)
            go.transform.localScale = Vector3.one;

            // On the "Enemy" layer so the hero's enemy-mask sweep (PlayerAttackController /
            // HeroAbilities OverlapSphere) finds the IDamageable on it.
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0) go.layer = enemyLayer;

            // CreatePrimitive ships the built-in Standard shader (magenta under URP). Build a
            // URP-compatible tinted material explicitly (same class of fix as the pink-floor lesson).
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    Color tint = TintFor(visualToken);
                    var mat = new Material(shader);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
                    rend.material = mat;
                }
            }

            // The primitive already carries a BoxCollider; ensure it is SOLID (not a trigger)
            // so it reads as a struck body and lightly blocks. Add one if somehow missing.
            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            box.isTrigger = false;

            var bc = go.AddComponent<BreakableContainer>();
            bc.lootTableId = string.IsNullOrEmpty(lootTableId) ? "crate-common" : lootTableId;
            return bc;
        }

        private static Color TintFor(string token)
        {
            token = (token ?? "crate").ToLowerInvariant();
            if (token.Contains("chest"))  return new Color(0.78f, 0.62f, 0.22f); // gold chest
            if (token.Contains("barrel")) return new Color(0.42f, 0.28f, 0.16f); // dark wood
            return new Color(0.55f, 0.40f, 0.24f);                               // crate wood
        }
    }
}
