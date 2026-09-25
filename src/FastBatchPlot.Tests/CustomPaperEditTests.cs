using System;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class CustomPaperEditTests
    {
        private static PlotFrame Frame() => new PlotFrame { MinX = 100, MinY = 200, MaxX = 42100, MaxY = 29900,
            CalculatedScale = 100, IsLandscape = true, DetectedPaper = new PaperSize("A3", 420, 297) };
        [Fact]
        public void SmallerPaperRequiresExplicitFitAndDoesNotModifyOriginal()
        {
            var original = Frame(); var smaller = CustomPaperEdit.Preview(original, 297, 210, false);
            Assert.Throws<InvalidOperationException>(() => PlotPlanBuilder.Create(smaller, new PlotConfig()));
            var fitted = CustomPaperEdit.Preview(original, 297, 210, true);
            var plan = PlotPlanBuilder.Create(fitted, new PlotConfig());
            Assert.Equal(297, plan.PaperWidthMm); Assert.Equal(210, plan.PaperHeightMm);
            Assert.Equal(Math.Max(42000.0 / 297, 29700.0 / 210), fitted.CalculatedScale);
            Assert.Equal(100, original.CalculatedScale); Assert.Equal("A3", original.DetectedPaper.Name);
            Assert.NotSame(fitted.DetectedPaper, original.DetectedPaper);
        }
        [Fact]
        public void PortraitFitUsesCroppedBoundsAndAppliesMarginsOnlyOnce()
        {
            var original = Frame(); original.PrintBounds = new Rect2D(100, 200, 10100, 20200);
            var fitted = CustomPaperEdit.Preview(original, 200.125, 400.25, true);
            var plan = PlotPlanBuilder.Create(fitted, new PlotConfig { MarginMm = 2 });
            Assert.False(fitted.IsLandscape); Assert.Equal(204.125, plan.PaperWidthMm); Assert.Equal(404.25, plan.PaperHeightMm);
            Assert.Equal(10000 / 200.125, fitted.CalculatedScale); Assert.Equal(2, plan.ContentLeftMm, 8);
            Assert.Equal(2, plan.ContentBottomMm, 8); Assert.Equal(100, plan.MinX);
            Assert.Equal(100, original.CalculatedScale);
        }
        [Theory]
        [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(0)]
        [InlineData(-2)] [InlineData(100001)] [InlineData(210.1234)]
        public void InvalidDimensionsRejected(double value) => Assert.Throws<ArgumentException>(() => CustomPaperEdit.Preview(Frame(), value, 297, true));
    }
}
