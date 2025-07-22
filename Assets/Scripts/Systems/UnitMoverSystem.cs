using Authoring;
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
            
            // Get flow field entities
            var flowFieldQuery = SystemAPI.QueryBuilder().WithAll<FlowFieldData>().Build();
            var flowFieldEntities = flowFieldQuery.ToEntityArray(Allocator.TempJob);
            var flowFieldDatas = flowFieldQuery.ToComponentDataArray<FlowFieldData>(Allocator.TempJob);
            
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            foreach (var (mover, unit, transform, targetData) in 
                SystemAPI.Query<RefRW<UnitMover>, RefRO<Unit>, RefRW<LocalTransform>, RefRO<TargetData>>())
            {
                if (!targetData.ValueRO.HasTarget) 
                    continue;
                
                // Find flow field for this unit's faction
                FlowFieldData? unitFlowField = null;
                for (int i = 0; i < flowFieldDatas.Length; i++)
                {
                    if (flowFieldDatas[i].faction == unit.ValueRO.faction)
                    {
                        unitFlowField = flowFieldDatas[i];
                        break;
                    }
                }
                
                if (!unitFlowField.HasValue || !unitFlowField.Value.flowField.IsCreated)
                {
                    continue;
                }
                
                var flowField = unitFlowField.Value;
                ref var blob = ref flowField.flowField.Value;
                
                // Check distance to target - stop if close enough
                float distanceToTarget = math.distance(transform.ValueRO.Position, targetData.ValueRO.TargetPosition);
                if (distanceToTarget <= mover.ValueRO.minDistanceToTarget) // Use fixed small distance instead of mover setting
                    continue;
                
                // Get flow field direction at current position
                int2 gridPos = WorldToGrid(transform.ValueRO.Position, flowField);
                if (!IsValidGridPosition(gridPos, flowField.gridSize))
                {
                    continue;
                }
                
                int index = GridToIndex(gridPos, flowField.gridSize);
                
                
                float2 flowDirection = blob.directions[index];
                
                
                if (math.lengthsq(flowDirection) < 0.01f)
                {
                    continue;
                }
                
                // Convert 2D flow direction to 3D movement direction
                float3 moveDirection = new float3(flowDirection.x, 0, flowDirection.y);
                
                // Move the unit
                float3 velocity = moveDirection * mover.ValueRO.moveSpeed * deltaTime;
                transform.ValueRW.Position += velocity;
                
                // Rotate towards movement direction
                if (math.lengthsq(moveDirection) > 0.01f)
                {
                    quaternion targetRotation = quaternion.LookRotationSafe(moveDirection, math.up());
                    transform.ValueRW.Rotation = math.slerp(transform.ValueRO.Rotation, targetRotation, 
                        mover.ValueRO.rotationSpeed * deltaTime);
                }
            }
            
            flowFieldEntities.Dispose();
            flowFieldDatas.Dispose();
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