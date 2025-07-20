using Unity.Entities;
using UnityEngine;

namespace Authoring
{
    public class HealthAuthoring : MonoBehaviour
    {
        public int maxHealth = 100;

        private class Baker : Baker<HealthAuthoring>
        {
            public override void Bake(HealthAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new Health
                    {
                        maxHealth = authoring.maxHealth, currentHealth = authoring.maxHealth, isDead = false
                    });
            }
        }
    }

    public struct Health : IComponentData
    {
        public float maxHealth;
        public float currentHealth;
        public bool isDead; // Explicit death state flag

        public readonly bool IsAlive => currentHealth > 0 && !isDead;
        public readonly float HealthPercentage => maxHealth > 0 ? currentHealth / maxHealth : 0;
    }

    public struct FlagForCleanup : IComponentData
    {
    }
}