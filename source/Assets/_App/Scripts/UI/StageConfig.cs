using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject chứa cấu hình cho các stage trong game
/// </summary>
[CreateAssetMenu(fileName = "StageConfig", menuName = "Game/Stage Configuration")]
public class StageConfig : ScriptableObject
{
    [System.Serializable]
    public class StageData
    {
        [Header("Stage Info")]
        public string stageName;
        public int stageIndex;
        public string stageDescription;
        
        [Header("Scene Settings")]
        public string sceneName; // Tên scene cần load
        
        [Header("UI Settings")]
        public Sprite stageThumbnail; // Ảnh thumbnail cho stage
        public bool isUnlocked = true; // Stage đã unlock chưa
    }
    
    [Header("Stage List")]
    [SerializeField] private List<StageData> stages = new List<StageData>();
    
    /// <summary>
    /// Lấy danh sách tất cả stages
    /// </summary>
    public List<StageData> GetStages()
    {
        return stages;
    }
    
    /// <summary>
    /// Lấy số lượng stages
    /// </summary>
    public int GetStageCount()
    {
        return stages.Count;
    }
    
    /// <summary>
    /// Lấy stage theo index
    /// </summary>
    public StageData GetStage(int index)
    {
        if (index >= 0 && index < stages.Count)
        {
            return stages[index];
        }
        return null;
    }
    
    /// <summary>
    /// Thêm stage mới
    /// </summary>
    public void AddStage(StageData stage)
    {
        if (stage != null && !stages.Contains(stage))
        {
            stages.Add(stage);
        }
    }
    
    /// <summary>
    /// Xóa stage
    /// </summary>
    public void RemoveStage(int index)
    {
        if (index >= 0 && index < stages.Count)
        {
            stages.RemoveAt(index);
        }
    }
}

