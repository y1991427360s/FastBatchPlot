using System;
using System.Globalization;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Planning
{
    /// <summary>纸张编辑生成独立候选，不修改原图框；全批预览通过后再应用。</summary>
    public static class CustomPaperEdit
    {
        public static PlotFrame Preview(PlotFrame frame, double widthMm, double heightMm, bool fitContent)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (!ValidDimension(widthMm) || !ValidDimension(heightMm))
                throw new ArgumentException("自定义纸张宽高必须在 1 到 100000 毫米之间，最多三位小数。");
            var bounds = frame.PrintBounds ?? new Common.Rect2D(frame.MinX, frame.MinY, frame.MaxX, frame.MaxY);
            double width = bounds.MaxX - bounds.MinX, height = bounds.MaxY - bounds.MinY;
            if (!Finite(width) || !Finite(height) || width <= 0 || height <= 0)
                throw new ArgumentException("打印范围宽高必须为有限正数。");
            double scale = fitContent ? Math.Max(width / widthMm, height / heightMm) : frame.CalculatedScale;
            if (!Finite(scale) || scale <= 0) throw new ArgumentException("打印比例必须为有限正数。");
            string name = "自定义 " + widthMm.ToString("0.###", CultureInfo.InvariantCulture) + "×" + heightMm.ToString("0.###", CultureInfo.InvariantCulture);
            return new PlotFrame {
                Id = frame.Id, OrderIndex = frame.OrderIndex,
                MinX = frame.MinX, MinY = frame.MinY, MaxX = frame.MaxX, MaxY = frame.MaxY,
                PrintBounds = frame.PrintBounds, TitleInfo = frame.TitleInfo,
                IsLandscape = widthMm >= heightMm, CalculatedScale = scale,
                DetectedPaper = new PaperSize(name, widthMm, heightMm, widthMm >= heightMm) { StandardName = "" }
            };
        }
        private static bool ValidDimension(double value) => Finite(value) && value >= 1 && value <= 100000 && Math.Abs(value - Math.Round(value, 3)) < 1e-8;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
