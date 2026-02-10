using UnityEngine;
using System.Collections.Generic;
using Antymology.Terrain;

public class WorkerAnt : AntBase
{
    private int feedQueenHealthThreshold = MAX_HEALTH * 3 / 4;

    public const int NUM_BLOCK_TYPES = 6;
    public const int NEIGHBOURHOOD_SIZE = 5;
    public static readonly int TOTAL_RULES = (int)Mathf.Pow(NUM_BLOCK_TYPES, NEIGHBOURHOOD_SIZE);

    // ruleset: encoded 5-block pattern -> action (0 = do nothing, 1 = dig, 2 = dig)
    private Dictionary<string, int> ruleset = new Dictionary<string, int>();

    private int totalEnergyGiven = 0;
    public int TotalEnergyGiven => totalEnergyGiven;
    public int MulchEaten => mulchEaten;

    private const float STOCHASTIC_MOVE_CHANCE = 0.24f;
    private const double WORKER_PHEROMONE_DROP = 10.0;
    private bool movedThisFrame = false;

    public int FeedQueenHealthThreshold
    {
        get => feedQueenHealthThreshold;
        set => feedQueenHealthThreshold = value;
    }

    public Dictionary<string, int> Ruleset => ruleset;

    protected override void Start()
    {
        base.Start();
        moveSpeed *= 1.35f;
        maxDropHeight = 2f;
        if (ruleset.Count == 0)
            InitializeRuleset();
    }

    void Update()
    {
        UpdateGroundHeight();
        SnapToGround();

        if (health >= feedQueenHealthThreshold)
        {
            QueenAnt queen = FindQueen();
            movedThisFrame = false;

            if (isGrounded)
            {
                Vector3 posBefore = transform.position;

                if (Random.value < STOCHASTIC_MOVE_CHANCE)
                {
                    TryMove();
                }
                else if (!MoveTowardPheromone())
                {
                    if (queen != null)
                        MoveTowardQueen(queen);
                    else
                        TryMove();
                }

                movedThisFrame = (transform.position - posBefore).sqrMagnitude > 0.0001f;
            }

            // only deposit trail pheromone if actually moved (prevents buildup when stuck)
            if (movedThisFrame)
                DropWorkerPheromone();

            if (queen != null)
                TryShareHealthWith(queen);
        }
        else
        {
            if (isGrounded)
                TryMove();

            ExecuteNeighbourhoodAction();
        }

        ApplyHealthDecay();
    }

    #region Queen Seeking

    private QueenAnt FindQueen()
    {
        QueenAnt[] queens = FindObjectsByType<QueenAnt>(FindObjectsSortMode.None);
        if (queens.Length > 0)
            return queens[0];
        return null;
    }

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

    // Moves toward highest pheromone among 8 horizontal neighbors
    // Uses current block as baseline (only moves if neighbor is strictly better)
    // Random tie-breaking among neighbors within 10% of best so co-located ants spread out
    private bool MoveTowardPheromone()
    {
        if (WorldManager.Instance == null)
            return false;

        int blockX = Mathf.FloorToInt(transform.position.x);
        int blockY = Mathf.FloorToInt(transform.position.y - 0.1f) + 1;
        int blockZ = Mathf.FloorToInt(transform.position.z);

        double currentPheromone = 0;
        AbstractBlock currentBlock = WorldManager.Instance.GetBlock(blockX, blockY, blockZ);
        if (currentBlock is AirBlock currentAir)
            currentPheromone = currentAir.QueenPheromone + currentAir.WorkerPheromone;

        int[] dx = { -1, 1, 0, 0, -1, 1, -1, 1 };
        int[] dz = { 0, 0, -1, 1, -1, -1, 1, 1 };
        Vector3[] dirs =
        {
            Vector3.left,
            Vector3.right,
            Vector3.back,
            Vector3.forward,
            new Vector3(-1, 0, -1).normalized,
            new Vector3(1, 0, -1).normalized,
            new Vector3(-1, 0, 1).normalized,
            new Vector3(1, 0, 1).normalized
        };

        double bestPheromone = currentPheromone;
        int candidateCount = 0;

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

        if (bestPheromone <= currentPheromone)
            return false;

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

    private void DropWorkerPheromone()
    {
        if (WorldManager.Instance == null)
            return;

        int blockX = Mathf.FloorToInt(transform.position.x);
        int blockY = Mathf.FloorToInt(transform.position.y - 0.1f) + 1;
        int blockZ = Mathf.FloorToInt(transform.position.z);

        AbstractBlock block = WorldManager.Instance.GetBlock(blockX, blockY, blockZ);

        if (block is AirBlock airBlock)
            airBlock.WorkerPheromone += WORKER_PHEROMONE_DROP;
    }

    #endregion

    #region Health Sharing

    // Shares excess health above feedQueenHealthThreshold with target if on the same block
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

        if (otherX != blockX || otherY != blockY || otherZ != blockZ)
            return 0;

        int excess = health - feedQueenHealthThreshold;
        if (excess <= 0)
            return 0;

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

    private void InitializeRuleset()
    {
        int totalCombinations = (int)Mathf.Pow(NUM_BLOCK_TYPES, NEIGHBOURHOOD_SIZE);
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
            ruleset[key] = Random.Range(0, 3);
        }
    }

    // Looks up action for current neighbourhood. Actions: 0 = do nothing, 1/2 = dig
    private void ExecuteNeighbourhoodAction()
    {
        string key = EncodeNeighbourhood();

        if (!ruleset.TryGetValue(key, out int action))
            return;

        switch (action)
        {
            case 1:
            case 2:
                TryDig();
                break;
        }
    }

    // Encodes 5 touching blocks: below, left(-x), right(+x), back(-z), forward(+z)
    private string EncodeNeighbourhood()
    {
        int centerX = Mathf.FloorToInt(transform.position.x);
        int centerY = Mathf.FloorToInt(transform.position.y - 0.1f);
        int centerZ = Mathf.FloorToInt(transform.position.z);

        char[] encoded = new char[NEIGHBOURHOOD_SIZE];
        encoded[0] = BlockToChar(WorldManager.Instance.GetBlock(centerX, centerY - 1, centerZ));
        encoded[1] = BlockToChar(WorldManager.Instance.GetBlock(centerX - 1, centerY, centerZ));
        encoded[2] = BlockToChar(WorldManager.Instance.GetBlock(centerX + 1, centerY, centerZ));
        encoded[3] = BlockToChar(WorldManager.Instance.GetBlock(centerX, centerY, centerZ - 1));
        encoded[4] = BlockToChar(WorldManager.Instance.GetBlock(centerX, centerY, centerZ + 1));
        return new string(encoded);
    }

    // 0=Grass, 1=Stone, 2=Mulch, 3=Acidic, 4=Container, 5=Nest
    private char BlockToChar(AbstractBlock block)
    {
        if (block is GrassBlock)     return '0';
        if (block is StoneBlock)     return '1';
        if (block is MulchBlock)     return '2';
        if (block is AcidicBlock)    return '3';
        if (block is ContainerBlock) return '4';
        if (block is NestBlock)      return '5';
        return '0';
    }

    #endregion
}

