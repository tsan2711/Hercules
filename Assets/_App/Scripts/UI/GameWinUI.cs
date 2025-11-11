using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// UI hiển thị thông báo người thắng và tự động chuyển về menu sau 5 giây
/// </summary>
public class GameWinUI : MonoBehaviour
{
    public static GameWinUI Instance { get; private set; }
    
    [Header("UI Components")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private TextMeshProUGUI winnerText; // Text hiển thị người thắng
    [SerializeField] private TextMeshProUGUI reasonText; // Text hiển thị lý do (Checkmate, Stalemate)
    [SerializeField] private TextMeshProUGUI countdownText; // Text đếm ngược
    
    [Header("Animation Settings")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float scaleInDuration = 0.6f;
    [SerializeField] private float bounceScale = 1.1f;
    [SerializeField] private float bounceDuration = 0.3f;
    
    [Header("Scene Settings")]
    [SerializeField] private string menuSceneName = "Menu"; // Tên scene menu
    [SerializeField] private float delayBeforeSceneChange = 5f; // Delay 5 giây trước khi chuyển scene
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    private CanvasGroup canvasGroup;
    private RectTransform panelRectTransform;
    private Sequence animationSequence;
    private bool isShowing = false;
    private float countdownTimer = 0f;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        
        // Tự động tìm components nếu chưa assign
        if (winPanel == null)
        {
            winPanel = gameObject;
        }
        
        // Tạo CanvasGroup nếu chưa có
        canvasGroup = winPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = winPanel.AddComponent<CanvasGroup>();
        }
        
        // Lấy RectTransform
        panelRectTransform = winPanel.GetComponent<RectTransform>();
        if (panelRectTransform == null)
        {
            panelRectTransform = winPanel.AddComponent<RectTransform>();
        }
        
        // Ẩn panel ban đầu
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        
        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }
    }
    
    private void Start()
    {
        // Subscribe vào event từ ChessCheckSystem
        // Note: ChessCheckSystem sẽ gọi ShowWinUI trực tiếp
    }
    
    private void Update()
    {
        // Đếm ngược nếu đang hiển thị
        if (isShowing && countdownText != null)
        {
            countdownTimer -= Time.deltaTime;
            
            if (countdownTimer > 0f)
            {
                int seconds = Mathf.CeilToInt(countdownTimer);
                countdownText.text = $"Quay về menu sau {seconds} giây...";
            }
            else
            {
                countdownText.text = "Đang chuyển về menu...";
            }
        }
    }
    
    /// <summary>
    /// Hiển thị UI thông báo người thắng
    /// </summary>
    /// <param name="winner">true = Trắng thắng, false = Đen thắng, null = Hòa</param>
    /// <param name="reason">Lý do kết thúc (Checkmate, Stalemate, etc.)</param>
    public void ShowWinUI(bool? winner, string reason = "Checkmate")
    {
        if (isShowing)
        {
            return; // Đã đang hiển thị, bỏ qua
        }
        
        isShowing = true;
        
        if (showDebugLogs)
        {
            string winnerName = winner.HasValue 
                ? (winner.Value ? "Trắng" : "Đen") 
                : "Hòa";
            Debug.Log($"[GameWinUI] Hiển thị UI thắng: {winnerName}, Lý do: {reason}");
        }
        
        // Kích hoạt panel
        if (winPanel != null)
        {
            winPanel.SetActive(true);
        }
        
        // Set text
        if (winnerText != null)
        {
            if (winner.HasValue)
            {
                winnerText.text = winner.Value ? "QUÂN TRẮNG THẮNG!" : "QUÂN ĐEN THẮNG!";
            }
            else
            {
                winnerText.text = "HÒA CỜ!";
            }
        }
        
        if (reasonText != null)
        {
            reasonText.text = reason;
        }
        
        // Reset countdown
        countdownTimer = delayBeforeSceneChange;
        if (countdownText != null)
        {
            countdownText.text = $"Quay về menu sau {Mathf.CeilToInt(countdownTimer)} giây...";
        }
        
        // Enable canvas group
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        
        // Kill sequence cũ nếu có
        if (animationSequence != null && animationSequence.IsActive())
        {
            animationSequence.Kill();
        }
        
        // Reset về trạng thái ban đầu
        canvasGroup.alpha = 0f;
        panelRectTransform.localScale = Vector3.zero;
        
        // Tạo animation sequence
        animationSequence = DOTween.Sequence();
        
        // Fade in
        animationSequence.Append(canvasGroup.DOFade(1f, fadeInDuration).SetEase(Ease.OutQuad));
        
        // Scale up với bounce
        animationSequence.Join(panelRectTransform.DOScale(Vector3.one * bounceScale, scaleInDuration)
            .SetEase(Ease.OutBack));
        
        // Bounce back về scale gốc
        animationSequence.Append(panelRectTransform.DOScale(Vector3.one, bounceDuration)
            .SetEase(Ease.OutBounce));
        
        // Sau khi animation xong, bắt đầu đếm ngược để chuyển scene
        animationSequence.OnComplete(() => {
            StartCoroutine(CountdownAndLoadScene());
        });
    }
    
    /// <summary>
    /// Coroutine đếm ngược và chuyển scene
    /// </summary>
    private System.Collections.IEnumerator CountdownAndLoadScene()
    {
        // Đợi hết thời gian delay
        yield return new WaitForSeconds(delayBeforeSceneChange);
        
        if (showDebugLogs)
        {
            Debug.Log($"[GameWinUI] Chuyển về scene menu: {menuSceneName}");
        }
        
        // Fade out trước khi chuyển scene
        if (animationSequence != null && animationSequence.IsActive())
        {
            animationSequence.Kill();
        }
        
        animationSequence = DOTween.Sequence();
        animationSequence.Append(canvasGroup.DOFade(0f, 0.3f).SetEase(Ease.InQuad));
        animationSequence.OnComplete(() => {
            // Chuyển scene
            LoadMenuScene();
        });
    }
    
    /// <summary>
    /// Chuyển về scene menu
    /// </summary>
    private void LoadMenuScene()
    {
        try
        {
            SceneManager.LoadScene(menuSceneName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameWinUI] Không thể load scene '{menuSceneName}': {e.Message}");
            Debug.LogError("Vui lòng kiểm tra tên scene trong Inspector!");
        }
    }
    
    /// <summary>
    /// Ẩn UI ngay lập tức
    /// </summary>
    public void HideUI()
    {
        if (animationSequence != null && animationSequence.IsActive())
        {
            animationSequence.Kill();
        }
        
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        
        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }
        
        isShowing = false;
    }
    
    /// <summary>
    /// Set tên scene menu
    /// </summary>
    public void SetMenuSceneName(string sceneName)
    {
        menuSceneName = sceneName;
    }
    
    /// <summary>
    /// Set thời gian delay trước khi chuyển scene
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
}

