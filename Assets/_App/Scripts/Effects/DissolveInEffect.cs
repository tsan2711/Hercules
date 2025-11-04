using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Hiệu ứng Dissolve In cho các object sử dụng DissolveIn shader
/// </summary>
[System.Serializable]
public class DissolveInEffect : MonoBehaviour, IEffect
{
    [Header("Dissolve Settings")]
    [SerializeField] private float duration = 3f;
    [SerializeField] private AnimationCurve dissolveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool playOnStart = false;
    [SerializeField] private bool resetOnPlay = true;
    
    [Header("Shader Properties")]
    [SerializeField] private string dissolvePropertyName = "_DissolveAmount";
    [SerializeField] private string edgeWidthPropertyName = "_DissolveEdgeWidth";
    [SerializeField] private string edgeIntensityPropertyName = "_DissolveEdgeIntensity";
    [SerializeField] private string edgeColorPropertyName = "_DissolveEdgeColor";
    
    // Private fields
    private Material[] materials;
    private Renderer[] renderers;
    private bool isPlaying = false;
    private Tween dissolveTween;
    private System.Action onCompleteCallback;
    
    // Properties from IEffect
    public string EffectName => "Dissolve In";
    public float Duration { get => duration; set => duration = value; }
    public bool IsPlaying => isPlaying;
    public GameObject Target { get; set; }
    
    private void Awake()
    {
        if (Target == null)
            Target = gameObject;
            
        InitializeMaterials();
    }
    
    private void Start()
    {
        if (playOnStart)
        {
            PlayEffect();
        }
    }
    
    private void InitializeMaterials()
    {
        if (Target == null)
            Target = gameObject;
            
        renderers = Target.GetComponentsInChildren<Renderer>();
        
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning($"No Renderers found on {Target.name}");
            materials = new Material[0];
            return;
        }
        
        List<Material> materialList = new List<Material>();
        
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            
            // Use sharedMaterials first to check, then get instance materials
            Material[] sharedMats = renderer.sharedMaterials;
            Material[] instanceMats = renderer.materials; // Force instance materials
            
