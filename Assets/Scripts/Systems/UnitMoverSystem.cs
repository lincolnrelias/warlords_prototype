using Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FlowFieldSystem))]
    public partial struct FlowFieldMovementSystem : ISystem
    {
        private ComponentLookup<FlowFieldData> flowFieldLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            flowFieldLookup = state.GetComponentLookup<FlowFieldData>(true);
            state.RequireForUpdate<FlowFieldSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            flowFieldLookup.Update(ref state);
            var settings = SystemAPI.GetSingleton<FlowFieldSettings>();
            
            // Cache flow field data for the frame
            var flowFieldQuery = SystemAPI.QueryBuilder().WithAll<FlowFieldData>().Build();
            var flowFieldEntities = flowFieldQuery.ToEntityArray(Allocator.TempJob);
            var flowFieldDatas = flowFieldQuery.ToComponentDataArray<FlowFieldData>(Allocator.TempJob);
            
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            // Process all units with targets
            foreach (var (mover, unit, transform, targetData) in 
                SystemAPI.Query<RefRW<UnitMover>, RefRO<Unit>, RefRW<LocalTransform>, RefRO<TargetData>>())
            {
                if (!targetData.ValueRO.HasTarget) continue;
                
                var flowField = FindFlowFieldForFaction(unit.ValueRO.faction, flowFieldDatas);
                if (!flowField.HasValue || !flowField.Value.flowField.IsCreated) continue;
                
                var currentPos = transform.ValueRO.Position;
                var targetPos = targetData.ValueRO.TargetPosition;
                
                bool withinRange = math.distance(currentPos, targetPos) <= mover.ValueRO.minDistanceToTarget;
                
                if (withinRange)
                {
                    // Gradually slow down when in range
                    var directionToTarget = math.normalize(targetPos - currentPos);
                    ApplyDeceleration(ref mover.ValueRW, deltaTime);
                    transform.ValueRW.Position += mover.ValueRW.currentVelocity * deltaTime;
                    RotateTowards(ref transform.ValueRW, directionToTarget, mover.ValueRO, deltaTime);
                    continue;
                }
                
                // Sample flow field at current position
                var flowDirection = SampleFlowField(currentPos, flowField.Value);
                
                // If flow field direction is too weak, use direct movement to target
                if (math.lengthsq(flowDirection) < 0.01f)
                {
                    var directDirection = math.normalize(targetPos - currentPos);
                    flowDirection = new float2(directDirection.x, directDirection.z);
                }
                
                // Apply local unit avoidance
                var avoidanceDirection = CalculateLocalAvoidance(ref state, currentPos, unit.ValueRO.faction, 
                    flowFieldDatas, settings);
                
                // Combine flow field and avoidance directions
                var combinedDirection = math.normalize(flowDirection + avoidanceDirection * 0.7f);
                
                // Apply movement and rotation with smoothing
                MoveUnit(ref transform.ValueRW, combinedDirection, ref mover.ValueRW, deltaTime);
            }
            
            flowFieldEntities.Dispose();
            flowFieldDatas.Dispose();
        }

        private FlowFieldData? FindFlowFieldForFaction(Faction faction, NativeArray<FlowFieldData> flowFields)
        {
            for (int i = 0; i < flowFields.Length; i++)
            {
                if (flowFields[i].faction == faction)
                    return flowFields[i];
            }
            return null;
        }

        private float2 SampleFlowField(float3 worldPos, FlowFieldData flowField)
        {
            float3 localPos = worldPos - flowField.gridOrigin;
            float2 gridPosFloat = new float2(localPos.x / flowField.cellSize, localPos.z / flowField.cellSize);
            
            int2 gridPos00 = new int2((int)math.floor(gridPosFloat.x), (int)math.floor(gridPosFloat.y));
            
            // If completely outside flow field, return zero (will trigger fallback)
            if (!IsValidGridPosition(gridPos00, flowField.gridSize) &&
                !IsValidGridPosition(gridPos00 + new int2(1, 0), flowField.gridSize) &&
                !IsValidGridPosition(gridPos00 + new int2(0, 1), flowField.gridSize) &&
                !IsValidGridPosition(gridPos00 + new int2(1, 1), flowField.gridSize))
            {
                return float2.zero;
            }
            
            int2 gridPos10 = gridPos00 + new int2(1, 0);
            int2 gridPos01 = gridPos00 + new int2(0, 1);
            int2 gridPos11 = gridPos00 + new int2(1, 1);
            
            float2 fractionalPart = gridPosFloat - new float2(gridPos00.x, gridPos00.y);
            
            // For edge cases, use the nearest valid direction instead of zero
            float2 value00 = GetFlowFieldValue(gridPos00, flowField);
            float2 value10 = GetFlowFieldValue(gridPos10, flowField);
            float2 value01 = GetFlowFieldValue(gridPos01, flowField);
            float2 value11 = GetFlowFieldValue(gridPos11, flowField);
            
            float2 interpolatedX0 = math.lerp(value00, value10, fractionalPart.x);
            float2 interpolatedX1 = math.lerp(value01, value11, fractionalPart.x);
            
            return math.lerp(interpolatedX0, interpolatedX1, fractionalPart.y);
        }

        private void MoveUnit(ref LocalTransform transform, float2 flowDirection, ref UnitMover mover, float deltaTime)
        {
            var desiredDirection = new float3(flowDirection.x, 0, flowDirection.y);
            
            // Apply steering behavior with acceleration/deceleration
            var steering = CalculateSteering(desiredDirection, mover, deltaTime);
            mover.currentVelocity += steering * deltaTime;
            
            // Limit velocity to max speed
            float currentSpeed = math.length(mover.currentVelocity);
            if (currentSpeed > mover.moveSpeed)
            {
                mover.currentVelocity = math.normalize(mover.currentVelocity) * mover.moveSpeed;
            }
            
            // Apply movement
            transform.Position += mover.currentVelocity * deltaTime;
            
            // Apply rotation towards movement direction (smoother)
            if (currentSpeed > 0.1f)
            {
                var normalizedVelocity = math.normalize(mover.currentVelocity);
                RotateTowards(ref transform, normalizedVelocity, mover, deltaTime);
            }
        }

        private void RotateTowards(ref LocalTransform transform, float3 direction, UnitMover mover, float deltaTime)
        {
            if (math.lengthsq(direction) > 0.01f)
            {
                var targetRotation = quaternion.LookRotationSafe(direction, math.up());
                transform.Rotation = math.slerp(transform.Rotation, targetRotation, 
                    mover.rotationSpeed * deltaTime);
            }
        }
        
        private int2 WorldToGrid(float3 worldPos, FlowFieldData flowField)
        {
            float3 localPos = worldPos - flowField.gridOrigin;
            return new int2(
                (int)math.floor(localPos.x / flowField.cellSize),
                (int)math.floor(localPos.z / flowField.cellSize)
            );
        }
        
        private int GridToIndex(int2 gridPos, int2 gridSize)
        {
            return gridPos.y * gridSize.x + gridPos.x;
        }
        
        private bool IsValidGridPosition(int2 gridPos, int2 gridSize)
        {
            return gridPos.x >= 0 && gridPos.x < gridSize.x && 
                   gridPos.y >= 0 && gridPos.y < gridSize.y;
        }

        private float2 GetFlowFieldValue(int2 gridPos, FlowFieldData flowField)
        {
            if (IsValidGridPosition(gridPos, flowField.gridSize))
            {
                return flowField.flowField.Value.directions[GridToIndex(gridPos, flowField.gridSize)];
            }
            
            // Find nearest valid grid position
            int2 clampedPos = new int2(
                math.clamp(gridPos.x, 0, flowField.gridSize.x - 1),
                math.clamp(gridPos.y, 0, flowField.gridSize.y - 1)
            );
            
            return flowField.flowField.Value.directions[GridToIndex(clampedPos, flowField.gridSize)];
        }

        private float3 CalculateSteering(float3 desiredDirection, UnitMover mover, float deltaTime)
        {
            var desiredVelocity = desiredDirection * mover.moveSpeed;
            var steering = desiredVelocity - mover.currentVelocity;
            
            float steeringMagnitude = math.length(steering);
            if (steeringMagnitude > mover.acceleration)
            {
                steering = math.normalize(steering) * mover.acceleration;
            }
            
            return steering;
        }

        private float2 CalculateLocalAvoidance(
            ref SystemState state,
            float3 unitPosition, 
            Faction unitFaction, 
            NativeArray<FlowFieldData> flowFieldDatas, 
            FlowFieldSettings settings)
        {
            float2 avoidanceForce = float2.zero;
            float avoidanceRadius = settings.unitAvoidanceRadius;
            
            // Query nearby units of the same faction
            foreach (var (otherTransform, otherUnit) in 
                SystemAPI.Query<RefRO<LocalTransform>, RefRO<Unit>>())
            {
                if (otherUnit.ValueRO.faction != unitFaction)
                    continue;
                    
                float3 otherPosition = otherTransform.ValueRO.Position;
                float3 difference = unitPosition - otherPosition;
                float distance = math.length(difference);
                
                // Skip self and units outside avoidance radius
                if (distance < 0.1f || distance > avoidanceRadius)
                    continue;
                
                // Calculate repulsion force (stronger when closer)
                float2 repulsionDirection = math.normalize(new float2(difference.x, difference.z));
                float strength = (avoidanceRadius - distance) / avoidanceRadius;
                strength *=strength; // Square for more dramatic falloff
                
                avoidanceForce += repulsionDirection * strength;
            }
            
            return math.normalizesafe(avoidanceForce);
        }

        private void ApplyDeceleration(ref UnitMover mover, float deltaTime)
        {
            float currentSpeed = math.length(mover.currentVelocity);
            if (currentSpeed > 0.1f)
            {
                float decelerationAmount = mover.deceleration * deltaTime;
                if (decelerationAmount >= currentSpeed)
                {
                    mover.currentVelocity = float3.zero;
                }
                else
                {
                    var decelerationDirection = math.normalize(mover.currentVelocity);
                    mover.currentVelocity -= decelerationDirection * decelerationAmount;
                }
            }
            else
            {
                mover.currentVelocity = float3.zero;
            }
        }
    }
}