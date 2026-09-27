using System;
using System.IO;

namespace FastBatchPlot.Core.Pdf
{
    /// <summary>
    /// 单张 PDF 全流程诊断日志记录器，记录到 %LOCALAPPDATA%\FastBatchPlot\logs\single-pdf-flow.log
    /// 仅记录执行阶段与状态，不记录敏感图纸内容；每条日志即时 Flush。
    /// </summary>
    public static class SinglePdfTrace
    {
        private static readonly object _lock = new object();
        private const long MaxLogBytes = 2 * 1024 * 1024; // 2MB 滚动

        public static void Write(string stage, string? detail = null)
        {
            try
            {
                var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FastBatchPlot", "logs");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "single-pdf-flow.log");

                lock (_lock)
                {
                    if (File.Exists(path) && new FileInfo(path).Length > MaxLogBytes)
                    {
                        var backup = path + ".1";
                        if (File.Exists(backup)) File.Delete(backup);
                        File.Move(path, backup);
                    }

                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var tid = System.Threading.Thread.CurrentThread.ManagedThreadId;
                    var line = string.IsNullOrWhiteSpace(detail)
                        ? $"[{timestamp}] [TID:{tid}] {stage}"
                        : $"[{timestamp}] [TID:{tid}] {stage} - {detail}";

                    using (var writer = new StreamWriter(path, true, System.Text.Encoding.UTF8))
                    {
                        writer.WriteLine(line);
                        writer.Flush();
                    }
                }
            }
            catch
            {
                // 诊断日志不能阻断业务流程
            }
        }
    }
}
