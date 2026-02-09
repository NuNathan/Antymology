using UnityEngine;
using System.Collections.Generic;
using Antymology.Terrain;

// Worker ant behavior driven by a health-based ruleset:
// - health >= feedQueenHealthThreshold: seek the queen via pheromone trail and share food
// - health < feedQueenHealthThreshold: use neighbourhood ruleset to decide action
public class WorkerAnt : AntBase
{
    // health threshold at which the worker stops normal activity and seeks the queen (75% of max)
    private int feedQueenHealthThreshold = MAX_HEALTH * 3 / 4;

    // number of distinct block types used to encode neighbourhood (air excluded)
    public const int NUM_BLOCK_TYPES = 6;

    // number of blocks in the immediate touching neighbourhood
    public const int NEIGHBOURHOOD_SIZE = 5;

    // total number of ruleset entries (6^5 = 7776)
    public static readonly int TOTAL_RULES = (int)Mathf.Pow(NUM_BLOCK_TYPES, NEIGHBOURHOOD_SIZE);

    // neighbourhood-based ruleset: maps an encoded pattern of the 5 touching blocks
    // to an action (0 = do nothing, 1 = eat mulch, 2 = dig)
    // Pre-populated at spawn with all 6^5 = 7776 combinations
    private Dictionary<string, int> ruleset = new Dictionary<string, int>();

    // fitness: total energy given to the queen during this ant's lifetime
    private int totalEnergyGiven = 0;
    public int TotalEnergyGiven => totalEnergyGiven;

    // expose mulch eaten count for fitness evaluation
    public int MulchEaten => mulchEaten;

    // probability of making a random move instead of following pheromone (anti-trapping)
    private const float STOCHASTIC_MOVE_CHANCE = 0.12f;

    // amount of worker pheromone deposited per tick while seeking the queen
    private const double WORKER_PHEROMONE_DROP = 10.0;

    // tracks whether the ant successfully moved this frame (used to gate pheromone deposition)
    private bool movedThisFrame = false;

    // public accessors for genes so EvolutionManager can read/write them
    public int FeedQueenHealthThreshold
    {
        get => feedQueenHealthThreshold;
        set => feedQueenHealthThreshold = value;
    }

    public Dictionary<string, int> Ruleset => ruleset;

    protected override void Start()
    {
        base.Start();
        moveSpeed *= 1.35f; // workers move 1.35x faster than base speed
        maxDropHeight = 2f;  // workers won't drop more than 2 blocks
        // only generate random ruleset if EvolutionManager hasn't injected one
        if (ruleset.Count == 0)
            InitializeRuleset();
    }

    void Update()
    {
        UpdateGroundHeight();
        SnapToGround();

        if (health >= feedQueenHealthThreshold)
        {
            // --- Feeding state: find the queen and deliver food ---
            // no eating, no digging, only seek queen and share health
            QueenAnt queen = FindQueen();
            movedThisFrame = false;

            if (isGrounded)
            {
                Vector3 posBefore = transform.position;

                // stochastic movement: small chance of random move to escape local maxima
                if (Random.value < STOCHASTIC_MOVE_CHANCE)
                {
                    TryMove();
                }
                // try pheromone-guided movement first, fall back to direct movement
                else if (!MoveTowardPheromone())
                {
                    if (queen != null)
                        MoveTowardQueen(queen);
                    else
                        TryMove(); // wander if no queen and no pheromone
                }

                // only count as moved if position actually changed
                movedThisFrame = (transform.position - posBefore).sqrMagnitude > 0.0001f;
            }

            // only deposit worker trail pheromone if the ant actually moved
            // this prevents pheromone buildup at stuck/crowded positions
            if (movedThisFrame)
                DropWorkerPheromone();

            if (queen != null)
                TryShareHealthWith(queen);
        }
        else
        {
            // --- Normal state: wander + neighbourhood ruleset action ---
            if (isGrounded)
            {
                TryMove();
            }

            ExecuteNeighbourhoodAction();
        }

        ApplyHealthDecay();
    }

    #region Queen Seeking

    // Finds the queen ant in the scene, returns null if no queen exists
    private QueenAnt FindQueen()
    {
        QueenAnt[] queens = FindObjectsByType<QueenAnt>(FindObjectsSortMode.None);
        if (queens.Length > 0)
            return queens[0];
        return null;
    }

