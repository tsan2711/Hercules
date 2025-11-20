using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Manages the exit confirmation panel that appears when pressing ESC key
/// </summary>
public class ExitPanelManager : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private GameObject exitPanel;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    
    private const string menuSceneName = "MainMenu";
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    private bool isPanelActive = false;
    
    private void Awake()
    {
        // Hide panel initially
        if (exitPanel != null)
        {
            exitPanel.SetActive(false);
        }
    }
    
    private void Start()
    {
        // Setup yes button onClick event
        if (yesButton != null)
        {
            yesButton.onClick.AddListener(OnYesButtonClicked);
        }
        
        // Setup no button onClick event
        if (noButton != null)
        {
            noButton.onClick.AddListener(OnNoButtonClicked);
        }
    }
    
    private void Update()
    {
        // Check for ESC key press on keyboard
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ShowExitPanel();
        }
    }
    
    /// <summary>
    /// Toggle the exit panel visibility
    /// </summary>
    public void ToggleExitPanel()
    {
        if (exitPanel == null)
        {
            Debug.LogError("[ExitPanelManager] exitPanel is null! Please assign in Inspector.");
            return;
        }
        
        isPanelActive = !isPanelActive;
        exitPanel.SetActive(isPanelActive);
        
        if (showDebugLogs)
        {
            Debug.Log($"[ExitPanelManager] Exit panel {(isPanelActive ? "shown" : "hidden")}");
        }
    }
    
    /// <summary>
    /// Show the exit panel
    /// </summary>
    public void ShowExitPanel()
    {
        if (exitPanel != null)
        {
            exitPanel.SetActive(true);
            isPanelActive = true;
        }
    }
    
    /// <summary>
    /// Hide the exit panel
    /// </summary>
    public void HideExitPanel()
    {
        if (exitPanel != null)
        {
            exitPanel.SetActive(false);
            isPanelActive = false;
        }
    }
    
    /// <summary>
    /// Called when Yes button is clicked - loads menu scene
    /// </summary>
    public void OnYesButtonClicked()
    {
        if (showDebugLogs)
        {
            Debug.Log($"[ExitPanelManager] Loading menu scene: {menuSceneName}");
        }
        
        try
        {
            SceneManager.LoadScene(menuSceneName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ExitPanelManager] Cannot load scene '{menuSceneName}': {e.Message}");
            Debug.LogError("Please check the scene name in Inspector!");
        }
    }
    
    /// <summary>
    /// Called when No button is clicked - hides the panel
    /// </summary>
    public void OnNoButtonClicked()
    {
        HideExitPanel();
        
        if (showDebugLogs)
        {
            Debug.Log("[ExitPanelManager] Exit cancelled");
        }
    }
    
    private void OnDestroy()
    {
        // Remove listeners to prevent memory leaks
        if (yesButton != null)
        {
            yesButton.onClick.RemoveListener(OnYesButtonClicked);
        }
        
        if (noButton != null)
        {
            noButton.onClick.RemoveListener(OnNoButtonClicked);
        }
    }
}

