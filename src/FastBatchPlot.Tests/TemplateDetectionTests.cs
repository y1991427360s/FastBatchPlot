using System;
using System.Collections.Generic;
using System.Linq;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;
using Xunit;
namespace FastBatchPlot.Tests
{
    public class TemplateDetectionTests
    {
        private static RawBlockCandidate Candidate()=>new RawBlockCandidate{Handle="A1",BlockName="特殊框",SourceDocumentId="doc",SourceLayoutId="model",Bounds=new Rect2D(0,0,10,10),ScaleX=1,ScaleY=1,Attributes=new Dictionary<string,string>{{"DWG_NO","001"}}};
        private static TitleTemplateLibrary Library(double scale=0)=>new TitleTemplateLibrary{Templates=new List<TitleBlockTemplate>{new TitleBlockTemplate{Name="特殊框",BlockName="特殊框",Priority=4,PrintScale=scale,PrintRegion=new TemplateRegion{X1=-420,Y1=0,X2=0,Y2=297}}}};
        [Fact] public void TinyLocatorBlockIsDetectedFromRegisteredPrintRegion()
        {
            var c=Candidate();Assert.Empty(BlockFrameDetector.ProcessBlockCandidates(new[]{c}));
            var f=TemplateFrameDetector.Detect(new[]{c},Library(),(frame,r)=>new Rect2D(0,0,420,297)).Single();
            Assert.Equal("A3",f.DetectedPaper.Name);Assert.Equal(1,f.CalculatedScale);Assert.Equal(10,f.MaxX);Assert.Equal(420,f.PrintBounds!.Value.MaxX);Assert.Equal("001",f.TitleInfo.DrawingNo);Assert.Equal(4,f.TemplatePriority);
        }
        [Fact] public void ExplicitFractionalScaleSupportsCustomPaperWithoutGuessing()
        {
            var f=TemplateFrameDetector.Detect(new[]{Candidate()},Library(2.5),(frame,r)=>new Rect2D(0,0,250,250)).Single();
            Assert.Equal(2.5,f.CalculatedScale);Assert.Equal(100,f.DetectedPaper.WidthMm);Assert.Equal("自定义",f.DetectedPaper.Name);
            Assert.Throws<InvalidOperationException>(()=>TemplateFrameDetector.Detect(new[]{Candidate()},Library(),(frame,r)=>new Rect2D(0,0,250,250)));
        }
        [Fact] public void ResolverFailureNeverReturnsPartialTemplateSet()
        {
            var a=Candidate();var b=Candidate();b.Handle="B";
            var ex=Assert.Throws<InvalidOperationException>(()=>TemplateFrameDetector.Detect(new[]{a,b},Library(),(frame,r)=>frame.HandleOrId=="B"?throw new InvalidOperationException("布局失效"):new Rect2D(0,0,420,297)));
            Assert.Contains("B",ex.Message);Assert.Contains("布局失效",ex.Message);
        }
        [Fact] public void TemplateWithoutCropStillGetsPriorityButUsesGenericGeometry()
        {
            var library=Library();library.Templates[0].PrintRegion=null;var c=Candidate();c.Bounds=new Rect2D(0,0,420,297);
            var f=TemplateFrameDetector.Detect(new[]{c},library,(frame,r)=>throw new Exception("不应调用")).Single();
            Assert.Equal(4,f.TemplatePriority);Assert.Null(f.PrintBounds);
        }
        [Fact] public void DedupeUsesPrintAreaAndKeepsTemplateOverGenericBorder()
        {
            var f=TemplateFrameDetector.Detect(new[]{Candidate()},Library(),(frame,r)=>new Rect2D(0,0,420,297)).Single();
            var generic=new PlotFrame{SourceDocumentId="doc",SourceLayoutId="model",MinX=0,MinY=0,MaxX=421,MaxY=297,Type=FrameType.ClosedPolyline};
            var result=FrameSelectionService.Select(new[]{generic,f},new FrameSelectionOptions{MinimumAreaPercent=90});
            Assert.Same(f,Assert.Single(result.Frames));Assert.Equal(1,result.DuplicateCount);
        }
        [Theory] [InlineData(-1)] [InlineData(double.NaN)] [InlineData(1000001)]
        public void InvalidTemplateScaleIsRejected(double scale)=>Assert.Throws<System.IO.InvalidDataException>(()=>TitleTemplateService.Validate(Library(scale)));

        [Fact]
        public void TemplateDetection_A3Plus1_And_A2Plus1_Succeeds()
        {
            var lib = new TitleTemplateLibrary
            {
                Templates = new List<TitleBlockTemplate>
                {
                    new TitleBlockTemplate { Name = "A3+1", BlockName = "A-C", PaperWidth = 840, PaperHeight = 297, Priority = 2 },
                    new TitleBlockTemplate { Name = "A2+1", BlockName = "A-C", PaperWidth = 1188, PaperHeight = 420, Priority = 2 }
                }
            };

            var candA3Plus1 = new RawBlockCandidate
            {
                Handle = "H1", BlockName = "A-C", Bounds = new Rect2D(0, 0, 84000, 29700),
                ScaleX = 100, ScaleY = 100, SourceDocumentId = "doc", SourceLayoutId = "model"
            };

            var candA2Plus1 = new RawBlockCandidate
            {
                Handle = "H2", BlockName = "A-C", Bounds = new Rect2D(0, 0, 118800, 42000),
                ScaleX = 100, ScaleY = 100, SourceDocumentId = "doc", SourceLayoutId = "model"
            };

            var frames = TemplateFrameDetector.Detect(new[] { candA3Plus1, candA2Plus1 }, lib, (f, r) => new Rect2D(f.MinX, f.MinY, f.MaxX, f.MaxY));
            Assert.Equal(2, frames.Count);
            Assert.Equal("A3+1", frames[0].DetectedPaper.Name);
            Assert.Equal(100, frames[0].CalculatedScale);
            Assert.Equal("A2+1", frames[1].DetectedPaper.Name);
            Assert.Equal(100, frames[1].CalculatedScale);
        }

        [Fact]
        public void UniversalMode_Detects_Both_Polylines_And_Generic_Blocks()
        {
            // 在通用型模式下，无需预设图框，也能同时识别闭合多段线和任意图块
            var poly = new RawPolylineCandidate
            {
                Handle = "P1", Layer = "0", SourceDocumentId = "doc", SourceLayoutId = "model",
                Vertices = new List<Point2D> { new Point2D(0, 0), new Point2D(84000, 0), new Point2D(84000, 29700), new Point2D(0, 29700) }
            };

            var polyFrames = PolylineFrameDetector.CreateCandidates(new[] { poly });
            Assert.Single(polyFrames);
            Assert.Equal("A3+1", polyFrames[0].DetectedPaper.Name);
            Assert.Equal(100, polyFrames[0].CalculatedScale);

            var blk = new RawBlockCandidate
            {
                Handle = "B1", BlockName = "未登记图块", Layer = "0", Bounds = new Rect2D(0, 0, 118800, 42000),
                ScaleX = 100, ScaleY = 100, SourceDocumentId = "doc", SourceLayoutId = "model"
            };

            var blkFrames = BlockFrameDetector.ProcessBlockCandidates(new[] { blk });
            Assert.Single(blkFrames);
            Assert.Equal("A2+1", blkFrames[0].DetectedPaper.Name);
            Assert.Equal(100, blkFrames[0].CalculatedScale);
        }
    }
}
