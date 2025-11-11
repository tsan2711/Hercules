using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Example implementation của StageItem
/// Bạn có thể tạo class riêng extend từ StageItem hoặc sử dụng class này
/// </summary>
[RequireComponent(typeof(Button))]
public class StageItemExample : StageItem
{
    [Header("UI References")]
    [SerializeField] private Text stageNameText;
    [SerializeField] private Text stageDescriptionText;
    [SerializeField] private Image thumbnailImage;
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private GameObject unlockedIndicator;
    
    private Button button;
    
    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnClick);
        }
    }
    
    /// <summary>
    /// Override UpdateUI để customize UI của stage item
    /// </summary>
    protected override void UpdateUI()
    {
        if (stageData == null) return;
        
        // Update stage name
        if (stageNameText != null)
        {
            stageNameText.text = stageData.stageName;
        }
        
        // Update stage description
        if (stageDescriptionText != null)
        {
            stageDescriptionText.text = stageData.stageDescription;
        }
        
        // Update thumbnail
        if (thumbnailImage != null && stageData.stageThumbnail != null)
        {
            thumbnailImage.sprite = stageData.stageThumbnail;
        }
        
        // Update lock/unlock status
        bool isUnlocked = stageData.isUnlocked;
        
        if (lockedOverlay != null)
        {
            lockedOverlay.SetActive(!isUnlocked);
        }
        
        if (unlockedIndicator != null)
        {
            unlockedIndicator.SetActive(isUnlocked);
        }
        
        // Set button interactable
        if (button != null)
        {
            button.interactable = isUnlocked;
        }
    }
    
    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
        }
    }
}

