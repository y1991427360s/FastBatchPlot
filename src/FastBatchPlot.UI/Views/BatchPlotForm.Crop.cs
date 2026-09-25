using System;
using System.Collections.Generic;
using System.Linq;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;
namespace FastBatchPlot.UI.Views
{
    public partial class BatchPlotForm
    {
        private void ApplyTemplatePrintRegions(bool clear=false)
        {
            if(_isPlotting)return;
            var frames=DisplayOrder().Where(f=>f.IsSelected).ToList();
            if(frames.Count==0){lblStatus.Text="请勾选需要调整打印范围的图纸。";return;}
            if(!clear&&_templateLoadError!=null){lblStatus.Text="模板库无效："+_templateLoadError;return;}
            // 整批准备通过后才更新列表，任何一张失败均保留原设置。
            var prepared=new List<Tuple<PlotFrame,PlotFrame,TemplateRegion?>>();
            try
            {
                foreach(var frame in frames)
                {
                    var template=TemplateOutputSettings.Match(frame,_titleTemplates);
                    var region=clear?null:template?.PrintRegion;
                    if(!clear&&region==null)throw new InvalidOperationException("图纸 "+frame.OrderIndex+" 未设置模板打印范围。");
                    var bounds=region==null?new FastBatchPlot.Core.Common.Rect2D(frame.MinX,frame.MinY,frame.MaxX,frame.MaxY):
                        CadHostProvider.Host is ICadTemplateCropHost host?host.ResolveTemplatePrintBounds(frame,region):throw new NotSupportedException("当前宿主不支持模板裁切。");
                    var size=ManualFrameFactory.Create(bounds,frame.CalculatedScale,frame.SourceDocumentId,frame.SourceLayoutId,frame.LayoutName);
                    prepared.Add(Tuple.Create(frame,size,region==null?null:TemplateCropGeometry.Copy(region)));
                }
                foreach(var item in prepared)
                {
                    var frame=item.Item1;var size=item.Item2;frame.AppliedPrintRegion=item.Item3;
                    frame.PrintBounds=item.Item3==null?(FastBatchPlot.Core.Common.Rect2D?)null:new FastBatchPlot.Core.Common.Rect2D(size.MinX,size.MinY,size.MaxX,size.MaxY);
                    frame.DetectedPaper=size.DetectedPaper;frame.IsLandscape=size.IsLandscape;frame.Status=clear?"已恢复图框原范围":"已应用模板打印范围";
                }
                RefreshGrid();lblStatus.Text=$"已更新 {prepared.Count} 张打印范围，保持原出图比例并重新计算纸张；请检查图幅。";
            }
            catch(Exception ex){lblStatus.Text="打印范围未修改："+ex.Message;}
        }
    }
}
