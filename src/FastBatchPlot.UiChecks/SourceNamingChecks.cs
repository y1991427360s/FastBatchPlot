using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckSourceNaming()
    {
        var previous = CadHostProvider.Host;
        try {
            CadHostProvider.Host = new SelectionHost(); // 当前活动文件为 test.dwg，与列表来源不同。
            using var main = new BatchPlotForm(null);
            var frames = Field<List<PlotFrame>>(main, "_frames");
            var first = new PlotFrame { SourceFileName = "原图甲.dwg", LayoutName = "总图", OrderIndex = 1 };
            var second = new PlotFrame { SourceFileName = "原图乙.dwg", LayoutName = "详图", OrderIndex = 2 };
            frames.AddRange(new[] { first, second });
            Field<TextBox>(main, "txtNamingTemplate").Text = "{DwgFileName}_{Layout}_{Index}";
            Call(main, "RefreshGrid");
            Check(first.CustomOutputFileName == "原图甲_总图_01" && second.CustomOutputFileName == "原图乙_详图_02", "列表刷新使用各页来源文件，不使用当前活动 DWG");
            var grid = Field<DataGridView>(main, "dgvDrawings"); grid.Rows[0].Cells[2].Value = "重新编号";
            Check(first.CustomOutputFileName == "原图甲_总图_01", "编辑图号后的文件名刷新保留原文件快照");
            CadHostProvider.Host = null; Call(main, "RefreshFileNames");
            Check(first.CustomOutputFileName == "原图甲_总图_01", "离开宿主后刷新名称仍使用来源快照");
            grid.Rows[0].Cells[7].Value = "人工命名"; Call(main, "RefreshGrid");
            Check(first.CustomOutputFileName == "人工命名", "来源命名修复保留人工覆盖名称");
            using var bookmark = new BookmarkTemplateForm("{文件名}/{布局}", frames);
            Check(Field<ListBox>(bookmark, "preview").Items[1].ToString() == "原图乙/详图", "书签编辑器预览来源文件和布局");
        } finally { CadHostProvider.Host = previous; }
    }
}
