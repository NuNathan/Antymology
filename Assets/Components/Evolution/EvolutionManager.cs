using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Antymology.Terrain;

public class EvolutionManager : Singleton<EvolutionManager>
{
    private int generation = 1;
    public int Generation => generation;

    private int bestNestCount = 0;
    public int BestNestCount => bestNestCount;

    // queen breeding pool: top queens of all time
    private List<QueenGenes> topQueens = new List<QueenGenes>();
    private const int MAX_TOP_QUEENS = 5;

    // worker breeding pool: top all-time + random from last generation
    private List<WorkerGenes> topWorkers = new List<WorkerGenes>();
    private const int MAX_TOP_WORKERS = 20;
    private List<WorkerGenes> lastGenWorkers = new List<WorkerGenes>();
    private const int RANDOM_FROM_LAST_GEN = 10;
    private const int ELITE_CARRY_OVER = 5;

    // evolution parameters
    private const float RULESET_MUTATION_RATE = 0.05f;
    private const int THRESHOLD_MUTATION_RANGE = AntBase.MAX_HEALTH / 20;
    private const int TOURNAMENT_SIZE = 3;
    private const int MULCH_FITNESS_WEIGHT = 50;

    private bool isTransitioning = false;

    #region Gene Data Structures

    public struct QueenGenes
    {
        public int fitness;
        public int placeNestHealthThreshold;
    }

    public struct WorkerGenes
    {
        public int fitness;
        public int feedQueenHealthThreshold;
        public Dictionary<string, int> ruleset;
    }

    #endregion

    #region Ant Death Reporting

    /// <summary> Called by AntBase.Die(), queen death triggers a new generation </summary>
    public void ReportDeath(AntBase ant)
    {
        if (isTransitioning) return;

        if (ant is QueenAnt queen)
        {
            isTransitioning = true;
            RecordQueen(queen);
            RecordSurvivingWorkers();
            generation++;
            SpawnNextGeneration();
            isTransitioning = false;
        }
        else if (ant is WorkerAnt worker)
        {
            RecordWorker(worker);
        }
    }

    /// <summary> Saves queen genes into the top-queens breeding pool </summary>
    private void RecordQueen(QueenAnt queen)
    {
        QueenGenes genes = new QueenGenes
        {
            fitness = queen.NestsPlaced,
            placeNestHealthThreshold = queen.PlaceNestHealthThreshold
        };

        topQueens.Add(genes);
        topQueens = topQueens.OrderByDescending(q => q.fitness).Take(MAX_TOP_QUEENS).ToList();

        if (queen.NestsPlaced > bestNestCount)
            bestNestCount = queen.NestsPlaced;
    }

    /// <summary> Saves worker genes into both the last-gen and top-workers pools </summary>
    private void RecordWorker(WorkerAnt worker)
    {
        WorkerGenes genes = new WorkerGenes
        {
            fitness = worker.TotalEnergyGiven + worker.MulchEaten * MULCH_FITNESS_WEIGHT,
            feedQueenHealthThreshold = worker.FeedQueenHealthThreshold,
            ruleset = new Dictionary<string, int>(worker.Ruleset)
        };

        lastGenWorkers.Add(genes);
        topWorkers.Add(genes);
        topWorkers = topWorkers.OrderByDescending(w => w.fitness).Take(MAX_TOP_WORKERS).ToList();
    }

    /// <summary> Records all still-living workers before the generation resets </summary>
    private void RecordSurvivingWorkers()
    {
        WorkerAnt[] livingWorkers = FindObjectsByType<WorkerAnt>(FindObjectsSortMode.None);
        foreach (WorkerAnt w in livingWorkers)
        {
            RecordWorker(w);
        }
    }

    #endregion

    #region Spawning Next Generation

    /// <summary> Destroys remaining ants, resets the world, and spawns evolved queen + workers </summary>
    private void SpawnNextGeneration()
    {
        WorkerAnt[] remaining = FindObjectsByType<WorkerAnt>(FindObjectsSortMode.None);
        foreach (WorkerAnt w in remaining)
            Destroy(w.gameObject);

        WorldManager.Instance.ResetWorld();

        Vector3 spawnPos = WorldManager.Instance.FindValidSpawnPosition();
        if (spawnPos == Vector3.zero) return;

        SpawnEvolvedQueen(spawnPos);
        SpawnEvolvedWorkers(spawnPos, 20);
        lastGenWorkers.Clear();
    }

    /// <summary> Spawns a queen with threshold evolved from the top-queens pool </summary>
    private void SpawnEvolvedQueen(Vector3 spawnPos)
    {
        GameObject queenPrefab = WorldManager.Instance.queenAntPrefab;
        if (queenPrefab == null) return;

        GameObject queenObj = Instantiate(queenPrefab, spawnPos, Quaternion.identity);
        QueenAnt queen = queenObj.GetComponent<QueenAnt>();

        if (topQueens.Count >= 2)
        {
            int idx1 = TournamentSelectQueen(topQueens);
            int idx2 = TournamentSelectQueen(topQueens, idx1);
            QueenGenes parent1 = topQueens[idx1];
            QueenGenes parent2 = topQueens[idx2];

            int childThreshold = (parent1.placeNestHealthThreshold + parent2.placeNestHealthThreshold) / 2;
            childThreshold += Random.Range(-THRESHOLD_MUTATION_RANGE, THRESHOLD_MUTATION_RANGE + 1);
            childThreshold = Mathf.Clamp(childThreshold, AntBase.MAX_HEALTH / 2, AntBase.MAX_HEALTH * 9 / 10);

            queen.PlaceNestHealthThreshold = childThreshold;
        }
    }

