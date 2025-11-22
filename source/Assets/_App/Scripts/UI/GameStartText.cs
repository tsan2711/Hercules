using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Component hiển thị text "Start" với animation mượt mà khi game sẵn sàng
/// </summary>
public class GameStartText : MonoBehaviour
{
    [Header("Text Component")]
    [SerializeField] private TextMeshProUGUI startText; // Nếu dùng TextMeshPro
    [SerializeField] private Text legacyText; // Fallback nếu dùng Text thường
    
    [Header("Animation Settings")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float scaleInDuration = 0.6f;
    [SerializeField] private float bounceScale = 1.2f;
    [SerializeField] private float bounceDuration = 0.3f;
    [SerializeField] private float stayDuration = 1.5f; // Thời gian hiển thị trước khi fade out
    [SerializeField] private float fadeOutDuration = 0.5f;
    
    [Header("Animation Style")]
    [SerializeField] private AnimationStyle style = AnimationStyle.Bounce;
    [SerializeField] private Ease fadeEase = Ease.OutQuad;
    [SerializeField] private Ease scaleEase = Ease.OutBack;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector3 originalScale;
    private Sequence animationSequence;
    private bool isAnimating = false;
    
    public enum AnimationStyle
    {
        Bounce,      // Scale bounce effect
        Fade,        // Chỉ fade in/out
        Scale,       // Scale đơn giản
        Pulse,       // Pulse effect
        Slide        // Slide từ trên xuống
    }
    
    private void Awake()
    {
        // Tự động tìm Text component nếu chưa assign
        if (startText == null && legacyText == null)
        {
            startText = GetComponent<TextMeshProUGUI>();
            legacyText = GetComponent<Text>();
        }
        
        // Tạo CanvasGroup nếu chưa có để control alpha
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        // Lấy RectTransform
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = gameObject.AddComponent<RectTransform>();
        }
        
        // Lưu scale gốc
        originalScale = rectTransform.localScale;
        
        // Ẩn text ban đầu
        canvasGroup.alpha = 0f;
        rectTransform.localScale = Vector3.zero;
    }
    
