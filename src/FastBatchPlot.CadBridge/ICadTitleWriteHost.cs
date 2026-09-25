using System.Collections.Generic;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;

namespace FastBatchPlot.CadBridge
{
    public interface ICadTitleWriteHost
    {
        TitleWritePlan PrepareTitleWrite(PlotFrame frame,TitleBlockTemplate template,IDictionary<TitleField,string> values);
        void ApplyTitleWrites(IReadOnlyList<TitleWritePlan> plans,bool undo=false);
    }
}
