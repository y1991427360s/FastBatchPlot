using System;
using System.Globalization;
using FastBatchPlot.Core.Paper;

namespace FastBatchPlot.Core.Paper
{
    /// <summary>
    /// 物理纸张安全校验：防止由于比例错误或计算异常产生极端微型纸张或超大纸张，
    /// 避免将微型高密度图纸送入 CAD 驱动导致黑斑、耗尽资源或驱动渲染卡死。
    /// </summary>
    public static class PaperSizeSafety
    {
        /// <summary>短边不得小于 50 mm（现有合法自定义图纸如 500x100mm 依然通过，而 5.94x4.2mm 必定拦截）。</summary>
        public const double MinimumShortEdgeMm = 50.0;

        /// <summary>长边不得小于 100 mm。</summary>
        public const double MinimumLongEdgeMm = 100.0;

        /// <summary>长边不得大于 100000 mm（100米），避免浮点溢出，同时兼容历史超长自定义测试。</summary>
        public const double MaximumLongEdgeMm = 100000.0;

        /// <summary>
        /// 校验物理纸张尺寸是否处于安全工程范围内。如果不合法，抛出包含详细尺寸和比例的 ArgumentException。
        /// </summary>
        /// <param name="widthMm">计算后的物理纸张宽度（毫米）</param>
        /// <param name="heightMm">计算后的物理纸张高度（毫米）</param>
        /// <param name="scale">当前出图比例分母（例如 100 表示 1:100）</param>
        public static void ValidatePhysicalSize(double widthMm, double heightMm, double scale)
        {
            if (double.IsNaN(widthMm) || double.IsInfinity(widthMm) ||
                double.IsNaN(heightMm) || double.IsInfinity(heightMm) ||
                widthMm <= 0 || heightMm <= 0)
            {
                throw new ArgumentException("纸张物理尺寸必须为大于零的有限数值。");
            }

            double shortEdge = Math.Min(widthMm, heightMm);
            double longEdge = Math.Max(widthMm, heightMm);
            string scaleText = scale > 0 ? ScaleCalculator.FormatScale(scale) : "未知";

            if (shortEdge < MinimumShortEdgeMm || longEdge < MinimumLongEdgeMm)
            {
                throw new ArgumentException(
                    $"FastBatchPlot 检测到异常纸张尺寸 {widthMm:0.##} × {heightMm:0.##} mm，已停止处理。" +
                    $"当前打印比例为 {scaleText}。" +
                    $"该尺寸明显小于正常工程图纸（短边不小于 {MinimumShortEdgeMm:0} mm，长边不小于 {MinimumLongEdgeMm:0} mm），请检查图框尺寸、CAD 单位或打印比例。");
            }

            if (longEdge > MaximumLongEdgeMm)
            {
                throw new ArgumentException(
                    $"FastBatchPlot 检测到超大纸张尺寸 {widthMm:0.##} × {heightMm:0.##} mm，已停止处理。" +
                    $"当前打印比例为 {scaleText}。" +
                    $"该尺寸超出最大安全出图尺寸（长边不超过 {MaximumLongEdgeMm:0} mm），请检查图框尺寸或打印比例。");
            }
        }
    }
}
