using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// UI that displays the winner notification and automatically returns to menu after 5 seconds
/// </summary>
public class GameWinUI : MonoBehaviour
{
    public static GameWinUI Instance { get; private set; }
    
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI winnerText; // Text displaying the winner
    
    [Header("Animation Settings")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float scaleInDuration = 0.6f;
    [SerializeField] private float bounceScale = 1.1f;
    [SerializeField] private float bounceDuration = 0.3f;
    
    [Header("Scene Settings")]
    [SerializeField] private string menuSceneName = "Menu"; // Menu scene name
    [SerializeField] private float delayBeforeSceneChange = 5f; // Delay 5 seconds before changing scene
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    private Sequence animationSequence;
    private bool isShowing = false;
    
    /// <summary>
    /// Check if the win UI is currently showing
    /// </summary>
    public bool IsShowing => isShowing;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        
        // Hide text initially
        if (winnerText != null)
        {
            winnerText.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// Display the winner notification UI
    /// </summary>
    /// <param name="winner">true = White , false = Black win, null = Draw</param>
    /// <param name="reason">End reason (Checkmate, Stalemate, etc.)</param>
    public void ShowWinUI(bool? winner, string reason = "Checkmate")
    {
        if (isShowing)
        {
            return; // Already showing, skip
        }
        
        if (winnerText == null)
        {
            Debug.LogError("[GameWinUI] winnerText is null! Please assign in Inspector.");
            return;
        }
        
        isShowing = true;
        
        if (showDebugLogs)
        {
            string winnerName = winner.HasValue 
                ? (winner.Value ? "White" : "Black") 
                : "Draw";
            Debug.Log($"[GameWinUI] Showing win UI: {winnerName}, Reason: {reason}");
        }
        
        // Set text
        if (winner.HasValue)
        {
            winnerText.text = winner.Value ? "WHITE WIN!" : "BLACK WIN!";
        }
        else
        {
            winnerText.text = "DRAW!";
        }
        
        // Activate text
        winnerText.gameObject.SetActive(true);
        
        // Kill old sequence if exists
        if (animationSequence != null && animationSequence.IsActive())
        {
            animationSequence.Kill();
        }
        
        // Get RectTransform of text (TextMeshProUGUI already has it)
        RectTransform textRectTransform = winnerText.rectTransform;
        
        // Get CanvasGroup of text (or create new if doesn't exist)
        CanvasGroup textCanvasGroup = winnerText.GetComponent<CanvasGroup>();
        if (textCanvasGroup == null)
        {
            textCanvasGroup = winnerText.gameObject.AddComponent<CanvasGroup>();
        }
        
        // Reset to initial state
        textCanvasGroup.alpha = 0f;
        textRectTransform.localScale = Vector3.zero;
        
        // Create animation sequence
        animationSequence = DOTween.Sequence();
        
        // Fade in
        animationSequence.Append(textCanvasGroup.DOFade(1f, fadeInDuration).SetEase(Ease.OutQuad));
        
        // Scale up with bounce
        animationSequence.Join(textRectTransform.DOScale(Vector3.one * bounceScale, scaleInDuration)
            .SetEase(Ease.OutBack));
        
        // Bounce back to original scale
        animationSequence.Append(textRectTransform.DOScale(Vector3.one, bounceDuration)
            .SetEase(Ease.OutBounce));
        
        // After animation completes, start countdown to change scene
        animationSequence.OnComplete(() => {
            StartCoroutine(CountdownAndLoadScene());
        });
    }
    
    /// <summary>
    /// Coroutine that counts down and changes scene
    /// </summary>
    private System.Collections.IEnumerator CountdownAndLoadScene()
    {
        // Wait for delay time
        yield return new WaitForSeconds(delayBeforeSceneChange);
        
        if (showDebugLogs)
        {
            Debug.Log($"[GameWinUI] Changing to menu scene: {menuSceneName}");
        }
        
        // Fade out before changing scene
        if (animationSequence != null && animationSequence.IsActive())
        {
            animationSequence.Kill();
        }
        
        CanvasGroup textCanvasGroup = winnerText.GetComponent<CanvasGroup>();
        if (textCanvasGroup != null)
        {
            animationSequence = DOTween.Sequence();
            animationSequence.Append(textCanvasGroup.DOFade(0f, 0.3f).SetEase(Ease.InQuad));
            animationSequence.OnComplete(() => {
                // Change scene
                LoadMenuScene();
            });
        }
        else
        {
            LoadMenuScene();
        }
    }
    
    /// <summary>
    /// Load the menu scene
    /// </summary>
    private void LoadMenuScene()
    {
        try
        {
            SceneManager.LoadScene(menuSceneName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameWinUI] Cannot load scene '{menuSceneName}': {e.Message}");
            Debug.LogError("Please check the scene name in Inspector!");
        }
    }
    
    /// <summary>
    /// Hide UI immediately
    /// </summary>
    public void HideUI()
    {
        if (animationSequence != null && animationSequence.IsActive())
        {
            animationSequence.Kill();
        }
        
        if (winnerText != null)
        {
            winnerText.gameObject.SetActive(false);
        }
        
        isShowing = false;
    }
    
    /// <summary>
    /// Set the menu scene name
    /// </summary>
    public void SetMenuSceneName(string sceneName)
    {
        menuSceneName = sceneName;
    }
    
    /// <summary>
    /// Set the delay time before changing scene
    /// </summary>
    public void SetDelayTime(float delaySeconds)
    {
        delayBeforeSceneChange = Mathf.Max(0f, delaySeconds);
    }
    
    private void OnDestroy()
    {
        if (animationSequence != null && animationSequence.IsActive())
        {
            animationSequence.Kill();
        }
    }

    [ContextMenu("Show Win UI")]
    public void ShowWinUI()
    {
        ShowWinUI(true, "Checkmate");
    }
}
