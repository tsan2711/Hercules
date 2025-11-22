using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Quản lý VFX effects cho chess pieces
/// Spawn và despawn VFX một cách có tổ chức
/// </summary>
public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance;
    
    [Header("VFX Settings")]
    [SerializeField] private float defaultVFXDuration = 3f;
    [SerializeField] private bool autoCleanup = true;
    [SerializeField] private int maxVFXInstances = 50;
    
    [Header("VFX Prefabs")]
    [SerializeField] private GameObject moveVFXPrefab;
    [SerializeField] private GameObject attackVFXPrefab;
    [SerializeField] private GameObject teleportVFXPrefab;
    [SerializeField] private GameObject spawnVFXPrefab;
    [SerializeField] private GameObject destroyVFXPrefab;
    [SerializeField] private GameObject hitVFXPrefab;
    [SerializeField] private GameObject missVFXPrefab;
    
    [Header("VFX Timing")]
    [SerializeField] private float moveVFXDuration = 2f;
    [SerializeField] private float attackVFXDuration = 1.5f;
    [SerializeField] private float teleportVFXDuration = 3f;
    [SerializeField] private float spawnVFXDuration = 2.5f;
    [SerializeField] private float destroyVFXDuration = 2f;
    [SerializeField] private float hitVFXDuration = 1f;
    [SerializeField] private float missVFXDuration = 0.8f;
    
    // Private fields
    private List<VFXInstance> activeVFXInstances = new List<VFXInstance>();
    private Dictionary<VFXType, GameObject> vfxPrefabs = new Dictionary<VFXType, GameObject>();
    private Dictionary<VFXType, float> vfxDurations = new Dictionary<VFXType, float>();
    
    // Events
    public System.Action<VFXInstance> OnVFXSpawned;
    public System.Action<VFXInstance> OnVFXDespawned;
    public System.Action<VFXType, Vector3> OnVFXRequested;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeVFXSystem();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void InitializeVFXSystem()
    {
        // Setup VFX prefabs dictionary
        vfxPrefabs[VFXType.Move] = moveVFXPrefab;
        vfxPrefabs[VFXType.Attack] = attackVFXPrefab;
        vfxPrefabs[VFXType.Teleport] = teleportVFXPrefab;
        vfxPrefabs[VFXType.Spawn] = spawnVFXPrefab;
        vfxPrefabs[VFXType.Destroy] = destroyVFXPrefab;
        vfxPrefabs[VFXType.Hit] = hitVFXPrefab;
        vfxPrefabs[VFXType.Miss] = missVFXPrefab;
        
        // Setup VFX durations dictionary
        vfxDurations[VFXType.Move] = moveVFXDuration;
        vfxDurations[VFXType.Attack] = attackVFXDuration;
        vfxDurations[VFXType.Teleport] = teleportVFXDuration;
        vfxDurations[VFXType.Spawn] = spawnVFXDuration;
        vfxDurations[VFXType.Destroy] = destroyVFXDuration;
        vfxDurations[VFXType.Hit] = hitVFXDuration;
        vfxDurations[VFXType.Miss] = missVFXDuration;
    }
    
    /// <summary>
    /// Spawn VFX tại vị trí cụ thể
    /// </summary>
    /// <param name="vfxType">Loại VFX</param>
    /// <param name="position">Vị trí spawn</param>
    /// <param name="rotation">Rotation (optional)</param>
    /// <param name="duration">Thời gian tồn tại (optional, dùng default nếu null)</param>
    /// <param name="onComplete">Callback khi VFX hoàn thành</param>
    /// <returns>VFXInstance để có thể control sau này</returns>
    public VFXInstance SpawnVFX(VFXType vfxType, Vector3 position, Quaternion? rotation = null, float? duration = null, System.Action onComplete = null)
    {
        GameObject prefab = GetVFXPrefab(vfxType);
        if (prefab == null)
        {
            Debug.LogWarning($"VFX prefab for {vfxType} is not assigned!");
            onComplete?.Invoke();
            return null;
        }
        
        // Check max instances limit
        if (activeVFXInstances.Count >= maxVFXInstances)
        {
            CleanupOldestVFX();
        }
        
        // Spawn VFX
        Quaternion spawnRotation = rotation ?? Quaternion.identity;
        GameObject vfxObject = Instantiate(prefab, position, spawnRotation);
        
        // Create VFX instance
        VFXInstance vfxInstance = new VFXInstance
        {
            gameObject = vfxObject,
            vfxType = vfxType,
            spawnTime = Time.time,
            duration = duration ?? GetVFXDuration(vfxType),
            onCompleteCallback = onComplete,
            isActive = true
        };
        
        // Add to active list
        activeVFXInstances.Add(vfxInstance);
        
        // Setup auto cleanup if enabled
        if (autoCleanup)
        {
            StartCoroutine(AutoDespawnVFX(vfxInstance));
        }
        
        // Trigger events
        OnVFXSpawned?.Invoke(vfxInstance);
        OnVFXRequested?.Invoke(vfxType, position);
        
        return vfxInstance;
    }
    
    /// <summary>
    /// Despawn VFX instance cụ thể
    /// </summary>
    /// <param name="vfxInstance">VFX instance cần despawn</param>
    /// <param name="immediate">Despawn ngay lập tức hay có animation</param>
    public void DespawnVFX(VFXInstance vfxInstance, bool immediate = false)
    {
        if (vfxInstance == null || !vfxInstance.isActive) return;
        
        vfxInstance.isActive = false;
        
        if (immediate)
        {
            DestroyVFXObject(vfxInstance);
        }
        else
        {
            StartCoroutine(FadeOutVFX(vfxInstance));
        }
    }
    
    /// <summary>
    /// Despawn tất cả VFX của một loại
    /// </summary>
    /// <param name="vfxType">Loại VFX cần despawn</param>
    /// <param name="immediate">Despawn ngay lập tức</param>
    public void DespawnAllVFXOfType(VFXType vfxType, bool immediate = false)
    {
        List<VFXInstance> toDespawn = new List<VFXInstance>();
        
        foreach (var vfx in activeVFXInstances)
        {
            if (vfx.vfxType == vfxType && vfx.isActive)
            {
                toDespawn.Add(vfx);
            }
        }
        
        foreach (var vfx in toDespawn)
        {
            DespawnVFX(vfx, immediate);
        }
    }
    
    /// <summary>
    /// Despawn tất cả VFX
    /// </summary>
    /// <param name="immediate">Despawn ngay lập tức</param>
    public void DespawnAllVFX(bool immediate = false)
    {
        List<VFXInstance> toDespawn = new List<VFXInstance>(activeVFXInstances);
        
        foreach (var vfx in toDespawn)
        {
            DespawnVFX(vfx, immediate);
        }
    }
    
    /// <summary>
    /// Despawn VFX tại vị trí cụ thể
    /// </summary>
    /// <param name="position">Vị trí</param>
    /// <param name="radius">Bán kính tìm kiếm</param>
    /// <param name="immediate">Despawn ngay lập tức</param>
    public void DespawnVFXAtPosition(Vector3 position, float radius = 1f, bool immediate = false)
    {
        List<VFXInstance> toDespawn = new List<VFXInstance>();
        
        foreach (var vfx in activeVFXInstances)
        {
            if (vfx.isActive && Vector3.Distance(vfx.gameObject.transform.position, position) <= radius)
            {
                toDespawn.Add(vfx);
            }
        }
        
        foreach (var vfx in toDespawn)
        {
            DespawnVFX(vfx, immediate);
        }
    }
    
    /// <summary>
    /// Lấy VFX prefab theo type
    /// </summary>
    private GameObject GetVFXPrefab(VFXType vfxType)
    {
        return vfxPrefabs.TryGetValue(vfxType, out GameObject prefab) ? prefab : null;
    }
    
    /// <summary>
    /// Lấy duration của VFX theo type
    /// </summary>
    private float GetVFXDuration(VFXType vfxType)
    {
        return vfxDurations.TryGetValue(vfxType, out float duration) ? duration : defaultVFXDuration;
    }
    
    /// <summary>
    /// Auto despawn VFX sau thời gian duration
    /// </summary>
    private IEnumerator AutoDespawnVFX(VFXInstance vfxInstance)
    {
        yield return new WaitForSeconds(vfxInstance.duration);
        
        if (vfxInstance.isActive)
        {
            DespawnVFX(vfxInstance, false);
        }
    }
    
    /// <summary>
    /// Fade out animation cho VFX
    /// </summary>
    private IEnumerator FadeOutVFX(VFXInstance vfxInstance)
    {
        GameObject vfxObject = vfxInstance.gameObject;
        
        // Try to fade out using DOTween
        bool hasFadeOut = false;
        
        // Check for ParticleSystem
        ParticleSystem[] particleSystems = vfxObject.GetComponentsInChildren<ParticleSystem>();
        if (particleSystems.Length > 0)
        {
            foreach (var ps in particleSystems)
            {
                var main = ps.main;
                main.loop = false;
                ps.Stop();
            }
            hasFadeOut = true;
        }
        
        // Check for Renderer with alpha
        Renderer[] renderers = vfxObject.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            Material[] materials = renderer.materials;
            foreach (var material in materials)
            {
                if (material.HasProperty("_Color"))
                {
                    Color color = material.color;
                    DOTween.To(() => color.a, x => {
                        color.a = x;
                        material.color = color;
                    }, 0f, 0.5f);
                    hasFadeOut = true;
                }
            }
        }
        
        // Wait for fade out or minimum time
        float fadeTime = hasFadeOut ? 0.5f : 0.1f;
        yield return new WaitForSeconds(fadeTime);
        
        DestroyVFXObject(vfxInstance);
    }
    
    /// <summary>
    /// Destroy VFX object và cleanup
    /// </summary>
    private void DestroyVFXObject(VFXInstance vfxInstance)
    {
        if (vfxInstance.gameObject != null)
        {
            Destroy(vfxInstance.gameObject);
        }
        
        // Remove from active list
        activeVFXInstances.Remove(vfxInstance);
        
        // Trigger callback
        vfxInstance.onCompleteCallback?.Invoke();
        
        // Trigger event
        OnVFXDespawned?.Invoke(vfxInstance);
    }
    
    /// <summary>
    /// Cleanup VFX instance cũ nhất
    /// </summary>
    private void CleanupOldestVFX()
    {
        VFXInstance oldest = null;
        float oldestTime = float.MaxValue;
        
        foreach (var vfx in activeVFXInstances)
        {
            if (vfx.spawnTime < oldestTime)
            {
                oldestTime = vfx.spawnTime;
                oldest = vfx;
            }
        }
        
        if (oldest != null)
        {
            DespawnVFX(oldest, true);
        }
    }
    
    /// <summary>
    /// Lấy số lượng VFX đang active
    /// </summary>
    public int GetActiveVFXCount()
    {
        return activeVFXInstances.Count;
    }
    
    /// <summary>
    /// Lấy số lượng VFX của một loại cụ thể
    /// </summary>
    public int GetActiveVFXCount(VFXType vfxType)
    {
        int count = 0;
        foreach (var vfx in activeVFXInstances)
        {
            if (vfx.vfxType == vfxType && vfx.isActive)
            {
                count++;
            }
        }
        return count;
    }
    
    /// <summary>
    /// Kiểm tra có VFX nào đang active không
    /// </summary>
    public bool HasActiveVFX(VFXType vfxType)
    {
        foreach (var vfx in activeVFXInstances)
        {
            if (vfx.vfxType == vfxType && vfx.isActive)
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Pause tất cả VFX
    /// </summary>
    public void PauseAllVFX()
    {
        foreach (var vfx in activeVFXInstances)
        {
            if (vfx.isActive && vfx.gameObject != null)
            {
                ParticleSystem[] particleSystems = vfx.gameObject.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in particleSystems)
                {
                    ps.Pause();
                }
            }
        }
    }
    
    /// <summary>
    /// Resume tất cả VFX
    /// </summary>
    public void ResumeAllVFX()
    {
        foreach (var vfx in activeVFXInstances)
        {
            if (vfx.isActive && vfx.gameObject != null)
            {
                ParticleSystem[] particleSystems = vfx.gameObject.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in particleSystems)
                {
                    ps.Play();
                }
            }
        }
    }
    
    private void OnDestroy()
    {
        // Cleanup all VFX when manager is destroyed
        DespawnAllVFX(true);
    }
    
    // Context menu for testing
    [ContextMenu("Test Spawn Move VFX")]
    private void TestSpawnMoveVFX()
    {
        SpawnVFX(VFXType.Move, transform.position);
    }
    
    [ContextMenu("Test Spawn Attack VFX")]
    private void TestSpawnAttackVFX()
    {
        SpawnVFX(VFXType.Attack, transform.position);
    }
    
    [ContextMenu("Test Spawn Teleport VFX")]
    private void TestSpawnTeleportVFX()
    {
        SpawnVFX(VFXType.Teleport, transform.position);
    }
    
    [ContextMenu("Despawn All VFX")]
    private void TestDespawnAllVFX()
    {
        DespawnAllVFX();
    }
    
    [ContextMenu("Show VFX Stats")]
    private void ShowVFXStats()
    {
        Debug.Log($"Active VFX Count: {GetActiveVFXCount()}");
        foreach (VFXType type in System.Enum.GetValues(typeof(VFXType)))
        {
            int count = GetActiveVFXCount(type);
            if (count > 0)
            {
                Debug.Log($"{type}: {count} instances");
            }
        }
    }
}

// VFX Types
[System.Serializable]
public enum VFXType
{
    Move,       // VFX khi di chuyển
    Attack,     // VFX khi tấn công
    Teleport,   // VFX khi teleport
    Spawn,      // VFX khi spawn
    Destroy,    // VFX khi destroy
    Hit,        // VFX khi hit target
    Miss        // VFX khi miss
}

// VFX Instance data structure
[System.Serializable]
public class VFXInstance
{
    public GameObject gameObject;
    public VFXType vfxType;
    public float spawnTime;
    public float duration;
    public System.Action onCompleteCallback;
    public bool isActive;
    
    public float Age => Time.time - spawnTime;
    public float RemainingTime => duration - Age;
    public bool IsExpired => Age >= duration;
}
