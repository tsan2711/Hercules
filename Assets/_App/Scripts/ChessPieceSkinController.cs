using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

// Enums and Structs - Defined outside class for global access
[System.Serializable]
public enum SkinState
{
    Normal,      // Trạng thái bình thường
    Hover,       // Khi hover chuột
    Selected,    // Khi được select
    Moving,      // Khi đang di chuyển
    Attacking,   // Khi đang tấn công
    Dissolving   // Khi đang dissolve (spawn/destroy)
}

[System.Serializable]
public struct MaterialSet
{
    [SerializeField] public Material[] materials;
    [SerializeField] public string description;
    
    public MaterialSet(Material[] mats, string desc = "")
    {
        materials = mats;
        description = desc;
    }
}

/// <summary>
/// Quản lý tất cả material states của chess pieces
/// Thay thế hardcode material switching, hỗ trợ hover, select, attack, dissolve states
/// </summary>
public class ChessPieceSkinController : MonoBehaviour
{
    [Header("Material Settings")]
    [SerializeField] private MaterialSet normalMaterials;
    [SerializeField] private MaterialSet hoverMaterials;
    [SerializeField] private MaterialSet selectedMaterials;
    [SerializeField] private MaterialSet movingMaterials;
    [SerializeField] private MaterialSet attackingMaterials;
    [SerializeField] private MaterialSet dissolveMaterials;
    
    [Header("Transition Settings")]
    [SerializeField] private float transitionDuration = 0.3f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Dissolve Settings")]
    [SerializeField] private float dissolveInDuration = 2f;
    [SerializeField] private float dissolveOutDuration = 1.5f;
    [SerializeField] private string dissolvePropertyName = "_DissolveAmount";
    [SerializeField] private string edgeWidthPropertyName = "_EdgeWidth";
    [SerializeField] private string edgeIntensityPropertyName = "_EdgeIntensity";
    
    [Header("Auto Setup")]
    [SerializeField] private bool autoSetupFromPieceInfo = true;
    [SerializeField] private bool debugMode = false;
    
    // Private fields
    private ChessPieceInfo pieceInfo;
    private SkinnedMeshRenderer[] renderers;
    private Material[][] originalMaterials;
    private SkinState currentState = SkinState.Normal;
    private SkinState previousState = SkinState.Normal;
    private Coroutine transitionCoroutine;
    private DissolveInEffect dissolveEffect;
    
    // Events
    public System.Action<SkinState, SkinState> OnSkinStateChanged;
    public System.Action OnDissolveInCompleted;
    public System.Action OnDissolveOutCompleted;
    
    // Properties
    public SkinState CurrentState => currentState;
    public bool IsTransitioning => transitionCoroutine != null;
    public bool IsDissolving => dissolveEffect != null && dissolveEffect.IsPlaying;
    
    private void Awake()
    {
        Initialize();
    }
    
    private void Start()
    {
        if (autoSetupFromPieceInfo)
        {
            AutoSetupMaterials();
        }
        
        // Set initial state
        SetSkinState(SkinState.Normal, false);
    }
    
