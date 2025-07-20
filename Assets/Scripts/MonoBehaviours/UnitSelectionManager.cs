using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class UnitSelectionManager : MonoBehaviour {

	public static UnitSelectionManager Instance { get; private set; }

	[SerializeField] private float clickThreshold = 5f;

	private Vector2 selectionStartMousePosition;
	private UnitSelector unitSelector;
	private FormationManager formationManager;

	public event EventHandler OnSelectionAreaStart;
	public event EventHandler OnSelectionAreaEnd;

	private void Awake() {
		Instance = this;
		unitSelector = new UnitSelector();
		formationManager = new FormationManager();
	}

	private void Update() {
		HandleSelectionInput();
		HandleMovementInput();
	}

	private void HandleSelectionInput() {
		if (Input.GetMouseButtonDown(0)) {
			selectionStartMousePosition = Input.mousePosition;
			OnSelectionAreaStart?.Invoke(this, EventArgs.Empty);
		}

		if (Input.GetMouseButtonUp(0)) {
			OnSelectionAreaEnd?.Invoke(this, EventArgs.Empty);

			Vector2 currentMousePosition = Input.mousePosition;
			float dragDistance = Vector2.Distance(selectionStartMousePosition, currentMousePosition);
			bool isClick = dragDistance <= clickThreshold;

			if (isClick) {
				unitSelector.SelectUnitAtPosition(selectionStartMousePosition, clickThreshold);
			}
			else {
				Rect selectionRect = CalculateSelectionRect();
				unitSelector.SelectUnitsInArea(selectionRect);
			}
		}
	}

	private void HandleMovementInput() {
		if (Input.GetMouseButtonDown(1)) {
			Vector3 mouseWorldPosition = MouseWorldPosition.Instance.GetPosition();
			formationManager.MoveSelectedUnitsToPosition(mouseWorldPosition);
		}
	}

	private Rect CalculateSelectionRect() {
		Vector2 selectionEndMousePosition = Input.mousePosition;
		Vector2 lowerLeftCorner = new Vector2(
			Mathf.Min(selectionStartMousePosition.x, selectionEndMousePosition.x),
			Mathf.Min(selectionStartMousePosition.y, selectionEndMousePosition.y));
		Vector2 upperRightCorner = new Vector2(
			Mathf.Max(selectionStartMousePosition.x, selectionEndMousePosition.x),
			Mathf.Max(selectionStartMousePosition.y, selectionEndMousePosition.y));

		return new Rect(lowerLeftCorner.x, lowerLeftCorner.y,
			upperRightCorner.x - lowerLeftCorner.x,
			upperRightCorner.y - lowerLeftCorner.y);
	}

	public Vector2 GetSelectionStartMousePosition() {
		return selectionStartMousePosition;
	}

}
