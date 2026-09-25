using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FastBatchPlot.Core.Planning;
using FastBatchPlot.Core.Printing;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.PlottingServices;
using ZwSoft.ZwCAD.Runtime;

namespace FastBatchPlot.ZWCAD
{
    public partial class ZwCadPlotEngine
    {
        // 缺少精确介质或覆盖 PDF 参数时创建私有配置；不切换 PlotConfigManager.CurrentConfig 或修改搜索路径。
        private static PlotMedia ResolvePdfMedia(PlotSettings settings, PlotPlan plan, string selectedDevice, FastBatchPlot.Core.Models.PdfOutputOptions options, out ZwPdfMediaFiles? files)
        {
            files = null;
            if (settings == null || plan == null) throw new ArgumentNullException(settings == null ? nameof(settings) : nameof(plan));
            if (string.IsNullOrWhiteSpace(selectedDevice)) throw new InvalidOperationException("未选择 PDF 设备，不能创建独立纸张配置。");

            bool selectedIsPath = Path.IsPathRooted(selectedDevice);
            string? selectedPath = selectedIsPath ? LocalPc5Path(selectedDevice) : null;
            var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var devices = PlotConfigManager.Devices)
            {
                foreach (PlotConfigInfo device in devices)
                {
                    // 枚举返回的条目仅在集合存活期间使用，不保存原生对象引用。
                    string candidateName = device.DeviceName;
                    string candidatePath = device.FullPath;
                    bool nameMatches = !selectedIsPath && string.Equals(candidateName, selectedDevice, StringComparison.OrdinalIgnoreCase);
                    bool pathMatches = selectedIsPath && !string.IsNullOrWhiteSpace(candidatePath) && Path.IsPathRooted(candidatePath) &&
                        string.Equals(Path.GetFullPath(candidatePath), selectedPath, StringComparison.OrdinalIgnoreCase);
                    if (!nameMatches && !pathMatches) continue;
                    matches.Add(LocalPc5Path(candidatePath));
                }
            }
            if (matches.Count != 1)
                throw new InvalidOperationException(matches.Count == 0
                    ? "当前设备列表没有返回所选 PDF 设备的真实本地 PC5 路径，未猜测目录或替换设备。"
                    : "所选 PDF 设备名称对应多个 PC5 文件，无法确定原配置，已停止出图。");
            string source = matches.Single();
            if (!File.Exists(source)) throw new FileNotFoundException("设备列表中的源 PC5 已不存在，不能创建独立纸张配置。", source);

            // 必须在任何宿主调用前赋值；加载失败也由外层 finally 释放读锁并删除本次私有文件。
            files = ZwPdfMediaFiles.Create(source, plan.PaperWidthMm, plan.PaperHeightMm,
                Path.Combine(Path.GetTempPath(), "FastBatchPlot", "Media"), options);
            files.ReleaseLocks();
            var validator = PlotSettingsValidator.Current;
            validator.RefreshLists(settings);
            try
            {
                validator.SetPlotConfigurationName(settings, files.ConfigurationPath, null);
            }
            catch (ZwSoft.ZwCAD.Runtime.Exception ex) when (ex.ErrorStatus == ErrorStatus.InvalidInput)
            {
                // 该版本只接受打印机目录中的设备：暂存副本随 files.Dispose 删除，不留下失效设备。
                string stagedPc5 = files.StageInto(Path.GetDirectoryName(source)!);
                validator.RefreshLists(settings);
                validator.SetPlotConfigurationName(settings, Path.GetFileName(stagedPc5), null);
            }
            validator.RefreshLists(settings);
            var names = validator.GetCanonicalMediaNameList(settings);
            string expectedName = files.MediaName;
            if (names.Cast<string>().Count(name => string.Equals(name, expectedName, StringComparison.Ordinal)) != 1)
                throw new InvalidOperationException("驱动没有识别本次生成的唯一纸张标识，未使用近似纸张。");
            validator.SetCanonicalMediaName(settings, files.MediaName);
            validator.SetPlotPaperUnits(settings, PlotPaperUnit.Millimeters);
            validator.SetPlotRotation(settings, PlotRotation.Degrees000);
            var media = GeneratedSettingsMedia(settings);
            // 此处仅验证加载后的尺寸；真实加载路径将在 PlotInfoValidator.Validate 后单独核对。
            GeneratedPdfMediaGuard.Validate(plan, files.EffectiveConfigurationPath, files.EffectiveConfigurationPath, files.MediaName, media);
            return media;
        }