    private void Initialize()
    {
        pieceInfo = GetComponent<ChessPieceInfo>();
        renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"No SkinnedMeshRenderer found on {gameObject.name}");
            return;
        }
        
        // Store original materials
        StoreOriginalMaterials();
        
        // Setup dissolve effect if needed
        dissolveEffect = GetComponent<DissolveInEffect>();
        if (dissolveEffect == null)
        {
            dissolveEffect = gameObject.AddComponent<DissolveInEffect>();
        }
        
        if (debugMode)
        {
            Debug.Log($"ChessPieceSkinController initialized on {gameObject.name} with {renderers.Length} renderers");
        }
    }
    
    /// <summary>
    /// Lưu trữ materials gốc
    /// </summary>
    private void StoreOriginalMaterials()
    {
        originalMaterials = new Material[renderers.Length][];
        
        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterials[i] = new Material[renderers[i].sharedMaterials.Length];
            for (int j = 0; j < renderers[i].sharedMaterials.Length; j++)
            {
                originalMaterials[i][j] = renderers[i].sharedMaterials[j];
            }
        }
    }
    
    /// <summary>
    /// Tự động thiết lập materials dựa trên ChessPieceInfo
    /// </summary>
    private void AutoSetupMaterials()
    {
        if (pieceInfo == null)
        {
            Debug.LogWarning($"Cannot auto setup materials: No ChessPieceInfo found on {gameObject.name}");
            return;
        }
        
        // Tìm ChessRaycastDebug để lấy material settings
        ChessRaycastDebug chessDebug = FindObjectOfType<ChessRaycastDebug>();
        if (chessDebug != null)
        {
            // Lấy materials từ ChessRaycastDebug.chessMaterials
            Material[] foundMaterials = GetMaterialsFromChessDebug(chessDebug, pieceInfo.isWhite, pieceInfo.type);
            if (foundMaterials != null)
            {
                // Setup normal materials
                if (normalMaterials.materials == null || normalMaterials.materials.Length == 0)
                {
                    normalMaterials.materials = originalMaterials[0]; // Use original as normal
                }
                
                // Setup hover materials (slightly brighter)
                if (hoverMaterials.materials == null || hoverMaterials.materials.Length == 0)
                {
                    hoverMaterials.materials = foundMaterials;
                }
                
                // Setup selected materials (more saturated)
                if (selectedMaterials.materials == null || selectedMaterials.materials.Length == 0)
                {
                    selectedMaterials.materials = foundMaterials;
                }
            }
        }
        
        if (debugMode)
        {
            Debug.Log($"Auto setup completed for {pieceInfo.type} {(pieceInfo.isWhite ? "White" : "Black")}");
        }
    }
    
    /// <summary>
    /// Lấy materials từ ChessRaycastDebug
    /// </summary>
    private Material[] GetMaterialsFromChessDebug(ChessRaycastDebug chessDebug, bool isWhite, ChessRaycastDebug.ChessType type)
    {
        if (chessDebug.chessMaterials == null) return null;
        
        foreach (var chessMaterial in chessDebug.chessMaterials)
        {
            if (chessMaterial.isWhite == isWhite && chessMaterial.type == type)
            {
                return chessMaterial.materials;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Thiết lập skin state với hoặc không có transition
    /// </summary>
    /// <param name="newState">State mới</param>
    /// <param name="animated">Có animation transition không</param>
    public void SetSkinState(SkinState newState, bool animated = true)
    {
        if (currentState == newState) return;
        
        previousState = currentState;
        currentState = newState;
        
        OnSkinStateChanged?.Invoke(previousState, currentState);
        
        if (debugMode)
        {
            Debug.Log($"Skin state changed: {previousState} -> {currentState} on {gameObject.name}");
        }
        
        // Chỉ start coroutine nếu GameObject active
        if (animated && transitionDuration > 0f && gameObject.activeInHierarchy)
        {
            StartSkinTransition();
        }
        else
        {
            ApplySkinStateImmediate();
        }
    }
    
    /// <summary>
    /// Set skin state ngay lập tức mà không cần coroutine
    /// </summary>
    public void SetSkinStateImmediate(SkinState newState)
    {
        if (newState == currentState) return;
        
        SkinState previousState = currentState;
        currentState = newState;
        
        OnSkinStateChanged?.Invoke(previousState, currentState);
        
        if (debugMode)
        {
            Debug.Log($"Skin state changed immediately: {previousState} -> {currentState} on {gameObject.name}");
        }
        
        ApplySkinStateImmediate();
    }
    
    /// <summary>
    /// Áp dụng skin state ngay lập tức
    /// </summary>
    private void ApplySkinStateImmediate()
    {
        MaterialSet targetMaterialSet = GetMaterialSetForState(currentState);
        ApplyMaterialSet(targetMaterialSet);
    }
    
    /// <summary>
    /// Bắt đầu transition animation giữa các skin states
    /// </summary>
    private void StartSkinTransition()
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }
        
        transitionCoroutine = StartCoroutine(TransitionToState());
    }
    
    /// <summary>
    /// Coroutine thực hiện transition animation
    /// </summary>
    private System.Collections.IEnumerator TransitionToState()
    {
        MaterialSet targetMaterialSet = GetMaterialSetForState(currentState);
        
        if (targetMaterialSet.materials != null && targetMaterialSet.materials.Length > 0)
        {
            float elapsed = 0f;
            
            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / transitionDuration;
                float curveValue = transitionCurve.Evaluate(progress);
                
                // Smooth transition between materials (if needed)
                // For now, just apply the target material set
                if (progress >= 0.5f)
                {
                    ApplyMaterialSet(targetMaterialSet);
                    break;
                }
                
                yield return null;
            }
            
            ApplyMaterialSet(targetMaterialSet);
        }
        
        transitionCoroutine = null;
    }
    
    /// <summary>
    /// Lấy MaterialSet cho state cụ thể
    /// </summary>
    private MaterialSet GetMaterialSetForState(SkinState state)
    {
        switch (state)
        {
            case SkinState.Normal:
                return normalMaterials;
            case SkinState.Hover:
                return hoverMaterials;
            case SkinState.Selected:
                return selectedMaterials;
            case SkinState.Moving:
                return movingMaterials;
            case SkinState.Attacking:
                return attackingMaterials;
            case SkinState.Dissolving:
                return dissolveMaterials;
            default:
                return normalMaterials;
        }
    }
    
    /// <summary>
    /// Áp dụng MaterialSet lên tất cả renderers
    /// </summary>
    private void ApplyMaterialSet(MaterialSet materialSet)
    {
        if (materialSet.materials == null || materialSet.materials.Length == 0)
        {
            // Use original materials as fallback
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterials = originalMaterials[i];
            }
            return;
        }
        
        // Apply materials to all renderers
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] newMaterials = new Material[renderers[i].sharedMaterials.Length];
            
            for (int j = 0; j < newMaterials.Length; j++)
            {
                // Use materials from set, repeat last material if not enough
                int materialIndex = Mathf.Min(j, materialSet.materials.Length - 1);
                newMaterials[j] = materialSet.materials[materialIndex];
            }
            
            renderers[i].sharedMaterials = newMaterials;
        }
    }
    
    /// <summary>
    /// Trigger dissolve in effect
    /// </summary>
    /// <param name="onComplete">Callback khi hoàn thành</param>
    public void TriggerDissolveIn(System.Action onComplete = null)
    {
        SetSkinState(SkinState.Dissolving, false);
        
        if (dissolveEffect != null)
        {
            dissolveEffect.Duration = dissolveInDuration;
            dissolveEffect.PlayEffect(() => {
                OnDissolveInCompleted?.Invoke();
                onComplete?.Invoke();
            });
        }
        else
        {
            // Fallback
            onComplete?.Invoke();
        }
    }
    
    /// <summary>
    /// Trigger dissolve out effect (để destroy)
    /// </summary>
    /// <param name="onComplete">Callback khi hoàn thành</param>
    public void TriggerDissolveOut(System.Action onComplete = null)
    {
        SetSkinState(SkinState.Dissolving, false);
        
        if (dissolveEffect != null)
        {
            // Setup reverse dissolve effect
            var parameters = new Dictionary<string, object>
            {
                { "duration", dissolveOutDuration },
                { "dissolveamount", 0f }
            };
            
            dissolveEffect.SetParameters(parameters);
            
            // Animate from 1 to 0
            float currentDissolve = 1f;
            DOTween.To(() => currentDissolve, x => {
                currentDissolve = x;
                SetDissolveAmount(x);
            }, 0f, dissolveOutDuration).OnComplete(() => {
                OnDissolveOutCompleted?.Invoke();
                onComplete?.Invoke();
            });
        }
        else
        {
            // Fallback
            onComplete?.Invoke();
        }
    }
    
    /// <summary>
    /// Set dissolve amount trực tiếp
    /// </summary>
    private void SetDissolveAmount(float value)
    {
        foreach (var rendererGroup in originalMaterials)
        {
            foreach (var material in rendererGroup)
            {
                if (material != null && material.HasProperty(dissolvePropertyName))
                {
                    material.SetFloat(dissolvePropertyName, value);
                }
            }
        }
    }
    
    /// <summary>
    /// Reset về state normal
    /// </summary>
    public void ResetToNormal()
    {
        SetSkinState(SkinState.Normal, true);
        
        if (dissolveEffect != null)
        {
            dissolveEffect.ResetEffect();
        }
    }
    
    /// <summary>
    /// Thiết lập custom material set cho state cụ thể
    /// </summary>
    public void SetCustomMaterialSet(SkinState state, Material[] materials)
    {
        MaterialSet targetSet = GetMaterialSetForState(state);
        targetSet.materials = materials;
        
        if (currentState == state)
        {
            ApplyMaterialSet(targetSet);
        }
    }
    
    /// <summary>
    /// Lấy materials hiện tại
    /// </summary>
    public Material[] GetCurrentMaterials()
    {
        if (renderers.Length > 0)
        {
            return renderers[0].sharedMaterials;
        }
        return null;
    }
    
    /// <summary>
    /// Validation cho Editor
    /// </summary>
    private void OnValidate()
    {
        if (transitionDuration < 0f)
            transitionDuration = 0f;
            
        if (dissolveInDuration <= 0f)
            dissolveInDuration = 0.1f;
            
        if (dissolveOutDuration <= 0f)
            dissolveOutDuration = 0.1f;
    }
    
    private void OnDestroy()
    {
        // Stop any running transitions
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }
        
        // Stop DOTween animations
        transform.DOKill();
    }
    
    // Context menu for testing
    [ContextMenu("Test Hover State")]
    private void TestHover()
    {
        SetSkinState(SkinState.Hover);
    }
    
    [ContextMenu("Test Selected State")]
    private void TestSelected()
    {
        SetSkinState(SkinState.Selected);
    }
    
    [ContextMenu("Test Moving State")]
    private void TestMoving()
    {
        SetSkinState(SkinState.Moving);
    }
    
    [ContextMenu("Test Attacking State")]
    private void TestAttacking()
    {
        SetSkinState(SkinState.Attacking);
    }
    
    [ContextMenu("Test Dissolve In")]
    private void TestDissolveIn()
    {
        TriggerDissolveIn();
    }
    
    [ContextMenu("Test Dissolve Out")]
    private void TestDissolveOut()
    {
        TriggerDissolveOut();
    }
    
    [ContextMenu("Reset To Normal")]
    private void TestResetToNormal()
    {
        ResetToNormal();
    }
}
