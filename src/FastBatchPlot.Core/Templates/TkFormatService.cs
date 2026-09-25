using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FastBatchPlot.Core.Export;

namespace FastBatchPlot.Core.Templates
{
    /// <summary>
    /// 原版批打印“图框信息配置文件(.tk)”中的一条图框记录（以 &lt;&gt;&lt; 分隔）：
    /// 00 块名；01 文件命名规则（ABCDEFT）；02 录入时块旋转角(弧度)；03 录入时块插入比例；
    /// 04/05 纸张宽高(mm)；06~09 录入时块范围；10~45 九个信息框（图号、版次、图名、日期、信息1、信息2…）；
    /// 46 排序优先等级；47 标志；48~56 其他设置；57 以后为部分版本追加的字体等设置（原样保留）。
    /// 坐标均为录入时的世界坐标（已含插入比例与旋转）。
    /// </summary>
    public sealed class TkTemplateRecord
    {
        public string BlockName { get; set; } = "";
        public string NamingRule { get; set; } = "";
        public double Rotation { get; set; }
        public double Scale { get; set; } = 1;
        public double PaperWidth { get; set; }
        public double PaperHeight { get; set; }
        public double BBoxMinX { get; set; }
        public double BBoxMinY { get; set; }
        public double BBoxMaxX { get; set; }
        public double BBoxMaxY { get; set; }
        public TemplateRegion?[] FieldSlots { get; } = new TemplateRegion?[9];
        public int Priority { get; set; } = 2;
        public int Line48 { get; set; } = 1;
        public int[] ExtraZeros { get; set; } = new int[9];
        /// <summary>第 58 行以后的附加设置（如字体、线型），导出时原样写回。</summary>
        public List<string> TrailingLines { get; set; } = new List<string>();
        // 原始文本，未被程序修改的字段按原文写回，避免 4.71239 等数值在往返中丢失精度。
        internal string RotationText = "0", ScaleText = "1";
    }

    public sealed class TkFile
    {
        public const int StandardRecordLines = 57;
        public string Version { get; set; } = "20260805";
        public List<TkTemplateRecord> Templates { get; } = new List<TkTemplateRecord>();
        /// <summary>模板之后的线型等设置节（部分版本没有）。</summary>
        public List<string> Section26 { get; set; } = new List<string>();
        /// <summary>末尾加长图幅规则节。</summary>
        public List<string> Section27 { get; set; } = new List<string>();

        public static Encoding GetGbkEncoding()
        {
#if NET8_0_OR_GREATER
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
            return Encoding.GetEncoding(936);
        }

        public static TkFile Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("文件路径不能为空。", nameof(filePath));
            return Parse(File.ReadAllText(filePath, GetGbkEncoding()));
        }

        public void Save(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("文件路径不能为空。", nameof(filePath));
            string dir = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            // 原版插件要求 GBK + CRLF。
            File.WriteAllText(filePath, Serialize(), GetGbkEncoding());
        }

        public static TkFile Parse(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) throw new InvalidDataException("图框配置文件内容为空。");
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                .Select(l => l.Trim()).ToList();
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                lines.RemoveAt(lines.Count - 1);

            if (lines.Count < 2) throw new InvalidDataException("图框配置文件头部无效。");

            var file = new TkFile { Version = lines[0] };
            if (!int.TryParse(lines[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int templateCount) || templateCount < 0)
                throw new InvalidDataException("图框配置文件模板数量无效：" + lines[1]);

            // 按分隔符切分，记录长度可变（部分记录附带字体设置）。
            var segments = new List<List<string>>();
            for (int i = 2; i < lines.Count; i++)
            {
                if (lines[i] == "<><") { segments.Add(new List<string>()); continue; }
                if (segments.Count == 0) throw new InvalidDataException("第 1 个模板分节标记错误，应为 '<><'，实际为 '" + lines[i] + "'");
                segments[segments.Count - 1].Add(lines[i]);
            }
            if (segments.Count < templateCount)
                throw new InvalidDataException($"模板数量不匹配，期望 {templateCount} 个，仅读取到 {segments.Count} 个。");

            for (int i = 0; i < templateCount; i++)
            {
                var segment = segments[i];
                if (segment.Count < 6) throw new InvalidDataException($"第 {i + 1} 个模板记录不完整。");
                file.Templates.Add(ParseRecord(segment, i + 1));
            }

            var trailing = segments.Skip(templateCount).Select(s => new List<string> { "<><" }.Concat(s).ToList()).ToList();
            if (trailing.Count == 1) file.Section27 = trailing[0];
            else if (trailing.Count >= 2)
            {
                file.Section26 = trailing.Take(trailing.Count - 1).SelectMany(s => s).ToList();
                file.Section27 = trailing[trailing.Count - 1];
            }
            return file;
        }

