// =============================================================================
// PetRepairAdapter — the Attack/Repair toggle for a Patricia Light pet.
// -----------------------------------------------------------------------------
// WO-47 Phase 2/3 ("Defend the Tower"). The owner WO asks for pets that toggle
// between ATTACK (hunt enemies) and REPAIR (move to the tower + restore its HP).
//
// HARD CONSTRAINT (WO-47): do NOT add a new PetController and do NOT duplicate
// pet AI. So this is a THIN Village-side adapter that sits ON the existing
// DeNelle.Pets.Pet GameObject and just flips a switch:
//
//   • ATTACK  → Pet.Mode = Defend. The pet's OWN hunt-and-strike AI runs
//               (NearestHostile / Attack via IDamageable + DamageAttribution) —
//               zero duplicate combat code.
//   • REPAIR  → Pet.Mode = Idle (so the pet stops hunting), and THIS adapter
//               walks the pet toward the tower and tops up HeartController.Hp
//               (the canonical tower HP — no forked second HP). The repair
//               movement is the only behaviour added; combat stays in Pet.
//
// Lives in DeNelle.Village; DeNelle.Village references DeNelle.Pets (Pets does
// NOT reference Village, so there is no asmdef cycle).
// =============================================================================

using DeNelle.Pets;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Toggles a deployed <see cref="Pet"/> between Attack (its own Defend hunt
    /// AI) and Repair (walk to the tower + restore <see cref="HeartController"/>
    /// HP). Attached to the pet GameObject by <c>PatriciaLightController</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetRepairAdapter : MonoBehaviour
    {
        /// <summary>Whether this pet is currently attacking or repairing the tower.</summary>
        public enum Role
        {
            /// <summary>Hunt enemies — drives the Pet's own Defend mode.</summary>
            Attack = 0,
            /// <summary>Move to the tower and restore its integrity.</summary>
            Repair = 1,
        }

        private Pet _pet;
        private HeartController _tower;
        private Role _role = Role.Attack;

        // Repair tuning.
        private float _repairPerSecond = 4f;     // tower HP restored per second while in range
        private float _repairRange = 6f;         // distance from the tower at which repair ticks
        private float _moveSpeed = 5f;            // walk speed toward the tower in Repair mode

        /// <summary>This pet's current role (Attack / Repair).</summary>
        public Role CurrentRole => _role;

        /// <summary>The Pet this adapter drives (for HUD labels).</summary>
        public Pet Pet => _pet;

        /// <summary>
        /// Wires the adapter to its <see cref="Pet"/> and the tower it repairs.
        /// </summary>
        /// <param name="pet">The deployed pet whose mode this adapter flips.</param>
        /// <param name="tower">The Heart whose Hp Repair mode tops up.</param>
        /// <param name="repairPerSecond">Tower HP restored per second while repairing in range.</param>
        public void Initialize(Pet pet, HeartController tower, float repairPerSecond = 4f)
        {
            _pet = pet;
            _tower = tower;
            _repairPerSecond = Mathf.Max(0f, repairPerSecond);
            SetRole(Role.Attack);
        }

        /// <summary>Flips Attack ⇄ Repair (the HUD button calls this).</summary>
        public void Toggle() => SetRole(_role == Role.Attack ? Role.Repair : Role.Attack);

        /// <summary>Sets the role explicitly and re-points the Pet's own mode.</summary>
        public void SetRole(Role role)
        {
            _role = role;
            if (_pet == null) return;
            // Attack = let the Pet hunt (Defend). Repair = stop the Pet's hunt
            // (Idle) so this adapter owns its movement toward the tower.
            _pet.Mode = role == Role.Attack ? PetMode.Defend : PetMode.Idle;
        }

        private void Update()
        {
            if (_role != Role.Repair || _pet == null || _tower == null || !_pet.IsAlive)
                return;

            Vector3 towerPos = _tower.transform.position;
            Vector3 self = transform.position;
            float planar = Vector3.ProjectOnPlane(towerPos - self, Vector3.up).magnitude;

            if (planar > _repairRange)
            {
                // Walk toward the tower (the Pet is Idle, so it isn't moving itself).
                Vector3 flatTarget = new Vector3(towerPos.x, self.y, towerPos.z);
                transform.position = Vector3.MoveTowards(self, flatTarget, _moveSpeed * Time.deltaTime);

                Vector3 face = flatTarget - self; face.y = 0f;
                if (face.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, Quaternion.LookRotation(face), 10f * Time.deltaTime);
            }
            else
            {
                // In range — top up the canonical tower HP (HeartController.Hp).
                _tower.SetHp(_tower.Hp + _repairPerSecond * Time.deltaTime);
            }
        }
    }
}
