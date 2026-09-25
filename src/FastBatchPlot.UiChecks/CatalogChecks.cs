using System.Reflection;
using FastBatchPlot.Core.Export;
using FastBatchPlot.Core.Models;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckCatalogUi()
    {
        using var form=new CatalogOptionsForm();
        form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-32000,-32000);form.ShowInTaskbar=false;form.Show();
        var type=typeof(CatalogOptionsForm);
        var grid=(DataGridView)type.GetField("columns",PrivateInstance)!.GetValue(form)!;
        Check(grid.Rows.Count==17 && grid.Columns.Cast<DataGridViewColumn>().All(c=>c.SortMode==DataGridViewColumnSortMode.NotSortable),"目录设置提供17种字段，禁用表头排序防止错配");
        grid.CurrentCell=grid.Rows[2].Cells[1];
        type.GetMethod("MoveColumn",PrivateInstance)!.Invoke(form,new object[]{-1});
        var options=(CatalogOptions)type.GetMethod("ReadOptions",PrivateInstance)!.Invoke(form,null)!;
        Check(options.Columns[1].Field==CatalogField.DrawingName && options.Columns[2].Field==CatalogField.DrawingNo,"上移目录列时字段和列宽保持绑定");
        Check(options.SelectedOnly && options.IncludeStatistics,"目录默认仅导出勾选页且包含统计");
        string evidence=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","..","docs","audit-evidence"));
        var frames=new[] {
            new PlotFrame{OrderIndex=1,LayoutName="模型",CalculatedScale=100,DetectedPaper=new PaperSize("A1",841,594),TitleInfo=new TitleBlockInfo{DrawingNo="电施-001",DrawingName="110kV配电装置总平面图",Revision="A",Date="2026-09-20"}},
            new PlotFrame{OrderIndex=2,LayoutName="二次接线",CalculatedScale=2.5,DetectedPaper=new PaperSize("A3+1/2",630,297),TitleInfo=new TitleBlockInfo{DrawingNo="电施-002",DrawingName="保护屏端子接线图",Revision="B",Date="2026-09-20"}},
            new PlotFrame{OrderIndex=3,LayoutName="二次接线",CalculatedScale=50,DetectedPaper=new PaperSize("自定义",500,300),TitleInfo=new TitleBlockInfo{DrawingNo="003",DrawingName="自定义图幅测试",Revision="A",Date="2026-09-20"}}
        };
        CatalogXlsxExporter.Export(frames,Path.Combine(evidence,"catalog-sample.xlsx"),new CatalogOptions(),true);
        Check(File.Exists(Path.Combine(evidence,"catalog-sample.xlsx")),"实际产品导出器生成可供独立复核的中文目录工作簿");
        form.Size=form.MinimumSize;form.PerformLayout();
        using var bitmap=new System.Drawing.Bitmap(form.Width,form.Height);
        form.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,form.Width,form.Height));
        bitmap.Save(Path.Combine(evidence,"ui-catalog-window.png"),System.Drawing.Imaging.ImageFormat.Png);
    }
}
