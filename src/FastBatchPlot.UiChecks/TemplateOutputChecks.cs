using FastBatchPlot.Core.Export;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckTemplateOutputUi()
    {
        var options=new CatalogOptions{Title="专用工程目录",RowHeight=40,Columns=new List<CatalogColumn>{new(){Field=CatalogField.Revision,Header="修订版",Width=12},new(){Field=CatalogField.DrawingNo,Header="工程图号",Width=26}}};
        var choices=new List<KeyValuePair<string,CatalogOptions>>{new("通用",new()),new("电气图框",options)};
        using(var dialog=new CatalogOptionsForm(profiles:choices))
        {
            var save=dialog.Controls.OfType<Button>().Single();var top=dialog.Controls.OfType<FlowLayoutPanel>().Single();var source=top.Controls.OfType<ComboBox>().Single();
            Check(!save.Enabled&&source.SelectedIndex==-1,"含专用目录时要求明确选择格式，不能默认套用第一份");source.SelectedIndex=1;
            var read=(CatalogOptions)typeof(CatalogOptionsForm).GetMethod("ReadOptions",PrivateInstance)!.Invoke(dialog,null)!;
            Check(save.Enabled&&read.Title=="专用工程目录"&&read.Columns[0].Field==CatalogField.Revision&&read.RowHeight==40,"目录格式选择恢复列顺序、标题和行高");
        }
        using var form=new BatchPlotForm(null);var library=Field<TitleTemplateLibrary>(form,"_titleTemplates");
        library.Templates.Add(new TitleBlockTemplate{Name="测试图框",BlockName="A",NamingTemplate="专用_{DwgNo}",Catalog=options});
        var f=new PlotFrame{Type=FrameType.BlockReference,SourceBlockName="A",TitleInfo=new TitleBlockInfo{DrawingNo="001"}};Field<List<PlotFrame>>(form,"_frames").Add(f);Call(form,"RefreshGrid");
        Check(f.CustomOutputFileName=="专用_001","图框专用命名自动覆盖通用命名规则");
        var grid=Field<DataGridView>(form,"dgvDrawings");grid.Rows[0].Cells[7].Value="人工文件名";Call(form,"RefreshGrid");Check(f.CustomOutputFileName=="人工文件名","人工文件名在模板命名联动后仍优先保留");
        using(var editor=new TitleTemplateForm(library,null,()=>null,frame=>null))
        {
            Check(Field<TextBox>(editor,"naming").Text=="专用_{DwgNo}","模板编辑器读取专用命名规则");Field<TextBox>(editor,"naming").Text="新_{DwgNo}";
            typeof(TitleTemplateForm).GetMethod("CommitEditor",PrivateInstance)!.Invoke(editor,null);Check(editor.Library.Templates[0].NamingTemplate=="新_{DwgNo}"&&editor.Library.Templates[0].Catalog!.Title=="专用工程目录","模板编辑提交保留目录并更新命名");
            editor.ShowInTaskbar=false;editor.StartPosition=FormStartPosition.Manual;editor.Location=new System.Drawing.Point(-32000,-32000);editor.Show();editor.Size=editor.MinimumSize;
            using var image=new System.Drawing.Bitmap(editor.Width,editor.Height);editor.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,image.Width,image.Height));image.Save(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","..","docs","audit-evidence","ui-template-output.png")));
        }
        string dir=Path.Combine(Path.GetTempPath(),"catalog-pref-ui-"+Guid.NewGuid().ToString("N")),path=Path.Combine(dir,"prefs.json");
        try
        {
            using(var first=new BatchPlotForm(path)){Field<CatalogOptions>(first,"_catalogOptions").Title="保存目录格式";Call(first,"SavePreferences");}
            using(var second=new BatchPlotForm(path))Check(Field<CatalogOptions>(second,"_catalogOptions").Title=="保存目录格式","保存默认设置后新窗口恢复目录配置");
        }
        finally{if(File.Exists(path))File.Delete(path);if(Directory.Exists(dir))Directory.Delete(dir);}
    }
}