        private static TkTemplateRecord ParseRecord(List<string> lines, int ordinal)
        {
            string Get(int offset) => offset < lines.Count ? lines[offset] : "0";
            double GetDouble(int offset)
            {
                string s = Get(offset);
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && !double.IsNaN(v) && !double.IsInfinity(v)) return v;
                throw new InvalidDataException($"第 {ordinal} 个模板第 {offset + 1} 行不是有效数值：{s}");
            }
            int GetInt(int offset)
            {
                string s = Get(offset);
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;
                return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? (int)Math.Round(d) : 0;
            }

            var rec = new TkTemplateRecord
            {
                BlockName = Get(0),
                NamingRule = Get(1),
                Rotation = GetDouble(2),
                Scale = GetDouble(3),
                PaperWidth = GetDouble(4),
                PaperHeight = GetDouble(5),
                BBoxMinX = GetDouble(6),
                BBoxMinY = GetDouble(7),
                BBoxMaxX = GetDouble(8),
                BBoxMaxY = GetDouble(9),
                Priority = GetInt(46),
                Line48 = GetInt(47),
                RotationText = Get(2),
                ScaleText = Get(3)
            };
            if (string.IsNullOrWhiteSpace(rec.BlockName)) throw new InvalidDataException($"第 {ordinal} 个模板缺少块名。");

            for (int s = 0; s < 9; s++)
            {
                int slotStart = 10 + s * 4;
                double x1 = GetDouble(slotStart), y1 = GetDouble(slotStart + 1), x2 = GetDouble(slotStart + 2), y2 = GetDouble(slotStart + 3);
                if (Math.Abs(x1) > 1e-6 || Math.Abs(y1) > 1e-6 || Math.Abs(x2) > 1e-6 || Math.Abs(y2) > 1e-6)
                    rec.FieldSlots[s] = new TemplateRegion { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 };
            }

            for (int z = 0; z < 9; z++) rec.ExtraZeros[z] = GetInt(48 + z);
            if (lines.Count > StandardRecordLines) rec.TrailingLines = lines.Skip(StandardRecordLines).ToList();
            return rec;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            void Line(string text) => sb.Append(text).Append("\r\n");
            Line(Version);
            Line(Templates.Count.ToString(CultureInfo.InvariantCulture));

            foreach (var t in Templates)
            {
                Line("<><");
                Line(t.BlockName);
                Line(string.IsNullOrWhiteSpace(t.NamingRule) ? "A-C" : t.NamingRule);
                Line(SameNumber(t.RotationText, t.Rotation) ? t.RotationText : FormatNum(t.Rotation));
                Line(SameNumber(t.ScaleText, t.Scale) ? t.ScaleText : FormatNum(t.Scale));
                Line(FormatNum(t.PaperWidth));
                Line(FormatNum(t.PaperHeight));
                Line(FormatNum(t.BBoxMinX));
                Line(FormatNum(t.BBoxMinY));
                Line(FormatNum(t.BBoxMaxX));
                Line(FormatNum(t.BBoxMaxY));

                for (int s = 0; s < 9; s++)
                {
                    var slot = t.FieldSlots[s];
                    if (slot != null) { Line(FormatNum(slot.X1)); Line(FormatNum(slot.Y1)); Line(FormatNum(slot.X2)); Line(FormatNum(slot.Y2)); }
                    else for (int k = 0; k < 4; k++) Line("0");
                }

                Line(t.Priority.ToString(CultureInfo.InvariantCulture));
                Line(t.Line48.ToString(CultureInfo.InvariantCulture));
                for (int z = 0; z < 9; z++)
                    Line(t.ExtraZeros != null && z < t.ExtraZeros.Length ? t.ExtraZeros[z].ToString(CultureInfo.InvariantCulture) : "0");
                if (t.TrailingLines != null) foreach (var extra in t.TrailingLines) Line(extra);
            }

