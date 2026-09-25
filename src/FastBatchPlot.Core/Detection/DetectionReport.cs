using System.Collections.Generic;
using System.Text;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Detection
{
    /// <summary>一次识别的诊断快照；计数完整，明细有界，不持有 CAD 对象。</summary>
    public sealed class DetectionReport
    {
        public const int DetailLimit = 2000;
        public int TotalCount { get; private set; }
        public Dictionary<string, int> Counts { get; } = new Dictionary<string, int>();
        public List<string> Details { get; } = new List<string>();
        public void Add(string reason, string source)
        {
            TotalCount++;
            Counts.TryGetValue(reason, out int count);
            Counts[reason] = count + 1;
            if (Details.Count < DetailLimit) Details.Add(reason + "：" + source);
        }
        public void Add(string reason, RawBlockCandidate c) => Add(reason, Source(c.SourceFileName,c.LayoutName,c.Handle,c.Layer,c.BlockName));
        public void Add(string reason, RawPolylineCandidate c) => Add(reason, Source(c.SourceFileName,c.LayoutName,c.Handle,c.Layer,"多段线"));
        public void Add(string reason, PlotFrame c) => Add(reason, Source(c.SourceFileName,c.LayoutName,c.HandleOrId,c.SourceLayer,c.SourceBlockName));
        private static string Source(string file,string layout,string handle,string layer,string block)
            => $"文件={file} / 空间={layout} / 实体={handle} / 图层={layer} / 图块={block}";
        public string ToText()
        {
            var text = new StringBuilder();
            foreach (var pair in Counts) text.AppendLine(pair.Key + "：" + pair.Value);
            if (TotalCount > Details.Count) text.AppendLine($"明细仅展示前 {Details.Count} 条；另 {TotalCount-Details.Count} 条未展示，计数仍完整。");
            text.AppendLine();
            foreach (var detail in Details) text.AppendLine(detail);
            return text.ToString();
        }
    }
}