            for (int i = 0; i < instanceMats.Length; i++)
            {
                Material mat = instanceMats[i];
                if (mat != null)
                {
                    // Check if material has dissolve property
                    if (mat.HasProperty(dissolvePropertyName))
                    {
                        materialList.Add(mat);
                    }
                    // Also check shared material
                    else if (i < sharedMats.Length && sharedMats[i] != null && sharedMats[i].HasProperty(dissolvePropertyName))
                    {
                        // Material instance should also have the property
                        materialList.Add(mat);
                    }
                }
            }
        }
        
        materials = materialList.ToArray();
        
        if (materials.Length == 0)
        {
            Debug.LogWarning($"No materials with {dissolvePropertyName} property found on {Target.name}. " +
                           $"Make sure materials use a shader with dissolve support (e.g., Custom/DissolveIn or Unlit/Pawn)");
        }
        else
        {
            Debug.Log($"DissolveInEffect initialized on {Target.name} with {materials.Length} materials");
        }
    }
    
    /// <summary>
    /// Reinitialize materials (useful when materials change at runtime)
    /// </summary>
    public void ReinitializeMaterials()
    {
        InitializeMaterials();
    }
    
    public void PlayEffect(System.Action onComplete = null)
    {
        if (isPlaying)
        {
            StopEffect();
        }
        
        // Reinitialize materials in case they changed
        if (materials == null || materials.Length == 0)
        {
            InitializeMaterials();
        }
        
        if (materials == null || materials.Length == 0)
        {
            Debug.LogWarning($"Cannot play dissolve effect on {Target.name}: No materials with dissolve property found!");
            Debug.LogWarning($"Make sure the materials use a shader with {dissolvePropertyName} property (e.g., Custom/DissolveIn or Unlit/Pawn)");
            onComplete?.Invoke();
            return;
        }
        
        Debug.Log($"DissolveInEffect.PlayEffect: Found {materials.Length} materials with dissolve property on {Target.name}");
        
        onCompleteCallback = onComplete;
        
        if (resetOnPlay)
        {
            ResetEffect();
        }
        
        isPlaying = true;
        
        float startValue = GetDissolveAmount();
        Debug.Log($"Playing dissolve effect on {Target.name} from {startValue} to 1.0 over {duration} seconds");
        
        // Track last logged value
        float lastLoggedValue = -1f;
        
        // Animate dissolve amount from 0 to 1
        dissolveTween = DOTween.To(
            () => GetDissolveAmount(),
            (value) => {
                SetDissolveAmount(value);
                
                // Debug log every 0.2 progress (0.0, 0.2, 0.4, 0.6, 0.8, 1.0)
                float progressStep = Mathf.Floor(value * 5f) / 5f;
                if (progressStep != lastLoggedValue)
                {
                    lastLoggedValue = progressStep;
                    Debug.Log($"Dissolve progress: {value:F3} ({progressStep * 100:F0}%) on {Target.name}");
                    
                    // Verify dissolve amount was actually set
                    float verifyValue = GetDissolveAmount();
                    if (Mathf.Abs(verifyValue - value) > 0.01f)
                    {
                        Debug.LogWarning($"Dissolve amount mismatch! Expected: {value:F3}, Actual: {verifyValue:F3}");
                    }
                }
            },
            1f,
            duration
        ).SetEase(dissolveCurve)
        .OnComplete(() => {
            isPlaying = false;
            
            // Verify final dissolve amount
            float finalAmount = GetDissolveAmount();
            Debug.Log($"Dissolve effect completed on {Target.name}. Final dissolve amount: {finalAmount}");
            
            onCompleteCallback?.Invoke();
        });
    }
    
    public void StopEffect()
    {
        if (dissolveTween != null)
        {
            dissolveTween.Kill();
            dissolveTween = null;
        }
        
        isPlaying = false;
    }
    
    public void ResetEffect()
    {
        StopEffect();
        SetDissolveAmount(0f);
    }
    
    public void SetParameters(Dictionary<string, object> parameters)
    {
        foreach (var param in parameters)
        {
            switch (param.Key.ToLower())
            {
                case "duration":
                    if (param.Value is float durationValue)
                        Duration = durationValue;
                    break;
                    
                case "dissolveamount":
                    if (param.Value is float dissolveValue)
                        SetDissolveAmount(dissolveValue);
                    break;
                    
                case "edgewidth":
                    if (param.Value is float edgeWidthValue)
                        SetEdgeWidth(edgeWidthValue);
                    break;
                    
                case "edgeintensity":
                    if (param.Value is float edgeIntensityValue)
                        SetEdgeIntensity(edgeIntensityValue);
                    break;
                    
                case "edgecolor":
                    if (param.Value is Color edgeColorValue)
                        SetEdgeColor(edgeColorValue);
                    break;
                    
                case "playonstart":
                    if (param.Value is bool playOnStartValue)
                        playOnStart = playOnStartValue;
                    break;
                    
                case "resetonplay":
                    if (param.Value is bool resetOnPlayValue)
                        resetOnPlay = resetOnPlayValue;
                    break;
            }
        }
    }
    
    // Shader property setters/getters
    private void SetDissolveAmount(float value)
    {
        // If materials array is empty, try to reinitialize
        if (materials == null || materials.Length == 0)
        {
            InitializeMaterials();
        }
        
        if (materials == null || materials.Length == 0)
        {
            // Fallback: try to set on all renderers directly
            if (renderers != null)
            {
                foreach (var renderer in renderers)
                {
                    if (renderer != null)
                    {
                        Material[] rendererMats = renderer.materials;
                        foreach (var mat in rendererMats)
                        {
                            if (mat != null && mat.HasProperty(dissolvePropertyName))
                            {
                                mat.SetFloat(dissolvePropertyName, value);
                            }
                        }
                    }
                }
            }
            return;
        }
        
        foreach (var material in materials)
        {
            if (material != null)
            {
                if (material.HasProperty(dissolvePropertyName))
                {
                    material.SetFloat(dissolvePropertyName, value);
                }
            }
        }
    }
    
    private float GetDissolveAmount()
    {
        // If materials array is empty, try to reinitialize
        if (materials == null || materials.Length == 0)
        {
            InitializeMaterials();
        }
        
        if (materials != null && materials.Length > 0 && materials[0] != null)
        {
            if (materials[0].HasProperty(dissolvePropertyName))
            {
                return materials[0].GetFloat(dissolvePropertyName);
            }
        }
        
        // Fallback: try to get from renderers directly
        if (renderers != null && renderers.Length > 0)
        {
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    Material[] rendererMats = renderer.materials;
                    if (rendererMats.Length > 0 && rendererMats[0] != null && rendererMats[0].HasProperty(dissolvePropertyName))
                    {
                        return rendererMats[0].GetFloat(dissolvePropertyName);
                    }
                }
            }
        }
        
        return 0f;
    }
    
    private void SetEdgeWidth(float value)
    {
        foreach (var material in materials)
        {
            if (material != null && material.HasProperty(edgeWidthPropertyName))
            {
                material.SetFloat(edgeWidthPropertyName, value);
            }
        }
    }
    
    private void SetEdgeIntensity(float value)
    {
        foreach (var material in materials)
        {
            if (material != null && material.HasProperty(edgeIntensityPropertyName))
            {
                material.SetFloat(edgeIntensityPropertyName, value);
            }
        }
    }
    
    private void SetEdgeColor(Color value)
    {
        foreach (var material in materials)
        {
            if (material != null && material.HasProperty(edgeColorPropertyName))
            {
                material.SetColor(edgeColorPropertyName, value);
            }
        }
    }
    
    // Public methods for manual control
    [ContextMenu("Play Effect")]
    public void PlayEffectEditor()
    {
        PlayEffect();
    }
    
    [ContextMenu("Stop Effect")]
    public void StopEffectEditor()
    {
        StopEffect();
    }
    
    [ContextMenu("Reset Effect")]
    public void ResetEffectEditor()
    {
        ResetEffect();
    }
    
    private void OnDestroy()
    {
        StopEffect();
    }
    
    #if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying && materials != null)
        {
            // Update shader properties in real-time during development
            SetDissolveAmount(GetDissolveAmount());
        }
    }
    #endif
}