    private void Start()
    {
        // Subscribe vào event khi game ready
        if (GameStartDelayManager.Instance != null)
        {
            GameStartDelayManager.Instance.OnGameReady += ShowStartText;
        }
        else
        {
            Debug.LogWarning("[GameStartText] GameStartDelayManager.Instance is null!");
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe khi destroy
        if (GameStartDelayManager.Instance != null)
        {
            GameStartDelayManager.Instance.OnGameReady -= ShowStartText;
        }
        
        // Kill animation sequence
        if (animationSequence != null && animationSequence.IsActive())
        {
            animationSequence.Kill();
        }
    }
    
    /// <summary>
    /// Hiển thị text "Start" với animation
    /// </summary>
    public void ShowStartText()
    {
        if (isAnimating)
        {
            return; // Đang animate, bỏ qua
        }
        
        isAnimating = true;
        
        if (showDebugLogs)
        {
            Debug.Log("[GameStartText] Bắt đầu hiển thị text 'Start'");
        }
        
        // Kill sequence cũ nếu có
        if (animationSequence != null && animationSequence.IsActive())
        {
            animationSequence.Kill();
        }
        
        // Reset về trạng thái ban đầu
        canvasGroup.alpha = 0f;
        rectTransform.localScale = Vector3.zero;
        
        // Tạo sequence animation dựa trên style
        animationSequence = DOTween.Sequence();
        
        switch (style)
        {
            case AnimationStyle.Bounce:
                AnimateBounce(animationSequence);
                break;
            case AnimationStyle.Fade:
                AnimateFade(animationSequence);
                break;
            case AnimationStyle.Scale:
                AnimateScale(animationSequence);
                break;
            case AnimationStyle.Pulse:
                AnimatePulse(animationSequence);
                break;
            case AnimationStyle.Slide:
                AnimateSlide(animationSequence);
                break;
        }
        
        // Sau khi animation hoàn thành, set lại flag
        animationSequence.OnComplete(() => {
            isAnimating = false;
            if (showDebugLogs)
            {
                Debug.Log("[GameStartText] Animation hoàn thành");
            }
        });
    }
    
    /// <summary>
    /// Animation style: Bounce (mặc định - mượt mà nhất)
    /// </summary>
    private void AnimateBounce(Sequence seq)
    {
        // Fade in
        seq.Append(canvasGroup.DOFade(1f, fadeInDuration).SetEase(fadeEase));
        
        // Scale up với bounce effect
        seq.Join(rectTransform.DOScale(originalScale * bounceScale, scaleInDuration)
            .SetEase(scaleEase));
        
        // Bounce back về scale gốc
        seq.Append(rectTransform.DOScale(originalScale, bounceDuration)
            .SetEase(Ease.OutBounce));
        
        // Giữ nguyên một lúc
        seq.AppendInterval(stayDuration);
        
        // Fade out
        seq.Append(canvasGroup.DOFade(0f, fadeOutDuration).SetEase(fadeEase));
        
        // Scale down nhẹ khi fade out
        seq.Join(rectTransform.DOScale(originalScale * 0.8f, fadeOutDuration)
            .SetEase(Ease.InBack));
    }
    
    /// <summary>
    /// Animation style: Fade đơn giản
    /// </summary>
    private void AnimateFade(Sequence seq)
    {
        seq.Append(canvasGroup.DOFade(1f, fadeInDuration).SetEase(fadeEase));
        seq.AppendInterval(stayDuration);
        seq.Append(canvasGroup.DOFade(0f, fadeOutDuration).SetEase(fadeEase));
    }
    
    /// <summary>
    /// Animation style: Scale đơn giản
    /// </summary>
    private void AnimateScale(Sequence seq)
    {
        seq.Append(canvasGroup.DOFade(1f, fadeInDuration).SetEase(fadeEase));
        seq.Join(rectTransform.DOScale(originalScale, scaleInDuration).SetEase(scaleEase));
        seq.AppendInterval(stayDuration);
        seq.Append(canvasGroup.DOFade(0f, fadeOutDuration).SetEase(fadeEase));
        seq.Join(rectTransform.DOScale(Vector3.zero, fadeOutDuration).SetEase(Ease.InBack));
    }
    
    /// <summary>
    /// Animation style: Pulse (nhấp nháy)
    /// </summary>
    private void AnimatePulse(Sequence seq)
    {
        seq.Append(canvasGroup.DOFade(1f, fadeInDuration).SetEase(fadeEase));
        seq.Join(rectTransform.DOScale(originalScale, scaleInDuration).SetEase(scaleEase));
        
        // Pulse effect (scale lên xuống)
        seq.Append(rectTransform.DOScale(originalScale * 1.1f, 0.2f).SetEase(Ease.InOutSine));
        seq.Append(rectTransform.DOScale(originalScale, 0.2f).SetEase(Ease.InOutSine));
        seq.Append(rectTransform.DOScale(originalScale * 1.1f, 0.2f).SetEase(Ease.InOutSine));
        seq.Append(rectTransform.DOScale(originalScale, 0.2f).SetEase(Ease.InOutSine));
        
        seq.AppendInterval(stayDuration);
        seq.Append(canvasGroup.DOFade(0f, fadeOutDuration).SetEase(fadeEase));
    }
    
    /// <summary>
    /// Animation style: Slide từ trên xuống
    /// </summary>
    private void AnimateSlide(Sequence seq)
    {
        // Lưu vị trí gốc
        Vector3 originalPos = rectTransform.anchoredPosition;
        Vector3 startPos = originalPos + Vector3.up * 200f; // Slide từ trên
        
        // Set vị trí ban đầu
        rectTransform.anchoredPosition = startPos;
        
        // Fade in + slide down
        seq.Append(canvasGroup.DOFade(1f, fadeInDuration).SetEase(fadeEase));
        seq.Join(rectTransform.DOAnchorPos(originalPos, scaleInDuration).SetEase(Ease.OutBounce));
        
        seq.AppendInterval(stayDuration);
        
        // Fade out + slide up
        seq.Append(canvasGroup.DOFade(0f, fadeOutDuration).SetEase(fadeEase));
        seq.Join(rectTransform.DOAnchorPos(startPos, fadeOutDuration).SetEase(Ease.InBack));
    }
    
    /// <summary>
    /// Set text hiển thị (nếu muốn thay đổi từ "Start")
    /// </summary>
    public void SetText(string text)
    {
        if (startText != null)
        {
            startText.text = text;
        }
        else if (legacyText != null)
        {
            legacyText.text = text;
        }
    }
    
    /// <summary>
    /// Force hide text ngay lập tức
    /// </summary>
    public void HideImmediately()
    {
        if (animationSequence != null && animationSequence.IsActive())
        {
            animationSequence.Kill();
        }
        
        canvasGroup.alpha = 0f;
        rectTransform.localScale = Vector3.zero;
        isAnimating = false;
    }
    
    /// <summary>
    /// Set animation style
    /// </summary>
    public void SetAnimationStyle(AnimationStyle newStyle)
    {
        style = newStyle;
    }
}

