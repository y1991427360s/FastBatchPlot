using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Tasks
{
    public enum BatchPageState { Pending, Running, Succeeded, Failed, Cancelled, NotSubmitted }
    public sealed class BatchPageResult
    {
        public bool Success {get;}
        public string Error {get;}
        public BatchPageResult(bool success,string error=""){Success=success;Error=error??"";}
    }
    public sealed class BatchPage
    {
        private readonly PlotFrame frame;
        public PlotFrame Frame=>Snapshot(frame);
        public string OutputPath {get;}
        public BatchPageState State {get;internal set;}
        public string Error {get;internal set;}="";
        public BatchPage(PlotFrame source,string path){frame=Snapshot(source);OutputPath=path??throw new ArgumentNullException(nameof(path));}
        private static PlotFrame Snapshot(PlotFrame f)
        {
            var copy=new PlotFrame();
            foreach(var property in typeof(PlotFrame).GetProperties().Where(p=>p.CanWrite))property.SetValue(copy,property.GetValue(f));
            copy.RegistrationStampRegion=f.RegistrationStampRegion==null?null:FastBatchPlot.Core.Templates.TemplateCropGeometry.Copy(f.RegistrationStampRegion);
            copy.StampRegion=f.StampRegion==null?null:FastBatchPlot.Core.Templates.TemplateCropGeometry.Copy(f.StampRegion);
            copy.AppliedPrintRegion=f.AppliedPrintRegion==null?null:FastBatchPlot.Core.Templates.TemplateCropGeometry.Copy(f.AppliedPrintRegion);
            copy.DetectedPaper=f.DetectedPaper.Clone();
            copy.TitleInfo=new TitleBlockInfo();
            foreach(var property in typeof(TitleBlockInfo).GetProperties().Where(p=>p.PropertyType==typeof(string)))property.SetValue(copy.TitleInfo,property.GetValue(f.TitleInfo));
            copy.TitleInfo.RawAttributes=new Dictionary<string,string>(f.TitleInfo.RawAttributes,StringComparer.OrdinalIgnoreCase);
            return copy;
        }
    }
    /// <summary>单线程逐页状态机；宿主线程负责调用 Step，不在后台线程访问 CAD。</summary>
    public sealed class BatchPlotRun
    {
        private readonly PlotConfig config;
        private bool stepping;
        private int next;
        public ReadOnlyCollection<BatchPage> Pages {get;}
        public bool CancellationRequested {get;private set;}
        public bool IsFinished=>next>=Pages.Count;
        public bool CanMerge=>IsFinished&&!CancellationRequested&&Pages.Count>0&&Pages.All(p=>p.State==BatchPageState.Succeeded);
        public PlotConfig Config=>CopyConfig(config);
        public BatchPlotRun(IEnumerable<BatchPage> pages,PlotConfig source)
        {
            var list=pages.Select(p=>new BatchPage(p.Frame,p.OutputPath)).ToList();
            if(list.Count==0)throw new ArgumentException("任务不能为空。");
            Pages=list.AsReadOnly();config=CopyConfig(source);
        }
        private static PlotConfig CopyConfig(PlotConfig source)
        {
            var copy=new PlotConfig();
            foreach(var p in typeof(PlotConfig).GetProperties().Where(p=>p.CanWrite))p.SetValue(copy,p.GetValue(source));
            copy.Stamp=source.Stamp?.Copy();
            copy.RegistrationStamp=source.RegistrationStamp?.Copy();
            copy.Margins=source.Margins.Copy();
            copy.PdfOptions=(source.PdfOptions ?? throw new ArgumentException("PDF 参数不能为空。")).Copy();
            copy.PdfOptions.Validate();
            return copy;
        }
        public void RequestCancel(){CancellationRequested=true;}
        public void Step(Func<BatchPage,PlotConfig,BatchPageResult> execute)
        {
            if(stepping)throw new InvalidOperationException("任务正在执行，不能重入。");
            if(IsFinished)return;
            if(CancellationRequested){FinishPending(BatchPageState.Cancelled,"用户取消，未提交。");return;}
            stepping=true;var page=Pages[next];page.State=BatchPageState.Running;
            try
            {
                BatchPageResult result;
                try{result=execute(page,Config)??throw new InvalidOperationException("宿主未返回结果。");}
                catch(Exception ex){result=new BatchPageResult(false,ex.Message);}
                page.State=result.Success?BatchPageState.Succeeded:BatchPageState.Failed;page.Error=result.Error;next++;
                if(!result.Success&&config.SendToPrinter)FinishPending(BatchPageState.NotSubmitted,"前页设备任务失败，未提交；请检查设备队列。");
                else if(CancellationRequested)FinishPending(BatchPageState.Cancelled,"用户取消，未提交。");
            }
            finally{stepping=false;}
        }
        private void FinishPending(BatchPageState state,string reason)
        {while(next<Pages.Count){Pages[next].State=state;Pages[next].Error=reason;next++;}}
    }
}
