using System;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class UnitMoverAuthoring : MonoBehaviour
{
    public float moveSpeed;
    public float rotationSpeed;
    public float minDistanceToTarget = 5f;
    public float avoidanceRadius = 2f;
    public float avoidanceWeight = .5f;
    public float separationStrength = 1f;

    public class Baker : Baker<UnitMoverAuthoring>
    {
        public override void Bake(UnitMoverAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity,
                new UnitMover
                {
                    moveSpeed = authoring.moveSpeed, rotationSpeed = authoring.rotationSpeed,
                    minDistanceToTarget = authoring.minDistanceToTarget,
                    avoidanceRadius = authoring.avoidanceRadius,
                    avoidanceWeight = authoring.avoidanceWeight,
                    separationStrength = authoring.separationStrength
                });
        }
    }
}

public struct UnitMover : IComponentData
{
    public float moveSpeed;
    public float rotationSpeed;
    public float minDistanceToTarget;

    // New steering parameters
    public float avoidanceRadius;
    public float separationStrength;
    public float avoidanceWeight;
}