using Authoring;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct UnitCleanupSystem : ISystem
    {
        private double lastCleanupTime;
        private const double CLEANUP_INTERVAL = 0.1; // Check every 100ms

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            lastCleanupTime = SystemAPI.Time.ElapsedTime;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            double currentTime = SystemAPI.Time.ElapsedTime;

            if (currentTime - lastCleanupTime < CLEANUP_INTERVAL)
                return;

            lastCleanupTime = currentTime;

            using EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Process only units in current batch
            foreach (var (component, entity) in SystemAPI.Query<RefRO<FlagForCleanup>>().WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
        }
    }
}