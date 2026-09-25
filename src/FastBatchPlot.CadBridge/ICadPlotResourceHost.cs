using FastBatchPlot.Core.Models;

namespace FastBatchPlot.CadBridge
{
    /// <summary>核对当前所选打印样式及设备配置文件，不代表字体等全部出图依赖。</summary>
    public interface ICadPlotResourceHost
    {
        string GetPlotResourceRevision(PlotConfig config);
    }
}
