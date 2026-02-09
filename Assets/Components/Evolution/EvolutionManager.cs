using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Antymology.Terrain;

// Manages generational cycling, breeding pools, crossover, and mutation for both queens and workers.
// Generation advances when the queen dies. New queen + workers are spawned with evolved genes.
public class EvolutionManager : Singleton<EvolutionManager>
{
    // current generation number (starts at 1)
    private int generation = 1;
    public int Generation => generation;

    // best nest count across all queens ever
    private int bestNestCount = 0;
    public int BestNestCount => bestNestCount;

    // --- Queen breeding pool: top 5 queens of all time ---
    private List<QueenGenes> topQueens = new List<QueenGenes>();
    private const int MAX_TOP_QUEENS = 5;

    // --- Worker breeding pool: top 20 of all time + 10 random from last generation ---
    private List<WorkerGenes> topWorkers = new List<WorkerGenes>();
    private const int MAX_TOP_WORKERS = 20;
    private List<WorkerGenes> lastGenWorkers = new List<WorkerGenes>();
    private const int RANDOM_FROM_LAST_GEN = 10;
    private const int ELITE_CARRY_OVER = 5; // top 5 workers carried over unchanged

    // mutation rates
    private const float RULESET_MUTATION_RATE = 0.05f; // 5% of rules mutated
    private const int THRESHOLD_MUTATION_RANGE = AntBase.MAX_HEALTH / 20; // +/- 5% of max health

    // tournament selection size
    private const int TOURNAMENT_SIZE = 3;

    // weight for mulch eaten in worker composite fitness
    private const int MULCH_FITNESS_WEIGHT = 50;

    // guard to prevent re-entrant death reporting during generational transition
    private bool isTransitioning = false;

    #region Gene Data Structures

    public struct QueenGenes
    {
        public int fitness; // nestsPlaced
        public int placeNestHealthThreshold;
        public Dictionary<string, int> ruleset;
    }

    public struct WorkerGenes
    {
        public int fitness; // composite: totalEnergyGiven + mulchEaten * MULCH_FITNESS_WEIGHT
        public int feedQueenHealthThreshold;
        public Dictionary<string, int> ruleset;
    }

    #endregion

    #region Ant Death Reporting

