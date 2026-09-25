using System;
using System.IO;
using PdfSharpCore.Pdf.IO;

namespace FastBatchPlot.Core.Pdf
{
    public static class SinglePdfFile
    {
        public static string CreateTemporaryPath()
        {
            string directory = Path.Combine(Path.GetTempPath(), "FastBatchPlot", "SinglePdf", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "图纸.pdf");
        }

        public static void Validate(string path)
        {
            using (var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import))
                if (doc.PageCount != 1) throw new InvalidDataException("单张输出必须包含且仅包含一页 PDF。");
        }

        // 先完整复制并校验，再在目标目录内提交；不改写原始临时成果。
        public static void SaveCopy(string source, string destination, bool overwrite = false)
        {
            string from = Path.GetFullPath(source), target = Path.GetFullPath(destination);
            if (!string.Equals(Path.GetExtension(target), ".pdf", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("目标文件必须使用 .pdf 扩展名。");
            Validate(from);
            if (string.Equals(from, target, StringComparison.OrdinalIgnoreCase)) return;
            string directory = Path.GetDirectoryName(target)!;
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("保存目录不存在。");
            if (!overwrite && File.Exists(target)) throw new IOException("目标文件已存在，请另选文件名。");
            string staging = Path.Combine(directory, ".single-pdf-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.Copy(from, staging, false);
                Validate(staging);
                if (overwrite && File.Exists(target)) File.Replace(staging, target, null);
                else File.Move(staging, target);
            }
            finally { if (File.Exists(staging)) File.Delete(staging); }
        }
    }
}
