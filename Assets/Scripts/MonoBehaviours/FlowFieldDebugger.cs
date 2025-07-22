using Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class FlowFieldDebugger : MonoBehaviour
{
    [Header("Debug Info")]
    public bool showDebugInfo = true;
    public KeyCode debugKey = KeyCode.F1;
    
    private void Update()
    {
        if (Input.GetKeyDown(debugKey))
        {
            DebugFlowFieldSystem();
        }
    }
    
    private void OnGUI()
    {
        if (!showDebugInfo) return;
        
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            GUI.Label(new Rect(10, 10, 300, 20), "No ECS World found");
            return;
        }
        
        var entityManager = world.EntityManager;
        
        // Check for FlowFieldSettings
        var settingsQuery = entityManager.CreateEntityQuery(typeof(FlowFieldSettings));
        GUI.Label(new Rect(10, 10, 300, 20), $"FlowFieldSettings entities: {settingsQuery.CalculateEntityCount()}");
        
        // Check for FlowFieldData
        var flowFieldQuery = entityManager.CreateEntityQuery(typeof(FlowFieldData));
        int flowFieldCount = flowFieldQuery.CalculateEntityCount();
        GUI.Label(new Rect(10, 30, 300, 20), $"FlowFieldData entities: {flowFieldCount}");
        
        // Check for units
        var unitsQuery = entityManager.CreateEntityQuery(typeof(Unit));
        int unitCount = unitsQuery.CalculateEntityCount();
        GUI.Label(new Rect(10, 50, 300, 20), $"Units in scene: {unitCount}");
        
        if (flowFieldCount > 0)
        {
            var flowFieldEntities = flowFieldQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < flowFieldEntities.Length; i++)
            {
                var flowFieldData = entityManager.GetComponentData<FlowFieldData>(flowFieldEntities[i]);
                GUI.Label(new Rect(10, 70 + i * 20, 300, 20), 
                    $"FlowField {i}: Faction {flowFieldData.faction}, Size {flowFieldData.gridSize}");
            }
            flowFieldEntities.Dispose();
        }
        
        GUI.Label(new Rect(10, 150, 300, 20), $"Press {debugKey} to print debug info to console");
    }
    
    private void DebugFlowFieldSystem()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            Debug.Log("No ECS World found");
            return;
        }
        
        var entityManager = world.EntityManager;
        
        // Debug FlowFieldSettings
        var settingsQuery = entityManager.CreateEntityQuery(typeof(FlowFieldSettings));
        if (settingsQuery.CalculateEntityCount() > 0)
        {
            var settings = settingsQuery.GetSingleton<FlowFieldSettings>();
            Debug.Log($"FlowFieldSettings: GridSize={settings.gridSize}, CellSize={settings.cellSize}, " +
                     $"Origin={settings.worldOrigin}, MaxCost={settings.maxCost}, " +
                     $"Visualization={settings.enableVisualization}, Faction={settings.visualizedFaction}");
        }
        else
        {
            Debug.LogWarning("No FlowFieldSettings found! Make sure you have a FlowFieldAuthoring component in the scene.");
        }
        
        // Debug Units with positions
        var unitsQuery = entityManager.CreateEntityQuery(typeof(Unit), typeof(Unity.Transforms.LocalTransform));
        var units = unitsQuery.ToComponentDataArray<Unit>(Unity.Collections.Allocator.Temp);
        var transforms = unitsQuery.ToComponentDataArray<Unity.Transforms.LocalTransform>(Unity.Collections.Allocator.Temp);
        
        Debug.Log($"Found {units.Length} units in scene");
        
        int friendlyCount = 0, hostileCount = 0;
        Unity.Mathematics.float3 friendlyCenter = Unity.Mathematics.float3.zero;
        Unity.Mathematics.float3 hostileCenter = Unity.Mathematics.float3.zero;
        
        // Calculate unit positions and centers
        for (int i = 0; i < units.Length; i++)
        {
            if (units[i].faction == Faction.Friendly) 
            {
                friendlyCount++;
                friendlyCenter += transforms[i].Position;
                if (friendlyCount <= 3) // Log first few positions
                    Debug.Log($"Friendly unit {friendlyCount} at: {transforms[i].Position}");
            }
            else if (units[i].faction == Faction.Hostile) 
            {
                hostileCount++;
                hostileCenter += transforms[i].Position;
                if (hostileCount <= 3) // Log first few positions
                    Debug.Log($"Hostile unit {hostileCount} at: {transforms[i].Position}");
            }
        }
        
        if (friendlyCount > 0) friendlyCenter /= friendlyCount;
        if (hostileCount > 0) hostileCenter /= hostileCount;
        
        Debug.Log($"Friendly units: {friendlyCount} (center: {friendlyCenter}), Hostile units: {hostileCount} (center: {hostileCenter})");
        units.Dispose();
        transforms.Dispose();
        
        // Debug FlowField data with more detailed sampling
        var flowFieldQuery = entityManager.CreateEntityQuery(typeof(FlowFieldData));
        var flowFieldEntities = flowFieldQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        Debug.Log($"Found {flowFieldEntities.Length} FlowField entities");
        
        for (int i = 0; i < flowFieldEntities.Length; i++)
        {
            var flowFieldData = entityManager.GetComponentData<FlowFieldData>(flowFieldEntities[i]);
            Debug.Log($"FlowField {i}: Faction={flowFieldData.faction}, GridSize={flowFieldData.gridSize}, " +
                     $"CellSize={flowFieldData.cellSize}, Origin={flowFieldData.gridOrigin}");
            
            if (flowFieldData.flowField.IsCreated)
            {
                ref var blob = ref flowFieldData.flowField.Value;
                Debug.Log($"  - Directions: {blob.directions.Length}, Costs: {blob.costs.Length}, Obstacles: {blob.obstacles.Length}");
                
                // Sample multiple cells and look for non-zero directions
                int validDirections = 0;
                int obstacles = 0;
                int targets = 0;
                
                for (int j = 0; j < blob.directions.Length; j += 1000) // Sample every 1000th cell
                {
                    if (Unity.Mathematics.math.lengthsq(blob.directions[j]) > 0.01f)
                        validDirections++;
                    if (blob.obstacles[j])
                        obstacles++;
                    if (blob.costs[j] == 0f)
                        targets++;
                }
                
                Debug.Log($"  - Sampled cells: ValidDirections={validDirections}, Obstacles={obstacles}, Targets={targets}");
                
                // Sample around unit centers
                if (flowFieldData.faction == Faction.Friendly && hostileCount > 0)
                {
                    var gridPos = WorldToGrid(hostileCenter, flowFieldData);
                    if (IsValidGridPos(gridPos, flowFieldData.gridSize))
                    {
                        int index = gridPos.y * flowFieldData.gridSize.x + gridPos.x;
                        Debug.Log($"  - At hostile center {hostileCenter}: GridPos={gridPos}, Direction={blob.directions[index]}, Cost={blob.costs[index]}");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"FlowField {i} blob asset is not created!");
            }
        }
        flowFieldEntities.Dispose();
    }
    
    private Unity.Mathematics.int2 WorldToGrid(Unity.Mathematics.float3 worldPos, FlowFieldData flowField)
    {
        Unity.Mathematics.float3 localPos = worldPos - flowField.gridOrigin;
        return new Unity.Mathematics.int2(
            (int)Unity.Mathematics.math.floor(localPos.x / flowField.cellSize),
            (int)Unity.Mathematics.math.floor(localPos.z / flowField.cellSize)
        );
    }
    
    private bool IsValidGridPos(Unity.Mathematics.int2 gridPos, Unity.Mathematics.int2 gridSize)
    {
        return gridPos.x >= 0 && gridPos.x < gridSize.x && 
               gridPos.y >= 0 && gridPos.y < gridSize.y;
    }
}