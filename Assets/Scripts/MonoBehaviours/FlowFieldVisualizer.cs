using Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[ExecuteInEditMode]
public class FlowFieldVisualizer : MonoBehaviour
{
    [Header("Visualization Settings")]
    public bool enableVisualization = true;
    public Faction factionToVisualize = Faction.Friendly;
    public Color friendlyColor = Color.blue;
    public Color hostileColor = Color.red;
    public Color obstacleColor = Color.black;
    public Color targetColor = Color.green;
    public float arrowLength = 1f;
    public float arrowHeadSize = 0.3f;
    public bool showCosts = false;
    public bool showObstacles = true;
    public bool showTargets = true;
    
    [Header("Performance Settings")]
    public bool limitDrawDistance = true;
    public float maxDrawDistance = 50f;
    public int maxCellsToVisualize = 10000;
    public bool onlyShowAroundUnits = true;

    private void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.SceneView.RepaintAll();
#endif
    }

    private void OnDrawGizmos()
    {
        if (!enableVisualization || !Application.isPlaying)
            return;

        DrawFlowField();
    }

    private void DrawFlowField()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        var entityManager = world.EntityManager;
        
        // Get flow field settings
        var settingsQuery = entityManager.CreateEntityQuery(typeof(FlowFieldSettings));
        if (settingsQuery.CalculateEntityCount() == 0) return;
        
        var settings = settingsQuery.GetSingleton<FlowFieldSettings>();
        
        // Find flow field data for the selected faction
        var flowFieldQuery = entityManager.CreateEntityQuery(typeof(FlowFieldData));
        var flowFieldEntities = flowFieldQuery.ToEntityArray(Allocator.Temp);
        
        FlowFieldData? targetFlowField = null;
        
        foreach (var entity in flowFieldEntities)
        {
            var flowFieldData = entityManager.GetComponentData<FlowFieldData>(entity);
            if (flowFieldData.faction == factionToVisualize)
            {
                targetFlowField = flowFieldData;
                break;
            }
        }
        
        flowFieldEntities.Dispose();
        
        if (!targetFlowField.HasValue || !targetFlowField.Value.flowField.IsCreated)
            return;
            
        var flowField = targetFlowField.Value;
        ref var blob = ref flowField.flowField.Value;
        
        // Set colors based on faction
        Color arrowColor = factionToVisualize == Faction.Friendly ? friendlyColor : hostileColor;
        
        // Get camera position for distance culling
        Vector3 cameraPos = Camera.current != null ? Camera.current.transform.position : Vector3.zero;
        
        // Get unit positions if we want to focus visualization around them
        Vector3[] unitPositions = GetUnitPositions(entityManager);
        
        int cellsDrawn = 0;
        
        // Draw flow field with performance optimizations
        for (int y = 0; y < flowField.gridSize.y && cellsDrawn < maxCellsToVisualize; y++)
        {
            for (int x = 0; x < flowField.gridSize.x && cellsDrawn < maxCellsToVisualize; x++)
            {
                int index = y * flowField.gridSize.x + x;
                
                float3 worldPos = GridToWorldPosition(new int2(x, y), flowField);
                
                // Distance culling
                if (limitDrawDistance)
                {
                    float distanceToCamera = Vector3.Distance(cameraPos, worldPos);
                    if (distanceToCamera > maxDrawDistance)
                        continue;
                }
                
                // Only show around units if enabled
                if (onlyShowAroundUnits && unitPositions.Length > 0)
                {
                    bool nearUnit = false;
                    foreach (var unitPos in unitPositions)
                    {
                        if (Vector3.Distance(unitPos, worldPos) < maxDrawDistance * 0.5f)
                        {
                            nearUnit = true;
                            break;
                        }
                    }
                    if (!nearUnit) continue;
                }
                
                cellsDrawn++;
                
                // Draw obstacles
                if (showObstacles && blob.obstacles[index])
                {
                    Gizmos.color = obstacleColor;
                    Gizmos.DrawCube(worldPos, Vector3.one * flowField.cellSize * 0.8f);
                    continue;
                }
                
                // Draw targets
                if (showTargets && blob.costs[index] == 0f)
                {
                    Gizmos.color = targetColor;
                    Gizmos.DrawSphere(worldPos, flowField.cellSize * 0.3f);
                }
                
                // Draw flow direction arrows
                float2 direction = blob.directions[index];
                if (math.lengthsq(direction) > 0.01f)
                {
                    Gizmos.color = arrowColor;
                    
                    float3 dir3D = new float3(direction.x, 0, direction.y);
                    float3 arrowStart = worldPos;
                    float3 arrowEnd = arrowStart + dir3D * arrowLength * flowField.cellSize * 0.4f;
                    
                    // Draw arrow line
                    Gizmos.DrawLine(arrowStart, arrowEnd);
                    
                    // Draw arrow head
                    float3 right = math.cross(dir3D, math.up()) * arrowHeadSize * flowField.cellSize;
                    float3 arrowHead1 = arrowEnd - dir3D * arrowHeadSize * flowField.cellSize + right;
                    float3 arrowHead2 = arrowEnd - dir3D * arrowHeadSize * flowField.cellSize - right;
                    
                    Gizmos.DrawLine(arrowEnd, arrowHead1);
                    Gizmos.DrawLine(arrowEnd, arrowHead2);
                }
                
                // Draw cost text
                if (showCosts && blob.costs[index] < 999f)
                {
#if UNITY_EDITOR
                    UnityEditor.Handles.color = Color.white;
                    UnityEditor.Handles.Label(new Vector3(worldPos.x,worldPos.y,worldPos.z) + Vector3.up * 0.1f, 
                        blob.costs[index].ToString("F1"));
#endif
                }
            }
        }
        
        // Debug info
        if (cellsDrawn >= maxCellsToVisualize)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(cameraPos + Vector3.up * 5, Vector3.one);
        }
    }
    
    private Vector3[] GetUnitPositions(EntityManager entityManager)
    {
        var unitsQuery = entityManager.CreateEntityQuery(typeof(Unit), typeof(Unity.Transforms.LocalTransform));
        var transforms = unitsQuery.ToComponentDataArray<Unity.Transforms.LocalTransform>(Allocator.Temp);
        
        Vector3[] positions = new Vector3[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
        {
            positions[i] = transforms[i].Position;
        }
        
        transforms.Dispose();
        return positions;
    }

    private float3 GridToWorldPosition(int2 gridPos, FlowFieldData flowField)
    {
        return flowField.gridOrigin + new float3(
            (gridPos.x + 0.5f) * flowField.cellSize,
            0,
            (gridPos.y + 0.5f) * flowField.cellSize
        );
    }

    // Public method to toggle faction visualization
    public void ToggleFaction()
    {
        factionToVisualize = factionToVisualize == Faction.Friendly ? Faction.Hostile : Faction.Friendly;
        
#if UNITY_EDITOR
        UnityEditor.SceneView.RepaintAll();
#endif
    }

    // Public method to update visualization settings at runtime
    public void UpdateVisualizationSettings(bool enable, Faction faction)
    {
        enableVisualization = enable;
        factionToVisualize = faction;
        
        // Update the singleton settings if possible
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            var entityManager = world.EntityManager;
            var settingsQuery = entityManager.CreateEntityQuery(typeof(FlowFieldSettings));
            
            if (settingsQuery.CalculateEntityCount() > 0)
            {
                var entity = settingsQuery.GetSingletonEntity();
                var settings = entityManager.GetComponentData<FlowFieldSettings>(entity);
                settings.enableVisualization = enable;
                settings.visualizedFaction = faction;
                entityManager.SetComponentData(entity, settings);
            }
        }
        
#if UNITY_EDITOR
        UnityEditor.SceneView.RepaintAll();
#endif
    }
}