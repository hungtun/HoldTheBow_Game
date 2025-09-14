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


    private Vector3 targetPosition;
    private Vector3 velocity = Vector3.zero;
    private bool wakeUp = false;
    private Vector3 lastPosition;

    void Start()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        targetPosition = transform.position;
        lastPosition = transform.position;
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
            wakeUp = false;
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
