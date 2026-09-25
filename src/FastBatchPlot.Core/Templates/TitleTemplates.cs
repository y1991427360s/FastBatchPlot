using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Export;

namespace FastBatchPlot.Core.Templates
{
    public enum TitleField { DrawingNo, DrawingName, ProjectName, SubProject, Stage, Discipline, Revision, Date, Scale, Designer, Checker, Approver }
    [DataContract]
    public sealed class TemplateRegion
    {
        [DataMember(IsRequired = true)] public double X1 { get; set; }
        [DataMember(IsRequired = true)] public double Y1 { get; set; }
        [DataMember(IsRequired = true)] public double X2 { get; set; }
        [DataMember(IsRequired = true)] public double Y2 { get; set; }
    }
    [DataContract]
    public sealed class TitleFieldRule
    {
        [DataMember(IsRequired = true)] public TitleField Field { get; set; }
        [DataMember(IsRequired = true)] public TemplateRegion Region { get; set; } = new TemplateRegion();
        [DataMember(IsRequired = true)] public string AttributeTag { get; set; } = "";
    }
    [DataContract]
    public sealed class TitleBlockTemplate
    {
        [DataMember(EmitDefaultValue=false)] public TemplateRegion? StampRegion {get;set;}
        [DataMember(EmitDefaultValue=false)] public TemplateRegion? RegistrationStampRegion {get;set;}
        [DataMember(EmitDefaultValue=false)] public double PrintScale {get;set;}
        [DataMember(EmitDefaultValue=false)] public TemplateRegion? PrintRegion {get;set;}
        [DataMember(EmitDefaultValue=false)] public string? NamingTemplate {get;set;}
        [DataMember(EmitDefaultValue=false)] public CatalogOptions? Catalog {get;set;}
        [DataMember(EmitDefaultValue=false)] public double PaperWidth {get;set;}
        [DataMember(EmitDefaultValue=false)] public double PaperHeight {get;set;}
        [DataMember(IsRequired = true)] public string Id { get; set; } = Guid.NewGuid().ToString("N");
        [DataMember(IsRequired = true)] public string Name { get; set; } = "";
        [DataMember(IsRequired = true)] public string BlockName { get; set; } = "";
        [DataMember(IsRequired = true)] public int Priority { get; set; } = 100;
        [DataMember(IsRequired = true)] public List<TitleFieldRule> Fields { get; set; } = new List<TitleFieldRule>();
        public override string ToString() => Name + " [" + BlockName + "]";
    }
    [DataContract]
    public sealed class TitleTemplateLibrary
    {
        [DataMember(IsRequired = true)] public int SchemaVersion { get; set; } = 1;
        [DataMember(IsRequired = true)] public List<TitleBlockTemplate> Templates { get; set; } = new List<TitleBlockTemplate>();
    }
    public sealed class TemplateText
    {
        public string Handle {get;set;} = "";
        public string Owner {get;set;} = "";
        public string Kind {get;set;} = "";
        public string RawText {get;set;} = "";
        public string WriteBlockReason {get;set;} = "未提供写回定位信息。";
        public string Text { get; set; } = "";
        public string AttributeTag { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public TemplateRegion? Bounds { get; set; }
    }
    public sealed class TemplateExtractionResult
    {
        public TitleBlockInfo Info { get; internal set; } = new TitleBlockInfo();
        public List<string> Warnings { get; } = new List<string>();
    }
    public static class TitleTemplateService
    {
        public static readonly string[] FieldLabels = { "图号", "图名", "工程名称", "子项", "阶段", "专业", "版次", "日期", "比例", "设计", "校对", "审核" };
        public static void Validate(TitleTemplateLibrary library)
        {
            if (library == null || library.SchemaVersion != 1 || library.Templates == null)
                throw new InvalidDataException("模板库版本或结构无效，当前仅支持版本 1。");
            if (library.Templates.Count > 1000) throw new InvalidDataException("模板数量超过 1000。");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in library.Templates)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.Id) || string.IsNullOrWhiteSpace(t.Name) || string.IsNullOrWhiteSpace(t.BlockName)
                    || t.Id.Length > 100 || t.Name.Length > 256 || t.BlockName.Length > 256 || t.Priority < 0 || t.Priority > 10000
                    || t.Fields == null || t.Fields.Count > 12 || !ids.Add(t.Id) || !names.Add(t.Name.Trim()))
                    throw new InvalidDataException("模板名称、块名、优先级或字段无效，模板 ID 和模板名称不可重复。");
                if(!Finite(t.PrintScale)||t.PrintScale<0||t.PrintScale>1000000)throw new InvalidDataException("模板出图比例须为0（自动）或不超过1000000的正数。");
                try{if(t.PrintRegion!=null)TemplateCropGeometry.Validate(t.PrintRegion);if(t.StampRegion!=null)TemplateCropGeometry.Validate(t.StampRegion);if(t.RegistrationStampRegion!=null)TemplateCropGeometry.Validate(t.RegistrationStampRegion);}catch(ArgumentException ex){throw new InvalidDataException(ex.Message,ex);}
                if(t.NamingTemplate!=null && (t.NamingTemplate.Length>512 || t.NamingTemplate.Any(char.IsControl)))
                    throw new InvalidDataException("模板命名规则最长512字符，不能含控制字符。");
                try{t.Catalog?.Validate();}catch(ArgumentException ex){throw new InvalidDataException("模板目录设置无效："+ex.Message,ex);}
                var fields = new HashSet<TitleField>();
                foreach (var rule in t.Fields)
                {
                    if (rule == null || !Enum.IsDefined(typeof(TitleField), rule.Field) || !fields.Add(rule.Field)
                        || rule.AttributeTag == null || rule.AttributeTag.Length > 256 || rule.Region == null)
                        throw new InvalidDataException("字段无效或重复。");
                    var r = rule.Region;
                    if (!Finite(r.X1) || !Finite(r.X2) || !Finite(r.Y1) || !Finite(r.Y2)
                        || (string.IsNullOrWhiteSpace(rule.AttributeTag) && (r.X1 >= r.X2 || r.Y1 >= r.Y2)))
                        throw new InvalidDataException("区域坐标须为有限数值，右上坐标须大于左下坐标；或指定属性标签。");
                }
            }
        }
        public static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);

        /// <summary>
        /// 旧版本导入 .tk 时把“文件命名规则”（如 A-C、A-MLE）误存成了块名，图框型模式因此找不到任何图框。
        /// 检测到这种特征时返回提示文字（不自动改写用户模板库）。
        /// </summary>
        public static string? DescribeLegacyTkImport(TitleTemplateLibrary library)
        {
            if (library?.Templates == null) return null;
            var suspicious = library.Templates
                .Where(t => string.IsNullOrWhiteSpace(t.NamingTemplate) && System.Text.RegularExpressions.Regex.IsMatch(t.BlockName.Trim(), @"^[A-F T]([-_][A-Z]{1,4})*$"))
                .GroupBy(t => t.BlockName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() >= 3).ToList();
            if (suspicious.Count == 0) return null;
            return "模板库疑似由旧版本从 .tk 错误导入：" + string.Join("、", suspicious.Select(g => "“" + g.Key + "”×" + g.Count()))
                + " 实为文件命名规则而非块名，图框型模式将找不到图框。请在【图框模板】中重新导入原 .tk 文件（选择“否”完全替换）。";
        }
        public static TitleBlockTemplate Clone(TitleBlockTemplate source)
        {
            return new TitleBlockTemplate {
                StampRegion=source.StampRegion==null?null:TemplateCropGeometry.Copy(source.StampRegion),
                RegistrationStampRegion=source.RegistrationStampRegion==null?null:TemplateCropGeometry.Copy(source.RegistrationStampRegion),
                PrintScale=source.PrintScale,
                PrintRegion=source.PrintRegion==null?null:TemplateCropGeometry.Copy(source.PrintRegion),
                NamingTemplate=source.NamingTemplate,
                Catalog=source.Catalog?.Copy(),
                PaperWidth=source.PaperWidth,
                PaperHeight=source.PaperHeight,
                Name = source.Name,
                BlockName = source.BlockName,
                Priority = source.Priority,
                Fields = source.Fields.Select(f => new TitleFieldRule { Field = f.Field, AttributeTag = f.AttributeTag,
                    Region = new TemplateRegion { X1 = f.Region.X1, Y1 = f.Region.Y1, X2 = f.Region.X2, Y2 = f.Region.Y2 } }).ToList()
            };
        }
        public static TitleTemplateLibrary CopyLibrary(TitleTemplateLibrary source)
        {
            Validate(source);
            return new TitleTemplateLibrary { Templates = source.Templates.Select(t => { var copy = Clone(t); copy.Id = t.Id; return copy; }).ToList() };
        }
        public static TemplateExtractionResult Extract(TitleBlockTemplate template, IEnumerable<TemplateText> texts, TitleBlockInfo existing)
        {
            Validate(new TitleTemplateLibrary { Templates = new List<TitleBlockTemplate> { template } });
            if (texts == null || existing == null) throw new ArgumentNullException();
            var result = new TemplateExtractionResult();
            foreach (TitleField field in Enum.GetValues(typeof(TitleField))) Set(result.Info, field, Get(existing, field));
            result.Info.RawAttributes = new Dictionary<string, string>(existing.RawAttributes, StringComparer.OrdinalIgnoreCase);
            var items = texts.ToList();
            if (items.Any(t => t == null || !Finite(t.X) || !Finite(t.Y) || t.Text == null || t.AttributeTag == null))
                throw new InvalidDataException("文字采集含无效内容或坐标。");
            foreach (var rule in template.Fields)
            {
                bool byTag = !string.IsNullOrWhiteSpace(rule.AttributeTag);
                var r = rule.Region;
                var matches = items.Where(t => !string.IsNullOrWhiteSpace(t.Text) && (byTag
                    ? string.Equals(t.AttributeTag, rule.AttributeTag.Trim(), StringComparison.OrdinalIgnoreCase)
                    : t.X >= r.X1 && t.X <= r.X2 && t.Y >= r.Y1 && t.Y <= r.Y2))
                    .OrderByDescending(t => t.Y).ThenBy(t => t.X).ToList();
                // 保持中心点匹配优先；旧 TK 的框选区域有时仅覆盖文字的一部分。
                // 空区域只接纳唯一相交对象，且不能借用中心已落入其他字段的文字。
                if (!byTag && matches.Count == 0)
                {
                    var crossing = items.Where(t => !string.IsNullOrWhiteSpace(t.Text) && Intersects(t.Bounds, r)
                        && !template.Fields.Any(other => other != rule && string.IsNullOrWhiteSpace(other.AttributeTag)
                            && t.X >= other.Region.X1 && t.X <= other.Region.X2
                            && t.Y >= other.Region.Y1 && t.Y <= other.Region.Y2)).ToList();
                    if (crossing.Count == 1) matches = crossing;
                    else if (crossing.Count > 1)
                    {
                        result.Warnings.Add(FieldLabels[(int)rule.Field] + "区域与多项边界文字相交，保留原值。");
                        continue;
                    }
                }
                if (matches.Count == 0) { result.Warnings.Add(FieldLabels[(int)rule.Field] + "未找到文字，保留原值。"); continue; }
                if (byTag && matches.Count > 1) { result.Warnings.Add(FieldLabels[(int)rule.Field] + "属性标签重复，保留原值。"); continue; }
                Set(result.Info, rule.Field, string.Join(" ", matches.Select(t => t.Text.Trim().Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' '))));
            }
            return result;
        }
        private static bool Intersects(TemplateRegion? a, TemplateRegion b)
            => a != null && Finite(a.X1) && Finite(a.X2) && Finite(a.Y1) && Finite(a.Y2)
                && a.X1 < a.X2 && a.Y1 < a.Y2
                && Math.Min(a.X2, b.X2) > Math.Max(a.X1, b.X1)
                && Math.Min(a.Y2, b.Y2) > Math.Max(a.Y1, b.Y1);
        public static string Get(TitleBlockInfo info, TitleField field)
            => (string)typeof(TitleBlockInfo).GetProperty(field.ToString())!.GetValue(info)!;
        private static void Set(TitleBlockInfo info, TitleField field, string value)
            => typeof(TitleBlockInfo).GetProperty(field.ToString())!.SetValue(info, value);
        public static TitleBlockTemplate? FindBestTemplate(IEnumerable<TitleBlockTemplate> templates, double width, double height)
        {
            if (templates == null) return null;
            var list = templates.ToList();
            if (list.Count == 0) return null;
            if (list.Count == 1) return list[0];
            if (width > 0 && height > 0)
            {
                double cadLonger = Math.Max(width, height);
                double cadShorter = Math.Min(width, height);
                double cadRatio = cadLonger / Math.Max(1.0, cadShorter);

                var scored = list.Select(t =>
                {
                    double tw = t.PaperWidth > 0 ? t.PaperWidth : (t.PrintRegion != null ? Math.Abs(t.PrintRegion.X2 - t.PrintRegion.X1) : 0);
                    double th = t.PaperHeight > 0 ? t.PaperHeight : (t.PrintRegion != null ? Math.Abs(t.PrintRegion.Y2 - t.PrintRegion.Y1) : 0);
                    double score = double.MaxValue;
                    if (tw > 0 && th > 0)
                    {
                        double stdLonger = Math.Max(tw, th);
                        double stdShorter = Math.Min(tw, th);
                        double stdRatio = stdLonger / stdShorter;

                        double ratioDiff = Math.Abs(cadRatio - stdRatio) / stdRatio;
                        if (ratioDiff <= 0.10)
                        {
                            double rawScale = cadShorter / stdShorter;
                            double scale = Paper.ScaleCalculator.MatchClosestStandardScale(rawScale);
                            if (scale <= 0) scale = Math.Round(rawScale);
                            if (scale > 0)
                            {
                                double expLonger = stdLonger * scale;
                                double expShorter = stdShorter * scale;
                                double errLonger = Math.Abs(cadLonger - expLonger) / expLonger;
                                double errShorter = Math.Abs(cadShorter - expShorter) / expShorter;
                                score = (errLonger + errShorter) / 2.0;

                                if ((width >= height) != (tw >= th))
                                {
                                    score += 1.0;
                                }
                            }
                        }
                    }
                    return new { Template = t, Score = score, Priority = t.Priority };
                }).OrderBy(x => x.Score).ThenBy(x => x.Priority).ToList();

                if (scored[0].Score < 0.10) return scored[0].Template;
            }
            return list.OrderBy(t => t.Priority).FirstOrDefault();
        }
    }
}
