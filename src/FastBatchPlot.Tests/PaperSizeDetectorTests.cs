using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Paper;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class PaperSizeDetectorTests
    {
        [Theory]
        [InlineData(84100, 59400, "A1", 100, true)]       // A1 横向 1:100
        [InlineData(59400, 84100, "A1", 100, false)]      // A1 纵向 1:100
        [InlineData(118900, 84100, "A0", 100, true)]     // A0 横向 1:100
        [InlineData(59400, 42000, "A2", 100, true)]       // A2 横向 1:100
        [InlineData(42000, 29700, "A3", 100, true)]       // A3 横向 1:100
        [InlineData(29700, 21000, "A4", 100, true)]       // A4 横向 1:100
        [InlineData(126150, 59400, "A1+1/2", 100, true)]  // A1+1/2 加长横向 1:100
        [InlineData(84000, 29700, "A3+1", 100, true)]     // A3+1 横向 1:100 (840x297)
        [InlineData(84100, 29700, "A3+1", 100, true)]     // A3+1 横向 1:100 (841x297 容差)
        [InlineData(29700, 84000, "A3+1", 100, false)]    // A3+1 纵向 1:100
        [InlineData(118800, 42000, "A2+1", 100, true)]    // A2+1 横向 1:100 (1188x420)
        [InlineData(118900, 42000, "A2+1", 100, true)]    // A2+1 横向 1:100 (1189x420 容差)
        [InlineData(42000, 118800, "A2+1", 100, false)]   // A2+1 纵向 1:100
        [InlineData(840.0, 297.0, "A3+1", 1, true)]       // 布局空间 1:1 A3+1
        [InlineData(1188.0, 420.0, "A2+1", 1, true)]     // 布局空间 1:1 A2+1
        [InlineData(420.0, 297.0, "A3", 1, true)]         // 布局空间 1:1 A3
        [InlineData(841 * 150, 594 * 150, "A1", 150, true)] // A1 横向 1:150
        // A 系列 2 倍关系回归：布局 1:1 与非 1:100 比例不能被 1:100 偏好吞掉
        [InlineData(841.0, 594.0, "A1", 1, true)]          // 布局 1:1 A1（不是 1:2 的 A3）
        [InlineData(1189.0, 841.0, "A0", 1, true)]         // 布局 1:1 A0
        [InlineData(594.0, 420.0, "A2", 1, true)]          // 布局 1:1 A2
        [InlineData(297.0, 210.0, "A4", 1, true)]          // 布局 1:1 A4（不是 2:1 的 A2）
        [InlineData(42050, 29700, "A1", 50, true)]         // A1 1:50（不是 1:100 的 A3）
        [InlineData(420 * 150, 297 * 150, "A3", 150, true)] // A3 1:150（不是 1:75 的 A1）
        [InlineData(594 * 200, 420 * 200, "A2", 200, true)] // A2 1:200
        [InlineData(189200, 59400, "A1+5/4", 100, true)]   // GB/T 50001 加长 1892×594
        [InlineData(1783.0, 420.0, "A2+2", 1, true)]       // A2 加长超过 +1
        public void Detect_ShouldMatchCorrectPaperAndScale(double width, double height, string expectedPaper, double expectedScale, bool expectedLandscape)
        {
            var result = PaperSizeDetector.Detect(width, height);

            Assert.Equal(expectedPaper, result.Paper.StandardName);
            Assert.Equal(expectedScale, result.Scale);
            Assert.Equal(expectedLandscape, result.IsLandscape);
        }

        [Fact]
        public void RangeLargerThanStandardSheetGetsExactCustomPaper()
        {
            // 会签栏伸出外框：纸张 = 范围 / 比例，出图时不会因放不下而失败
            var result = PaperSizeDetector.Detect(861, 594);
            Assert.Equal(1, result.Scale);
            Assert.Equal(861, result.Paper.WidthMm, 2);
            Assert.Equal(594, result.Paper.HeightMm, 2);
            // 略小于标准纸时仍按标准纸输出
            var smaller = PaperSizeDetector.Detect(840.6, 593.8);
            Assert.Equal("A1", smaller.Paper.Name);
        }

        [Fact]
        public void MirroredBlockKeepsItsScaleHint()
        {
            var frames = FastBatchPlot.Core.Detection.BlockFrameDetector.ProcessBlockCandidates(new[] {
                new FastBatchPlot.Core.Detection.RawBlockCandidate { Handle = "1", BlockName = "TK", ScaleX = -1, ScaleY = 1,
                    Bounds = new FastBatchPlot.Core.Common.Rect2D(0, 0, 841, 594) } });
            Assert.Single(frames);
            Assert.Equal("A1", frames[0].DetectedPaper.StandardName);
            Assert.Equal(1, frames[0].CalculatedScale);
        }

        [Fact]
        public void Detect_A3Plus1_And_A2Plus1_AtExactScale()
        {
            var r840 = PaperSizeDetector.AtExactScale(84000, 29700, 100);
            Assert.Equal("A3+1", r840.Paper.StandardName);
            var r841 = PaperSizeDetector.AtExactScale(84100, 29700, 100);
            Assert.Equal("A3+1", r841.Paper.StandardName);

            var r1188 = PaperSizeDetector.AtExactScale(118800, 42000, 100);
            Assert.Equal("A2+1", r1188.Paper.StandardName);
            var r1189 = PaperSizeDetector.AtExactScale(118900, 42000, 100);
            Assert.Equal("A2+1", r1189.Paper.StandardName);
        }

        [Fact]
        public void Detect_WithPreferredScale_ShouldDisambiguateScale()
        {
            // 42050 x 29700 当指定 1:50 比例时，精确匹配为 A1
            var result50 = PaperSizeDetector.Detect(42050, 29700, preferredScale: 50);
            Assert.Equal("A1", result50.Paper.StandardName);
            Assert.Equal(50, result50.Scale);

            // 当指定 1:100 比例时，匹配为 A3
            var result100 = PaperSizeDetector.Detect(42000, 29700, preferredScale: 100);
            Assert.Equal("A3", result100.Paper.StandardName);
            Assert.Equal(100, result100.Scale);
        }

        [Fact]
        public void Detect_CaseA_594x420_ShouldBeA2_Scale1_HighConfidence()
        {
            var res = PaperSizeDetector.Detect(594, 420);
            Assert.Equal("A2", res.Paper.Name);
            Assert.Equal(1.0, res.Scale);
            Assert.True(res.MatchScore <= PaperSizeDetector.AcceptableMatchError);
        }

        [Fact]
        public void Detect_CaseB_42000x29700_ShouldBeA3_Scale100_HighConfidence()
        {
            var res = PaperSizeDetector.Detect(42000, 29700);
            Assert.Equal("A3", res.Paper.Name);
            Assert.Equal(100.0, res.Scale);
            Assert.True(res.MatchScore <= PaperSizeDetector.AcceptableMatchError);
        }

        [Fact]
        public void Detect_CaseC_841x594_ShouldBeA1_Scale1_HighConfidence()
        {
            var res = PaperSizeDetector.Detect(841, 594);
            Assert.Equal("A1", res.Paper.Name);
            Assert.Equal(1.0, res.Scale);
            Assert.True(res.MatchScore <= PaperSizeDetector.AcceptableMatchError);
        }

        [Fact]
        public void Detect_CaseD_84100x59400_ShouldBeA1_Scale100_HighConfidence()
        {
            var res = PaperSizeDetector.Detect(84100, 59400);
            Assert.Equal("A1", res.Paper.Name);
            Assert.Equal(100.0, res.Scale);
            Assert.True(res.MatchScore <= PaperSizeDetector.AcceptableMatchError);
        }

        [Fact]
        public void Detect_CaseE_Nonstandard_50000x10000_ShouldNotBeHighConfidence()
        {
            var res = PaperSizeDetector.Detect(50000, 10000);
            // 50000x10000 长宽比 5:1，非标准工程图纸比例，MatchScore 应大于 AcceptableMatchError
            Assert.True(res.MatchScore > PaperSizeDetector.AcceptableMatchError);
        }
    }
}
