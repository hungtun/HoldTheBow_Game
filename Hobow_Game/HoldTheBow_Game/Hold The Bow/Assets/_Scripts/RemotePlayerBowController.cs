using UnityEngine;

public class RemotePlayerBowController : MonoBehaviour
{
    public Transform bowTransform;
    private Animator animator;
    private bool lastIsCharging;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (bowTransform == null) bowTransform = transform;
    }

    public void ApplyBowState(float angleDeg, bool isCharging, float chargePercent)
    {
        if (bowTransform != null)
        {
            bowTransform.localRotation = Quaternion.Euler(0, 0, angleDeg + 90f);
        }
        if (animator != null)
        {
            animator.SetBool("isCharging", isCharging);
            animator.SetFloat("chargePercent", chargePercent);
            animator.SetFloat("accuracyPercent", chargePercent);

            if (lastIsCharging && !isCharging)
            {
                animator.SetTrigger("shoot");
            }
        }
        lastIsCharging = isCharging;
    }
}


