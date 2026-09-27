using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Naming;
using FastBatchPlot.Core.Paper;

namespace FastBatchPlot.Core.Templates
{
    /// <summary>原版信息框：字母同时用于文件命名规则。</summary>
    public sealed class FrameInfoSlot
    {
        internal FrameInfoSlot(char letter, TitleField field, string label) { Letter = letter; Field = field; Label = label; }
        public char Letter { get; }
        public TitleField Field { get; }
        public string Label { get; }
        public string Caption => Label + "(" + Letter + ")";
    }

    /// <summary>
    /// 仿原版“图框信息库管理 / 录入新图框”的纸张、命名规则与记录换算。
    /// 区域仍以模板本地右下角为基准，不改变字段提取与 .tk 读写规则。
    /// </summary>
    public static class FrameLibrary
    {
        public const int DefaultPriority = 2;
        private const double StandardPaperTolerance = 1.5, SamePaperTolerance = 0.5;

        // 与 .tk 信息框和命名字母一致：A 图号、B 版次、C 图名、D 日期、E 信息1、F 信息2。
        public static readonly IReadOnlyList<FrameInfoSlot> Slots = new[]
        {
            new FrameInfoSlot('A', TitleField.DrawingNo, "图号"), new FrameInfoSlot('B', TitleField.Revision, "版次"),
            new FrameInfoSlot('C', TitleField.DrawingName, "图名"), new FrameInfoSlot('D', TitleField.Date, "日期"),
            new FrameInfoSlot('E', TitleField.ProjectName, "信息1"), new FrameInfoSlot('F', TitleField.SubProject, "信息2")
        };

        public static PaperSize? StandardPaper(double width, double height)
            => width > 0 && height > 0 ? PaperSize.StandardSizes.FirstOrDefault(p =>
                Math.Abs(p.LongerEdgeMm - Math.Max(width, height)) <= StandardPaperTolerance
                && Math.Abs(p.ShorterEdgeMm - Math.Min(width, height)) <= StandardPaperTolerance) : null;

        /// <summary>“对应纸张”列：标准及加长图幅显示名称，其余显示宽×高；未登记纸张时出图按图框自动识别。</summary>
        public static string PaperLabel(double width, double height)
            => !(width > 0 && height > 0) ? "自动识别" : StandardPaper(width, height)?.Name ?? Mm(width) + "×" + Mm(height);

        public static string PaperDescription(double width, double height)
        {
            if (!(width > 0 && height > 0)) return "自动识别（未登记纸张）";
            string size = Mm(width) + "×" + Mm(height) + "mm";
            var standard = StandardPaper(width, height);
            return standard == null ? "自定义（" + size + "）" : standard.Name + "（" + size + "）";
        }

        /// <summary>按图框尺寸推断对应纸张（方向与图框一致）；不能可靠识别时返回 null，由用户选择。</summary>
        public static PaperSize? ProposePaper(double width, double height)
        {
            if (!TitleTemplateService.Finite(width) || !TitleTemplateService.Finite(height) || width <= 0 || height <= 0) return null;
            var detected = PaperSizeDetector.Detect(width, height);
            return detected.MatchScore <= PaperSizeDetector.AcceptableMatchError ? detected.Paper : null;
        }

        /// <summary>“文件命名规则”列：能用字母表示时显示原版规则（如 A-C），否则显示占位符原文；空表示使用通用命名。</summary>
        public static string NamingRuleText(string? namingTemplate)
            => string.IsNullOrWhiteSpace(namingTemplate) ? "" : TkFile.ToNamingRule(namingTemplate) ?? namingTemplate!.Trim();

        /// <summary>原版字母规则转换为命名模板；空白表示使用通用命名，含花括号时按占位符模板原样保存。</summary>
        public static string? NamingTemplateFromRule(string? rule)
        {
            if (string.IsNullOrWhiteSpace(rule)) return null;
            string text = rule!.Trim();
            return text.IndexOf('{') >= 0 || text.IndexOf('}') >= 0 ? text : TkFile.ConvertNamingRule(text);
        }

        /// <summary>按固定示例信息生成输出文件名，供命名规则对话框预览。</summary>
        public static string PreviewFileName(string? rule)
        {
            var template = NamingTemplateFromRule(rule);
            if (template == null) return "（未定义，使用主界面通用命名）";
            var sample = new PlotFrame { OrderIndex = 1, DetectedPaper = new PaperSize("A1", 841, 594, true),
                TitleInfo = new TitleBlockInfo { DrawingNo = "建施03", Revision = "A", DrawingName = "一层建筑平面图",
                    Date = "2026.09", ProjectName = "信息1", SubProject = "信息2" } };
            return DrawingNameFormatter.Format(template, sample);
        }

        public static bool HasField(TitleBlockTemplate template, TitleField field) => template.Fields.Any(f => f.Field == field);

