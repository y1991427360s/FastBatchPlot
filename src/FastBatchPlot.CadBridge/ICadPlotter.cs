using System.Collections.Generic;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.CadBridge
{
    /// <summary>
    /// CAD 打印管线引擎接口
    /// </summary>
    public interface ICadPlotter
    {
        List<string> GetAvailablePlotters();
        List<string> GetAvailablePlotStyles();
        List<string> GetPaperSizesForPlotter(string plotterDevice);

        /// <summary>
        /// 将单个图框打印输出为临时或目标文件 (如单页PDF/DWF/PLT)
        /// </summary>
        bool PlotFrameToFile(PlotFrame frame, PlotConfig config, string outputFilePath, out string errorMessage);
    }

    public interface ICadPrinter
    {
        // 成功仅表示 CAD 引擎已提交任务，不证明纸张已实际输出。
        bool PrintFrameToDevice(PlotFrame frame, PlotConfig config, out string errorMessage);
    }

    /// <summary>
    /// CAD 宿主运行时全局提供者
    /// </summary>
    public static class CadHostProvider
    {
        public static ICadHost? Host { get; set; }
        public static ICadPlotter? Plotter { get; set; }

        public static bool IsInitialized => Host != null && Plotter != null;
    }
}
