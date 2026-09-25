using System;
using System.Collections.Generic;
using FastBatchPlot.Core.Detection;

namespace FastBatchPlot.CadBridge
{
    /// <summary>
    /// CAD 平台宿主抽象接口 (解耦 AutoCAD 与 中望CAD 差异)
    /// </summary>
    public interface ICadHost
    {
        string PlatformName { get; }
        string Version { get; }

        void WriteMessage(string message);
        string GetCurrentDocumentPath();
        string GetCurrentDocumentName();

        /// <summary>
        /// 收集当前图形或指定图层上的封闭多段线作为图框候选
        /// </summary>
        List<RawPolylineCandidate> CollectPolylineCandidates(string layerFilter = "*");

        /// <summary>
        /// 收集当前图形中的图块引用作为图框候选
        /// </summary>
        List<RawBlockCandidate> CollectBlockCandidates(string blockNameFilter = "*");

        /// <summary>
        /// 交互式框选图纸或图框实体
        /// </summary>
        bool PromptSelectFrames(out List<RawPolylineCandidate> polylines, out List<RawBlockCandidate> blocks);

        /// <summary>
        /// 从图中点选一个图框实体，返回其图块名称或图层名称
        /// </summary>
        bool PromptSelectSampleBlock(out string blockName, out string layerName);

        /// <summary>
        /// 高亮显示指定实体并缩放到视图中心
        /// </summary>
        void ZoomToFrame(double minX, double minY, double maxX, double maxY);

        /// <summary>
        /// 获取所有图层名称列表
        /// </summary>
        List<string> GetAllLayers();

        /// <summary>
        /// 获取所有图块定义名称列表
        /// </summary>
        List<string> GetAllBlockNames();
    }
}
