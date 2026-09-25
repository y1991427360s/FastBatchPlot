using FastBatchPlot.Core.Models;
using FastBatchPlot.UI.Views;
using System.ComponentModel;

internal static partial class Program
{
    private static void CheckPaperColumns()
    {
        using var form=new BatchPlotForm(null);form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-32000,-32000);form.Show();
        var frame=new PlotFrame {OrderIndex=1,MaxX=42000,MaxY=29700,CalculatedScale=100,IsLandscape=true,DetectedPaper=new PaperSize("A3",420,297),TitleInfo=new TitleBlockInfo{DrawingNo="B"}};
        var other=new PlotFrame {OrderIndex=2,MaxX=21000,MaxY=29700,CalculatedScale=100,IsLandscape=false,DetectedPaper=new PaperSize("A4",210,297),TitleInfo=new TitleBlockInfo{DrawingNo="A"}};
        var frames=Field<List<PlotFrame>>(form,"_frames");frames.AddRange(new[]{frame,other});Call(form,"RefreshGrid");
        var grid=Field<DataGridView>(form,"dgvDrawings");grid.Sort(grid.Columns[2],ListSortDirection.Ascending);
        var row=grid.Rows.Cast<DataGridViewRow>().Single(r=>ReferenceEquals(r.Tag,frame));
        Check(Convert.ToString(row.Cells["BasePaperSize"].Value)=="420 × 297","基础尺寸列显示横向实际宽高");
        Field<NumericUpDown>(form,"numMargin").Value=2;
        Check(Convert.ToString(row.Cells["PdfPaperSize"].Value)=="424 × 301"&&Convert.ToString(row.Cells["PdfScale"].Value)=="1:100","正留白即时更新 PDF 纸张，保持比例");
        Field<NumericUpDown>(form,"numMargin").Value=-2;
        Check(Convert.ToString(row.Cells["PdfPaperSize"].Value)=="420 × 297"&&Convert.ToString(row.Cells["PdfScale"].Value)!="1:100","负留白即时更新 PDF 实际比例，保持纸张");
        Field<NumericUpDown>(form,"numMargin").Value=0;
        row.Cells["BasePaperSize"].Value="500.125x350.5";
        Check(frame.DetectedPaper.WidthMm==500.125&&frame.DetectedPaper.HeightMm==350.5&&other.DetectedPaper.WidthMm==210,"排序后尺寸编辑定位原行并保留三位小数");
        row.Cells["BasePaperSize"].Value="100x100";
        Check(frame.DetectedPaper.WidthMm==500.125&&Convert.ToString(row.Cells["BasePaperSize"].Value)=="500.125 × 350.5"&&Field<Label>(form,"lblStatus").Text.Contains("尺寸未修改"),"放不下图纸的尺寸编辑被拒绝并恢复显示");
        row.Cells[5].Value="1:100.125";Call(form,"RefreshGrid");
        row=grid.Rows.Cast<DataGridViewRow>().Single(r=>ReferenceEquals(r.Tag,frame));
        Check(Convert.ToString(row.Cells[5].Value)=="1:100.125","刷新后比例不再截断为两位小数");
        row.Cells[5].Value="1:1";
        Check(Convert.ToString(row.Cells["PdfPaperSize"].Value)=="设置无效"&&row.Cells["PdfPaperSize"].ErrorText.Contains("超出"),"直接比例编辑导致溢出时即时显示规划错误");
        row.Cells[5].Value="1:100";
        Check(row.Cells["PdfPaperSize"].ErrorText=="","修正比例后清除过期错误");
        Reject(()=>Call(form,"ApplyScales",frames,1d),"全批比例预检拒绝超出纸张");
        Check(frames.All(f=>f.CalculatedScale==100),"全批比例失败不部分修改");
        Call(form,"ApplyScales",frames,200.125d);
        Check(frames.All(f=>f.CalculatedScale==200.125)&&frame.DetectedPaper.WidthMm==500.125,"全部比例支持小数且保持基础纸张");
    }
}
