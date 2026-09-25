using System;
using System.IO;
using System.Text.RegularExpressions;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;
using FastBatchPlot.Core.Configuration;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class PageMarginsTests
    {
        private static PlotFrame Frame()=>new PlotFrame{MinX=100,MinY=200,MaxX=42100,MaxY=29900,CalculatedScale=100,IsLandscape=true,DetectedPaper=new PaperSize("A3",420,297)};
        private static PageMargins Margins(MarginMode mode)=>new PageMargins{Mode=mode,Left=20,Right=5,Top=8,Bottom=2};
        [Fact]
        public void IndependentExpansionPreservesScaleAndPositionsFourEdges()
        {
            var p=PlotPlanBuilder.Create(Frame(),new PlotConfig{Margins=Margins(MarginMode.ExpandPaper)});
            Assert.Equal(445,p.PaperWidthMm);Assert.Equal(307,p.PaperHeightMm);Assert.Equal(100,p.ScaleDenominator);
            Assert.Equal(20,p.ContentLeftMm);Assert.Equal(2,p.ContentBottomMm);
            Assert.Equal(5,p.PaperWidthMm-p.ContentLeftMm-p.ContentWidthMm);
            Assert.Equal(8,p.PaperHeightMm-p.ContentBottomMm-p.ContentHeightMm);
        }
        [Fact]
        public void IndependentShrinkKeepsPaperAndCentersInRemainingArea()
        {
            var p=PlotPlanBuilder.Create(Frame(),new PlotConfig{Margins=Margins(MarginMode.ShrinkContent)});
            Assert.Equal(420,p.PaperWidthMm);Assert.Equal(297,p.PaperHeightMm);Assert.Equal(100*420d/395,p.ScaleDenominator,8);
            Assert.Equal(395,p.ContentWidthMm,8);Assert.Equal(20,p.ContentLeftMm,8);
            Assert.Equal((287-p.ContentHeightMm)/2+2,p.ContentBottomMm,8);
        }
        [Fact]
        public void AreaEnoughButPositionWrongIsRejected()
        {
            var p=PlotPlanBuilder.Create(Frame(),new PlotConfig{MarginMm=-10});
            var m=new PlotMedia{WidthMm=420,HeightMm=297,PrintableLeftMm=22,PrintableBottomMm=0,PrintableWidthMm=398,PrintableHeightMm=297};
            Assert.True(m.PrintableWidthMm>p.ContentWidthMm);Assert.False(PlotPlanBuilder.CanPlace(p,m));
            Assert.Throws<InvalidOperationException>(()=>PlotPlanBuilder.SelectMedia(p,new[]{m}));
        }
        [Fact]
        public void RotatedHardMarginsAreMappedIntoFinalPageCoordinates()
        {
            var p=PlotPlanBuilder.Create(Frame(),new PlotConfig{Margins=Margins(MarginMode.ExpandPaper)});
            var m=new PlotMedia{WidthMm=307,HeightMm=445,PrintableLeftMm=4,PrintableBottomMm=10,PrintableWidthMm=302,PrintableHeightMm=432};
            Assert.True(PlotPlanBuilder.CanPlace(p,m));PlotPlanBuilder.Origin(p,m,out var x,out var y);
            Assert.Equal(10,x);Assert.Equal(1,y);
            m.PrintableBottomMm=21;m.PrintableHeightMm=421;Assert.False(PlotPlanBuilder.CanPlace(p,m));
        }
        [Theory]
        [InlineData(-1)] [InlineData(101)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
        public void InvalidEdgesAreRejected(double left)=>Assert.Throws<ArgumentException>(()=>PlotPlanBuilder.Create(Frame(),new PlotConfig{Margins=new PageMargins{Mode=MarginMode.ExpandPaper,Left=left}}));
        [Fact]
        public void InvalidMediaAndNoRemainingPageAreRejected()
        {
            var frame=Frame();frame.DetectedPaper=new PaperSize("small",100,100);
            Assert.Throws<ArgumentException>(()=>PlotPlanBuilder.Create(frame,new PlotConfig{Margins=new PageMargins{Mode=MarginMode.ShrinkContent,Left=60,Right=40}}));
            var p=PlotPlanBuilder.Create(Frame(),new PlotConfig());
            Assert.False(PlotPlanBuilder.CanPlace(p,new PlotMedia{WidthMm=double.NaN,HeightMm=297,PrintableWidthMm=420,PrintableHeightMm=297}));
        }
        [Fact]
        public void MarginPreferencesRoundTripAndOldFilesDefaultToUniform()
        {
            var dir=Path.Combine(Path.GetTempPath(),"margin-check-"+Guid.NewGuid().ToString("N"));var path=Path.Combine(dir,"settings.json");
            try
            {
                UserSettingsStore.Save(path,new BatchPlotPreferences{Margins=Margins(MarginMode.ExpandPaper)});
                var prefs=UserSettingsStore.Load(path);Assert.Equal(20,prefs.Margins.Left);Assert.Equal(8,prefs.Margins.Top);Assert.Equal(MarginMode.ExpandPaper,prefs.Margins.Mode);
                File.WriteAllText(path,Regex.Replace(File.ReadAllText(path),",\\s*\"margins\"\\s*:\\s*\\{[^}]*\\}",""));
                Assert.Equal(MarginMode.Uniform,UserSettingsStore.Load(path).Margins.Mode);
                var bytes=File.ReadAllBytes(path);
                Assert.Throws<InvalidDataException>(()=>UserSettingsStore.Save(path,new BatchPlotPreferences{Margins=new PageMargins{Left=double.NaN}}));Assert.Equal(bytes,File.ReadAllBytes(path));
            }
            finally{if(File.Exists(path))File.Delete(path);if(Directory.Exists(dir))Directory.Delete(dir);}
        }
    }
}

