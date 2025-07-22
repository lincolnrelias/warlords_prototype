using Authoring;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Stateful;
using UnityEngine;
using BoxCollider = UnityEngine.BoxCollider;

public class UnitAuthoring : MonoBehaviour
{
    [SerializeField] public Faction faction;
    [SerializeField] public Faction targetFaction;
    public bool isDebuggable = false;
    public float maxHealth = 100f;

    public class Baker : Baker<UnitAuthoring>
    {
        private static int _nextUnitID = 0; // Static counter shared across all baking

        public override void Bake(UnitAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            // Generate sequential unit ID and assign frame batch
            int unitID = _nextUnitID++;
            int frameBatch = unitID % 15;

            // Original components
            AddComponent(entity, new Unit()
            {
                faction = authoring.faction,
                targetFaction = authoring.targetFaction,
                isDebuggable = authoring.isDebuggable,
                unitID = unitID // Add ID to unit component
            });

            // Add targeting components
            AddComponent(entity, new ChunkData
            {
                ChunkCoord = int2.zero,
                PreviousChunkCoord = int2.zero
            });

            AddComponent(entity, new TargetData
            {
                TargetPosition = float3.zero,
                TargetEntity = Entity.Null,
                HasTarget = false
            });

            // Add scheduling component
            AddComponent(entity, new TargetUpdateSchedule
            {
                FrameBatch = frameBatch,
                LastUpdateFrame = -1
            });

            AddComponent(entity, new Health()
            {
                maxHealth = authoring.maxHealth,
                currentHealth = authoring.maxHealth,
                isDead = false
            });

            AddComponent(entity, new EventRaisingCollidable());
        }
    }
}

public struct Unit : IComponentData, IEnableableComponent
{
    public Faction faction;
    public Faction targetFaction;
    public int unitID; // Sequential ID for this unit
    public bool isDebuggable;
}

public struct TargetUpdateSchedule : IComponentData
{
    public int FrameBatch; // 0-14, which frame in the cycle this unit updates
    public int LastUpdateFrame; // Track when it was last updated (for debugging)
}

public struct AttackTimer : IComponentData
{
    public float lastAttackTime;
    public float attackCooldown;
}