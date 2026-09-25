using System;
using System.IO;
using System.Text;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;
using FastBatchPlot.Core.Printing;

internal static class GeneratedMediaChecks
{
    public static void Run()
    {
        string directory = Path.Combine(Path.GetTempPath(), "FastBatchPlot.MediaFramework-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "source.pc5");
        string text = "[Meta]\r\nPrinterType=2\r\nconfig_description=中文测试\r\n" +
            "[Standard]\r\nDeviceName=PDF\r\nDriverName=DWG to PDF\r\nDriverPath=ZwPDFDriver.dll\r\nDriverCfgPath=PDF.ini\r\nPortName=FILE:\r\n" +
            "[Port]\r\nplot_to_file=1\r\n[Caps]\r\ndefinepapersize_caps=1\r\n";
        var gbk = Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        File.WriteAllText(source, text, gbk);
        try
        {
            string generated;
            using (var files = ZwPdfMediaFiles.Create(source, 634, 301, directory))
            {
                generated = files.ConfigurationPath;
                if (!File.ReadAllText(generated, gbk).Contains("中文测试") ||
                    !File.ReadAllText(files.PmpPath, gbk).Contains("userdef_num=1"))
                    throw new Exception("独立介质配置未保留编码或单张纸张结构。");
                bool locked = false;
                try { using (File.Open(generated, FileMode.Open, FileAccess.Write)) { } }
                catch (IOException) { locked = true; }
                if (!locked) throw new Exception("独立配置在使用期可以被改写。");
                var plan = PlotPlanBuilder.Create(new PlotFrame
                {
                    MaxX = 630, MaxY = 297, IsLandscape = true, CalculatedScale = 1,
                    DetectedPaper = new PaperSize("extended", 630, 297)
                }, new PlotConfig { MarginMm = 2 });
                var media = new PlotMedia
                {
                    Name = files.MediaName, WidthMm = 634, HeightMm = 301,
                    PrintableWidthMm = 634, PrintableHeightMm = 301
                };
                GeneratedPdfMediaGuard.Validate(plan, generated, generated, files.MediaName, media);
                media.WidthMm = 594;
                bool rejected = false;
                try { GeneratedPdfMediaGuard.Validate(plan, generated, generated, files.MediaName, media); }
                catch (InvalidOperationException) { rejected = true; }
                if (!rejected) throw new Exception("驱动替换纸张未被拒绝。");
            }
            if (File.Exists(generated) || File.ReadAllText(source, gbk) != text)
                throw new Exception("独立配置未清理或源文件被改写。");
            Console.WriteLine("NET48_GENERATED_PDF_MEDIA_OK");
        }
        finally { File.Delete(source); Directory.Delete(directory); }
    }
}
