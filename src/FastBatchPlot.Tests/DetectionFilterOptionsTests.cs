using System;
using System.Collections.Generic;
using System.IO;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Configuration;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class DetectionFilterOptionsTests
    {
        private static RawPolylineCandidate Rectangle(string id, double inset = 0) => new RawPolylineCandidate {
            Handle=id, SourceDocumentId="doc", SourceLayoutId="model", Vertices=new List<Point2D> {
                new Point2D(inset,inset),new Point2D(420-inset,inset),
                new Point2D(420-inset,297-inset),new Point2D(inset,297-inset) } };

        [Theory]
        [InlineData(true,true,1)]
        [InlineData(false,true,2)]
        [InlineData(true,false,2)]
        [InlineData(false,false,3)]
        public void IndependentSwitchesPreserveRequestedPolylineCandidates(bool duplicates,bool nested,int count)
        {
            var candidates=PolylineFrameDetector.CreateCandidates(new[]{Rectangle("outer"),Rectangle("duplicate"),Rectangle("inner",10)},1);
            Assert.Equal(3,candidates.Count);
            var result=FrameSelectionService.Select(candidates,new FrameSelectionOptions{RemoveDuplicates=duplicates,RemoveNestedFrames=nested});
            Assert.Equal(count,result.Frames.Count);
            Assert.Equal(3-count,result.DuplicateCount);
        }

        [Fact]
        public void BlockAndPolylineUseSameDuplicateSwitch()
        {
            var candidates=PolylineFrameDetector.CreateCandidates(new[]{Rectangle("poly")},1);
            candidates.Add(new PlotFrame{Type=FrameType.BlockReference,HandleOrId="block",SourceDocumentId="doc",SourceLayoutId="model",MaxX=420,MaxY=297});
            Assert.Single(FrameSelectionService.Select(candidates,new FrameSelectionOptions()).Frames);
            Assert.Equal(2,FrameSelectionService.Select(candidates,new FrameSelectionOptions{RemoveDuplicates=false}).Frames.Count);
        }

        [Fact]
        public void PreferencesRetainDisabledFiltersAndOldFilesDefaultToEnabled()
        {
            string directory=Path.Combine(Path.GetTempPath(),"filter-options-"+Guid.NewGuid().ToString("N"));
            string path=Path.Combine(directory,"settings.json");
            try {
                UserSettingsStore.Save(path,new BatchPlotPreferences{RemoveDuplicates=false,RemoveNestedFrames=false});
                var value=UserSettingsStore.Load(path);Assert.False(value.RemoveDuplicates);Assert.False(value.RemoveNestedFrames);
                string legacy=File.ReadAllText(path).Replace(",\"removeDuplicates\":false","").Replace(",\"removeNestedFrames\":false","");
                Assert.DoesNotContain("removeDuplicates",legacy);Assert.DoesNotContain("removeNestedFrames",legacy);
                File.WriteAllText(path,legacy);
                value=UserSettingsStore.Load(path);Assert.True(value.RemoveDuplicates);Assert.True(value.RemoveNestedFrames);
            } finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }

        [Fact]
        public void NestedSmallTablesAndSubFramesAreFilteredWhenNestedSwitchEnabled()
        {
            // 模拟大图框内包含明细表、标题栏、设备小框等多段线
            var outer = new RawPolylineCandidate {
                Handle="outer", SourceDocumentId="doc", SourceLayoutId="model", Vertices=new List<Point2D> {
                    new Point2D(0,0), new Point2D(1471,0), new Point2D(1471,594), new Point2D(0,594) } };
            var table1 = new RawPolylineCandidate { // 标题明细栏 (200x140) 面积比约 3%
                Handle="table1", SourceDocumentId="doc", SourceLayoutId="model", Vertices=new List<Point2D> {
                    new Point2D(1200,20), new Point2D(1400,20), new Point2D(1400,160), new Point2D(1200,160) } };
            var table2 = new RawPolylineCandidate { // 元器件表 (297x210) 面积比约 7%
                Handle="table2", SourceDocumentId="doc", SourceLayoutId="model", Vertices=new List<Point2D> {
                    new Point2D(50,50), new Point2D(347,50), new Point2D(347,260), new Point2D(50,260) } };
            var table3 = new RawPolylineCandidate { // 竖向开关柜间隔 (420x500) 面积比约 24%
                Handle="table3", SourceDocumentId="doc", SourceLayoutId="model", Vertices=new List<Point2D> {
                    new Point2D(400,20), new Point2D(820,20), new Point2D(820,520), new Point2D(400,520) } };

            var candidates = PolylineFrameDetector.CreateCandidates(new[] { outer, table1, table2, table3 }, 1);
            Assert.Equal(4, candidates.Count);

            // 开启过滤内含框：必须将大图框内的所有表格和元器件框彻底过滤，仅保留 1 个大外框
            var filtered = FrameSelectionService.Select(candidates, new FrameSelectionOptions { RemoveNestedFrames = true });
            Assert.Single(filtered.Frames);
            Assert.Equal("outer", filtered.Frames[0].HandleOrId);

            // 关闭过滤内含框：保留全部 4 个图框
            var preserved = FrameSelectionService.Select(candidates, new FrameSelectionOptions { RemoveNestedFrames = false });
            Assert.Equal(4, preserved.Frames.Count);
        }

        [Fact]
        public void DraftingToleranceAllowsSubMillimeterImperfectionInPlotPlanBuilder()
        {
            // CAD 中绘图可能产生 0.2mm 微小非整毫米误差 (如 743.2 x 420.1mm)
            var frame = new PlotFrame {
                MinX = 100, MinY = 100, MaxX = 843.2, MaxY = 520.1,
                CalculatedScale = 1.0, IsLandscape = true,
                DetectedPaper = new PaperSize("A2+1/4", 743, 420, true)
            };

            var plan = FastBatchPlot.Core.Planning.PlotPlanBuilder.Create(frame, new PlotConfig());
            Assert.NotNull(plan);
            Assert.Equal(743, plan.PaperWidthMm);
            Assert.Equal(420, plan.PaperHeightMm);
            Assert.True(plan.ContentLeftMm >= 0);
            Assert.True(plan.ContentBottomMm >= 0);
        }

        [Fact]
        public void A2HalfWith1mmDraftingImperfectionFitsInPlotPlanBuilder()
        {
            // CAD 实际绘图 891 x 421mm（高度比标准 A2 的 420mm 多 1mm）
            var frame = new PlotFrame {
                MinX = 0, MinY = 0, MaxX = 891, MaxY = 421,
                CalculatedScale = 1.0, IsLandscape = true,
                DetectedPaper = new PaperSize("A2+1/2", 891, 420, true)
            };

            var plan = FastBatchPlot.Core.Planning.PlotPlanBuilder.Create(frame, new PlotConfig());
            Assert.NotNull(plan);
            Assert.Equal(891, plan.PaperWidthMm);
            Assert.Equal(420, plan.PaperHeightMm);
            Assert.True(plan.ContentHeightMm <= 420.0 + 1e-6);
            Assert.True(plan.ContentWidthMm <= 891.0 + 1e-6);
            Assert.True(plan.ContentLeftMm >= 0);
            Assert.True(plan.ContentBottomMm >= 0);
        }
    }
}
