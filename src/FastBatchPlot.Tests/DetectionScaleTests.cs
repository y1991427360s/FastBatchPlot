using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Configuration;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;
using FastBatchPlot.Core.Templates;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class DetectionScaleTests
    {
        private static RawBlockCandidate Block()=>new RawBlockCandidate{BlockName="长框",SourceDocumentId="doc",SourceLayoutId="model",SourceFileName="来源.dwg",
            Handle="A",Bounds=new Rect2D(0,0,50000,10000),ScaleX=2,ScaleY=2};
        private static RawPolylineCandidate Poly()=>new RawPolylineCandidate{SourceDocumentId="doc",SourceLayoutId="model",
            Vertices=new List<Point2D>{new Point2D(0,0),new Point2D(50000,0),new Point2D(50000,10000),new Point2D(0,10000)}};
        [Theory]
        [InlineData(100,500,100)][InlineData(2.5,20000,4000)]
        public void ExplicitScaleFindsNonstandardFramesAndUsesExactDimensions(double scale,double width,double height)
        {
            Assert.Empty(BlockFrameDetector.ProcessBlockCandidates(new[]{Block()}));
            Assert.Empty(PolylineFrameDetector.FilterAndCreateFrames(new[]{Poly()}));
            var block=BlockFrameDetector.ProcessBlockCandidates(new[]{Block()},scale).Single();
            var poly=PolylineFrameDetector.FilterAndCreateFrames(new[]{Poly()},scale).Single();
            foreach(var frame in new[]{block,poly})
            {
                Assert.Equal(scale,frame.CalculatedScale);Assert.Equal(width,frame.DetectedPaper.WidthMm);Assert.Equal(height,frame.DetectedPaper.HeightMm);
                Assert.Equal("自定义",frame.DetectedPaper.Name);Assert.Equal(width,PlotPlanBuilder.Create(frame,new PlotConfig()).PaperWidthMm);
            }
            Assert.Equal("来源.dwg",block.SourceFileName);
        }
        [Fact]
        public void ExplicitScaleDoesNotSnapNearStandardDimensions()
        {
            var candidate=Block();candidate.Bounds=new Rect2D(0,0,42005,29700);
            var frame=BlockFrameDetector.ProcessBlockCandidates(new[]{candidate},100).Single();
            Assert.Equal("A3",frame.DetectedPaper.Name);Assert.Equal(420.05,frame.DetectedPaper.WidthMm);
            Assert.Equal(420.05,PlotPlanBuilder.Create(frame,new PlotConfig()).PaperWidthMm);
        }
        [Theory]
        [InlineData(0,100)][InlineData(50,50)]
        public void ExplicitTemplateScaleOverridesGlobalOtherwiseGlobalApplies(double templateScale,double expected)
        {
            var library=new TitleTemplateLibrary{Templates=new List<TitleBlockTemplate>{new TitleBlockTemplate{Name="长框",BlockName="长框",PrintScale=templateScale,
                PrintRegion=new TemplateRegion{X1=0,Y1=0,X2=50000,Y2=10000}}}};
            var frame=TemplateFrameDetector.Detect(new[]{Block()},library,(_,__)=>Block().Bounds,100).Single();
            Assert.Equal(expected,frame.CalculatedScale);Assert.Equal(50000/expected,frame.DetectedPaper.WidthMm);
        }
        [Fact]
        public void TemplateWithoutCropAlsoKeepsItsExplicitScale()
        {
            var library=new TitleTemplateLibrary{Templates=new List<TitleBlockTemplate>{new TitleBlockTemplate{Name="长框",BlockName="长框",PrintScale=50}}};
            var frame=TemplateFrameDetector.Detect(new[]{Block()},library,(_,__)=>throw new Exception("不应解析裁切"),100).Single();
            Assert.Equal(50,frame.CalculatedScale);Assert.Equal(1000,frame.DetectedPaper.WidthMm);
        }
        [Theory]
        [InlineData(-1)][InlineData(double.NaN)][InlineData(double.PositiveInfinity)][InlineData(1000001)]
        public void InvalidScaleCannotSilentlyFallBack(double scale)
        {
            Assert.Throws<ArgumentException>(()=>BlockFrameDetector.ProcessBlockCandidates(new[]{Block()},scale));
            Assert.Throws<ArgumentException>(()=>PolylineFrameDetector.FilterAndCreateFrames(new[]{Poly()},scale));
        }
        [Fact]
        public void PreferencesRetainDetectionScaleAndLegacyUsesAuto()
        {
            string dir=Path.Combine(Path.GetTempPath(),"DetectScale-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try
            {
                string path=Path.Combine(dir,"settings.json");UserSettingsStore.Save(path,new BatchPlotPreferences{DetectionScale=2.5});
                Assert.Equal(2.5,UserSettingsStore.Load(path).DetectionScale);
                var json=JsonNode.Parse(File.ReadAllText(path))!.AsObject();json.Remove("detectionScale");File.WriteAllText(path,json.ToJsonString());
                Assert.Equal(0,UserSettingsStore.Load(path).DetectionScale);
                Assert.Throws<InvalidDataException>(()=>UserSettingsStore.Save(path,new BatchPlotPreferences{DetectionScale=-1}));
            }
            finally{Directory.Delete(dir,true);}
        }
    }
}
