using UnityEngine;
using DG.Tweening;

/// <summary>
/// Controller cho quân cờ với các hiệu ứng dissolve
/// </summary>
public class PawnController : MonoBehaviour
{
    [Header("Pawn Settings")]
    [SerializeField] private PawnType pawnType = PawnType.Pawn;
    [SerializeField] private PawnTeam team = PawnTeam.White;
    [SerializeField] private bool spawnWithDissolveIn = true;
    
    [Header("Dissolve Settings")]
    [SerializeField] private float dissolveInDuration = 3f;
    [SerializeField] private float dissolveOutDuration = 2f;
    [SerializeField] private bool autoPlayDissolveIn = true;
    [SerializeField] private float autoPlayDelay = 0f;
    
    [Header("Animation Settings")]
    [SerializeField] private float spawnHeight = 2f;
    [SerializeField] private float dropDuration = 1f;
    [SerializeField] private AnimationCurve dropCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    // Private fields
    private DissolveInEffect dissolveInEffect;
    private Vector3 originalPosition;
    private bool isInitialized = false;
    private bool isActive = true;
    
    // Events
    public System.Action<PawnController> OnPawnSpawned;
    public System.Action<PawnController> OnPawnDestroyed;
    public System.Action<PawnController> OnDissolveInComplete;
    public System.Action<PawnController> OnDissolveOutComplete;
    
    // Properties
    public PawnType Type => pawnType;
    public PawnTeam Team => team;
    public bool IsActive => isActive;
    public bool IsDissolving => dissolveInEffect != null && dissolveInEffect.IsPlaying;
    
    private void Awake()
    {
        Initialize();
    }
    
    private void Start()
    {
        if (autoPlayDissolveIn && spawnWithDissolveIn)
        {
            if (autoPlayDelay > 0f)
            {
                Invoke(nameof(StartSpawnSequence), autoPlayDelay);
            }
            else
            {
                StartSpawnSequence();
            }
        }
    }
    
    private void Initialize()
    {
        if (isInitialized) return;
        
        originalPosition = transform.position;
        
        // Setup dissolve effect nếu chưa có
        dissolveInEffect = GetComponent<DissolveInEffect>();
        if (dissolveInEffect == null)
        {
            dissolveInEffect = gameObject.AddComponent<DissolveInEffect>();
        }
        
        // Configure dissolve effect
        dissolveInEffect.Duration = dissolveInDuration;
        
        isInitialized = true;
    }
    
    /// <summary>
    /// Bắt đầu sequence spawn với dissolve in
    /// </summary>
    public void StartSpawnSequence()
    {
        if (!isInitialized)
        {
            Initialize();
        }
        
        if (spawnWithDissolveIn)
        {
            // Position pawn above spawn point
            transform.position = originalPosition + Vector3.up * spawnHeight;
            
            // Start dissolve in effect
            StartDissolveIn(() => {
                // Drop down animation after dissolve completes
                DropToPosition();
            });
        }
        else
        {
            // Just drop without dissolve
            transform.position = originalPosition + Vector3.up * spawnHeight;
            DropToPosition();
        }
    }
    
    /// <summary>
    /// Bắt đầu hiệu ứng dissolve in
    /// </summary>
    /// <param name="onComplete">Callback khi hoàn thành</param>
    public void StartDissolveIn(System.Action onComplete = null)
    {
        if (!isInitialized)
        {
            Initialize();
        }
        
        // Sử dụng EffectManager để chạy hiệu ứng
        if (EffectManager.Instance != null)
        {
            EffectManager.Instance.PlayDissolveIn(gameObject, dissolveInDuration, () => {
                OnDissolveInComplete?.Invoke(this);
                onComplete?.Invoke();
            });
        }
        else
        {
            // Fallback nếu không có EffectManager
            dissolveInEffect.Duration = dissolveInDuration;
            dissolveInEffect.PlayEffect(() => {
                OnDissolveInComplete?.Invoke(this);
                onComplete?.Invoke();
            });
        }
        
        OnPawnSpawned?.Invoke(this);
    }
    
