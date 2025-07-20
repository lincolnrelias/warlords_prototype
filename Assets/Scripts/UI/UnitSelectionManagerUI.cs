using System;
using UnityEngine;

public class UnitSelectionUI : MonoBehaviour {

	[SerializeField] private RectTransform selectionAreaRectTransform;

	private void Start() {
		// Subscribe to the new system's events
		UnitSelectionManager.Instance.OnSelectionAreaStart += UnitSelectionSystem_OnSelectionAreaStart;
		UnitSelectionManager.Instance.OnSelectionAreaEnd += UnitSelectionSystem_OnSelectionAreaEnd;
		selectionAreaRectTransform.gameObject.SetActive(false);
	}

	private void OnDestroy() {
		// Clean up event subscriptions to prevent memory leaks
		if (UnitSelectionManager.Instance != null) {
			UnitSelectionManager.Instance.OnSelectionAreaStart -= UnitSelectionSystem_OnSelectionAreaStart;
			UnitSelectionManager.Instance.OnSelectionAreaEnd -= UnitSelectionSystem_OnSelectionAreaEnd;
		}
	}

	private void UnitSelectionSystem_OnSelectionAreaStart(object sender, EventArgs e) {
		selectionAreaRectTransform.gameObject.SetActive(true);
	}

	private void UnitSelectionSystem_OnSelectionAreaEnd(object sender, EventArgs e) {
		selectionAreaRectTransform.gameObject.SetActive(false);
	}

	private void LateUpdate() {
		if (selectionAreaRectTransform.gameObject.activeSelf) {
			UpdateVisual();
		}
	}

	private void UpdateVisual() {
		// Get selection rectangle from the new system
		Rect selectionAreaRect = CalculateSelectionRect();
		selectionAreaRectTransform.anchoredPosition = new Vector2(selectionAreaRect.x, selectionAreaRect.y);
		selectionAreaRectTransform.sizeDelta = new Vector2(selectionAreaRect.width, selectionAreaRect.height);
	}

	private Rect CalculateSelectionRect() {
		// This logic was moved from the main system, so we need to access it
		// We can either expose it through the UnitSelectionSystem or calculate it here
		Vector2 selectionStartMousePosition = GetSelectionStartPosition();
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

	private Vector2 GetSelectionStartPosition() {
		// This requires the UnitSelectionSystem to expose the selection start position
		return UnitSelectionManager.Instance.GetSelectionStartMousePosition();
	}

}
