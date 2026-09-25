using System;
using System.IO;

namespace FastBatchPlot.Core.Models
{
    public enum PlotExportFormat
    {
        PDF = 0,
        DWF = 1,
        PLT = 2,
        PNG = 3,
        JPG = 4,
        EPS = 5,
        SVG = 6
    }

    /// <summary>
    /// 批量打印全局配置参数
    /// </summary>
    public class PlotConfig
    {
        public string PrinterDevice { get; set; } = "DWG To PDF.pc3";
        public string PlotStyleTable { get; set; } = "monochrome.ctb";
        public string OutputDirectory { get; set; } = string.Empty;

        public bool SendToPrinter { get; set; }
        public int Copies { get; set; } = 1;

        public PlotExportFormat ExportFormat { get; set; } = PlotExportFormat.PDF;
        public bool MergeToSinglePdf { get; set; } = true;

        /// <summary>PDF 专用输出参数；字段为空时跟随当前 CAD 驱动预设。</summary>
        public PdfOutputOptions PdfOptions { get; set; } = new PdfOutputOptions();
        public string MergedFileName { get; set; } = "合并图纸.pdf";

        /// <summary>重新出图时覆盖同名成果文件（与原版批打印一致）；新文件先在临时路径校验，成功后才替换。</summary>
        public bool OverwriteExisting { get; set; } = true;

        /// <summary>
        /// 周边留白毫米数 (正值放大纸张，负值内容缩放，0为紧贴图框)
        /// </summary>
        public double MarginMm { get; set; } = 0.0;
        public PageMargins Margins { get; set; } = new PageMargins();

        /// <summary>
        /// 不留白时是否打印最外边线
        /// </summary>
        public bool PrintOuterBorderLine { get; set; } = true;
        /// <summary>关闭无留白外边线时，从打印窗口四边裁去的纸面宽度；不改变纸张、比例或内容位置。</summary>
        public double OuterBorderInsetMm { get; set; } = 0.25;

        /// <summary>
        /// 自动图纸方向 (横向/纵向自适应)
        /// </summary>
        public bool AutoOrientation { get; set; } = true;

        /// <summary>
        /// 排序规则
        /// </summary>
        public SortOrderRule SortRule { get; set; } = SortOrderRule.LeftToRight_TopToBottom;

        /// <summary>
        /// 文件命名规则模板，可用占位符：{Index}、{DwgNo}、{DwgName}、{PaperSize}、{Scale}、{Date}、{Rev}
        /// </summary>
        public string NamingTemplate { get; set; } = "{Index:D2}_{DwgNo}_{DwgName}";

        /// <summary>
        /// PDF 书签格式
        /// </summary>
        public string BookmarkTemplate { get; set; } = "{Index:D2} {DwgNo} {DwgName}";

        /// <summary>
        /// 打印签名
        /// </summary>
        public bool PrintSignatures { get; set; } = true;

        /// <summary>
        /// 打印印章
        /// </summary>
        public bool PrintStamps { get; set; } = true;
        public bool PrintPrimaryStamp {get;set;} = true;
        public bool PrintRegistrationStamp {get;set;} = true;
        public string RegistrationStampLibraryPath {get;set;} = "";
        public string RegistrationStampAssetId {get;set;} = "";
        public FastBatchPlot.Core.Assets.StampAsset? RegistrationStamp {get;set;}
        public FastBatchPlot.Core.Assets.StampUsePermit? RegistrationStampPermit {get;set;}
        public string StampLibraryPath {get;set;} = "";
        public string StampAssetId {get;set;} = "";
        public FastBatchPlot.Core.Assets.StampAsset? Stamp {get;set;}
        public FastBatchPlot.Core.Assets.StampUsePermit? StampPermit {get;set;}


        /// <summary>图纸中签名实体所在图层。关闭签名输出时仅在本次打印事务内禁用该层出图。</summary>
        public string SignatureLayerName { get; set; } = "MS_Sign";

        /// <summary>图纸中印章实体所在图层。关闭印章输出时仅在本次打印事务内禁用该层出图。</summary>
        public string StampLayerName { get; set; } = "MS_Stamp";

        public PlotConfig()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            OutputDirectory = Path.Combine(desktop, "CAD批打印输出");
        }
    }
}