    /// <summary> Spawns workers with genes bred from the breeding pool, plus elite carry-overs </summary>
    private void SpawnEvolvedWorkers(Vector3 spawnPos, int count)
    {
        GameObject workerPrefab = WorldManager.Instance.antPrefab;
        if (workerPrefab == null) return;

        // breeding pool: top all-time + random sample from last generation
        List<WorkerGenes> breedingPool = new List<WorkerGenes>(topWorkers);

        if (lastGenWorkers.Count > 0)
        {
            List<WorkerGenes> shuffled = lastGenWorkers.OrderBy(_ => Random.value).ToList();
            int randomCount = Mathf.Min(RANDOM_FROM_LAST_GEN, shuffled.Count);
            breedingPool.AddRange(shuffled.Take(randomCount));
        }

        float spawnRadius = 5f;

        int eliteCount = Mathf.Min(ELITE_CARRY_OVER, topWorkers.Count);

        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, 360f);
            float distance = Random.Range(0f, spawnRadius);
            float offsetX = Mathf.Cos(angle * Mathf.Deg2Rad) * distance;
            float offsetZ = Mathf.Sin(angle * Mathf.Deg2Rad) * distance;

            Vector3 workerPos = new Vector3(
                spawnPos.x + offsetX,
                spawnPos.y + 10f,
                spawnPos.z + offsetZ);

            float facingAngle = Random.Range(0f, 360f);
            Quaternion rotation = Quaternion.Euler(0f, facingAngle, 0f);

            GameObject workerObj = Instantiate(workerPrefab, workerPos, rotation);
            WorkerAnt worker = workerObj.GetComponent<WorkerAnt>();

            if (i < eliteCount)
            {
                // elite carry-over: top performers injected unchanged
                WorkerGenes elite = topWorkers[i];
                foreach (var kvp in elite.ruleset)
                    worker.Ruleset[kvp.Key] = kvp.Value;
                worker.FeedQueenHealthThreshold = elite.feedQueenHealthThreshold;
            }
            else if (breedingPool.Count >= 2)
            {
                int idx1 = TournamentSelectWorker(breedingPool);
                int idx2 = TournamentSelectWorker(breedingPool, idx1);
                WorkerGenes parent1 = breedingPool[idx1];
                WorkerGenes parent2 = breedingPool[idx2];

                Dictionary<string, int> childRuleset = CrossoverRuleset(parent1.ruleset, parent2.ruleset);
                MutateRuleset(childRuleset);

                int childThreshold = (parent1.feedQueenHealthThreshold + parent2.feedQueenHealthThreshold) / 2;
                childThreshold += Random.Range(-THRESHOLD_MUTATION_RANGE, THRESHOLD_MUTATION_RANGE + 1);
                childThreshold = Mathf.Clamp(childThreshold, AntBase.MAX_HEALTH / 5, AntBase.MAX_HEALTH * 19 / 20);

                foreach (var kvp in childRuleset)
                    worker.Ruleset[kvp.Key] = kvp.Value;
                worker.FeedQueenHealthThreshold = childThreshold;
            }
        }
    }

    #endregion

    #region Crossover and Mutation

    /// <summary> Uniform crossover: each rule randomly picked from one parent </summary>
    private Dictionary<string, int> CrossoverRuleset(
        Dictionary<string, int> parent1, Dictionary<string, int> parent2)
    {
        Dictionary<string, int> child = new Dictionary<string, int>();

        foreach (var kvp in parent1)
        {
            if (Random.value < 0.5f && parent2.ContainsKey(kvp.Key))
                child[kvp.Key] = parent2[kvp.Key];
            else
                child[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in parent2)
        {
            if (!child.ContainsKey(kvp.Key))
                child[kvp.Key] = kvp.Value;
        }

        return child;
    }

    /// <summary> Randomly mutates a small percentage of rules to new actions </summary>
    private void MutateRuleset(Dictionary<string, int> ruleset)
    {
        List<string> keys = new List<string>(ruleset.Keys);
        foreach (string key in keys)
        {
            if (Random.value < RULESET_MUTATION_RATE)
            {
                ruleset[key] = Random.Range(0, 3);
            }
        }
    }

    #endregion

    #region Tournament Selection

    /// <summary> Tournament selection for queens, re-rolls once on excludeIndex collision then wraps </summary>
    private int TournamentSelectQueen(List<QueenGenes> pool, int excludeIndex = -1)
    {
        int bestIndex = -1;
        int bestFitness = -1;

        for (int i = 0; i < TOURNAMENT_SIZE; i++)
        {
            int candidate = Random.Range(0, pool.Count);

            if (candidate == excludeIndex)
            {
                candidate = Random.Range(0, pool.Count);
                if (candidate == excludeIndex)
                    candidate = (candidate + 1) % pool.Count;
            }

            if (pool[candidate].fitness > bestFitness)
            {
                bestFitness = pool[candidate].fitness;
                bestIndex = candidate;
            }
        }

        return bestIndex;
    }

    /// <summary> Tournament selection for workers, re-rolls once on excludeIndex collision then wraps </summary>
    private int TournamentSelectWorker(List<WorkerGenes> pool, int excludeIndex = -1)
    {
        int bestIndex = -1;
        int bestFitness = -1;

        for (int i = 0; i < TOURNAMENT_SIZE; i++)
        {
            int candidate = Random.Range(0, pool.Count);

            if (candidate == excludeIndex)
            {
                candidate = Random.Range(0, pool.Count);
                if (candidate == excludeIndex)
                    candidate = (candidate + 1) % pool.Count;
            }

            if (pool[candidate].fitness > bestFitness)
            {
                bestFitness = pool[candidate].fitness;
                bestIndex = candidate;
            }
        }

        return bestIndex;
    }

    #endregion
}