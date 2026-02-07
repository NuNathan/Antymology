using UnityEngine;

public class AntAgent : MonoBehaviour
{
    private int maxHealth = 1000;
    private int health;
    private int healthDecay = 0;

    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    public float raycastDistance = 50f; // How far down to check for ground

    private Rigidbody rb;
    private bool isGrounded = false;
    private float currentGroundHeight = 0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        health = maxHealth;

        // Get the Rigidbody component
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Use kinematic movement - no gravity, we'll handle positioning manually
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        // Don't find ground height in Start - wait for position to be set by WorldManager
        // UpdateGroundHeight will be called in first Update()
    }

    /// <summary>
    /// Updates the current ground height by raycasting downward
    /// </summary>
    private void UpdateGroundHeight()
    {
        RaycastHit hit;
        // Start the ray high above the ant to make sure we're above any terrain
        Vector3 rayStart = new Vector3(transform.position.x, transform.position.y + 5f, transform.position.z);

        bool hitGround = Physics.Raycast(rayStart, Vector3.down, out hit, raycastDistance);

        if (hitGround)
        {
            currentGroundHeight = hit.point.y;
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Update ground height every frame
        UpdateGroundHeight();

        // Stick to the ground
        if (isGrounded)
        {
            Vector3 targetPosition = transform.position;
            targetPosition.y = currentGroundHeight;
            transform.position = targetPosition;
        }

        // Only try to move if we're grounded
        if (isGrounded)
        {
            TryMove(Vector3.forward);
        }

        //Decay health
        health -= healthDecay;
        if (health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Destroy(gameObject);
    }

    // TODO: implement a better movement system, check for constraints and stuff
    private void TryMove(Vector3 direction)
    {
        if (direction.magnitude > 0.01f && isGrounded)
        {
            // Calculate horizontal movement (X and Z only)
            Vector3 movement = direction.normalized * moveSpeed * Time.deltaTime;
            movement.y = 0; // Don't move vertically - ground height is handled separately

            // Apply movement
            Vector3 newPosition = transform.position + movement;

            // Keep the Y position at ground level (will be updated in next UpdateGroundHeight call)
            newPosition.y = currentGroundHeight;

            transform.position = newPosition;
        }
    }

}
