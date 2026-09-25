using FastBatchPlot.Core.Models;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckPdfParameters()
    {
        var initial = new PdfOutputOptions { VectorResolutionDpi = 1200, RasterResolutionDpi = 300, TextToGeometry = true, IncludeLayers = false };
        using var dialog = new PdfParametersForm(initial, "AutoCAD");
        dialog.ShowInTaskbar = false; dialog.StartPosition = FormStartPosition.Manual;
        dialog.Location = new System.Drawing.Point(-32000, -32000); dialog.Show();
        Check(dialog.Value.VectorResolutionDpi == 1200 && dialog.Value.RasterResolutionDpi == 300 && dialog.Value.TextToGeometry == true && dialog.Value.IncludeLayers == false,
            "PDF 参数面板恢复分辨率和三态开关");
        Field<ComboBox>(dialog, "vector").SelectedIndex = 1;
        Check(initial.VectorResolutionDpi == 1200 && dialog.Value.VectorResolutionDpi == 300, "PDF 参数编辑使用副本，取消不污染原参数");
        Field<ComboBox>(dialog, "layers").SelectedIndex = 0;
        Check(dialog.Value.IncludeLayers == null, "PDF 图层可恢复跟随驱动");
        Check(Field<ComboBox>(dialog, "raster").Enabled && Field<ComboBox>(dialog, "mergeLines").Enabled, "AutoCAD 支持光栅及直线合并");
        Field<ComboBox>(dialog,"mergeLines").SelectedIndex=1;
        Check(dialog.Value.MergeLines==true && Field<Button>(dialog,"apply").Enabled,"AutoCAD 可应用直线合并");
        using var zw = new PdfParametersForm(new PdfOutputOptions(), "ZWCAD");
        Field<ComboBox>(zw,"mergeLines").SelectedIndex=1;
        Check(Field<ComboBox>(zw,"mergeLines").Enabled && zw.Value.MergeLines==true && Field<Button>(zw,"apply").Enabled,"中望可开启直线合并");
        Check(Field<ComboBox>(zw, "vector").Enabled && Field<ComboBox>(zw, "textGeometry").Enabled && Field<ComboBox>(zw, "raster").Enabled && Field<ComboBox>(zw, "layers").Enabled,
            "中望参数面板支持独立光栅和图层覆盖");
        using var restored = new PdfParametersForm(initial, "ZWCAD");
        Check(Field<Button>(restored,"apply").Enabled && restored.Value.IncludeLayers==false && restored.Value.RasterResolutionDpi==300,"中望恢复已有分辨率和图层参数");
        Field<ComboBox>(restored,"vector").SelectedIndex=1;
        Field<ComboBox>(restored,"raster").SelectedIndex=4;
        Check(!Field<Button>(restored,"apply").Enabled && Field<Label>(restored,"status").Text.Contains("不能高于"),"中望阻止光栅超过矢量");
        Field<ComboBox>(restored,"vector").SelectedIndex=3;
        Check(Field<Button>(restored,"apply").Enabled,"修正分辨率组合后可以应用");
        using var incompatible = new PdfParametersForm(new PdfOutputOptions{MergeLines=true}, "");
        Check(!Field<Button>(incompatible, "apply").Enabled, "跨宿主加载不支持参数时阻止无声丢失");
        using var unknown = new PdfParametersForm(new PdfOutputOptions(), "");
        Check(!Field<ComboBox>(unknown, "vector").Enabled && Field<Button>(unknown, "apply").Enabled, "未知宿主仅允许跟随驱动");
        using var main = new BatchPlotForm(null);
        main.ShowInTaskbar = false; main.StartPosition = FormStartPosition.Manual;
        main.Location = new System.Drawing.Point(-32000, -32000); main.Size = main.MinimumSize; main.Show(); main.PerformLayout();
        var parameterButton = Field<Button>(main, "btnPdfParameters");
        Check(parameterButton.Enabled && parameterButton.Width >= 80 && parameterButton.Parent!.ClientRectangle.Contains(parameterButton.Bounds), "主窗最小尺寸下 PDF 参数按钮完整可见");
        Field<ComboBox>(main, "cboOutputMode").SelectedIndex = 3;
        Check(!parameterButton.Enabled, "非 PDF 输出禁用 PDF 参数按钮");
        Field<ComboBox>(main, "cboOutputMode").SelectedIndex = 0;
        Call(main, "SetPlottingState", true); Check(!parameterButton.Enabled, "任务运行时不能更改 PDF 参数");
        Call(main, "SetPlottingState", false); Check(parameterButton.Enabled, "任务结束后恢复 PDF 参数按钮");
        dialog.Size = dialog.MinimumSize; dialog.PerformLayout();
        foreach (string name in new[] { "vector", "raster", "textGeometry", "layers", "mergeLines", "status", "apply" })
        {
            var control = Field<Control>(dialog, name);
            Check(control.Width > 0 && control.Height > 0 && control.Parent!.ClientRectangle.Contains(control.Bounds), "PDF 参数最小窗口控件边界有效：" + name);
        }
        string evidence = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "audit-evidence"));
        Directory.CreateDirectory(evidence);
        using var bitmap = new System.Drawing.Bitmap(dialog.Width, dialog.Height);
        dialog.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, dialog.Width, dialog.Height));
        bitmap.Save(Path.Combine(evidence, "phase32-pdf-parameters-autocad.png"), System.Drawing.Imaging.ImageFormat.Png);
        zw.ShowInTaskbar=false;zw.StartPosition=FormStartPosition.Manual;
        zw.Location=new System.Drawing.Point(-32000,-32000);zw.Size=zw.MinimumSize;zw.Show();zw.PerformLayout();
        using var zwBitmap=new System.Drawing.Bitmap(zw.Width,zw.Height);
        zw.DrawToBitmap(zwBitmap,new System.Drawing.Rectangle(0,0,zw.Width,zw.Height));
        zwBitmap.Save(Path.Combine(evidence,"phase32-pdf-parameters-zwcad.png"),System.Drawing.Imaging.ImageFormat.Png);
    }
}
