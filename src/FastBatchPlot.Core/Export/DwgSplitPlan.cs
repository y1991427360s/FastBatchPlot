using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Export
{
    public enum SplitRelation { Outside, Inside, Crossing }

    /// <summary>WCS 包围盒判定；相交对象整件保留，不裁切几何。</summary>
    public static class DwgSplitGeometry
    {
        public static SplitRelation Classify(PlotFrame frame, double x1, double y1, double x2, double y2)
        {
            if (frame == null || !Valid(frame.MinX, frame.MinY, frame.MaxX, frame.MaxY)
                || frame.MinX == frame.MaxX || frame.MinY == frame.MaxY || !Valid(x1,y1,x2,y2))
                throw new ArgumentException("拆分范围或实体包围盒无效。");
            if (x2 < frame.MinX || x1 > frame.MaxX || y2 < frame.MinY || y1 > frame.MaxY) return SplitRelation.Outside;
            return x1 >= frame.MinX && x2 <= frame.MaxX && y1 >= frame.MinY && y2 <= frame.MaxY
                ? SplitRelation.Inside : SplitRelation.Crossing;
        }
        private static bool Valid(double x1,double y1,double x2,double y2) =>
            new[]{x1,y1,x2,y2}.All(v=>!double.IsNaN(v)&&!double.IsInfinity(v)) && x1<=x2 && y1<=y2;
    }

    public sealed class DwgSplitPlan
    {
        private readonly PlotFrame _source;
        public PlotFrame Source => new PlotFrame { SourceDocumentId=_source.SourceDocumentId,
            SourceLayoutId=_source.SourceLayoutId, LayoutName=_source.LayoutName, HandleOrId=_source.HandleOrId,
            MinX=_source.MinX,MinY=_source.MinY,MaxX=_source.MaxX,MaxY=_source.MaxY };
        public ReadOnlyCollection<string> Handles { get; }
        public ReadOnlyCollection<string> Warnings { get; }
        public ReadOnlyCollection<string> Errors { get; }
        public int CrossingCount { get; }
        public bool CanExport => Errors.Count==0 && Handles.Count>0;
        public DwgSplitPlan(PlotFrame source,IEnumerable<string> handles,int crossing,
            IEnumerable<string> warnings,IEnumerable<string> errors)
        {
            _source=source ?? throw new ArgumentNullException(nameof(source));
            _source=Source;
            Handles=Array.AsReadOnly(handles.OrderBy(h=>h,StringComparer.Ordinal).ToArray());
            CrossingCount=crossing;Warnings=Array.AsReadOnly(warnings.Distinct().ToArray());
            Errors=Array.AsReadOnly(errors.Distinct().ToArray());
        }
        public bool Matches(DwgSplitPlan other) => other.CanExport && CanExport
            && _source.SourceDocumentId==other._source.SourceDocumentId
            && _source.SourceLayoutId==other._source.SourceLayoutId
            && _source.LayoutName==other._source.LayoutName && _source.HandleOrId==other._source.HandleOrId
            && _source.MinX==other._source.MinX && _source.MinY==other._source.MinY
            && _source.MaxX==other._source.MaxX && _source.MaxY==other._source.MaxY
            && Handles.SequenceEqual(other.Handles) && CrossingCount==other.CrossingCount
            && Warnings.SequenceEqual(other.Warnings);
    }
}