    /// <summary>
    /// Bắt đầu hiệu ứng dissolve out (để destroy pawn)
    /// </summary>
    /// <param name="onComplete">Callback khi hoàn thành</param>
    public void StartDissolveOut(System.Action onComplete = null)
    {
        if (!isActive) return;
        
        isActive = false;
        
        // Reverse dissolve effect - từ 1 về 0
        if (EffectManager.Instance != null)
        {
            var parameters = new System.Collections.Generic.Dictionary<string, object>
            {
                { "duration", dissolveOutDuration },
                { "dissolveamount", 0f } // Start from current and go to 0
            };
            
            var effect = EffectManager.Instance.PlayEffect<DissolveInEffect>(gameObject, parameters, () => {
                OnDissolveOutComplete?.Invoke(this);
                onComplete?.Invoke();
                DestroyPawn();
            });
            
            // Manually animate from current dissolve amount to 0
            if (effect != null)
            {
                float currentDissolve = 1f;
                DOTween.To(() => currentDissolve, x => {
                    currentDissolve = x;
                    var param = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "dissolveamount", x }
                    };
                    effect.SetParameters(param);
                }, 0f, dissolveOutDuration);
            }
        }
        else
        {
            // Fallback
            OnDissolveOutComplete?.Invoke(this);
            onComplete?.Invoke();
            DestroyPawn();
        }
    }
    
    /// <summary>
    /// Animation rơi xuống vị trí gốc
    /// </summary>
    private void DropToPosition()
    {
        transform.DOMove(originalPosition, dropDuration)
            .SetEase(dropCurve);
    }
    
    /// <summary>
    /// Di chuyển pawn đến vị trí mới
    /// </summary>
    /// <param name="newPosition">Vị trí mới</param>
    /// <param name="duration">Thời gian di chuyển</param>
    /// <param name="onComplete">Callback khi hoàn thành</param>
    public void MoveTo(Vector3 newPosition, float duration = 1f, System.Action onComplete = null)
    {
        originalPosition = newPosition;
        
        transform.DOMove(newPosition, duration)
            .SetEase(Ease.OutQuart)
            .OnComplete(() => onComplete?.Invoke());
    }
    
    /// <summary>
    /// Destroy pawn
    /// </summary>
    public void DestroyPawn()
    {
        OnPawnDestroyed?.Invoke(this);
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Reset pawn về trạng thái ban đầu
    /// </summary>
    public void ResetPawn()
    {
        if (dissolveInEffect != null)
        {
            dissolveInEffect.ResetEffect();
        }
        
        transform.position = originalPosition;
        isActive = true;
    }
    
    // Context menu methods for testing
    [ContextMenu("Start Dissolve In")]
    private void TestDissolveIn()
    {
        StartDissolveIn();
    }
    
    [ContextMenu("Start Dissolve Out")]
    private void TestDissolveOut()
    {
        StartDissolveOut();
    }
    
    [ContextMenu("Start Spawn Sequence")]
    private void TestSpawnSequence()
    {
        StartSpawnSequence();
    }
    
    [ContextMenu("Reset Pawn")]
    private void TestResetPawn()
    {
        ResetPawn();
    }
    
    private void OnDestroy()
    {
        // Stop any running tweens
        transform.DOKill();
        
        if (EffectManager.Instance != null)
        {
            EffectManager.Instance.StopAllEffects(gameObject);
        }
    }
    
    // Validation in editor
    #if UNITY_EDITOR
    private void OnValidate()
    {
        if (dissolveInDuration <= 0f)
            dissolveInDuration = 0.1f;
            
        if (dissolveOutDuration <= 0f)
            dissolveOutDuration = 0.1f;
    }
    #endif
}

// Enums
[System.Serializable]
public enum PawnType
{
    Pawn,
    Rook,
    Knight,
    Bishop,
    Queen,
    King
}

[System.Serializable]
public enum PawnTeam
{
    White,
    Black
}
