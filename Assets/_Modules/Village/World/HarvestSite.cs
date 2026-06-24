using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Combat;
using DeNelle.Core.World;
using DeNelle.Core;

namespace DeNelle.Village.World
{
    /// <summary>
    /// A player-claimed harvest site. Provides pet-assignable, scalable,
    /// defendable resource income that always flows through EconomyService.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HarvestSite : MonoBehaviour, IDamageableStructure
    {
        [Header("Resource")]
        public MineResource ResourceType = MineResource.Wood;
        [Tooltip("Base units per harvest tick before multipliers.")]
        public int BaseYield = 4;
        [Tooltip("Seconds between income ticks.")]
        public float HarvestInterval = 5f;

        [Header("Pet Assignment")]
        [Tooltip("How much extra yield per assigned worker (multiplicative bonus).")]
        public float YieldPerAssignedPet = 0.6f;
        [Tooltip("Max pets/workers that can be assigned to this site.")]
        public int MaxAssigned = 3;

        [Header("Visual / Claim")]
        [Tooltip("Radius for auto-claiming nearby MineNode on Start (if present).")]
        public float ClaimRadius = 6f;

        [Header("Scaling & Defense")]
        [Tooltip("Further from Heart (0,0,0) or higher danger = better yields but more raids.")]
        public bool ApplyWorldScaling = true;
        [Tooltip("Base HP for the harvest structure (raids can damage it).")]
        public float MaxHp = 80f;

        // ── Runtime ──────────────────────────────────────────────────────────
        private float _tick;
        private float _hp;
        private MineNode _linkedNode;
        private readonly List<Transform> _assignedWorkers = new List<Transform>(4);
        private bool _claimed;

        public bool IsClaimed => _claimed;
        public int AssignedCount => _assignedWorkers.Count;

        // IDamageableStructure
        public bool IsAlive => _hp > 0f;

        public void ApplyContactDamage(float amount)
        {
            if (_hp <= 0f) return;
            _hp = Mathf.Max(0f, _hp - Mathf.Abs(amount));
            if (_hp <= 0f)
            {
                Debug.LogWarning($"[HarvestSite] {ResourceType} site at {transform.position:F1} was destroyed by raid.");
                Destroy(gameObject);
            }
        }

        private void Awake()
        {
            _hp = MaxHp;
        }

        private void Start()
        {
            TryLinkNearbyNode();
            if (!_claimed)
            {
                Claim();
            }
            BuildVisual();
        }

        private void Update()
        {
            if (!_claimed || _hp <= 0f) return;

            _tick += Time.deltaTime;
            if (_tick < HarvestInterval) return;

            _tick = 0f;
            int amount = CalculateYield();

            if (amount > 0)
            {
                var econ = EconomyService.Instance;
                // WO-424: trace this SECOND harvest source (HarvestSite's own AddResource
                // path, separate from MineNode.BankYield) so a headless run can confirm the
                // tick banked → fired OnChanged → reached HUD.SetResources. The economy/HUD
                // ends are already traced [Flow:Eco]; this is the source link for this path.
                DeNelle.Core.Diagnostics.FlowTrace.Step("Eco",
                    $"HarvestSite banked +{amount} {ResourceType} (econ={(econ != null)}) — expect OnChanged → HUD.SetResources next");
                if (econ != null)
                {
                    econ.AddResource(ResourceType, amount);
                }

                // Floating text
                Vector3 popupPos = _assignedWorkers.Count > 0 && _assignedWorkers[0] != null
                    ? _assignedWorkers[0].position + Vector3.up * 1.5f
                    : transform.position + Vector3.up * 2.2f;

                Color tint = GetResourceColor();
                ResourceGainPopup.Spawn(popupPos, $"+{amount} {ResourceType}", tint);

                TryAttractRaid();
            }

            if (_linkedNode != null && _linkedNode.UseFiniteReserve)
            {
                _linkedNode.DrainReserve(Mathf.Max(1, amount / 2));
            }
        }

        public void Claim()
        {
            if (_claimed) return;
            _claimed = true;
            _hp = MaxHp;
            Debug.Log($"[HarvestSite] Claimed {ResourceType} harvest site.");
        }

        public bool AssignPet(Transform worker)
        {
            if (worker == null || _assignedWorkers.Count >= MaxAssigned) return false;
            if (_assignedWorkers.Contains(worker)) return true;

            _assignedWorkers.Add(worker);
            Debug.Log($"[HarvestSite] Assigned worker to {ResourceType} site (now {_assignedWorkers.Count}).");
            return true;
        }

        public void UnassignPet(Transform worker)
        {
            _assignedWorkers.Remove(worker);
        }

        private void TryLinkNearbyNode()
        {
            var nodes = FindObjectsByType<MineNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            MineNode best = null;
            float bestDist = ClaimRadius * ClaimRadius;

            foreach (var n in nodes)
            {
                if (n == null || n.IsDepleted) continue;
                float d = (n.transform.position - transform.position).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = n;
                }
            }

            if (best != null)
            {
                _linkedNode = best;
                transform.position = best.transform.position;
            }
        }

