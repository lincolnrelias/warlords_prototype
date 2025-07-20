using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

public class FormationManager {

	[SerializeField] private float unitSpacing = 2f;
	[SerializeField] private float rowSpacing = 2f;

	private FormationGenerator formationGenerator;
	private PositionAssigner positionAssigner;

	public FormationManager() {
		formationGenerator = new FormationGenerator();
		positionAssigner = new PositionAssigner();

		// Set formation parameters
		formationGenerator.SetSpacing(unitSpacing, rowSpacing);
	}

	public void MoveSelectedUnitsToPosition(Vector3 targetPosition) {
		EntityManager manager = World.DefaultGameObjectInjectionWorld.EntityManager;

		EntityQuery selectedUnitsQuery = new EntityQueryBuilder(Allocator.Temp)
			.WithAll<UnitMover, Selected, LocalTransform>()
			.Build(manager);

		NativeArray<Entity> selectedEntities = selectedUnitsQuery.ToEntityArray(Allocator.Temp);
		NativeArray<UnitMover> unitMovers = selectedUnitsQuery.ToComponentDataArray<UnitMover>(Allocator.Temp);
		NativeArray<LocalTransform> currentTransforms =
			selectedUnitsQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

		if (selectedEntities.Length > 0) {
			// Calculate current center of selected units
			Vector3 unitsCenter = CalculateUnitsCenter(currentTransforms);

			// Generate formation positions
			NativeArray<Vector3> formationPositions = formationGenerator.GenerateOrganicFormationWithBias(
				targetPosition, unitsCenter, selectedEntities.Length, Allocator.Temp);

			// Get current positions for assignment optimization
			NativeArray<Vector3> currentPositions = ExtractPositions(currentTransforms, Allocator.Temp);
			NativeArray<Vector3> finalAssignments = new NativeArray<Vector3>(selectedEntities.Length, Allocator.Temp);

			// Assign positions to minimize movement
			positionAssigner.AssignClosestPositions(currentPositions, formationPositions, finalAssignments);

			// Apply assignments to units
			for (int i = 0; i < unitMovers.Length; i++) {
				UnitMover mover = unitMovers[i];
				//TODO remove comment when ready
				//mover.targetPosition = finalAssignments[i];
				unitMovers[i] = mover;
			}

			selectedUnitsQuery.CopyFromComponentDataArray(unitMovers);

			// Cleanup
			formationPositions.Dispose();
			currentPositions.Dispose();
			finalAssignments.Dispose();
		}

		selectedEntities.Dispose();
		unitMovers.Dispose();
		currentTransforms.Dispose();
	}

	private Vector3 CalculateUnitsCenter(NativeArray<LocalTransform> transforms) {
		if (transforms.Length == 0) return Vector3.zero;

		float3 center = Vector3.zero;
		for (int i = 0; i < transforms.Length; i++) {
			center += transforms[i].Position;
		}

		return center / transforms.Length;
	}

	private NativeArray<Vector3> ExtractPositions(NativeArray<LocalTransform> transforms, Allocator allocator) {
		NativeArray<Vector3> positions = new NativeArray<Vector3>(transforms.Length, allocator);
		for (int i = 0; i < transforms.Length; i++) {
			positions[i] = transforms[i].Position;
		}

		return positions;
	}

}
