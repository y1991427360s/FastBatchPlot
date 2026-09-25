using System;
using System.IO;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class GeneratedPdfMediaGuardTests
    {
        private static readonly string Configuration = Path.Combine(Path.GetTempPath(), "fbp-test.pc5");
        private static PlotPlan Plan() => PlotPlanBuilder.Create(new PlotFrame
        {
            MaxX = 630, MaxY = 297, CalculatedScale = 1, IsLandscape = true,
            DetectedPaper = new PaperSize("extended", 630, 297)
        }, new PlotConfig { MarginMm = 2 });
        private static PlotMedia Media() => new PlotMedia
        {
            Name = "task-media", WidthMm = 634, HeightMm = 301,
            PrintableWidthMm = 634, PrintableHeightMm = 301
        };

        [Fact]
        public void ExactGeneratedMediaIncludesPositiveMargins()
            => GeneratedPdfMediaGuard.Validate(Plan(), Configuration, Configuration, "task-media", Media());

        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
        [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
        public void DriverSubstitutionIsRejected(int change)
        {
            var media = Media();
            switch (change)
            {
                case 0: media.WidthMm -= 0.1; break;
                case 1: media.HeightMm += 0.1; break;
                case 2: media.PrintableLeftMm = 0.1; media.PrintableWidthMm -= 0.1; break;
                case 3: media.PrintableBottomMm = 0.1; media.PrintableHeightMm -= 0.1; break;
                case 4: media.Name = "driver-default"; break;
                case 5: media.WidthMm = double.NaN; break;
                case 6: media.PrintableHeightMm = double.PositiveInfinity; break;
                case 7: media.WidthMm = 301; media.HeightMm = 634; break;
            }
            Assert.Throws<InvalidOperationException>(() =>
                GeneratedPdfMediaGuard.Validate(Plan(), Configuration, Configuration, "task-media", media));
        }

        [Theory]
        [InlineData("")] [InlineData("fbp-test.pc5")] [InlineData("other.pc5")]
        public void MissingRelativeOrDifferentConfigurationIsRejected(string loaded)
        {
            if (loaded == "other.pc5") loaded = Path.Combine(Path.GetTempPath(), loaded);
            Assert.Throws<InvalidOperationException>(() =>
                GeneratedPdfMediaGuard.Validate(Plan(), Configuration, loaded, "task-media", Media()));
        }

        [Fact]
        public void SmallDriverUnitRoundingIsAllowed()
        {
            var media = Media();
            media.WidthMm += 0.001; media.PrintableWidthMm += 0.001;
            GeneratedPdfMediaGuard.Validate(Plan(), Configuration, Configuration, "task-media", media);
        }
    }
}
