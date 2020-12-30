﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClusterCalculator
{
    public class BranchAnalyzer
    {

        readonly (int x, int y)[] neighbourDiff = { (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1) };
        NeighbourCountFilter NeighCountFilter { get;}
        EnergyCenterFinder EnCenterFinder { get; }
        public BranchAnalyzer(EnergyCenterFinder centerFinder)
        {
            EnCenterFinder = centerFinder;
            NeighCountFilter = new NeighbourCountFilter(nCount => nCount >= 3, NeighbourCountOption.WithYpsilonNeighbours);
        }
        public BranchedCluster Analyze(Cluster cluster)
        {
            const int trivialBranchLength = 3;

            List<Branch> mainBranches = new List<Branch>();
            var usablePoints = cluster.Points.ToHashSet();
            var center = EnCenterFinder.CalcCenterPoint(cluster.Points);
            var crossPoints = NeighCountFilter.Process(cluster.Points);
            Branch mainBranch = GetCoreBranch(usablePoints, center, crossPoints);
            
            var currentBranch = mainBranch;
            usablePoints = usablePoints.Except(currentBranch.TotalPoints).ToHashSet();
            while (currentBranch.Points.Count > trivialBranchLength)
            {

                mainBranches.Add(currentBranch);
                currentBranch = GetCoreBranch(usablePoints, center, crossPoints);
                usablePoints = usablePoints.Except(currentBranch.TotalPoints).ToHashSet();
                usablePoints.Add(center);
            }
            return new BranchedCluster(cluster, mainBranches);
        }
        public Branch GetCoreBranch(HashSet<PixelPoint> usablePoints, PixelPoint startBranchPoint, IList<PixelPoint> crossPoints)
        {
            Branch mainBranch = new Branch(startBranchPoint,
                FindLongestPathBFS(startBranchPoint, usablePoints/*, crossPoints*/).ToHashSet());

            var localCrossPoints = crossPoints.Where(branchPoint => mainBranch.Points.Contains(branchPoint) && !branchPoint.Equals(startBranchPoint));
            var nonUsablePoints = mainBranch.Points.Except(localCrossPoints);
            nonUsablePoints.Append(startBranchPoint);
            var subBranchUsablePoints = usablePoints.Except(nonUsablePoints).ToHashSet();
            foreach (var localBranchPoint in localCrossPoints)
            {
                mainBranch.SubBranches.Add(GetCoreBranch(subBranchUsablePoints, localBranchPoint, crossPoints));
            }
            mainBranch.CalcTotalPoints();
            return mainBranch;
        }
        public PixelPoint[] FindLongestPathDFS(PixelPoint startPoint, HashSet<PixelPoint> usablePoints, IList<PixelPoint> crossPoints)

        {
            PixelPoint[] longestPath = new PixelPoint[0];
            Stack<PixelPoint> onBranch = new Stack<PixelPoint>();
            Stack<PixelPoint> toVisit = new Stack<PixelPoint>();
            HashSet<PixelPoint> visited = new HashSet<PixelPoint>();
            HashSet<PixelPoint> plannedToVisit = new HashSet<PixelPoint>();

            toVisit.Push(startPoint);
            PixelPoint current = startPoint;
            var neighbours = GetNeighbours(startPoint, usablePoints);
            while (toVisit.Count != 0)
            {
                //if current and previous are not neighbours then pop from onBranch till they are? and if they are push current on onBranch Stack? 
                current = toVisit.Pop();
                if (onBranch.Count > 0 && !AreNeighbours(current, onBranch.Peek()))
                    while (onBranch.Count > 0 && !crossPoints.Contains(onBranch.Peek())) //problem with neighbours close to branch point
                    {
                    onBranch.Pop();
                    }                
                bool isVertex = true;
                neighbours = GetNeighbours(current, usablePoints);
                for (int i = 0; i < neighbours.Count; i++)
                {
                    if (!visited.Contains((neighbours[i])) && !plannedToVisit.Contains(neighbours[i]))
                    {
                        isVertex = false;
                        toVisit.Push(neighbours[i]);
                        plannedToVisit.Add(neighbours[i]);
                    }
                }
                visited.Add(current);
                onBranch.Push(current);
                if (isVertex && onBranch.Count > longestPath.Length)
                    longestPath = onBranch.ToArray();

            }
            return longestPath;

        }
        public PixelPoint[] FindLongestPathBFS(PixelPoint startPoint, HashSet<PixelPoint> usablePoints)
        {
            Queue<PixelPoint> toVisit = new Queue<PixelPoint>();
            HashSet<PixelPoint> plannedToVisit = new HashSet<PixelPoint>();
            HashSet<PixelPoint> visited = new HashSet<PixelPoint>();
            Dictionary<PixelPoint, int> distLabeledPoints = new Dictionary<PixelPoint, int>();
            PixelPoint[] longestPath;
            toVisit.Enqueue(startPoint);
            PixelPoint current = startPoint;
            int distance = 0;
            var neighbours = GetNeighbours(startPoint, usablePoints);
            distLabeledPoints.Add(startPoint, distance);
            while (toVisit.Count != 0)
            {
                //if current and previous are not neighbours then pop from onBranch till they are? and if they are push current on onBranch Stack? 
                current = toVisit.Dequeue();
                
                neighbours = GetNeighbours(current, usablePoints);
                distance = distLabeledPoints[current];
                for (int i = 0; i < neighbours.Count; i++)
                {
                    if (!visited.Contains((neighbours[i])) && !plannedToVisit.Contains(neighbours[i]))
                    {                       
                        toVisit.Enqueue(neighbours[i]);
                        plannedToVisit.Add(neighbours[i]);
                        distLabeledPoints.Add(neighbours[i], distance + 1);
                    }
                }
                visited.Add(current);
            }
            longestPath = new PixelPoint[distance + 1];
            longestPath[distance] = current;
            for (int i = 1; i < longestPath.Length - 1; i++)
            {              
                longestPath[distance - i ] = GetNeighbours(current, usablePoints).Find(neighbour => distLabeledPoints[neighbour] == distance - i);
                current = longestPath[distance - i];
            }
            longestPath[0] = startPoint; //startPoint might not be in usablePOints
            return longestPath;
        }
        private List<PixelPoint> GetNeighbours(PixelPoint point, HashSet<PixelPoint> usablePoints)
        {
            List<PixelPoint> neighbours = new List<PixelPoint>();
            for (int i = 0; i < neighbourDiff.Length; i++)
            {
                var neighbourEq = new PixelPoint((ushort)(point.xCoord + neighbourDiff[i].x), (ushort)(point.yCoord + neighbourDiff[i].y));
                if (usablePoints.Contains(neighbourEq))
                {
                    usablePoints.TryGetValue(neighbourEq, out PixelPoint neighbour);
                    neighbours.Add(neighbour);
                }
                    
            }
            return neighbours;
        }
        private bool AreNeighbours(PixelPoint first, PixelPoint second)
        {
            return first.GetDistance(second) <= 1; 
        }
    }
    public class Branch
    {
        PixelPoint StartPoint;
        public List<Branch> SubBranches;
        public HashSet<PixelPoint> Points { get; private set; }
        public HashSet<PixelPoint> TotalPoints { get; private set; }
        public HashSet<PixelPoint> CalcTotalPoints() 
        {
            TotalPoints = TotalPoints.Union(Points).ToHashSet();
            foreach (var subBranch in SubBranches)
            {
                TotalPoints = TotalPoints.Union(subBranch.CalcTotalPoints()).ToHashSet(); ;
            }
            return TotalPoints;
        }

        public Branch(PixelPoint startPoint, HashSet<PixelPoint> points) 
        {
            StartPoint = startPoint;
            SubBranches = new List<Branch>();
            Points = points;
            TotalPoints = new HashSet<PixelPoint>();
        
        }
    }
        
}
