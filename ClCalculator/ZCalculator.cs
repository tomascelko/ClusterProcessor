﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ClusterCalculator
{
    public interface IZCalculator
    {
        PointD3[] TransformPoints(IList<PixelPoint> cluster);
        double CalculateZ(PixelPoint point, double firstToA);
    }
    /// <summary>
    /// calculates the relative z coordinate with respect to the first arrived pixel
    /// </summary>
    public class ZCalculator : IZCalculator
    {
        //Set default: 500 um, 110 V depl, bias: 230 V, mob: 45 V s / um2
        private Configuration Configuration { get; }

        public ZCalculator() 
        {
            Configuration = new Configuration(thickness: 500d, mobility: 45d, ud: 110d, ub: 230d);
        }
        public PointD3[] TransformPoints(IList<PixelPoint> points2D)
        {
            var points3D = new PointD3[points2D.Count];
            var firstToA = points2D.Min(point => point.ToA);
            for (int i = 0; i < points3D.Length; i++)
            {
                var z = CalculateZ(points2D[i], firstToA);
                points3D[i] = new PointD3(points2D[i].xCoord, points2D[i].yCoord, (float)z);
            }
            return points3D;
        }
        public double CalculateZ(PixelPoint point, double FirstToA)
        {
            var relativeToA = point.ToA - FirstToA;
            return (Configuration.Thick / (2 * Configuration.Ud)) * (Configuration.Ud + Configuration.Ub) *
                (1 - Math.Exp((-2) * Configuration.Ud * Configuration.Mob * relativeToA / Math.Pow(Configuration.Thick, 2)));
        }
    }
    public struct PointD3
    {
        public float X;
        public float Y;
        public float Z;
        public PointD3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
    struct Configuration
    {
        public double Thick { get; }
        public double Mob { get; }
        public double Ud { get; }
        public double Ub {get;}
        public Configuration(double thickness, double mobility, double ud, double ub)
        {
            Thick = thickness;
            Mob = mobility;
            Ud = ud;
            Ub = ub;
        }
    }
    

}
