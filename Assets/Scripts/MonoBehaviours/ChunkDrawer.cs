using UnityEngine;

[ExecuteInEditMode]
public class ChunkDrawer : MonoBehaviour {

	[Header("Grid Settings")] public int gridWidth = 20;
	public int gridHeight = 20;
	public Color gridColor = Color.white;
	public bool centerOnOrigin = true;

	[Header("Visual Options")] public bool showCoordinates;
	public Color coordinateColor = Color.yellow;

	[Header("Runtime Settings")] [SerializeField]
	private float editorChunkSize = 10f;

	private void OnValidate() {
		// Force scene view to repaint when values change in inspector
#if UNITY_EDITOR
		UnityEditor.SceneView.RepaintAll();
#endif
	}

	private void OnDrawGizmos() {
		float chunkSize = GetChunkSize();
		if (chunkSize <= 0) return;

		// Calculate grid bounds
		Vector3 startPos = centerOnOrigin
			? new Vector3(-gridWidth * chunkSize * 0.5f, 0, -gridHeight * chunkSize * 0.5f)
			: Vector3.zero;

		Gizmos.color = gridColor;

		// Draw vertical lines
		for (int x = 0; x <= gridWidth; x++) {
			Vector3 lineStart = startPos + new Vector3(x * chunkSize, 0, 0);
			Vector3 lineEnd = lineStart + new Vector3(0, 0, gridHeight * chunkSize);
			Gizmos.DrawLine(lineStart, lineEnd);
		}

		// Draw horizontal lines
		for (int z = 0; z <= gridHeight; z++) {
			Vector3 lineStart = startPos + new Vector3(0, 0, z * chunkSize);
			Vector3 lineEnd = lineStart + new Vector3(gridWidth * chunkSize, 0, 0);
			Gizmos.DrawLine(lineStart, lineEnd);
		}

		// Optionally draw chunk coordinates
		if (showCoordinates) {
			DrawChunkCoordinates(startPos, chunkSize);
		}
	}

	private float GetChunkSize() {
		// In play mode, use GameAssets if available
		if (Application.isPlaying && GameAssets.Instance != null) {
			return GameAssets.Instance.chunkSize;
		}

		// In edit mode, try to find GameAssets in scene, otherwise use editor value
		GameAssets gameAssets = FindFirstObjectByType<GameAssets>();
		if (gameAssets != null) {
			editorChunkSize = gameAssets.chunkSize; // Sync with GameAssets
			return gameAssets.chunkSize;
		}

		return editorChunkSize;
	}

	private void DrawChunkCoordinates(Vector3 startPos, float chunkSize) {
#if UNITY_EDITOR
		UnityEditor.Handles.color = coordinateColor;

		for (int x = 0; x < gridWidth; x++) {
			for (int z = 0; z < gridHeight; z++) {
				Vector3 chunkCenter = startPos + new Vector3(
					(x + 0.5f) * chunkSize,
					0.1f,
					(z + 0.5f) * chunkSize
				);

				// Calculate chunk coordinates (can be negative if centered on origin)
				int chunkX = centerOnOrigin ? x - gridWidth / 2 : x;
				int chunkZ = centerOnOrigin ? z - gridHeight / 2 : z;

				UnityEditor.Handles.Label(chunkCenter, $"{chunkX},{chunkZ}");
			}
		}
#endif
	}

	// Helper method to get chunk coordinate from world position
	public Vector2Int WorldToChunk(Vector3 worldPos) {
		float chunkSize = GetChunkSize();
		if (chunkSize <= 0) return Vector2Int.zero;

		return new Vector2Int(
			Mathf.FloorToInt(worldPos.x / chunkSize),
			Mathf.FloorToInt(worldPos.z / chunkSize)
		);
	}

	// Helper method to get world position from chunk coordinate
	public Vector3 ChunkToWorld(Vector2Int chunkCoord) {
		float chunkSize = GetChunkSize();
		if (chunkSize <= 0) return Vector3.zero;

		return new Vector3(
			chunkCoord.x * chunkSize + chunkSize * 0.5f,
			0,
			chunkCoord.y * chunkSize + chunkSize * 0.5f
		);
	}

}
