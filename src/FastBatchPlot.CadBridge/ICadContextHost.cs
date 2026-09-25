using FastBatchPlot.Core.Models;

namespace FastBatchPlot.CadBridge
{
    /// <summary>可选上下文校验能力，避免旧列表作用于另一个活动图纸。</summary>
    public interface ICadContextHost
    {
        bool IsFrameContextCurrent(PlotFrame frame);
    }
}
