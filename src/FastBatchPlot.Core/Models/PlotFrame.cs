using System;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Templates;

namespace FastBatchPlot.Core.Models
{
    public enum FrameType
    {
        ClosedPolyline = 0,
        BlockReference = 1,
        PickWindow = 2
    }

    /// <summary>
    /// 识别到的CAD图框对象
    /// </summary>
    public class PlotFrame
    {
        public TemplateRegion? RegistrationStampRegion {get;set;}
        public FastBatchPlot.Core.Assets.StampPlacement? RegistrationStampPlacement {get;set;}
        public TemplateRegion? StampRegion {get;set;}
        public FastBatchPlot.Core.Assets.StampPlacement? StampPlacement {get;set;}
        public TemplateRegion? AppliedPrintRegion {get;set;}
        public Rect2D? PrintBounds {get;set;}
        public int Id { get; set; }
        public int OrderIndex { get; set; }

        // CAD 图纸空间坐标 (包围盒)
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }

        public double Width => Math.Abs(MaxX - MinX);
        public double Height => Math.Abs(MaxY - MinY);
        public double CenterX => (MinX + MaxX) / 2.0;
        public double CenterY => (MinY + MaxY) / 2.0;

        // 旋转角度 (0, 90, 180, 270)
        public double RotationDegrees { get; set; } = 0.0;

        // 图框类型与来源
        public FrameType Type { get; set; } = FrameType.ClosedPolyline;
        public string SourceBlockName { get; set; } = string.Empty;
        public string SourceLayer { get; set; } = string.Empty;
        public string HandleOrId { get; set; } = string.Empty;
        public string LayoutName { get; set; } = "Model";
        public int LayoutOrder { get; set; }
        public string TemplateId { get; set; } = string.Empty;
        public int TemplatePriority { get; set; } = 100;
        public string SourceDocumentId { get; set; } = string.Empty;
        public string SourceFileName { get; set; } = string.Empty;
        public string SourceLayoutId { get; set; } = string.Empty;

        // 智能匹配计算结果
        public PaperSize DetectedPaper { get; set; } = new PaperSize("A1", 841, 594, true);
        public double CalculatedScale { get; set; } = 100.0; // 例如 1:100 即为 100
        public bool IsLandscape { get; set; } = true;

        // 标题栏信息
        public TitleBlockInfo TitleInfo { get; set; } = new TitleBlockInfo();

        // 输出文件设置
        public string CustomOutputFileName { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = true;
        public string Status { get; set; } = "待打印";
        public string? ErrorMessage { get; set; }

        public override string ToString()
        {
            var title = string.IsNullOrWhiteSpace(TitleInfo.DrawingName) ? $"图框#{OrderIndex}" : TitleInfo.DrawingName;
            return $"{OrderIndex:D2}: {title} [{DetectedPaper.Name}, 1:{CalculatedScale:0.##}]";
        }
    }
}
