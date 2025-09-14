using UnityEngine;

public class Arrow : MonoBehaviour
{
    private Rigidbody2D rb;
    private bool hasHit = false;

    void Start()
    {
        try
        {
            rb = GetComponent<Rigidbody2D>();
            if (gameObject != null)
            {
                Destroy(gameObject, 5f);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Arrow] Start failed: {ex.Message}");
        }
    }

    void Update()
    {
        if (rb != null && rb.velocity.magnitude < 0.1f && !hasHit)
        {
            OnHit();
        }
    }
    
    private void OnHit()
    {
        if (hasHit) return;
        hasHit = true;
        
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
        }
        
    }
}

