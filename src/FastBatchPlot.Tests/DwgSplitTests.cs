using System;
using FastBatchPlot.Core.Export;
using FastBatchPlot.Core.Models;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class DwgSplitTests
    {
        private static PlotFrame Frame()=>new PlotFrame{MinX=0,MinY=0,MaxX=100,MaxY=80,SourceDocumentId="doc",SourceLayoutId="1",LayoutName="Model"};
        [Theory]
        [InlineData(10,10,20,20,SplitRelation.Inside)]
        [InlineData(-1,10,20,20,SplitRelation.Crossing)]
        [InlineData(-100,-100,200,200,SplitRelation.Crossing)]
        [InlineData(100,10,110,20,SplitRelation.Crossing)]
        [InlineData(101,10,110,20,SplitRelation.Outside)]
        [InlineData(10,81,20,90,SplitRelation.Outside)]
        [InlineData(0,0,0,0,SplitRelation.Inside)]
        public void BoundsKeepCrossingAndTouchingEntities(double x1,double y1,double x2,double y2,SplitRelation expected)
            =>Assert.Equal(expected,DwgSplitGeometry.Classify(Frame(),x1,y1,x2,y2));
        [Fact]
        public void InvalidGeometryFailsInsteadOfSkippingEntities()
        {
            Assert.Throws<ArgumentException>(()=>DwgSplitGeometry.Classify(Frame(),double.NaN,0,1,1));
            Assert.Throws<ArgumentException>(()=>DwgSplitGeometry.Classify(Frame(),2,0,1,1));
            var f=Frame();f.MaxY=0;Assert.Throws<ArgumentException>(()=>DwgSplitGeometry.Classify(f,0,0,1,1));
        }
        [Fact]
        public void PlanCopiesSourceAndCollections()
        {
            var f=Frame();var handles=new[]{"B","A"};var warnings=new[]{"原警告"};
            var plan=new DwgSplitPlan(f,handles,1,warnings,Array.Empty<string>());
            f.MaxX=999;handles[0]="C";warnings[0]="变更";plan.Source.MaxX=555;
            Assert.Equal(100,plan.Source.MaxX);Assert.Equal(new[]{"A","B"},plan.Handles);Assert.Equal("原警告",plan.Warnings[0]);
        }
        [Fact]
        public void ChangedMembershipCrossingsAndWarningsInvalidateReview()
        {
            DwgSplitPlan Make(string[] handles,int crossing=0,string[]? warnings=null,string[]? errors=null)
                =>new DwgSplitPlan(Frame(),handles,crossing,warnings??Array.Empty<string>(),errors??Array.Empty<string>());
            var p=Make(new[]{"A","B"});Assert.True(p.Matches(Make(new[]{"B","A"})));
            Assert.False(p.Matches(Make(new[]{"A","C"})));Assert.False(p.Matches(Make(new[]{"A","B"},1)));
            Assert.False(p.Matches(Make(new[]{"A","B"},warnings:new[]{"新依赖"})));
            Assert.False(p.Matches(Make(new[]{"A","B"},errors:new[]{"丢失资源"})));
            Assert.False(Make(Array.Empty<string>()).CanExport);
        }
    }
}
