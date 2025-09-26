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

  
    public void Initialize(ArrowSpawnedEvent spawnedEvent)
    {
        arrowId = spawnedEvent.ArrowId;
        heroId = spawnedEvent.HeroId;
        damage = spawnedEvent.Damage;
        accuracy = spawnedEvent.Accuracy;
        speed = spawnedEvent.Speed;
        
        direction = new Vector3(spawnedEvent.DirectionX, spawnedEvent.DirectionY, 0f).normalized;
        
        transform.position = new Vector3(spawnedEvent.StartX, spawnedEvent.StartY, 0f);
        
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle + 90f, Vector3.forward);
    }

    public void HandleHit(ArrowHitEvent hitEvent)
    {
        if (hitEvent.ArrowId != arrowId) return;
        
        isStuck = true;
        
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.isKinematic = true;
        }
        
        if (arrowCollider != null)
        {
            arrowCollider.enabled = false;
        }
        
        transform.position = new Vector3(hitEvent.HitX, hitEvent.HitY, 0f);
        
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
        Debug.Log($"Arrow {arrowId} stuck to wall at {transform.position}");
    }

    private void HandleEnemyHit(ArrowHitEvent hitEvent)
    {
        Debug.Log($"Arrow {arrowId} hit enemy {hitEvent.TargetId}, damage: {hitEvent.Damage}, remaining health: {hitEvent.RemainingHealth}");
        
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
        Debug.Log($"Arrow {arrowId} hit hero {hitEvent.TargetId}, damage: {hitEvent.Damage}");
        
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
        if (isStuck) return;
        
        if (other.CompareTag("Player") && other.GetComponent<LocalPlayerController>()?.heroId == heroId)
        {
            return;
        }
        
        if (other.CompareTag("Wall") || other.CompareTag("Enemy") || other.CompareTag("Player"))
        {
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
        }
    }
}