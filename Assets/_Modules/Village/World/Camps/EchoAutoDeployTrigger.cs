// =============================================================================
// EchoAutoDeployTrigger — WO-360: summon Echo (the pet) when the player enters an
// enemy-outpost combat zone, so the Echo fights alongside them and persists.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// THE BEAT: walk into an EnemyOutpost's combat radius → the player's chosen Echo
// (the FTUE-named starter pet) is summoned at the player's position, golden-VFX
// flourish if a summon/celebration VFX exists, a 3s mini-tutorial toast pops, and
// the Echo joins the auto-fight (Pet in Defend mode hunts the garrison via the
// SAME IDamageable/TargetManager path the village uses). The Echo PERSISTS — it is
// never despawned here, so it keeps fighting through the raid AND trails the hero
// during exploration afterward (PetHeroLeash, added by PetDeployer.SpawnPet).
//
// REUSE (no reinvented wheels):
//   * PetDeployer.SummonAt(pos, Defend) — the ONE pet summon path (WO-360 add).
//     Self-heals a PetDeployer in the world scene if none exists (mirrors
//     DialogueCommandBridge.EnsurePetDeployer).
//   * VFXManager.Play(Juice_LevelUp, pos) — best-effort golden flourish; the
//     project has no dedicated "pet summon" VFX, so we reuse the celebratory
//     level-up burst. Null-safe (no VFXManager = no flourish, never an error).
//   * EchoTutorialUI.Show(name) — the code-built bottom-left mini-tutorial toast.
//
// IDEMPOTENT: summons the Echo at most once per session (a static guard) — the
// player only meets their Echo at the outpost once. Re-entering the trigger is a
// no-op. EnemyOutpost attaches this in Start() (Attach()), sizing the trigger to
// the garrison ring so the deploy fires as the player reaches the fight.
//
// Isolation/safety: lives in DeNelle.Village; references DeNelle.Pets via the
// Village asmdef. Every cross-call null-guarded + try/caught. ASCII-only strings.
// =============================================================================

using DeNelle.Core.State;
using DeNelle.Pets;
using UnityEngine;

