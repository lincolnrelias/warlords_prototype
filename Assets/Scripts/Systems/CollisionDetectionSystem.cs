using Authoring;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
public partial struct CollisionDetectionSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SimulationSingleton>();
        state.RequireForUpdate<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        ComponentLookup<Bullet> bulletLookup = SystemAPI.GetComponentLookup<Bullet>(true);
        ComponentLookup<Unit> unitLookup = SystemAPI.GetComponentLookup<Unit>(true);
        ComponentLookup<Health> healthLookup = SystemAPI.GetComponentLookup<Health>();
        ComponentLookup<FlagForCleanup> cleanupLookup = SystemAPI.GetComponentLookup<FlagForCleanup>(true);

        var collisionJob = new CollisionJob
        {
            bulletLookup = bulletLookup,
            unitLookup = unitLookup,
            healthLookup = healthLookup,
            cleanupLookup = cleanupLookup,
            ecbWriter = ecb.AsParallelWriter()
        };
        state.Dependency = collisionJob.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        var triggerJob = new TriggerJob();
        state.Dependency = triggerJob.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
    }
}

public struct CollisionJob : ICollisionEventsJob
{
    [ReadOnly] public ComponentLookup<Bullet> bulletLookup;
    [ReadOnly] public ComponentLookup<Unit> unitLookup;
    public ComponentLookup<Health> healthLookup;
    [ReadOnly] public ComponentLookup<FlagForCleanup> cleanupLookup;
    public EntityCommandBuffer.ParallelWriter ecbWriter;

    public void Execute(CollisionEvent collisionEvent)
    {
        var entityA = collisionEvent.EntityA;
        var entityB = collisionEvent.EntityB;

        if (IsBulletUnitCollision(entityA, entityB, out Entity bulletEntity, out Entity unitEntity))
        {
            HandleBulletUnitCollision(bulletEntity, unitEntity, entityA.Index);
        }
    }

    private bool IsBulletUnitCollision(Entity entityA, Entity entityB, out Entity bulletEntity, out Entity unitEntity)
    {
        bulletEntity = Entity.Null;
        unitEntity = Entity.Null;

        if (bulletLookup.HasComponent(entityA) && unitLookup.HasComponent(entityB))
        {
            bulletEntity = entityA;
            unitEntity = entityB;
            return true;
        }

        if (unitLookup.HasComponent(entityA) && bulletLookup.HasComponent(entityB))
        {
            bulletEntity = entityB;
            unitEntity = entityA;
            return true;
        }

        return false;
    }

    private void HandleBulletUnitCollision(Entity bulletEntity, Entity unitEntity, int sortKey)
    {
        ecbWriter.AddComponent<FlagForCleanup>(sortKey, bulletEntity);
        ecbWriter.RemoveComponent<PhysicsCollider>(sortKey, bulletEntity);
        if (cleanupLookup.HasComponent(unitEntity))
            return;

        var bullet = bulletLookup[bulletEntity];

        if (healthLookup.HasComponent(unitEntity))
        {
            var health = healthLookup[unitEntity];
            health.currentHealth -= bullet.damage;
            healthLookup[unitEntity] = health;
            Debug.Log(unitEntity.Index + " took damage: " + bullet.damage);
            if (health.currentHealth <= 0)
            {
                ecbWriter.AddComponent<FlagForCleanup>(sortKey, unitEntity);
            }
        }
    }
}

public struct TriggerJob : ITriggerEventsJob
{
    public void Execute(TriggerEvent triggerEvent)
    {
        Debug.Log($"Trigger: Entity {triggerEvent.EntityA.Index} triggered Entity {triggerEvent.EntityB.Index}");
    }
}