using UnityEngine;
using DG.Tweening;

/// <summary>
/// Projectile đơn giản - chỉ cần collision detection
/// </summary>
public class ProjectileController : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private ProjectileType projectileType;
    [SerializeField] private float speed = 5f;
    
    [Header("Collision")]
    [SerializeField] private LayerMask targetLayers = -1;
    [SerializeField] private float explosionRadius = 2f;
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private GameObject explosionEffect;
    
    // Properties
    public ProjectileType Type => projectileType;
    
    // Events
    public System.Action<ProjectileController, GameObject> OnHit;
    public System.Action<ProjectileController, Vector3> OnExplode;
    
    private Vector3 targetPosition;
    private Tween moveTween;
    
    /// <summary>
    /// Initialize projectile
    /// </summary>
    public void Initialize(ProjectileType type, Vector3 target, float projectileSpeed = 5f)
    {
        Debug.Log($"ProjectileController.Initialize called - Type: {type}, Target: {target}, Speed: {projectileSpeed}");
        
        projectileType = type;
        targetPosition = target;
        speed = projectileSpeed;
        
        // Calculate direction và face target
        Vector3 direction = (targetPosition - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
            Debug.Log($"Projectile facing direction: {direction}");
        }
        
        // Start movement
        StartMovement();
    }
    
    /// <summary>
    /// Bắt đầu di chuyển đến target
    /// </summary>
    private void StartMovement()
    {
        float distance = Vector3.Distance(transform.position, targetPosition);
        float duration = distance / speed;
        
        Debug.Log($"Starting movement - Distance: {distance}, Duration: {duration}, Speed: {speed}");
        
        moveTween = transform.DOMove(targetPosition, duration)
            .SetEase(Ease.Linear)
            .OnComplete(() => {
                Debug.Log($"Projectile {projectileType} reached target, destroying...");
                Destroy(gameObject);
            });
    }
    
    /// <summary>
    /// Handle collision với target
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // Check if target is valid
        if (IsValidTarget(other.gameObject))
        {
            HandleHit(other.gameObject);
        }
    }
    
    /// <summary>
    /// Check if target is valid
    /// </summary>
    private bool IsValidTarget(GameObject target)
    {
        // Check layer mask
        if (targetLayers != -1 && (targetLayers.value & (1 << target.layer)) == 0)
        {
            return false;
        }
        
        // Check if target has ChessPieceInfo
        ChessPieceInfo pieceInfo = target.GetComponent<ChessPieceInfo>();
        if (pieceInfo == null)
        {
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Handle hit với target
    /// </summary>
    private void HandleHit(GameObject target)
    {
        Debug.Log($"Projectile {projectileType} hit {target.name}");
        
        // Stop movement
        moveTween?.Kill();
        
        // Trigger explode effect
        TriggerExplosion(target.transform.position);
        
        // Apply knockback to target
        ApplyKnockback(target);
        
        // Trigger hit event
        OnHit?.Invoke(this, target);
        
        // Destroy projectile
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Trigger explosion effect
    /// </summary>
    private void TriggerExplosion(Vector3 explosionPos)
    {
        Debug.Log($"Explosion at {explosionPos} with radius {explosionRadius}");
        
        // Spawn explosion effect
        if (explosionEffect != null)
        {
            GameObject explosion = Instantiate(explosionEffect, explosionPos, Quaternion.identity);
            Destroy(explosion, 3f); // Auto destroy after 3 seconds
        }
        
        // Spawn VFX explosion nếu có VFXManager
        if (VFXManager.Instance != null)
        {
            VFXManager.Instance.SpawnVFX(VFXType.Hit, explosionPos);
        }
        
        // Apply explosion force to nearby objects
        ApplyExplosionForce(explosionPos);
        
        // Trigger explode event
        OnExplode?.Invoke(this, explosionPos);
    }
    
    /// <summary>
    /// Apply explosion force to nearby objects
    /// </summary>
    private void ApplyExplosionForce(Vector3 explosionPos)
    {
        Collider[] colliders = Physics.OverlapSphere(explosionPos, explosionRadius);
        
        foreach (Collider col in colliders)
        {
            if (col.gameObject == gameObject) continue; // Skip self
            
            Rigidbody rb = col.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 direction = (col.transform.position - explosionPos).normalized;
                float distance = Vector3.Distance(col.transform.position, explosionPos);
                float force = knockbackForce * (1f - distance / explosionRadius); // Force decreases with distance
                
                rb.AddForce(direction * force, ForceMode.Impulse);
                Debug.Log($"Applied explosion force {force} to {col.name}");
            }
        }
    }
    
    /// <summary>
    /// Apply knockback to specific target
    /// </summary>
    private void ApplyKnockback(GameObject target)
    {
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            Vector3 direction = (target.transform.position - transform.position).normalized;
            targetRb.AddForce(direction * knockbackForce, ForceMode.Impulse);
            Debug.Log($"Applied knockback {knockbackForce} to {target.name}");
        }
        else
        {
            Debug.LogWarning($"No Rigidbody found on {target.name} for knockback");
        }
    }
    
    private void OnDestroy()
    {
        moveTween?.Kill();
    }
}