        private int CalculateYield()
        {
            float multiplier = 1f;
            if (ApplyWorldScaling)
            {
                float dist = Vector3.Distance(transform.position, Vector3.zero);
                float distBonus = Mathf.Clamp01(dist / 120f) * 0.6f;
                int tier = Mathf.Max(0, ZoneManager.DangerTierAt(transform.position));
                float dangerBonus = tier * 0.25f;
                multiplier += distBonus + dangerBonus;
            }

            float petBonus = AssignedCount * YieldPerAssignedPet;
            multiplier += petBonus;

            int raw = Mathf.RoundToInt(BaseYield * multiplier);
            return Mathf.Max(1, raw);
        }

        private void TryAttractRaid()
        {
            float danger = ZoneManager.DangerTierAt(transform.position) + (Vector3.Distance(transform.position, Vector3.zero) / 80f);
            float chance = Mathf.Clamp01(0.08f + danger * 0.04f);
            if (Random.value > chance) return;

            int count = 2 + Mathf.FloorToInt(danger * 0.6f);
            Vector3 spawnPos = transform.position + Random.insideUnitSphere * 12f;
            spawnPos.y = transform.position.y;

            for (int i = 0; i < count; i++)
            {
                var raider = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                raider.name = "HarvestRaider";
                raider.transform.position = spawnPos + Vector3.right * i * 1.5f;
                raider.transform.localScale = Vector3.one * 0.9f;

                var attacker = raider.AddComponent<HarvestSiteRaider>();
                attacker.TargetSite = this;
                attacker.DamagePerHit = 8f + danger * 3f;
                attacker.Speed = 3.5f;

                Destroy(raider, 25f);
            }
        }

