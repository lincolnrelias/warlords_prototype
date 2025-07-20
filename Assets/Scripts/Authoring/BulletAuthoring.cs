using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using BoxCollider = UnityEngine.BoxCollider;

namespace Authoring
{
    public class BulletAuthoring : MonoBehaviour
    {
        public int damage;

        private class Baker : Baker<BulletAuthoring>
        {
            public override void Bake(BulletAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Bullet
                {
                    damage = authoring.damage,
                    maxLifetime = 5,
                    timeOfCreation = Time.time
                });
            }
        }
    }

    public struct Bullet : IComponentData, IEnableableComponent
    {
        public int damage;
        public float maxLifetime;
        public double timeOfCreation;
    }
}