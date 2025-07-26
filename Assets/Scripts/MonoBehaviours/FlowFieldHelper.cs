using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public class FlowFieldHelper : MonoBehaviour
{
    [Header("Quick Setup")]
    public bool autoConfigureGrid = true;
    public float gridPadding = 20f;
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    [Header("Manual Settings")]
    public int2 suggestedGridSize = new int2(50, 50);
    public float suggestedCellSize = 2f;
    public float3 suggestedOrigin = float3.zero;
    public float maxCostChangePerUpdate = 10f;
    [Tooltip("How many cells to aim for")]
    public float suggestedGridCellCount = 100f; 
    
    [Header("Current Scene Analysis")]
    [SerializeField] private float3 unitBoundsMin;
    [SerializeField] private float3 unitBoundsMax;
    [SerializeField] private float3 unitBoundsSize;
    [SerializeField] private float3 unitBoundsCenter;
    [SerializeField] private int friendlyCount;
    [SerializeField] private int hostileCount;
    
    private void Start()
    {
        if (autoConfigureGrid)
        {
            AnalyzeScene();
            ConfigureFlowField();
        }
    }
    
    [ContextMenu("Analyze Scene")]
    public void AnalyzeScene()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;
        
        var entityManager = world.EntityManager;
        var unitsQuery = entityManager.CreateEntityQuery(typeof(Unit), typeof(Unity.Transforms.LocalTransform));
        
        if (unitsQuery.CalculateEntityCount() == 0)
        {
            if (showDebugLogs) Debug.LogWarning("No units found in scene!");
            return;
        }
        
        var units = unitsQuery.ToComponentDataArray<Unit>(Unity.Collections.Allocator.Temp);
        var transforms = unitsQuery.ToComponentDataArray<Unity.Transforms.LocalTransform>(Unity.Collections.Allocator.Temp);
        
        // Calculate bounds
        unitBoundsMin = transforms[0].Position;
        unitBoundsMax = transforms[0].Position;
        friendlyCount = 0;
        hostileCount = 0;
        
        for (int i = 0; i < transforms.Length; i++)
        {
            float3 pos = transforms[i].Position;
            unitBoundsMin = math.min(unitBoundsMin, pos);
            unitBoundsMax = math.max(unitBoundsMax, pos);
            
            if (units[i].faction == Faction.Friendly) friendlyCount++;
            else if (units[i].faction == Faction.Hostile) hostileCount++;
        }
        
        unitBoundsSize = unitBoundsMax - unitBoundsMin;
        unitBoundsCenter = (unitBoundsMin + unitBoundsMax) * 0.5f;
        
        // Calculate suggested settings
        float3 paddedSize = unitBoundsSize + new float3(gridPadding, 0, gridPadding);
        suggestedCellSize = math.max(1f, math.max(paddedSize.x, paddedSize.z) / suggestedGridCellCount); 
        
        suggestedGridSize = new int2(
            math.max(10, (int)math.ceil(paddedSize.x / suggestedCellSize)),
            math.max(10, (int)math.ceil(paddedSize.z / suggestedCellSize))
        );
        
        suggestedOrigin = new float3(
            unitBoundsCenter.x - (suggestedGridSize.x * suggestedCellSize) * 0.5f,
            0,
            unitBoundsCenter.z - (suggestedGridSize.y * suggestedCellSize) * 0.5f
        );
        
        units.Dispose();
        transforms.Dispose();
        
        if (showDebugLogs)
        {
            Debug.Log($"Scene Analysis Complete:");
            Debug.Log($"Unit Bounds: Min={unitBoundsMin}, Max={unitBoundsMax}, Size={unitBoundsSize}");
            Debug.Log($"Unit Center: {unitBoundsCenter}");
            Debug.Log($"Units: {friendlyCount} Friendly, {hostileCount} Hostile");
            Debug.Log($"Suggested Grid: Size={suggestedGridSize}, CellSize={suggestedCellSize}, Origin={suggestedOrigin}");
        }
    }
    
    [ContextMenu("Configure FlowField")]
    public void ConfigureFlowField()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;
        
        var entityManager = world.EntityManager;
        var settingsQuery = entityManager.CreateEntityQuery(typeof(FlowFieldSettings));
        
        if (settingsQuery.CalculateEntityCount() == 0)
        {
            if (showDebugLogs) Debug.LogWarning("No FlowFieldSettings found! Add a FlowFieldAuthoring component to the scene.");
            return;
        }
        
        var entity = settingsQuery.GetSingletonEntity();
        var settings = entityManager.GetComponentData<FlowFieldSettings>(entity);
        
        settings.gridSize = suggestedGridSize;
        settings.cellSize = suggestedCellSize;
        settings.worldOrigin = suggestedOrigin;
        settings.maxCostChangePerUpdate = maxCostChangePerUpdate;
        entityManager.SetComponentData(entity, settings);
        
        if (showDebugLogs)
        {
            Debug.Log($"FlowField configured with: GridSize={suggestedGridSize}, CellSize={suggestedCellSize}, Origin={suggestedOrigin}");
        }
    }
    
    private void OnDrawGizmos()
    {
        // Draw current unit bounds
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(unitBoundsCenter, unitBoundsSize);
        
        // Draw suggested grid bounds
        Gizmos.color = Color.yellow;
        float3 gridSize3D = new float3(suggestedGridSize.x * suggestedCellSize, 1, suggestedGridSize.y * suggestedCellSize);
        float3 gridCenter = suggestedOrigin + gridSize3D * 0.5f;
        Gizmos.DrawWireCube(gridCenter, gridSize3D);
        
        // Draw some grid lines for reference
        Gizmos.color = Color.white;
        for (int i = 0; i <= 10; i++)
        {
            float t = i / 10f;
            float3 start = suggestedOrigin + new float3(t * suggestedGridSize.x * suggestedCellSize, 0, 0);
            float3 end = start + new float3(0, 0, suggestedGridSize.y * suggestedCellSize);
            Gizmos.DrawLine(start, end);
            
            start = suggestedOrigin + new float3(0, 0, t * suggestedGridSize.y * suggestedCellSize);
            end = start + new float3(suggestedGridSize.x * suggestedCellSize, 0, 0);
            Gizmos.DrawLine(start, end);
        }
    }
}