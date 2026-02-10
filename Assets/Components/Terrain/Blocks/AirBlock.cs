using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Antymology.Terrain
{
    /// <summary>
    /// The air type of block. Contains the internal data representing phermones in the air.
    /// </summary>
    public class AirBlock : AbstractBlock
    {

        #region Constants

        public static readonly byte QUEEN_PHEROMONE_KEY = 0;
        public static readonly byte WORKER_PHEROMONE_KEY = 1;

        public static double queenPheromoneEvaporationRate = 0.001;
        public static double queenPheromoneDiffusionRate = 0.06;
        public static double workerPheromoneEvaporationRate = 0.005;
        public static double workerPheromoneDiffusionRate = 0.04;

        #endregion

        #region Fields

        /// <summary>
        /// Statically held is visible.
        /// </summary>
        private static bool _isVisible = false;
        private static HashSet<AirBlock> activeBlocks = new HashSet<AirBlock>();
        private static List<AirBlock> tickBuffer = new List<AirBlock>();
        private static AbstractBlock[] neighbourBuffer = new AbstractBlock[6];
        private Dictionary<byte, double> phermoneDeposits = new Dictionary<byte, double>();

        #endregion

        #region Properties

        public double QueenPheromone
        {
            get
            {
                phermoneDeposits.TryGetValue(QUEEN_PHEROMONE_KEY, out double val);
                return val;
            }
            set
            {
                if (value > 0)
                {
                    phermoneDeposits[QUEEN_PHEROMONE_KEY] = value;
                    activeBlocks.Add(this);
                }
                else
                {
                    phermoneDeposits.Remove(QUEEN_PHEROMONE_KEY);
                    if (WorkerPheromone <= 0)
                        activeBlocks.Remove(this);
                }
            }
        }

        public double WorkerPheromone
        {
            get
            {
                phermoneDeposits.TryGetValue(WORKER_PHEROMONE_KEY, out double val);
                return val;
            }
            set
            {
                if (value > 0)
                {
                    phermoneDeposits[WORKER_PHEROMONE_KEY] = value;
                    activeBlocks.Add(this);
                }
                else
                {
                    phermoneDeposits.Remove(WORKER_PHEROMONE_KEY);
                    if (QueenPheromone <= 0)
                        activeBlocks.Remove(this);
                }
            }
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Clears all static pheromone tracking before world regeneration
        /// </summary>
        public static void ClearAll()
        {
            activeBlocks.Clear();
            tickBuffer.Clear();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Air blocks are going to be invisible.
        /// </summary>
        public override bool isVisible()
        {
            return _isVisible;
        }

        /// <summary>
        /// Air blocks are invisible so asking for their tile map coordinate doesn't make sense.
        /// </summary>
        public override Vector2 tileMapCoordinate()
        {
            throw new Exception("An invisible tile cannot have a tile map coordinate.");
        }

        /// <summary>
        /// Processes evaporation and diffusion for all active pheromone blocks.
        /// </summary>
        public static void TickAll()
        {
            if (activeBlocks.Count == 0)
                return;

            tickBuffer.Clear();
            tickBuffer.AddRange(activeBlocks);

            for (int i = 0; i < tickBuffer.Count; i++)
            {
                tickBuffer[i].Evaporate();
            }

            for (int i = 0; i < tickBuffer.Count; i++)
            {
                AirBlock block = tickBuffer[i];
                if (block.QueenPheromone <= 0 && block.WorkerPheromone <= 0)
                    continue;

                int x = block.worldXCoordinate;
                int y = block.worldYCoordinate;
                int z = block.worldZCoordinate;

                neighbourBuffer[0] = WorldManager.Instance.GetBlockOrNull(x - 1, y, z);
                neighbourBuffer[1] = WorldManager.Instance.GetBlockOrNull(x + 1, y, z);
                neighbourBuffer[2] = WorldManager.Instance.GetBlockOrNull(x, y - 1, z);
                neighbourBuffer[3] = WorldManager.Instance.GetBlockOrNull(x, y + 1, z);
                neighbourBuffer[4] = WorldManager.Instance.GetBlockOrNull(x, y, z - 1);
                neighbourBuffer[5] = WorldManager.Instance.GetBlockOrNull(x, y, z + 1);

                block.Diffuse(neighbourBuffer);
            }
        }

        /// <summary>
        /// Evaporates both queen and worker pheromone in this block.
        /// </summary>
        public void Evaporate()
        {
            double queenCurrent = QueenPheromone;
            if (queenCurrent > 0)
            {
                queenCurrent *= (1.0 - queenPheromoneEvaporationRate);
                if (queenCurrent < 0.1) queenCurrent = 0;
                QueenPheromone = queenCurrent;
            }

            double workerCurrent = WorkerPheromone;
            if (workerCurrent > 0)
            {
                workerCurrent *= (1.0 - workerPheromoneEvaporationRate);
                if (workerCurrent < 0.1) workerCurrent = 0;
                WorkerPheromone = workerCurrent;
            }
        }

        /// <summary>
        /// Diffuses both queen and worker pheromone to neighbouring air blocks.
        /// </summary>
        /// <param name="neighbours"></param>
        public void Diffuse(AbstractBlock[] neighbours)
        {
            double queenCurrent = QueenPheromone;
            if (queenCurrent > 0)
            {
                double share = queenCurrent * queenPheromoneDiffusionRate;
                double totalLost = 0;
                for (int i = 0; i < neighbours.Length; i++)
                {
                    if (neighbours[i] is AirBlock neighbourAir)
                    {
                        neighbourAir.QueenPheromone += share;
                        totalLost += share;
                    }
                }
                QueenPheromone = queenCurrent - totalLost;
            }

            double workerCurrent = WorkerPheromone;
            if (workerCurrent > 0)
            {
                double share = workerCurrent * workerPheromoneDiffusionRate;
                double totalLost = 0;
                for (int i = 0; i < neighbours.Length; i++)
                {
                    if (neighbours[i] is AirBlock neighbourAir)
                    {
                        neighbourAir.WorkerPheromone += share;
                        totalLost += share;
                    }
                }
                WorkerPheromone = workerCurrent - totalLost;
            }
        }

        #endregion

    }
}
