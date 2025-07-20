using Authoring;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;

namespace Systems
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    //[UpdateAfter(typeof(TagForCollisionEventRaising))]
    public partial struct AllowCollisionEventRaisingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

            foreach (var (collider,
                         eventRaisingComponent,
                         entity) in
                     SystemAPI.Query<RefRW<PhysicsCollider>,
                             RefRW<EventRaisingCollidable>>()
                         .WithEntityAccess().WithAll<EventRaisingCollidable>())
            {
                if (!collider.ValueRO.Value.IsCreated) continue;
                collider.ValueRW.Value.Value.SetCollisionResponse(CollisionResponsePolicy.CollideRaiseCollisionEvents);
                ecb.SetComponentEnabled<EventRaisingCollidable>(entity, false);
            }

            ecb.Playback(state.EntityManager);
        }
    }
}