using UnityEngine;

public class FreeRoamCamera : MonoBehaviour {

	[Header("Movement Settings")] public float moveSpeed = 5f;
	public float fastMoveSpeed = 10f;
	public float mouseSensitivity = 2f;

	[Header("Smoothing")] public float smoothTime = 0.1f;

	private Vector3 velocity;
	private Vector3 smoothVelocity;
	private float xRotation = 0f;

	void Start() {
		// Lock cursor to center of screen
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	void Update() {
		HandleMouseLook();
		HandleMovement();
		HandleInput();
	}

	void HandleMouseLook() {
		// Get mouse input
		float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
		float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

		// Rotate camera up/down
		xRotation -= mouseY;
		xRotation = Mathf.Clamp(xRotation, -90f, 90f);
		transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

		// Rotate camera left/right
		transform.parent.Rotate(Vector3.up * mouseX);
	}

	void HandleMovement() {
		// Get input
		float x = Input.GetAxis("Horizontal");
		float z = Input.GetAxis("Vertical");
		float y = 0f;

		// Vertical movement
		if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space))
			y = 1f;
		if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl))
			y = -1f;

		// Calculate movement direction
		Vector3 direction = (transform.right * x + transform.up * y + transform.forward * z).normalized;

		// Determine speed
		float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? fastMoveSpeed : moveSpeed;

		// Apply movement with smoothing
		Vector3 targetVelocity = direction * currentSpeed;
		velocity = Vector3.SmoothDamp(velocity, targetVelocity, ref smoothVelocity, smoothTime);

		// Move the camera
		transform.Translate(velocity * Time.deltaTime, Space.World);
	}

	void HandleInput() {
		// Toggle cursor lock
		if (Input.GetKeyDown(KeyCode.Escape)) {
			if (Cursor.lockState == CursorLockMode.Locked) {
				Cursor.lockState = CursorLockMode.None;
				Cursor.visible = true;
			}
			else {
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
			}
		}
	}

}
