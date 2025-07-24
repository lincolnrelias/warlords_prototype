using Authoring;
using Data;
using Unity.Burst;
using Unity.Entities;

namespace Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CollisionDetectionSystem))]
    public partial struct FactionHealthSystem : ISystem
    {
        private double lastUpdateTime;
        private const double UPDATE_INTERVAL = 0.1; // Update 10 times per second
        private bool initializedHealth;
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Create singleton entity for faction health data
            Entity healthEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(healthEntity, new FactionHealthData());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            double currentTime = SystemAPI.Time.ElapsedTime;
        
            if (currentTime - lastUpdateTime < UPDATE_INTERVAL)
                return;
            
            lastUpdateTime = currentTime;
            var healthSingleton = SystemAPI.GetSingleton<FactionHealthData>();
            float friendlyCurrentHealth = 0f;
            float friendlyMaxHealth = 0f;
            float hostileCurrentHealth = 0f;
            float hostileMaxHealth = 0f;

            foreach (var (health, unit) in SystemAPI.Query<RefRO<Health>, RefRO<Unit>>()
                         .WithNone<FlagForCleanup>())
            {
                if (unit.ValueRO.faction == Faction.Friendly)
                {
                    friendlyCurrentHealth += health.ValueRO.currentHealth;
                    friendlyMaxHealth += health.ValueRO.maxHealth;
                }
                else if (unit.ValueRO.faction == Faction.Hostile)
                {
                    hostileCurrentHealth += health.ValueRO.currentHealth;
                    hostileMaxHealth += health.ValueRO.maxHealth;
                }
            }

            // Update singleton
            SystemAPI.SetSingleton(new FactionHealthData
            {
                friendlyCurrentHealth = friendlyCurrentHealth,
                friendlyMaxHealth = initializedHealth?healthSingleton.friendlyMaxHealth:friendlyMaxHealth,
                hostileCurrentHealth = hostileCurrentHealth,
                hostileMaxHealth = initializedHealth?healthSingleton.hostileMaxHealth:hostileMaxHealth
            });
            initializedHealth = true;
        }
    }
}