using System;
using System.IO;
using System.Runtime.Serialization;
using FastBatchPlot.Core.Export;

namespace FastBatchPlot.Core.Models
{
    /// <summary>批打印识别模式：通用型（自动识别所有纸张）与图框型（仅识别已导入图框模板）</summary>
    public enum PlotDetectMode
    {
        Universal = 0,
        Template = 1
    }

    /// <summary>用户偏好，只保存跨图纸可复用的设置，不保存图框、文档身份或正在执行的任务。</summary>
    [DataContract]
    public sealed class BatchPlotPreferences
    {
        public const int CurrentSchemaVersion = 1;
        [DataMember(Name="overwriteExisting",Order=33)] public bool OverwriteExisting {get;set;} = true;
        [DataMember(Name="openOutputWhenDone",Order=34)] public bool OpenOutputWhenDone {get;set;} = true;
        [DataMember(Name="detectMode",Order=32)] public PlotDetectMode DetectMode {get;set;} = PlotDetectMode.Universal;
        [DataMember(Name="removeDuplicates",Order=30)] public bool RemoveDuplicates {get;set;} = true;
        [DataMember(Name="removeNestedFrames",Order=31)] public bool RemoveNestedFrames {get;set;} = true;
        [DataMember(Name="detectionScale",Order=29)] public double DetectionScale {get;set;}
        [DataMember(Name="printOuterBorderLine",Order=27)] public bool PrintOuterBorderLine {get;set;} = true;
        [DataMember(Name="outerBorderInsetMm",Order=28)] public double OuterBorderInsetMm {get;set;} = 0.25;
        [DataMember(Name="catalog",Order=14)] public CatalogOptions Catalog {get;set;} = new CatalogOptions();
        [DataMember(Name="printSignatures",Order=15)] public bool PrintSignatures {get;set;} = true;
        [DataMember(Name="printStamps",Order=16)] public bool PrintStamps {get;set;} = true;
        [DataMember(Name = "margins", Order = 13)] public PageMargins Margins { get; set; } = new PageMargins();
        [DataMember(Name = "exportFormat", Order = 10)] public PlotExportFormat ExportFormat { get; set; }
        [DataMember(Name = "sendToPrinter", Order = 11)] public bool SendToPrinter { get; set; }
        [DataMember(Name = "copies", Order = 12)] public int Copies { get; set; } = 1;
        [DataMember(Name="signatureLayerName",Order=17)] public string SignatureLayerName {get;set;} = "MS_Sign";
        [DataMember(Name="stampLayerName",Order=18)] public string StampLayerName {get;set;} = "MS_Stamp";
        [DataMember(Name="stampLibraryPath",Order=19)] public string StampLibraryPath {get;set;} = "";
        [DataMember(Name="stampAssetId",Order=20)] public string StampAssetId {get;set;} = "";
        [DataMember(Name="printPrimaryStamp",Order=21)] public bool PrintPrimaryStamp {get;set;} = true;
        [DataMember(Name="printRegistrationStamp",Order=22)] public bool PrintRegistrationStamp {get;set;} = true;
        [DataMember(Name="registrationStampLibraryPath",Order=23)] public string RegistrationStampLibraryPath {get;set;} = "";
        [DataMember(Name="registrationStampAssetId",Order=24)] public string RegistrationStampAssetId {get;set;} = "";
        [DataMember(Name="bookmarkTemplate",Order=25)] public string BookmarkTemplate {get;set;} = Naming.DrawingNameFormatter.DefaultBookmarkTemplate;
        [DataMember(Name="pdfOptions",Order=26)] public PdfOutputOptions PdfOptions { get; set; } = new PdfOutputOptions();
        [OnDeserializing] private void BeforeDeserializing(StreamingContext context) { OverwriteExisting=true; OpenOutputWhenDone=true; DetectMode=PlotDetectMode.Universal; RemoveDuplicates=true;RemoveNestedFrames=true; PrintOuterBorderLine=true;OuterBorderInsetMm=0.25; BookmarkTemplate=Naming.DrawingNameFormatter.DefaultBookmarkTemplate; PdfOptions = new PdfOutputOptions(); PrintPrimaryStamp=true; PrintRegistrationStamp=true; RegistrationStampLibraryPath=""; RegistrationStampAssetId=""; Copies = 1; Margins = new PageMargins(); Catalog = new CatalogOptions(); PrintSignatures = true; PrintStamps = true; SignatureLayerName = "MS_Sign"; StampLayerName = "MS_Stamp"; StampLibraryPath = ""; StampAssetId = ""; }


        [DataMember(Name = "schemaVersion", Order = 0, IsRequired = true)]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        [DataMember(Name = "printerDevice", Order = 1, IsRequired = true)]
        public string PrinterDevice { get; set; } = "DWG To PDF.pc3";

        [DataMember(Name = "plotStyleTable", Order = 2, IsRequired = true)]
        public string PlotStyleTable { get; set; } = "monochrome.ctb";

        [DataMember(Name = "outputDirectory", Order = 3, IsRequired = true)]
        public string OutputDirectory { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "CAD批打印输出");

        [DataMember(Name = "namingTemplate", Order = 4, IsRequired = true)]
        public string NamingTemplate { get; set; } = "{Index:D2}_{DwgNo}_{DwgName}";

        [DataMember(Name = "mergedFileName", Order = 5, IsRequired = true)]
        public string MergedFileName { get; set; } = "施工图图纸合集.pdf";

        [DataMember(Name = "mergeToSinglePdf", Order = 6, IsRequired = true)]
        public bool MergeToSinglePdf { get; set; } = true;

        [DataMember(Name = "marginMm", Order = 7, IsRequired = true)]
        public double MarginMm { get; set; }

        [DataMember(Name = "minimumAreaPercent", Order = 8, IsRequired = true)]
        public double MinimumAreaPercent { get; set; }

        [DataMember(Name = "sortRuleIndex", Order = 9, IsRequired = true)]
        public int SortRuleIndex { get; set; }
    }
}

