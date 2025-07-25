using System;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class UnitMoverAuthoring : MonoBehaviour
{
    public float moveSpeed;
    public float rotationSpeed;
    public float minDistanceToTarget = 5f;
    public float acceleration = 10f;
    public float deceleration = 15f;

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
                    acceleration = authoring.acceleration,
                    deceleration = authoring.deceleration,
                    currentVelocity = float3.zero
                });
        }
    }
}

public struct UnitMover : IComponentData
{
    public float moveSpeed;
    public float rotationSpeed;
    public float minDistanceToTarget;
    public float acceleration;
    public float deceleration;
    public float3 currentVelocity;
}