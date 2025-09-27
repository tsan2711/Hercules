using UnityEngine;

/// <summary>
/// Script đơn giản để tạo projectile prefabs
/// </summary>
public class ProjectileCreator : MonoBehaviour
{
    [ContextMenu("Create All Projectile Prefabs")]
    public void CreateAllProjectilePrefabs()
    {
        CreateProjectilePrefab(ProjectileType.Pawn, Color.white);
        CreateProjectilePrefab(ProjectileType.Knight, Color.yellow);
        CreateProjectilePrefab(ProjectileType.Bishop, Color.cyan);
        CreateProjectilePrefab(ProjectileType.Rook, Color.red);
        CreateProjectilePrefab(ProjectileType.Queen, Color.magenta);
        CreateProjectilePrefab(ProjectileType.King, Color.yellow);
        
        Debug.Log("All projectile prefabs created!");
    }
    
    private GameObject CreateProjectilePrefab(ProjectileType type, Color color)
    {
        // Tạo GameObject
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = $"{type}Projectile";
        
        // Set color
        Renderer renderer = projectile.GetComponent<Renderer>();
        Material material = new Material(Shader.Find("Standard"));
        material.color = color;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * 0.5f);
        renderer.material = material;
        
        // Set scale
        projectile.transform.localScale = Vector3.one * 0.5f;
        
        // Setup collider
        Collider collider = projectile.GetComponent<Collider>();
        collider.isTrigger = true;
        
        // Add ProjectileController
        ProjectileController controller = projectile.AddComponent<ProjectileController>();
        
        // Set projectile type trong ProjectileController
        var projectileTypeField = typeof(ProjectileController).GetField("projectileType", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        projectileTypeField?.SetValue(controller, type);
        
        // Set explosion settings
        var explosionRadiusField = typeof(ProjectileController).GetField("explosionRadius", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        explosionRadiusField?.SetValue(controller, GetExplosionRadius(type));
        
        var knockbackForceField = typeof(ProjectileController).GetField("knockbackForce", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        knockbackForceField?.SetValue(controller, GetKnockbackForce(type));
        
        // Create explosion effect
        GameObject explosionEffect = CreateExplosionEffect(type, color);
        var explosionEffectField = typeof(ProjectileController).GetField("explosionEffect", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        explosionEffectField?.SetValue(controller, explosionEffect);
        
        return projectile;
    }
    
    /// <summary>
    /// Get explosion radius cho từng loại projectile
    /// </summary>
    private float GetExplosionRadius(ProjectileType type)
    {
        switch (type)
        {
            case ProjectileType.Pawn: return 1f;
            case ProjectileType.Knight: return 1.5f;
            case ProjectileType.Bishop: return 2f;
            case ProjectileType.Rook: return 2.5f;
            case ProjectileType.Queen: return 3f;
            case ProjectileType.King: return 2f;
            default: return 1.5f;
        }
    }
    
    /// <summary>
    /// Get knockback force cho từng loại projectile
    /// </summary>
    private float GetKnockbackForce(ProjectileType type)
    {
        switch (type)
        {
            case ProjectileType.Pawn: return 5f;
            case ProjectileType.Knight: return 8f;
            case ProjectileType.Bishop: return 10f;
            case ProjectileType.Rook: return 12f;
            case ProjectileType.Queen: return 15f;
            case ProjectileType.King: return 10f;
            default: return 8f;
        }
    }
    
    /// <summary>
    /// Tạo explosion effect cho projectile
    /// </summary>
    private GameObject CreateExplosionEffect(ProjectileType type, Color color)
    {
        GameObject explosion = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        explosion.name = $"{type}ExplosionEffect";
        explosion.transform.localScale = Vector3.one * 0.1f; // Small sphere
        
        // Set color với alpha
        Renderer renderer = explosion.GetComponent<Renderer>();
        Material material = new Material(Shader.Find("Standard"));
        material.color = new Color(color.r, color.g, color.b, 0.8f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * 0.5f);
        renderer.material = material;
        
        // Disable collider
        Collider collider = explosion.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }
        
        // Add particle system for explosion effect
        ParticleSystem particles = explosion.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.startLifetime = 1f;
        main.startSpeed = 5f;
        main.startSize = 0.5f;
        main.startColor = color;
        main.maxParticles = 50;
        
        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 50)
        });
        
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;
        
        var velocityOverLifetime = particles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(5f);
        
        explosion.SetActive(false); // Start inactive
        
        return explosion;
    }
}
