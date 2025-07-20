using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class PositionAssigner {

	/// <summary>
	/// Assigns target positions to units based on proximity to minimize total movement.
	/// Uses a greedy algorithm for good performance with acceptable visual results.
	/// </summary>
	public void AssignClosestPositions(NativeArray<Vector3> currentPositions, NativeArray<Vector3> targetPositions,
		NativeArray<Vector3> finalAssignments) {
		int unitCount = currentPositions.Length;

		if (unitCount != targetPositions.Length || unitCount != finalAssignments.Length) {
			Debug.LogError("PositionAssigner: Array length mismatch!");
			return;
		}

		// Simple greedy assignment - not perfect but fast and good enough visually
		NativeArray<bool> positionTaken = new NativeArray<bool>(unitCount, Allocator.Temp);

		for (int unitIndex = 0; unitIndex < unitCount; unitIndex++) {
			Vector3 currentPos = currentPositions[unitIndex];
			float closestDistance = float.MaxValue;
			int closestPositionIndex = 0;

			// Find the closest available target position
			for (int posIndex = 0; posIndex < unitCount; posIndex++) {
				if (positionTaken[posIndex]) continue; // Position already assigned

				float distance = math.distancesq(currentPos, targetPositions[posIndex]);
				if (distance < closestDistance) {
					closestDistance = distance;
					closestPositionIndex = posIndex;
				}
			}

			// Assign this position and mark it as taken
			finalAssignments[unitIndex] = targetPositions[closestPositionIndex];
			positionTaken[closestPositionIndex] = true;
		}

		positionTaken.Dispose();
	}

	/// <summary>
	/// Alternative assignment method that considers unit priorities or roles.
	/// For example, you might want certain unit types to get preferred positions.
	/// </summary>
	public void AssignPositionsWithPriority(NativeArray<Vector3> currentPositions, NativeArray<Vector3> targetPositions,
		NativeArray<int> unitPriorities, NativeArray<Vector3> finalAssignments) {
		int unitCount = currentPositions.Length;
		NativeArray<bool> positionTaken = new NativeArray<bool>(unitCount, Allocator.Temp);
		NativeArray<int> assignmentOrder = new NativeArray<int>(unitCount, Allocator.Temp);

		// Create assignment order based on priority (higher priority first)
		for (int i = 0; i < unitCount; i++) {
			assignmentOrder[i] = i;
		}

		// Simple bubble sort by priority (could be optimized for larger armies)
		for (int i = 0; i < unitCount - 1; i++) {
			for (int j = 0; j < unitCount - i - 1; j++) {
				if (unitPriorities[assignmentOrder[j]] < unitPriorities[assignmentOrder[j + 1]]) {
					int temp = assignmentOrder[j];
					assignmentOrder[j] = assignmentOrder[j + 1];
					assignmentOrder[j + 1] = temp;
				}
			}
		}

		// Assign positions in priority order
		for (int orderIndex = 0; orderIndex < unitCount; orderIndex++) {
			int unitIndex = assignmentOrder[orderIndex];
			Vector3 currentPos = currentPositions[unitIndex];
			float closestDistance = float.MaxValue;
			int closestPositionIndex = 0;

			for (int posIndex = 0; posIndex < unitCount; posIndex++) {
				if (positionTaken[posIndex]) continue;

				float distance = math.distancesq(currentPos, targetPositions[posIndex]);
				if (distance < closestDistance) {
					closestDistance = distance;
					closestPositionIndex = posIndex;
				}
			}

			finalAssignments[unitIndex] = targetPositions[closestPositionIndex];
			positionTaken[closestPositionIndex] = true;
		}

		positionTaken.Dispose();
		assignmentOrder.Dispose();
	}

}
