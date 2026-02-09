using UnityEngine;
using System.Collections.Generic;
using Antymology.Terrain;

// Queen ant behavior, drops pheromone, places nest blocks, and wanders randomly
// Uses neighbourhood ruleset when health < placeNestHealthThreshold
public class QueenAnt : AntBase
{
    [Header("Queen Pheromone")]
    // amount of pheromone the queen deposits into the air block at her position each tick
    public double queenPheromoneDropAmount = 150.0;

    [Header("Queen Visuals")]
    public float pillarHeight = 20f;

    // queen moves once every movementInterval ticks (1/3 the rate of workers)
    private int movementInterval = 3;
    private int tickCounter = 0;

    // evolvable: health threshold before placing nest block (default 75%, constrained 50%-90%)
    private int placeNestHealthThreshold = MAX_HEALTH * 3 / 4;

    // fitness: number of nest blocks placed during this queen's lifetime
    private int nestsPlaced = 0;
    public int NestsPlaced => nestsPlaced;

    // neighbourhood-based ruleset (same structure as WorkerAnt)
    private Dictionary<string, int> ruleset = new Dictionary<string, int>();

    // public accessors for genes so EvolutionManager can read/write them
    public int PlaceNestHealthThreshold
    {
        get => placeNestHealthThreshold;
        set => placeNestHealthThreshold = Mathf.Clamp(value, MAX_HEALTH / 2, MAX_HEALTH * 9 / 10);
    }

    public Dictionary<string, int> Ruleset => ruleset;

    protected override void Start()
    {
        base.Start();
        maxDropHeight = 2f; // queen won't drop more than 2 blocks
        // only generate random ruleset if EvolutionManager hasn't injected one
        if (ruleset.Count == 0)
            InitializeRuleset();
        SetupQueenVisuals();
    }

    void Update()
    {
        UpdateGroundHeight();
        SnapToGround();

        // random movement at 1/3 the rate of workers
        tickCounter++;
        if (tickCounter >= movementInterval)
        {
            tickCounter = 0;
            if (isGrounded)
            {
                TryMove();
            }
        }

        // place nest block when health is high enough
        if (health >= placeNestHealthThreshold)
        {
            TryPlaceNest();
        }
        else
        {
            // when health is low, use neighbourhood ruleset
            ExecuteNeighbourhoodAction();
        }

        // drop pheromone every tick
        DropQueenPheromone();

        ApplyHealthDecay();
    }

    // Sets the queen ant to gold and creates a tall visible pillar above it
    private void SetupQueenVisuals()
    {
        MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.material.color = new Color(1f, 0.84f, 0f); // gold
        }

        // create a tall pillar above the queen for easy visibility
        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.transform.SetParent(transform, false);
        pillar.transform.localPosition = new Vector3(0f, pillarHeight / 2f, 0f);
        pillar.transform.localScale = new Vector3(0.002f, pillarHeight / 2f, 0.002f);

        // make the pillar gold and remove its collider so it doesnt interfere with raycasts
        Destroy(pillar.GetComponent<Collider>());
        pillar.GetComponent<MeshRenderer>().material.color = new Color(1f, 0.84f, 0f);
    }

    // Places a nest block into an adjacent air space at ground level, costing 1/3 of max health
    // Checks 4 cardinal neighbors at the level below the queen
    private bool TryPlaceNest()
    {
        int centerX = Mathf.FloorToInt(transform.position.x);
        int belowY = Mathf.FloorToInt(transform.position.y - 0.1f);
        int centerZ = Mathf.FloorToInt(transform.position.z);

        int[] dx = { -1, 1, 0, 0 };
        int[] dz = { 0, 0, -1, 1 };

        for (int i = 0; i < 4; i++)
        {
            int checkX = centerX + dx[i];
            int checkZ = centerZ + dz[i];
            AbstractBlock neighbor = WorldManager.Instance.GetBlock(checkX, belowY, checkZ);

            if (neighbor is AirBlock)
            {
                WorldManager.Instance.SetBlock(checkX, belowY, checkZ, new NestBlock());
                health -= maxHealth / 3;
                nestsPlaced++;
                return true;
            }
        }

        return false; // no available air space nearby
    }

    // Deposits pheromone into the air block above the queens position
    private void DropQueenPheromone()
    {
        int blockX = Mathf.FloorToInt(transform.position.x);
        int blockY = Mathf.FloorToInt(transform.position.y - 0.1f) + 1;
        int blockZ = Mathf.FloorToInt(transform.position.z);

        AbstractBlock block = WorldManager.Instance.GetBlock(blockX, blockY, blockZ);

        if (block is AirBlock airBlock)
        {
            airBlock.QueenPheromone += queenPheromoneDropAmount;
        }
    }

    #region Neighbourhood Ruleset

    // Pre-populates the ruleset with a random action for every possible 5-block combination.
    private void InitializeRuleset()
    {
        int totalCombinations = WorkerAnt.TOTAL_RULES;
        char[] pattern = new char[WorkerAnt.NEIGHBOURHOOD_SIZE];

        for (int i = 0; i < totalCombinations; i++)
        {
            int value = i;
            for (int pos = WorkerAnt.NEIGHBOURHOOD_SIZE - 1; pos >= 0; pos--)
            {
                pattern[pos] = (char)('0' + (value % WorkerAnt.NUM_BLOCK_TYPES));
                value /= WorkerAnt.NUM_BLOCK_TYPES;
            }

            string key = new string(pattern);
            ruleset[key] = Random.Range(0, 3); // 0 = do nothing, 1 = eat mulch, 2 = dig
        }
    }

    // Reads the 5-block touching neighbourhood, looks up the pre-assigned action, and executes it.
    // Queen no longer digs — all actions are effectively "do nothing".
    private void ExecuteNeighbourhoodAction()
    {
        // neighbourhood ruleset is still maintained for evolution, but queen doesn't dig
    }

    // Encodes the 5 touching blocks into a string key for dictionary lookup.
    private string EncodeNeighbourhood()
    {
        int centerX = Mathf.FloorToInt(transform.position.x);
        int centerY = Mathf.FloorToInt(transform.position.y - 0.1f);
        int centerZ = Mathf.FloorToInt(transform.position.z);

        char[] encoded = new char[WorkerAnt.NEIGHBOURHOOD_SIZE];

        encoded[0] = BlockToChar(WorldManager.Instance.GetBlock(centerX, centerY - 1, centerZ));
        encoded[1] = BlockToChar(WorldManager.Instance.GetBlock(centerX - 1, centerY, centerZ));
        encoded[2] = BlockToChar(WorldManager.Instance.GetBlock(centerX + 1, centerY, centerZ));
        encoded[3] = BlockToChar(WorldManager.Instance.GetBlock(centerX, centerY, centerZ - 1));
        encoded[4] = BlockToChar(WorldManager.Instance.GetBlock(centerX, centerY, centerZ + 1));

        return new string(encoded);
    }

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

