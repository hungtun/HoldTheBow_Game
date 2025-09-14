using UnityEngine;

public class RemotePlayerController : MonoBehaviour
{
    private float interpolationSpeed = 15f;
    private float animationThreshold = 0.01f;

    private Animator animator;
    private SpriteRenderer spriteRenderer;

    // State
    private Vector3 targetPosition;
    private bool isMoving;
    private int playerId;
    private Vector2 lastNonZeroDirection = Vector2.down; 

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        

        var rigidbodies = GetComponentsInChildren<Rigidbody2D>(true);
        foreach (var rb in rigidbodies)
        {
            rb.isKinematic = true; 
        }

        var colliders = GetComponentsInChildren<Collider2D>(true);
        foreach (var col in colliders)
        {
            gameObject.layer = LayerMask.NameToLayer("RemotePlayer");
        }
    }

    public void Initialize(int id)
    {
        playerId = id;
        gameObject.name = $"RemotePlayer_{id}";
        targetPosition = transform.position;
    }

    public void UpdatePosition(Vector3 newPosition)
    {
        targetPosition = newPosition;
    }

    private void Update()
    {
        InterpolatePosition();
    }

    private void InterpolatePosition()
    {
        Vector3 oldPosition = transform.position;
        transform.position = Vector3.Lerp(transform.position, targetPosition, interpolationSpeed * Time.deltaTime);

        Vector3 movement = transform.position - oldPosition;
        float distance = movement.magnitude;

        if (distance > animationThreshold)
        {
            Vector2 dir = movement.normalized;
            UpdateAnimation(true, dir);
        }
        else
        {
            UpdateAnimation(false, lastNonZeroDirection);
        }
    }

    private void UpdateAnimation(bool moving, Vector2 direction)
    {
        if (animator != null)
        {
            Vector2 usedDirection = direction;
            if (moving)
            {
                if (direction.sqrMagnitude > 0.0001f)
                {
                    lastNonZeroDirection = direction;
                }
            }
            else
            {
                usedDirection = lastNonZeroDirection;
            }

            animator.SetFloat("moveX", usedDirection.x);
            animator.SetFloat("moveY", usedDirection.y);
            animator.SetBool("moving", moving);
        }
        isMoving = moving;
    }

    public void ApplyAnimationState(bool moving, Vector2 direction)
    {
        UpdateAnimation(moving, direction);
    }



    public void Dispose()
    {
        try
        {
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RemotePlayerController] Dispose failed: {ex.Message}");
        }
    }
}
