using System.Collections.Generic;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;

namespace FastBatchPlot.CadBridge
{
    public interface ICadTitleTemplateHost
    {
        bool FindTemplateSample(string blockName, out PlotFrame? frame);
        bool PromptSelectTemplateSample(out PlotFrame? frame);
        bool PromptTemplateRegion(PlotFrame frame, out TemplateRegion? region);
        List<TemplateText> CollectTemplateTexts(PlotFrame frame);
    }
}
