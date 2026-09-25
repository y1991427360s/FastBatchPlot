using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Pdf;
using FastBatchPlot.Core.Tasks;
using FastBatchPlot.UI.Views;
using PdfSharpCore.Pdf;

internal static partial class Program
{
    private static void CheckSinglePdfUi()
    {
        var oldHost=CadHostProvider.Host;var oldPlotter=CadHostProvider.Plotter;
        var host=new SelectionHost();var plotter=new SinglePdfPlotter();
        CadHostProvider.Host=host;CadHostProvider.Plotter=plotter;
        string root=Path.Combine(Path.GetTempPath(),"single-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            using var form=new BatchPlotForm(Path.Combine(root,"settings.json"));
            form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-32000,-32000);form.ShowInTaskbar=false;form.Show();
            var frames=Field<List<PlotFrame>>(form,"_frames");
            var frame=FastBatchPlot.Core.Detection.ManualFrameFactory.Create(new Rect2D(0,0,841,594),1,"fake-document","fake-model","Model");
            frame.CustomOutputFileName="人工名称";frame.Status="原状态";frame.IsSelected=false;frames.Add(frame);Call(form,"RefreshGrid");
            var grid=Field<DataGridView>(form,"dgvDrawings");grid.ClearSelection();grid.Rows[0].Selected=true;
            Field<CheckBox>(form,"chkMergePdf").Checked=true;
            var task=(System.Threading.Tasks.Task<string?>)Call(form,"ExecuteSinglePdf",0,Path.Combine(root,"single.pdf"))!;
            PumpUntil(task);SinglePdfFile.Validate(task.Result!);
            Check(plotter.Calls==1&&!plotter.LastConfig!.MergeToSinglePdf&&plotter.LastConfig.ExportFormat==PlotExportFormat.PDF,"单张只输出一页且强制 PDF、不合并");
            Check(frames.Count==1&&ReferenceEquals(frames[0],frame)&&frame.Status=="原状态"&&!frame.IsSelected,"单张按选中行输出、不依赖勾选且保留列表状态");
            Check(Field<CheckBox>(form,"chkMergePdf").Checked&&Field<PlotConfig>(form,"_config").MergeToSinglePdf&&form.Visible&&!Field<bool>(form,"_isPlotting"),"单张完成恢复窗口、批量合并设置和任务状态");
            var records=Directory.GetFiles(Path.Combine(root,"tasks"),"*.json").Select(PdfTaskHistory.Load).ToList();
            Check(records.Count==1&&records[0].Pages.Single().State==BatchPageState.Succeeded&&!records[0].Config.MergeToSinglePdf,"单张成果进入任务历史且只记录自己的配置");
            host.ManualFrame=null;
            var cancelled=(System.Threading.Tasks.Task<string?>)Call(form,"ExecuteSinglePdf",1,Path.Combine(root,"cancelled.pdf"))!;PumpUntil(cancelled);
            Check(cancelled.Result==null&&plotter.Calls==1&&!File.Exists(Path.Combine(root,"cancelled.pdf"))&&frames.Count==1,"取消两点范围不输出、不改批量列表");
            host.ManualFrame=frame;
            var grouped=(System.Threading.Tasks.Task<string?>)Call(form,"ExecuteSinglePdf",2,Path.Combine(root,"group.pdf"))!;PumpUntil(grouped);
            Check(host.LastManualMode==ManualFrameSelectionMode.EntityGroup&&plotter.Calls==2&&frames.Count==1,"单张图形集合复用宿主范围接口、不追加批量行");
            var temporary=(System.Threading.Tasks.Task<string?>)Call(form,"ExecuteSinglePdf",0,null!)!;PumpUntil(temporary);
            string temporaryPath=temporary.Result!;
            try {
                Check(temporaryPath.Contains(Path.Combine("FastBatchPlot","SinglePdf"))&&File.Exists(temporaryPath),"默认单张输出进入独立临时目录");
                SinglePdfFile.SaveCopy(temporaryPath,Path.Combine(root,"copy.pdf"));
                Check(File.ReadAllBytes(temporaryPath).SequenceEqual(File.ReadAllBytes(Path.Combine(root,"copy.pdf"))),"另存不重新打印且保持 PDF 字节内容");
            } finally {Directory.Delete(Path.GetDirectoryName(temporaryPath)!,true);}
            plotter.AfterWrite=()=>plotter.Token="changed";
            // 新任务在同一会话内连续输出，不再逐页做全空间复核（中望打印期间的内部事件会造成误判且耗时）；
            // 逐页复核只用于从历史重试、需要与旧页混用的场景。
            var changed=(System.Threading.Tasks.Task<string?>)Call(form,"ExecuteSinglePdf",0,Path.Combine(root,"changed.pdf"))!;
            PumpUntil(changed);
            records=Directory.GetFiles(Path.Combine(root,"tasks"),"*.json").Select(PdfTaskHistory.Load).ToList();
            var changedPage=records.Single(r=>r.Pages[0].OutputPath.EndsWith("changed.pdf")).Pages[0];
            Check(changedPage.State==BatchPageState.Succeeded&&File.Exists(changedPage.OutputPath),"新单张任务不做逐页复核，已校验的 PDF 正常记为成功");
            plotter.AfterWrite=null;
            plotter.Invalid=true;
            var invalid=(System.Threading.Tasks.Task<string?>)Call(form,"ExecuteSinglePdf",0,Path.Combine(root,"bad.pdf"))!;
            try{PumpUntil(invalid);throw new Exception("无效 PDF 不应成功");}catch(Exception) when(invalid.IsFaulted){}
            records=Directory.GetFiles(Path.Combine(root,"tasks"),"*.json").Select(PdfTaskHistory.Load).ToList();
            Check(records.Single(r=>r.Pages[0].OutputPath.EndsWith("bad.pdf")).Pages[0].State==BatchPageState.Failed,"无效 PDF 在记录成功前被拒绝，历史记为失败");
            Check(form.Visible&&!Field<bool>(form,"_isPlotting")&&frame.Status=="原状态","单张失败也恢复界面、保留原行状态");
        }
        finally{CadHostProvider.Host=oldHost;CadHostProvider.Plotter=oldPlotter;Directory.Delete(root,true);}
    }
    private sealed class SinglePdfPlotter:ICadPlotter,ICadPlotResourceHost
    {
        public int Calls;public bool Invalid;public PlotConfig? LastConfig;
        public string Token="resources";public Action? AfterWrite;
        public string GetPlotResourceRevision(PlotConfig config)=>Token;
        public List<string> GetAvailablePlotters()=>new(){"offline-pdf.pc3"};
        public List<string> GetAvailablePlotStyles()=>new(){"monochrome.ctb"};
        public List<string> GetPaperSizesForPlotter(string device)=>new();
        public bool PlotFrameToFile(PlotFrame frame,PlotConfig config,string path,out string error)
        {
            Calls++;LastConfig=config;error="";
            if(Invalid)File.WriteAllText(path,"invalid");
            else RecoveryWritePdf(frame,config,path);
            AfterWrite?.Invoke();
            return true;
        }
    }
}
