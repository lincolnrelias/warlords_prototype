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
    [UpdateAfter(typeof(TargetFindingSystem))]
    public partial struct FlowFieldSystem : ISystem
    {
        private EntityQuery friendlyUnitsQuery;
        private EntityQuery hostileUnitsQuery;
        private ComponentLookup<LocalTransform> transformLookup;
        private ComponentLookup<Unit> unitLookup;
        private ComponentLookup<Health> healthLookup;

        private double lastUpdateTime;
        private const double UPDATE_INTERVAL = 0.2; // 5 times per second
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FlowFieldSettings>();

            friendlyUnitsQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Unit, LocalTransform, Health>()
                .Build(ref state);

            hostileUnitsQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Unit, LocalTransform, Health>()
                .Build(ref state);

            transformLookup = state.GetComponentLookup<LocalTransform>(true);
            unitLookup = state.GetComponentLookup<Unit>(true);
            healthLookup = state.GetComponentLookup<Health>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            double currentTime = SystemAPI.Time.ElapsedTime;
        
            if (currentTime - lastUpdateTime < UPDATE_INTERVAL)
                return;
            
            lastUpdateTime = currentTime;
        
            var settings = SystemAPI.GetSingleton<FlowFieldSettings>();
        
            transformLookup.Update(ref state);
            unitLookup.Update(ref state);
            healthLookup.Update(ref state);

            UpdateFlowFieldForFaction(ref state, Faction.Friendly, settings);
            UpdateFlowFieldForFaction(ref state, Faction.Hostile, settings);
        }

        private void UpdateFlowFieldForFaction(ref SystemState state, Faction faction, FlowFieldSettings settings)
        {
            // Get all units
            var allEntities = friendlyUnitsQuery.ToEntityArray(Allocator.TempJob);
            var allTransforms = friendlyUnitsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            var allUnits = friendlyUnitsQuery.ToComponentDataArray<Unit>(Allocator.TempJob);
            var allHealth = friendlyUnitsQuery.ToComponentDataArray<Health>(Allocator.TempJob);

            // Separate units by faction and get positions
            var friendlyPositions = new NativeList<float3>(Allocator.TempJob);
            var hostilePositions = new NativeList<float3>(Allocator.TempJob);

            for (int i = 0; i < allUnits.Length; i++)
            {
                if (!allHealth[i].IsAlive) continue;

                if (allUnits[i].faction == Faction.Friendly)
                    friendlyPositions.Add(allTransforms[i].Position);
                else if (allUnits[i].faction == Faction.Hostile)
                    hostilePositions.Add(allTransforms[i].Position);
            }

            // Calculate flow field
            var flowField = CalculateFlowField(
                settings,
                faction,
                friendlyPositions.AsArray(),
                hostilePositions.AsArray(),
                Allocator.TempJob
            );

            // Update or create flow field entity for this faction
            UpdateFlowFieldEntity(ref state, faction, settings, flowField);

            // Cleanup
            allEntities.Dispose();
            allTransforms.Dispose();
            allUnits.Dispose();
            allHealth.Dispose();
            friendlyPositions.Dispose();
            hostilePositions.Dispose();
            flowField.Dispose();
        }

        private NativeArray<FlowFieldCell> CalculateFlowField(
            FlowFieldSettings settings,
            Faction faction,
            NativeArray<float3> friendlyPositions,
            NativeArray<float3> hostilePositions,
            Allocator allocator)
        {
            int totalCells = settings.gridSize.x * settings.gridSize.y;
            var flowField = new NativeArray<FlowFieldCell>(totalCells, allocator);
            var costField = new NativeArray<float>(totalCells, allocator);
            var obstacles = new NativeArray<bool>(totalCells, allocator);

            // Initialize cost field with base values
            for (int i = 0; i < totalCells; i++)
            {
                costField[i] = settings.maxCost;
                obstacles[i] = false;
            }

// Create weight multiplier array
            var weightMultipliers = new NativeArray<float>(totalCells, allocator);
            for (int i = 0; i < totalCells; i++)
            {
                weightMultipliers[i] = 1f;
            }

            // Apply same-faction unit influence
            var sameFactionPositions = faction == Faction.Friendly ? friendlyPositions : hostilePositions;
            for (int i = 0; i < sameFactionPositions.Length; i++)
            {
                int2 unitGrid = WorldToGrid(sameFactionPositions[i], settings);
    
                // Influence unit cell and adjacent cells
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int2 gridPos = unitGrid + new int2(dx, dy);
                        if (IsValidGridPosition(gridPos, settings.gridSize))
                        {
                            int index = GridToIndex(gridPos, settings.gridSize);
                            weightMultipliers[index] += settings.neighborCostMultiplier;
                        }
                    }
                }
            }


            // Set target positions (enemy faction) with zero cost
            var targetPositions = faction == Faction.Friendly ? hostilePositions : friendlyPositions;
            var targetQueue = new NativeQueue<int2>(Allocator.Temp);

            for (int i = 0; i < targetPositions.Length; i++)
            {
                int2 gridPos = WorldToGrid(targetPositions[i], settings);
                if (IsValidGridPosition(gridPos, settings.gridSize))
                {
                    int index = GridToIndex(gridPos, settings.gridSize);
                    if (!obstacles[index])
                    {
                        costField[index] = 0f;
                        targetQueue.Enqueue(gridPos);
                    }
                }
            }

            // Dijkstra's algorithm to calculate cost field
            CalculateCostField(costField, obstacles, weightMultipliers, targetQueue, settings);
            weightMultipliers.Dispose();

            // Calculate flow directions based on cost field
            CalculateFlowDirections(flowField, costField, obstacles, settings);

            costField.Dispose();
            obstacles.Dispose();
            targetQueue.Dispose();
            return flowField;
        }

        private void CalculateCostField(
            NativeArray<float> costField,
            NativeArray<bool> obstacles,
            NativeArray<float> weightMultipliers,
            NativeQueue<int2> queue,
            FlowFieldSettings settings)
        {
            var directions = new NativeArray<int2>(8, Allocator.Temp);
            directions[0] = new int2(0, 1);   // North
            directions[1] = new int2(1, 1);   // Northeast
            directions[2] = new int2(1, 0);   // East
            directions[3] = new int2(1, -1);  // Southeast
            directions[4] = new int2(0, -1);  // South
            directions[5] = new int2(-1, -1); // Southwest
            directions[6] = new int2(-1, 0);  // West
            directions[7] = new int2(-1, 1);  // Northwest
    
            var costs = new NativeArray<float>(8, Allocator.Temp);
            costs[0] = 1f; costs[1] = 1.414f; costs[2] = 1f; costs[3] = 1.414f;
            costs[4] = 1f; costs[5] = 1.414f; costs[6] = 1f; costs[7] = 1.414f;

            while (queue.TryDequeue(out int2 current))
            {
                int currentIndex = GridToIndex(current, settings.gridSize);
                float currentCost = costField[currentIndex];

                for (int i = 0; i < directions.Length; i++)
                {
                    int2 neighbor = current + directions[i];

                    if (!IsValidGridPosition(neighbor, settings.gridSize))
                        continue;

                    int neighborIndex = GridToIndex(neighbor, settings.gridSize);

                    if (obstacles[neighborIndex])
                        continue;
            
                    // Apply weight multiplier
                    float calculatedCost = currentCost + (costs[i] * weightMultipliers[neighborIndex]);
                    float newCost = calculatedCost >= currentCost
                        ? math.min(calculatedCost, currentCost + settings.maxCostChangePerUpdate)
                        : math.max(calculatedCost, currentCost - settings.maxCostChangePerUpdate);
            
                    if (newCost < costField[neighborIndex])
                    {
                        costField[neighborIndex] = newCost;
                        queue.Enqueue(neighbor);
                    }
                }
            }
    
            directions.Dispose();
            costs.Dispose();
        }


        private void CalculateFlowDirections(
            NativeArray<FlowFieldCell> flowField,
            NativeArray<float> costField,
            NativeArray<bool> obstacles,
            FlowFieldSettings settings)
        {
            for (int y = 0; y < settings.gridSize.y; y++)
            {
                for (int x = 0; x < settings.gridSize.x; x++)
                {
                    int2 gridPos = new int2(x, y);
                    int index = GridToIndex(gridPos, settings.gridSize);

                    if (obstacles[index])
                    {
                        flowField[index] = new FlowFieldCell(float2.zero, settings.maxCost, true, false);
                        continue;
                    }

                    float currentCost = costField[index];
                    bool isTarget = currentCost == 0f;

                    if (isTarget)
                    {
                        flowField[index] = new FlowFieldCell(float2.zero, 0f, false, true);
                        continue;
                    }

                    // Find direction to neighbor with lowest cost
                    float2 bestDirection = float2.zero;
                    float lowestCost = currentCost;

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;

                            int2 neighbor = gridPos + new int2(dx, dy);

                            if (!IsValidGridPosition(neighbor, settings.gridSize))
                                continue;

                            int neighborIndex = GridToIndex(neighbor, settings.gridSize);
                            float neighborCost = costField[neighborIndex];

                            if (neighborCost < lowestCost)
                            {
                                lowestCost = neighborCost;
                                bestDirection = math.normalize(new float2(dx, dy));
                            }
                        }
                    }

                    flowField[index] = new FlowFieldCell(bestDirection, currentCost, false, false);
                }
            }
        }

        private void UpdateFlowFieldEntity(
            ref SystemState state,
            Faction faction,
            FlowFieldSettings settings,
            NativeArray<FlowFieldCell> flowField)
        {
            // Find existing flow field entity for this faction or create one
            var query = SystemAPI.QueryBuilder()
                .WithAll<FlowFieldData>()
                .Build();

            var entities = query.ToEntityArray(Allocator.Temp);
            var flowFieldDatas = query.ToComponentDataArray<FlowFieldData>(Allocator.Temp);

            Entity targetEntity = Entity.Null;
            bool entityExists = false;

            for (int i = 0; i < flowFieldDatas.Length; i++)
            {
                if (flowFieldDatas[i].faction == faction)
                {
                    targetEntity = entities[i];
                    entityExists = true;
                    break;
                }
            }

            // Create blob asset
            var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref var root = ref blobBuilder.ConstructRoot<FlowFieldBlob>();

            var directionsArray = blobBuilder.Allocate(ref root.directions, flowField.Length);
            var costsArray = blobBuilder.Allocate(ref root.costs, flowField.Length);
            var obstaclesArray = blobBuilder.Allocate(ref root.obstacles, flowField.Length);

            for (int i = 0; i < flowField.Length; i++)
            {
                directionsArray[i] = flowField[i].direction;
                costsArray[i] = flowField[i].cost;
                obstaclesArray[i] = flowField[i].isObstacle;
            }

            var blobAsset = blobBuilder.CreateBlobAssetReference<FlowFieldBlob>(Allocator.Persistent);
            blobBuilder.Dispose();

            if (entityExists)
            {
                // Dispose previous blob asset if it exists
                var oldData = state.EntityManager.GetComponentData<FlowFieldData>(targetEntity);
                if (oldData.flowField.IsCreated)
                    oldData.flowField.Dispose();

                // Update existing entity
                state.EntityManager.SetComponentData(targetEntity, new FlowFieldData
                {
                    faction = faction,
                    gridSize = settings.gridSize,
                    cellSize = settings.cellSize,
                    gridOrigin = settings.worldOrigin,
                    flowField = blobAsset
                });
            }
            else
            {
                // Create new entity for this faction's flow field
                targetEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(targetEntity, new FlowFieldData
                {
                    faction = faction,
                    gridSize = settings.gridSize,
                    cellSize = settings.cellSize,
                    gridOrigin = settings.worldOrigin,
                    flowField = blobAsset
                });
            }

            entities.Dispose();
            flowFieldDatas.Dispose();
        }

        // Helper methods
        private int2 WorldToGrid(float3 worldPos, FlowFieldSettings settings)
        {
            float3 localPos = worldPos - settings.worldOrigin;
            return new int2(
                (int)math.floor(localPos.x / settings.cellSize),
                (int)math.floor(localPos.z / settings.cellSize)
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