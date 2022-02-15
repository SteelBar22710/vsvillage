using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Essentials;

namespace VsVillage
{
    public class VillagerAStar
    {
        protected ICoreServerAPI api;
        protected ICachingBlockAccessor blockAccess;

        public List<string> traversableCodes { get; set; } = new List<string>() { "door", "gate" };

        public int NodesChecked;

        public double centerOffsetX = 0.5;
        public double centerOffsetZ = 0.5;

        public VillagerAStar(ICoreServerAPI api)
        {
            this.api = api;
            blockAccess = api.World.GetCachingBlockAccessor(true, true);
        }

        public PathNodeSet openSet = new PathNodeSet();
        public HashSet<PathNode> closedSet = new HashSet<PathNode>();

        public List<Vec3d> FindPathAsWaypoints(BlockPos start, BlockPos end, int maxFallHeight, float stepHeight, Cuboidf entityCollBox, int searchDepth = 9999, bool allowReachAlmost = false)
        {
            List<PathNode> nodes = FindPath(start, end, maxFallHeight, stepHeight, entityCollBox, searchDepth, allowReachAlmost);
            return nodes == null ? null : ToWaypoints(nodes);
        }

        public List<PathNode> FindPath(BlockPos start, BlockPos end, int maxFallHeight, float stepHeight, Cuboidf entityCollBox, int searchDepth = 9999, bool allowReachAlmost = false)
        {
            blockAccess.Begin();

            NodesChecked = 0;

            PathNode startNode = new PathNode(start);
            PathNode targetNode = new PathNode(end);

            openSet.Clear();
            closedSet.Clear();

            openSet.Add(startNode);

            while (openSet.Count > 0)
            {
                if (NodesChecked++ > searchDepth) return null;

                PathNode nearestNode = openSet.RemoveNearest();
                closedSet.Add(nearestNode);

                if (nearestNode == targetNode || (allowReachAlmost && Math.Abs(nearestNode.X - targetNode.X) <= 1 && Math.Abs(nearestNode.Z - targetNode.Z) <= 1 && (nearestNode.Y == targetNode.Y || nearestNode.Y == targetNode.Y + 1)))
                {
                    return retracePath(startNode, nearestNode);
                }

                //PathNode[] validNextNodes = getValidNextNodes(nearestNode)
                for (int i = 0; i < Cardinal.ALL.Length; i++)
                {
                    Cardinal card = Cardinal.ALL[i];

                    PathNode neighbourNode = new PathNode(nearestNode, card);

                    float extraCost = 0;
                    PathNode existingNeighbourNode = openSet.TryFindValue(neighbourNode);
                    if (!(existingNeighbourNode is null))   // we have to do a null check using "is null" due to foibles in PathNode.Equals()
                    {
                        // if it is already in openSet, update the gCost and parent if this nearestNode gives a shorter route to it
                        float baseCostToNeighbour = nearestNode.gCost + nearestNode.distanceTo(neighbourNode);
                        if (existingNeighbourNode.gCost > baseCostToNeighbour + 0.0001f)
                        {
                            if (traversable(neighbourNode, stepHeight, maxFallHeight, entityCollBox, card.IsDiagnoal, ref extraCost) && existingNeighbourNode.gCost > baseCostToNeighbour + extraCost + 0.0001f)
                            {
                                UpdateNode(nearestNode, existingNeighbourNode, extraCost);
                            }
                        }
                    }
                    else if (!closedSet.Contains(neighbourNode))
                    {
                        if (traversable(neighbourNode, stepHeight, maxFallHeight, entityCollBox, card.IsDiagnoal, ref extraCost))
                        {
                            UpdateNode(nearestNode, neighbourNode, extraCost);
                            neighbourNode.hCost = neighbourNode.distanceTo(targetNode);
                            openSet.Add(neighbourNode);
                        }
                    }
                }
            }

            return null;
        }



        /// <summary>
        /// Actually now only sets fields in neighbourNode as appropriate.  The calling code must add this to openSet if necessary.
        /// </summary>
        private void UpdateNode(PathNode nearestNode, PathNode neighbourNode, float extraCost)
        {
            neighbourNode.gCost = nearestNode.gCost + nearestNode.distanceTo(neighbourNode) + extraCost;
            neighbourNode.Parent = nearestNode;
            neighbourNode.pathLength = nearestNode.pathLength + 1;
        }

        protected virtual bool traversable(PathNode node, float stepHeight, int maxFallHeight, Cuboidf entityCollBox, bool isDiagonal, ref float extraCost)
        {
            if (traversable(blockAccess.GetBlock(node.X, node.Y, node.Z))
                && traversable(blockAccess.GetBlock(node.X, node.Y + 1, node.Z)))
            {
                for (; 0 <= maxFallHeight; maxFallHeight--)
                {
                    Block belowBlock = blockAccess.GetBlock(node.X, node.Y - 1, node.Z);
                    if (canStep(belowBlock)) { return true; }
                    if (!traversable(belowBlock)) { return false; };
                    node.Y--;
                }
            }
            else
            {
                for (; 1f < stepHeight; stepHeight--)
                {
                    node.Y++;
                    if (canStep(blockAccess.GetBlock(node.X, node.Y - 1, node.Z)) && traversable(blockAccess.GetBlock(node.X, node.Y, node.Z)) && traversable(blockAccess.GetBlock(node.X, node.Y + 1, node.Z)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        protected virtual bool canStep(Block belowBlock)
        {
            return belowBlock != null && belowBlock.SideSolid[BlockFacing.UP.Index];
        }

        protected virtual bool traversable(Block block)
        {
            return block == null || block.CollisionBoxes == null || block.CollisionBoxes.Length == 0 || traversableCodes.Exists(code => block.Code.Path.Contains(code));
        }

        List<PathNode> retracePath(PathNode startNode, PathNode endNode)
        {
            int length = endNode.pathLength;
            List<PathNode> path = new List<PathNode>(length);
            for (int i = 0; i < length; i++) path.Add(null);  // pre-fill the path with dummy values to achieve the required Count, needed for assignment to path[i] later
            PathNode currentNode = endNode;

            for (int i = length - 1; i >= 0; i--)
            {
                path[i] = currentNode;
                currentNode = currentNode.Parent;
            }

            return path;
        }



        public List<Vec3d> ToWaypoints(List<PathNode> path)
        {
            List<Vec3d> waypoints = new List<Vec3d>(path.Count + 1);
            for (int i = 1; i < path.Count; i++)
            {
                waypoints.Add(path[i].ToWaypoint().Add(centerOffsetX, 0, centerOffsetZ));
            }

            return waypoints;
        }

    }
}