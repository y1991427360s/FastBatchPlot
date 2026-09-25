using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckOutputUi()
    {
        using var form = new BatchPlotForm(null);
        var mode = Field<ComboBox>(form,"cboOutputMode");
        var copies = Field<NumericUpDown>(form,"numCopies");
        mode.SelectedIndex = 3;
        Check(!Field<CheckBox>(form,"chkMergePdf").Enabled && !copies.Enabled, "PNG 输出禁用 PDF 合并及份数");
        mode.SelectedIndex = 7; copies.Value = 3;
        Check(copies.Enabled && !Field<TextBox>(form,"txtOutputFolder").Enabled, "实体打印机启用份数并禁用文件目录");
        Call(form,"SetPlottingState",true); Call(form,"SetPlottingState",false);
        Check(!Field<TextBox>(form,"txtOutputFolder").Enabled && !Field<CheckBox>(form,"chkMergePdf").Enabled && copies.Value==3,
            "打印结束恢复当前模式，不能重新启用不适用的文件选项");
        mode.SelectedIndex = 0;
        Check(Field<CheckBox>(form,"chkMergePdf").Enabled && Field<TextBox>(form,"txtOutputFolder").Enabled && copies.Value==1,
            "返回 PDF 模式恢复文件设置并把份数归一");
        var printer = new OutputPlotter();
        object[] args = { printer,new PlotFrame(),new PlotConfig { SendToPrinter=true,Copies=3 },"", "" };
        bool ok=(bool)Call(null,"DispatchPage",args)!;
        Check(ok && printer.DeviceCalls==1 && printer.FileCalls==0 && printer.Copies==3,"设备任务走独立接口并传递份数，不要求生成文件");
        string temp=Path.Combine(Path.GetTempPath(),"OutputUi-"+Guid.NewGuid().ToString("N")+".png");
        try
        {
            args = new object[] {printer,new PlotFrame(),new PlotConfig {ExportFormat=PlotExportFormat.PNG,OverwriteExisting=false},temp,""};
            ok=(bool)Call(null,"DispatchPage",args)!;
            Check(!ok && Convert.ToString(args[4])!.Contains("未生成"),"文件模式拒绝引擎返回成功但没有新文件");
            File.WriteAllText(temp,"existing");
            try { Call(null,"DispatchPage",args); throw new Exception("覆盖未拒绝"); }
            catch (System.Reflection.TargetInvocationException ex) when(ex.InnerException is IOException)
            { Check(File.ReadAllText(temp)=="existing","分派前拒绝覆盖已有文件"); }
        }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    private sealed class OutputPlotter : ICadPlotter, ICadPrinter
    {
        public int FileCalls,DeviceCalls,Copies;
        public List<string> GetAvailablePlotters()=>new();
        public List<string> GetAvailablePlotStyles()=>new();
        public List<string> GetPaperSizesForPlotter(string device)=>new();
        public bool PlotFrameToFile(PlotFrame frame,PlotConfig config,string path,out string error) {FileCalls++;error="";return true;}
        public bool PrintFrameToDevice(PlotFrame frame,PlotConfig config,out string error) {DeviceCalls++;Copies=config.Copies;error="";return true;}
    }
}
