using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;
using FastBatchPlot.UI.Views;
internal static partial class Program
{
    private static void CheckCropUi()
    {
        var old=CadHostProvider.Host;CadHostProvider.Host=new TemplateHost();
        try
        {
            using var form=new BatchPlotForm(null);var list=Field<List<PlotFrame>>(form,"_frames");var lib=Field<TitleTemplateLibrary>(form,"_titleTemplates");
            lib.Templates.Add(new TitleBlockTemplate{Name="A",BlockName="A",PrintRegion=new TemplateRegion{X1=-420,Y1=0,X2=0,Y2=297}});
            for(int i=1;i<=2;i++)list.Add(new PlotFrame{OrderIndex=i,HandleOrId=i.ToString(),Type=FrameType.BlockReference,SourceBlockName="A",SourceDocumentId="doc",SourceLayoutId="space",MinX=0,MinY=0,MaxX=500,MaxY=400,CalculatedScale=1});
            Call(form,"RefreshGrid");Call(form,"ApplyTemplatePrintRegions",false);Check(list.All(f=>f.PrintBounds==null),"任一图框裁切失败时整批列表保持原范围");
            ((TemplateHost)CadHostProvider.Host).FailSecond=false;Call(form,"ApplyTemplatePrintRegions",false);
            Check(list.All(f=>f.PrintBounds!.Value.MinX==10&&f.DetectedPaper.Name=="A3"&&f.CalculatedScale==1),"应用裁切保持比例并按新范围识别纸张");
            Check(list.All(f=>f.MinX==0&&f.MaxX==500&&f.SourceLayoutId=="space"),"应用裁切不修改原图框几何或来源身份");
            Call(form,"ApplyTemplatePrintRegions",true);Check(list.All(f=>f.PrintBounds==null&&f.AppliedPrintRegion==null&&f.DetectedPaper.WidthMm==500),"恢复原范围清除裁切并重算原范围纸张");
            using var dialog=new TemplateCropForm(lib.Templates[0].PrintRegion,()=>null);var region=(TemplateRegion)typeof(TemplateCropForm).GetMethod("ReadRegion",PrivateInstance)!.Invoke(dialog,null)!;
            Check(region.X1==-420&&region.Y2==297,"裁切编辑器读取本地坐标，不改写调用方模板");
            dialog.ShowInTaskbar=false;dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new System.Drawing.Point(-32000,-32000);dialog.Show();
            using var image=new System.Drawing.Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,image.Width,image.Height));image.Save(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","..","docs","audit-evidence","ui-template-crop.png")));
        }
        finally{CadHostProvider.Host=old;}
    }
}
