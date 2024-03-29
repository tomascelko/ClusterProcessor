﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
namespace ClusterCalculator
{
    /// <summary>
    /// essential object which stores primary data about the cluster
    /// </summary>
    public class Cluster
    {
        public double FirstToA { get; protected set; }
        public uint PixelCount { get; protected set; }
        public ulong ByteStart { get; protected set; }
        public PixelPoint[] Points { get; set; }
        public Cluster(double FirstToA, uint PixelCount, ulong ByteStart) {
            this.FirstToA = FirstToA;
            this.PixelCount = PixelCount;
            this.ByteStart = ByteStart;
        }

        public Color ToColor(double color)
        {
            if (double.IsNaN(color))
                return Color.FromArgb(255, 255, 255, 255);
            if (color < 0.20)
                return Color.FromArgb(255, 255, 255, (int)Math.Round((1 - 5*color) * 255) );
            return Color.FromArgb(255, 255, (int)Math.Round((1 - color) * 255), 0);
        }
               
    }
    /// <summary>
    /// A single detected point in the cluster
    /// </summary>
    public struct PixelPoint : IEquatable<PixelPoint>
    {

        public PixelPoint(ushort xCoord, ushort yCoord, double ToA = 0, double Energy = 0)
        {
            this.xCoord = xCoord;
            this.yCoord = yCoord;
            this.ToA = ToA;
            this.Energy = Energy;
        }
        public PixelPoint(ushort xCoord, ushort yCoord, double ToA, double ToT, EnergyCalculator enCalculator)
        {
            this.xCoord = xCoord;
            this.yCoord = yCoord;
            this.ToA = ToA;
            this.Energy = enCalculator.ToElectronVolts(ToT, xCoord, yCoord);
        }
        public ushort xCoord { get; }
        public ushort yCoord { get; }
        public double ToA { get; }
        public double Energy { get; private set; }
        public void SetEnergy(double newEnergy)
        {
            Energy = newEnergy;
        }
        public override int GetHashCode()
        {
            return (xCoord << 8) + yCoord;
        }
        public bool Equals(PixelPoint other)
        {
            return this.xCoord == other.xCoord && this.yCoord == other.yCoord;
        }
        public override bool Equals(object obj)
        {
            if (obj.GetType() == typeof(PixelPoint))
            {
                var other = (PixelPoint)obj;
                return Equals(other);
            }
            return false;
        }
        public ushort GetDistance(PixelPoint other)
        {
            return (ushort)Math.Max(Math.Abs((this.xCoord - other.xCoord)), Math.Abs((this.yCoord - other.yCoord))); 
        }
        public override string ToString()
        {
            return $"({this.xCoord},{this.yCoord})";
        }
    }    
    /// <summary>
    /// stores information about the cluster accessible from cl file
    /// </summary>
    public struct ClusterInfo
    { 
        public double FirstToA { get; set; }
        public uint PixCount { get; set; }
        public ulong ByteStart { get; set; }
        public ulong LineStart { get; set; }
        public override string ToString()
        {
            return $"{FirstToA} {PixCount} {LineStart} {ByteStart}";
        }

    }
    public class ClusterInfoCollection : IEnumerable<ClusterInfo>
    {
        public readonly StreamReader ClFile;
        public readonly StreamReader PxFile;
        public int CurrentIndex { get; private set; } = 0;
        public ClusterInfoCollection(StreamReader clFile)
        {
            this.ClFile = clFile;
        }
        public ClusterInfoCollection(StreamReader clFile, StreamReader pxFile)
        {
            this.ClFile = clFile;
            this.PxFile = pxFile;
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        public IEnumerator<ClusterInfo> GetEnumerator()
        {
            this.ClFile.BaseStream.Position = 0;
            if (this.PxFile != null)
            {
                this.PxFile.BaseStream.Position = 0;
                this.PxFile.DiscardBufferedData();
            }
            this.ClFile.DiscardBufferedData();
            
            while (ClFile.BaseStream.Position < ClFile.BaseStream.Length)
            {
                string[] tokens = ClFile.ReadLine().Split();
                var clInfo = new ClusterInfo()
                {
                    FirstToA = double.Parse(tokens[0]),
                    PixCount = uint.Parse(tokens[1]),
                    LineStart = ulong.Parse(tokens[2]),
                    ByteStart = ulong.Parse(tokens[3])
                };
                CurrentIndex++;
                yield return clInfo;
            }
        }

    }
    /// <summary>
    /// represents attributes of a cluster that can be calculated and serialized
    /// </summary>
    public enum ClusterAttribute
    {
        TotalEnergy, PixelCount, AverageEnergy, MaxEnergy,
        Convexity, Width, Branches, CrosspointCount,
        BranchCount, RelativeHaloSize, VertexCount, StdOfEnergy, StdOfArrival,RelLowEnergyPixels,
        Class, ClFile, PxFile, ClIndex
        
    }
    /// <summary>
    /// attributes of a single branch in the cluster
    /// </summary>
    public enum BranchAttribute
    {
        StartPoint, Length, SubBranches, BranchEnergy

    }
    /// <summary>
    /// attribute conversion between enum and string
    /// </summary>
    public static class AttrExtensions
    {
        public static string AttrToString(this ClusterAttribute attribute)
        {
            var fullName = attribute.ToString();
            return fullName.Substring(fullName.LastIndexOf('.') + 1);
        }
        public static ClusterAttribute ToAttribute(this string attributeName)
        {
            Enum.TryParse(attributeName, out ClusterAttribute enumAttribute);
            return enumAttribute;
            
        }
    }
    public static class NumExtensions
    {
        public static double Sqr(this double value) => value * value;
        public static int Sqr(this int value) => value * value;

    }
}
