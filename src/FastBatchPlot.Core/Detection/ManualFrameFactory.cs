using System;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Paper;

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

            var exactResult = PaperSizeDetector.AtExactScale(bounds.Width, bounds.Height, scale);
            PaperSizeSafety.ValidatePhysicalSize(exactResult.Paper.WidthMm, exactResult.Paper.HeightMm, scale);

            return new PlotFrame {
                MinX = bounds.MinX, MinY = bounds.MinY, MaxX = bounds.MaxX, MaxY = bounds.MaxY,
                Type = FrameType.PickWindow, SourceDocumentId = documentId, SourceLayoutId = spaceId,
                LayoutName = layoutName, CalculatedScale = scale, DetectedPaper = exactResult.Paper,
                IsLandscape = exactResult.IsLandscape,
                TitleInfo = new TitleBlockInfo { DrawingName = "手工范围" }
            };
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
