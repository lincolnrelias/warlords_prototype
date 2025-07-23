using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Data
{
    // Component to store flow field data for a faction
    public struct FlowFieldData : IComponentData
    {
        public Faction faction;
        public int2 gridSize;
        public float cellSize;
        public float3 gridOrigin;
        public BlobAssetReference<FlowFieldBlob> flowField;
    }

    // Blob asset to store the actual flow field directions and costs
    public struct FlowFieldBlob
    {
        public BlobArray<float2> directions; // Direction vectors for each cell
        public BlobArray<float> costs; // Cost to reach nearest target from each cell
        public BlobArray<bool> obstacles; // Whether each cell contains an obstacle
    }
}

// Singleton component to manage flow field settings
public struct FlowFieldSettings : IComponentData
{
    public int2 gridSize;
    public float cellSize;
    public float3 worldOrigin;
    public float maxCost;
    public bool enableVisualization;
    public float neighborCostMultiplier;
    public Faction visualizedFaction;
    public float maxCostChangePerUpdate; 
}

// Helper struct for flow field calculations
public struct FlowFieldCell
{
    public float2 direction;
    public float cost;
    public bool isObstacle;
    public bool isTarget;
    
    public FlowFieldCell(float2 dir, float costValue, bool obstacle, bool target)
    {
        direction = dir;
        cost = costValue;
        isObstacle = obstacle;
        isTarget = target;
    }
}