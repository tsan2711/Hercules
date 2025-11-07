using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manager tự động tạo ra StageLevelUI (Prefab) dựa trên các level được config
/// </summary>
public class StageLevelManager : MonoBehaviour
{
    [Header("Level Configuration")]
    [SerializeField] private int totalLevels = 10; // Tổng số level
    [SerializeField] private int startLevel = 1; // Level bắt đầu (thường là 1)
    
    [Header("UI Prefab")]
    [SerializeField] private GameObject stageLevelUIPrefab; // Prefab của StageLevelUI
    
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
        
        // Tạo các level UI
        for (int i = 0; i < totalLevels; i++)
        {
            int levelNumber = startLevel + i;
            CreateLevelUI(levelNumber);
        }
        
        Debug.Log($"Created {totalLevels} level UIs (Level {startLevel} to {startLevel + totalLevels - 1})");
    }
    
    /// <summary>
    /// Tạo một level UI cụ thể
    /// </summary>
    private void CreateLevelUI(int levelNumber)
    {
        GameObject levelUIObject = Instantiate(stageLevelUIPrefab, containerParent);
        levelUIObject.name = $"Level_{levelNumber}_UI";
        
        StageLevelUI levelUI = levelUIObject.GetComponent<StageLevelUI>();
        if (levelUI == null)
        {
            levelUI = levelUIObject.AddComponent<StageLevelUI>();
        }
        
        // Initialize level UI với level number
        levelUI.Initialize(levelNumber);
        
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
    public void AddLevel(int levelNumber)
    {
        CreateLevelUI(levelNumber);
        totalLevels = createdLevelUIs.Count;
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

