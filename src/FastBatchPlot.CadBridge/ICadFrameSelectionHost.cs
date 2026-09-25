using System.Collections.Generic;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.CadBridge
{
    public sealed class CadCandidateSnapshot
    {
        public List<RawPolylineCandidate> Polylines { get; } = new List<RawPolylineCandidate>();
        public List<RawBlockCandidate> Blocks { get; } = new List<RawBlockCandidate>();
        public List<string> Warnings { get; } = new List<string>();
    }

    public enum ManualFrameSelectionMode { TwoCorners, EntityGroup }

    /// <summary>在同一宿主上下文采集快照和手工范围，业务层不持有 CAD 对象。</summary>
    public interface ICadFrameSelectionHost
    {
        CadCandidateSnapshot CollectCurrentSpaceCandidates(string layerFilter = "*");
        bool PromptManualFrame(ManualFrameSelectionMode mode, out PlotFrame? frame);
    }
}
