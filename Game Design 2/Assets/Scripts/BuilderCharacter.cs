using UnityEngine;
using System.Collections;

public class BuilderCharacter : MonoBehaviour
{
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool isDead = false;

    void Start()
    {
        Debug.Log("BuilderCharacter script initialized.");
        animator = GetComponent<Animator>();
        if (animator == null) Debug.LogError("BuilderCharacter: No Animator found on this object!");

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) originalColor = spriteRenderer.color;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;

        Debug.Log($"Builder collided with {collision.gameObject.name}");

        // Check what hit us. During collapse, Nodes and Anvils have Rigidbodies.
        // We assume anything hitting us with significant force during simulation is dangerous.
        
        // You can filter by name or tag if needed.
        // For now, any dynamic object hitting us triggers death.
        if (collision.gameObject.GetComponent<Rigidbody2D>() != null)
        {
             // Optional: Check impact force
             // if (collision.relativeVelocity.magnitude < 1.0f) return;

             // Visual Feedback (Hit Flash)
             if (spriteRenderer != null) StartCoroutine(FlashRedSequence());

             if (animator != null)
             {
                 Debug.Log("Builder hit by Rigidbody! Triggering 'Die' animation.");
                 animator.SetTrigger("Die");
                 isDead = true;
             }
             else
             {
                 Debug.LogError("Builder hit, but Animator is missing!");
             }
        }
        else
        {
             Debug.Log($"Builder hit by {collision.gameObject.name}, but it has no Rigidbody2D. Ignoring.");
        }
    }

    IEnumerator FlashRedSequence()
    {
        spriteRenderer.color = new Color(1f, 0.3f, 0.3f); // Bright Red tint
        yield return new WaitForSeconds(0.15f);
        spriteRenderer.color = originalColor;
    }
}
