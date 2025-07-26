using Authoring;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Stateful;
using Unity.Transforms;
using UnityEngine;

namespace Systems
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(CollisionDetectionSystem))]
    public partial struct ShootingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;

            var ecbSingleton = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (attackTimer, soldier, soldierTransform, targetData, entity) in
                     SystemAPI.Query<RefRW<AttackTimer>, RefRO<Soldier>, RefRO<LocalTransform>, RefRO<TargetData>>()
                         .WithEntityAccess())
            {
                if (currentTime - attackTimer.ValueRO.lastAttackTime < attackTimer.ValueRO.attackCooldown) continue;

                if (!targetData.ValueRO.HasTarget || targetData.ValueRO.TargetEntity == Entity.Null) continue;

                if (!SystemAPI.Exists(targetData.ValueRO.TargetEntity)) continue;

                float distance = math.distance(soldierTransform.ValueRO.Position, targetData.ValueRO.TargetPosition);

                if (distance > soldier.ValueRO.shootingRange) continue;

                float3 directionToTarget = math.normalize(targetData.ValueRO.TargetPosition - soldierTransform.ValueRO.Position);
                float3 forwardDirection = math.mul(soldierTransform.ValueRO.Rotation, new float3(0, 0, 1));
                
                float facingDot = math.dot(forwardDirection, directionToTarget);
                float maxFacingAngle = math.cos(math.radians(soldier.ValueRO.facingTolerance));
                
                if (facingDot >= maxFacingAngle)
                {
                    attackTimer.ValueRW.lastAttackTime = currentTime;
                    attackTimer.ValueRW.attackCooldown = 1f / soldier.ValueRO.atkSpeed;

                    float3 worldSpawnPosition = soldierTransform.ValueRO.Position +
                                                math.mul(soldierTransform.ValueRO.Rotation,
                                                    soldier.ValueRO.bulletSpawnOffset);
                    quaternion worldBulletRotation = math.mul(soldierTransform.ValueRO.Rotation,
                        soldier.ValueRO.bulletSpawnRotation);

                    Entity bulletEntity = ecb.Instantiate(soldier.ValueRO.bulletPrefab);

                    ecb.SetComponent(bulletEntity, new LocalTransform
                    {
                        Position = worldSpawnPosition,
                        Rotation = worldBulletRotation,
                        Scale = 1f
                    });

                    ecb.SetComponent(bulletEntity, new Bullet
                    {
                        damage = soldier.ValueRO.shootingDamage,
                        maxLifetime = 5,
                        timeOfCreation = SystemAPI.Time.ElapsedTime
                    });

                    float3 shootDirection = math.normalize(targetData.ValueRO.TargetPosition - worldSpawnPosition);
                    float3 finalDirection = new float3(shootDirection.x, 0f, shootDirection.z);
                    finalDirection = math.normalize(finalDirection);

                    ecb.SetComponent(bulletEntity, new PhysicsVelocity
                    {
                        Linear = finalDirection * soldier.ValueRO.shootingForce,
                        Angular = float3.zero
                    });
                }
            }
        }
    }
}