using FastBatchPlot.Core.Planning;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class PaperDimensionTextTests
    {
        [Theory]
        [InlineData("841 × 594",841,594)]
        [InlineData("420.125x297.5",420.125,297.5)]
        [InlineData("210*297",210,297)]
        public void ParsesDimensionsWithoutLosingDecimals(string text,double width,double height)
        {
            Assert.True(PaperDimensionText.TryParse(text,out var w,out var h));Assert.Equal(width,w);Assert.Equal(height,h);
            Assert.True(PaperDimensionText.TryParse(PaperDimensionText.Format(w,h),out w,out h));Assert.Equal(width,w);Assert.Equal(height,h);
        }
        [Theory]
        [InlineData("841,594")][InlineData("0x20")][InlineData("NaNx20")][InlineData("1e3x20")]
        [InlineData("100001x10")][InlineData("1.0001x10")][InlineData("20x30x40")][InlineData("-2x30")]
        public void RejectsAmbiguousOrInvalidDimensions(string text)=>Assert.False(PaperDimensionText.TryParse(text,out _,out _));
    }
}