namespace DeNelle.Village.World.Camps
{
    /// <summary>
    /// A SphereCollider trigger that, the first time the Player enters an enemy
    /// outpost's combat radius, summons the player's Echo (pet) to fight alongside
    /// them — with a golden flourish + a one-shot mini-tutorial toast. Idempotent
    /// per session. Attach via <see cref="Attach"/> from <see cref="EnemyOutpost"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EchoAutoDeployTrigger : MonoBehaviour
    {
        // The Echo meets the player at the outpost ONCE per session (it then
        // persists). A static guard keeps multiple outposts / re-entries from
        // re-summoning. (Session-scoped on purpose: the live Echo carries over.)
        private static bool s_summonedThisSession;

        private float _triggerRadius = 12f;
        private bool _fired;

        /// <summary>
        /// Adds an EchoAutoDeployTrigger to <paramref name="host"/> with a trigger
        /// sphere of <paramref name="radius"/>. Called by EnemyOutpost.Start().
        /// </summary>
        public static EchoAutoDeployTrigger Attach(GameObject host, float radius)
        {
            if (host == null) return null;
            var trig = host.GetComponent<EchoAutoDeployTrigger>();
            if (trig == null) trig = host.AddComponent<EchoAutoDeployTrigger>();
            trig._triggerRadius = Mathf.Max(2f, radius);
            trig.EnsureTriggerCollider();
            return trig;
        }

        private void EnsureTriggerCollider()
        {
            // A dedicated child trigger so we don't disturb any solid colliders on
            // the outpost root (garrison/fort pieces have their own).
            var existing = GetComponent<SphereCollider>();
            SphereCollider col = existing;
            if (col == null) col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = _triggerRadius;
            col.center = Vector3.zero;
        }

        private void OnTriggerEnter(Collider other)
        {
            TrySummon(other);
        }

        // Belt-and-braces: if the player is already standing inside the radius when
        // the trigger spawns, OnTriggerEnter won't fire — poll a couple of frames.
        private void Start()
        {
            if (!s_summonedThisSession && !_fired)
                Invoke(nameof(PollForPlayer), 0.25f);
        }

        private void PollForPlayer()
        {
            if (_fired || s_summonedThisSession) return;
            var player = GameObject.FindWithTag("Player");
            if (player != null &&
                Vector3.Distance(player.transform.position, transform.position) <= _triggerRadius)
            {
                Fire(player.transform.position);
            }
        }

        private void TrySummon(Collider other)
        {
            if (_fired || s_summonedThisSession || other == null) return;
            // The hero is tagged "Player" (CLAUDE.md §7); accept the player or a
            // child collider of the player root.
            if (!other.CompareTag("Player"))
            {
                var root = other.transform.root;
                if (root == null || !root.CompareTag("Player")) return;
            }
            Fire(other.transform.position);
        }

        private void Fire(Vector3 atPosition)
        {
            if (_fired || s_summonedThisSession) return;
            _fired = true;
            s_summonedThisSession = true;

            string echoName = ResolveEchoName();

            try
            {
                var deployer = EnsurePetDeployer();
                if (deployer != null)
                {
                    Pet echo = deployer.SummonAt(atPosition, PetMode.Defend);
                    if (echo != null)
                    {
                        echo.name = string.IsNullOrEmpty(echoName) ? "Echo" : echoName;
                        PlaySummonFlourish(echo.transform.position);
                    }
                    else
                    {
                        Debug.LogWarning("[EchoAutoDeployTrigger] SummonAt returned null — Echo not deployed.");
                    }
                }
                else
                {
                    Debug.LogWarning("[EchoAutoDeployTrigger] Could not resolve/create a PetDeployer — Echo not deployed.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[EchoAutoDeployTrigger] Echo summon error (continuing): {ex.Message}");
            }

            // Mini-tutorial toast — non-blocking, 3s, tap-to-dismiss. Try/caught so a
            // UI hiccup never blocks the raid.
            try { EchoTutorialUI.Show(echoName); }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[EchoAutoDeployTrigger] EchoTutorialUI error (ignored): {ex.Message}");
            }
        }

        // Best-effort GOLDEN flourish at the summon point. The project has no
        // dedicated "pet summon" VFX — reuse the celebratory level-up burst, which
        // reads gold. VFXManager.Play is static + null-safe (no manager = no-op).
        private static void PlaySummonFlourish(Vector3 pos)
        {
            try { VFXManager.Play(VFXType.Juice_LevelUp, pos); }
            catch { /* VFX is cosmetic — never let it break the deploy */ }
        }

        private static string ResolveEchoName()
        {
            var state = GameStateService.Instance?.State;
            if (state != null && !string.IsNullOrWhiteSpace(state.PetName))
                return state.PetName.Trim();
            return "Echo";
        }

        // Self-heal a PetDeployer in the world scene if none exists (mirrors
        // DialogueCommandBridge.EnsurePetDeployer): the OuterWorld may ship without
        // one. Heart/origin centre, project "Enemy" layer mask, save-bond ranks.
        private static PetDeployer EnsurePetDeployer()
        {
            var deployer = FindObjectOfType<PetDeployer>();
            if (deployer != null) return deployer;

            var go = new GameObject("PetDeployer");
            deployer = go.AddComponent<PetDeployer>();

            Vector3 heartPos = Vector3.zero;
            var heart = FindObjectOfType<HeartController>();
            if (heart != null) heartPos = heart.transform.position;
            deployer.SetHeartPosition(heartPos);

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            deployer.SetEnemyMask(enemyLayer >= 0 ? (1 << enemyLayer) : ~0);

            var svc = GameStateService.Instance;
            if (svc != null && svc.State != null && svc.State.PetBonds != null)
            {
                var b = svc.State.PetBonds;
                int aether = b.Count > 0 ? b[0] : 0;
                int flame  = b.Count > 1 ? b[1] : 0;
                int ice    = b.Count > 2 ? b[2] : 0;
                deployer.SetBondRanks(aether, flame, ice);
            }

            Debug.Log($"[EchoAutoDeployTrigger] Self-healed a PetDeployer (heart={heartPos}).");
            return deployer;
        }
    }
}
