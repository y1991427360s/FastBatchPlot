using System;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Detection
{
    public static class ManualFrameFactory
    {
        public static PlotFrame Create(Rect2D bounds, double scale, string documentId, string spaceId, string layoutName)
        {
            if (!Finite(bounds.MinX) || !Finite(bounds.MinY) || !Finite(bounds.MaxX) || !Finite(bounds.MaxY) ||
                bounds.Width <= 0 || bounds.Height <= 0 || !Finite(scale) || scale <= 0)
                throw new ArgumentException("范围和比例必须为有效正数。");
            if (string.IsNullOrWhiteSpace(documentId) || string.IsNullOrWhiteSpace(spaceId))
                throw new ArgumentException("手工范围缺少来源文档或空间。");
            double width = bounds.Width / scale, height = bounds.Height / scale;
            if (!Finite(width) || !Finite(height) || width <= 0 || height <= 0)
                throw new ArgumentException("纸张尺寸超出有效范围。");
            PaperSize paper = new PaperSize("自定义", width, height, width >= height);
            foreach (var standard in PaperSize.StandardSizes)
            {
                if (Math.Abs(standard.LongerEdgeMm - Math.Max(width,height)) <= 1.5 &&
                    Math.Abs(standard.ShorterEdgeMm - Math.Min(width,height)) <= 1.5)
                {
                    paper.Name = standard.Name;
                    paper.StandardName = standard.StandardName;
                    break;
                }
            }
            return new PlotFrame {
                MinX = bounds.MinX, MinY = bounds.MinY, MaxX = bounds.MaxX, MaxY = bounds.MaxY,
                Type = FrameType.PickWindow, SourceDocumentId = documentId, SourceLayoutId = spaceId,
                LayoutName = layoutName, CalculatedScale = scale, DetectedPaper = paper,
                IsLandscape = width >= height,
                TitleInfo = new TitleBlockInfo { DrawingName = "手工范围" }
            };
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
