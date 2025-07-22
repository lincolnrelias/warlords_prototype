using Data;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class FlowFieldRuntimeVisualizer : MonoBehaviour
{
    [Header("Visualization Settings")]
    public bool enableVisualization = true;
    public Faction factionToVisualize = Faction.Friendly;
    
    [Header("Colors")]
    public Color friendlyColor = Color.blue;
    public Color hostileColor = Color.red;
    public Color obstacleColor = Color.black;
    public Color targetColor = Color.green;
    
    [Header("Arrow Settings")]
    public float arrowLength = 1f;
    public float arrowWidth = 0.1f;
    public GameObject arrowPrefab; // Optional: custom arrow prefab
    
    [Header("Performance Settings")]
    public bool limitDrawDistance = true;
    public float maxDrawDistance = 50f;
    public int maxArrowsToRender = 500;
    public bool onlyShowAroundUnits = true;
    public float updateFrequency = 0.2f;
    
    [Header("Display Options")]
    public bool showArrows = true;
    public bool showObstacles = true;
    public bool showTargets = true;
    
    [Header("Prefabs")]
    public GameObject obstaclePrefab;
    public GameObject targetPrefab;
    
    // Runtime data
    private List<GameObject> arrowObjects = new List<GameObject>();
    private List<GameObject> obstacleObjects = new List<GameObject>();
    private List<GameObject> targetObjects = new List<GameObject>();
    private List<LineRenderer> arrowLines = new List<LineRenderer>();
    
    private float lastUpdateTime;
    private Camera mainCamera;
    private Transform visualizationParent;
    
    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindFirstObjectByType<Camera>();
        
        // Create parent object for organization
        GameObject parentGO = new GameObject("FlowField Visualization");
        visualizationParent = parentGO.transform;
        visualizationParent.SetParent(transform);
        
        CreateDefaultPrefabs();
    }
    
    private void CreateDefaultPrefabs()
    {
        // Create default arrow prefab if none provided
        if (arrowPrefab == null)
        {
            arrowPrefab = CreateArrowPrefab();
        }
        
        // Create default obstacle prefab if none provided
        if (obstaclePrefab == null)
        {
            obstaclePrefab = CreateObstaclePrefab();
        }
        
        // Create default target prefab if none provided
        if (targetPrefab == null)
        {
            targetPrefab = CreateTargetPrefab();
        }
    }
    
    private GameObject CreateArrowPrefab()
    {
        GameObject arrow = new GameObject("Arrow");
        
        // Add LineRenderer for the arrow
        LineRenderer line = arrow.AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.SetColors(friendlyColor,friendlyColor);
        line.startWidth = arrowWidth;
        line.endWidth = arrowWidth;
        line.positionCount = 2;
        line.useWorldSpace = false;
        
        // Create arrow head using a simple mesh
        GameObject arrowHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        arrowHead.transform.SetParent(arrow.transform);
        arrowHead.transform.localPosition = Vector3.forward * arrowLength;
        arrowHead.transform.localRotation = Quaternion.Euler(90, 0, 0);
        arrowHead.transform.localScale = Vector3.one * (arrowWidth * 3);
        
        // Remove colliders
        Destroy(arrowHead.GetComponent<Collider>());
        
        arrow.SetActive(false);
        return arrow;
    }
    
    private GameObject CreateObstaclePrefab()
    {
        GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.name = "Obstacle";
        obstacle.GetComponent<Renderer>().material.color = obstacleColor;
        Destroy(obstacle.GetComponent<Collider>());
        obstacle.SetActive(false);
        return obstacle;
    }
    
    private GameObject CreateTargetPrefab()
    {
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        target.name = "Target";
        target.GetComponent<Renderer>().material.color = targetColor;
        Destroy(target.GetComponent<Collider>());
        target.SetActive(false);
        return target;
    }
    
    private void Update()
    {
        if (!enableVisualization)
        {
            HideAllVisuals();
            return;
        }
        
        if (Time.time - lastUpdateTime > updateFrequency)
        {
            UpdateVisualization();
            lastUpdateTime = Time.time;
        }
    }
    
    private void UpdateVisualization()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            HideAllVisuals();
            return;
        }
        
        var entityManager = world.EntityManager;
        
        // Find flow field data
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
        {
            HideAllVisuals();
            return;
        }
        
        var flowField = targetFlowField.Value;
        ref var blob = ref flowField.flowField.Value;
        
        // Get unit positions and camera position
        Vector3[] unitPositions = GetUnitPositions(entityManager);
        Vector3 cameraPos = mainCamera != null ? mainCamera.transform.position : Vector3.zero;
        
        // Clear previous visuals
        HideAllVisuals();
        
        // Collect visible cells
        List<CellVisualizationData> visibleCells = new List<CellVisualizationData>();
        
        for (int y = 0; y < flowField.gridSize.y && visibleCells.Count < maxArrowsToRender; y++)
        {
            for (int x = 0; x < flowField.gridSize.x && visibleCells.Count < maxArrowsToRender; x++)
            {
                int index = y * flowField.gridSize.x + x;
                float3 worldPos = GridToWorldPosition(new int2(x, y), flowField);
                
                // Distance culling
                if (limitDrawDistance && Vector3.Distance(cameraPos, worldPos) > maxDrawDistance)
                    continue;
                
                // Unit proximity culling
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
                
                // Add cell data
                visibleCells.Add(new CellVisualizationData
                {
                    worldPosition = worldPos,
                    direction = blob.directions[index],
                    cost = blob.costs[index],
                    isObstacle = blob.obstacles[index],
                    cellSize = flowField.cellSize
                });
            }
        }
        
        // Create visuals for visible cells
        CreateVisuals(visibleCells);
    }
    
    private void CreateVisuals(List<CellVisualizationData> cells)
    {
        Color currentArrowColor = factionToVisualize == Faction.Friendly ? friendlyColor : hostileColor;
        
        foreach (var cell in cells)
        {
            // Create obstacles
            if (showObstacles && cell.isObstacle)
            {
                GameObject obstacle = GetOrCreateObstacle();
                obstacle.transform.position = cell.worldPosition;
                obstacle.transform.localScale = Vector3.one * cell.cellSize * 0.8f;
                obstacle.SetActive(true);
                continue;
            }
            
            // Create targets
            if (showTargets && cell.cost == 0f)
            {
                GameObject target = GetOrCreateTarget();
                target.transform.position = cell.worldPosition;
                target.transform.localScale = Vector3.one * cell.cellSize * 0.6f;
                target.SetActive(true);
            }
            
            // Create arrows
            if (showArrows && math.lengthsq(cell.direction) > 0.01f)
            {
                GameObject arrow = GetOrCreateArrow();
                
                float3 direction3D = new float3(cell.direction.x, 0, cell.direction.y);
                arrow.transform.position = cell.worldPosition;
                arrow.transform.rotation = Quaternion.LookRotation(direction3D, Vector3.up);
                arrow.transform.localScale = Vector3.one * cell.cellSize;
                
                // Update arrow color
                LineRenderer line = arrow.GetComponent<LineRenderer>();
                if (line != null)
                {
                    line.SetColors(currentArrowColor,currentArrowColor);
                    line.SetPosition(0, Vector3.zero);
                    line.SetPosition(1, Vector3.forward * arrowLength);
                }
                
                // Update arrow head color
                Renderer headRenderer = arrow.transform.GetChild(0).GetComponent<Renderer>();
                if (headRenderer != null)
                {
                    headRenderer.material.color = currentArrowColor;
                }
                
                arrow.SetActive(true);
            }
        }
    }
    
    private GameObject GetOrCreateArrow()
    {
        // Find inactive arrow or create new one
        foreach (var arrow in arrowObjects)
        {
            if (!arrow.activeInHierarchy)
                return arrow;
        }
        
        // Create new arrow
        GameObject newArrow = Instantiate(arrowPrefab, visualizationParent);
        arrowObjects.Add(newArrow);
        return newArrow;
    }
    
    private GameObject GetOrCreateObstacle()
    {
        foreach (var obstacle in obstacleObjects)
        {
            if (!obstacle.activeInHierarchy)
                return obstacle;
        }
        
        GameObject newObstacle = Instantiate(obstaclePrefab, visualizationParent);
        obstacleObjects.Add(newObstacle);
        return newObstacle;
    }
    
    private GameObject GetOrCreateTarget()
    {
        foreach (var target in targetObjects)
        {
            if (!target.activeInHierarchy)
                return target;
        }
        
        GameObject newTarget = Instantiate(targetPrefab, visualizationParent);
        targetObjects.Add(newTarget);
        return newTarget;
    }
    
    private void HideAllVisuals()
    {
        foreach (var arrow in arrowObjects)
            if (arrow != null) arrow.SetActive(false);
            
        foreach (var obstacle in obstacleObjects)
            if (obstacle != null) obstacle.SetActive(false);
            
        foreach (var target in targetObjects)
            if (target != null) target.SetActive(false);
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
    
    public void ToggleFaction()
    {
        factionToVisualize = factionToVisualize == Faction.Friendly ? Faction.Hostile : Faction.Friendly;
    }
    
    public void ToggleVisualization()
    {
        enableVisualization = !enableVisualization;
        if (!enableVisualization)
            HideAllVisuals();
    }
    
    private void OnDestroy()
    {
        // Clean up created objects
        if (visualizationParent != null)
            DestroyImmediate(visualizationParent.gameObject);
    }
    
    private struct CellVisualizationData
    {
        public float3 worldPosition;
        public float2 direction;
        public float cost;
        public bool isObstacle;
        public float cellSize;
    }
}