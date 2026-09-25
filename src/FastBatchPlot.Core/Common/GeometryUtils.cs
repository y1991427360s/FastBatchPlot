using System;
using System.Collections.Generic;

namespace FastBatchPlot.Core.Common
{
    public struct Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double DistanceTo(Point2D other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public override string ToString() => $"({X:F2}, {Y:F2})";
    }

    public struct Rect2D
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }

        public double Width => Math.Abs(MaxX - MinX);
        public double Height => Math.Abs(MaxY - MinY);
        public double Area => Width * Height;
        public Point2D Center => new Point2D((MinX + MaxX) / 2.0, (MinY + MaxY) / 2.0);

        public Rect2D(double minX, double minY, double maxX, double maxY)
        {
            MinX = Math.Min(minX, maxX);
            MinY = Math.Min(minY, maxY);
            MaxX = Math.Max(minX, maxX);
            MaxY = Math.Max(minY, maxY);
        }

        public bool Contains(Rect2D other, double tolerance = 1e-3)
        {
            return MinX - tolerance <= other.MinX &&
                   MinY - tolerance <= other.MinY &&
                   MaxX + tolerance >= other.MaxX &&
                   MaxY + tolerance >= other.MaxY;
        }

        public bool IsNearlyEqual(Rect2D other, double tolerancePercent = 0.02)
        {
            double tolX = Math.Max(Width, other.Width) * tolerancePercent;
            double tolY = Math.Max(Height, other.Height) * tolerancePercent;
            return Math.Abs(MinX - other.MinX) <= tolX &&
                   Math.Abs(MinY - other.MinY) <= tolY &&
                   Math.Abs(MaxX - other.MaxX) <= tolX &&
                   Math.Abs(MaxY - other.MaxY) <= tolY;
        }
    }

    public static class GeometryUtils
    {
        /// <summary>
        /// 判断多边形是否为正交轴对齐矩形 (边与坐标轴大致平行)
        /// </summary>
        /// <summary>
        /// 去掉闭合重复点与共线中间点（部分图框多段线在边上多打了顶点），返回简化后的角点序列。
        /// </summary>
        public static List<Point2D> SimplifyClosedPolygon(IList<Point2D> pts, double closeTolerance = 2.0)
        {
            var list = new List<Point2D>();
            if (pts == null) return list;
            foreach (var p in pts)
                if (list.Count == 0 || list[list.Count - 1].DistanceTo(p) > 1e-6) list.Add(p);
            if (list.Count > 1 && list[0].DistanceTo(list[list.Count - 1]) < closeTolerance) list.RemoveAt(list.Count - 1);
            bool removed = true;
            while (removed && list.Count > 3)
            {
                removed = false;
                for (int i = 0; i < list.Count && list.Count > 3; i++)
                {
                    var a = list[(i - 1 + list.Count) % list.Count]; var b = list[i]; var c = list[(i + 1) % list.Count];
                    double abx = b.X - a.X, aby = b.Y - a.Y, bcx = c.X - b.X, bcy = c.Y - b.Y;
                    double lenAb = Math.Sqrt(abx * abx + aby * aby), lenBc = Math.Sqrt(bcx * bcx + bcy * bcy);
                    if (lenAb < 1e-9 || lenBc < 1e-9) { list.RemoveAt(i); removed = true; break; }
                    // 同向共线（夹角 < 0.1°）的中间点对矩形形状没有贡献。
                    double cross = Math.Abs(abx * bcy - aby * bcx) / (lenAb * lenBc);
                    double dot = (abx * bcx + aby * bcy) / (lenAb * lenBc);
                    if (cross < 0.0017 && dot > 0) { list.RemoveAt(i); removed = true; break; }
                }
            }
            return list;
        }

        public static bool IsOrthogonalRectangle(IList<Point2D> pts, double tolerance = 5.0)
        {
            if (pts == null) return false;
            var p = (pts.Count == 5 && pts[0].DistanceTo(pts[4]) < 2.0)
                ? new List<Point2D> { pts[0], pts[1], pts[2], pts[3] }
                : pts;

            if (p.Count != 4) return false;

            // 检查对边长度相等，且对角线长度相等
            double d01 = p[0].DistanceTo(p[1]);
            double d12 = p[1].DistanceTo(p[2]);
            double d23 = p[2].DistanceTo(p[3]);
            double d30 = p[3].DistanceTo(p[0]);

            if (d01 < tolerance || d12 < tolerance) return false;

            bool oppSidesEqual = Math.Abs(d01 - d23) <= Math.Max(d01, d23) * 0.05 &&
                                 Math.Abs(d12 - d30) <= Math.Max(d12, d30) * 0.05;

            double diag1 = p[0].DistanceTo(p[2]);
            double diag2 = p[1].DistanceTo(p[3]);
            bool diagsEqual = Math.Abs(diag1 - diag2) <= Math.Max(diag1, diag2) * 0.05;

            if (!oppSidesEqual || !diagsEqual) return false;

            // 全部边必须接近坐标轴、相邻边接近直角，且四次转向一致。
            // 转向一致排除凹四边形和蝴蝶形自交；不能仅凭对边/对角线等长判断矩形。
            int turnSign = 0;
            for (int i = 0; i < 4; i++)
            {
                var a = p[i];
                var b = p[(i + 1) % 4];
                var c = p[(i + 2) % 4];
                double dx = b.X - a.X, dy = b.Y - a.Y;
                double nextDx = c.X - b.X, nextDy = c.Y - b.Y;
                double length = Math.Sqrt(dx * dx + dy * dy);
                double nextLength = Math.Sqrt(nextDx * nextDx + nextDy * nextDy);
                if (double.IsNaN(length) || double.IsInfinity(length) ||
                    double.IsNaN(nextLength) || double.IsInfinity(nextLength) ||
                    length <= 0 || nextLength <= 0 || length < tolerance || nextLength < tolerance)
                    return false;

                if (Math.Min(Math.Abs(dx), Math.Abs(dy)) / length >= 0.045)
                    return false;
                // 单位向量避免使用长度乘积，减少大坐标下的溢出风险。
                double dot = (dx / length) * (nextDx / nextLength) + (dy / length) * (nextDy / nextLength);
                if (Math.Abs(dot) > 0.05) return false;
                double cross = (dx / length) * (nextDy / nextLength) - (dy / length) * (nextDx / nextLength);
                int sign = Math.Sign(cross);
                if (sign == 0 || (turnSign != 0 && sign != turnSign)) return false;
                turnSign = sign;
            }

            return true;
        }

        /// <summary>
        /// 获取点集的轴对齐包围盒
        /// </summary>
        public static Rect2D GetBoundingBox(IEnumerable<Point2D> points)
        {
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            bool hasPoints = false;
            foreach (var p in points)
            {
                hasPoints = true;
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }

            if (!hasPoints) return new Rect2D(0, 0, 0, 0);
            return new Rect2D(minX, minY, maxX, maxY);
        }
    }
}
