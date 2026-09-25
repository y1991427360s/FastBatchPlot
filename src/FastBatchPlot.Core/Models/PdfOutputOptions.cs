using System;
using System.Runtime.Serialization;

namespace FastBatchPlot.Core.Models
{
    /// <summary>PDF 输出参数。空值表示跟随当前 CAD 驱动预设，不覆盖宿主默认配置。</summary>
    [DataContract]
    public sealed class PdfOutputOptions
    {
        [DataMember(Name = "vectorResolutionDpi", Order = 1)]
        public int? VectorResolutionDpi { get; set; }

        [DataMember(Name = "rasterResolutionDpi", Order = 2)]
        public int? RasterResolutionDpi { get; set; }

        [DataMember(Name = "textToGeometry", Order = 3)]
        public bool? TextToGeometry { get; set; }

        [DataMember(Name = "includeLayers", Order = 4)]
        public bool? IncludeLayers { get; set; }

        [DataMember(Name = "mergeLines", Order = 5)]
        public bool? MergeLines { get; set; }

        public PdfOutputOptions Copy() => new PdfOutputOptions
        {
            VectorResolutionDpi = VectorResolutionDpi,
            RasterResolutionDpi = RasterResolutionDpi,
            TextToGeometry = TextToGeometry,
            IncludeLayers = IncludeLayers,
            MergeLines = MergeLines
        };

        public static PdfOutputOptions FollowDriver => new PdfOutputOptions();

        public bool HasOverrides => VectorResolutionDpi.HasValue || RasterResolutionDpi.HasValue ||
            TextToGeometry.HasValue || IncludeLayers.HasValue || MergeLines.HasValue;

        public void Validate()
        {
            if (VectorResolutionDpi.HasValue && Array.IndexOf(new[] { 300, 600, 1200, 2400 }, VectorResolutionDpi.Value) < 0)
                throw new ArgumentException("PDF 矢量分辨率无效。");
            if (RasterResolutionDpi.HasValue && Array.IndexOf(new[] { 150, 300, 400, 600, 1200 }, RasterResolutionDpi.Value) < 0)
                throw new ArgumentException("PDF 光栅分辨率无效。");
        }

        public override string ToString() =>
            $"矢量={Format(VectorResolutionDpi)};光栅={Format(RasterResolutionDpi)};文字转图形={Format(TextToGeometry)};图层={Format(IncludeLayers)};直线合并={Format(MergeLines)}";

        private static string Format(int? value) => value.HasValue ? value.Value + " DPI" : "跟随驱动";
        private static string Format(bool? value) => !value.HasValue ? "跟随驱动" : (value.Value ? "开启" : "关闭");
    }
}
