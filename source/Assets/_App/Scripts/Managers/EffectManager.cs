using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manager quản lý tất cả các hiệu ứng trong game
/// </summary>
public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance { get; private set; }
    
    [Header("Effect Settings")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private int maxConcurrentEffects = 50;
    
    // Dictionary lưu trữ các effect đang chạy
    private Dictionary<string, List<IEffect>> activeEffects = new Dictionary<string, List<IEffect>>();
    private Queue<IEffect> effectPool = new Queue<IEffect>();
    private int currentEffectCount = 0;
    
    // Events
    public System.Action<IEffect> OnEffectStarted;
    public System.Action<IEffect> OnEffectCompleted;
    public System.Action<IEffect> OnEffectStopped;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        InitializeManager();
    }
    
    private void InitializeManager()
    {
        activeEffects.Clear();
        currentEffectCount = 0;
        
        if (debugMode)
        {
            Debug.Log("EffectManager initialized successfully");
        }
    }
    
    /// <summary>
    /// Chạy hiệu ứng trên một GameObject
    /// </summary>
    /// <typeparam name="T">Loại hiệu ứng</typeparam>
    /// <param name="target">GameObject đích</param>
    /// <param name="parameters">Tham số cho hiệu ứng</param>
    /// <param name="onComplete">Callback khi hoàn thành</param>
    /// <returns>Instance của hiệu ứng</returns>
    public T PlayEffect<T>(GameObject target, Dictionary<string, object> parameters = null, System.Action onComplete = null) where T : MonoBehaviour, IEffect
    {
        if (target == null)
        {
            Debug.LogError("Target GameObject is null");
            return null;
        }
        
        if (currentEffectCount >= maxConcurrentEffects)
        {
            Debug.LogWarning("Maximum concurrent effects reached. Skipping effect.");
            return null;
        }
        
        // Kiểm tra xem target đã có component này chưa
        T effect = target.GetComponent<T>();
        
        if (effect == null)
        {
            // Thêm component nếu chưa có
            effect = target.AddComponent<T>();
        }
        
        // Thiết lập target
        effect.Target = target;
        
        // Thiết lập parameters nếu có
        if (parameters != null)
        {
            effect.SetParameters(parameters);
        }
        
        // Thêm vào danh sách active effects
        AddToActiveEffects(effect);
        
        // Chạy hiệu ứng với callback
        effect.PlayEffect(() => {
            OnEffectCompleted?.Invoke(effect);
            RemoveFromActiveEffects(effect);
            onComplete?.Invoke();
            
            if (debugMode)
            {
                Debug.Log($"Effect {effect.EffectName} completed on {target.name}");
            }
        });
        
        OnEffectStarted?.Invoke(effect);
        
        if (debugMode)
        {
            Debug.Log($"Playing effect {effect.EffectName} on {target.name}");
        }
        
        return effect;
    }
    
    /// <summary>
    /// Dừng tất cả hiệu ứng trên một GameObject
    /// </summary>
    /// <param name="target">GameObject đích</param>
    public void StopAllEffects(GameObject target)
    {
        if (target == null) return;
        
        var effects = target.GetComponents<IEffect>();
        foreach (var effect in effects)
        {
            StopEffect(effect);
        }
    }
    
    /// <summary>
    /// Dừng một hiệu ứng cụ thể
    /// </summary>
    /// <param name="effect">Hiệu ứng cần dừng</param>
    public void StopEffect(IEffect effect)
    {
        if (effect == null) return;
        
        effect.StopEffect();
        RemoveFromActiveEffects(effect);
        OnEffectStopped?.Invoke(effect);
        
        if (debugMode)
        {
            Debug.Log($"Stopped effect {effect.EffectName}");
        }
    }
    
    /// <summary>
    /// Dừng tất cả hiệu ứng của một loại
    /// </summary>
    /// <param name="effectName">Tên hiệu ứng</param>
    public void StopAllEffectsOfType(string effectName)
    {
        if (activeEffects.ContainsKey(effectName))
        {
            var effectList = new List<IEffect>(activeEffects[effectName]);
            foreach (var effect in effectList)
            {
                StopEffect(effect);
            }
        }
    }
    
    /// <summary>
    /// Lấy tất cả hiệu ứng đang chạy
    /// </summary>
    /// <returns>Dictionary các hiệu ứng active</returns>
    public Dictionary<string, List<IEffect>> GetActiveEffects()
    {
        return new Dictionary<string, List<IEffect>>(activeEffects);
    }
    
    /// <summary>
    /// Kiểm tra xem có hiệu ứng nào đang chạy trên GameObject không
    /// </summary>
    /// <param name="target">GameObject cần kiểm tra</param>
    /// <returns>True nếu có hiệu ứng đang chạy</returns>
    public bool HasActiveEffects(GameObject target)
    {
        if (target == null) return false;
        
        var effects = target.GetComponents<IEffect>();
        foreach (var effect in effects)
        {
            if (effect.IsPlaying)
                return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Lấy số lượng hiệu ứng đang chạy
    /// </summary>
    /// <returns>Số lượng hiệu ứng active</returns>
    public int GetActiveEffectCount()
    {
        return currentEffectCount;
    }
    
    // Helper methods
    private void AddToActiveEffects(IEffect effect)
    {
        string effectName = effect.EffectName;
        
        if (!activeEffects.ContainsKey(effectName))
        {
            activeEffects[effectName] = new List<IEffect>();
        }
        
        activeEffects[effectName].Add(effect);
        currentEffectCount++;
    }
    
    private void RemoveFromActiveEffects(IEffect effect)
    {
        string effectName = effect.EffectName;
        
        if (activeEffects.ContainsKey(effectName))
        {
            activeEffects[effectName].Remove(effect);
            currentEffectCount = Mathf.Max(0, currentEffectCount - 1);
            
            // Remove empty lists
            if (activeEffects[effectName].Count == 0)
            {
                activeEffects.Remove(effectName);
            }
        }
    }
    
    // Utility methods for specific effects
    
    /// <summary>
    /// Shortcut để chạy Dissolve In effect
    /// </summary>
    /// <param name="target">GameObject đích</param>
    /// <param name="duration">Thời gian hiệu ứng</param>
    /// <param name="onComplete">Callback khi hoàn thành</param>
    /// <returns>DissolveInEffect instance</returns>
    public DissolveInEffect PlayDissolveIn(GameObject target, float duration = 3f, System.Action onComplete = null)
    {
        var parameters = new Dictionary<string, object>
        {
            { "duration", duration },
            { "resetonplay", true }
        };
        
        return PlayEffect<DissolveInEffect>(target, parameters, onComplete);
    }
    
    private void OnDestroy()
    {
        // Cleanup
        foreach (var effectList in activeEffects.Values)
        {
            foreach (var effect in effectList)
            {
                effect?.StopEffect();
            }
        }
        
        activeEffects.Clear();
    }
    
    // Debug methods
    [ContextMenu("Print Active Effects")]
    private void PrintActiveEffects()
    {
        Debug.Log($"Active Effects Count: {currentEffectCount}");
        foreach (var kvp in activeEffects)
        {
            Debug.Log($"Effect Type: {kvp.Key}, Count: {kvp.Value.Count}");
        }
    }
    
    [ContextMenu("Stop All Effects")]
    private void StopAllEffectsDebug()
    {
        var allEffects = new List<IEffect>();
        foreach (var effectList in activeEffects.Values)
        {
            allEffects.AddRange(effectList);
        }
        
        foreach (var effect in allEffects)
        {
            StopEffect(effect);
        }
    }
}
