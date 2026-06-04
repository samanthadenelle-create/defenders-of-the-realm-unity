// =============================================================================
// EnemyData — WO-86. ScriptableObject for all enemy balance stats.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Data   Namespace: DeNelle.Data
//
// Create assets via Assets > Create > Defenders/Data/Enemy.
// EnemyBrain reads from this SO at Awake and overlays its hardcoded inspector
// fields. If data is null the existing hardcoded values are kept intact.
// EnemyDamageable checks for EnemyData on the same GameObject at Die() to
// grant Aether rewards via WalletService.
// =============================================================================

using UnityEngine;

namespace DeNelle.Data
{
    [CreateAssetMenu(fileName = "NewEnemyData", menuName = "Defenders/Data/Enemy")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        public string enemyName             = "Grunt";
        public bool   isElite               = false;
        public bool   isBoss                = false;

        [Header("Combat")]
        public int   maxHealth              = 45;
        public int   damage                 = 12;
        public float attackCooldown         = 1.8f;
        public float attackRange            = 2.5f;
        public float detectionRange         = 15f;

        [Header("Movement")]
        public float chaseSpeed             = 4.2f;
        public float patrolSpeed            = 2.2f;

        [Header("Death Rewards")]
        public int   aetherReward           = 0;
        public int   woodReward             = 5;

        [Header("VFX")]
        public int   heavyHitThreshold      = 20;   // Damage amount that triggers heavy hit reaction
    }
}
