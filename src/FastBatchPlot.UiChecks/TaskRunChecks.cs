using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Tasks;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void PumpUntil(System.Threading.Tasks.Task task)
    {
        var limit=DateTime.UtcNow.AddSeconds(10);
        while(!task.IsCompleted&&DateTime.UtcNow<limit){Application.DoEvents();System.Threading.Thread.Sleep(5);}
        if(!task.IsCompleted)throw new Exception("离线任务超时");task.GetAwaiter().GetResult();
    }
    private sealed class TaskPrinter : ICadPlotter,ICadPrinter
    {
        public int Calls;public List<int> Threads=new();public Action? Submitted;
        public List<string> GetAvailablePlotters()=>new();public List<string> GetAvailablePlotStyles()=>new();public List<string> GetPaperSizesForPlotter(string d)=>new();
        public bool PlotFrameToFile(PlotFrame f,PlotConfig c,string p,out string error){error="不应走文件接口";return false;}
        public bool PrintFrameToDevice(PlotFrame f,PlotConfig c,out string error){Calls++;Threads.Add(Environment.CurrentManagedThreadId);Submitted?.Invoke();error="";return true;}
    }
    private static void CheckTaskRunUi()
    {
        var oldHost=CadHostProvider.Host;var oldPlotter=CadHostProvider.Plotter;
        var host=new LayoutHost();var printer=new TaskPrinter();CadHostProvider.Host=host;CadHostProvider.Plotter=printer;
        try
        {
            using var form=new BatchPlotForm(null);form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-32000,-32000);form.Show();
            var frames=Enumerable.Range(1,3).Select(i=>new PlotFrame{OrderIndex=i,SourceDocumentId="layout-document",SourceLayoutId="space-1"}).ToArray();
            Field<List<PlotFrame>>(form,"_frames").AddRange(frames);Call(form,"RefreshGrid");Call(form,"SetPlottingState",true);
            var run=new BatchPlotRun(frames.Select(f=>new BatchPage(f,"")),new PlotConfig{SendToPrinter=true});
            int thread=Environment.CurrentManagedThreadId;
            printer.Submitted=()=>form.BeginInvoke(new Action(()=>Call(form,"RequestPlotCancellation")));
            var task=(System.Threading.Tasks.Task)Call(form,"RunPlotPages",run,frames,host,printer)!;PumpUntil(task);
            Check(printer.Calls==1&&run.Pages[1].State==BatchPageState.Cancelled,"消息循环在页间接收取消，不提交下一页");
            Check(printer.Threads.All(t=>t==thread),"逐页 CAD 分派始终在原 UI 线程执行");
            Check(frames[0].Status=="已提交设备"&&frames[1].Status.Contains("已取消"),"取消后准确区分已提交和未提交行");
            Check(!Field<Button>(form,"btnCancelPlot").Enabled&&!run.CanMerge,"结束后关闭取消按钮且取消批次禁止合并");
            printer.Submitted=null;var run2=new BatchPlotRun(frames.Select(f=>new BatchPage(f,"")),new PlotConfig{SendToPrinter=true});
            host.Accessible=false;int before=printer.Calls;PumpUntil((System.Threading.Tasks.Task)Call(form,"RunPlotPages",run2,frames,host,printer)!);
            Check(printer.Calls==before&&run2.Pages[0].State==BatchPageState.Failed&&run2.Pages[1].State==BatchPageState.NotSubmitted,"页间文档上下文失效时停止设备提交");
            Call(form,"SetPlottingState",false);form.Size=form.MinimumSize;form.PerformLayout();var cancel=Field<Button>(form,"btnCancelPlot");
            Check(cancel.Parent!.ClientRectangle.Contains(cancel.Bounds)&&cancel.Visible,"最小窗口取消任务按钮可见且不被裁剪");
        }
        finally{CadHostProvider.Host=oldHost;CadHostProvider.Plotter=oldPlotter;}
    }
}
