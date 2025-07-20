using Authoring;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateBefore(typeof(UnitCleanupSystem))]
    public partial struct BulletLifetimeSystem : ISystem
    {
        private double lastCleanupTime;
        private const double CLEANUP_INTERVAL = 0.1; // Check every 100ms

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            lastCleanupTime = SystemAPI.Time.ElapsedTime;
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            double currentTime = SystemAPI.Time.ElapsedTime;

            if (currentTime - lastCleanupTime < CLEANUP_INTERVAL)
                return;

            lastCleanupTime = currentTime;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (bullet, entity) in SystemAPI.Query<RefRO<Bullet>>().WithNone<FlagForCleanup>()
                         .WithEntityAccess())
            {
                var flagForRemoval = currentTime - bullet.ValueRO.timeOfCreation >= bullet.ValueRO.maxLifetime;
                if (flagForRemoval)
                {
                    ecb.AddComponent(entity, new FlagForCleanup());
                }
            }
        }
    }
}