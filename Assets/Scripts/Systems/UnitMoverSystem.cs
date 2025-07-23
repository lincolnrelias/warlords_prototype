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
                    // Rotate towards target when in range
                    var directionToTarget = math.normalize(targetPos - currentPos);
                    RotateTowards(ref transform.ValueRW, directionToTarget, mover.ValueRO, deltaTime);
                    continue;
                }
                
                // Sample flow field at current position
                var flowDirection = SampleFlowField(currentPos, flowField.Value);
                if (math.lengthsq(flowDirection) < 0.01f) continue;
                
                // Apply movement and rotation
                MoveUnit(ref transform.ValueRW, flowDirection, mover.ValueRO, deltaTime);
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
            var gridPos = WorldToGrid(worldPos, flowField);
            
            if (!IsValidGridPosition(gridPos, flowField.gridSize))
                return float2.zero;
            
            int index = GridToIndex(gridPos, flowField.gridSize);
            return flowField.flowField.Value.directions[index];
        }

        private void MoveUnit(ref LocalTransform transform, float2 flowDirection, UnitMover mover, float deltaTime)
        {
            var moveDirection = new float3(flowDirection.x, 0, flowDirection.y);
            var velocity = moveDirection * mover.moveSpeed * deltaTime;
            
            // Apply movement
            transform.Position += velocity;
            
            // Apply rotation towards movement direction
            RotateTowards(ref transform, moveDirection, mover, deltaTime);
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
    }
}