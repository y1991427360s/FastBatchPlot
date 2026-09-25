using System;
using System.IO;

namespace FastBatchPlot.Core.Planning
{
    /// <summary>生成配置只是请求；必须用宿主实际加载的配置路径和介质尺寸确认请求已生效。</summary>
    public static class GeneratedPdfMediaGuard
    {
        public const double ToleranceMm = PlotPlanBuilder.MediaToleranceMm;

        public static void Validate(PlotPlan plan, string expectedConfigurationPath,
            string loadedConfigurationPath, string expectedMediaName, PlotMedia actual)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (!Absolute(expectedConfigurationPath) || !Absolute(loadedConfigurationPath) ||
                !string.Equals(Path.GetFullPath(expectedConfigurationPath),
                    Path.GetFullPath(loadedConfigurationPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("驱动没有确认加载本次独立纸张配置，已停止出图。");
            if (actual == null || string.IsNullOrWhiteSpace(expectedMediaName) ||
                !string.Equals(expectedMediaName, actual.Name, StringComparison.Ordinal))
                throw new InvalidOperationException("驱动没有返回本次生成的纸张标识，已停止出图。");
            // 新建纸张明确指定宽高和零边距，不接受驱动改成相近纸张或偷偷交换宽高。
            if (!Same(plan.PaperWidthMm, actual.WidthMm) || !Same(plan.PaperHeightMm, actual.HeightMm) ||
                !Same(0, actual.PrintableLeftMm) || !Same(0, actual.PrintableBottomMm) ||
                !Same(plan.PaperWidthMm, actual.PrintableWidthMm) ||
                !Same(plan.PaperHeightMm, actual.PrintableHeightMm) || !PlotPlanBuilder.CanPlace(plan, actual))
                throw new InvalidOperationException("驱动返回的自定义纸张尺寸或可打印区域与请求不一致，已停止出图。");
        }

        private static bool Same(double expected, double actual) =>
            !double.IsNaN(actual) && !double.IsInfinity(actual) && Math.Abs(expected - actual) <= ToleranceMm;

        private static bool Absolute(string path) => !string.IsNullOrWhiteSpace(path) &&
            Path.IsPathRooted(path) && string.Equals(path, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
    }
}
