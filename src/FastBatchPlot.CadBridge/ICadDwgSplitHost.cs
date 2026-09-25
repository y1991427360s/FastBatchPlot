using FastBatchPlot.Core.Export;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.CadBridge
{
    public interface ICadDwgSplitHost
    {
        DwgSplitPlan InspectDwgSplit(PlotFrame frame);
        void ExportDwgSplit(DwgSplitPlan inspectedPlan, string targetPath);
    }
}
