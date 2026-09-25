using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Naming;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckBookmarkEditor()
    {
        using var dialog = new BookmarkTemplateForm("{DwgName} / {Scale}", new[] {
            new PlotFrame { OrderIndex = 4, CalculatedScale = 2.5, TitleInfo = new TitleBlockInfo { DrawingName = "总图:详图" } } });
        dialog.ShowInTaskbar = false; dialog.StartPosition = FormStartPosition.Manual;
        dialog.Location = new System.Drawing.Point(-32000, -32000); dialog.Show();
        var template = Field<TextBox>(dialog, "template");
        var preview = Field<ListBox>(dialog, "preview");
        var apply = Field<Button>(dialog, "apply");
        Check(preview.Items[0].ToString() == "总图:详图 / 1:2.5" && apply.Enabled, "书签编辑器即时预览中文、标点和小数比例");
        template.Text = "{Wrong}";
        Check(!apply.Enabled && Field<Label>(dialog, "status").Text.Contains("未知"), "未知书签字段禁用应用并显示原因");
        template.Text = "";
        Check(apply.Enabled && preview.Items[0].ToString() == "04 图纸4 总图:详图", "清空书签模板恢复默认格式预览");
        template.Text = DrawingNameFormatter.DefaultBookmarkTemplate;
        dialog.Size = dialog.MinimumSize; dialog.PerformLayout();
        foreach (var control in new Control[] { template, preview, apply, Field<Label>(dialog, "status") })
            Check(control.Width > 0 && control.Height > 0 && control.Parent!.ClientRectangle.Contains(control.Bounds), "书签编辑器最小窗口控件边界有效：" + control.GetType().Name);
        string evidence = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "audit-evidence"));
        using var bitmap = new System.Drawing.Bitmap(dialog.Width, dialog.Height);
        dialog.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, dialog.Width, dialog.Height));
        bitmap.Save(Path.Combine(evidence, "phase25-bookmark-editor.png"), System.Drawing.Imaging.ImageFormat.Png);
    }
}
