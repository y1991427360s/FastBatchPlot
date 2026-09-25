using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Export;
using FastBatchPlot.Core.Models;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private sealed class FakeSplitHost : ICadDwgSplitHost
    {
        public int Calls;public int FailAt=int.MaxValue;public bool Write=true;
        public DwgSplitPlan InspectDwgSplit(PlotFrame f)=>new(f,new[]{"1"},0,Array.Empty<string>(),Array.Empty<string>());
        public void ExportDwgSplit(DwgSplitPlan plan,string path)
        {Calls++;if(Calls==FailAt)throw new IOException("离线模拟磁盘失败");if(Write)File.WriteAllText(path,"仅供离线调度测试，不是 DWG");}
    }
    private static void CheckSplitUi()
    {
        using var form=new BatchPlotForm(null);
        var menu=Field<ContextMenuStrip>(form,"ctxMenu");
        Check(menu.Items.Cast<ToolStripItem>().Any(i=>i.Text?.Contains("拆分勾选图纸")==true),"右键菜单提供 DWG 拆分入口");
        Call(form,"ExportSplitDwg");Check(Field<Label>(form,"lblStatus").Text.Contains("不支持"),"离线宿主拒绝调用真实 CAD 拆分");
        var frames=Enumerable.Range(1,3).Select(i=>new PlotFrame{MaxX=100,MaxY=80}).ToArray();
        var host=new FakeSplitHost{FailAt=2};var plans=frames.Select(host.InspectDwgSplit).ToArray();
        string folder=Path.Combine(Path.GetTempPath(),"FastBatchPlotSplitChecks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var paths=Enumerable.Range(1,3).Select(i=>Path.Combine(folder,i+".dwg")).ToArray();
        try
        {
            Call(form,"ExecuteSplitPlans",host,frames,plans,paths);
            Check(host.Calls==2&&File.Exists(paths[0])&&!File.Exists(paths[2]),"拆分失败停止后续页并保留已成功文件");
            Check(frames[0].Status=="DWG 已导出"&&frames[1].Status=="DWG 失败"&&frames[2].Status=="DWG 未执行","拆分结果逐行区分成功失败与未执行");
            var noFile=new FakeSplitHost{Write=false};Call(form,"ExecuteSplitPlans",noFile,new[]{frames[2]},new[]{plans[2]},new[]{paths[2]});
            Check(frames[2].Status=="DWG 失败","宿主未输出文件时不报告成功");
            var bad=new DwgSplitPlan(frames[0],Array.Empty<string>(),0,Array.Empty<string>(),new[]{"布局未支持"});
            using var preview=new DwgSplitPreviewForm(new[]{plans[0],bad},new[]{paths[0],paths[1]});
            preview.ShowInTaskbar=false;preview.StartPosition=FormStartPosition.Manual;preview.Location=new System.Drawing.Point(-32000,-32000);preview.Show();
            var root=(TableLayoutPanel)preview.Controls[0];var bar=(FlowLayoutPanel)root.GetControlFromPosition(0,2)!;
            Check(!bar.Controls.OfType<Button>().Single(b=>b.DialogResult==DialogResult.OK).Enabled,"存在预检错误时禁止确认拆分");
            string evidence=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","..","docs","audit-evidence","ui-dwg-split-window.png"));
            using var image=new System.Drawing.Bitmap(preview.Width,preview.Height);preview.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,image.Width,image.Height));image.Save(evidence);
        }
        finally{foreach(var file in paths)if(File.Exists(file))File.Delete(file);Directory.Delete(folder);}
    }
}

