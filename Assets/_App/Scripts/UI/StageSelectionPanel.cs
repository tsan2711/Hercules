using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Panel hiển thị danh sách stage, sử dụng list StageItem có sẵn để initialize
/// </summary>
public class StageSelectionPanel : MonoBehaviour
{
    [Header("Stage Configuration")]
    [SerializeField] private StageConfig stageConfig;
    
    [Header("Stage Items")]
    [SerializeField] private List<StageItem> stageItems = new List<StageItem>(); // List các StageItem có sẵn trong scene
    
    [Header("Audio")]
    [SerializeField] private bool playClickSound = true;
    
    private void OnEnable()
    {
        // Khi panel được kích hoạt, initialize các stage items
        InitializeStageItems();
    }
    
    /// <summary>
    /// Initialize các StageItem từ StageConfig
    /// </summary>
    private void InitializeStageItems()
    {
        if (stageConfig == null)
        {
            Debug.LogWarning("StageConfig is not assigned!");
            return;
        }
        
        // Lấy danh sách stages từ config
        List<StageConfig.StageData> stages = stageConfig.GetStages();
        
        // Initialize từng StageItem với StageData tương ứng
        for (int i = 0; i < stageItems.Count && i < stages.Count; i++)
        {
            if (stageItems[i] != null)
            {
                stageItems[i].Initialize(stages[i], this);
            }
        }
        
        // Ẩn các StageItem thừa nếu số lượng StageItem nhiều hơn số stage
        for (int i = stages.Count; i < stageItems.Count; i++)
        {
            if (stageItems[i] != null)
            {
                stageItems[i].gameObject.SetActive(false);
            }
        }
        
        // Cảnh báo nếu số lượng stage nhiều hơn số StageItem
        if (stages.Count > stageItems.Count)
        {
            Debug.LogWarning($"StageConfig has {stages.Count} stages but only {stageItems.Count} StageItems assigned!");
        }
    }
    
    /// <summary>
    /// Xử lý khi click vào stage button
    /// </summary>
    public void OnStageButtonClicked(StageConfig.StageData stageData)
    {
        PlayClickSound();
        
        if (stageData == null)
        {
            Debug.LogWarning("Stage Data is null!");
            return;
        }
        
        if (string.IsNullOrEmpty(stageData.sceneName))
        {
            Debug.LogWarning($"Scene name is not set for stage: {stageData.stageName}");
            return;
        }
        
        Debug.Log($"Loading stage: {stageData.stageName} - Scene: {stageData.sceneName}");
        
        // Load scene
        SceneManager.LoadScene(stageData.sceneName);
    }
    
    /// <summary>
    /// Đóng panel
    /// </summary>
    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }
    
    /// <summary>
    /// Play click sound nếu có SoundManager
    /// </summary>
    private void PlayClickSound()
    {
        if (playClickSound && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayClick();
        }
    }
}

/// <summary>
/// Component cho StageItem, cần implement Initialize method
/// </summary>
public abstract class StageItem : MonoBehaviour
{
    protected StageConfig.StageData stageData;
    protected StageSelectionPanel panel;
    
    /// <summary>
    /// Initialize StageItem với StageData
    /// </summary>
    public virtual void Initialize(StageConfig.StageData data, StageSelectionPanel parentPanel)
    {
        stageData = data;
        panel = parentPanel;
        gameObject.SetActive(true);
        UpdateUI();
    }
    
    /// <summary>
    /// Update UI của StageItem (tên, thumbnail, unlock status, etc.)
    /// Override method này để customize UI
    /// </summary>
    protected virtual void UpdateUI()
    {
        // Override trong class con để setup UI
    }
    
    /// <summary>
    /// Xử lý khi click vào StageItem
    /// </summary>
    public virtual void OnClick()
    {
        if (panel != null && stageData != null)
        {
            panel.OnStageButtonClicked(stageData);
        }
    }
    
    /// <summary>
    /// Lấy StageData của item này
    /// </summary>
    public StageConfig.StageData GetStageData()
    {
        return stageData;
    }
}

