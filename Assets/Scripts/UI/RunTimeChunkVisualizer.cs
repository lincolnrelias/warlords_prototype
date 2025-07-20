using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class RuntimeChunkVisualizer : MonoBehaviour {

	[Header("Visualization Settings")] public bool showChunksInPlayMode = false;
	public Color chunkLineColor = Color.white;
	public Color occupiedChunkColor = Color.yellow;
	public bool showOccupiedChunksOnly = false;
	public bool showChunkCoordinates = false;

	[Header("Grid Settings")] public int gridWidth = 40;
	public int gridHeight = 40;
	public bool centerOnOrigin = true;

	private Camera _camera;
	private World _world;
	private EntityManager _entityManager;
	private EntityQuery _unitQuery;

	void Start() {
		_camera = Camera.main;
		_world = World.DefaultGameObjectInjectionWorld;
		if (_world != null) {
			_entityManager = _world.EntityManager;
			_unitQuery = _entityManager.CreateEntityQuery(typeof(ChunkData), typeof(Unit));
		}
	}

	void OnGUI() {
		if (!showChunksInPlayMode || !Application.isPlaying) return;

		float chunkSize = GetChunkSize();
		if (chunkSize <= 0 || _camera == null) return;

		// Get all occupied chunks if we're only showing those
		var occupiedChunks = new HashSet<int2>();
		if (showOccupiedChunksOnly && !_unitQuery.IsEmpty) {
			var chunkDataArray = _unitQuery.ToComponentDataArray<ChunkData>(Allocator.Temp);
			foreach (var chunkData in chunkDataArray) {
				occupiedChunks.Add(chunkData.ChunkCoord);
			}

			chunkDataArray.Dispose();
		}

		Vector3 startPos = centerOnOrigin
			? new Vector3(-gridWidth * chunkSize * 0.5f, 0, -gridHeight * chunkSize * 0.5f)
			: Vector3.zero;

		// Draw grid lines
		for (int x = 0; x <= gridWidth; x++) {
			Vector3 lineStart = startPos + new Vector3(x * chunkSize, 0, 0);
			Vector3 lineEnd = lineStart + new Vector3(0, 0, gridHeight * chunkSize);

			if (IsLineVisible(lineStart, lineEnd)) {
				DrawWorldLine(lineStart, lineEnd, chunkLineColor);
			}
		}

		for (int z = 0; z <= gridHeight; z++) {
			Vector3 lineStart = startPos + new Vector3(0, 0, z * chunkSize);
			Vector3 lineEnd = lineStart + new Vector3(gridWidth * chunkSize, 0, 0);

			if (IsLineVisible(lineStart, lineEnd)) {
				DrawWorldLine(lineStart, lineEnd, chunkLineColor);
			}
		}

		// Highlight occupied chunks
		if (showOccupiedChunksOnly && occupiedChunks.Count > 0) {
			foreach (var chunkCoord in occupiedChunks) {
				DrawChunkHighlight(chunkCoord, chunkSize, occupiedChunkColor);

				if (showChunkCoordinates) {
					DrawChunkCoordinate(chunkCoord, chunkSize);
				}
			}
		}
		else if (showChunkCoordinates) {
			// Draw coordinates for visible chunks
			DrawVisibleChunkCoordinates(startPos, chunkSize);
		}
	}

	private void DrawWorldLine(Vector3 start, Vector3 end, Color color) {
		Vector3 screenStart = _camera.WorldToScreenPoint(start);
		Vector3 screenEnd = _camera.WorldToScreenPoint(end);

		// Flip Y coordinate for GUI
		screenStart.y = Screen.height - screenStart.y;
		screenEnd.y = Screen.height - screenEnd.y;

		// Only draw if both points are in front of camera
		if (screenStart.z > 0 && screenEnd.z > 0) {
			DrawScreenLine(screenStart, screenEnd, color);
		}
	}

	private void DrawScreenLine(Vector3 start, Vector3 end, Color color) {
		GUI.color = color;
		Vector3 direction = (end - start).normalized;
		float distance = Vector3.Distance(start, end);

		float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

		GUIUtility.RotateAroundPivot(angle, start);
		GUI.DrawTexture(new Rect(start.x, start.y - 0.5f, distance, 1f), Texture2D.whiteTexture);
		GUIUtility.RotateAroundPivot(-angle, start);

		GUI.color = Color.white;
	}

	private void DrawChunkHighlight(int2 chunkCoord, float chunkSize, Color color) {
		Vector3 chunkWorldPos = new Vector3(
			chunkCoord.x * chunkSize,
			0,
			chunkCoord.y * chunkSize
		);

		Vector3[] corners = new Vector3[4] {
			chunkWorldPos,
			chunkWorldPos + new Vector3(chunkSize, 0, 0),
			chunkWorldPos + new Vector3(chunkSize, 0, chunkSize),
			chunkWorldPos + new Vector3(0, 0, chunkSize)
		};

		// Draw chunk outline
		for (int i = 0; i < 4; i++) {
			DrawWorldLine(corners[i], corners[(i + 1) % 4], color);
		}
	}

	private void DrawChunkCoordinate(int2 chunkCoord, float chunkSize) {
		Vector3 chunkCenter = new Vector3(
			chunkCoord.x * chunkSize + chunkSize * 0.5f,
			0.1f,
			chunkCoord.y * chunkSize + chunkSize * 0.5f
		);

		Vector3 screenPos = _camera.WorldToScreenPoint(chunkCenter);
		if (screenPos.z > 0) {
			screenPos.y = Screen.height - screenPos.y;

			GUI.color = Color.yellow;
			GUI.Label(new Rect(screenPos.x - 30, screenPos.y - 10, 60, 20),
				$"{chunkCoord.x},{chunkCoord.y}",
				new GUIStyle()
					{ alignment = TextAnchor.MiddleCenter, normal = new GUIStyleState() { textColor = Color.yellow } });
			GUI.color = Color.white;
		}
	}

	private void DrawVisibleChunkCoordinates(Vector3 startPos, float chunkSize) {
		for (int x = 0; x < gridWidth; x++) {
			for (int z = 0; z < gridHeight; z++) {
				Vector3 chunkCenter = startPos + new Vector3(
					(x + 0.5f) * chunkSize,
					0.1f,
					(z + 0.5f) * chunkSize
				);

				if (IsPointVisible(chunkCenter)) {
					int chunkX = centerOnOrigin ? x - gridWidth / 2 : x;
					int chunkZ = centerOnOrigin ? z - gridHeight / 2 : z;

					Vector3 screenPos = _camera.WorldToScreenPoint(chunkCenter);
					screenPos.y = Screen.height - screenPos.y;

					GUI.color = Color.white;
					GUI.Label(new Rect(screenPos.x - 30, screenPos.y - 10, 60, 20),
						$"{chunkX},{chunkZ}",
						new GUIStyle() {
							alignment = TextAnchor.MiddleCenter,
							normal = new GUIStyleState() { textColor = Color.white }
						});
					GUI.color = Color.white;
				}
			}
		}
	}

	private bool IsLineVisible(Vector3 start, Vector3 end) {
		Vector3 screenStart = _camera.WorldToScreenPoint(start);
		Vector3 screenEnd = _camera.WorldToScreenPoint(end);

		return (screenStart.z > 0 || screenEnd.z > 0) &&
			IsPointOnScreen(screenStart) || IsPointOnScreen(screenEnd);
	}

	private bool IsPointVisible(Vector3 worldPoint) {
		Vector3 screenPoint = _camera.WorldToScreenPoint(worldPoint);
		return screenPoint.z > 0 && IsPointOnScreen(screenPoint);
	}

	private bool IsPointOnScreen(Vector3 screenPoint) {
		return screenPoint.x >= 0 && screenPoint.x <= Screen.width &&
		       screenPoint.y >= 0 && screenPoint.y <= Screen.height;
	}

	private float GetChunkSize() {
		return 15f; // Should match your system's chunk size
	}

	// Helper method to convert world position to chunk coordinate
	public int2 WorldToChunk(Vector3 worldPos) {
		float chunkSize = GetChunkSize();
		return new int2(
			(int)math.floor(worldPos.x / chunkSize),
			(int)math.floor(worldPos.z / chunkSize)
		);
	}

}
