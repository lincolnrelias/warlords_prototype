using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Authoring
{
    public class MeleeAttackAuthoring : MonoBehaviour
    {
        public int damage = 25;
        public float attackRadius = 2f;
        public float attackInterval = 1.5f;
        public Transform attackPoint;

        private void OnDrawGizmosSelected()
        {
            if (attackPoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
            }
        }

        private class Baker : Baker<MeleeAttackAuthoring>
        {
            public override void Bake(MeleeAttackAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new MeleeAttack
                {
                    damage = authoring.damage,
                    attackRadius = authoring.attackRadius,
                    attackPoint = authoring.attackPoint != null ? authoring.attackPoint.localPosition : float3.zero
                });
                
                AddComponent(entity, new AttackTimer
                {
                    lastAttackTime = 0f,
                    attackCooldown = authoring.attackInterval
                });
            }
        }
    }

    public struct MeleeAttack : IComponentData
    {
        public int damage;
        public float attackRadius;
        public float3 attackPoint;
    }
}