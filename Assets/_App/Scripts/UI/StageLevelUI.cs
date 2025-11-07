using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Component cho StageLevelUI Prefab, xử lý click và load scene tương ứng
/// </summary>
[RequireComponent(typeof(Button))]
public class StageLevelUI : MonoBehaviour
{
    [Header("Level Info")]
    [SerializeField] private int levelNumber = 1;
    
    [Header("UI References (Optional)")]
    [SerializeField] private TextMeshProUGUI levelNumberText; // TextMeshPro hiển thị số level
    [SerializeField] private TextMeshProUGUI levelNameText; // TextMeshPro hiển thị tên level (nếu có)
    
    [Header("Settings")]
    [SerializeField] private string sceneNamePrefix = "Level_"; // Prefix cho tên scene (mặc định: "Level_")
    [SerializeField] private bool useSceneNamePrefix = true; // Có sử dụng prefix không
    
    [Header("Audio")]
    [SerializeField] private bool playClickSound = true;
    
    private Button button;
    private string sceneName;
    
    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnLevelClicked);
        }
    }
    
    /// <summary>
    /// Initialize level UI với level number
    /// </summary>
    public void Initialize(int level)
    {
        levelNumber = level;
        UpdateSceneName();
        UpdateUI();
    }
    
    /// <summary>
    /// Cập nhật tên scene dựa trên level number
    /// </summary>
    private void UpdateSceneName()
    {
        if (useSceneNamePrefix)
        {
            sceneName = $"{sceneNamePrefix}{levelNumber}";
        }
        else
        {
            sceneName = levelNumber.ToString();
        }
    }
    
    /// <summary>
    /// Cập nhật UI hiển thị
    /// </summary>
    private void UpdateUI()
    {
        // Update level number text
        if (levelNumberText != null)
        {
            levelNumberText.text = levelNumber.ToString();
        }
        
        // Update level name text (nếu có)
        if (levelNameText != null)
        {
            levelNameText.text = $"Level {levelNumber}";
        }
    }
    
    /// <summary>
    /// Xử lý khi click vào level
    /// </summary>
    private void OnLevelClicked()
    {
        PlayClickSound();
        
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning($"Scene name is empty for level {levelNumber}!");
            return;
        }
        
        Debug.Log($"Loading Level {levelNumber} - Scene: {sceneName}");
        
        // Load scene
        LoadLevelScene();
    }
    
    /// <summary>
    /// Load scene của level
    /// </summary>
    private void LoadLevelScene()
    {
        try
        {
            SceneManager.LoadScene(sceneName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load scene: {sceneName}. Error: {e.Message}");
            Debug.LogError($"Make sure scene '{sceneName}' exists in Build Settings!");
        }
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
    
    /// <summary>
    /// Lấy level number
    /// </summary>
    public int GetLevelNumber()
    {
        return levelNumber;
    }
    
    /// <summary>
    /// Lấy scene name
    /// </summary>
    public string GetSceneName()
    {
        return sceneName;
    }
    
    /// <summary>
    /// Set level number (runtime)
    /// </summary>
    public void SetLevelNumber(int level)
    {
        levelNumber = level;
        UpdateSceneName();
        UpdateUI();
    }
    
    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnLevelClicked);
        }
    }
}

