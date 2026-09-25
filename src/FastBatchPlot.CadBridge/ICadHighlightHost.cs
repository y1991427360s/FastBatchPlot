using System.Collections.Generic;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.CadBridge
{
    /// <summary>
    /// CAD 视口图框高亮与序号标记交互宿主
    /// </summary>
    public interface ICadHighlightHost
    {
        /// <summary>在 CAD 视口绘制所有识别图框的临时外框与序号标注</summary>
        void ShowFrameMarkers(IReadOnlyList<PlotFrame> frames);

        /// <summary>高亮显示单个图框（例如当前列表选中的行）</summary>
        void HighlightCurrentFrame(PlotFrame? frame);

        /// <summary>清除视口中的所有临时图框标记与高亮</summary>
        void ClearFrameMarkers();
    }
}
