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
    [SerializeField] private string edgeWidthPropertyName = "_EdgeWidth";
    [SerializeField] private string edgeIntensityPropertyName = "_EdgeIntensity";
    
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
        renderers = Target.GetComponentsInChildren<Renderer>();
        List<Material> materialList = new List<Material>();
        
        foreach (var renderer in renderers)
        {
            foreach (var material in renderer.materials)
            {
                if (material.HasProperty(dissolvePropertyName))
                {
                    materialList.Add(material);
                }
            }
        }
        
        materials = materialList.ToArray();
        
        if (materials.Length == 0)
        {
            Debug.LogWarning($"No materials with {dissolvePropertyName} property found on {Target.name}");
        }
    }
    
    public void PlayEffect(System.Action onComplete = null)
    {
        if (isPlaying)
        {
            StopEffect();
        }
        
        onCompleteCallback = onComplete;
        
        if (resetOnPlay)
        {
            ResetEffect();
        }
        
        isPlaying = true;
        
        // Animate dissolve amount from 0 to 1
        dissolveTween = DOTween.To(
            () => GetDissolveAmount(),
            (value) => SetDissolveAmount(value),
            1f,
            duration
        ).SetEase(dissolveCurve)
        .OnComplete(() => {
            isPlaying = false;
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
        foreach (var material in materials)
        {
            if (material != null)
            {
                material.SetFloat(dissolvePropertyName, value);
            }
        }
    }
    
    private float GetDissolveAmount()
    {
        if (materials.Length > 0 && materials[0] != null)
        {
            return materials[0].GetFloat(dissolvePropertyName);
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
