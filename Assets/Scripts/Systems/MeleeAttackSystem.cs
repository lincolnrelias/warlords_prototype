using Authoring;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CollisionDetectionSystem))]
    public partial struct MeleeAttackSystem : ISystem
    {
        private ComponentLookup<Unit> unitLookup;
        private ComponentLookup<Health> healthLookup;
        private ComponentLookup<LocalTransform> transformLookup;
        private ComponentLookup<FlagForCleanup> cleanupLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            unitLookup = state.GetComponentLookup<Unit>(true);
            healthLookup = state.GetComponentLookup<Health>();
            transformLookup = state.GetComponentLookup<LocalTransform>(true);
            cleanupLookup = state.GetComponentLookup<FlagForCleanup>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            
            unitLookup.Update(ref state);
            healthLookup.Update(ref state);
            transformLookup.Update(ref state);
            cleanupLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // Get physics world
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;

            foreach (var (meleeAttack, attackTimer, unit, transform, entity) in
                     SystemAPI.Query<RefRO<MeleeAttack>, RefRW<AttackTimer>, RefRO<Unit>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (currentTime - attackTimer.ValueRO.lastAttackTime < attackTimer.ValueRO.attackCooldown)
                    continue;

                // Calculate world attack point
                float3 worldAttackPoint = transform.ValueRO.Position + 
                                         math.mul(transform.ValueRO.Rotation, meleeAttack.ValueRO.attackPoint);

                // Perform overlap sphere to find targets
                var filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = ~0u
                };

                var hits = new NativeList<DistanceHit>(Allocator.Temp);
                if (physicsWorld.OverlapSphere(worldAttackPoint, meleeAttack.ValueRO.attackRadius, ref hits, filter))
                {
                    // Find first target of opposing faction
                    foreach (var hit in hits)
                    {
                        Entity hitEntity = hit.Entity;

                        if (!unitLookup.HasComponent(hitEntity) || cleanupLookup.HasComponent(hitEntity))
                            continue;

                        var hitUnit = unitLookup[hitEntity];
                        
                        // Check if it's an opposing faction
                        if (hitUnit.faction == unit.ValueRO.targetFaction)
                        {
                            // Deal damage
                            if (healthLookup.HasComponent(hitEntity))
                            {
                                var health = healthLookup[hitEntity];
                                health.currentHealth -= meleeAttack.ValueRO.damage;
                                healthLookup[hitEntity] = health;
                                
                                if (health.currentHealth <= 0)
                                {
                                    ecb.AddComponent<FlagForCleanup>(hitEntity);
                                }
                            }

                            // Update attack timer and break after first hit
                            attackTimer.ValueRW.lastAttackTime = currentTime;
                            break;
                        }
                    }
                }

                hits.Dispose();
            }
        }
    }
}