using System.Linq;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;
using FastBatchPlot.UI.Views;
internal static partial class Program
{
    private static void CheckTemplateDetectionUi()
    {
        var old=CadHostProvider.Host;var host=new TemplateHost{FailSecond=false};CadHostProvider.Host=host;
        try
        {
            using var form=new BatchPlotForm(null);var library=Field<TitleTemplateLibrary>(form,"_titleTemplates");
            var longFrame=new List<RawBlockCandidate>{new(){BlockName="长框",Bounds=new Rect2D(0,0,50000,10000),SourceDocumentId="doc",SourceLayoutId="space"}};
            var initial=(FrameSelectionResult)Call(form,"SelectCandidates",new List<RawPolylineCandidate>(),longFrame)!;
            Check(initial.Frames.Count==0,"自动模式保留原标准图幅过滤");
            Field<NumericUpDown>(form,"numDetectionScale").Value=100;
            initial=(FrameSelectionResult)Call(form,"SelectCandidates",new List<RawPolylineCandidate>(),longFrame)!;
            Check(initial.Frames.Count==1 && initial.Frames[0].DetectedPaper.WidthMm==500 && initial.Frames[0].CalculatedScale==100,"主窗识别比例传入搜索并发现非标图框");
            library.Templates.Add(new TitleBlockTemplate{Name="小定位框",BlockName="小框",PrintScale=1,PrintRegion=new TemplateRegion{X1=-420,Y1=0,X2=0,Y2=297}});
            var candidates=new List<RawBlockCandidate>{new(){BlockName="小框",Handle="1",Bounds=new Rect2D(0,0,10,10),SourceDocumentId="doc",SourceLayoutId="space"}};
            var result=(FrameSelectionResult)Call(form,"SelectCandidates",new List<RawPolylineCandidate>(),candidates)!;
            Check(result.Frames.Count==1&&result.Frames[0].PrintBounds.HasValue&&result.Frames[0].DetectedPaper.Name=="A3","搜索按模板裁切识别小定位块并自动应用打印范围");
            Check(result.Frames[0].CalculatedScale==1,"模板明确比例优先于主窗识别比例");
            host.FailSecond=true;candidates[0].Handle="2";
            typeof(BatchPlotForm).GetField("_specifiedLayerName",PrivateInstance)!.SetValue(form,"OTHER");
            result=(FrameSelectionResult)Call(form,"SelectCandidates",new List<RawPolylineCandidate>(),candidates)!;
            Check(result.Frames.Count==0,"图层筛选在模板解析前排除无关对象，不触发无关错误");
            typeof(BatchPlotForm).GetField("_specifiedLayerName",PrivateInstance)!.SetValue(form,"");
            // 单个模板图框解析失败不再中断整次搜索：跳过该块并在识别报告中写明原因。
            result=(FrameSelectionResult)Call(form,"SelectCandidates",new List<RawPolylineCandidate>(),candidates)!;
            Check(result.Frames.Count==0&&result.Report.Counts.Keys.Any(k=>k.StartsWith("模板图框解析失败")),"模板解析失败的图框被跳过并写入识别报告，不中断其他图框");
            using var dialog=new TemplateCropForm(library.Templates[0].PrintRegion,()=>null,2.5);
            Check(dialog.PrintScale==2.5,"模板裁切窗口保留非整数搜索比例");
            dialog.ShowInTaskbar=false;dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new System.Drawing.Point(-32000,-32000);dialog.Show();
            using var image=new System.Drawing.Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,image.Width,image.Height));image.Save(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","..","docs","audit-evidence","ui-template-detection.png")));
        }
        finally{CadHostProvider.Host=old;}
    }
}
