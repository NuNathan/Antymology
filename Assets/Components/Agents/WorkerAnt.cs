using UnityEngine;
using Antymology.Terrain;

// Worker ant behavior, eats mulch, feeds the queen when health is high, wanders randomly otherwise
public class WorkerAnt : AntBase
{
    void FixedUpdate()
    {
        UpdateGroundHeight();
        SnapToGround();

        // when health > 2/3, move toward queen and share food
        bool feedingQueen = false;
        if (health > maxHealth * 2 / 3)
        {
            QueenAnt queen = FindQueen();
            if (queen != null)
            {
                feedingQueen = true;
                if (isGrounded)
                    MoveTowardQueen(queen);
                TryShareHealthWith(queen);
            }
        }

        // normal random movement when not feeding queen
        if (!feedingQueen && isGrounded)
        {
            TryMove();
        }

        // eat mulch if health is low enough for a full munch, only when not feeding queen
        if (!feedingQueen && health < maxHealth - healthPerMulch)
        {
            TryEatMulch();
        }

        ApplyHealthDecay();
    }

    // Finds the queen ant in the scene, returns null if no queen exists
    private QueenAnt FindQueen()
    {
        QueenAnt[] queens = FindObjectsByType<QueenAnt>(FindObjectsSortMode.None);
        if (queens.Length > 0)
            return queens[0];
        return null;
    }

    // Moves the worker ant toward the queens position instead of wandering randomly
    private void MoveTowardQueen(QueenAnt queen)
    {
        if (!isGrounded || queen == null)
            return;

        Vector3 dirToQueen = queen.transform.position - transform.position;
        dirToQueen.y = 0f;

        // already at the queens position
        if (dirToQueen.sqrMagnitude < 0.01f)
            return;

        currentDirection = dirToQueen.normalized;
        MoveInDirection(currentDirection);
    }

    // Shares excess health above 2/3 threshold with the target when on the same block
    private int TryShareHealthWith(AntBase target)
    {
        if (health <= 0 || target == null)
            return 0;

        int blockX = Mathf.FloorToInt(transform.position.x);
        int blockY = Mathf.FloorToInt(transform.position.y - 0.1f);
        int blockZ = Mathf.FloorToInt(transform.position.z);

        int otherX = Mathf.FloorToInt(target.transform.position.x);
        int otherY = Mathf.FloorToInt(target.transform.position.y - 0.1f);
        int otherZ = Mathf.FloorToInt(target.transform.position.z);

        // must be on the same block
        if (otherX != blockX || otherY != blockY || otherZ != blockZ)
            return 0;

        // share excess health above 2/3 threshold
        int excess = health - (maxHealth * 2 / 3);
        if (excess <= 0)
            return 0;

        // cap by what the target can accept
        int transfer = Mathf.Min(excess, target.MaxHealth - target.Health);
        if (transfer <= 0)
            return 0;

        health -= transfer;
        target.Health += transfer;
        return transfer;
    }

    // Transfers health to any other ant on the same block, capped by receiver capacity
    private int TryShareHealth(int amount)
    {
        if (amount <= 0 || health <= 0)
            return 0;

        int blockX = Mathf.FloorToInt(transform.position.x);
        int blockY = Mathf.FloorToInt(transform.position.y - 0.1f);
        int blockZ = Mathf.FloorToInt(transform.position.z);

        AntBase[] ants = FindObjectsByType<AntBase>(FindObjectsSortMode.None);
        foreach (AntBase other in ants)
        {
            if (other == this) continue;

            int otherX = Mathf.FloorToInt(other.transform.position.x);
            int otherY = Mathf.FloorToInt(other.transform.position.y - 0.1f);
            int otherZ = Mathf.FloorToInt(other.transform.position.z);

            if (otherX == blockX && otherY == blockY && otherZ == blockZ)
            {
                int transfer = Mathf.Min(amount, health);
                transfer = Mathf.Min(transfer, other.MaxHealth - other.Health);

                if (transfer <= 0)
                    continue;

                health -= transfer;
                other.Health += transfer;
                return transfer;
            }
        }
        return 0;
    }
}

