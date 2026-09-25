using System.Collections.Generic;
using System.Linq;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class DetectionReportTests
    {
        [Fact]
        public void PolylineRejectionsAreCountedOnceWithoutChangingSelection()
        {
            var source = new[] { Poly("kept",841,594), Poly("duplicate",841,594), Poly("tiny",50,50),
                Poly("long",50000,10000), new RawPolylineCandidate { Handle="triangle",Vertices=new List<Point2D>{new Point2D(0,0),new Point2D(500,0),new Point2D(0,500)} } };
            var report = new DetectionReport();
            var frames = PolylineFrameDetector.FilterAndCreateFrames(source,0,report);
            Assert.Equal(PolylineFrameDetector.FilterAndCreateFrames(source).Select(f=>f.HandleOrId), frames.Select(f=>f.HandleOrId));
            Assert.Single(frames);
            Assert.Equal(4,report.TotalCount);
            Assert.Equal(1,report.Counts["重复或嵌套内框"]);
            Assert.Contains("实体=duplicate",report.ToText());
            Assert.Contains("文件=sample.dwg",report.ToText());
        }

        [Fact]
        public void SelectionReportsFilterDuplicateAndAreaWithNoDoubleCounting()
        {
            var first=Frame("first",0,1189,841); var duplicate=Frame("duplicate",0,1189,841);
            var small=Frame("small",2000,297,210); var excluded=Frame("excluded",4000,841,594); excluded.SourceLayer="other";
            var result=FrameSelectionService.Select(new[]{first,duplicate,small,excluded},new FrameSelectionOptions{LayerName="frame",MinimumAreaPercent=10});
            Assert.Single(result.Frames); Assert.Equal(3,result.Report.TotalCount);
            Assert.Equal(1,result.Report.Counts["图层筛选"]);
            Assert.Equal(1,result.Report.Counts["重复或嵌套内框"]);
            Assert.Equal(1,result.Report.Counts["面积阈值过滤"]);
        }

        [Fact]
        public void DetailLimitDoesNotLoseCounts()
        {
            var report=new DetectionReport();
            for(int i=0;i<DetectionReport.DetailLimit+17;i++)report.Add("测试",i.ToString());
            Assert.Equal(DetectionReport.DetailLimit,report.Details.Count);
            Assert.Equal(DetectionReport.DetailLimit+17,report.Counts["测试"]);
            Assert.Contains("另 17 条未展示",report.ToText());
        }

        [Fact]
        public void ExplicitScaleRemovesOnlyAutomaticPaperMismatch()
        {
            var source=new[]{new RawBlockCandidate{Handle="long",Bounds=new Rect2D(0,0,50000,10000)},new RawBlockCandidate{Handle="tiny",Bounds=new Rect2D(0,0,50,50)}};
            var autoReport=new DetectionReport(); var explicitReport=new DetectionReport();
            Assert.Empty(BlockFrameDetector.ProcessBlockCandidates(source,0,autoReport));
            Assert.Single(BlockFrameDetector.ProcessBlockCandidates(source,100,explicitReport));
            Assert.Equal(2,autoReport.TotalCount); Assert.Equal(1,explicitReport.TotalCount);
            Assert.Contains("实体=tiny",explicitReport.ToText());
        }
        private static PlotFrame Frame(string id,double x,double w,double h)=>new PlotFrame{HandleOrId=id,MinX=x,MaxX=x+w,MaxY=h,SourceLayer="frame"};
        private static RawPolylineCandidate Poly(string id,double w,double h)=>new RawPolylineCandidate{Handle=id,SourceFileName="sample.dwg",Vertices=new List<Point2D>{new Point2D(0,0),new Point2D(w,0),new Point2D(w,h),new Point2D(0,h)}};
    }
}
