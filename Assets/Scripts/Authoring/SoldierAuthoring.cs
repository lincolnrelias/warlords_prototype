using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Authoring
{
    public class SoldierAuthoring : MonoBehaviour
    {
        public float accuracy = .5f;
        public int shootingDamage = 10;
        public int shootingRange = 15;
        [Tooltip("Attacks per second")]
        public float atkSpeed = 2.0f;
        public float shootingForce = 10.0f;
        public Transform shotPoint;
        public GameObject bulletPrefab;

        private class SoldierAuthoringBaker : Baker<SoldierAuthoring>
        {
            public override void Bake(SoldierAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                // Get the bullet prefab as an entity
                Entity bulletPrefabEntity = GetEntity(authoring.bulletPrefab, TransformUsageFlags.Dynamic);
                AddComponent(entity, new Soldier()
                {
                    accuracy = authoring.accuracy,
                    shootingDamage = authoring.shootingDamage,
                    shootingRange = authoring.shootingRange,
                    atkSpeed = authoring.atkSpeed,
                    shootingForce = authoring.shootingForce,
                    bulletSpawnOffset = authoring.shotPoint.localPosition, // Store as local offset
                    bulletSpawnRotation = authoring.bulletPrefab.transform.rotation,
                    bulletPrefab = bulletPrefabEntity
                });
                AddComponent(entity, new AttackTimer()
                {
                    lastAttackTime = 0f,
                    attackCooldown = 1f / authoring.atkSpeed // convert attacks per second to seconds per attack
                });
            }
        }
    }

    public struct Soldier : IComponentData
    {
        public float accuracy;
        public int shootingDamage;
        public int shootingRange;
        public float shootingForce;
        public float atkSpeed;
        public float3 bulletSpawnOffset; // Changed from bulletSpawnPosition
        public quaternion bulletSpawnRotation;
        public Entity bulletPrefab; // Add this to store the prefab entity
    }
}