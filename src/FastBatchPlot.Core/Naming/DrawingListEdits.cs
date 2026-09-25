using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Naming
{
    public sealed class RenumberOptions
    {
        public string Prefix { get; set; } = "";
        public string Suffix { get; set; } = "";
        public int Start { get; set; } = 1;
        public int Step { get; set; } = 1;
        public int Digits { get; set; } = 3;
    }
    public sealed class DrawingNumberChange
    {
        public PlotFrame Frame { get; internal set; } = null!;
        public string Before { get; internal set; } = "";
        public string After { get; internal set; } = "";
    }
    public static class DrawingListEdits
    {
        // 计划与提交分离，预览不会改变列表。
        public static List<DrawingNumberChange> PlanNumbers(IEnumerable<PlotFrame> orderedFrames, RenumberOptions options)
        {
            if(options==null || options.Prefix==null || options.Suffix==null || options.Prefix.Length>100 || options.Suffix.Length>100
                || options.Start<0 || options.Step<1 || options.Digits<1 || options.Digits>9)
                throw new ArgumentException("编号前后缀最长100字符，起号非负，步长须为正整数，位数为1至9。");
            if(options.Prefix.Any(char.IsControl)||options.Suffix.Any(char.IsControl)) throw new ArgumentException("编号前后缀不能含控制字符。");
            var frames=orderedFrames.ToList();
            if(frames.Count==0 || frames.Any(f=>f==null||f.TitleInfo==null) || frames.Distinct().Count()!=frames.Count)
                throw new ArgumentException("编号对象不能为空或重复。");
            return frames.Select((f,i)=>new DrawingNumberChange {Frame=f,Before=f.TitleInfo.DrawingNo,
                After=options.Prefix+checked(options.Start+checked(i*options.Step)).ToString("D"+options.Digits,CultureInfo.InvariantCulture)+options.Suffix}).ToList();
        }
        public static void ApplyNumbers(IReadOnlyList<DrawingNumberChange> changes, IEnumerable<PlotFrame> current, bool undo=false)
        {
            var all=new HashSet<PlotFrame>(current);
            if(changes.Count==0 || changes.Select(c=>c.Frame).Distinct().Count()!=changes.Count)
                throw new InvalidOperationException("编号计划为空或含重复对象。");
            foreach(var c in changes)
                if(!all.Contains(c.Frame)||!string.Equals(c.Frame.TitleInfo.DrawingNo,undo?c.After:c.Before,StringComparison.Ordinal))
                    throw new InvalidOperationException("图纸列表或图号已变化，未应用任何编号，请重新预览。");
            foreach(var c in changes)c.Frame.TitleInfo.DrawingNo=undo?c.Before:c.After;
        }
        public static List<PlotFrame> Move(IEnumerable<PlotFrame> order,IEnumerable<PlotFrame> selection,int direction)
        {
            if(direction!=1&&direction!=-1)throw new ArgumentException("移动方向须为-1或1。");
            var result=order.ToList();var selected=new HashSet<PlotFrame>(selection);
            if(selected.Any(f=>!result.Contains(f)))throw new ArgumentException("选择对象已不在列表中。");
            if(direction<0)
            {
                for(int i=1;i<result.Count;i++)if(selected.Contains(result[i])&&!selected.Contains(result[i-1]))
                {var previous=result[i-1];result[i-1]=result[i];result[i]=previous;}
            }
            else
            {
                for(int i=result.Count-2;i>=0;i--)if(selected.Contains(result[i])&&!selected.Contains(result[i+1]))
                {var next=result[i+1];result[i+1]=result[i];result[i]=next;}
            }
            return result;
        }
    }
    public sealed class DrawingListIssue
    {
        public PlotFrame Frame {get;internal set;}=null!;
        public bool DuplicateNumber {get;internal set;}
        public bool DuplicateFileName {get;internal set;}
    }
    public static class DrawingListDiagnostics
    {
        public static List<DrawingListIssue> Find(IEnumerable<PlotFrame> frames)
        {
            var items=frames.ToList();
            var numbers=new HashSet<PlotFrame>(items.Where(f=>!string.IsNullOrWhiteSpace(f.TitleInfo.DrawingNo))
                .GroupBy(f=>f.TitleInfo.DrawingNo.Trim(),StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1).SelectMany(g=>g));
            var files=new HashSet<PlotFrame>(items.Where(f=>!string.IsNullOrWhiteSpace(f.CustomOutputFileName))
                .GroupBy(f=>f.CustomOutputFileName.Trim().TrimEnd('.'),StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1).SelectMany(g=>g));
            return items.Where(f=>numbers.Contains(f)||files.Contains(f)).Select(f=>new DrawingListIssue{Frame=f,DuplicateNumber=numbers.Contains(f),DuplicateFileName=files.Contains(f)}).ToList();
        }
    }
}
