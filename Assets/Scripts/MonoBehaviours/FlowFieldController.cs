namespace MonoBehaviours
{
using UnityEngine;
using UnityEngine.UI;

public class FlowFieldUIController : MonoBehaviour
{
    [Header("UI References")]
    public Toggle visualizationToggle;
    public Button factionToggleButton;
    public Text factionDisplayText;
    public Toggle showCostsToggle;
    public Toggle showObstaclesToggle;
    public Toggle showTargetsToggle;
    
    [Header("Visualizer Reference")]
    public FlowFieldVisualizer visualizer;
    
    private void Start()
    {
        if (visualizer == null)
            visualizer = FindFirstObjectByType<FlowFieldVisualizer>();
        
        SetupUI();
        UpdateUI();
    }
    
    private void SetupUI()
    {
        if (visualizationToggle != null)
        {
            visualizationToggle.onValueChanged.AddListener(OnVisualizationToggle);
        }
        
        if (factionToggleButton != null)
        {
            factionToggleButton.onClick.AddListener(OnFactionToggle);
        }
        
        if (showCostsToggle != null)
        {
            showCostsToggle.onValueChanged.AddListener(OnShowCostsToggle);
        }
        
        if (showObstaclesToggle != null)
        {
            showObstaclesToggle.onValueChanged.AddListener(OnShowObstaclesToggle);
        }
        
        if (showTargetsToggle != null)
        {
            showTargetsToggle.onValueChanged.AddListener(OnShowTargetsToggle);
        }
    }
    
    private void UpdateUI()
    {
        if (visualizer == null) return;
        
        if (visualizationToggle != null)
            visualizationToggle.isOn = visualizer.enableVisualization;
            
        if (factionDisplayText != null)
            factionDisplayText.text = $"Viewing: {visualizer.factionToVisualize}";
            
        if (showCostsToggle != null)
            showCostsToggle.isOn = visualizer.showCosts;
            
        if (showObstaclesToggle != null)
            showObstaclesToggle.isOn = visualizer.showObstacles;
            
        if (showTargetsToggle != null)
            showTargetsToggle.isOn = visualizer.showTargets;
    }
    
    private void OnVisualizationToggle(bool enabled)
    {
        if (visualizer != null)
        {
            visualizer.enableVisualization = enabled;
            visualizer.UpdateVisualizationSettings(enabled, visualizer.factionToVisualize);
        }
    }
    
    private void OnFactionToggle()
    {
        if (visualizer != null)
        {
            visualizer.ToggleFaction();
            UpdateUI();
        }
    }
    
    private void OnShowCostsToggle(bool enabled)
    {
        if (visualizer != null)
            visualizer.showCosts = enabled;
    }
    
    private void OnShowObstaclesToggle(bool enabled)
    {
        if (visualizer != null)
            visualizer.showObstacles = enabled;
    }
    
    private void OnShowTargetsToggle(bool enabled)
    {
        if (visualizer != null)
            visualizer.showTargets = enabled;
    }
    
    private void Update()
    {
        // Allow keyboard shortcuts for quick testing
        if (Input.GetKeyDown(KeyCode.F))
        {
            OnFactionToggle();
        }
        
        if (Input.GetKeyDown(KeyCode.V))
        {
            if (visualizationToggle != null)
                visualizationToggle.isOn = !visualizationToggle.isOn;
        }
    }
}
}