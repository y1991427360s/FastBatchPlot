using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FastBatchPlot.Core.Pdf;

namespace FastBatchPlot.Cli
{
    internal class Program
    {
        static int Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("==================================================");
            Console.WriteLine(" FastBatchPlot PDF Merger & Processor (Open Edition)");
            Console.WriteLine("==================================================");

            if (args.Length == 0)
            {
                string legacyTmPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MSteel", "plot2.tm");
                if (File.Exists(legacyTmPath))
                {
                    Console.WriteLine($"[兼容模式] 检测到旧版 MSteel plot2.tm 任务文件: {legacyTmPath}");
                    return ProcessLegacyTmFile(legacyTmPath);
                }

                PrintHelp();
                return 0;
            }

            string? outputFile = null;
            var mergeItems = new List<PdfMergeItem>();
            string? rotateFile = null;
            int rotateAngle = 0;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLowerInvariant();
                if ((arg == "-o" || arg == "--output") && i + 1 < args.Length)
                {
                    outputFile = args[++i];
                }
                else if ((arg == "-i" || arg == "--input") && i + 1 < args.Length)
                {
                    string filePath = args[++i];
                    string title = Path.GetFileNameWithoutExtension(filePath);
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        title = args[++i];
                    }
                    mergeItems.Add(new PdfMergeItem(filePath, title));
                }
                else if ((arg == "-r" || arg == "--rotate") && i + 1 < args.Length)
                {
                    rotateFile = args[++i];
                }
                else if (arg == "--angle" && i + 1 < args.Length)
                {
                    if (!int.TryParse(args[++i], out rotateAngle) || rotateAngle % 90 != 0)
                    {
                        Console.WriteLine("[错误] 旋转角度必须为90的整数倍。");
                        return 1;
                    }
                }
                else if (arg == "-f" || arg == "--file")
                {
                    if (i + 1 < args.Length)
                    {
                        return ProcessLegacyTmFile(args[++i]);
                    }
                }
                else if (arg == "-h" || arg == "--help")
                {
                    PrintHelp();
                    return 0;
                }
                else
                {
                    Console.WriteLine($"[错误] 无法识别参数或缺少参数值：{args[i]}");
                    return 1;
                }
            }

            if (!string.IsNullOrEmpty(rotateFile))
            {
                Console.WriteLine($"正在旋转 PDF: {rotateFile}, 角度: {rotateAngle}°...");
                bool rotOk = PdfMerger.RotatePdf(rotateFile, rotateAngle, out string rotErr);
                if (!rotOk)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[错误] 旋转失败: {rotErr}");
                    Console.ResetColor();
                    return 1;
                }
                Console.WriteLine("[成功] 页面旋转完成。");
                return 0;
            }

            if (string.IsNullOrEmpty(outputFile) || mergeItems.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[错误] 缺少输出文件或没有输入PDF文件。");
                Console.ResetColor();
                PrintHelp();
                return 1;
            }

            Console.WriteLine($"准备合并 {mergeItems.Count} 个单页PDF到 -> {outputFile}");
            bool ok = PdfMerger.MergePdfFiles(mergeItems, outputFile, out string err);
            if (!ok)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[错误] 合并失败: {err}");
                Console.ResetColor();
                return 1;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[成功] 合并完成！输出文件: {outputFile}");
            Console.ResetColor();
            return 0;
        }

        private static int ProcessLegacyTmFile(string tmPath)
        {
            try
            {
                using var fs = new FileStream(tmPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, Encoding.GetEncoding("gb2312"));

                string? outputPdf = sr.ReadLine();
                if (string.IsNullOrEmpty(outputPdf))
                {
                    Console.WriteLine("[错误] plot2.tm 输出文件路径为空。");
                    return 1;
                }

                string? countLine = sr.ReadLine();
                if (!int.TryParse(countLine, out int count) || count <= 0)
                {
                    Console.WriteLine("[错误] plot2.tm 包含的图纸数量无效。");
                    return 1;
                }

                var items = new List<PdfMergeItem>();
                for (int i = 0; i < count; i++)
                {
                    string? file = sr.ReadLine();
                    string? title = sr.ReadLine();
                    if (string.IsNullOrWhiteSpace(file) || title == null)
                    {
                        Console.WriteLine($"[错误] 任务文件不完整：声明 {count} 份，第 {i + 1} 份缺少路径或标题行。未生成合并文件。");
                        return 1;
                    }
                    items.Add(new PdfMergeItem(file, title));
                }

                Console.WriteLine($"读取到 {items.Count} 份图纸，目标输出: {outputPdf}");
                bool ok = PdfMerger.MergePdfFiles(items, outputPdf, out string err);
                if (!ok)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[错误] 合并失败: {err}");
                    Console.ResetColor();
                    return 1;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[成功] 兼容模式合并完成！");
                Console.ResetColor();
                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[异常] 处理任务文件失败: {ex.Message}");
                Console.ResetColor();
                return 1;
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("用法:");
            Console.WriteLine("  1. 合并PDF并生成书签大纲:");
            Console.WriteLine("     PlotMerger.exe -o \"输出.pdf\" -i \"图纸1.pdf\" \"标题1\" -i \"图纸2.pdf\" \"标题2\"");
            Console.WriteLine("  2. 处理旧版 MSteel 任务文件:");
            Console.WriteLine("     PlotMerger.exe -f \"path\\to\\plot2.tm\"");
            Console.WriteLine("  3. 旋转PDF文件页面:");
            Console.WriteLine("     PlotMerger.exe -r \"图纸.pdf\" --angle 90");
        }
    }
}
