using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Quản lý projectiles đơn giản
/// </summary>
public class ProjectileManager : MonoBehaviour
{
    public static ProjectileManager Instance { get; private set; }
    
    [Header("Projectile Prefabs")]
    [SerializeField] private GameObject pawnProjectilePrefab;
    [SerializeField] private GameObject knightProjectilePrefab;
    [SerializeField] private GameObject bishopProjectilePrefab;
    [SerializeField] private GameObject rookProjectilePrefab;
    [SerializeField] private GameObject queenProjectilePrefab;
    [SerializeField] private GameObject kingProjectilePrefab;
    
    private Dictionary<ProjectileType, GameObject> projectilePrefabs;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Initialize()
    {
        projectilePrefabs = new Dictionary<ProjectileType, GameObject>();
        projectilePrefabs[ProjectileType.Pawn] = pawnProjectilePrefab;
        projectilePrefabs[ProjectileType.Knight] = knightProjectilePrefab;
        projectilePrefabs[ProjectileType.Bishop] = bishopProjectilePrefab;
        projectilePrefabs[ProjectileType.Rook] = rookProjectilePrefab;
        projectilePrefabs[ProjectileType.Queen] = queenProjectilePrefab;
        projectilePrefabs[ProjectileType.King] = kingProjectilePrefab;
    }
    
    /// <summary>
    /// Spawn projectile đơn giản - chỉ instantiate, để ProjectileController handle movement
    /// </summary>
    public GameObject SpawnProjectile(ProjectileType type, Vector3 startPos, Vector3 targetPos, float speed = 5f)
    {
        Debug.Log($"ProjectileManager.SpawnProjectile called - Type: {type}, Start: {startPos}, Target: {targetPos}, Speed: {speed}");
        
        GameObject prefab = projectilePrefabs[type];
        if (prefab == null)
        {
            Debug.LogError($"No prefab found for {type}");
            return null;
        }
        
        Debug.Log($"Prefab found: {prefab.name}");
        
        GameObject projectile = Instantiate(prefab, startPos, Quaternion.identity);
        Debug.Log($"Projectile instantiated: {projectile.name}");
        
        // Let ProjectileController handle movement và collision
        ProjectileController controller = projectile.GetComponent<ProjectileController>();
        if (controller == null)
        {
            Debug.Log("ProjectileController not found, adding one...");
            controller = projectile.AddComponent<ProjectileController>();
        }
        
        if (controller != null)
        {
            Debug.Log("ProjectileController found, initializing...");
            controller.Initialize(type, targetPos, speed);
            Debug.Log("ProjectileController initialized successfully");
        }
        else
        {
            Debug.LogError($"Failed to create ProjectileController for {type}!");
        }
        
        return projectile;
    }
}
