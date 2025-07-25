﻿using Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MonoBehaviours
{
    

    public class FlowFieldAuthoring : MonoBehaviour
    {
        [Header("Flow Field Settings")]
        public int2 gridSize = new int2(50, 50);
        public float cellSize = 2f;
        public float3 worldOrigin = float3.zero;
        public float maxCost = 1000f;
        public float maxCostChangePerUpdate = 10f;
        [Header("Unit Avoidance")]
        public float unitAvoidanceRadius = 3f;
        public float unitAvoidanceStrength = 5f;
        [Header("Visualization")]
        public bool enableVisualization = true;
        public Faction visualizedFaction = Faction.Friendly;

        private class Baker : Baker<FlowFieldAuthoring>
        {
            public override void Bake(FlowFieldAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
            
                // Create singleton settings
                AddComponent(entity, new FlowFieldSettings
                {
                    gridSize = authoring.gridSize,
                    cellSize = authoring.cellSize,
                    worldOrigin = authoring.worldOrigin,
                    maxCost = authoring.maxCost,
                    enableVisualization = authoring.enableVisualization,
                    visualizedFaction = authoring.visualizedFaction,
                    maxCostChangePerUpdate = authoring.maxCostChangePerUpdate,
                    unitAvoidanceRadius = authoring.unitAvoidanceRadius,
                    unitAvoidanceStrength = authoring.unitAvoidanceStrength
                });
            }
        }
    }
}