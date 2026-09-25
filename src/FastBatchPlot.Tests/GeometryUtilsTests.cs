using System.Collections.Generic;
using FastBatchPlot.Core.Common;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class GeometryUtilsTests
    {
        [Fact]
        public void Rectangle_ShouldRejectBowTieWithEqualOppositeSidesAndDiagonals()
        {
            Assert.False(GeometryUtils.IsOrthogonalRectangle(new List<Point2D>
            {
                new Point2D(0, 0), new Point2D(420, 0),
                new Point2D(0, 297), new Point2D(420, 297)
            }));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Rectangle_ShouldAcceptEitherVertexWinding(bool reverse)
        {
            var vertices = new List<Point2D>
            {
                new Point2D(10, 20), new Point2D(430, 20),
                new Point2D(430, 317), new Point2D(10, 317)
            };
            if (reverse) vertices.Reverse();
            Assert.True(GeometryUtils.IsOrthogonalRectangle(vertices));
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void Rectangle_ShouldRejectNonFiniteCoordinates(double coordinate)
        {
            Assert.False(GeometryUtils.IsOrthogonalRectangle(new List<Point2D>
            {
                new Point2D(0, 0), new Point2D(coordinate, 0),
                new Point2D(420, 297), new Point2D(0, 297)
            }));
        }

        [Fact]
        public void Rectangle_ShouldRejectRepeatedVertex()
        {
            Assert.False(GeometryUtils.IsOrthogonalRectangle(new List<Point2D>
            {
                new Point2D(0, 0), new Point2D(420, 0),
                new Point2D(420, 0), new Point2D(0, 297)
            }));
        }
    }
}
