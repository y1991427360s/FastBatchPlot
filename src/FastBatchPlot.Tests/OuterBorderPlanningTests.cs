using System;
using System.IO;
using System.Text.Json.Nodes;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;
using FastBatchPlot.Core.Configuration;
using FastBatchPlot.Core.Tasks;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class OuterBorderPlanningTests
    {
        private static PlotFrame Frame()=>new PlotFrame{MinX=100,MinY=200,MaxX=42100,MaxY=29900,CalculatedScale=100,
            IsLandscape=true,DetectedPaper=new PaperSize("A3",420,297),SourceDocumentId="doc",SourceLayoutId="model"};

        [Theory]
        [InlineData(0.01)][InlineData(0.25)][InlineData(1.5)]
        public void BorderCropPreservesPaperScaleAndInteriorCoordinates(double inset)
        {
            var frame=Frame();var before=PlotPlanBuilder.Create(frame,new PlotConfig());
            var after=PlotPlanBuilder.Create(frame,new PlotConfig{PrintOuterBorderLine=false,OuterBorderInsetMm=inset});
            Assert.Equal(before.PaperWidthMm,after.PaperWidthMm);Assert.Equal(before.PaperHeightMm,after.PaperHeightMm);
            Assert.Equal(before.ScaleDenominator,after.ScaleDenominator);Assert.Equal(inset,after.AppliedBorderInsetMm);
            Assert.Equal(before.MinX+inset*100,after.MinX,8);Assert.Equal(before.MaxX-inset*100,after.MaxX,8);
            Assert.Equal(before.ContentWidthMm-inset*2,after.ContentWidthMm,8);
            // 同一个内部点在纸面的位置必须不变，不能仅验证窗口尺寸。
            foreach(double x in new[]{1000.0,12000,40000})
                Assert.Equal(before.ContentLeftMm+(x-before.MinX)/before.ScaleDenominator,
                    after.ContentLeftMm+(x-after.MinX)/after.ScaleDenominator,8);
            foreach(double y in new[]{1000.0,12000,29000})
                Assert.Equal(before.ContentBottomMm+(y-before.MinY)/before.ScaleDenominator,
                    after.ContentBottomMm+(y-after.MinY)/after.ScaleDenominator,8);
            Assert.Equal(100,frame.MinX);Assert.Equal(42100,frame.MaxX);
        }

        [Theory]
        [InlineData(2)][InlineData(-2)]
        public void NonzeroUniformMarginDoesNotCrop(double margin)
        {
            var plan=PlotPlanBuilder.Create(Frame(),new PlotConfig{PrintOuterBorderLine=false,MarginMm=margin});
            Assert.Equal(0,plan.AppliedBorderInsetMm);Assert.Equal(100,plan.MinX);
        }

        [Fact]
        public void AnyIndependentMarginDisablesCropAndPrintBoundsAreHonored()
        {
            var frame=Frame();frame.PrintBounds=new Core.Common.Rect2D{MinX=1000,MinY=1000,MaxX=41000,MaxY=29000};
            var cropped=PlotPlanBuilder.Create(frame,new PlotConfig{PrintOuterBorderLine=false});
            Assert.Equal(1025,cropped.MinX);Assert.Equal(40975,cropped.MaxX);
            var margins=new PageMargins{Mode=MarginMode.ExpandPaper,Left=0.1};
            var withMargin=PlotPlanBuilder.Create(frame,new PlotConfig{PrintOuterBorderLine=false,Margins=margins});
            Assert.Equal(1000,withMargin.MinX);Assert.Equal(0,withMargin.AppliedBorderInsetMm);
        }

        [Theory]
        [InlineData(0)][InlineData(-1)][InlineData(5.01)][InlineData(double.NaN)][InlineData(double.PositiveInfinity)]
        public void InvalidCropWidthIsRejected(double width)=>Assert.Throws<ArgumentException>(()=>
            PlotPlanBuilder.Create(Frame(),new PlotConfig{PrintOuterBorderLine=false,OuterBorderInsetMm=width}));

        [Fact]
        public void CropCannotRemoveEntireWindow()
        {
            var frame=Frame();frame.MaxX=110;frame.MaxY=210;
            Assert.Throws<ArgumentException>(()=>PlotPlanBuilder.Create(frame,new PlotConfig{PrintOuterBorderLine=false}));
        }

        [Fact]
        public void PreferencesAndHistoryKeepCropAndLegacyDefaults()
        {
            string dir=Path.Combine(Path.GetTempPath(),"Border-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try
            {
                string path=Path.Combine(dir,"prefs.json");UserSettingsStore.Save(path,new BatchPlotPreferences{PrintOuterBorderLine=false,OuterBorderInsetMm=0.6});
                var loaded=UserSettingsStore.Load(path);Assert.False(loaded.PrintOuterBorderLine);Assert.Equal(0.6,loaded.OuterBorderInsetMm);
                var json=JsonNode.Parse(File.ReadAllText(path))!.AsObject();json.Remove("printOuterBorderLine");json.Remove("outerBorderInsetMm");File.WriteAllText(path,json.ToJsonString());
                loaded=UserSettingsStore.Load(path);Assert.True(loaded.PrintOuterBorderLine);Assert.Equal(0.25,loaded.OuterBorderInsetMm);
                var config=new PlotConfig{OutputDirectory=dir,MergeToSinglePdf=false,PrintOuterBorderLine=false,OuterBorderInsetMm=0.6};
                var run=new BatchPlotRun(new[]{new BatchPage(Frame(),Path.Combine(dir,"page.pdf"))},config);config.OuterBorderInsetMm=2;
                Assert.Equal(0.6,run.Config.OuterBorderInsetMm);
                path=Path.Combine(dir,"history.json");PdfTaskHistory.Save(path,PdfTaskHistory.Create(run,""));
                var restored=PdfTaskHistory.Load(path).Config;Assert.False(restored.PrintOuterBorderLine);Assert.Equal(0.6,restored.OuterBorderInsetMm);
                json=JsonNode.Parse(File.ReadAllText(path))!.AsObject();json["Config"]!.AsObject().Remove("OuterBorderInset");File.WriteAllText(path,json.ToJsonString());
                Assert.Equal(0.25,PdfTaskHistory.Load(path).Config.OuterBorderInsetMm);
            }
            finally{Directory.Delete(dir,true);}
        }
    }
}
