using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Collections;

partial struct UnitMoverSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var query = SystemAPI.QueryBuilder().WithAll<LocalTransform, UnitMover>().Build();
        var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var allPositions = new NativeArray<float3>(transforms.Length, Allocator.TempJob);

        for (int i = 0; i < transforms.Length; i++)
        {
            allPositions[i] = transforms[i].Position;
        }

        UnitMoverJob unitMoverJob = new UnitMoverJob
        {
            deltaTime = SystemAPI.Time.DeltaTime,
            allPositions = allPositions
        };

        var jobHandle = unitMoverJob.ScheduleParallel(state.Dependency);
        jobHandle.Complete();

        transforms.Dispose();
        allPositions.Dispose();
    }
}

[BurstCompile]
public partial struct UnitMoverJob : IJobEntity
{
    public float deltaTime;
    [ReadOnly] public NativeArray<float3> allPositions;

    public void Execute(ref LocalTransform localTransform, in UnitMover unitMover,
        ref PhysicsVelocity physicsVelocity, ref TargetData targetData)
    {
        float3 moveDirection = targetData.TargetPosition - localTransform.Position;
        float3 avoidanceForce = CalculateAvoidance(localTransform.Position, unitMover);

        float reachedTargetDistanceSq = unitMover.minDistanceToTarget * unitMover.minDistanceToTarget;
        if (math.lengthsq(moveDirection) < reachedTargetDistanceSq)
        {
            physicsVelocity.Linear = float3.zero;
            physicsVelocity.Angular = float3.zero;
            targetData.isInRange = true;
        }
        else
        {
            targetData.isInRange = false;

            // Combine target direction with avoidance
            float3 targetDir = math.normalize(moveDirection);
            float3 finalDirection = math.normalize(targetDir + avoidanceForce * unitMover.avoidanceWeight);

            physicsVelocity.Linear = finalDirection * unitMover.moveSpeed;
            physicsVelocity.Angular = float3.zero;
        }

        // Rotate towards final movement direction
        if (math.lengthsq(physicsVelocity.Linear) > 0.01f)
        {
            float3 lookDirection = math.normalize(physicsVelocity.Linear);
            localTransform.Rotation = math.slerp(localTransform.Rotation,
                quaternion.LookRotation(lookDirection, math.up()),
                deltaTime * unitMover.rotationSpeed);
        }
    }

    private float3 CalculateAvoidance(float3 position, UnitMover unitMover)
    {
        float3 separationForce = float3.zero;
        int neighborCount = 0;

        for (int i = 0; i < allPositions.Length; i++)
        {
            float3 otherPos = allPositions[i];
            float3 offset = position - otherPos;
            float distanceSq = math.lengthsq(offset);

            if (distanceSq > 0.01f && distanceSq < unitMover.avoidanceRadius * unitMover.avoidanceRadius)
            {
                separationForce += math.normalize(offset) / math.sqrt(distanceSq);
                neighborCount++;
            }
        }

        if (neighborCount > 0)
        {
            separationForce /= neighborCount;
            separationForce *= unitMover.separationStrength;
        }

        return separationForce;
    }
}