    // Called by AntBase.Die() before the ant is destroyed
    public void ReportDeath(AntBase ant)
    {
        if (isTransitioning) return; // ignore deaths during generational transition

        if (ant is QueenAnt queen)
        {
            isTransitioning = true;
            RecordQueen(queen);
            // all current workers also need to be recorded before respawning
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

    private void RecordQueen(QueenAnt queen)
    {
        QueenGenes genes = new QueenGenes
        {
            fitness = queen.NestsPlaced,
            placeNestHealthThreshold = queen.PlaceNestHealthThreshold,
            ruleset = new Dictionary<string, int>(queen.Ruleset)
        };

        topQueens.Add(genes);
        topQueens = topQueens.OrderByDescending(q => q.fitness).Take(MAX_TOP_QUEENS).ToList();

        if (queen.NestsPlaced > bestNestCount)
            bestNestCount = queen.NestsPlaced;
    }

    private void RecordWorker(WorkerAnt worker)
    {
        WorkerGenes genes = new WorkerGenes
        {
            fitness = worker.TotalEnergyGiven + worker.MulchEaten * MULCH_FITNESS_WEIGHT,
            feedQueenHealthThreshold = worker.FeedQueenHealthThreshold,
            ruleset = new Dictionary<string, int>(worker.Ruleset)
        };

        // add to last gen list (for random selection at end of generation)
        lastGenWorkers.Add(genes);

        // maintain top 20 all-time
        topWorkers.Add(genes);
        topWorkers = topWorkers.OrderByDescending(w => w.fitness).Take(MAX_TOP_WORKERS).ToList();
    }

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

    private void SpawnNextGeneration()
    {
        // destroy all remaining workers
        WorkerAnt[] remaining = FindObjectsByType<WorkerAnt>(FindObjectsSortMode.None);
        foreach (WorkerAnt w in remaining)
            Destroy(w.gameObject);

        // reset the world terrain for the new generation
        WorldManager.Instance.ResetWorld();

        Vector3 spawnPos = WorldManager.Instance.FindValidSpawnPosition();
        if (spawnPos == Vector3.zero) return;

        // spawn evolved queen
        SpawnEvolvedQueen(spawnPos);

        // spawn 20 evolved workers
        SpawnEvolvedWorkers(spawnPos, 20);

        // reset last gen workers for the new generation
        lastGenWorkers.Clear();
    }



    private void SpawnEvolvedQueen(Vector3 spawnPos)
    {
        GameObject queenPrefab = WorldManager.Instance.queenAntPrefab;
        if (queenPrefab == null) return;

        GameObject queenObj = Instantiate(queenPrefab, spawnPos, Quaternion.identity);
        QueenAnt queen = queenObj.GetComponent<QueenAnt>();

        if (topQueens.Count >= 2)
        {
            // tournament selection with self-mating prevention
            int idx1 = TournamentSelectQueen(topQueens);
            int idx2 = TournamentSelectQueen(topQueens, idx1);
            QueenGenes parent1 = topQueens[idx1];
            QueenGenes parent2 = topQueens[idx2];

            // crossover ruleset
            Dictionary<string, int> childRuleset = CrossoverRuleset(parent1.ruleset, parent2.ruleset);
            MutateRuleset(childRuleset);

            // crossover threshold (average + small mutation, clamped 50%-90% of max health)
            int childThreshold = (parent1.placeNestHealthThreshold + parent2.placeNestHealthThreshold) / 2;
            childThreshold += Random.Range(-THRESHOLD_MUTATION_RANGE, THRESHOLD_MUTATION_RANGE + 1);
            childThreshold = Mathf.Clamp(childThreshold, AntBase.MAX_HEALTH / 2, AntBase.MAX_HEALTH * 9 / 10);

            // inject genes before Start() runs
            foreach (var kvp in childRuleset)
                queen.Ruleset[kvp.Key] = kvp.Value;
            queen.PlaceNestHealthThreshold = childThreshold;
        }
        // else: first generation, queen uses random genes from Start()
    }

    private void SpawnEvolvedWorkers(Vector3 spawnPos, int count)
    {
        GameObject workerPrefab = WorldManager.Instance.antPrefab;
        if (workerPrefab == null) return;

        // build breeding pool: top 20 all-time + 10 random from last gen
        List<WorkerGenes> breedingPool = new List<WorkerGenes>(topWorkers);

        if (lastGenWorkers.Count > 0)
        {
            List<WorkerGenes> shuffled = lastGenWorkers.OrderBy(_ => Random.value).ToList();
            int randomCount = Mathf.Min(RANDOM_FROM_LAST_GEN, shuffled.Count);
            breedingPool.AddRange(shuffled.Take(randomCount));
        }

        float spawnRadius = 5f;

        // number of elite workers to carry over unchanged from top all-time
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
                // carry over top performers unchanged
                WorkerGenes elite = topWorkers[i];
                foreach (var kvp in elite.ruleset)
                    worker.Ruleset[kvp.Key] = kvp.Value;
                worker.FeedQueenHealthThreshold = elite.feedQueenHealthThreshold;
            }
            else if (breedingPool.Count >= 2)
            {
                // tournament selection with self-mating prevention
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

    // Uniform crossover: for each rule, randomly pick from parent1 or parent2
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

        // add any keys only in parent2
        foreach (var kvp in parent2)
        {
            if (!child.ContainsKey(kvp.Key))
                child[kvp.Key] = kvp.Value;
        }

        return child;
    }

    // Mutate a small percentage of rules to random actions
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

    // Tournament selection for queens: pick TOURNAMENT_SIZE random candidates, return index of fittest.
    // If excludeIndex is provided (>= 0), that index is excluded to prevent self-mating.
    private int TournamentSelectQueen(List<QueenGenes> pool, int excludeIndex = -1)
    {
        int bestIndex = -1;
        int bestFitness = -1;

        for (int i = 0; i < TOURNAMENT_SIZE; i++)
        {
            int candidate = Random.Range(0, pool.Count);

            // skip the excluded index to prevent self-mating
            if (candidate == excludeIndex)
            {
                // re-roll once; if pool is small this avoids infinite loops
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

    // Tournament selection for workers: pick TOURNAMENT_SIZE random candidates, return index of fittest.
    // If excludeIndex is provided (>= 0), that index is excluded to prevent self-mating.
    private int TournamentSelectWorker(List<WorkerGenes> pool, int excludeIndex = -1)
    {
        int bestIndex = -1;
        int bestFitness = -1;

        for (int i = 0; i < TOURNAMENT_SIZE; i++)
        {
            int candidate = Random.Range(0, pool.Count);

            // skip the excluded index to prevent self-mating
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