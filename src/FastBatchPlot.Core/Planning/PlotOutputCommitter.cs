using System;
using System.Collections.Generic;
using System.IO;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace FastBatchPlot.Core.Planning
{
    /// <summary>每次打印使用新临时路径，校验真实 PDF 后才提交，旧文件不能冒充成功。</summary>
    public static class PlotOutputCommitter
    {
        public static string CreateTemporaryPath(string outputPath)
        {
            if (!string.Equals(Path.GetExtension(outputPath), ".pdf", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("PDF 输出文件必须使用 .pdf 扩展名。");
            string fullPath = Path.GetFullPath(outputPath);
            string directory = Path.GetDirectoryName(fullPath)!;
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, ".batchplot-" + Guid.NewGuid().ToString("N") + ".pdf");
        }

        public static void ValidateAndCommit(string temporaryPath, string outputPath, PlotPlan plan, bool overwrite = false)
        {
            Validate(temporaryPath, plan);
            Commit(temporaryPath, outputPath, overwrite);
        }

        /// <summary>校验通过后才替换目标；不允许覆盖时目标已存在即失败。被占用时给出可操作的提示。</summary>
        public static void Commit(string temporaryPath, string outputPath, bool overwrite)
        {
            string target = Path.GetFullPath(outputPath);
            if (Directory.Exists(target)) throw new IOException("输出路径是已有文件夹：" + target);
            try
            {
                if (overwrite && File.Exists(target)) File.Replace(temporaryPath, target, null, true);
                else File.Move(temporaryPath, target);
            }
            catch (IOException ex) when (overwrite && File.Exists(target))
            {
                throw new IOException("无法覆盖输出文件，可能正在被 PDF 阅读器或其他程序打开：" + target, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new IOException("无法写入输出文件（只读或无权限，或正被其他程序打开）：" + target, ex);
            }
        }

        public static void Validate(string temporaryPath, PlotPlan plan)
        {
            if (!File.Exists(temporaryPath) || new FileInfo(temporaryPath).Length == 0)
                throw new IOException("CAD 未生成新的 PDF 文件，打印不能视为成功。");
            using (var pdf = PdfReader.Open(temporaryPath, PdfDocumentOpenMode.Import))
            {
                if (pdf.PageCount != 1) throw new IOException("单图框 PDF 页数不为 1。");
                var page = pdf.Pages[0];
                // UserUnit 不属于 PDF 可继承页面属性；缺省为 1，不能从 /Pages 读取。
                if (page.Elements.ContainsKey("/UserUnit") && page.Elements.GetReal("/UserUnit") != 1.0)
                    throw new IOException("PDF 使用了非标准 UserUnit，无法按计划确认实际纸面尺寸。");
                var mediaOwner = FindInheritedOwner(page, "/MediaBox")
                    ?? throw new IOException("PDF 页面缺少 MediaBox。");
                var media = mediaOwner.Elements.GetRectangle("/MediaBox");
                ValidateBox(media, "MediaBox");
                var cropOwner = FindInheritedOwner(page, "/CropBox");
                if (cropOwner != null)
                {
                    var crop = cropOwner.Elements.GetRectangle("/CropBox");
                    ValidateBox(crop, "CropBox");
                    // 容忍驱动转 PDF 时的微小舍入误差
                    const double tolerancePoints = PlotPlanBuilder.MediaToleranceMm * 72.0 / 25.4;
                    if (crop.X1 > media.X1 + tolerancePoints || crop.Y1 > media.Y1 + tolerancePoints
                        || crop.X2 < media.X2 - tolerancePoints || crop.Y2 < media.Y2 - tolerancePoints)
                        throw new IOException("PDF 的 CropBox 裁切了纸张范围，未提交可能缺失图纸内容的文件。");
                }
                double width = media.Width * 25.4 / 72.0;
                double height = media.Height * 25.4 / 72.0;
                var rotationOwner = FindInheritedOwner(page, "/Rotate");
                int rotation = rotationOwner?.Elements.GetInteger("/Rotate") ?? 0;
                if (rotation % 90 != 0) throw new IOException("PDF 页面旋转角度不是 90 度的整数倍。");
                if (rotation % 180 != 0) { double temp = width; width = height; height = temp; }
                if (Math.Abs(width - plan.PaperWidthMm) > PlotPlanBuilder.MediaToleranceMm
                    || Math.Abs(height - plan.PaperHeightMm) > PlotPlanBuilder.MediaToleranceMm)
                    throw new IOException($"生成 PDF 的尺寸 {width:0.###} × {height:0.###} mm 与打印计划不一致。");
            }
        }

        private static PdfDictionary? FindInheritedOwner(PdfDictionary page, string key)
        {
            var visited = new HashSet<PdfDictionary>();
            for (PdfDictionary? current = page; current != null; current = current.Elements.GetDictionary("/Parent"))
            {
                if (!visited.Add(current) || visited.Count > 128)
                    throw new IOException("PDF 页面继承结构循环或层级过深。");
                if (current.Elements.ContainsKey(key)) return current;
            }
            return null;
        }

        private static void ValidateBox(PdfRectangle box, string name)
        {
            foreach (double value in new[] { box.X1, box.Y1, box.X2, box.Y2, box.Width, box.Height })
                if (double.IsNaN(value) || double.IsInfinity(value)) throw new IOException("PDF " + name + " 坐标无效。");
            if (box.Width <= 0 || box.Height <= 0) throw new IOException("PDF " + name + " 宽高必须大于零。");
        }
    }
}
