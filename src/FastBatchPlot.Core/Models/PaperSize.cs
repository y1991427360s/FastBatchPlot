using System;
using System.Collections.Generic;

namespace FastBatchPlot.Core.Models
{
    /// <summary>
    /// 标准图纸幅面与尺寸定义 (ISO 216 及工程延伸图幅)
    /// </summary>
    public class PaperSize
    {
        public string Name { get; set; } = string.Empty;
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public bool IsLandscape { get; set; }
        public string StandardName { get; set; } = string.Empty;

        public PaperSize() { }

        public PaperSize(string name, double widthMm, double heightMm, bool isLandscape = true)
        {
            Name = name;
            WidthMm = widthMm;
            HeightMm = heightMm;
            IsLandscape = isLandscape;
            StandardName = name;
        }

        public double LongerEdgeMm => Math.Max(WidthMm, HeightMm);
        public double ShorterEdgeMm => Math.Min(WidthMm, HeightMm);
        public double AspectRatio => LongerEdgeMm / Math.Max(1.0, ShorterEdgeMm);

        public PaperSize Clone() => new PaperSize(Name, WidthMm, HeightMm, IsLandscape) { StandardName = this.StandardName };

        public PaperSize CloneRotated()
        {
            return new PaperSize(Name, HeightMm, WidthMm, !IsLandscape)
            {
                StandardName = this.StandardName
            };
        }

        public override string ToString()
        {
            return $"{Name} ({WidthMm:0}x{HeightMm:0}mm)";
        }

        /// <summary>
        /// 获取所有内置标准及加长工程图纸幅面 (默认均为横向定义)
        /// </summary>
        // 每次提供独立对象，调用方编辑图幅不会污染其他图纸和标准目录。
        public static IReadOnlyList<PaperSize> StandardSizes => new List<PaperSize>
        {
            // ISO 216 标准图幅
            new PaperSize("A0", 1189, 841, true),
            new PaperSize("A1", 841, 594, true),
            new PaperSize("A2", 594, 420, true),
            new PaperSize("A3", 420, 297, true),
            new PaperSize("A4", 297, 210, true),

            // 加长图幅：按 GB/T 50001 短边不变、长边按 1/4、1/2 递增（A3+1=841、A2+1=1189），
            // 另含原版图框配置中常用的 1/8 级加长及 A3+1/4、A3+3/4。
            // A0 加长图幅
            new PaperSize("A0+1/8", 1338, 841, true),
            new PaperSize("A0+1/4", 1486, 841, true),
            new PaperSize("A0+3/8", 1635, 841, true),
            new PaperSize("A0+1/2", 1783, 841, true),
            new PaperSize("A0+5/8", 1932, 841, true),
            new PaperSize("A0+3/4", 2080, 841, true),
            new PaperSize("A0+7/8", 2229, 841, true),
            new PaperSize("A0+1", 2378, 841, true),
            // A1 加长图幅
            new PaperSize("A1+1/8", 946, 594, true),
            new PaperSize("A1+1/4", 1051, 594, true),
            new PaperSize("A1+3/8", 1156, 594, true),
            new PaperSize("A1+1/2", 1261, 594, true),
            new PaperSize("A1+5/8", 1366, 594, true),
            new PaperSize("A1+3/4", 1471, 594, true),
            new PaperSize("A1+7/8", 1577, 594, true),
            new PaperSize("A1+1", 1682, 594, true),
            new PaperSize("A1+5/4", 1892, 594, true),
            new PaperSize("A1+3/2", 2102, 594, true),
            // A2 加长图幅
            new PaperSize("A2+1/8", 668, 420, true),
            new PaperSize("A2+1/4", 743, 420, true),
            new PaperSize("A2+3/8", 817, 420, true),
            new PaperSize("A2+1/2", 891, 420, true),
            new PaperSize("A2+5/8", 966, 420, true),
            new PaperSize("A2+3/4", 1041, 420, true),
            new PaperSize("A2+7/8", 1115, 420, true),
            new PaperSize("A2+1", 1189, 420, true),
            new PaperSize("A2+5/4", 1338, 420, true),
            new PaperSize("A2+3/2", 1486, 420, true),
            new PaperSize("A2+7/4", 1635, 420, true),
            new PaperSize("A2+2", 1783, 420, true),
            new PaperSize("A2+9/4", 1932, 420, true),
            new PaperSize("A2+5/2", 2080, 420, true),
            // A3 加长图幅
            new PaperSize("A3+1/4", 525, 297, true),
            new PaperSize("A3+1/2", 630, 297, true),
            new PaperSize("A3+3/4", 735, 297, true),
            new PaperSize("A3+1", 841, 297, true),
            new PaperSize("A3+3/2", 1051, 297, true),
            new PaperSize("A3+2", 1261, 297, true),
            new PaperSize("A3+5/2", 1471, 297, true),
            new PaperSize("A3+3", 1682, 297, true),
            new PaperSize("A3+7/2", 1892, 297, true),
        };
    }
}