        /// <summary>点取的区域取代原有规则（含属性标签规则）。</summary>
        public static void SetFieldRegion(TitleBlockTemplate template, TitleField field, TemplateRegion region)
        {
            template.Fields.RemoveAll(f => f.Field == field);
            template.Fields.Add(new TitleFieldRule { Field = field, AttributeTag = "", Region = TemplateCropGeometry.Copy(region) });
            template.Fields.Sort((a, b) => a.Field.CompareTo(b.Field));
        }

        public static void ClearField(TitleBlockTemplate template, TitleField field) => template.Fields.RemoveAll(f => f.Field == field);

        /// <summary>从已录图框复制文件命名规则与信息框（区域以右下角为基准，原样复制）。</summary>
        public static void CopyInformation(TitleBlockTemplate source, TitleBlockTemplate target)
        {
            var copy = TitleTemplateService.Clone(source);
            target.NamingTemplate = copy.NamingTemplate;
            target.Fields = copy.Fields;
        }

        /// <summary>
        /// 同一图块、同一纸张（不分方向）的已录记录；动态块的不同尺寸是不同记录。
        /// 没有同纸张记录时，未登记纸张的同块旧记录视为同一图框。
        /// </summary>
        public static TitleBlockTemplate? FindRecorded(TitleTemplateLibrary library, string blockName, double width, double height)
        {
            var sameBlock = library.Templates.Where(t => string.Equals(t.BlockName.Trim(), blockName.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            return sameBlock.FirstOrDefault(t => SamePaper(t.PaperWidth, t.PaperHeight, width, height))
                ?? sameBlock.FirstOrDefault(t => !(t.PaperWidth > 0 && t.PaperHeight > 0));
        }

        /// <summary>模板名称须唯一：默认用块名，同一图块的其他尺寸追加纸张。</summary>
        public static string UniqueName(TitleTemplateLibrary library, string blockName, double width, double height)
        {
            var names = new HashSet<string>(library.Templates.Select(t => t.Name.Trim()), StringComparer.OrdinalIgnoreCase);
            string root = blockName.Trim(), name = root;
            if (names.Contains(name)) name = root + " (" + PaperLabel(width, height) + ")";
            for (int n = 2; names.Contains(name); n++) name = root + " (" + n.ToString(CultureInfo.InvariantCulture) + ")";
            return name;
        }

        /// <summary>按拾取的样本图框建立录入草稿：排序优先级别默认 2，命名规则未定义时使用通用命名。</summary>
        public static TitleBlockTemplate CreateDraft(TitleTemplateLibrary library, string blockName, PaperSize? paper, TemplateRegion? frameRegion)
        {
            if (string.IsNullOrWhiteSpace(blockName)) throw new ArgumentException("图框对应的图块不能为空。");
            var draft = new TitleBlockTemplate { BlockName = blockName.Trim(), Priority = DefaultPriority,
                PaperWidth = paper?.WidthMm ?? 0, PaperHeight = paper?.HeightMm ?? 0,
                PrintRegion = frameRegion == null ? null : TemplateCropGeometry.Copy(frameRegion) };
            draft.Name = UniqueName(library, draft.BlockName, draft.PaperWidth, draft.PaperHeight);
            return draft;
        }

        /// <summary>
        /// 导入设置：合并时同一图块同一纸张的记录由导入内容替换，沿用原名称与标识；
        /// 其余追加，名称或标识冲突时重新分配。不合并时只保留导入内容。
        /// </summary>
        public static TitleTemplateLibrary Merge(TitleTemplateLibrary current, TitleTemplateLibrary imported, bool keepCurrent)
        {
            TitleTemplateService.Validate(imported);
            var merged = keepCurrent ? TitleTemplateService.CopyLibrary(current) : new TitleTemplateLibrary();
            var settled = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in imported.Templates)
            {
                var template = TitleTemplateService.Clone(source); template.Id = source.Id;
                int index = merged.Templates.FindIndex(t => !settled.Contains(t.Id) && SameFrame(t, template));
                if (index >= 0)
                {
                    template.Id = merged.Templates[index].Id; template.Name = merged.Templates[index].Name;
                    merged.Templates[index] = template; settled.Add(template.Id); continue;
                }
                if (merged.Templates.Any(t => t.Id == template.Id)) template.Id = Guid.NewGuid().ToString("N");
                if (merged.Templates.Any(t => string.Equals(t.Name.Trim(), template.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                    template.Name = UniqueName(merged, template.BlockName, template.PaperWidth, template.PaperHeight);
                merged.Templates.Add(template); settled.Add(template.Id);
            }
            TitleTemplateService.Validate(merged);
            return merged;
        }

        private static bool SameFrame(TitleBlockTemplate a, TitleBlockTemplate b)
            => string.Equals(a.BlockName.Trim(), b.BlockName.Trim(), StringComparison.OrdinalIgnoreCase)
                && SamePaper(a.PaperWidth, a.PaperHeight, b.PaperWidth, b.PaperHeight);

        private static bool SamePaper(double w1, double h1, double w2, double h2)
            => Math.Abs(Math.Max(w1, h1) - Math.Max(w2, h2)) <= SamePaperTolerance && Math.Abs(Math.Min(w1, h1) - Math.Min(w2, h2)) <= SamePaperTolerance;

        private static string Mm(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
