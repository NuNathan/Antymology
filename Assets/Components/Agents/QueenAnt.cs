using UnityEngine;
using Antymology.Terrain;

// Queen ant behavior, drops pheromone, places nest blocks, and wanders randomly
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

    // public accessors for genes so EvolutionManager can read/write them
    public int PlaceNestHealthThreshold
    {
        get => placeNestHealthThreshold;
        set => placeNestHealthThreshold = Mathf.Clamp(value, MAX_HEALTH / 2, MAX_HEALTH * 9 / 10);
    }

    protected override void Start()
    {
        base.Start();
        maxDropHeight = 2f; // queen won't drop more than 2 blocks
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

}

