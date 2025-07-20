using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// Add this to your existing UnitAuthoring or create a separate component
public class TargetConfigAuthoring : MonoBehaviour {

	[Header("Target Finding Configuration")]
	public int maxChunkSearchRadius = 3; // How many chunks to expand search

	public float maxSearchRange = 0f; // 0 = unlimited range

	public float chunkSize = 20f;

	public class Baker : Baker<TargetConfigAuthoring> {

		public override void Bake(TargetConfigAuthoring authoring) {
			Entity entity = GetEntity(TransformUsageFlags.Dynamic);
			// Add target finding configuration
			AddComponent(entity, new TargetFindingConfig {
				MaxChunkSearchRadius = authoring.maxChunkSearchRadius,
				MaxSearchRange = authoring.maxSearchRange,
				ChunkSize = authoring.chunkSize,
			});
		}

	}

}
