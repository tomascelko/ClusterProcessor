﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Accord.Statistics.Models.Regression.Linear;
namespace ClusterCalculator
{
    /// <summary>
    /// Class performing the branch analysis
    /// </summary>
    public class BranchAnalyzer
    {

        readonly (int x, int y)[] neighbourDiff = { (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1) };
        NeighbourCountFilter NeighCountFilter { get; }
        EnergyCenterFinder EnCenterFinder { get; }
        const int trivialBranchLength = 2;
        int MaxDepth { get; set; } = 4;
        int MaxbranchCount { get; set; } = 20;
        public BranchAnalyzer(EnergyCenterFinder centerFinder)
        {
            EnCenterFinder = centerFinder;
            NeighCountFilter = new NeighbourCountFilter(nCount => nCount >= 3, NeighbourCountOption.WithYpsilonNeighbours);
        }
        /// <summary>
        /// Performs the branch analysis on the top level
        /// </summary>
        public BranchedCluster Analyze(Cluster skeletCluster, Cluster originCluster, int maxDepth = 4)
        {

            this.MaxDepth = maxDepth;
            List<Branch> mainBranches = new List<Branch>();
            var usablePoints = skeletCluster.Points.ToHashSetPixPoints();
            var center = EnCenterFinder.CalcCenterPoint(skeletCluster, originCluster.Points);
            var crossPoints = NeighCountFilter.Process(usablePoints.ToList());
            Branch mainBranch = GetBranch(usablePoints, center, crossPoints, maxDepth);
            var currentBranch = mainBranch;
            MaxbranchCount = 30;
            usablePoints = usablePoints.Except(currentBranch.TotalPoints).ToHashSet();
            while (currentBranch.Points.Count > trivialBranchLength || mainBranches.Count == 0)
            {
                mainBranches.Add(currentBranch);
                currentBranch = GetBranch(usablePoints, center, crossPoints, maxDepth);
                usablePoints = usablePoints.Except(currentBranch.TotalPoints).ToHashSet();
                usablePoints.Add(center);
            }
            AdjustMissedCrossPoints(mainBranches, crossPoints, skeletCluster.Points);
            mainBranches.Sort((left, right) => {

                if (left.Points.Count < right.Points.Count)
                    return 1;
                else if (left.Points.Count > right.Points.Count)
                    return -1;
                else
                    return 0;
            });
            
            const int epsilonBranchDist = 4;
            var branchesToCheck = new Dictionary<Branch, List<Branch>>();
            foreach (var mainBr in mainBranches)
            {
                branchesToCheck.Add(mainBr, new List<Branch>());
                foreach (var subBr in mainBr.SubBranches)
                    if ((subBr.StartPoint.GetDistance(mainBr.StartPoint) < epsilonBranchDist))
                        branchesToCheck[mainBr].Add(subBr);
            }
            //merge two mainbranches if they are Continuous
            for (int i = 0; i < mainBranches.Count - 1; ++i)
            {
                for (int j = i + 1; j < mainBranches.Count; ++j)
                {
                    if (AreContinuous(mainBranches[i], mainBranches[j]))
                    {
                        var leftBranch = mainBranches[i];
                        leftBranch.TotalPoints.UnionWith(mainBranches[j].TotalPoints);
                        MergeBranches(mainBranches[i], mainBranches[j], mainBranches.Except(new Branch[]{ mainBranches[i], mainBranches[j]}));
                        mainBranches.RemoveAll(branch => !branch.Equals(leftBranch));
                        return new BranchedCluster(skeletCluster, mainBranches, center);
                    }
                }
            }
            //merge mainbranch and its suitable subbranch
            for (int i = 0; i < mainBranches.Count; ++i)
            {
                for(int j = 0; j < mainBranches[i].SubBranches.Count; ++j)
                {
                    var subBranch = mainBranches[i].SubBranches[j];
                    var leftBranch = mainBranches[i];
                    if (mainBranches[i].StartPoint.GetDistance(subBranch.StartPoint) < epsilonBranchDist &&AreContinuous(mainBranches[i], mainBranches[i].SubBranches[j]))
                    {

                        MergeBranches(mainBranches[i], subBranch, mainBranches.Except(new Branch[] { mainBranches[i] }));
                        mainBranches.RemoveAll(branch => !branch.Equals(leftBranch));
                        return new BranchedCluster(skeletCluster, mainBranches, center);
                    }
                }
            }
            return new BranchedCluster(skeletCluster, mainBranches, center);
        }     
        public bool AreContinuous(Branch left, Branch right)
        {
            const double epsilonAngle = 0.35d;
            const int epsilonPointCount = 15;
            var angle = CalculateAngle(left.Points.Take(epsilonPointCount).ToArray(), right.Points.Take(epsilonPointCount).ToArray());
            return ( angle < epsilonAngle);


        }
        /// <summary>
        /// Merges two distinct branches into one if the angle between them is small enough
        /// </summary>
        public void MergeBranches(Branch left, Branch right, IEnumerable<Branch> otherMainBranches)
        {
            left.Points.UnionWith(right.Points.Reverse());
            left.SubBranches = left.SubBranches.Union(right.SubBranches).Union(otherMainBranches).Except(new Branch[] { left, right}).ToList();

        }
        /// <summary>
        /// Calculates angle between two branches based on up to 10 first points and linear regression
        /// </summary>
        public double CalculateAngle(IEnumerable<PixelPoint> left, IEnumerable<PixelPoint> right)
        {
            double leftSlope, rightSlope;
            SimpleLinearRegression leftRegression, rightRegression;
            //catch blocks handle infinity-slope cases
            try
            {
                leftRegression = SimpleLinearRegression.FromData(left.Select(point => (double)point.xCoord).ToArray(), left.Select(point => (double)point.yCoord).ToArray());
                leftSlope = leftRegression.Slope;
            }
            catch
            {
                leftSlope = double.MaxValue;
            }
            try
            {
                rightRegression = SimpleLinearRegression.FromData(right.Select(point => (double)point.xCoord).ToArray(), right.Select(point => (double)point.yCoord).ToArray());
                rightSlope = rightRegression.Slope;
            }
            catch
            {
                rightSlope = double.MaxValue;
            }


            return Math.Atan(Math.Abs((leftSlope - rightSlope) / (1 + leftSlope * rightSlope)));
        }
        /// <summary>
        /// A recursive function for finding branch and its subbranches
        /// </summary>
        /// <param name="usablePoints">points not members of any branch</param>
        /// <param name="startBranchPoint"></param>
        /// <param name="crossPoints"></param>
        /// <param name="depth">remaining depth of calculation</param>
        /// <returns>Currently analyzed branch</returns>
        public Branch GetBranch(HashSet<PixelPoint> usablePoints, PixelPoint startBranchPoint, IList<PixelPoint> crossPoints, int depth)
        {
            Branch mainBranch = new Branch(startBranchPoint,
                FindLongestPathBFS(startBranchPoint, usablePoints/*, crossPoints*/).ToHashSet());
            MaxbranchCount--;
            var localCrossPoints = crossPoints.Where(branchPoint => mainBranch.Points.Contains(branchPoint) && !branchPoint.Equals(startBranchPoint));
            var nonUsablePoints = mainBranch.Points.Except(localCrossPoints);
            var closeToStartPoints = usablePoints.Where(point => startBranchPoint.GetDistance(point) == 1 && !localCrossPoints.Contains(point)).ToList();
            nonUsablePoints = nonUsablePoints.Append(startBranchPoint).Union(closeToStartPoints);
            var subBranchUsablePoints = usablePoints.Except(nonUsablePoints).ToHashSet();
            if (depth > 0 && MaxbranchCount > 0)
            foreach (var localBranchPoint in localCrossPoints)
            {
                 mainBranch.SubBranches.Add(GetBranch(subBranchUsablePoints, localBranchPoint, crossPoints, depth - 1));
            }
            if (depth == MaxDepth)
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
        /// <summary>
        /// Does the BFS on the points to find longest branch
        /// </summary>
        /// <param name="startPoint">start of the search</param>
        /// <param name="usablePoints"> points we can use for BFS</param>
        /// <returns>points which were found in BFS as the longest branch</returns>
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
                var usableNeighbours = GetNeighbours(current, usablePoints).FindAll(neighbour => distLabeledPoints[neighbour] == distance - i);
                //sort the neighbours so that 4 neighbours are prefered before 8 neighbours
                usableNeighbours.Sort((PixelPoint left, PixelPoint right) => (Math.Abs(left.xCoord - current.xCoord) + Math.Abs(left.yCoord - current.yCoord)) 
                - (Math.Abs(right.xCoord - current.xCoord) + Math.Abs(right.yCoord - current.yCoord)));
                longestPath[distance - i] = usableNeighbours[0];
                current = longestPath[distance - i];
            }
            longestPath[0] = startPoint; //startPoint might not be in usablePoints
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
        private void AdjustMissedCrossPoints(IList<Branch> mainBranches, IList<PixelPoint> crossPoints, IList<PixelPoint> allPoints)
        {
            HashSet<PixelPoint> usedPoints = new HashSet<PixelPoint>();
            foreach (var branch in mainBranches)
            {
                usedPoints = usedPoints.Union(branch.CalcTotalPoints()).ToHashSet();
            }
            var usablePoints = allPoints.Except(usedPoints).ToHashSet();
            for (int i = 0; i < crossPoints.Count; ++i)
            {
                var neighbours = GetNeighbours(crossPoints[i], usedPoints);
                if (neighbours.Count >= 1 && usablePoints.Contains(crossPoints[i])) 
                {
                    foreach (var branch in mainBranches)
                    {
                        if (branch.CalcTotalPoints().Contains(neighbours[0]))
                        {
                            var parentBranch = branch.FindBranch(neighbours[0]);
                            var newBranch = GetBranch(usablePoints, crossPoints[i], crossPoints, MaxDepth);
                            var newPoints = newBranch.CalcTotalPoints();
                            if (newPoints.Count > trivialBranchLength)
                                parentBranch.SubBranches.Add(newBranch);
                            usablePoints = usablePoints.Except(newPoints).ToHashSet();
                            break;
                        }
                    }
                }
            }
        }
    }
    /// <summary>
    /// Class representing a single branch of the cluster
    /// </summary>
    public class Branch
    {
        public PixelPoint StartPoint;
        public List<Branch> SubBranches;
        public HashSet<PixelPoint> Points { get; private set; }
        public HashSet<PixelPoint> TotalPoints { get; set; }
        /// <summary>
        /// retrieves all the points of this branches and its subbranches
        /// </summary>
        /// <returns></returns>
        public HashSet<PixelPoint> CalcTotalPoints() 
        {
            TotalPoints = TotalPoints.Union(Points).ToHashSet();
            foreach (var subBranch in SubBranches)
            {
                TotalPoints = TotalPoints.Union(subBranch.CalcTotalPoints()).ToHashSet(); 
            }
            return TotalPoints;
        }
       /// <summary>
       /// Get the branch where given point belongs
       /// </summary>
        public Branch FindBranch(PixelPoint point)
        {
            if (Points.Contains(point))
                return this;
            foreach (var subBranch in SubBranches)
            {
                var searchResult = subBranch.FindBranch(point);
                if (searchResult != null)
                {
                    return searchResult;
                }
            }
            return null;
        }

