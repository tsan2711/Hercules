using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serializable data class chứa thông tin level và sprite
/// </summary>
[System.Serializable]
public class LevelData
{
    public int level = 1; // Số level
    public Sprite sprite; // Sprite cho level này
}

/// <summary>
/// Manager tự động tạo ra StageLevelUI (Prefab) dựa trên các level được config
/// </summary>
public class StageLevelManager : MonoBehaviour
{
    [Header("Level Configuration")]
    [SerializeField] private LevelData[] levelDataArray; // Mảng dữ liệu level với sprite
    
    [Header("UI Prefab")]
    [SerializeField] private StageLevelUI stageLevelUIPrefab; // Prefab của StageLevelUI
    
    [Header("Parent Container")]
    [SerializeField] private Transform containerParent; // Container để chứa các level UI (Grid Layout, Vertical Layout, etc.)
    
    [Header("Settings")]
    [SerializeField] private bool createOnStart = true; // Tự động tạo khi Start
    [SerializeField] private bool clearExistingOnCreate = true; // Xóa các level UI cũ khi tạo mới
    
    private List<StageLevelUI> createdLevelUIs = new List<StageLevelUI>();
    
    private void Start()
    {
        if (createOnStart)
        {
            CreateLevelUIs();
        }
    }
    
    /// <summary>
    /// Tạo tất cả các level UI từ prefab
    /// </summary>
    public void CreateLevelUIs()
    {
        if (stageLevelUIPrefab == null)
        {
            Debug.LogError("StageLevelUI Prefab is not assigned!");
            return;
        }
        
        if (containerParent == null)
        {
            Debug.LogWarning("Container Parent is not assigned! Using this transform as parent.");
            containerParent = transform;
        }
        
        // Xóa các level UI cũ nếu cần
        if (clearExistingOnCreate)
        {
            ClearExistingLevelUIs();
        }
        
        // Kiểm tra mảng dữ liệu
        if (levelDataArray == null || levelDataArray.Length == 0)
        {
            Debug.LogWarning("Level Data Array is empty! No levels will be created.");
            return;
        }
        
        // Tạo các level UI từ mảng dữ liệu
        foreach (LevelData levelData in levelDataArray)
        {
            if (levelData != null)
            {
                CreateLevelUI(levelData.level, levelData.sprite);
            }
        }
        
        Debug.Log($"Created {levelDataArray.Length} level UIs from level data array");
    }
    
    /// <summary>
    /// Tạo một level UI cụ thể
    /// </summary>
    private void CreateLevelUI(int levelNumber, Sprite sprite = null)
    {
        GameObject levelUIObject = Instantiate(stageLevelUIPrefab.gameObject, containerParent);
        levelUIObject.name = $"Level_{levelNumber}_UI";
        
        StageLevelUI levelUI = levelUIObject.GetComponent<StageLevelUI>();
        if (levelUI == null)
        {
            levelUI = levelUIObject.AddComponent<StageLevelUI>();
        }
        
        // Initialize level UI với level number và sprite
        levelUI.Initialize(levelNumber, sprite);
        
        createdLevelUIs.Add(levelUI);
    }
    
    /// <summary>
    /// Xóa tất cả các level UI đã tạo
    /// </summary>
    public void ClearExistingLevelUIs()
    {
        // Xóa từ list
        foreach (var levelUI in createdLevelUIs)
        {
            if (levelUI != null)
            {
                Destroy(levelUI.gameObject);
            }
        }
        createdLevelUIs.Clear();
        
        // Xóa tất cả child objects trong container (phòng trường hợp có object không có StageLevelUI component)
        if (containerParent != null)
        {
            for (int i = containerParent.childCount - 1; i >= 0; i--)
            {
                Transform child = containerParent.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }
    }
    
    /// <summary>
    /// Thêm level mới (runtime)
    /// </summary>
    public void AddLevel(int levelNumber, Sprite sprite = null)
    {
        CreateLevelUI(levelNumber, sprite);
    }
    
    /// <summary>
    /// Lấy danh sách tất cả level UI đã tạo
    /// </summary>
    public List<StageLevelUI> GetLevelUIs()
    {
        return new List<StageLevelUI>(createdLevelUIs);
    }
    
    /// <summary>
    /// Lấy level UI theo level number
    /// </summary>
    public StageLevelUI GetLevelUI(int levelNumber)
    {
        foreach (var levelUI in createdLevelUIs)
        {
            if (levelUI != null && levelUI.GetLevelNumber() == levelNumber)
            {
                return levelUI;
            }
        }
        return null;
    }
}

