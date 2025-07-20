using Unity.Entities;
using UnityEngine;

public class SelectedAuthoring : MonoBehaviour {

	public GameObject selectedEffect;
	public float selectedScale = 2;

	public class Baker : Baker<SelectedAuthoring> {

		public override void Bake(SelectedAuthoring authoring) {
			Entity entity = GetEntity(TransformUsageFlags.Dynamic);
			AddComponent(entity, new Selected() {
				selectedEffectEntity = GetEntity(authoring.selectedEffect, TransformUsageFlags.Dynamic),
				showScale = authoring.selectedScale
			});
			SetComponentEnabled<Selected>(entity, false);
		}

	}

}

public struct Selected : IComponentData, IEnableableComponent {

	public Entity selectedEffectEntity;
	public float showScale;
	public bool onSelected;
	public bool onDeselected;

}
