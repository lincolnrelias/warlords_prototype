using Unity.Entities;
using UnityEngine;

namespace Authoring
{
    public class EventRaisingCollidableAuthoring : MonoBehaviour
    {
        private class Baker : Baker<EventRaisingCollidableAuthoring>
        {
            public override void Bake(EventRaisingCollidableAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<EventRaisingCollidable>(entity);
            }
        }
    }

    public struct EventRaisingCollidable : IComponentData, IEnableableComponent
    {
    }
}