            // 原文件有什么节就写回什么节；全新文件才写入默认节。
            bool fresh = (Section26 == null || Section26.Count == 0) && (Section27 == null || Section27.Count == 0);
            foreach (var l in fresh ? DefaultSection26() : (Section26 ?? new List<string>())) Line(l);
            foreach (var l in fresh ? DefaultSection27() : (Section27 ?? new List<string>())) Line(l);
            return sb.ToString();
        }

        private static bool SameNumber(string text, double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) && Math.Abs(parsed - value) <= 1e-12 * Math.Max(1, Math.Abs(value));

        private static string FormatNum(double d)
        {
            if (Math.Abs(d) < 1e-9) return "0";
            if (Math.Abs(d - Math.Round(d)) < 1e-9) return ((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture);
            return d.ToString("0.######", CultureInfo.InvariantCulture);
        }

        public static List<string> DefaultSection26()
        {
            return new List<string>
            {
                "<><", "1", "_", "2", "0", "0", "0", "0", "0", "0",
                "1", "Continuous", "-3", "1", "1000", "0", "0", "0"
            };
        }

        public static List<string> DefaultSection27()
        {
            return new List<string>
            {
                "<><", "1", "_", "2", "0", "0", "0", "0", "0", "0",
                "A-C", "+", "1", "0", "47",
                "A3+1∕2", "A3+1", "A3+3∕2", "A3+2", "A3+5∕2", "A3+3", "A3+7∕2",
                "A2+1∕8", "A2+1∕4", "A2+3∕8", "A2+1∕2", "A2+5∕8", "A2+3∕4", "A2+7∕8",
                "A2+1", "A2+9∕8", "A2+5∕4", "A2+11∕8", "A2+3∕2", "A2+13∕8", "A2+7∕4",
                "A2+15∕8", "A2+2", "A2+17∕8", "A2+9∕4", "A2+19∕8", "A2+5∕2",
                "A1+1∕8", "A1+1∕4", "A1+3∕8", "A1+1∕2", "A1+5∕8", "A1+3∕4", "A1+7∕8",
                "A1+1", "A1+9∕8", "A1+5∕4", "A1+11∕8", "A1+3∕2",
                "A0+1∕8", "A0+1∕4", "A0+3∕8", "A0+1∕2", "A0+5∕8", "A0+3∕4", "A0+7∕8", "A0+1"
            };
        }

        // 信息框顺序与命名规则字母一致：A 图号、B 版次、C 图名、D 日期、E 信息1、F 信息2。
        private static readonly (int Slot, TitleField Field)[] SlotFields =
        {
            (0, TitleField.DrawingNo), (1, TitleField.Revision), (2, TitleField.DrawingName),
            (3, TitleField.Date), (4, TitleField.ProjectName), (5, TitleField.SubProject)
        };

        /// <summary>把录入时的世界坐标（含旋转与插入比例）换算成块内本地坐标。</summary>
        private static (double X, double Y) ToLocal(TkTemplateRecord rec, double x, double y)
        {
            double scale = Math.Abs(rec.Scale) > 1e-12 ? Math.Abs(rec.Scale) : 1;
            double cos = Math.Cos(-rec.Rotation), sin = Math.Sin(-rec.Rotation);
            return ((x * cos - y * sin) / scale, (x * sin + y * cos) / scale);
        }

        private static (double X, double Y) ToWorld(TkTemplateRecord rec, double x, double y)
        {
            double scale = Math.Abs(rec.Scale) > 1e-12 ? Math.Abs(rec.Scale) : 1;
            double cos = Math.Cos(rec.Rotation), sin = Math.Sin(rec.Rotation);
            return ((x * cos - y * sin) * scale, (x * sin + y * cos) * scale);
        }

        private static (double MinX, double MinY, double MaxX, double MaxY) LocalBox(TkTemplateRecord rec, double x1, double y1, double x2, double y2)
        {
            var corners = new[] { ToLocal(rec, x1, y1), ToLocal(rec, x2, y1), ToLocal(rec, x2, y2), ToLocal(rec, x1, y2) };
            return (corners.Min(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.X), corners.Max(c => c.Y));
        }

        public TitleTemplateLibrary ToLibrary()
        {
            var lib = new TitleTemplateLibrary();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rec in Templates)
            {
                var box = LocalBox(rec, rec.BBoxMinX, rec.BBoxMinY, rec.BBoxMaxX, rec.BBoxMaxY);
                double bw = box.MaxX - box.MinX, bh = box.MaxY - box.MinY;
                if (!(bw > 0 && bh > 0)) throw new InvalidDataException("图框 " + rec.BlockName + " 的范围无效。");

                // 同名块（动态块的多个尺寸）各自成为一个模板，名称追加尺寸以示区分。
                string name = rec.BlockName;
                if (!names.Add(name))
                {
                    name = rec.BlockName + " (" + FormatNum(rec.PaperWidth) + "x" + FormatNum(rec.PaperHeight) + ")";
                    for (int n = 2; !names.Add(name); n++) name = rec.BlockName + " (" + n + ")";
                }

                var template = new TitleBlockTemplate
                {
                    Name = name,
                    BlockName = rec.BlockName,
                    Priority = Math.Max(0, Math.Min(10000, rec.Priority)),
                    PaperWidth = rec.PaperWidth > 0 ? rec.PaperWidth : bw,
                    PaperHeight = rec.PaperHeight > 0 ? rec.PaperHeight : bh,
                    PrintScale = 0,
                    NamingTemplate = ConvertNamingRule(rec.NamingRule),
                    PrintRegion = new TemplateRegion { X1 = -bw, Y1 = 0, X2 = 0, Y2 = bh }
                };

                foreach (var (s, field) in SlotFields)
                {
                    var slot = rec.FieldSlots[s];
                    if (slot == null) continue;
                    var local = LocalBox(rec, slot.X1, slot.Y1, slot.X2, slot.Y2);
                    // 以块范围右下角为基准，与宿主 TemplateAnchor 一致。
                    double fx1 = local.MinX - box.MaxX, fx2 = local.MaxX - box.MaxX;
                    double fy1 = local.MinY - box.MinY, fy2 = local.MaxY - box.MinY;
                    if (fx2 > fx1 && fy2 > fy1)
                        template.Fields.Add(new TitleFieldRule
                        {
                            Field = field,
                            Region = new TemplateRegion { X1 = Math.Round(fx1, 4), Y1 = Math.Round(fy1, 4), X2 = Math.Round(fx2, 4), Y2 = Math.Round(fy2, 4) }
                        });
                }

                lib.Templates.Add(template);
            }

            TitleTemplateService.Validate(lib);
            return lib;
        }

        /// <summary>
        /// 原版命名规则：A 图号、B 版次、C 图名、D 日期、E 信息1、F 信息2、T 图幅；
        /// 连写两个相同字母表示该字母本身（如 FF 表示 F），其他字符原样保留。
        /// </summary>
        public static string ConvertNamingRule(string rule)
        {
            if (string.IsNullOrWhiteSpace(rule)) return "{图号}-{图名}";
            var map = new Dictionary<char, string>
            {
                ['A'] = "{图号}", ['B'] = "{版次}", ['C'] = "{图名}", ['D'] = "{日期}",
                ['E'] = "{信息1}", ['F'] = "{信息2}", ['T'] = "{图幅}"
            };
            var sb = new StringBuilder();
            for (int i = 0; i < rule.Length; i++)
            {
                char c = rule[i];
                if (map.TryGetValue(c, out var token))
                {
                    if (i + 1 < rule.Length && rule[i + 1] == c) { sb.Append(c); i++; }
                    else sb.Append(token);
                }
                else if (c == '{' || c == '}') sb.Append('_');
                else sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>把占位符命名模板转换回原版字母规则；包含无法表示的占位符时返回 null。</summary>
        public static string? ToNamingRule(string? template)
        {
            if (string.IsNullOrWhiteSpace(template)) return null;
            var map = new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase)
            {
                ["图号"] = 'A', ["DwgNo"] = 'A', ["版次"] = 'B', ["Rev"] = 'B', ["图名"] = 'C', ["图面名称"] = 'C', ["DwgName"] = 'C',
                ["日期"] = 'D', ["Date"] = 'D', ["信息1"] = 'E', ["信息2"] = 'F', ["图幅"] = 'T', ["PaperSize"] = 'T'
            };
            var sb = new StringBuilder();
            for (int i = 0; i < template!.Length; i++)
            {
                char c = template[i];
                if (c == '{')
                {
                    int end = template.IndexOf('}', i);
                    if (end < 0 || !map.TryGetValue(template.Substring(i + 1, end - i - 1), out char letter)) return null;
                    sb.Append(letter); i = end;
                }
                else if ("ABCDEFT".IndexOf(c) >= 0) sb.Append(c).Append(c);
                else sb.Append(c);
            }
            return sb.ToString();
        }

        public static TkFile FromLibrary(TitleTemplateLibrary library, TkFile? baseline = null)
        {
            if (library == null) throw new ArgumentNullException(nameof(library));
            TitleTemplateService.Validate(library);

            var file = new TkFile
            {
                Version = baseline?.Version ?? "20260805",
                Section26 = baseline != null ? new List<string>(baseline.Section26) : new List<string>(),
                Section27 = baseline != null ? new List<string>(baseline.Section27) : new List<string>()
            };

            // 基准记录按块名与纸张尺寸对应，动态块的多个尺寸不会互相覆盖。
            var remaining = baseline?.Templates.ToList() ?? new List<TkTemplateRecord>();

            foreach (var t in library.Templates)
            {
                var baseRec = remaining.FirstOrDefault(r => string.Equals(r.BlockName, t.BlockName, StringComparison.OrdinalIgnoreCase) &&
                        Math.Abs(r.PaperWidth - t.PaperWidth) < 0.5 && Math.Abs(r.PaperHeight - t.PaperHeight) < 0.5)
                    ?? remaining.FirstOrDefault(r => string.Equals(r.BlockName, t.BlockName, StringComparison.OrdinalIgnoreCase));
                if (baseRec != null) remaining.Remove(baseRec);

                double pw = t.PaperWidth > 0 ? t.PaperWidth : (t.PrintRegion != null ? Math.Abs(t.PrintRegion.X2 - t.PrintRegion.X1) : 420);
                double ph = t.PaperHeight > 0 ? t.PaperHeight : (t.PrintRegion != null ? Math.Abs(t.PrintRegion.Y2 - t.PrintRegion.Y1) : 297);

                var rec = new TkTemplateRecord
                {
                    BlockName = t.BlockName,
                    NamingRule = ToNamingRule(t.NamingTemplate) ?? baseRec?.NamingRule ?? "A-C",
                    Rotation = baseRec?.Rotation ?? 0,
                    Scale = baseRec?.Scale ?? 1,
                    RotationText = baseRec?.RotationText ?? "0",
                    ScaleText = baseRec?.ScaleText ?? "1",
                    PaperWidth = pw,
                    PaperHeight = ph,
                    Priority = t.Priority,
                    Line48 = baseRec?.Line48 ?? 1,
                    ExtraZeros = baseRec?.ExtraZeros != null ? (int[])baseRec.ExtraZeros.Clone() : new int[9],
                    TrailingLines = baseRec?.TrailingLines != null ? new List<string>(baseRec.TrailingLines) : new List<string>()
                };

                // 块范围：沿用基准记录；新模板以本地范围居中写出（插入比例 1、无旋转）。
                double minX, minY, maxX, maxY;
                if (baseRec != null) { minX = baseRec.BBoxMinX; minY = baseRec.BBoxMinY; maxX = baseRec.BBoxMaxX; maxY = baseRec.BBoxMaxY; }
                else
                {
                    double w = t.PrintRegion != null ? Math.Abs(t.PrintRegion.X2 - t.PrintRegion.X1) : pw;
                    double h = t.PrintRegion != null ? Math.Abs(t.PrintRegion.Y2 - t.PrintRegion.Y1) : ph;
                    minX = -w / 2.0; minY = -h / 2.0; maxX = w / 2.0; maxY = h / 2.0;
                }
                rec.BBoxMinX = minX; rec.BBoxMinY = minY; rec.BBoxMaxX = maxX; rec.BBoxMaxY = maxY;
                var box = LocalBox(rec, minX, minY, maxX, maxY);

                foreach (var rule in t.Fields)
                {
                    int slotIndex = Array.FindIndex(SlotFields, p => p.Field == rule.Field);
                    if (slotIndex < 0) continue;
                    var p1 = ToWorld(rec, rule.Region.X1 + box.MaxX, rule.Region.Y1 + box.MinY);
                    var p2 = ToWorld(rec, rule.Region.X2 + box.MaxX, rule.Region.Y2 + box.MinY);
                    rec.FieldSlots[SlotFields[slotIndex].Slot] = new TemplateRegion
                    {
                        X1 = Math.Round(Math.Min(p1.X, p2.X), 4), Y1 = Math.Round(Math.Min(p1.Y, p2.Y), 4),
                        X2 = Math.Round(Math.Max(p1.X, p2.X), 4), Y2 = Math.Round(Math.Max(p1.Y, p2.Y), 4)
                    };
                }
                // 未映射的信息框（信息3 等）沿用原记录。
                if (baseRec != null)
                    for (int s = 6; s < 9; s++) rec.FieldSlots[s] = baseRec.FieldSlots[s];

                file.Templates.Add(rec);
            }

            return file;
        }
    }

    public static class TkFormatService
    {
        public static TitleTemplateLibrary Load(string filePath) => TkFile.Load(filePath).ToLibrary();

        public static void Save(string filePath, TitleTemplateLibrary library, TkFile? baseline = null)
            => TkFile.FromLibrary(library, baseline).Save(filePath);
    }
}
