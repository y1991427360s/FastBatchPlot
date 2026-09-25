using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml;
using FastBatchPlot.Core.Models;
using SixLabors.ImageSharp;

namespace FastBatchPlot.Core.Planning
{
    /// <summary>文件类型独立校验。非 PDF 不宣称已核验工程比例或全部绘图内容。</summary>
    public static class PlotFileFormats
    {
        public static string Extension(PlotExportFormat format)
        {
            switch (format)
            {
                case PlotExportFormat.PDF: return ".pdf";
                case PlotExportFormat.DWF: return ".dwf";
                case PlotExportFormat.PLT: return ".plt";
                case PlotExportFormat.PNG: return ".png";
                case PlotExportFormat.JPG: return ".jpg";
                case PlotExportFormat.EPS: return ".eps";
                case PlotExportFormat.SVG: return ".svg";
                default: throw new ArgumentException("未知输出格式。");
            }
        }
        public static string CreateTemporaryPath(string target, PlotExportFormat format)
        {
            if (!string.Equals(Path.GetExtension(target), Extension(format), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("文件扩展名与所选格式不一致。");
            string directory = Path.GetDirectoryName(Path.GetFullPath(target))!;
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, ".batchplot-" + Guid.NewGuid().ToString("N") + Extension(format));
        }
        public static void ValidateAndCommit(string temporary, string target, PlotExportFormat format, PlotPlan plan, bool overwrite = false)
        {
            if (!string.Equals(Path.GetExtension(target), Extension(format), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("文件扩展名与所选格式不一致。");
            if (format == PlotExportFormat.PDF) { PlotOutputCommitter.ValidateAndCommit(temporary, target, plan, overwrite); return; }
            if (!File.Exists(temporary) || new FileInfo(temporary).Length == 0) throw new IOException("打印驱动未生成新的输出文件。");
            switch (format)
            {
                case PlotExportFormat.PNG:
                case PlotExportFormat.JPG:
                    ValidateImage(temporary, format, plan); break;
                case PlotExportFormat.DWF:
                    ValidateDwf(temporary); break;
                case PlotExportFormat.SVG:
                    ValidateSvg(temporary); break;
                case PlotExportFormat.EPS:
                    string eps = ReadPrefix(temporary, 1024 * 1024);
                    if (!eps.StartsWith("%!PS-Adobe-", StringComparison.Ordinal) || !eps.Contains("EPSF-")
                        || !Regex.IsMatch(eps, @"%%BoundingBox:\s*(-?\d+\s+){3}-?\d+"))
                        throw new InvalidDataException("输出不是包含边界信息的 EPS 文件。");
                    break;
                case PlotExportFormat.PLT:
                    string plt = ReadPrefix(temporary, 1024 * 1024);
                    if (!Regex.IsMatch(plt, @"(?:^|;|\x1b%[-0-9]*[BA])\s*(?:IN|BP|DF|SP)[0-9 ,]*;", RegexOptions.IgnoreCase)
                        || !Regex.IsMatch(plt, @"(?:PA|PR|PD|PU|PE)[-+0-9 ,.]", RegexOptions.IgnoreCase))
                        throw new InvalidDataException("输出未识别为包含绘图指令的 HP-GL/2 PLT，未提交文件。");
                    break;
                default: throw new ArgumentException("未知输出格式。");
            }
            PlotOutputCommitter.Commit(temporary, target, overwrite);
        }
        private static void ValidateImage(string path, PlotExportFormat format, PlotPlan plan)
        {
            using(FastBatchPlot.Core.Common.LegacyDependencyResolution.BeginImageOperation())ValidateImageCore(path,format,plan);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ValidateImageCore(string path,PlotExportFormat format,PlotPlan plan)
        {
            var info = Image.Identify(path, out var actual);
            string expected = format == PlotExportFormat.PNG ? "PNG" : "JPEG";
            if (info == null || !string.Equals(actual?.Name, expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("驱动生成的图片类型与所选格式不一致。");
            if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > 150000000)
                throw new InvalidDataException("图片尺寸无效或超过一亿五千万像素校验上限。");
            double aspect = (double)info.Width / info.Height, planned = plan.PaperWidthMm / plan.PaperHeightMm;
            if (Math.Abs(aspect / planned - 1) > 0.015)
                throw new InvalidDataException("生成图片的宽高比与打印计划不一致。");
            // 完整解码，避免只通过文件头的截断文件混入成果。
            using (var decoded = Image.Load(path))
                if (decoded.Frames.Count != 1) throw new InvalidDataException("单图框图片必须只有一帧。");
        }
        private static void ValidateDwf(string path)
        {
            string header = ReadPrefix(path, 32);
            if (!header.StartsWith("(DWF V", StringComparison.Ordinal)) throw new InvalidDataException("输出不是 DWF 文件。");
            // DWF6 为 ZIP 容器（有 DWF 前导），ZipArchive 按中央目录定位。
            using (var stream = File.OpenRead(path))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var manifest = archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, "manifest.xml", StringComparison.OrdinalIgnoreCase));
                if (manifest == null || archive.Entries.Count < 2 || manifest.Length > 8 * 1024 * 1024)
                    throw new InvalidDataException("DWF 缺少有效清单或内容资源；当前仅校验 DWF6 容器。");
                using (var input = manifest.Open())
                using (var reader = XmlReader.Create(input, XmlSettings())) { while (reader.Read()) { } }
                long total = 0;
                var buffer = new byte[65536];
                foreach (var entry in archive.Entries)
                {
                    total += entry.Length;
                    if (entry.Length < 0 || total > 512L * 1024 * 1024) throw new InvalidDataException("DWF 解压内容超过校验上限。");
                    using (var input = entry.Open()) { int count; long read = 0; while ((count = input.Read(buffer,0,buffer.Length)) > 0) read += count;
                        if (read != entry.Length) throw new InvalidDataException("DWF 内容被截断。"); }
                }
            }
        }
        private static void ValidateSvg(string path)
        {
            bool root = false, drawing = false;
            using (var reader = XmlReader.Create(path, XmlSettings()))
            {
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element) continue;
                    if (!root)
                    {
                        if (reader.LocalName != "svg" || reader.NamespaceURI != "http://www.w3.org/2000/svg")
                            throw new InvalidDataException("输出 XML 不是 SVG。");
                        root = true;
                    }
                    if (new[] { "path", "line", "polyline", "polygon", "rect", "circle", "ellipse", "text", "image", "use" }.Contains(reader.LocalName)) drawing = true;
                }
            }
            if (!root || !drawing) throw new InvalidDataException("SVG 缺少图形内容。");
        }
        private static XmlReaderSettings XmlSettings() => new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 64L*1024*1024 };
        private static string ReadPrefix(string path, int maximum)
        {
            using (var stream = File.OpenRead(path))
            {
                var data = new byte[(int)Math.Min(stream.Length, maximum)];
                int offset = 0, count;
                while (offset < data.Length && (count = stream.Read(data, offset, data.Length-offset)) > 0) offset += count;
                return Encoding.ASCII.GetString(data,0,offset);
            }
        }
    }
}
