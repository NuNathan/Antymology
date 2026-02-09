using UnityEngine;
using Antymology.Terrain;

// Base class for all ant types, contains shared health, movement, eating, and digging logic
public abstract class AntBase : MonoBehaviour
{
    #region Health Fields

    protected int maxHealth = 1000;
    protected int health;
    protected int healthDecay = 1;
    protected int healthPerMulch = 250;

    #endregion

    #region Movement Fields

    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    public float raycastDistance = 50f;

    public float randomTurnRange = 20f;

    protected int maxClimbHeight = 2;
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
    protected void TryMove()
    {
        if (!isGrounded)
            return;

        float turnAngle = Random.Range(-randomTurnRange, randomTurnRange);
        currentDirection = Quaternion.Euler(0f, turnAngle, 0f) * currentDirection;
        currentDirection.y = 0f;
        currentDirection.Normalize();

        MoveInDirection(currentDirection);
    }

    // Moves the ant in the given horizontal direction, respecting climb height limits
    protected void MoveInDirection(Vector3 direction)
    {
        Vector3 movement = direction * moveSpeed * Time.fixedDeltaTime;
        Vector3 newPosition = transform.position + movement;
        RaycastHit hit;
        Vector3 rayStart = new Vector3(newPosition.x, newPosition.y + 5f, newPosition.z);

        if (Physics.Raycast(rayStart, Vector3.down, out hit, raycastDistance))
        {
            float heightDifference = hit.point.y - currentGroundHeight;
            if (heightDifference > maxClimbHeight)
                return;
            newPosition.y = hit.point.y;
        }
        else
        {
            newPosition.y = currentGroundHeight;
        }

        transform.position = newPosition;
    }

    #endregion

    #region Eating

    // Attempts to eat the mulch block directly below the ant
    protected void TryEatMulch()
    {
        int blockX = Mathf.FloorToInt(transform.position.x);
        int blockY = Mathf.FloorToInt(transform.position.y - 0.1f);
        int blockZ = Mathf.FloorToInt(transform.position.z);

        AbstractBlock block = WorldManager.Instance.GetBlock(blockX, blockY, blockZ);

        if (block is MulchBlock && !IsAntOnBlock(blockX, blockY, blockZ))
        {
            health += healthPerMulch;
            if (health > maxHealth)
                health = maxHealth;

            WorldManager.Instance.SetBlock(blockX, blockY, blockZ, new AirBlock());
        }
    }

    #endregion

    #region Digging

    // Attempts to dig the block directly below the ant, cannot dig mulch, air, or container blocks
    protected bool TryDig()
    {
        int blockX = Mathf.FloorToInt(transform.position.x);
        int blockY = Mathf.FloorToInt(transform.position.y - 0.1f);
        int blockZ = Mathf.FloorToInt(transform.position.z);

        AbstractBlock block = WorldManager.Instance.GetBlock(blockX, blockY, blockZ);

        if (block is AirBlock || block is MulchBlock || block is ContainerBlock)
            return false;

        if (IsAntOnBlock(blockX, blockY, blockZ))
            return false;

        WorldManager.Instance.SetBlock(blockX, blockY, blockZ, new AirBlock());
        return true;
    }

    #endregion

    #region Utility

    // Checks if another ant is standing on the given block
    protected bool IsAntOnBlock(int blockX, int blockY, int blockZ)
    {
        AntBase[] ants = FindObjectsByType<AntBase>(FindObjectsSortMode.None);
        foreach (AntBase ant in ants)
        {
            if (ant == this) continue;

            int otherX = Mathf.FloorToInt(ant.transform.position.x);
            int otherY = Mathf.FloorToInt(ant.transform.position.y - 0.1f);
            int otherZ = Mathf.FloorToInt(ant.transform.position.z);

            if (otherX == blockX && otherY == blockY && otherZ == blockZ)
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
        Destroy(gameObject);
    }

    #endregion
}