    // Moves the worker ant directly toward the queens position
    private void MoveTowardQueen(QueenAnt queen)
    {
        if (!isGrounded || queen == null)
            return;

        Vector3 dirToQueen = queen.transform.position - transform.position;
        dirToQueen.y = 0f;

        if (dirToQueen.sqrMagnitude < 0.01f)
            return;

        currentDirection = dirToQueen.normalized;
        MoveInDirection(currentDirection);
    }

    // Samples combined queen + worker pheromone in the 8 horizontal neighboring air blocks
    // (4 cardinal + 4 diagonal) and moves toward the highest combined concentration.
    // Uses the current block's pheromone as a baseline — only moves if a neighbor is strictly
    // better, preventing ants from oscillating around their own pheromone deposits.
    // Adds random tie-breaking so co-located ants don't all pick the same direction.
    private bool MoveTowardPheromone()
    {
        if (WorldManager.Instance == null)
            return false;

        int blockX = Mathf.FloorToInt(transform.position.x);
        int blockY = Mathf.FloorToInt(transform.position.y - 0.1f) + 1; // air layer above ground
        int blockZ = Mathf.FloorToInt(transform.position.z);

        // read pheromone at the ant's current block as a baseline
        // only move to a neighbor if it has MORE pheromone than here
        double currentPheromone = 0;
        AbstractBlock currentBlock = WorldManager.Instance.GetBlock(blockX, blockY, blockZ);
        if (currentBlock is AirBlock currentAir)
            currentPheromone = currentAir.QueenPheromone + currentAir.WorkerPheromone;

        // sample 8 horizontal neighbors: 4 cardinal + 4 diagonal
        int[] dx = { -1, 1, 0, 0, -1, 1, -1, 1 };
        int[] dz = { 0, 0, -1, 1, -1, -1, 1, 1 };
        Vector3[] dirs =
        {
            Vector3.left,                              // -x
            Vector3.right,                             // +x
            Vector3.back,                              // -z
            Vector3.forward,                           // +z
            new Vector3(-1, 0, -1).normalized,         // -x, -z
            new Vector3(1, 0, -1).normalized,          // +x, -z
            new Vector3(-1, 0, 1).normalized,          // -x, +z
            new Vector3(1, 0, 1).normalized            // +x, +z
        };

        double bestPheromone = currentPheromone; // baseline = current block
        int candidateCount = 0;

        // first pass: find the best pheromone value among neighbors
        for (int i = 0; i < 8; i++)
        {
            AbstractBlock neighbor = WorldManager.Instance.GetBlock(
                blockX + dx[i], blockY, blockZ + dz[i]);

            if (neighbor is AirBlock airBlock)
            {
                double pheromone = airBlock.QueenPheromone + airBlock.WorkerPheromone;
                if (pheromone > bestPheromone)
                    bestPheromone = pheromone;
            }
        }

        // no neighbor is better than where we already are
        if (bestPheromone <= currentPheromone)
            return false;

        // second pass: collect all neighbors within 10% of the best value (tie-breaking pool)
        // then pick one at random so co-located ants spread out
        double threshold = bestPheromone * 0.9;
        Vector3 chosenDirection = Vector3.zero;

        for (int i = 0; i < 8; i++)
        {
            AbstractBlock neighbor = WorldManager.Instance.GetBlock(
                blockX + dx[i], blockY, blockZ + dz[i]);

            if (neighbor is AirBlock airBlock)
            {
                double pheromone = airBlock.QueenPheromone + airBlock.WorkerPheromone;
                if (pheromone >= threshold)
                {
                    candidateCount++;
                    // reservoir sampling: each candidate has equal probability of being chosen
                    if (Random.Range(0, candidateCount) == 0)
                        chosenDirection = dirs[i];
                }
            }
        }

        if (candidateCount == 0)
            return false;

        currentDirection = chosenDirection;
        MoveInDirection(currentDirection);
        return true;
    }

    // Deposits worker trail pheromone into the air block at the worker's position.
    // Called every tick while the worker is in feeding state (seeking the queen).
    // Creates a reinforcing trail that other workers can follow toward the queen.
    private void DropWorkerPheromone()
    {
        if (WorldManager.Instance == null)
            return;

        int blockX = Mathf.FloorToInt(transform.position.x);
        int blockY = Mathf.FloorToInt(transform.position.y - 0.1f) + 1;
        int blockZ = Mathf.FloorToInt(transform.position.z);

        AbstractBlock block = WorldManager.Instance.GetBlock(blockX, blockY, blockZ);

        if (block is AirBlock airBlock)
        {
            airBlock.WorkerPheromone += WORKER_PHEROMONE_DROP;
        }
    }

