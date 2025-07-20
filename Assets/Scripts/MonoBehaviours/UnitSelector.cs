using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class UnitSelector {

	public void SelectUnitAtPosition(Vector2 screenPosition, float threshold) {
		EntityManager manager = World.DefaultGameObjectInjectionWorld.EntityManager;

		ClearAllSelections(manager);

		EntityQuery selectableQuery = CreateSelectableUnitsQuery(manager);
		NativeArray<Entity> entities = selectableQuery.ToEntityArray(Allocator.Temp);
		NativeArray<LocalTransform> transforms = selectableQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

		// Find the first unit within threshold
		for (int i = 0; i < entities.Length; i++) {
			Vector2 unitScreenPosition = Camera.main.WorldToScreenPoint(transforms[i].Position);

			if (Vector2.Distance(unitScreenPosition, screenPosition) <= threshold) {
				SetUnitSelected(manager, entities[i], true);
				break;
			}
		}

		entities.Dispose();
		transforms.Dispose();
	}

	public void SelectUnitsInArea(Rect selectionRect) {
		EntityManager manager = World.DefaultGameObjectInjectionWorld.EntityManager;

		ClearAllSelections(manager);

		EntityQuery selectableQuery = CreateSelectableUnitsQuery(manager);
		NativeArray<Entity> entities = selectableQuery.ToEntityArray(Allocator.Temp);
		NativeArray<LocalTransform> transforms = selectableQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

		for (int i = 0; i < entities.Length; i++) {
			Vector2 unitScreenPosition = Camera.main.WorldToScreenPoint(transforms[i].Position);

			if (selectionRect.Contains(unitScreenPosition)) {
				SetUnitSelected(manager, entities[i], true);
			}
		}

		entities.Dispose();
		transforms.Dispose();
	}

	private void ClearAllSelections(EntityManager manager) {
		EntityQuery allSelectedQuery = new EntityQueryBuilder(Allocator.Temp)
			.WithAll<Selected>()
			.Build(manager);

		NativeArray<Entity> selectedEntities = allSelectedQuery.ToEntityArray(Allocator.Temp);
		NativeArray<Selected> selectedComponents = allSelectedQuery.ToComponentDataArray<Selected>(Allocator.Temp);

		for (int i = 0; i < selectedEntities.Length; i++) {
			manager.SetComponentEnabled<Selected>(selectedEntities[i], false);
			Selected selected = selectedComponents[i];
			selected.onDeselected = true;
			manager.SetComponentData(selectedEntities[i], selected);
		}

		selectedEntities.Dispose();
		selectedComponents.Dispose();
	}

	private EntityQuery CreateSelectableUnitsQuery(EntityManager manager) {
		return new EntityQueryBuilder(Allocator.Temp)
			.WithAll<LocalTransform, Unit>()
			.WithPresent<Selected>()
			.Build(manager);
	}

	private void SetUnitSelected(EntityManager manager, Entity entity, bool selected) {
		manager.SetComponentEnabled<Selected>(entity, selected);
		Selected selectedComponent = manager.GetComponentData<Selected>(entity);
		selectedComponent.onSelected = selected;
		manager.SetComponentData(entity, selectedComponent);
	}

}
