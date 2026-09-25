using System;
using System.Linq;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Tasks;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class BatchPlotRunTests
    {
        private static BatchPlotRun Run(bool printer=false)=>new BatchPlotRun(Enumerable.Range(1,3).Select(i=>new BatchPage(new PlotFrame{OrderIndex=i},i+".pdf")),new PlotConfig{SendToPrinter=printer});
        [Fact] public void CancellationBeforeFirstPageSubmitsNothing()
        {
            var run=Run();run.RequestCancel();int calls=0;run.Step((p,c)=>{calls++;return new BatchPageResult(true);});
            Assert.Equal(0,calls);Assert.True(run.IsFinished);Assert.All(run.Pages,p=>Assert.Equal(BatchPageState.Cancelled,p.State));Assert.False(run.CanMerge);
        }
        [Fact] public void CancellationDuringCurrentPageKeepsSuccessAndCancelsRemainder()
        {
            var run=Run();run.Step((p,c)=>{run.RequestCancel();return new BatchPageResult(true);});
            Assert.Equal(BatchPageState.Succeeded,run.Pages[0].State);Assert.Equal(BatchPageState.Cancelled,run.Pages[1].State);Assert.False(run.CanMerge);
        }
        [Fact] public void FileFailureContinuesButCannotProduceCompleteMerge()
        {
            var run=Run();run.Step((p,c)=>throw new InvalidOperationException("驱动失败"));while(!run.IsFinished)run.Step((p,c)=>new BatchPageResult(true));
            Assert.Equal("驱动失败",run.Pages[0].Error);Assert.Equal(2,run.Pages.Count(p=>p.State==BatchPageState.Succeeded));Assert.False(run.CanMerge);
        }
        [Fact] public void PrinterFailureStopsWithoutRetry()
        {
            var run=Run(true);int calls=0;run.Step((p,c)=>{calls++;return new BatchPageResult(false,"已部分提交");});run.Step((p,c)=>{calls++;return new BatchPageResult(true);});
            Assert.Equal(1,calls);Assert.Equal(BatchPageState.NotSubmitted,run.Pages[1].State);Assert.True(run.IsFinished);
        }
        [Fact] public void FullSuccessAllowsMergeUntilCancellationRequested()
        {var run=Run();while(!run.IsFinished)run.Step((p,c)=>new BatchPageResult(true));Assert.True(run.CanMerge);run.RequestCancel();Assert.False(run.CanMerge);}
        [Fact] public void ReentrantStepIsRejectedBeforeSecondSubmission()
        {
            var run=Run();run.Step((p,c)=>{Assert.Throws<InvalidOperationException>(()=>run.Step((x,y)=>new BatchPageResult(true)));return new BatchPageResult(true);});
            Assert.Equal(BatchPageState.Pending,run.Pages[1].State);
        }
        [Fact] public void FramePaperTitleAndConfigRemainIndependentOfLiveUi()
        {
            var f=new PlotFrame{CalculatedScale=100};f.TitleInfo.DrawingNo="A";var c=new PlotConfig{Copies=3};c.Margins.Left=7;
            var page=new BatchPage(f,"a.pdf");var run=new BatchPlotRun(new[]{page},c);
            f.TitleInfo.DrawingNo="B";f.DetectedPaper.WidthMm=1;c.Copies=9;c.Margins.Left=99;
            run.Pages[0].Frame.TitleInfo.DrawingNo="C";run.Config.Copies=15;
            run.Step((p,config)=>{Assert.Equal("A",p.Frame.TitleInfo.DrawingNo);Assert.Equal(841,p.Frame.DetectedPaper.WidthMm);Assert.Equal(3,config.Copies);Assert.Equal(7,config.Margins.Left);return new BatchPageResult(true);});
            Assert.True(run.CanMerge);
        }
    }
}
