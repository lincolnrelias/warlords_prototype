using UnityEngine;

public class GameAssets : MonoBehaviour {

	public static GameAssets Instance { get; private set; }
	public float chunkSize = 10f;

	private void Awake() {
		Instance = this;
	}

}
