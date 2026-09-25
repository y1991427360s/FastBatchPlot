using FastBatchPlot.Core.Models;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckMarginsUi()
    {
        var frame=new PlotFrame{OrderIndex=1,MaxX=42000,MaxY=29700,CalculatedScale=100,IsLandscape=true,DetectedPaper=new PaperSize("A3",420,297)};
        using var dialog=new PageMarginsForm(new PageMargins{Mode=MarginMode.ExpandPaper,Left=20,Right=5,Top=8,Bottom=2},0,new[]{frame});
        Check(dialog.Value.Left==20&&dialog.Value.Right==5,"四边留白窗口保留独立边值");
        var preview=Field<DataGridView>(dialog,"preview");
        Check(preview.Rows.Count==1&&preview.Rows[0].Cells[1].Value!.ToString()!.Contains("445"),"四边留白预览显示扩展后纸张尺寸");
        Field<ComboBox>(dialog,"mode").SelectedIndex=(int)MarginMode.ShrinkContent;
        Check(preview.Rows[0].Cells[2].Value!.ToString()!="1:100","固定纸张模式预览显示实际缩小比例");
        Field<ComboBox>(dialog,"mode").SelectedIndex=(int)MarginMode.Uniform;
        Check(Field<NumericUpDown[]>(dialog,"sides").All(n=>!n.Enabled),"统一留白模式禁用四边数值避免混淆");
        Field<CheckBox>(dialog,"outerBorder").Checked=false;
        Field<NumericUpDown>(dialog,"borderInset").Value=0.6m;
        Check(!dialog.PrintOuterBorderLine && dialog.OuterBorderInsetMm==0.6 && preview.Rows[0].Cells[5].Value!.ToString()!.Contains("0.6"),"不留白时预览显示裁边宽度");
        Check(preview.Rows[0].Cells[2].Value!.ToString()=="1:100","裁边不改变打印比例");
        Field<ComboBox>(dialog,"mode").SelectedIndex=(int)MarginMode.ExpandPaper;
        dialog.ShowInTaskbar=false;dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new System.Drawing.Point(-32000,-32000);dialog.Show();
        string file=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","..","docs","audit-evidence","ui-page-margins.png"));
        using(var image=new System.Drawing.Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,image.Width,image.Height));image.Save(file);}
        var dir=Path.Combine(Path.GetTempPath(),"MarginUi-"+Guid.NewGuid().ToString("N"));var path=Path.Combine(dir,"prefs.json");
        try
        {
            using(var first=new BatchPlotForm(path))
            {
                var margins=Field<PageMargins>(first,"_pageMargins");margins.Mode=MarginMode.ShrinkContent;margins.Left=12.5;margins.Top=3;
                var config=Field<PlotConfig>(first,"_config");config.PrintOuterBorderLine=false;config.OuterBorderInsetMm=0.6;
                Field<NumericUpDown>(first,"numDetectionScale").Value=2.5m;
                Call(first,"UpdateMarginMode");Check(!Field<NumericUpDown>(first,"numMargin").Enabled,"独立留白模式禁用主窗口统一数值");Call(first,"SavePreferences");
            }
            using(var second=new BatchPlotForm(path))
            {
                Check(Field<PageMargins>(second,"_pageMargins").Left==12.5&&!Field<NumericUpDown>(second,"numMargin").Enabled,"重新打开主界面恢复四边留白与模式");
                Check(!Field<PlotConfig>(second,"_config").PrintOuterBorderLine && Field<PlotConfig>(second,"_config").OuterBorderInsetMm==0.6,"重新打开主界面恢复外边线参数");
                Check(Field<NumericUpDown>(second,"numDetectionScale").Value==2.5m,"重新打开主界面恢复识别比例");
                Call(second,"SetPlottingState",true);Check(!Field<Button>(second,"btnPageMargins").Enabled,"任务执行时禁止修改四边留白");Call(second,"SetPlottingState",false);
                Check(!Field<NumericUpDown>(second,"numMargin").Enabled&&Field<Button>(second,"btnPageMargins").Enabled,"任务结束恢复当前留白模式控件状态");
            }
        }
        finally{if(File.Exists(path))File.Delete(path);if(Directory.Exists(dir))Directory.Delete(dir);}
    }
}
