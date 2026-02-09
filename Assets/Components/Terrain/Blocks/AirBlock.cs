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

        /// <summary>
        /// Byte key for QueenPheromone in the phermoneDeposits dictionary.
        /// </summary>
        public static readonly byte QUEEN_PHEROMONE_KEY = 0;

        /// <summary>
        /// Amount of QueenPheromone that evaporates each tick.
        /// Each tick: pheromone *= (1 - evaporationRate).
        /// Very slow so the trail lingers for a long time.
        /// </summary>
        public static double queenPheromoneEvaporationRate = 0.001;

        /// <summary>
        /// Fraction of QueenPheromone that diffuses to each neighbouring air block per tick.
        /// Each neighbour receives: pheromone * diffusionRate.
        /// The source block loses the total amount shared.
        /// </summary>
        public static double queenPheromoneDiffusionRate = 0.01;

        #endregion

        #region Fields

        /// <summary>
        /// Statically held is visible.
        /// </summary>
        private static bool _isVisible = false;

        /// <summary>
        /// Set of air blocks that currently contain pheromone. Only these are ticked.
        /// </summary>
        private static HashSet<AirBlock> activeBlocks = new HashSet<AirBlock>();

        /// <summary>
        /// Reusable buffer for iterating active blocks without allocating each tick.
        /// </summary>
        private static List<AirBlock> tickBuffer = new List<AirBlock>();

        /// <summary>
        /// Reusable buffer for the 6 face-adjacent neighbours.
        /// </summary>
        private static AbstractBlock[] neighbourBuffer = new AbstractBlock[6];

        /// <summary>
        /// A dictionary representing the phermone deposits in the air.
        /// Each type of phermone gets its own byte key, and each phermone type has a concentration.
        /// </summary>
        private Dictionary<byte, double> phermoneDeposits = new Dictionary<byte, double>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the QueenPheromone concentration stored in this air block.
        /// Automatically registers/unregisters the block for per-tick processing.
        /// </summary>
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
                    activeBlocks.Remove(this);
                }
            }
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
        /// Processes evaporation and diffusion for all air blocks that currently hold pheromone.
        /// Called once per tick from WorldManager.FixedUpdate.
        /// </summary>
        public static void TickAll()
        {
            if (activeBlocks.Count == 0)
                return;

            // Snapshot active set so we can safely modify it during iteration
            tickBuffer.Clear();
            tickBuffer.AddRange(activeBlocks);

            // Evaporate first
            for (int i = 0; i < tickBuffer.Count; i++)
            {
                tickBuffer[i].Evaporate();
            }

            // Then diffuse (blocks that evaporated to 0 are skipped by Diffuse's early-out)
            for (int i = 0; i < tickBuffer.Count; i++)
            {
                AirBlock block = tickBuffer[i];
                if (block.QueenPheromone <= 0)
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
        /// Evaporates pheromone in this block. Should be called each tick.
        /// </summary>
        public void Evaporate()
        {
            double current = QueenPheromone;
            if (current <= 0)
                return;

            current *= (1.0 - queenPheromoneEvaporationRate);
            if (current < 0.0001)
                current = 0;

            QueenPheromone = current;
        }

        /// <summary>
        /// Diffuses QueenPheromone from this block to its neighbouring air blocks.
        /// Each air neighbour receives a share of this block's pheromone.
        /// </summary>
        /// <param name="neighbours">The 6 face-adjacent blocks (may be null or non-air).</param>
        public void Diffuse(AbstractBlock[] neighbours)
        {
            double current = QueenPheromone;
            if (current <= 0)
                return;

            double sharePerNeighbour = current * queenPheromoneDiffusionRate;
            double totalLost = 0;

            for (int i = 0; i < neighbours.Length; i++)
            {
                if (neighbours[i] is AirBlock neighbourAir)
                {
                    neighbourAir.QueenPheromone += sharePerNeighbour;
                    totalLost += sharePerNeighbour;
                }
            }

            QueenPheromone = current - totalLost;
        }

        #endregion

    }
}
