using System;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class UnitMoverAuthoring : MonoBehaviour
{
    public float moveSpeed;
    public float rotationSpeed;
    public float minDistanceToTarget = 5f;

    public class Baker : Baker<UnitMoverAuthoring>
    {
        public override void Bake(UnitMoverAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity,
                new UnitMover
                {
                    moveSpeed = authoring.moveSpeed, rotationSpeed = authoring.rotationSpeed,
                    minDistanceToTarget = authoring.minDistanceToTarget
                });
        }
    }
}

public struct UnitMover : IComponentData
{
    public float moveSpeed;
    public float rotationSpeed;
    public float minDistanceToTarget;
}