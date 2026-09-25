using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Export
{
    /// <summary>
    /// 图纸目录与图幅统计导出器 (支持Excel CSV与HTML报告)
    /// </summary>
    public static class CatalogExporter
    {
        public static void ExportToCsv(IEnumerable<PlotFrame> frames, string filePath)
        {
            frames = frames.ToList();
            var sb = new StringBuilder();
            // 写入 CSV 表头
            sb.AppendLine("序号,图号,图名,图幅,比例,版次,出图日期,状态,输出文件名");

            foreach (var f in frames)
            {
                string line = string.Join(",", new string[]
                {
                    f.OrderIndex.ToString(),
                    EscapeCsv(f.TitleInfo.DrawingNo),
                    EscapeCsv(f.TitleInfo.DrawingName),
                    EscapeCsv(f.DetectedPaper?.Name ?? "-"),
                    EscapeCsv(f.CalculatedScale > 0 ? FormattableString.Invariant($"1:{f.CalculatedScale:0.##}") : "-"),
                    EscapeCsv(f.TitleInfo.Revision),
                    EscapeCsv(f.TitleInfo.Date),
                    EscapeCsv(f.Status),
                    EscapeCsv(f.CustomOutputFileName)
                });
                sb.AppendLine(line);
            }

            // 附带图幅统计
            sb.AppendLine();
            sb.AppendLine("【图幅统计汇总】");
            var stats = GetPaperStatistics(frames);
            foreach (var kvp in stats)
            {
                sb.AppendLine($"{EscapeCsv(kvp.Key)},{kvp.Value} 张");
            }

            // 写入带 UTF-8 BOM 的文件，确保 Excel 直接双击打开不乱码
            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
        }

        public static Dictionary<string, int> GetPaperStatistics(IEnumerable<PlotFrame> frames)
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in frames)
            {
                string paper = f.DetectedPaper?.Name ?? "未知";
                if (!dict.ContainsKey(paper))
                    dict[paper] = 0;
                dict[paper]++;
            }
            return dict;
        }

        private static string EscapeCsv(string? val)
        {
            if (string.IsNullOrEmpty(val)) return "";
            // Excel 的 CSV 自动公式解释：来自图纸的文本始终按文字导出。
            string trimmed = val!.TrimStart();
            if (trimmed.Length > 0 && "=+-@".IndexOf(trimmed[0]) >= 0) val = "'" + val;
            if (val!.Contains(",") || val.Contains("\"") || val.Contains("\n") || val.Contains("\r"))
            {
                return $"\"{val.Replace("\"", "\"\"")}\"";
            }
            return val;
        }

        public static void ExportConfiguredCsv(IEnumerable<PlotFrame> frames, string path, CatalogOptions options, bool overwrite=false)
        {
            options.Validate();
            if(!string.Equals(Path.GetExtension(path),".csv",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("CSV 目录须使用 .csv 扩展名。");
            var rows=frames.Where(f=>!options.SelectedOnly||f.IsSelected).ToList();
            if(rows.Count==0||rows.Count>100000)throw new ArgumentException("目录需包含 1 到 100000 张图纸。");
            var usage=PaperUsageCalculator.Calculate(rows);
            var text=new StringBuilder();
            text.AppendLine(string.Join(",",options.Columns.Select(c=>EscapeCsv(c.Header))));
            foreach(var f in rows)text.AppendLine(string.Join(",",options.Columns.Select(c=>EscapeCsv(CatalogOptions.Value(f,c.Field)))));
            if(options.IncludeStatistics)
            {
                text.AppendLine();text.AppendLine("图幅,长边(mm),短边(mm),张数,面积(m²),折合A1(张)");
                foreach(var u in usage)text.AppendLine(string.Join(",",EscapeCsv(u.Name),u.WidthMm.ToString(System.Globalization.CultureInfo.InvariantCulture),u.HeightMm.ToString(System.Globalization.CultureInfo.InvariantCulture),u.Count.ToString(),u.AreaSquareMetres.ToString("0.######",System.Globalization.CultureInfo.InvariantCulture),u.EquivalentA1.ToString("0.######",System.Globalization.CultureInfo.InvariantCulture)));
                text.AppendLine("折算口径：按面积除以841×594mm，不计份数、留白和损耗");
            }
            string full=Path.GetFullPath(path),directory=Path.GetDirectoryName(full)!;
            if(File.Exists(full)&&!overwrite)throw new IOException("目录文件已存在，未覆盖。");
            Directory.CreateDirectory(directory);string temp=Path.Combine(directory,".catalog-"+Guid.NewGuid().ToString("N")+".csv");
            try
            {
                File.WriteAllText(temp,text.ToString(),new UTF8Encoding(true));
                if(overwrite&&File.Exists(full))File.Replace(temp,full,null);else File.Move(temp,full);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
