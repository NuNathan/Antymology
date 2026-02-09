using UnityEngine;
using Antymology.Terrain;

// Queen ant behavior, drops pheromone, places nest blocks, and wanders randomly
public class QueenAnt : AntBase
{
    [Header("Queen Pheromone")]
    // amount of pheromone the queen deposits into the air block at her position each tick
    public double queenPheromoneDropAmount = 10.0;

    [Header("Queen Visuals")]
    public float pillarHeight = 20f;

    protected override void Start()
    {
        base.Start();
        SetupQueenVisuals();
    }

    void FixedUpdate()
    {
        UpdateGroundHeight();
        SnapToGround();

        // random movement
        if (isGrounded)
        {
            TryMove();
        }

        // place nest blocks when healthy
        if (health > maxHealth * 3 / 4)
        {
            TryPlaceNest();
        }

        // drop pheromone every tick
        DropQueenPheromone();

        // eat mulch if health is low enough for a full munch
        if (health < maxHealth - healthPerMulch)
        {
            TryEatMulch();
        }

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
        pillar.transform.SetParent(transform);
        pillar.transform.localPosition = new Vector3(0f, pillarHeight / 2f, 0f);
        pillar.transform.localScale = new Vector3(0.002f, pillarHeight / 2f, 0.002f);

        // make the pillar gold and remove its collider so it doesnt interfere with raycasts
        Destroy(pillar.GetComponent<Collider>());
        pillar.GetComponent<MeshRenderer>().material.color = new Color(1f, 0.84f, 0f);
    }

    // Places a nest block directly below the queen, costing 1/3 of max health
    private bool TryPlaceNest()
    {
        int blockX = Mathf.FloorToInt(transform.position.x);
        int blockY = Mathf.FloorToInt(transform.position.y - 0.1f);
        int blockZ = Mathf.FloorToInt(transform.position.z);

        AbstractBlock blockBelow = WorldManager.Instance.GetBlock(blockX, blockY, blockZ);

        if (blockBelow is AirBlock || blockBelow is NestBlock)
            return false;

        WorldManager.Instance.SetBlock(blockX, blockY, blockZ, new NestBlock());

        health -= maxHealth / 3;

        return true;
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

