using UnityEngine;
using Antymology.Terrain;

// Base class for all ant types, contains shared health, movement, eating, and digging logic
public abstract class AntBase : MonoBehaviour
{
    #region Health Fields

    public const int MAX_HEALTH = 750;
    protected int maxHealth = MAX_HEALTH;
    protected int health;
    protected int healthDecay = 1;
    protected int healthPerMulch = MAX_HEALTH / 3; // 33% of max health
    protected int mulchEaten = 0; // number of mulch blocks consumed (used for worker fitness)

    #endregion

    #region Movement Fields

    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    public float raycastDistance = 50f;

    public float randomTurnRange = 20f;

    protected int maxClimbHeight = 2;
    protected float maxDropHeight = Mathf.Infinity; // no drop limit by default
    protected Rigidbody rb;
    protected bool isGrounded = false;
    protected float currentGroundHeight = 0f;

    // the ants current horizontal movement direction in the XZ plane
    protected Vector3 currentDirection;

    #endregion

    #region Public Properties

    // current health of the ant, used by other ants for health sharing
    public int Health
    {
        get => health;
        set => health = value;
    }

    // maximum health of the ant, used by other ants to cap health transfers
    public int MaxHealth => maxHealth;

    #endregion

    #region Initialization

    protected virtual void Start()
    {
        health = maxHealth;

        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        // initialize with a random horizontal direction
        float randomAngle = Random.Range(0f, 360f);
        currentDirection = new Vector3(Mathf.Sin(randomAngle * Mathf.Deg2Rad), 0f, Mathf.Cos(randomAngle * Mathf.Deg2Rad)).normalized;
    }

    #endregion

    #region Ground Detection

    // Updates the current ground height by raycasting downward
    protected void UpdateGroundHeight()
    {
        RaycastHit hit;
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

    // Snaps the ant to the ground surface
    protected void SnapToGround()
    {
        if (isGrounded)
        {
            Vector3 targetPosition = transform.position;
            targetPosition.y = currentGroundHeight;
            transform.position = targetPosition;
        }
    }

    #endregion

    #region Movement

    // Randomly turns the ant within a set range, then moves forward in that new direction
    // If the move is blocked, pick a random new direction and try again
    protected void TryMove()
    {
        if (!isGrounded)
            return;

        float turnAngle = Random.Range(-randomTurnRange, randomTurnRange);
        currentDirection = Quaternion.Euler(0f, turnAngle, 0f) * currentDirection;
        currentDirection.y = 0f;
        currentDirection.Normalize();

        if (!MoveInDirection(currentDirection))
        {
            // cant move then pick a completely random new direction
            float randomAngle = Random.Range(0f, 360f);
            currentDirection = new Vector3(
                Mathf.Sin(randomAngle * Mathf.Deg2Rad), 0f,
                Mathf.Cos(randomAngle * Mathf.Deg2Rad));

            // rotate to face the new direction
            if (currentDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(currentDirection);
        }
    }

    // Moves the ant in the given horizontal direction, respecting climb and drop limits
    // Returns true if the move succeeded, false if blocked
    protected bool MoveInDirection(Vector3 direction)
    {
        Vector3 movement = direction * moveSpeed * Time.deltaTime;
        Vector3 newPosition = transform.position + movement;
        RaycastHit hit;
        Vector3 rayStart = new Vector3(newPosition.x, newPosition.y + 5f, newPosition.z);

        if (Physics.Raycast(rayStart, Vector3.down, out hit, raycastDistance))
        {
            float heightDifference = hit.point.y - currentGroundHeight;
            if (heightDifference > maxClimbHeight)
                return false;
            if (-heightDifference > maxDropHeight)
                return false;
            newPosition.y = hit.point.y;
        }
        else
        {
            newPosition.y = currentGroundHeight;
        }

        // Check that the block at the ant's body level is air (not inside a wall)
        int checkX = Mathf.FloorToInt(newPosition.x);
        int checkY = Mathf.FloorToInt(newPosition.y + 0.5f); // block at body height
        int checkZ = Mathf.FloorToInt(newPosition.z);
        AbstractBlock bodyBlock = WorldManager.Instance.GetBlock(checkX, checkY, checkZ);
        if (!(bodyBlock is AirBlock))
            return false;

        transform.position = newPosition;

        // rotate the ant to face its movement direction
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);

        return true;
    }

    #endregion

    #region Digging

    // Attempts to dig the block directly below the ant, cannot dig air, container, or nest blocks
    // If the block is mulch, the ant eats it and gains health
    protected bool TryDig()
    {
        int blockX = Mathf.FloorToInt(transform.position.x);
        int blockY = Mathf.FloorToInt(transform.position.y - 0.1f);
        int blockZ = Mathf.FloorToInt(transform.position.z);

        AbstractBlock block = WorldManager.Instance.GetBlock(blockX, blockY, blockZ);

        if (block is AirBlock || block is ContainerBlock || block is NestBlock)
            return false;

        if (IsAntOnBlock(blockX, blockY, blockZ))
            return false;

        // if mulch, eat it and gain health
        if (block is MulchBlock)
        {
            health += healthPerMulch;
            if (health > maxHealth)
                health = maxHealth;
            mulchEaten++;
        }

        WorldManager.Instance.SetBlock(blockX, blockY, blockZ, new AirBlock());
        return true;
    }

    #endregion

    #region Utility

    // Checks if another ant is close enough to be considered "on" the given block.
    // Uses a proximity check: within 1 block horizontally and 2 blocks vertically of the block center.
    protected bool IsAntOnBlock(int blockX, int blockY, int blockZ)
    {
        float centerX = blockX + 0.5f;
        float topY = blockY + 1f; // ants stand on top of the block
        float centerZ = blockZ + 0.5f;

        AntBase[] ants = FindObjectsByType<AntBase>(FindObjectsSortMode.None);
        foreach (AntBase ant in ants)
        {
            if (ant == this) continue;

            float dx = ant.transform.position.x - centerX;
            float dz = ant.transform.position.z - centerZ;
            float dy = ant.transform.position.y - topY;

            if (Mathf.Abs(dx) < 1f && Mathf.Abs(dz) < 1f && dy >= -0.5f && dy <= 1.5f)
                return true;
        }
        return false;
    }

    // Applies health decay for the current tick, acidic blocks double the decay rate
    protected void ApplyHealthDecay()
    {
        int decay = healthDecay;
        int blockX = Mathf.FloorToInt(transform.position.x);
        int blockY = Mathf.FloorToInt(transform.position.y - 0.1f);
        int blockZ = Mathf.FloorToInt(transform.position.z);
        if (WorldManager.Instance.GetBlock(blockX, blockY, blockZ) is AcidicBlock)
            decay *= 2;

        health -= decay;
        if (health <= 0)
        {
            Die();
        }
    }

    protected void Die()
    {
        // notify EvolutionManager before destruction so genes/fitness are recorded
        if (EvolutionManager.Instance != null)
            EvolutionManager.Instance.ReportDeath(this);

        Destroy(gameObject);
    }

    #endregion
}

