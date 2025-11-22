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
    private bool hasHit = false; // Flag to prevent multiple hits
    
    /// <summary>
    /// Initialize projectile
    /// </summary>
    public void Initialize(ProjectileType type, Vector3 target, float projectileSpeed = 5f)
    {
        Debug.Log($"ProjectileController.Initialize called - Type: {type}, Target: {target}, Speed: {projectileSpeed}");
        
        projectileType = type;
        targetPosition = target;
        speed = projectileSpeed;
        hasHit = false; // Reset hit flag
        
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
                Debug.Log($"Projectile {projectileType} reached target position");
                
                // Check for target at position (in case collision didn't trigger)
                if (!hasHit)
                {
                    CheckForTargetAtPosition();
                }
                
                // Destroy projectile if not already destroyed by HandleHit
                if (!hasHit)
                {
                    Destroy(gameObject);
                }
            });
    }
    
    /// <summary>
    /// Check for target at final position (fallback if collision didn't trigger)
    /// </summary>
    private void CheckForTargetAtPosition()
    {
        // Use overlap sphere to find target at position
        Collider[] colliders = Physics.OverlapSphere(targetPosition, 0.5f);
        
        foreach (Collider col in colliders)
        {
            if (IsValidTarget(col.gameObject))
            {
                Debug.Log($"Found target {col.gameObject.name} at position, triggering hit");
                HandleHit(col.gameObject);
                return; // Only hit first valid target
            }
        }
        
        // If no target found, trigger explode event
        Debug.Log($"No target found at position, triggering explosion");
        TriggerExplosion(targetPosition);
    }
    
    /// <summary>
    /// Handle collision với target
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // Check if target is valid and haven't hit yet
        if (!hasHit && IsValidTarget(other.gameObject))
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
        if (hasHit) return; // Prevent multiple hits
        
        hasHit = true;
        Debug.Log($"HandleHit called - Projectile {projectileType} hit {target.name}");
        
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
    /// Apply explosion force only to knocked down chess pieces (not affecting normal pieces)
    /// </summary>
    private void ApplyExplosionForce(Vector3 explosionPos)
    {
        Collider[] colliders = Physics.OverlapSphere(explosionPos, explosionRadius);
        
        foreach (Collider col in colliders)
        {
            if (col.gameObject == gameObject) continue; // Skip self
            
            // Check if this is a chess piece
            ChessPieceInfo pieceInfo = col.GetComponent<ChessPieceInfo>();
            if (pieceInfo == null) continue; // Skip non-chess pieces
            
            // Only apply force to knocked down pieces
            bool isKnockedDown = IsPieceKnockedDown(pieceInfo);
            if (!isKnockedDown) continue; // Skip normal pieces
            
            // Get or add Rigidbody for knocked down pieces
            Rigidbody rb = col.GetComponent<Rigidbody>();
            if (rb == null)
            {
                // Add Rigidbody if it doesn't exist (for knocked down pieces)
                rb = col.gameObject.AddComponent<Rigidbody>();
                rb.mass = 1f;
                rb.linearDamping = 0.5f;
                rb.angularDamping = 0.5f;
            }
            
            // Ensure Rigidbody is not kinematic so it can receive forces
            if (rb.isKinematic)
            {
                rb.isKinematic = false;
            }
            
            // Apply explosion force
            Vector3 direction = (col.transform.position - explosionPos).normalized;
            float distance = Vector3.Distance(col.transform.position, explosionPos);
            float force = knockbackForce * (1f - distance / explosionRadius); // Force decreases with distance
            
            // Apply force
            rb.AddForce(direction * force, ForceMode.Impulse);
            
            // Add upward force and torque for more dramatic effect on knocked down pieces
            rb.AddForce(Vector3.up * force * 0.3f, ForceMode.Impulse);
            rb.AddTorque(new Vector3(
                Random.Range(-1f, 1f) * force * 0.1f,
                Random.Range(-1f, 1f) * force * 0.1f,
                Random.Range(-1f, 1f) * force * 0.1f
            ), ForceMode.Impulse);
            
            Debug.Log($"Applied explosion force {force} to knocked down piece {col.name}");
        }
    }
    
    /// <summary>
    /// Check if a chess piece is knocked down (dissolving/falling)
    /// </summary>
    private bool IsPieceKnockedDown(ChessPieceInfo pieceInfo)
    {
        if (pieceInfo == null) return false;
        
        // Check ChessPieceSkinController for dissolving state
        ChessPieceSkinController skinController = pieceInfo.GetComponent<ChessPieceSkinController>();
        if (skinController != null && skinController.CurrentState == SkinState.Dissolving)
        {
            return true;
        }
        
        // Check PawnController for inactive state
        PawnController pawnController = pieceInfo.GetComponent<PawnController>();
        if (pawnController != null && !pawnController.IsActive)
        {
            return true;
        }
        
        // Check if piece is dissolving (has DissolveInEffect playing)
        DissolveInEffect dissolveEffect = pieceInfo.GetComponent<DissolveInEffect>();
        if (dissolveEffect != null && dissolveEffect.IsPlaying)
        {
            return true;
        }
        
        return false;
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
