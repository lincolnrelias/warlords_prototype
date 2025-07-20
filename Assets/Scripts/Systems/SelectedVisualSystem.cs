using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

partial struct SelectedVisualSystem : ISystem {

	[BurstCompile]
	public void OnUpdate(ref SystemState state) {
		foreach (var selected in SystemAPI.Query<RefRW<Selected>>().WithPresent<Selected>()) {
			if (selected.ValueRO.onDeselected) {
				var localTransform = SystemAPI.GetComponentRW<LocalTransform>(selected.ValueRO.selectedEffectEntity);
				localTransform.ValueRW.Scale = 0f;
				selected.ValueRW.onDeselected = false;
				break;
			}

			if (selected.ValueRO.onSelected) {
				var localTransform = SystemAPI.GetComponentRW<LocalTransform>(selected.ValueRO.selectedEffectEntity);
				localTransform.ValueRW.Scale = selected.ValueRO.showScale;
				selected.ValueRW.onSelected = false;
			}
		}
	}

}