    #endregion

    #region Health Sharing

    // Shares excess health above the feeding threshold with the target when on the same block
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

        // share excess health above the feeding threshold
        int excess = health - feedQueenHealthThreshold;
        if (excess <= 0)
            return 0;

        // cap by what the target can accept
        int transfer = Mathf.Min(excess, target.MaxHealth - target.Health);
        if (transfer <= 0)
            return 0;

        health -= transfer;
        target.Health += transfer;
        totalEnergyGiven += transfer;
        return transfer;
    }

    #endregion

    #region Neighbourhood Ruleset

    // Pre-populates the ruleset with a random action for every possible 5-block combination.
    // 6 block types ^ 5 positions = 7776 entries.
    private void InitializeRuleset()
    {
        int totalCombinations = (int)Mathf.Pow(NUM_BLOCK_TYPES, NEIGHBOURHOOD_SIZE); // 7776
        char[] pattern = new char[NEIGHBOURHOOD_SIZE];

        for (int i = 0; i < totalCombinations; i++)
        {
            int value = i;
            for (int pos = NEIGHBOURHOOD_SIZE - 1; pos >= 0; pos--)
            {
                pattern[pos] = (char)('0' + (value % NUM_BLOCK_TYPES));
                value /= NUM_BLOCK_TYPES;
            }

            string key = new string(pattern);
            ruleset[key] = Random.Range(0, 3); // 0 = do nothing, 1 = eat mulch, 2 = dig
        }
    }

    // Reads the 5-block touching neighbourhood, looks up the pre-assigned action, and executes it.
    // Blocks: below + 4 cardinal neighbors at same level (left, right, back, forward).
    // Actions: 0 = do nothing, 1 = eat mulch, 2 = dig
    private void ExecuteNeighbourhoodAction()
    {
        string key = EncodeNeighbourhood();

        if (!ruleset.TryGetValue(key, out int action))
            return; // key not in ruleset, skip this tick

        switch (action)
        {
            case 1:
            case 2:
                TryDig();
                break;
            // case 0: do nothing
        }
    }

    // Encodes the 5 touching blocks into a string key for dictionary lookup.
    // Order: below, left(-x), right(+x), back(-z), forward(+z)
    private string EncodeNeighbourhood()
    {
        int centerX = Mathf.FloorToInt(transform.position.x);
        int centerY = Mathf.FloorToInt(transform.position.y - 0.1f);
        int centerZ = Mathf.FloorToInt(transform.position.z);

        char[] encoded = new char[NEIGHBOURHOOD_SIZE];

        // below
        encoded[0] = BlockToChar(WorldManager.Instance.GetBlock(centerX, centerY - 1, centerZ));
        // left (-x)
        encoded[1] = BlockToChar(WorldManager.Instance.GetBlock(centerX - 1, centerY, centerZ));
        // right (+x)
        encoded[2] = BlockToChar(WorldManager.Instance.GetBlock(centerX + 1, centerY, centerZ));
        // back (-z)
        encoded[3] = BlockToChar(WorldManager.Instance.GetBlock(centerX, centerY, centerZ - 1));
        // forward (+z)
        encoded[4] = BlockToChar(WorldManager.Instance.GetBlock(centerX, centerY, centerZ + 1));

        return new string(encoded);
    }

    // Maps a block to a single character for neighbourhood encoding (air excluded).
    // Air blocks are treated as type 0 (same as grass fallback — they shouldn't appear
    // as touching blocks in normal gameplay since ants stand on solid ground).
    // 0=Grass, 1=Stone, 2=Mulch, 3=Acidic, 4=Container, 5=Nest
    private char BlockToChar(AbstractBlock block)
    {
        if (block is GrassBlock)     return '0';
        if (block is StoneBlock)     return '1';
        if (block is MulchBlock)     return '2';
        if (block is AcidicBlock)    return '3';
        if (block is ContainerBlock) return '4';
        if (block is NestBlock)      return '5';
        return '0'; // fallback for air or unknown types
    }

    #endregion
}

