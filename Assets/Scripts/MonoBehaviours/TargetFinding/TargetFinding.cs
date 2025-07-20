using Authoring;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// Component to store chunk information for each unit
public struct ChunkData : IComponentData
{
    public int2 ChunkCoord;
    public int2 PreviousChunkCoord;
}

// Component to store target finding results
public struct TargetData : IComponentData
{
    public float3 TargetPosition;
    public Entity TargetEntity;
    public bool HasTarget;
    public bool isInRange;
}

// Singleton component to configure target finding behavior
public struct TargetFindingConfig : IComponentData
{
    public int
        MaxChunkSearchRadius; // How many chunks to expand search (1 = immediate neighbors, 2 = neighbors + their neighbors, etc.)

    public float MaxSearchRange; // Optional: maximum world distance to search
    public float ChunkSize;
}

// System to update chunk coordinates when units move
[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial struct ChunkUpdateSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TargetFindingConfig>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float chunkSize = GetChunkSize();
        if (chunkSize <= 0) return;

        var job = new UpdateChunkJob
        {
            ChunkSize = chunkSize
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    private float GetChunkSize()
    {
        return SystemAPI.GetSingleton<TargetFindingConfig>().ChunkSize;
    }
}

[BurstCompile]
public partial struct UpdateChunkJob : IJobEntity
{
    public float ChunkSize;

    public void Execute(ref ChunkData chunkData, in LocalTransform transform)
    {
        chunkData.PreviousChunkCoord = chunkData.ChunkCoord;
        chunkData.ChunkCoord = new int2(
            (int)math.floor(transform.Position.x / ChunkSize),
            (int)math.floor(transform.Position.z / ChunkSize)
        );
    }
}

// Combined system that both populates the chunk map AND finds targets
// This avoids the inter-system dependency issue
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ChunkUpdateSystem))]
public partial struct TargetFindingSystem : ISystem
{
    private NativeParallelMultiHashMap<int2, Entity> _chunkMap;
    private ComponentLookup<LocalTransform> _transformLookup;
    private ComponentLookup<Unit> _unitLookup;
    private ComponentLookup<FlagForCleanup> _cleanupLookup;


    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _chunkMap = new NativeParallelMultiHashMap<int2, Entity>(10000, Allocator.Persistent);
        _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        _unitLookup = state.GetComponentLookup<Unit>(true);
        _cleanupLookup = state.GetComponentLookup<FlagForCleanup>(true);
        // Require the config to exist
        state.RequireForUpdate<TargetFindingConfig>();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (_chunkMap.IsCreated)
            _chunkMap.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _transformLookup.Update(ref state);
        _unitLookup.Update(ref state);

        // Get the singleton config
        var config = SystemAPI.GetSingleton<TargetFindingConfig>();

        // Clear the chunk map
        _chunkMap.Clear();

        // Step 1: Populate the chunk map
        var populateJob = new PopulateChunkMapJob
        {
            ChunkMapWriter = _chunkMap.AsParallelWriter()
        };

        var populateHandle = populateJob.ScheduleParallel(state.Dependency);

        // Step 2: Find targets (depends on chunk map population)
        var findTargetsJob = new FindTargetsJob
        {
            ChunkMap = _chunkMap,
            TransformLookup = _transformLookup,
            UnitLookup = _unitLookup,
            Config = config // Pass the singleton config to the job
        };

        state.Dependency = findTargetsJob.ScheduleParallel(populateHandle);
    }
}

[BurstCompile]
public partial struct PopulateChunkMapJob : IJobEntity
{
    public NativeParallelMultiHashMap<int2, Entity>.ParallelWriter ChunkMapWriter;

    public void Execute(Entity entity, in ChunkData chunkData, in Unit unit)
    {
        ChunkMapWriter.Add(chunkData.ChunkCoord, entity);
    }
}

