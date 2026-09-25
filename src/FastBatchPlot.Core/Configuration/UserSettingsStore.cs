using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Xml;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Configuration
{
    /// <summary>版本化用户设置存储。读取错误必须向用户报告，不自动用默认值覆盖原文件。</summary>
    public static class UserSettingsStore
    {
        private const long MaximumFileBytes = 1024 * 1024;
        private static readonly object Gate = new object();

        /// <summary>文件缺失返回默认设置；内容损坏或版本不支持时抛出 InvalidDataException。</summary>
        public static BatchPlotPreferences Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("设置文件路径不能为空。", nameof(path));
            lock (Gate)
            {
                try
                {
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (stream.Length > MaximumFileBytes) throw new InvalidDataException("用户设置文件过大，请检查文件内容。");
                        using (var reader = JsonReaderWriterFactory.CreateJsonReader(stream, XmlDictionaryReaderQuotas.Max))
                        {
                            var preferences = new DataContractJsonSerializer(typeof(BatchPlotPreferences))
                                .ReadObject(reader) as BatchPlotPreferences;
                            if (preferences == null) throw new InvalidDataException("用户设置文件不能为 null。");
                            // 继续读取到末尾，避免把合法对象之后的垃圾内容当作有效配置。
                            while (reader.Read())
                                if (reader.NodeType != XmlNodeType.Whitespace && reader.NodeType != XmlNodeType.SignificantWhitespace)
                                    throw new InvalidDataException("用户设置文件包含多余内容。");
                            Validate(preferences);
                            return preferences;
                        }
                    }
                }
                catch (FileNotFoundException) { return new BatchPlotPreferences(); }
                catch (DirectoryNotFoundException) { return new BatchPlotPreferences(); }
                catch (SerializationException ex) { throw InvalidFile(ex); }
                catch (XmlException ex) { throw InvalidFile(ex); }
                catch (ArgumentException ex) { throw InvalidFile(ex); }
            }
        }

        /// <summary>先验证旧文件，再在同目录写临时文件并原子替换；不覆盖无效或未来版本的设置。</summary>
        public static void Save(string path, BatchPlotPreferences preferences)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("设置文件路径不能为空。", nameof(path));
            if (preferences == null) throw new ArgumentNullException(nameof(preferences));
            var snapshot = Snapshot(preferences);
            Validate(snapshot);
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath)!;
            lock (Gate)
            {
                // Load 只把文件缺失当默认值；访问拒绝、损坏及未来版本都中止保存。
                Load(fullPath);
                Directory.CreateDirectory(directory);
                string temporaryPath = Path.Combine(directory, ".batchplot-settings-" + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        new DataContractJsonSerializer(typeof(BatchPlotPreferences)).WriteObject(stream, snapshot);
                        if (stream.Length > MaximumFileBytes) throw new InvalidDataException("用户设置内容过大，未写入设置文件。");
                        stream.Flush(true);
                    }
                    if (File.Exists(fullPath)) File.Replace(temporaryPath, fullPath, null);
                    else File.Move(temporaryPath, fullPath);
                }
                finally
                {
                    // 不用删除旧文件再移动的降级方案，替换失败时保留旧设置。
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
            }
        }

        private static void Validate(BatchPlotPreferences value)
        {
            if (value.SchemaVersion != BatchPlotPreferences.CurrentSchemaVersion)
                throw new InvalidDataException($"不支持用户设置版本 {value.SchemaVersion}，当前支持版本为 {BatchPlotPreferences.CurrentSchemaVersion}；已保留原文件。");
            if (value.PrinterDevice == null || value.PlotStyleTable == null || value.OutputDirectory == null ||
                value.NamingTemplate == null || value.MergedFileName == null)
                throw new InvalidDataException("用户设置文本字段不能为 null。");
            try { Naming.DrawingNameFormatter.ValidateBookmarkTemplate(value.BookmarkTemplate); }
            catch (ArgumentException ex) { throw new InvalidDataException(ex.Message, ex); }
            if(value.StampLibraryPath==null || value.StampLibraryPath.Length>2048 || value.StampAssetId==null ||
                (value.StampAssetId.Length>0 && (!Guid.TryParseExact(value.StampAssetId,"N",out _) || string.IsNullOrWhiteSpace(value.StampLibraryPath))))
                throw new InvalidDataException("印章库路径或印章 ID 无效。");
            if(value.RegistrationStampLibraryPath==null || value.RegistrationStampLibraryPath.Length>2048 || value.RegistrationStampAssetId==null ||
                (value.RegistrationStampAssetId.Length>0 && (!Guid.TryParseExact(value.RegistrationStampAssetId,"N",out _) || string.IsNullOrWhiteSpace(value.RegistrationStampLibraryPath))))
                throw new InvalidDataException("注册章库路径或印章 ID 无效。");
            try { Planning.PlotLayerVisibility.Validate(value.SignatureLayerName, value.StampLayerName); }
            catch (ArgumentException ex) { throw new InvalidDataException(ex.Message, ex); }
            if(value.Catalog==null)throw new InvalidDataException("目录设置不能为空。");
            if(value.PdfOptions==null) throw new InvalidDataException("PDF 参数不能为空。");
            ValidatePdfOptions(value.PdfOptions);
            try{value.Catalog.Validate();}catch(ArgumentException ex){throw new InvalidDataException(ex.Message,ex);}
            if (value.Margins == null) throw new InvalidDataException("留白设置不能为空。");
            try { value.Margins.Validate(); } catch (ArgumentException ex) { throw new InvalidDataException(ex.Message,ex); }
            if (!InRange(value.MarginMm, -50, 100)) throw new InvalidDataException("留白必须为 -50 到 100 毫米之间的有限数值。");
            try { Planning.PlotPlanBuilder.ValidateBorderInset(value.OuterBorderInsetMm); }
            catch(ArgumentException ex){throw new InvalidDataException(ex.Message,ex);}
            if (!InRange(value.MinimumAreaPercent, 0, 100)) throw new InvalidDataException("面积阈值必须为 0 到 100 之间的有限百分比。");
            if(!InRange(value.DetectionScale,0,1000000))throw new InvalidDataException("识别比例必须为 0（自动）或不超过 1000000 的有限正数。");
            if (!Enum.IsDefined(typeof(PlotExportFormat), value.ExportFormat) || value.Copies < 1 || value.Copies > 999)
                throw new InvalidDataException("输出格式或份数无效。");
            if (value.SortRuleIndex < 0 || value.SortRuleIndex > 4) throw new InvalidDataException("排序索引必须在 0 到 4 之间。");
        }

        private static bool InRange(double value, double minimum, double maximum) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= minimum && value <= maximum;

        private static void ValidatePdfOptions(PdfOutputOptions value)
        {
            try { value.Validate(); } catch (ArgumentException ex) { throw new InvalidDataException(ex.Message, ex); }
        }

        private static InvalidDataException InvalidFile(Exception exception) =>
            new InvalidDataException("用户设置文件格式无效，已保留原文件。", exception);

        private static BatchPlotPreferences Snapshot(BatchPlotPreferences value) => new BatchPlotPreferences
        {
            SchemaVersion = value.SchemaVersion, Catalog = value.Catalog?.Copy()!,
            StampLibraryPath=value.StampLibraryPath, StampAssetId=value.StampAssetId,
            PrintPrimaryStamp=value.PrintPrimaryStamp,PrintRegistrationStamp=value.PrintRegistrationStamp,
            RegistrationStampLibraryPath=value.RegistrationStampLibraryPath,RegistrationStampAssetId=value.RegistrationStampAssetId,
            PrintSignatures = value.PrintSignatures, PrintStamps = value.PrintStamps,
            SignatureLayerName = value.SignatureLayerName, StampLayerName = value.StampLayerName,
            PrinterDevice = value.PrinterDevice,
            PlotStyleTable = value.PlotStyleTable,
            OutputDirectory = value.OutputDirectory,
            NamingTemplate = value.NamingTemplate, BookmarkTemplate = value.BookmarkTemplate,
            MergedFileName = value.MergedFileName,
            MergeToSinglePdf = value.MergeToSinglePdf,
            MarginMm = value.MarginMm, Margins = value.Margins?.Copy()!,
            PrintOuterBorderLine=value.PrintOuterBorderLine,OuterBorderInsetMm=value.OuterBorderInsetMm,
            MinimumAreaPercent = value.MinimumAreaPercent,
            RemoveDuplicates=value.RemoveDuplicates,RemoveNestedFrames=value.RemoveNestedFrames,
            DetectionScale=value.DetectionScale,
            SortRuleIndex = value.SortRuleIndex, ExportFormat = value.ExportFormat, SendToPrinter = value.SendToPrinter, Copies = value.Copies
            ,PdfOptions = value.PdfOptions?.Copy()!
        };
    }
}

