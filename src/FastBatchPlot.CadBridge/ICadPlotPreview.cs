using FastBatchPlot.Core.Models;

namespace FastBatchPlot.CadBridge
{
    public enum PlotPreviewOutcome { Failed, Closed, Cancelled, PrintRequested }

    /// <summary>只预览单页；不得生成输出文件或提交打印队列。</summary>
    public interface ICadPlotPreview
    {
        PlotPreviewOutcome PreviewFrame(PlotFrame frame, PlotConfig config, out string errorMessage);
    }
}
