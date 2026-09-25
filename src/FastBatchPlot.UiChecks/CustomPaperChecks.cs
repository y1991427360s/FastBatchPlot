using FastBatchPlot.Core.Models;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckCustomPaper()
    {
        var frame = new PlotFrame { OrderIndex = 1, MaxX = 42000, MaxY = 29700, CalculatedScale = 100,
            DetectedPaper = new FastBatchPlot.Core.Models.PaperSize("A3", 420, 297), IsLandscape = true };
        using var dialog = new CustomPaperForm(new[] { frame }, new PlotConfig { MarginMm = 2 });
        dialog.ShowInTaskbar = false; dialog.StartPosition = FormStartPosition.Manual;
        dialog.Location = new System.Drawing.Point(-32000, -32000); dialog.Show(); dialog.Size = dialog.MinimumSize; dialog.PerformLayout();
        Field<NumericUpDown>(dialog, "width").Value = 297;
        Field<NumericUpDown>(dialog, "height").Value = 210;
        Check(!Field<Button>(dialog, "apply").Enabled, "自定义小纸张保持原比例时阻止裁切输出");
        Field<ComboBox>(dialog, "scaleMode").SelectedIndex = 1;
        Check(Field<Button>(dialog, "apply").Enabled && Field<DataGridView>(dialog, "preview").Rows[0].Cells[2].Value.ToString()!.Contains("301"), "自定义等比适配预览包含留白后的真实尺寸");
        Check(frame.CalculatedScale == 100 && frame.DetectedPaper.Name == "A3", "自定义纸张预览和取消不改变原列表");
        foreach (var name in new[] { "width", "height", "scaleMode", "preview", "status", "apply" }) {
            var control = Field<Control>(dialog, name);
            Check(control.Width > 0 && control.Height > 0 && control.Parent!.ClientRectangle.Contains(control.Bounds), "自定义纸张最小窗口控件完整：" + name);
        }
        string evidence = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "audit-evidence"));
        using var bitmap = new System.Drawing.Bitmap(dialog.Width, dialog.Height);
        dialog.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, dialog.Width, dialog.Height));
        bitmap.Save(Path.Combine(evidence, "phase29-custom-paper.png"), System.Drawing.Imaging.ImageFormat.Png);

        using var main = new BatchPlotForm(null);
        main.ShowInTaskbar = false; main.StartPosition = FormStartPosition.Manual; main.Location = new System.Drawing.Point(-32000, -32000); main.Show();
        var frames = Field<List<PlotFrame>>(main, "_frames"); frames.Add(frame);
        var other = new PlotFrame { OrderIndex = 2, MaxX = 84100, MaxY = 59400, CalculatedScale = 100,
            DetectedPaper = new FastBatchPlot.Core.Models.PaperSize("A1", 841, 594), IsLandscape = true };
        frames.Add(other); Call(main, "RefreshGrid");
        var grid = Field<DataGridView>(main, "dgvDrawings"); grid.Rows[0].Selected = true; grid.Rows[1].Selected = true;
        Reject(() => Call(main, "ApplyCustomPaper", 420d, 297d, false), "批量纸张预检失败时阻止应用");
        Check(frame.DetectedPaper.Name == "A3" && other.DetectedPaper.Name == "A1", "批量后页失败时前页不会被部分修改");
        Call(main, "ApplyCustomPaper", 200d, 400d, true);
        Check(frames.All(f => !f.IsLandscape && f.DetectedPaper.WidthMm == 200 && f.DetectedPaper.HeightMm == 400), "自定义尺寸按所选行批量应用纵向方向");
        Check(!ReferenceEquals(frame.DetectedPaper, other.DetectedPaper), "自定义纸张对象按页隔离");
        Call(main, "SetPlottingState", true);
        Reject(() => Call(main, "ApplyCustomPaper", 400d, 200d, true), "运行中禁止修改自定义尺寸");
        Call(main, "SetPlottingState", false);
    }
}
