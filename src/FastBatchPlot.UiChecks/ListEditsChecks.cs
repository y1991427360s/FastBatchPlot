using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Naming;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckListEditsUi()
    {
        using var form=new BatchPlotForm(null);
        var frames=Field<List<PlotFrame>>(form,"_frames");
        var a=new PlotFrame{TitleInfo=new TitleBlockInfo{DrawingNo="A",DrawingName="第一图"},SourceLayoutId="1",LayoutOrder=1};
        var b=new PlotFrame{TitleInfo=new TitleBlockInfo{DrawingNo="B",DrawingName="第二图"},SourceLayoutId="2",LayoutOrder=2};
        var c=new PlotFrame{TitleInfo=new TitleBlockInfo{DrawingNo="C",DrawingName="第三图"},SourceLayoutId="1",LayoutOrder=1};
        frames.AddRange(new[]{a,b,c});Call(form,"RefreshGrid");
        var grid=Field<DataGridView>(form,"dgvDrawings");
        grid.Rows[0].Cells[7].Value="人工名字";
        grid.Sort(grid.Columns[2],System.ComponentModel.ListSortDirection.Descending);
        grid.ClearSelection();grid.Rows[0].Selected=true;
        Call(form,"MoveSelectedRows",1);
        Check(frames.SequenceEqual(new[]{b,c,a}) && Field<ComboBox>(form,"cboSortRule").SelectedIndex==4,"表头排序后移动作用于可见行，切换为跨布局手动顺序");
        Call(form,"ReorderFrames");
        Check(frames.SequenceEqual(new[]{b,c,a}) && a.CustomOutputFileName=="人工名字","手动顺序经刷新保持，人工文件名不被覆盖");
        var plan=DrawingListEdits.PlanNumbers(new[]{b,c},new RenumberOptions{Prefix="电-",Digits=2,Start=5});
        Call(form,"ApplyRenumber",plan);
        Check(b.TitleInfo.DrawingNo=="电-05" && c.TitleInfo.DrawingNo=="电-06" && b.CustomOutputFileName.Contains("电-05"),"应用重编更新选定对象和自动文件名");
        Call(form,"UndoRenumber");
        Check(b.TitleInfo.DrawingNo=="B" && c.TitleInfo.DrawingNo=="C" && a.CustomOutputFileName=="人工名字","撤销重编恢复原图号并保留人工文件名");
        b.TitleInfo.DrawingNo="A";b.CustomOutputFileName=a.CustomOutputFileName;
        Call(form,"RefreshDuplicateIndicators");
        var row=grid.Rows.Cast<DataGridViewRow>().Single(r=>ReferenceEquals(r.Tag,b));
        Check(row.Cells[2].ToolTipText.Contains("重复") && row.Cells[7].Style.BackColor==System.Drawing.Color.MistyRose,"重复图号及文件名在正确对象行标红并提供原因");
        b.TitleInfo.DrawingNo="B";b.CustomOutputFileName="独立";Call(form,"RefreshDuplicateIndicators");
        Check(row.Cells[2].ToolTipText=="" && row.Cells[7].Style.BackColor.IsEmpty,"冲突消除后清理旧标记");
        Field<TextBox>(form,"txtNamingTemplate").Text="相同文件名";
        Check(grid.Rows.Cast<DataGridViewRow>().Where(r=>ReferenceEquals(r.Tag,b)||ReferenceEquals(r.Tag,c)).All(r=>r.Cells[7].ToolTipText.Contains("重复")),
            "修改命名模板后立即刷新生成文件名的重复提示");
        using var dialog=new RenumberForm(new[]{b,c},frames);
        dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new System.Drawing.Point(-32000,-32000);dialog.ShowInTaskbar=false;dialog.Show();
        var type=typeof(RenumberForm);
        var prefix=(TextBox)type.GetField("prefix",PrivateInstance)!.GetValue(dialog)!;
        prefix.Text="电施-";
        Check(dialog.Changes[0].After=="电施-001" && b.TitleInfo.DrawingNo=="B","重编窗口输入实时生成预览且不修改图纸");
        a.TitleInfo.DrawingNo="电施-001";
        type.GetMethod("RefreshPreview",PrivateInstance)!.Invoke(dialog,null);
        Check(!((Button)type.GetField("apply",PrivateInstance)!.GetValue(dialog)!).Enabled,"预览与未重编图号冲突时禁止应用");
        string evidence=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","..","docs","audit-evidence"));
        dialog.Size=dialog.MinimumSize;dialog.PerformLayout();
        using var image=new System.Drawing.Bitmap(dialog.Width,dialog.Height);
        dialog.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,dialog.Width,dialog.Height));
        image.Save(Path.Combine(evidence,"ui-renumber-window.png"),System.Drawing.Imaging.ImageFormat.Png);
    }
}