        private static void ValidateGeneratedPdfMedia(PlotInfo info, PlotSettings requested, PlotPlan plan, ZwPdfMediaFiles files)
        {
            if (!info.IsValidated) throw new InvalidOperationException("独立 PDF 纸张配置未通过宿主验证，已停止出图。");
            // 中望 2026 的 ValidatedConfig 即使 IsValidated=true 也可能没有原生对象。
            // 直接读取宿主验证后的设置，不回填预期值，也不切换全局 CurrentConfig。
            using (var actualSettings = info.ValidatedSettings)
            {
                string actualName = actualSettings.CanonicalMediaName;
                string actualConfigPath = Path.IsPathRooted(actualSettings.PlotConfigurationName)
                    ? actualSettings.PlotConfigurationName
                    : Path.Combine(Path.GetDirectoryName(files.EffectiveConfigurationPath) ?? string.Empty, actualSettings.PlotConfigurationName);
                GeneratedPdfMediaGuard.Validate(plan, files.EffectiveConfigurationPath, actualConfigPath, files.MediaName,
                    GeneratedSettingsMedia(actualSettings));

                var window = requested.PlotWindowArea;
                var actualWindow = actualSettings.PlotWindowArea;
                var scale = requested.CustomPrintScale;
                var actualScale = actualSettings.CustomPrintScale;
                bool unchanged = requested.PlotPaperUnits == PlotPaperUnit.Millimeters &&
                    actualSettings.PlotPaperUnits == PlotPaperUnit.Millimeters &&
                    requested.PlotRotation == PlotRotation.Degrees000 && actualSettings.PlotRotation == requested.PlotRotation &&
                    requested.PlotType == ZwSoft.ZwCAD.DatabaseServices.PlotType.Window && actualSettings.PlotType == requested.PlotType &&
                    string.Equals(actualName, requested.CanonicalMediaName, StringComparison.Ordinal) &&
                    SameGeneratedNumber(window.MinPoint.X, actualWindow.MinPoint.X) && SameGeneratedNumber(window.MinPoint.Y, actualWindow.MinPoint.Y) &&
                    SameGeneratedNumber(window.MaxPoint.X, actualWindow.MaxPoint.X) && SameGeneratedNumber(window.MaxPoint.Y, actualWindow.MaxPoint.Y) &&
                    !requested.UseStandardScale && !actualSettings.UseStandardScale &&
                    SameGeneratedScale(scale.Numerator, scale.Denominator, actualScale.Numerator, actualScale.Denominator) &&
                    SameGeneratedNumber(requested.PlotOrigin.X, actualSettings.PlotOrigin.X) && SameGeneratedNumber(requested.PlotOrigin.Y, actualSettings.PlotOrigin.Y) &&
                    actualSettings.PlotCentered == requested.PlotCentered &&
                    string.Equals(actualSettings.CurrentStyleSheet, requested.CurrentStyleSheet, StringComparison.OrdinalIgnoreCase) &&
                    actualSettings.PlotPlotStyles == requested.PlotPlotStyles && actualSettings.PrintLineweights == requested.PrintLineweights &&
                    actualSettings.ScaleLineweights == requested.ScaleLineweights && actualSettings.PlotTransparency == requested.PlotTransparency &&
                    actualSettings.PlotHidden == requested.PlotHidden && actualSettings.DrawViewportsFirst == requested.DrawViewportsFirst &&
                    actualSettings.PlotViewportBorders == requested.PlotViewportBorders && actualSettings.ShowPlotStyles == requested.ShowPlotStyles &&
                    actualSettings.ShadePlot == requested.ShadePlot && actualSettings.ShadePlotId == requested.ShadePlotId &&
                    actualSettings.ShadePlotResLevel == requested.ShadePlotResLevel && actualSettings.ShadePlotCustomDpi == requested.ShadePlotCustomDpi &&
                    actualSettings.ModelType == requested.ModelType;
                if (!unchanged)
                    throw new InvalidOperationException("宿主验证改变了 PDF 的单位、方向、窗口、比例、原点或输出样式，已停止出图。");
            }
        }

        private static PlotMedia GeneratedSettingsMedia(PlotSettings settings)
        {
            var size = settings.PlotPaperSize;
            var margins = settings.PlotPaperMargins;
            return new PlotMedia {
                Name = settings.CanonicalMediaName, WidthMm = size.X, HeightMm = size.Y,
                PrintableLeftMm = margins.MinPoint.X, PrintableBottomMm = margins.MinPoint.Y,
                PrintableWidthMm = size.X - margins.MinPoint.X - margins.MaxPoint.X,
                PrintableHeightMm = size.Y - margins.MinPoint.Y - margins.MaxPoint.Y };
        }
        private static string LocalPc5Path(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length < 3 || !char.IsLetter(path[0]) || path[1] != ':' ||
                (path[2] != '\\' && path[2] != '/') || !string.Equals(Path.GetExtension(path), ".pc5", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("所选设备没有提供本地磁盘上的完整 PC5 路径，不能自动生成纸张。");
            return Path.GetFullPath(path);
        }
        private static bool SameGeneratedNumber(double expected, double actual) =>
            !double.IsNaN(expected) && !double.IsInfinity(expected) && !double.IsNaN(actual) && !double.IsInfinity(actual) &&
            Math.Abs(expected - actual) <= 1e-8;

        private static bool SameGeneratedScale(double numerator, double denominator, double actualNumerator, double actualDenominator)
        {
            // 1:100 与 10:1000 等价；比较打印比例，不要求驱动保留分数的写法。
            if (numerator <= 0 || denominator <= 0 || actualNumerator <= 0 || actualDenominator <= 0) return false;
            double expected = numerator / denominator, actual = actualNumerator / actualDenominator;
            return !double.IsNaN(expected) && !double.IsInfinity(expected) && expected > 0 &&
                !double.IsNaN(actual) && !double.IsInfinity(actual) && actual > 0 &&
                Math.Abs(actual / expected - 1) <= 1e-10;
        }
    }
}
