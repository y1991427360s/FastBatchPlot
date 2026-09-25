using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Export
{
    public enum CatalogField { Index, DrawingNo, DrawingName, Paper, Scale, Revision, Date, Layout, ProjectName, SubProject, Stage, Discipline, Designer, Checker, Approver, Status, OutputFileName }
    [DataContract]
    public sealed class CatalogColumn
    {
        [DataMember(IsRequired=true)] public CatalogField Field { get; set; }
        [DataMember(IsRequired=true)] public string Header { get; set; } = "";
        [DataMember(IsRequired=true)] public double Width { get; set; } = 18;
    }
    [DataContract]
    public sealed class CatalogOptions
    {
        [DataMember(IsRequired=true)] public string Title { get; set; } = "图纸目录";
        [DataMember(IsRequired=true)] public bool SelectedOnly { get; set; } = true;
        [DataMember(IsRequired=true)] public bool IncludeStatistics { get; set; } = true;
        [DataMember(IsRequired=true)] public double RowHeight { get; set; } = 28;
        [DataMember(IsRequired=true)] public List<CatalogColumn> Columns { get; set; } = new List<CatalogColumn> {
            new CatalogColumn { Field=CatalogField.Index, Header="序号", Width=8 },
            new CatalogColumn { Field=CatalogField.DrawingNo, Header="图号", Width=22 },
            new CatalogColumn { Field=CatalogField.DrawingName, Header="图名", Width=44 },
            new CatalogColumn { Field=CatalogField.Paper, Header="图幅", Width=16 },
            new CatalogColumn { Field=CatalogField.Scale, Header="比例", Width=12 },
            new CatalogColumn { Field=CatalogField.Revision, Header="版次", Width=10 },
            new CatalogColumn { Field=CatalogField.Date, Header="出图日期", Width=16 },
            new CatalogColumn { Field=CatalogField.Layout, Header="布局", Width=18 }
        };
        public static string Label(CatalogField field)
        {
            string[] labels={"序号","图号","图名","图幅","比例","版次","出图日期","布局","工程名称","子项","阶段","专业","设计","校对","审核","状态","输出文件名"};
            if (!Enum.IsDefined(typeof(CatalogField),field)) throw new ArgumentException("未知目录字段。");
            return labels[(int)field];
        }
        public static string Value(PlotFrame f, CatalogField field)
        {
            switch(field)
            {
                case CatalogField.Index: return f.OrderIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case CatalogField.DrawingNo: return f.TitleInfo.DrawingNo;
                case CatalogField.DrawingName: return f.TitleInfo.DrawingName;
                case CatalogField.Paper: return f.DetectedPaper.Name;
                case CatalogField.Scale: return FormattableString.Invariant($"1:{f.CalculatedScale:0.########}");
                case CatalogField.Revision: return f.TitleInfo.Revision;
                case CatalogField.Date: return f.TitleInfo.Date;
                case CatalogField.Layout: return f.LayoutName;
                case CatalogField.ProjectName: return f.TitleInfo.ProjectName;
                case CatalogField.SubProject: return f.TitleInfo.SubProject;
                case CatalogField.Stage: return f.TitleInfo.Stage;
                case CatalogField.Discipline: return f.TitleInfo.Discipline;
                case CatalogField.Designer: return f.TitleInfo.Designer;
                case CatalogField.Checker: return f.TitleInfo.Checker;
                case CatalogField.Approver: return f.TitleInfo.Approver;
                case CatalogField.Status: return f.Status;
                case CatalogField.OutputFileName: return f.CustomOutputFileName;
                default: throw new ArgumentException("未知目录字段。");
            }
        }
        public CatalogOptions Copy()
        {
            Validate();
            return new CatalogOptions{Title=Title,SelectedOnly=SelectedOnly,IncludeStatistics=IncludeStatistics,RowHeight=RowHeight,
                Columns=Columns.Select(c=>new CatalogColumn{Field=c.Field,Header=c.Header,Width=c.Width}).ToList()};
        }
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Title) || Title.Length>200 || !Finite(RowHeight) || RowHeight<16 || RowHeight>120
                || Columns==null || Columns.Count<1 || Columns.Count>17) throw new ArgumentException("目录标题、行高或列数无效。");
            var unique = new HashSet<CatalogField>();
            foreach(var c in Columns)
                if (c==null || !Enum.IsDefined(typeof(CatalogField),c.Field) || !unique.Add(c.Field)
                    || string.IsNullOrWhiteSpace(c.Header) || c.Header.Length>100 || !Finite(c.Width) || c.Width<6 || c.Width>100)
                    throw new ArgumentException("目录字段重复，或列名/列宽无效。");
        }
        internal static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
    }
    public sealed class PaperUsage
    {
        public string Name { get; internal set; } = "";
        [DataMember(IsRequired=true)] public double WidthMm { get; internal set; }
        public double HeightMm { get; internal set; }
        public int Count { get; internal set; }
        public double AreaSquareMetres => WidthMm*HeightMm*Count/1000000;
        public double EquivalentA1 => AreaSquareMetres/PaperUsageCalculator.A1AreaSquareMetres;
    }
    public static class PaperUsageCalculator
    {
        public const double A1AreaSquareMetres=841.0*594.0/1000000;
        public static List<PaperUsage> Calculate(IEnumerable<PlotFrame> frames)
        {
            var snapshot=frames.ToList();
            foreach(var f in snapshot)
                if(f?.DetectedPaper==null || !CatalogOptions.Finite(f.DetectedPaper.WidthMm) || !CatalogOptions.Finite(f.DetectedPaper.HeightMm)
                    || f.DetectedPaper.WidthMm<=0 || f.DetectedPaper.HeightMm<=0
                    || !CatalogOptions.Finite(f.DetectedPaper.WidthMm*f.DetectedPaper.HeightMm))
                    throw new ArgumentException("存在无效纸张尺寸，未导出可能低估的统计。");
            return snapshot.GroupBy(f=>new {f.DetectedPaper.Name, Width=f.DetectedPaper.LongerEdgeMm,Height=f.DetectedPaper.ShorterEdgeMm})
                .Select(g=>new PaperUsage {Name=g.Key.Name,WidthMm=g.Key.Width,HeightMm=g.Key.Height,Count=g.Count()})
                .OrderByDescending(g=>g.WidthMm*g.HeightMm).ThenBy(g=>g.Name,StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
