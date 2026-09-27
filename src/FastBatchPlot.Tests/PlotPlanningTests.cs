using System;
using System.IO;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class PlotPlanningTests
    {
        private static PlotFrame Frame() => new PlotFrame
        {
            MinX = 100, MinY = 200, MaxX = 42100, MaxY = 29900,
            CalculatedScale = 100, DetectedPaper = new PaperSize("A3", 420, 297), IsLandscape = true
        };

        [Fact]
        public void PositiveMarginEnlargesPaperAndPreservesScaleAndWindow()
        {
            var plan = PlotPlanBuilder.Create(Frame(), new PlotConfig { MarginMm = 5 });
            Assert.Equal(430, plan.PaperWidthMm);
            Assert.Equal(307, plan.PaperHeightMm);
            Assert.Equal(100, plan.ScaleDenominator);
            Assert.Equal(100, plan.MinX);
            Assert.Equal(42100, plan.MaxX);
        }

        [Fact]
        public void FractionalPositiveMarginCannotReuseTheOriginalPaper()
        {
            var plan = PlotPlanBuilder.Create(Frame(), new PlotConfig { MarginMm = 0.1 });
            Assert.Throws<InvalidOperationException>(() => PlotPlanBuilder.SelectMedia(plan,
                new[] { Media("original", 420, 297) }));
        }

        [Fact]
        public void PrintableEdgeCannotSilentlyClipAFractionOfAMillimeter()
        {
            var plan = PlotPlanBuilder.Create(Frame(), new PlotConfig());
            var media = Media("clipped", 420, 297);
            media.PrintableWidthMm = 419.8;
            Assert.False(PlotPlanBuilder.CanPlace(plan, media));
        }

        [Fact]
        public void NegativeMarginKeepsPaperAndShrinksContent()
        {
            var plan = PlotPlanBuilder.Create(Frame(), new PlotConfig { MarginMm = -5 });
            Assert.Equal(420, plan.PaperWidthMm);
            Assert.Equal(297, plan.PaperHeightMm);
            Assert.True(plan.ScaleDenominator > 100);
            Assert.Equal(287, plan.ContentHeightMm, 8);
            Assert.True(plan.ContentWidthMm <= 410);
            Assert.Equal(42100, plan.MaxX);
        }

        [Fact]
        public void ZeroMarginHonorsExactUserScale()
        {
            var frame = Frame();
            frame.CalculatedScale = 101;
            var plan = PlotPlanBuilder.Create(frame, new PlotConfig());
            Assert.Equal(101, plan.ScaleDenominator);
        }

        [Fact]
        public void OversizeAtExactScaleFailsInsteadOfFitting()
        {
            var frame = Frame();
            frame.CalculatedScale = 50;
            Assert.Throws<InvalidOperationException>(() => PlotPlanBuilder.Create(frame, new PlotConfig()));
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(0)]
        [InlineData(-100)]
        public void InvalidScaleIsRejected(double scale)
        {
            var frame = Frame();
            frame.CalculatedScale = scale;
            Assert.Throws<ArgumentException>(() => PlotPlanBuilder.Create(frame, new PlotConfig()));
        }

        [Fact]
        public void ExcessiveNegativeMarginIsRejected() =>
            Assert.Throws<ArgumentException>(() => PlotPlanBuilder.Create(Frame(), new PlotConfig { MarginMm = -149 }));

        [Fact]
        public void ExtendedPaperNeverFallsBackToBasePaper()
        {
            var frame = Frame();
            frame.DetectedPaper = new PaperSize("A3+1/2", 630, 297);
            var plan = PlotPlanBuilder.Create(frame, new PlotConfig());
            Assert.Throws<InvalidOperationException>(() => PlotPlanBuilder.SelectMedia(plan,
                new[] { Media("A3", 420, 297) }));
        }

        [Fact]
        public void MatchingUsesMeasuredSizeAndPrintableArea()
        {
            var plan = PlotPlanBuilder.Create(Frame(), new PlotConfig());
            var fullBleed = Media("arbitrary name", 297, 420);
            var clipped = Media("A3", 420, 297);
            clipped.PrintableWidthMm = 410;
            var match = PlotPlanBuilder.SelectMedia(plan, new[] { clipped, fullBleed });
            Assert.Same(fullBleed, match);
            Assert.True(PlotPlanBuilder.RotateMedia(plan, match));
        }

        [Fact]
        public void PositiveMarginRequiresLargerActualMedia()
        {
            var plan = PlotPlanBuilder.Create(Frame(), new PlotConfig { MarginMm = 5 });
            Assert.Throws<InvalidOperationException>(() => PlotPlanBuilder.SelectMedia(plan,
                new[] { Media("A3", 420, 297) }));
        }

        [Fact]
        public void UnknownOutputIsRejected() =>
            Assert.Throws<ArgumentException>(() => PlotPlanBuilder.Create(Frame(), new PlotConfig { ExportFormat = (PlotExportFormat)999 }));

        [Fact]
        public void MissingNewOutputCannotUseOldFileAsSuccess()
        {
            string dir = Path.Combine(Path.GetTempPath(), "plot-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string output = Path.Combine(dir, "existing.pdf");
                File.WriteAllText(output, "old output");
                string temp = PlotOutputCommitter.CreateTemporaryPath(output);
                Assert.Throws<IOException>(() => PlotOutputCommitter.ValidateAndCommit(temp, output, PlotPlanBuilder.Create(Frame(), new PlotConfig())));
                Assert.Equal("old output", File.ReadAllText(output));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void CommitChecksPdfPaperAndPreservesExistingOutput()
        {
            string dir = Path.Combine(Path.GetTempPath(), "plot-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string output = Path.Combine(dir, "result.pdf");
                string temp = PlotOutputCommitter.CreateTemporaryPath(output);
                var plan = PlotPlanBuilder.Create(Frame(), new PlotConfig());
                using (var document = new PdfDocument())
                {
                    var page = document.AddPage();
                    page.Width = XUnit.FromMillimeter(420);
                    page.Height = XUnit.FromMillimeter(297);
                    document.Save(temp);
                }
                File.WriteAllText(output, "old output");
                Assert.Throws<IOException>(() => PlotOutputCommitter.ValidateAndCommit(temp, output, plan));
                Assert.Equal("old output", File.ReadAllText(output));
                File.Delete(output);
                PlotOutputCommitter.ValidateAndCommit(temp, output, plan);
                Assert.True(File.Exists(output));
                Assert.False(File.Exists(temp));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Theory]
        [InlineData(419.8, false)]
        [InlineData(420.001, true)]
        public void FinalPdfDistinguishesClippingFromUnitRounding(double width, bool valid)
        {
            string path = Path.Combine(Path.GetTempPath(), "plot-precision-" + Guid.NewGuid().ToString("N") + ".pdf");
            try
            {
                using (var document = new PdfDocument())
                {
                    var page = document.AddPage();
                    page.Width = XUnit.FromMillimeter(width); page.Height = XUnit.FromMillimeter(297);
                    document.Save(path);
                }
                var plan = PlotPlanBuilder.Create(Frame(), new PlotConfig());
                if (valid) PlotOutputCommitter.Validate(path, plan);
                else Assert.Throws<IOException>(() => PlotOutputCommitter.Validate(path, plan));
            }
            finally { File.Delete(path); }
        }

        private static PlotMedia Media(string name, double width, double height) => new PlotMedia
        {
            Name = name, WidthMm = width, HeightMm = height,
            PrintableWidthMm = width, PrintableHeightMm = height
        };

        [Fact]
        public void PlotPlanBuilderRejectsMicroPaperBypassingManualFrameFactory()
        {
            // 构造绕过 ManualFrameFactory 的微型纸张 PlotFrame
            var badFrame = new PlotFrame
            {
                MinX = 0, MinY = 0, MaxX = 594, MaxY = 420,
                CalculatedScale = 100,
                DetectedPaper = new PaperSize("自定义", 5.94, 4.2),
                IsLandscape = true
            };

            var ex = Assert.Throws<ArgumentException>(() => PlotPlanBuilder.Create(badFrame, new PlotConfig()));
            Assert.Contains("异常纸张尺寸", ex.Message);
            Assert.Contains("5.94", ex.Message);
        }

        [Fact]
        public void PlotPlanBuilderAllowsLegitimateCustomPaper()
        {
            // 500x100mm 合法自定义长条图纸依然允许正常规划
            var customFrame = new PlotFrame
            {
                MinX = 0, MinY = 0, MaxX = 50000, MaxY = 10000,
                CalculatedScale = 100,
                DetectedPaper = new PaperSize("自定义", 500, 100),
                IsLandscape = true
            };

            var plan = PlotPlanBuilder.Create(customFrame, new PlotConfig());
            Assert.Equal(500, plan.PaperWidthMm);
            Assert.Equal(100, plan.PaperHeightMm);
            Assert.Equal(100, plan.ScaleDenominator);
        }
    }
}