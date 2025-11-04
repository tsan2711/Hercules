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
        Debug.Log($"TriggerDissolveOut called on {gameObject.name}");
        
        SetSkinState(SkinState.Dissolving, false);
        
        // Lấy material từ ChessRaycastDebug trước khi dissolve (không cần thiết nhưng có thể dùng để reference)
        Material[] dissolveMaterials = GetDissolveMaterialsFromChessDebug();
        
        // Luôn apply dissolve materials (sẽ tạo material mới với dissolve shader từ materials hiện tại)
        // Nếu không có dissolveMaterials từ ChessRaycastDebug, vẫn sẽ tạo dissolve materials từ materials hiện tại
        ApplyDissolveMaterials(dissolveMaterials ?? new Material[0]);
        
        // Đợi một frame để materials được apply
        StartCoroutine(DelayedDissolveStart(onComplete));
    }
    
    /// <summary>
    /// Delay một frame để đảm bảo materials được apply trước khi start dissolve
    /// </summary>
    private System.Collections.IEnumerator DelayedDissolveStart(System.Action onComplete)
    {
        yield return null; // Wait one frame
        
        // Ensure dissolve effect is initialized
        if (dissolveEffect == null)
        {
            dissolveEffect = GetComponent<DissolveInEffect>();
            if (dissolveEffect == null)
            {
                dissolveEffect = gameObject.AddComponent<DissolveInEffect>();
            }
        }
        
        if (dissolveEffect != null)
        {
            // Reinitialize materials in case they changed
            dissolveEffect.ReinitializeMaterials();
            
            // Check if materials were found
            Renderer[] checkRenderers = GetComponentsInChildren<Renderer>();
            bool hasDissolveProperty = false;
            foreach (var renderer in checkRenderers)
            {
                if (renderer != null)
                {
                    Material[] mats = renderer.materials;
                    foreach (var mat in mats)
                    {
                        if (mat != null && mat.HasProperty(dissolvePropertyName))
                        {
                            hasDissolveProperty = true;
                            Debug.Log($"Found material with dissolve property: {mat.name} on {renderer.name} (Shader: {mat.shader.name})");
                            
                            // Log current dissolve amount
                            float currentAmount = mat.GetFloat(dissolvePropertyName);
                            Debug.Log($"Current dissolve amount on {mat.name}: {currentAmount}");
                            break;
                        }
                    }
                    if (hasDissolveProperty) break;
                }
            }
            
            if (!hasDissolveProperty)
            {
                Debug.LogWarning($"No materials with {dissolvePropertyName} property found on {gameObject.name}. " +
                               $"Materials may not use a dissolve shader. Using fallback DOTween method.");
                
                // Fallback: dùng DOTween trực tiếp trên materials
                float currentDissolve = GetDissolveAmount();
                Debug.Log($"Starting fallback dissolve from {currentDissolve} to 1.0 over {dissolveOutDuration} seconds");
                
                DOTween.To(() => currentDissolve, x => {
                    currentDissolve = x;
                    SetDissolveAmount(x);
                }, 1f, dissolveOutDuration).OnComplete(() => {
                    Debug.Log($"Fallback dissolve out completed on {gameObject.name}");
                    OnDissolveOutCompleted?.Invoke();
                    onComplete?.Invoke();
                });
                yield break;
            }
            
            // Reset dissolve amount về 0 trước (quân cờ hiện tại không dissolve)
            dissolveEffect.ResetEffect();
            
            // Verify dissolve amount is 0
            float verifyAmount = GetDissolveAmount();
            Debug.Log($"Dissolve amount after reset: {verifyAmount}");
            
            // Setup dissolve out effect: từ 0 (không dissolve) -> 1 (dissolve hoàn toàn)
            dissolveEffect.Duration = dissolveOutDuration;
            
            Debug.Log($"Starting dissolve out effect on {gameObject.name} with duration {dissolveOutDuration}");
            
            // Play dissolve effect (từ 0 -> 1)
            dissolveEffect.PlayEffect(() => {
                Debug.Log($"Dissolve out completed on {gameObject.name}");
                OnDissolveOutCompleted?.Invoke();
                onComplete?.Invoke();
            });
        }
        else
        {
            Debug.LogWarning($"No DissolveInEffect found on {gameObject.name}, using fallback DOTween");
            
            // Fallback: dùng DOTween trực tiếp
            float currentDissolve = GetDissolveAmount();
            DOTween.To(() => currentDissolve, x => {
                currentDissolve = x;
                SetDissolveAmount(x);
            }, 1f, dissolveOutDuration).OnComplete(() => {
                Debug.Log($"Fallback dissolve out completed on {gameObject.name}");
                OnDissolveOutCompleted?.Invoke();
                onComplete?.Invoke();
            });
        }
    }
    
    /// <summary>
    /// Lấy dissolve materials từ ChessRaycastDebug
    /// </summary>
    private Material[] GetDissolveMaterialsFromChessDebug()
    {
        if (pieceInfo == null)
        {
            Debug.LogWarning($"Cannot get dissolve materials: No ChessPieceInfo found on {gameObject.name}");
            return null;
        }
        
        ChessRaycastDebug chessDebug = FindObjectOfType<ChessRaycastDebug>();
        if (chessDebug == null)
        {
            Debug.LogWarning($"ChessRaycastDebug not found in scene");
            return null;
        }
        
        return GetMaterialsFromChessDebug(chessDebug, pieceInfo.isWhite, pieceInfo.type);
    }
    
    /// <summary>
    /// Áp dụng dissolve materials vào renderers
    /// </summary>
    private void ApplyDissolveMaterials(Material[] dissolveMats)
    {
        // Tìm dissolve shader trước
        Shader dissolveShader = Shader.Find("Custom/DissolveIn");
        if (dissolveShader == null)
        {
            dissolveShader = Shader.Find("Unlit/Pawn");
        }
        
        if (dissolveShader == null)
        {
            Debug.LogError($"Could not find dissolve shader! Dissolve effect will not work.");
            return;
        }
        
        Debug.Log($"Using dissolve shader: {dissolveShader.name}");
        
        // Reinitialize renderers if needed
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        }
        
        // Apply materials to all renderers
        foreach (var renderer in renderers)
        {
            if (renderer != null)
            {
                Material[] currentMats = renderer.materials; // Use instance materials để giữ texture
                Material[] newMaterials = new Material[currentMats.Length];
                
                for (int i = 0; i < newMaterials.Length; i++)
                {
                    Material currentMat = i < currentMats.Length ? currentMats[i] : null;
                    
                    // Tạo material mới với dissolve shader
                    Material instanceMat = new Material(dissolveShader);
                    
                    // Copy các properties từ material hiện tại để giữ texture và color
                    if (currentMat != null)
                    {
                        // Copy main texture
                        if (currentMat.HasProperty("_MainTex") && instanceMat.HasProperty("_MainTex"))
                        {
                            Texture mainTex = currentMat.GetTexture("_MainTex");
                            if (mainTex != null)
                            {
                                instanceMat.SetTexture("_MainTex", mainTex);
                            }
                        }
                        
                        // Copy color
                        if (currentMat.HasProperty("_Color") && instanceMat.HasProperty("_Color"))
                        {
                            instanceMat.SetColor("_Color", currentMat.GetColor("_Color"));
                        }
                        else if (currentMat.HasProperty("_BaseColor") && instanceMat.HasProperty("_Color"))
                        {
                            instanceMat.SetColor("_Color", currentMat.GetColor("_BaseColor"));
                        }
                    }
                    
                    // Set dissolve properties
                    instanceMat.SetFloat(dissolvePropertyName, 0f); // Start at 0 (no dissolve)
                    
                    // Set noise properties for noise-based dissolve
                    if (instanceMat.HasProperty("_NoiseScale"))
                    {
                        instanceMat.SetFloat("_NoiseScale", 1.0f);
                    }
                    if (instanceMat.HasProperty("_UseProceduralNoise"))
                    {
                        instanceMat.SetFloat("_UseProceduralNoise", 1.0f); // Use procedural noise
                    }
                    
                    // Set edge properties if available
                    if (instanceMat.HasProperty("_EdgeWidth"))
                    {
                        instanceMat.SetFloat("_EdgeWidth", 0.1f);
                    }
                    if (instanceMat.HasProperty("_EdgeIntensity"))
                    {
                        instanceMat.SetFloat("_EdgeIntensity", 2.0f);
                    }
                    if (instanceMat.HasProperty("_EdgeColor"))
                    {
                        instanceMat.SetColor("_EdgeColor", new Color(1f, 0.5f, 0f, 1f)); // Orange edge
                    }
                    
                    instanceMat.name = $"{currentMat?.name ?? "DissolveMaterial"}_Dissolve";
                    newMaterials[i] = instanceMat;
                    
                    Debug.Log($"Created dissolve material {instanceMat.name} with shader {instanceMat.shader.name}, " +
                             $"has {dissolvePropertyName}: {instanceMat.HasProperty(dissolvePropertyName)}");
                }
                
                // Force instance materials to allow property modification
                renderer.materials = newMaterials;
                Debug.Log($"Applied {newMaterials.Length} dissolve materials to {renderer.name}");
                
                // Verify materials were applied
                Material[] verifyMats = renderer.materials;
                foreach (var mat in verifyMats)
                {
                    if (mat != null && mat.HasProperty(dissolvePropertyName))
                    {
                        float dissolveAmount = mat.GetFloat(dissolvePropertyName);
                        Debug.Log($"Verified material {mat.name} on {renderer.name}: dissolve amount = {dissolveAmount}, shader = {mat.shader.name}");
                    }
                }
            }
        }
        
        // Also apply to other renderer types (MeshRenderer, etc.)
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in allRenderers)
        {
            // Skip if already processed as SkinnedMeshRenderer
            if (renderer is SkinnedMeshRenderer)
                continue;
                
            if (renderer != null)
            {
                Material[] currentMats = renderer.materials;
                Material[] newMaterials = new Material[currentMats.Length];
                
                for (int i = 0; i < newMaterials.Length; i++)
                {
                    Material currentMat = i < currentMats.Length ? currentMats[i] : null;
                    
                    // Tạo material mới với dissolve shader
                    Material instanceMat = new Material(dissolveShader);
                    
                    // Copy properties từ material hiện tại
                    if (currentMat != null)
                    {
                        if (currentMat.HasProperty("_MainTex") && instanceMat.HasProperty("_MainTex"))
                        {
                            Texture mainTex = currentMat.GetTexture("_MainTex");
                            if (mainTex != null)
                            {
                                instanceMat.SetTexture("_MainTex", mainTex);
                            }
                        }
                        if (currentMat.HasProperty("_Color") && instanceMat.HasProperty("_Color"))
                        {
                            instanceMat.SetColor("_Color", currentMat.GetColor("_Color"));
                        }
                    }
                    
                    // Set dissolve properties
                    instanceMat.SetFloat(dissolvePropertyName, 0f);
                    
                    if (instanceMat.HasProperty("_NoiseScale"))
                    {
                        instanceMat.SetFloat("_NoiseScale", 1.0f);
                    }
                    if (instanceMat.HasProperty("_UseProceduralNoise"))
                    {
                        instanceMat.SetFloat("_UseProceduralNoise", 1.0f);
                    }
                    
                    instanceMat.name = $"{currentMat?.name ?? "DissolveMaterial"}_Dissolve";
                    newMaterials[i] = instanceMat;
                }
                
                renderer.materials = newMaterials;
                Debug.Log($"Applied {newMaterials.Length} dissolve materials to {renderer.name} (non-skinned)");
            }
        }
    }
    
    /// <summary>
    /// Set dissolve amount trực tiếp
    /// </summary>
    private void SetDissolveAmount(float value)
    {
        // Reinitialize renderers if needed
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        }
        
        // Apply to all skinned mesh renderers
        foreach (var renderer in renderers)
        {
            if (renderer != null)
            {
                // Force instance materials
                Material[] materials = renderer.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null)
                    {
                        // Check if material has the property
                        if (materials[i].HasProperty(dissolvePropertyName))
                        {
                            materials[i].SetFloat(dissolvePropertyName, value);
                        }
                        else
                        {
                            // Try shared material
                            Material[] sharedMats = renderer.sharedMaterials;
                            if (i < sharedMats.Length && sharedMats[i] != null && sharedMats[i].HasProperty(dissolvePropertyName))
                            {
                                // Create instance material if needed
                                if (materials[i] == sharedMats[i])
                                {
                                    materials[i] = new Material(sharedMats[i]);
                                    renderer.materials = materials; // Update materials array
                                }
                                materials[i].SetFloat(dissolvePropertyName, value);
                            }
                        }
                    }
                }
            }
        }
        
        // Also check for other Renderer types (MeshRenderer, etc.) in case they exist
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in allRenderers)
        {
            // Skip if already processed as SkinnedMeshRenderer
            if (renderer is SkinnedMeshRenderer)
                continue;
                
            if (renderer != null)
            {
                Material[] materials = renderer.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materials[i].HasProperty(dissolvePropertyName))
                    {
                        materials[i].SetFloat(dissolvePropertyName, value);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Get dissolve amount hiện tại
    /// </summary>
    private float GetDissolveAmount()
    {
        if (renderers.Length > 0 && renderers[0] != null)
        {
            Material[] materials = renderers[0].materials;
            if (materials.Length > 0 && materials[0] != null && materials[0].HasProperty(dissolvePropertyName))
            {
                return materials[0].GetFloat(dissolvePropertyName);
            }
        }
        return 0f;
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