        private void BuildVisual()
        {
            // Clear old visuals
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("HarvestVisual") || child.name.Contains("Platform") || child.name.Contains("Resource"))
                    Destroy(child.gameObject);
            }

            Color tint = GetResourceColor();

            // Platform (always — reads as a claimed harvest station the model sits on).
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            platform.name = "HarvestVisual_Platform";
            platform.transform.SetParent(transform, false);
            platform.transform.localPosition = Vector3.zero;
            platform.transform.localScale = new Vector3(2.8f, 0.25f, 2.8f);
            StripCollider(platform);
            Paint(platform, new Color(0.35f, 0.32f, 0.28f));

            // Real lightweight resource model on the platform (owner 2026-06-16 art drop:
            // Resources/Harvest/<type>, ~33-156KB each — WebGL-friendly). Fit + seated by
            // VisualFactory; Tripo materials URP-fixed. When the model is absent we fall
            // through to the primitive post + per-resource top below (never breaks the demo).
            string modelPath = ResourceModelPath(ResourceType);
            var model = string.IsNullOrEmpty(modelPath) ? null : VisualFactory.Skin(transform, modelPath,
                new SkinOptions { FitLargest = 2.4f, SeatOnGround = true, FixTripoMaterials = true,
                    LocalRotation = Quaternion.Euler(0f, 180f, -90f) });   // Offset Forge 2026-06-23: Tripo resource-model orientation fix (all harvest nodes).
            if (model != null)
            {
                model.name = "HarvestVisual_Model";
                var mp = model.transform.localPosition; mp.y += 0.18f;   // clear the platform top
                model.transform.localPosition = mp;
                return;   // the real model IS the visual — skip the primitive post/top
            }

            // ── Fallback (model missing on disk): primitive post + per-resource top ──
            // Post
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "HarvestVisual_Post";
            post.transform.SetParent(transform, false);
            post.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            post.transform.localScale = new Vector3(0.35f, 1.8f, 0.35f);
            StripCollider(post);
            Paint(post, tint * 0.85f);

            // Resource Top
            GameObject top = null;
            switch (ResourceType)
            {
                case MineResource.Wood:
                    top = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    top.name = "HarvestVisual_Lumber";
                    top.transform.localScale = new Vector3(1.6f, 0.6f, 1.1f);
                    top.transform.localPosition = new Vector3(0f, 2.3f, 0f);
                    break;
                case MineResource.Iron:
                    top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    top.name = "HarvestVisual_Ore";
                    top.transform.localScale = new Vector3(1.3f, 0.9f, 1.3f);
                    top.transform.localPosition = new Vector3(0f, 2.15f, 0f);
                    break;
                case MineResource.Food:
                    top = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    top.name = "HarvestVisual_Grain";
                    top.transform.localScale = new Vector3(1.1f, 0.7f, 1.1f);
                    top.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                    break;
                case MineResource.AetherCrystal:
                    top = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    top.name = "HarvestVisual_Crystal";
                    top.transform.localScale = new Vector3(0.9f, 1.2f, 0.9f);
                    top.transform.localPosition = new Vector3(0f, 2.4f, 0f);
                    break;
            }

            if (top != null)
            {
                top.transform.SetParent(transform, false);
                StripCollider(top);
                Paint(top, tint);
            }
        }

        // Resources path of the lightweight per-type harvest model (owner art drop). Null for
        // an unmapped resource → the primitive fallback in BuildVisual is used.
        private static string ResourceModelPath(MineResource r) => r switch
        {
            MineResource.Wood          => "Harvest/wood",
            MineResource.Iron          => "Harvest/iron",
            MineResource.Food          => "Harvest/food",
            MineResource.AetherCrystal => "Harvest/crystals",
            _ => null,
        };

        private Color GetResourceColor()
        {
            return ResourceType switch
            {
                MineResource.Wood => new Color(0.55f, 0.38f, 0.22f),
                MineResource.Iron => new Color(0.48f, 0.50f, 0.55f),
                MineResource.Food => new Color(0.72f, 0.62f, 0.28f),
                MineResource.AetherCrystal => new Color(0.35f, 0.72f, 0.95f),
                _ => Color.gray
            };
        }

        private static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        private static void Paint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            r.material.color = c;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _claimed ? new Color(0.2f, 0.9f, 0.5f, 0.4f) : new Color(0.9f, 0.7f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, 3.5f);
        }
#endif
    }

    // Raider Proxy
    public sealed class HarvestSiteRaider : MonoBehaviour
    {
        public HarvestSite TargetSite;
        public float Speed = 3.5f;
        public float DamagePerHit = 10f;
        private float _nextDamageTime;

        private void Update()
        {
            if (TargetSite == null || !TargetSite.IsAlive)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 dir = (TargetSite.transform.position - transform.position).normalized;
            transform.position += dir * Speed * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 0.2f);

            float dist = Vector3.Distance(transform.position, TargetSite.transform.position);
            if (dist < 1.8f && Time.time >= _nextDamageTime)
            {
                TargetSite.ApplyContactDamage(DamagePerHit);
                _nextDamageTime = Time.time + 1.2f;
            }
        }
    }
}