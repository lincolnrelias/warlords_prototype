using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class FormationGenerator {

	private float unitSpacing = 2f;
	private float rowSpacing = 2f;

	public void SetSpacing(float unitSpacing, float rowSpacing) {
		this.unitSpacing = unitSpacing;
		this.rowSpacing = rowSpacing;
	}

	public NativeArray<Vector3> GenerateOrganicFormation(Vector3 centerPosition, int unitCount, Allocator allocator) {
		NativeArray<Vector3> positions = new NativeArray<Vector3>(unitCount, allocator);

		if (unitCount == 0) return positions;

		if (unitCount == 1) {
			positions[0] = centerPosition;
			return positions;
		}

		// Create concentric circles/rings around the center point
		int unitsPlaced = 0;
		int ring = 0;
		float baseRadius = unitSpacing * 0.8f;

		// Place first unit at center
		positions[unitsPlaced] = centerPosition;
		unitsPlaced++;

		while (unitsPlaced < unitCount) {
			ring++;
			float ringRadius = baseRadius * ring;

			// Calculate how many units can fit in this ring (roughly)
			int unitsInRing = math.max(1, (int)(6 * ring)); // Hexagonal-ish packing
			unitsInRing = math.min(unitsInRing, unitCount - unitsPlaced);

			// Add some randomness to make it more organic
			float angleOffset = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;

			for (int i = 0; i < unitsInRing && unitsPlaced < unitCount; i++) {
				float angle = (2f * math.PI * i / unitsInRing) + angleOffset;

				// Add some radius variation for organic feel
				float radiusVariation = UnityEngine.Random.Range(-unitSpacing * 0.3f, unitSpacing * 0.3f);
				float actualRadius = ringRadius + radiusVariation;

				Vector3 offset = new Vector3(
					math.cos(angle) * actualRadius,
					0,
					math.sin(angle) * actualRadius
				);

				positions[unitsPlaced] = centerPosition + offset;
				unitsPlaced++;
			}
		}

		return positions;
	}

	public NativeArray<Vector3> GenerateOrganicFormationWithBias(Vector3 centerPosition, Vector3 unitsCenter,
		int unitCount, Allocator allocator) {
		NativeArray<Vector3> positions = new NativeArray<Vector3>(unitCount, allocator);

		if (unitCount == 0) return positions;

		if (unitCount == 1) {
			positions[0] = centerPosition;
			return positions;
		}

		// Calculate loose direction (optional bias)
		Vector3 direction = (centerPosition - unitsCenter);
		bool hasDirection = direction.sqrMagnitude > 0.01f;
		if (hasDirection) {
			direction.Normalize();
		}

		int unitsPlaced = 0;
		int ring = 0;
		float baseRadius = unitSpacing * 0.9f;

		// Place first unit at center
		positions[unitsPlaced] = centerPosition;
		unitsPlaced++;

		while (unitsPlaced < unitCount) {
			ring++;
			float ringRadius = baseRadius * ring;

			int unitsInRing = math.max(1, (int)(6 * ring));
			unitsInRing = math.min(unitsInRing, unitCount - unitsPlaced);

			// Optional directional bias - units slightly favor the movement direction
			float biasAngle = hasDirection ? math.atan2(direction.z, direction.x) : 0f;
			float angleOffset = UnityEngine.Random.Range(-30f, 30f) * Mathf.Deg2Rad;

			for (int i = 0; i < unitsInRing && unitsPlaced < unitCount; i++) {
				float baseAngle = (2f * math.PI * i / unitsInRing);

				// Apply loose directional bias (only 30% influence)
				float angle = baseAngle + (biasAngle * 0.3f) + angleOffset;

				// Organic radius variation
				float radiusVariation = UnityEngine.Random.Range(-unitSpacing * 0.4f, unitSpacing * 0.2f);
				float actualRadius = ringRadius + radiusVariation;

				Vector3 offset = new Vector3(
					math.cos(angle) * actualRadius,
					0,
					math.sin(angle) * actualRadius
				);

				positions[unitsPlaced] = centerPosition + offset;
				unitsPlaced++;
			}
		}

		return positions;
	}

	public NativeArray<Vector3> GenerateRectangularFormation(Vector3 targetPosition, Vector3 unitsCenter, int unitCount,
		Allocator allocator) {
		NativeArray<Vector3> positions = new NativeArray<Vector3>(unitCount, allocator);

		if (unitCount == 0) return positions;

		// Calculate optimal formation dimensions with 3:5 ratio
		FormationDimensions dimensions = CalculateOptimalFormationDimensions(unitCount);
		int formationWidth = dimensions.width;
		int formationHeight = dimensions.height;

		// Calculate direction from units center to target
		Vector3 direction = (targetPosition - unitsCenter);
		if (direction.sqrMagnitude < 0.01f) {
			direction = Vector3.forward;
		}
		else {
			direction.Normalize();
		}

		// Calculate right vector (perpendicular to direction)
		Vector3 rightVector = Vector3.Cross(Vector3.up, direction).normalized;
		Vector3 forwardVector = direction;

		// Calculate formation center (slightly behind the target position)
		Vector3 formationCenter = targetPosition - forwardVector * (formationHeight * rowSpacing * 0.5f);

		// Calculate starting position (back-left corner of formation)
		Vector3 startPosition = formationCenter
		                        - rightVector * ((formationWidth - 1) * unitSpacing * 0.5f)
		                        - forwardVector * ((formationHeight - 1) * rowSpacing * 0.5f);

		int unitIndex = 0;

		for (int row = 0; row < formationHeight && unitIndex < unitCount; row++) {
			int unitsInThisRow = math.min(formationWidth, unitCount - (row * formationWidth));
			float rowOffset = (formationWidth - unitsInThisRow) * unitSpacing * 0.5f;

			for (int col = 0; col < unitsInThisRow && unitIndex < unitCount; col++) {
				Vector3 localPosition = new Vector3(
					(col * unitSpacing) + rowOffset,
					0,
					row * rowSpacing
				);

				Vector3 worldPosition = startPosition
				                        + rightVector * localPosition.x
				                        + forwardVector * localPosition.z;

				positions[unitIndex] = worldPosition;
				unitIndex++;
			}
		}

		return positions;
	}

	private struct FormationDimensions {

		public int width;
		public int height;

		public FormationDimensions(int w, int h) {
			width = w;
			height = h;
		}

	}

	private FormationDimensions CalculateOptimalFormationDimensions(int unitCount) {
		if (unitCount <= 1) {
			return new FormationDimensions(1, 1);
		}

		// Target ratio: height:width = 3:5
		float targetRatio = 3f / 5f;
		float bestScore = float.MaxValue;
		int bestWidth = 1;
		int bestHeight = unitCount;

		for (int w = 1; w <= unitCount; w++) {
			int h = (int)math.ceil((float)unitCount / w);
			float currentRatio = (float)h / w;
			float ratioError = math.abs(currentRatio - targetRatio);

			int totalCells = w * h;
			int wastedCells = totalCells - unitCount;
			float wasteScore = (float)wastedCells / totalCells;

			float score = ratioError + wasteScore * 0.5f;

			if (score < bestScore) {
				bestScore = score;
				bestWidth = w;
				bestHeight = h;
			}
		}

		return new FormationDimensions(bestWidth, bestHeight);
	}

}
