using System;
using System.Collections.Generic;

namespace FastBatchPlot.Core.Paper
{
    /// <summary>
    /// 工程图纸标准比例匹配器
    /// </summary>
    public static class ScaleCalculator
    {
        public static readonly double[] StandardScales = new double[]
        {
            0.1, 0.2, 0.25, 0.5,
            1.0, 2.0, 2.5, 3.0, 4.0, 5.0,
            10.0, 15.0, 20.0, 25.0, 30.0, 40.0, 50.0, 60.0, 75.0, 80.0,
            100.0, 120.0, 150.0, 200.0, 250.0, 300.0, 400.0, 500.0,
            600.0, 800.0, 1000.0, 1200.0, 1500.0, 2000.0, 2500.0, 5000.0, 10000.0
        };

        /// <summary>
        /// 根据原始计算比例值，匹配最接近的标准工程比例
        /// </summary>
        public static double MatchClosestStandardScale(double rawScale, double maxErrorPercent = 0.15)
        {
            if (rawScale <= 0) return 100.0;

            double bestScale = rawScale;
            double minDiff = double.MaxValue;

            foreach (var s in StandardScales)
            {
                double diff = Math.Abs(s - rawScale) / rawScale;
                if (diff < minDiff)
                {
                    minDiff = diff;
                    bestScale = s;
                }
            }

            if (minDiff <= maxErrorPercent)
            {
                return bestScale;
            }

            // 若误差大于阈值，说明根本不是工程出图标准比例（如 1:74, 1:98, 1:2 等）
            return -1.0;
        }

        public static string FormatScale(double scale)
        {
            if (Math.Abs(scale - 1.0) < 1e-4) return "1:1";
            if (scale < 1.0) return $"{1.0 / scale:0.##}:1";
            return $"1:{scale:0.##}";
        }
    }
}
