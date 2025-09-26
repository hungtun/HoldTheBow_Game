using UnityEngine;

public class LogEnemyController : MonoBehaviour
{
    [Header("Animation Settings")]
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    [Header("Movement Settings")]
    public float moveSpeed;
    public float smoothTime = 0.1f;
    public float positionThreshold = 0.01f;

    [SerializeField] private HealthBar healthBar;


    private Vector3 targetPosition;
    private Vector3 velocity = Vector3.zero;
    private bool wakeUp = false;
    private Vector3 lastPosition;

    [Header("Chase Settings")]
    public float chaseRadius = 5f;
    public float homeSleepThreshold = 0.3f; 
    private Transform localPlayer;
    private Vector3 homePosition;

    private int currentHealth;
    private int maxHealth;

    void Start()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        targetPosition = transform.position;
        homePosition = transform.position; 
        lastPosition = transform.position;
        currentHealth = maxHealth;
        healthBar.SetMaxHealth(maxHealth);

        var local = FindObjectOfType<LocalPlayerController>();
        if (local != null) localPlayer = local.transform;
    }

    void Update()
    {
        if (Vector3.Distance(transform.position, targetPosition) > positionThreshold)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
            wakeUp = true;
        }
        else 
        {
            transform.position = targetPosition;
            float distHome = Vector3.Distance(transform.position, homePosition);
            wakeUp = distHome <= homeSleepThreshold ? false : true;
        }

        UpdateAnimation();
    }

    void UpdateAnimation()
    {
        animator.SetBool("wakeUp", wakeUp);

        Vector3 toTarget = targetPosition - transform.position;
        Vector2 dir = new Vector2(toTarget.x, toTarget.y);

        if (wakeUp && dir.sqrMagnitude > 1e-6f)
        {
            changeAnimator(dir);
        }
        else if (wakeUp && localPlayer != null)
        {
            Vector3 toPlayer = localPlayer.position - transform.position;
            Vector2 dirToPlayer = new Vector2(toPlayer.x, toPlayer.y);
            if (dirToPlayer.sqrMagnitude > 1e-6f)
            {
                changeAnimator(dirToPlayer.normalized);
            }
        }

        lastPosition = transform.position;
    }

    private void SetAnimatorFloat(Vector2 setVector)
    {
        animator.SetFloat("moveX", setVector.x);
        animator.SetFloat("moveY", setVector.y);
    }

    private void changeAnimator(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            if (direction.x > 0f)
            {
                SetAnimatorFloat(Vector2.right);
            }
            else if (direction.x < 0f)
            {
                SetAnimatorFloat(Vector2.left);
            }
        }
        else if(Mathf.Abs(direction.y) > Mathf.Abs(direction.x))
        {
            if (direction.y > 0f)
            {
                SetAnimatorFloat(Vector2.up);
            }
            else if (direction.y < 0f)
            {
                SetAnimatorFloat(Vector2.down);
            }
        }
        
    }

    public void SetTargetPosition(float x, float y)
    {
        targetPosition = new Vector3(x, y, transform.position.z);
    }
}
