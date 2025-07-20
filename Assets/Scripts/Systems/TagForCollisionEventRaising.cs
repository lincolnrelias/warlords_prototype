/*using Authoring;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics.Systems;
using UnityEngine;

namespace Systems
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct TagForCollisionEventRaising : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (bullet, entity) in SystemAPI.Query<RefRO<Bullet>>()
                         .WithEntityAccess()
                         .WithNone<EventRaisingCollidable>())
            {
                ecb.AddComponent<EventRaisingCollidable>(entity);
            }
        }
    }
}*/

