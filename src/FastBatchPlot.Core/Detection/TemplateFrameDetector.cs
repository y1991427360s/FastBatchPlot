using System;
using System.Collections.Generic;
using System.Linq;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Paper;
using FastBatchPlot.Core.Templates;

namespace FastBatchPlot.Core.Detection
{
    /// <summary>已登记的打印范围先解析，再推断纸张；不能先用原块长宽比淘汰模板块。</summary>
    public static class TemplateFrameDetector
    {
        public static List<PlotFrame> Detect(IEnumerable<RawBlockCandidate> source,TitleTemplateLibrary library,
            Func<PlotFrame,TemplateRegion,Rect2D> resolve,double preferredScale=0,DetectionReport? report=null)
        {
            PaperSizeDetector.ValidatePreferredScale(preferredScale);
            TitleTemplateService.Validate(library);
            var byBlock=library.Templates.ToLookup(t=>t.BlockName.Trim(),StringComparer.OrdinalIgnoreCase);
            var result=new List<PlotFrame>();
            foreach(var candidate in source)
            {
                var template=TitleTemplateService.FindBestTemplate(byBlock[candidate.BlockName],candidate.Bounds.Width,candidate.Bounds.Height);
                if(template?.PrintRegion==null)
                {
                    var generic=BlockFrameDetector.ProcessBlockCandidates(new[]{candidate},template!=null && template.PrintScale>0?template.PrintScale:preferredScale,report);
                    foreach(var frame in generic){if(template!=null){frame.TemplateId=template.Id;frame.TemplatePriority=template.Priority;}result.Add(frame);}
                    continue;
                }
                try
                {
                    var original=candidate.Bounds;
                    if(!Valid(original))throw new ArgumentException("来源块包围盒无效。");
                    var frame=new PlotFrame{Type=FrameType.BlockReference,SourceBlockName=candidate.BlockName,SourceLayer=candidate.Layer,
                        SourceFileName=candidate.SourceFileName,SourceDocumentId=candidate.SourceDocumentId,SourceLayoutId=candidate.SourceLayoutId,LayoutName=candidate.LayoutName,
                        LayoutOrder=candidate.LayoutOrder,HandleOrId=candidate.Handle,RotationDegrees=candidate.RotationDegrees,
                        MinX=original.MinX,MinY=original.MinY,MaxX=original.MaxX,MaxY=original.MaxY,
                        TemplateId=template.Id,TemplatePriority=template.Priority,TitleInfo=BlockFrameDetector.ExtractTitleBlockInfo(candidate.Attributes)};
                    var bounds=resolve(frame,TemplateCropGeometry.Copy(template.PrintRegion));
                    if(!Valid(bounds))throw new ArgumentException("模板转换后的打印范围无效。");
                    double scale=template.PrintScale>0?template.PrintScale:preferredScale;
                    // 图框型：模板已登记纸张尺寸时，比例 = 打印范围 / 登记纸张（与原版一致，也适用于以厘米绘制的图框块）。
                    if(scale==0&&template.PaperWidth>0&&template.PaperHeight>0)scale=ScaleFromTemplatePaper(template,bounds);
                    if(scale==0)
                    {
                        double sx=Math.Abs(candidate.ScaleX),sy=Math.Abs(candidate.ScaleY);
                        double hint=TitleTemplateService.Finite(sx)&&sx>0&&Math.Abs(sx-sy)<sx*1e-6?sx:0;
                        var detected=PaperSizeDetector.Detect(bounds.Width,bounds.Height,hint);
                        if(detected.MatchScore>PaperSizeDetector.AcceptableMatchError&&hint>0)detected=PaperSizeDetector.Detect(bounds.Width,bounds.Height);
                        if(detected.MatchScore>PaperSizeDetector.AcceptableMatchError)
                        {
                            if(template.PaperWidth>0 && template.PaperHeight>0)
                            {
                                double tLonger=Math.Max(template.PaperWidth,template.PaperHeight);
                                double tShorter=Math.Min(template.PaperWidth,template.PaperHeight);
                                double bLonger=Math.Max(bounds.Width,bounds.Height);
                                double bShorter=Math.Min(bounds.Width,bounds.Height);
                                double rScale=bShorter/tShorter;
                                double cScale=ScaleCalculator.MatchClosestStandardScale(rScale);
                                if(cScale<=0)cScale=Math.Round(rScale);
                                if(cScale>0 && (Math.Abs(bLonger-tLonger*cScale)/(tLonger*cScale)+Math.Abs(bShorter-tShorter*cScale)/(tShorter*cScale))/2.0<=0.10)
                                {
                                    scale=cScale;
                                }
                                else throw new InvalidOperationException("非标准裁切尺寸无法可靠推断比例，请在模板打印范围中填写出图比例。");
                            }
                            else throw new InvalidOperationException("非标准裁切尺寸无法可靠推断比例，请在模板打印范围中填写出图比例。");
                        }
                        else
                        {
                            scale=detected.Scale;
                        }
                    }
                    var sized=ManualFrameFactory.Create(bounds,scale,candidate.SourceDocumentId,candidate.SourceLayoutId,candidate.LayoutName);
                    if(sized.DetectedPaper.Name=="自定义" && !string.IsNullOrWhiteSpace(template.Name))
                    {
                        var std=PaperSize.StandardSizes.FirstOrDefault(s=>string.Equals(s.Name,template.Name.Trim(),StringComparison.OrdinalIgnoreCase));
                        if(std!=null){sized.DetectedPaper.Name=std.Name;sized.DetectedPaper.StandardName=std.StandardName;}
                    }
                    frame.DetectedPaper=sized.DetectedPaper;frame.CalculatedScale=scale;frame.IsLandscape=sized.IsLandscape;
                    frame.AppliedPrintRegion=TemplateCropGeometry.Copy(template.PrintRegion);frame.PrintBounds=bounds;
                    frame.Status="已按模板识别打印范围";result.Add(frame);
                }
                // 单个图框块异常不能中断整次搜索：记入识别报告并跳过。
                catch(Exception ex) when(report!=null){report.Add("模板图框解析失败："+ex.Message,candidate);}
                catch(Exception ex){throw new InvalidOperationException("模板图框 "+candidate.BlockName+" ["+candidate.Handle+"]："+ex.Message+" 原列表已保留。",ex);}
            }
            for(int i=0;i<result.Count;i++){result[i].Id=i+1;result[i].OrderIndex=i+1;}
            return result;
        }
        private static double ScaleFromTemplatePaper(TitleBlockTemplate template,Rect2D bounds)
        {
            double tLonger=Math.Max(template.PaperWidth,template.PaperHeight),tShorter=Math.Min(template.PaperWidth,template.PaperHeight);
            double bLonger=Math.Max(bounds.Width,bounds.Height),bShorter=Math.Min(bounds.Width,bounds.Height);
            if(!(tShorter>0&&bShorter>0))return 0;
            // 长宽比与登记纸张明显不同（如模板裁切了打印范围）时交回通用推断。
            if(Math.Abs(bLonger/bShorter-tLonger/tShorter)/(tLonger/tShorter)>0.03)return 0;
            double raw=(bLonger/tLonger+bShorter/tShorter)/2.0;
            double standard=ScaleCalculator.MatchClosestStandardScale(raw,0.005);
            return standard>0?standard:Math.Round(raw,4);
        }
        private static bool Valid(Rect2D b)=>new[]{b.MinX,b.MinY,b.MaxX,b.MaxY}.All(TitleTemplateService.Finite)&&b.MaxX>b.MinX&&b.MaxY>b.MinY;
    }
}
