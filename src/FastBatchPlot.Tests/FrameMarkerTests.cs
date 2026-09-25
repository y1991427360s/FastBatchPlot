using System;
using System.Collections.Generic;
using FastBatchPlot.Core.Models;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class FrameMarkerTests
    {
        [Fact]
        public void NumberLayout_CalculatesExpectedBounds()
        {
            double minX = 0;
            double minY = 0;
            double maxX = 420;
            double maxY = 297;

            double frameW = maxX - minX;
            double frameH = maxY - minY;

            double digitH = frameH * 0.65;
            double digitW = digitH * 0.48;
            double spacing = digitH * 0.22;

            int number = 17;
            string s = number.ToString();
            int n = s.Length;
            double totalW = n * digitW + (n - 1) * spacing;

            Assert.True(totalW <= frameW * 0.80);
            Assert.Equal(193.05, digitH, 2);
            Assert.True(digitH > 100);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(8)]
        [InlineData(17)]
        [InlineData(100)]
        public void NumberLayout_ShouldScaleDownWhenExceedingMaxWidth(int number)
        {
            // A narrow vertical frame
            double minX = 0, minY = 0, maxX = 50, maxY = 500;
            double frameW = maxX - minX;
            double frameH = maxY - minY;

            double digitH = frameH * 0.65;
            double digitW = digitH * 0.48;
            double spacing = digitH * 0.22;

            string s = number.ToString();
            int n = s.Length;
            double totalW = n * digitW + (n - 1) * spacing;

            if (totalW > frameW * 0.80)
            {
                double scale = (frameW * 0.80) / totalW;
                digitH *= scale;
                digitW *= scale;
                spacing *= scale;
                totalW = n * digitW + (n - 1) * spacing;
            }

            Assert.True(totalW <= frameW * 0.80 + 1e-6);
        }

        [Fact]
        public void SelectedFrame_ShouldHaveValidDimensions()
        {
            var frame = new PlotFrame
            {
                MinX = 100,
                MinY = 200,
                MaxX = 941,
                MaxY = 794,
                OrderIndex = 8,
                IsSelected = true,
                DetectedPaper = new PaperSize("A1", 841, 594, true)
            };

            Assert.Equal(841, frame.Width);
            Assert.Equal(594, frame.Height);
            Assert.True(frame.IsSelected);
            Assert.Equal("A1", frame.DetectedPaper.Name);
        }
    }
}
