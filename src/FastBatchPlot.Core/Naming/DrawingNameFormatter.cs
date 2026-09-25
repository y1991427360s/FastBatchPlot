using System;
using System.IO;
using System.Text.RegularExpressions;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Naming
{
    /// <summary>
    /// 图纸文件命名与书签标题生成引擎
    /// </summary>
    public static class DrawingNameFormatter
    {
        private static readonly Regex InvalidCharsRegex = new Regex(@"[\\/:*?""<>|]", RegexOptions.Compiled);

        /// <summary>
        /// 根据命名模板及图框属性格式化输出文件名 (不含扩展名)
        /// </summary>
        public static string Format(string template, PlotFrame frame, string dwgFileName = "")
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                template = "{Index:D2}_{DwgNo}_{DwgName}";
            }

            string result = Expand(template, frame, dwgFileName, false);

            // 清理非法文件名字符
            result = CleanFileName(result);

            // 去除多余连续下划线或空格
            result = Regex.Replace(result, @"_{2,}", "_").Trim('_', ' ');

            if (string.IsNullOrWhiteSpace(result))
            {
                result = $"Plot_{frame.OrderIndex:D2}";
            }

            // 过长的图名会超出 Windows 路径长度；保留前 120 个字符，末尾不留空格或句点。
            if (result.Length > MaximumFileNameLength) result = result.Substring(0, MaximumFileNameLength).TrimEnd(' ', '.', '_');
            // CON、PRN、COM1 等系统保留名不能作为文件名，追加下划线而不是让整批失败。
            string stem = result.Split('.')[0].Trim().ToUpperInvariant();
            if (Regex.IsMatch(stem, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$")) result += "_";
            return result.TrimEnd('.', ' ');
        }

        public const int MaximumFileNameLength = 120;
        public const string DefaultBookmarkTemplate = "{Index:D2} {DwgNo} {DwgName}";
        public const int MaximumBookmarkLength = 32768;

        public static void ValidateBookmarkTemplate(string template)
        {
            if (template == null || template.Length > MaximumBookmarkLength)
                throw new ArgumentException("书签模板不能为空值或超过 32768 个字符。");
            string literal = Regex.Replace(template, @"\{([^{}]+)\}", "");
            if (literal.IndexOfAny(new[] { '{', '}' }) >= 0)
                throw new ArgumentException("书签模板的花括号不完整。");
            Expand(template, new PlotFrame(), "", true);
        }

        /// <summary>只使用任务图框快照，不依赖当前时间、CAD 或当前打开文件。</summary>
        public static string FormatBookmark(string template, PlotFrame frame)
        {
            ValidateBookmarkTemplate(template);
            if (string.IsNullOrWhiteSpace(template)) template = DefaultBookmarkTemplate;
            string result = Regex.Replace(Expand(template, frame, "", true), @"[\p{Cc}\p{Zl}\p{Zp}]", " ").Trim();
            if (result.Length == 0) result = "图纸 " + frame.OrderIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (result.Length > MaximumBookmarkLength) throw new ArgumentException("生成的书签标题超过 32768 个字符。");
            return result;
        }

        private static string Expand(string template, PlotFrame frame, string dwgFileName, bool bookmark)
        {
            // 只扫描用户模板一次：图纸文字里的花括号不是第二层模板。
            return Regex.Replace(template, @"\{([^{}]+)\}", match =>
            {
                string token = match.Groups[1].Value;
                var indexMatch = Regex.Match(token, @"^(?:Index|序号)(?::D(\d+))?$", RegexOptions.IgnoreCase);
                if (indexMatch.Success)
                {
                    int digits = 2;
                    if (indexMatch.Groups[1].Success && (!int.TryParse(indexMatch.Groups[1].Value, out digits) || digits < 1 || digits > 9))
                        return bookmark ? throw new ArgumentException("书签序号格式须为 D1 到 D9。") : match.Value;
                    return frame.OrderIndex.ToString("D" + digits, System.Globalization.CultureInfo.InvariantCulture);
                }
                switch (token.ToUpperInvariant())
                {
                    case "DWGNO": case "图号":
                        return string.IsNullOrWhiteSpace(frame.TitleInfo.DrawingNo) ? "图纸" + frame.OrderIndex : frame.TitleInfo.DrawingNo;
                    case "DWGNAME": case "图名": case "图面名称":
                        return string.IsNullOrWhiteSpace(frame.TitleInfo.DrawingName) ? "图面" + frame.OrderIndex : frame.TitleInfo.DrawingName;
                    case "PROJECTNAME": case "项目": case "项目名称": case "工程名称": case "信息1": return frame.TitleInfo.ProjectName ?? "";
                    case "SUBPROJECT": case "子项": case "信息2": return frame.TitleInfo.SubProject ?? "";
                    case "PAPERSIZE": case "图幅": case "纸张": return frame.DetectedPaper?.Name ?? "A1";
                    case "PAPERDIMENSIONS": case "纸张尺寸":
                        return frame.DetectedPaper == null ? "" : frame.DetectedPaper.WidthMm.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                            + "x" + frame.DetectedPaper.HeightMm.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    case "SCALE": case "比例": case "出图比例": return (bookmark ? "1:" : "1-") + (frame.CalculatedScale > 0 ? frame.CalculatedScale.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture) : "100");
                    case "DATE": case "日期": case "出图日期": return frame.TitleInfo.Date ?? "";
                    case "REV": case "版次": case "版本": return frame.TitleInfo.Revision ?? "";
                    case "LAYOUT": case "布局": return frame.LayoutName ?? "";
                    case "DWGFILENAME": case "文件名": case "图纸文件":
                        string source = string.IsNullOrWhiteSpace(frame.SourceFileName) ? dwgFileName : frame.SourceFileName;
                        return string.IsNullOrWhiteSpace(source) ? "Drawing" : Path.GetFileNameWithoutExtension(source);
                    default: return bookmark ? throw new ArgumentException("未知书签占位符：" + match.Value) : match.Value;
                }
            });

        }

        public static string CleanFileName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            string clean = InvalidCharsRegex.Replace(raw, "_");
            return clean.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }
}
