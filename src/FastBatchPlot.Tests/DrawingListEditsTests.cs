using System;
using System.Collections.Generic;
using System.Linq;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Naming;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class DrawingListEditsTests
    {
        private static PlotFrame Frame(string no)=>new PlotFrame{TitleInfo=new TitleBlockInfo{DrawingNo=no}};
        [Fact]
        public void PreviewDoesNotMutateAndApplyUndoUseObjectIdentity()
        {
            var a=Frame("旧A");var b=Frame("旧B");
            var plan=DrawingListEdits.PlanNumbers(new[]{b,a},new RenumberOptions{Prefix="电施-",Start=8,Step=2,Digits=3,Suffix="A"});
            Assert.Equal("旧B",b.TitleInfo.DrawingNo);
            Assert.Equal(new[]{"电施-008A","电施-010A"},plan.Select(c=>c.After));
            DrawingListEdits.ApplyNumbers(plan,new[]{a,b});
            Assert.Equal("电施-010A",a.TitleInfo.DrawingNo);
            DrawingListEdits.ApplyNumbers(plan,new[]{b,a},true);
            Assert.Equal("旧A",a.TitleInfo.DrawingNo);Assert.Equal("旧B",b.TitleInfo.DrawingNo);
        }
        [Fact]
        public void StalePlanDoesNotPartiallyApplyAndStaleUndoDoesNotOverwrite()
        {
            var a=Frame("A");var b=Frame("B");var all=new[]{a,b};
            var plan=DrawingListEdits.PlanNumbers(all,new RenumberOptions());
            b.TitleInfo.DrawingNo="edited";
            Assert.Throws<InvalidOperationException>(()=>DrawingListEdits.ApplyNumbers(plan,all));
            Assert.Equal("A",a.TitleInfo.DrawingNo);
            b.TitleInfo.DrawingNo="B";DrawingListEdits.ApplyNumbers(plan,all);
            b.TitleInfo.DrawingNo="manual";
            Assert.Throws<InvalidOperationException>(()=>DrawingListEdits.ApplyNumbers(plan,all,true));
            Assert.Equal("001",a.TitleInfo.DrawingNo);Assert.Equal("manual",b.TitleInfo.DrawingNo);
            Assert.Throws<InvalidOperationException>(()=>DrawingListEdits.ApplyNumbers(plan,new[]{a},true));
        }
        [Theory]
        [InlineData(-1,1,3)]
        [InlineData(1,0,3)]
        [InlineData(1,1,10)]
        public void InvalidNumberingIsRejected(int start,int step,int digits)
        {Assert.Throws<ArgumentException>(()=>DrawingListEdits.PlanNumbers(new[]{Frame("A")},new RenumberOptions{Start=start,Step=step,Digits=digits}));}
        [Fact]
        public void OverflowAndDuplicateObjectsAreRejectedBeforeMutation()
        {
            var a=Frame("A");var b=Frame("B");
            Assert.Throws<OverflowException>(()=>DrawingListEdits.PlanNumbers(new[]{a,b},new RenumberOptions{Start=int.MaxValue,Step=2}));
            Assert.Throws<ArgumentException>(()=>DrawingListEdits.PlanNumbers(new[]{a,a},new RenumberOptions()));
            Assert.Equal("A",a.TitleInfo.DrawingNo);
        }
        [Fact]
        public void MoveMultipleRowsPreservesSelectionOrderAtBoundaries()
        {
            var a=Frame("A");var b=Frame("B");var c=Frame("C");var d=Frame("D");var e=Frame("E");var all=new[]{a,b,c,d,e};
            Assert.Equal(new[]{b,c,a,e,d},DrawingListEdits.Move(all,new[]{b,c,e},-1));
            Assert.Equal(new[]{b,a,e,c,d},DrawingListEdits.Move(all,new[]{a,c,d},1));
            Assert.Equal(all,DrawingListEdits.Move(all,all,-1));
        }
        [Fact]
        public void CustomOrderDoesNotRegroupAcrossLayouts()
        {
            var a=Frame("A");a.SourceLayoutId="A";a.LayoutOrder=1;
            var b=Frame("B");b.SourceLayoutId="B";b.LayoutOrder=2;
            var c=Frame("C");c.SourceLayoutId="A";c.LayoutOrder=1;
            var input=new List<PlotFrame>{b,a,c};var result=FrameSorter.Sort(input,SortOrderRule.Custom);
            Assert.Equal(input,result);Assert.NotSame(input,result);Assert.Equal(new[]{1,2,3},result.Select(f=>f.OrderIndex));
        }
        [Fact]
        public void DiagnosticsFindCaseAndWhitespaceCollisionsWithoutBlankFalsePositives()
        {
            var a=Frame("A-01");a.CustomOutputFileName="输出";
            var b=Frame(" a-01 ");b.CustomOutputFileName="输出.";
            var result=DrawingListDiagnostics.Find(new[]{a,b,Frame(""),Frame("")});
            Assert.Equal(2,result.Count);Assert.All(result,r=>Assert.True(r.DuplicateNumber&&r.DuplicateFileName));
            Assert.Equal(" a-01 ",b.TitleInfo.DrawingNo);
        }
    }
}