        public Branch(PixelPoint startPoint, HashSet<PixelPoint> points) 
        {
            StartPoint = startPoint;
            SubBranches = new List<Branch>();
            Points = points;
            TotalPoints = new HashSet<PixelPoint>();
        
        }
        /// <summary>
        /// Convert this object to dictionary for further processing
        /// </summary>
        public Dictionary<BranchAttribute, object> ToDictionary(EnergyCalculator energyCalc)
        {
            Dictionary<BranchAttribute, object> dict = new Dictionary<BranchAttribute, object>();
            dict.Add(BranchAttribute.Length, this.Points.Count);
            dict.Add(BranchAttribute.BranchEnergy, energyCalc.CalcTotalEnergy(this.Points.ToList()));
            if (this.SubBranches.Count != 0)
            {
                List<Dictionary<BranchAttribute, object>> subBranches = new List<Dictionary<BranchAttribute, object>>();
                foreach (var subBranch in SubBranches)
                {
                    subBranches.Add(subBranch.ToDictionary(energyCalc));        
                }
                dict.Add(BranchAttribute.SubBranches, subBranches);
            }
            return dict;
        }
        
        public int GetTotalSubBranchCount()
        {
            var totalSubBranchCount = this.SubBranches.Count;
                foreach (var branch in SubBranches)
                    totalSubBranchCount += branch.GetTotalSubBranchCount();
            return totalSubBranchCount;
                    
        }

    }
        
}
