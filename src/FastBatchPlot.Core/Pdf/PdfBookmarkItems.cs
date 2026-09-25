using System.Collections.Generic;
using System.Linq;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Naming;
using FastBatchPlot.Core.Tasks;

namespace FastBatchPlot.Core.Pdf
{
    public static class PdfBookmarkItems
    {
        public static List<PdfMergeItem> Create(IEnumerable<BatchPage> pages, PlotConfig config) =>
            pages.Select(p => new PdfMergeItem(p.OutputPath,
                DrawingNameFormatter.FormatBookmark(config.BookmarkTemplate, p.Frame))).ToList();
    }
}
