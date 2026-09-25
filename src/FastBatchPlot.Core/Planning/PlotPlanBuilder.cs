using System;
using System.Collections.Generic;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Planning
{
    /// <summary>所有尺寸均为毫米；比例为 CAD 图形单位 / 纸面毫米。</summary>
    public sealed class PlotPlan
    {
        public double MinX { get; internal set; }
        public double MinY { get; internal set; }
        public double MaxX { get; internal set; }
        public double MaxY { get; internal set; }
        public double PaperWidthMm { get; internal set; }
        public double PaperHeightMm { get; internal set; }
        public double ScaleDenominator { get; internal set; }
        public double ContentLeftMm { get; internal set; }
        public double ContentBottomMm { get; internal set; }
        public double AppliedBorderInsetMm { get; internal set; }
        public double ContentWidthMm => (MaxX - MinX) / ScaleDenominator;
        public double ContentHeightMm => (MaxY - MinY) / ScaleDenominator;
    }

    public sealed class PlotMedia
    {
        public string Name { get; set; } = string.Empty;
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public double PrintableLeftMm { get; set; }
        public double PrintableBottomMm { get; set; }
        public double PrintableWidthMm { get; set; }
        public double PrintableHeightMm { get; set; }
    }

    public static class PlotPlanBuilder
    {
        // 只容忍单位换算的微小舍入；0.5 mm 会吞掉小留白并放过可打印区域的实际裁切。
        public const double MediaToleranceMm = 0.01;

        public static PlotPlan Create(PlotFrame frame, PlotConfig config)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (config == null) throw new ArgumentNullException(nameof(config));
            (config.PdfOptions ?? throw new ArgumentException("PDF 参数不能为空。")).Validate();
            if (!Enum.IsDefined(typeof(PlotExportFormat), config.ExportFormat))
                throw new ArgumentException("未知输出格式。");
            if (config.Copies < 1 || config.Copies > 999) throw new ArgumentException("打印份数必须为 1 到 999。");
            var bounds=frame.PrintBounds??new FastBatchPlot.Core.Common.Rect2D{MinX=frame.MinX,MinY=frame.MinY,MaxX=frame.MaxX,MaxY=frame.MaxY};
            if (!Finite(bounds.MinX) || !Finite(bounds.MinY) || !Finite(bounds.MaxX) || !Finite(bounds.MaxY)
                || bounds.MaxX <= bounds.MinX || bounds.MaxY <= bounds.MinY)
                throw new ArgumentException("图框坐标必须有限且宽高大于零。");
            if (!Finite(frame.CalculatedScale) || frame.CalculatedScale <= 0)
                throw new ArgumentException("打印比例必须为有限正数。");
            if (!Finite(config.MarginMm)) throw new ArgumentException("留白必须为有限数值。");
            ValidateBorderInset(config.OuterBorderInsetMm);
            if (frame.DetectedPaper == null || !Finite(frame.DetectedPaper.WidthMm)
                || !Finite(frame.DetectedPaper.HeightMm) || frame.DetectedPaper.WidthMm <= 0 || frame.DetectedPaper.HeightMm <= 0)
                throw new ArgumentException("纸张宽高必须为有限正数。");

            bool landscape = config.AutoOrientation ? frame.IsLandscape : frame.DetectedPaper.IsLandscape;
            double width = landscape ? frame.DetectedPaper.LongerEdgeMm : frame.DetectedPaper.ShorterEdgeMm;
            double height = landscape ? frame.DetectedPaper.ShorterEdgeMm : frame.DetectedPaper.LongerEdgeMm;
            double scale = frame.CalculatedScale;
            var margins=config.Margins ?? throw new ArgumentException("留白设置不能为空。");
            margins.Validate();
            bool uniform=margins.Mode==MarginMode.Uniform;
            double left=uniform?Math.Abs(config.MarginMm):margins.Left;
            double right=uniform?Math.Abs(config.MarginMm):margins.Right;
            double top=uniform?Math.Abs(config.MarginMm):margins.Top;
            double bottom=uniform?Math.Abs(config.MarginMm):margins.Bottom;
            bool expand=uniform?config.MarginMm>=0:margins.Mode==MarginMode.ExpandPaper;
            if(expand){width+=left+right;height+=top+bottom;}
            else
            {
                double innerWidth=width-left-right,innerHeight=height-top-bottom;
                if(innerWidth<=0 || innerHeight<=0)throw new ArgumentException("留白过大，纸张已无可用内容区域。");
                scale/=Math.Min(innerWidth/width,innerHeight/height);
            }
            var plan = new PlotPlan
            {
                MinX = bounds.MinX, MinY = bounds.MinY, MaxX = bounds.MaxX, MaxY = bounds.MaxY,
                PaperWidthMm = width, PaperHeightMm = height, ScaleDenominator = scale
            };
            if (!Finite(width) || !Finite(height) || !Finite(scale)
                || !Finite(plan.ContentWidthMm) || !Finite(plan.ContentHeightMm))
                throw new ArgumentException("打印参数超出有效数值范围。");
            double innerW=width-left-right,innerH=height-top-bottom;
            double contentFitTolerance = Math.Max(MediaToleranceMm, 1.5);
            if (plan.ContentWidthMm > innerW + contentFitTolerance
                || plan.ContentHeightMm > innerH + contentFitTolerance)
                throw new InvalidOperationException("图框在指定比例下超出纸张或留白区域，请核对图幅与比例。");

            // 若在绘图容差范围内微小超出（如工程制图常见的 1mm 绘图误差，如 891x421 匹配 A2+1/2 891x420），
            // 微调出图比例分母，确保内容完全落入可用纸张区域，避免介质放置与打印校验失败。
            if (plan.ContentWidthMm > innerW || plan.ContentHeightMm > innerH)
            {
                plan.ScaleDenominator = Math.Max(scale, Math.Max((bounds.MaxX - bounds.MinX) / innerW, (bounds.MaxY - bounds.MinY) / innerH));
            }

            plan.ContentLeftMm = Math.Max(left, left + (innerW - plan.ContentWidthMm) / 2);
            plan.ContentBottomMm = Math.Max(bottom, bottom + (innerH - plan.ContentHeightMm) / 2);
            if(!config.PrintOuterBorderLine && left==0 && right==0 && top==0 && bottom==0)
            {
                double inset=config.OuterBorderInsetMm*scale;
                double minX=plan.MinX+inset,minY=plan.MinY+inset,maxX=plan.MaxX-inset,maxY=plan.MaxY-inset;
                if(!Finite(inset) || minX<=plan.MinX || minY<=plan.MinY || maxX>=plan.MaxX || maxY>=plan.MaxY || maxX<=minX || maxY<=minY)
                    throw new ArgumentException("外边线裁切宽度过大或坐标精度不足，无法保留有效打印范围。");
                plan.MinX=minX;plan.MinY=minY;plan.MaxX=maxX;plan.MaxY=maxY;
                plan.ContentLeftMm+=config.OuterBorderInsetMm;plan.ContentBottomMm+=config.OuterBorderInsetMm;
                plan.AppliedBorderInsetMm=config.OuterBorderInsetMm;
            }
            return plan;
        }

        public static void ValidateBorderInset(double value)
        {
            if(!Finite(value) || value<0.01 || value>5)
                throw new ArgumentException("外边线裁切宽度必须为 0.01 到 5 毫米之间的有限数值。");
        }

        public static PlotMedia SelectMedia(PlotPlan plan, IEnumerable<PlotMedia> available)
        {
            PlotMedia? best = null;
            double bestArea = -1;
            foreach (var media in available)
            {
                bool direct = Same(media.WidthMm, plan.PaperWidthMm) && Same(media.HeightMm, plan.PaperHeightMm);
                bool rotated = Same(media.HeightMm, plan.PaperWidthMm) && Same(media.WidthMm, plan.PaperHeightMm);
                if (!direct && !rotated) continue;
                if(!CanPlace(plan,media))continue;
                double printableWidth=media.PrintableWidthMm,printableHeight=media.PrintableHeightMm;
                double area = printableWidth * printableHeight;
                if (area > bestArea) { bestArea = area; best = media; }
            }
            if (best == null)
                throw new InvalidOperationException($"设备没有匹配的纸张：{plan.PaperWidthMm:0.##}x{plan.PaperHeightMm:0.##}mm。");
            return best;
        }

        // 90°打印旋转时，介质坐标转为最终纸面坐标：(x,y) -> (y,W-x)。
        public static bool CanPlace(PlotPlan plan,PlotMedia media)
        {
            bool sizeMatches=(Same(media.WidthMm,plan.PaperWidthMm)&&Same(media.HeightMm,plan.PaperHeightMm))
                ||(Same(media.HeightMm,plan.PaperWidthMm)&&Same(media.WidthMm,plan.PaperHeightMm));
            if(!sizeMatches)return false;
            double left=media.PrintableLeftMm,bottom=media.PrintableBottomMm;
            double width=media.PrintableWidthMm,height=media.PrintableHeightMm;
            if(!Finite(left)||!Finite(bottom)||!Finite(width)||!Finite(height)||left<0||bottom<0||width<=0||height<=0
                ||left+width>media.WidthMm+MediaToleranceMm||bottom+height>media.HeightMm+MediaToleranceMm)return false;
            if(RotateMedia(plan,media))
            {double oldLeft=left;left=bottom;bottom=media.WidthMm-oldLeft-width;double oldWidth=width;width=height;height=oldWidth;}
            return plan.ContentLeftMm+MediaToleranceMm>=left && plan.ContentBottomMm+MediaToleranceMm>=bottom
                && plan.ContentLeftMm+plan.ContentWidthMm<=left+width+MediaToleranceMm
                && plan.ContentBottomMm+plan.ContentHeightMm<=bottom+height+MediaToleranceMm;
        }
        public static void Origin(PlotPlan plan,PlotMedia media,out double x,out double y)
        {
            if(!CanPlace(plan,media))throw new InvalidOperationException("目标内容位置超出驱动可打印区域。");
            double left=media.PrintableLeftMm,bottom=media.PrintableBottomMm;
            if(RotateMedia(plan,media)){left=media.PrintableBottomMm;bottom=media.WidthMm-media.PrintableLeftMm-media.PrintableWidthMm;}
            x=plan.ContentLeftMm-left;y=plan.ContentBottomMm-bottom;
        }
        public static bool RotateMedia(PlotPlan plan, PlotMedia media) =>
            !(Same(media.WidthMm, plan.PaperWidthMm) && Same(media.HeightMm, plan.PaperHeightMm));
        private static bool Same(double a, double b) => Finite(a) && Math.Abs(a - b) <= MediaToleranceMm;
        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
