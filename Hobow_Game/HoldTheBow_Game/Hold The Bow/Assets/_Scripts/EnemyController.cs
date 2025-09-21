using UnityEngine;
using System.Collections;

public class EnemyController : MonoBehaviour
{
    [Header("Flash Effect")]
    public Color flashColor = Color.red;
    public float flashDuration = 0.2f;
    
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool isFlashing = false;
    
    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }
    
    /// <summary>
    /// Flash red when enemy is hit
    /// </summary>
    public void FlashRed()
    {
        if (!isFlashing && spriteRenderer != null)
        {
            StartCoroutine(FlashCoroutine());
        }
    }
    
    private IEnumerator FlashCoroutine()
    {
        isFlashing = true;
        
        // Flash to red
        spriteRenderer.color = flashColor;
        yield return new WaitForSeconds(flashDuration / 2);
        
        // Flash back to original color
        spriteRenderer.color = originalColor;
        yield return new WaitForSeconds(flashDuration / 2);
        
        isFlashing = false;
    }
    
    /// <summary>
    /// Destroy enemy when it dies
    /// </summary>
    public void Die()
    {
        // Add death effect here if needed
        StartCoroutine(DeathEffect());
    }
    
    private IEnumerator DeathEffect()
    {
        // Flash red quickly before death
        if (spriteRenderer != null)
        {
            spriteRenderer.color = flashColor;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = originalColor;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = flashColor;
            yield return new WaitForSeconds(0.1f);
        }
        
        // Destroy the enemy
        Destroy(gameObject);
    }
}