[BurstCompile]
public partial struct FindTargetsJob : IJobEntity
{
    [ReadOnly] public NativeParallelMultiHashMap<int2, Entity> ChunkMap;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    [ReadOnly] public ComponentLookup<Unit> UnitLookup;
    [ReadOnly] public TargetFindingConfig Config; // Singleton config passed to job

    public void Execute(ref TargetData targetData, in ChunkData chunkData, in Unit unit, in LocalTransform transform)
    {
        targetData.HasTarget = false;
        targetData.TargetEntity = Entity.Null;

        // First, check current chunk
        if (FindTargetInChunk(chunkData.ChunkCoord, unit, transform.Position, ref targetData))
        {
            return;
        }

        // If no target found, check expanding rings of neighboring chunks
        CheckExpandingChunks(chunkData.ChunkCoord, unit, transform.Position, ref targetData, Config);
    }

    private bool FindTargetInChunk(int2 chunkCoord, Unit unit, float3 unitPosition, ref TargetData targetData)
    {
        if (!ChunkMap.TryGetFirstValue(chunkCoord, out Entity entity, out var iterator))
            return false;

        float closestDistanceSq = float.MaxValue;
        Entity closestEnemy = Entity.Null;

        do
        {
            if (!UnitLookup.HasComponent(entity) || !TransformLookup.HasComponent(entity))
                continue;

            var otherUnit = UnitLookup[entity];

            // Check if this is a valid target (different faction, matching target faction)
            if (otherUnit.faction != unit.targetFaction)
                continue;

            var otherTransform = TransformLookup[entity];
            float distanceSq = math.distancesq(unitPosition, otherTransform.Position);

            if (distanceSq < closestDistanceSq)
            {
                closestDistanceSq = distanceSq;
                closestEnemy = entity;
            }
        } while (ChunkMap.TryGetNextValue(out entity, ref iterator));

        if (closestEnemy != Entity.Null)
        {
            targetData.HasTarget = true;
            targetData.TargetEntity = closestEnemy;
            targetData.TargetPosition = TransformLookup[closestEnemy].Position;
            return true;
        }

        return false;
    }

    private void CheckExpandingChunks(int2 centerChunk, Unit unit, float3 unitPosition, ref TargetData targetData,
        TargetFindingConfig config)
    {
        float closestDistanceSq = float.MaxValue;
        Entity closestEnemy = Entity.Null;
        float maxSearchDistanceSq = config.MaxSearchRange * config.MaxSearchRange;

        // Search in expanding rings around the center chunk
        for (int radius = 1; radius <= config.MaxChunkSearchRadius; radius++)
        {
            // Check all chunks at this radius
            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    // Skip chunks that aren't on the edge of this radius (already checked in inner rings)
                    if (math.abs(x) != radius && math.abs(z) != radius)
                        continue;

                    int2 chunkToCheck = centerChunk + new int2(x, z);

                    // Check if this chunk has any valid targets
                    if (ChunkMap.TryGetFirstValue(chunkToCheck, out Entity entity, out var iterator))
                    {
                        do
                        {
                            if (!UnitLookup.HasComponent(entity) || !TransformLookup.HasComponent(entity))
                                continue;

                            var otherUnit = UnitLookup[entity];
                            if (otherUnit.faction != unit.targetFaction)
                                continue;

                            var otherTransform = TransformLookup[entity];
                            float distanceSq = math.distancesq(unitPosition, otherTransform.Position);

                            // Check if within max search range (if specified)
                            if (config.MaxSearchRange > 0 && distanceSq > maxSearchDistanceSq)
                                continue;

                            if (distanceSq < closestDistanceSq)
                            {
                                closestDistanceSq = distanceSq;
                                closestEnemy = entity;
                            }
                        } while (ChunkMap.TryGetNextValue(out entity, ref iterator));
                    }
                }
            }

            // If we found a target at this radius, we can stop expanding
            if (closestEnemy != Entity.Null)
            {
                targetData.HasTarget = true;
                targetData.TargetEntity = closestEnemy;
                targetData.TargetPosition = TransformLookup[closestEnemy].Position;
                return;
            }
        }
    }
}