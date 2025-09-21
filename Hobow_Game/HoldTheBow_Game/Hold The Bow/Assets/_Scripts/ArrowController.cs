using UnityEngine;
using SharedLibrary.Events;

/// <summary>
/// Controls arrow behavior on client side
/// </summary>
public class ArrowController : MonoBehaviour
{
    [Header("Arrow Settings")]
    public float speed = 15f;
    public float lifetime = 10f; // Max lifetime before auto-destroy
    
    private int arrowId;
    private int heroId;
    private float damage;
    private float accuracy;
    private Vector3 direction;
    private bool isStuck = false;
    private float stuckTime = 0f;
    private const float STUCK_LIFETIME = 5f; // 5 seconds stuck lifetime
    
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Collider2D arrowCollider;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        arrowCollider = GetComponent<Collider2D>();
        
        // Set initial velocity
        if (rb != null)
        {
            rb.velocity = direction * speed;
        }
        
        // Auto-destroy after lifetime
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (isStuck)
        {
            stuckTime += Time.deltaTime;
            if (stuckTime >= STUCK_LIFETIME)
            {
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// Initialize arrow with data from server
    /// </summary>
    public void Initialize(ArrowSpawnedEvent spawnedEvent)
    {
        arrowId = spawnedEvent.ArrowId;
        heroId = spawnedEvent.HeroId;
        damage = spawnedEvent.Damage;
        accuracy = spawnedEvent.Accuracy;
        speed = spawnedEvent.Speed;
        
        direction = new Vector3(spawnedEvent.DirectionX, spawnedEvent.DirectionY, 0f).normalized;
        
        // Position arrow at spawn location
        transform.position = new Vector3(spawnedEvent.StartX, spawnedEvent.StartY, 0f);
        
        // Rotate arrow to face direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    /// <summary>
    /// Handle arrow hit event from server
    /// </summary>
    public void HandleHit(ArrowHitEvent hitEvent)
    {
        if (hitEvent.ArrowId != arrowId) return;
        
        isStuck = true;
        
        // Stop movement
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.isKinematic = true;
        }
        
        // Disable collision
        if (arrowCollider != null)
        {
            arrowCollider.enabled = false;
        }
        
        // Position at hit location
        transform.position = new Vector3(hitEvent.HitX, hitEvent.HitY, 0f);
        
        // Handle different hit types
        switch (hitEvent.HitType)
        {
            case "Wall":
                HandleWallHit();
                break;
            case "Enemy":
                HandleEnemyHit(hitEvent);
                break;
            case "Hero":
                HandleHeroHit(hitEvent);
                break;
        }
    }

    private void HandleWallHit()
    {
        // Arrow sticks to wall - no special effects needed
        Debug.Log($"Arrow {arrowId} stuck to wall at {transform.position}");
    }

    private void HandleEnemyHit(ArrowHitEvent hitEvent)
    {
        // Arrow sticks to enemy
        Debug.Log($"Arrow {arrowId} hit enemy {hitEvent.TargetId}, damage: {hitEvent.Damage}, remaining health: {hitEvent.RemainingHealth}");
        
        // Try to parent to enemy if it exists
        if (hitEvent.TargetId.HasValue)
        {
            var enemyObj = GameObject.Find($"Enemy_{hitEvent.TargetId}");
            if (enemyObj != null)
            {
                transform.SetParent(enemyObj.transform);
            }
        }
    }

    private void HandleHeroHit(ArrowHitEvent hitEvent)
    {
        // Arrow sticks to hero
        Debug.Log($"Arrow {arrowId} hit hero {hitEvent.TargetId}, damage: {hitEvent.Damage}");
        
        // Try to parent to hero if it exists
        if (hitEvent.TargetId.HasValue)
        {
            var heroObj = GameObject.Find($"Hero_{hitEvent.TargetId}");
            if (heroObj != null)
            {
                transform.SetParent(heroObj.transform);
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Only handle collision if not stuck
        if (isStuck) return;
        
        // Don't hit the shooter
        if (other.CompareTag("Player") && other.GetComponent<LocalPlayerController>()?.heroId == heroId)
        {
            return;
        }
        
        // Handle collision with walls, enemies, or other players
        if (other.CompareTag("Wall") || other.CompareTag("Enemy") || other.CompareTag("Player"))
        {
            // Stop the arrow (server will handle the actual hit logic)
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
        }
    }
}