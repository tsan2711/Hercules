using System.Collections;
using UnityEngine;

/// <summary>
/// Manager quản lý delay sau khi load scene trước khi cho phép chơi
/// </summary>
public class GameStartDelayManager : MonoBehaviour
{
    public static GameStartDelayManager Instance { get; private set; }
    
    [Header("Delay Settings")]
    [SerializeField] private float delayAfterSceneLoad = 3f; // Thời gian delay (giây) - có thể config trong Inspector
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    // Flag để kiểm tra xem game đã sẵn sàng chơi chưa
    public bool IsGameReady { get; private set; } = false;
    
    // Event khi game sẵn sàng
    public System.Action OnGameReady;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
    }
    
    private void Start()
    {
        // Bắt đầu delay sau khi scene load
        StartCoroutine(DelayBeforeGameStart());
    }
    
    /// <summary>
    /// Coroutine delay trước khi cho phép chơi
    /// </summary>
    private IEnumerator DelayBeforeGameStart()
    {
        if (showDebugLogs)
        {
            Debug.Log($"[GameStartDelayManager] Bắt đầu delay {delayAfterSceneLoad} giây trước khi cho phép chơi...");
        }
        
        // Đợi n giây
        yield return new WaitForSeconds(delayAfterSceneLoad);
        
        // Enable game
        IsGameReady = true;
        
        if (showDebugLogs)
        {
            Debug.Log("[GameStartDelayManager] Game đã sẵn sàng! Người chơi có thể bắt đầu chơi.");
        }
        
        // Trigger event
        OnGameReady?.Invoke();
    }
    
    /// <summary>
    /// Set delay time từ code (runtime)
    /// </summary>
    public void SetDelayTime(float delaySeconds)
    {
        delayAfterSceneLoad = Mathf.Max(0f, delaySeconds);
        
        if (showDebugLogs)
        {
            Debug.Log($"[GameStartDelayManager] Delay time đã được set thành {delayAfterSceneLoad} giây");
        }
    }
    
    /// <summary>
    /// Get delay time hiện tại
    /// </summary>
    public float GetDelayTime()
    {
        return delayAfterSceneLoad;
    }
    
    /// <summary>
    /// Force enable game ngay lập tức (skip delay)
    /// </summary>
    public void ForceEnableGame()
    {
        StopAllCoroutines();
        IsGameReady = true;
        OnGameReady?.Invoke();
        
        if (showDebugLogs)
        {
            Debug.Log("[GameStartDelayManager] Game đã được force enable!");
        }
    }
}

