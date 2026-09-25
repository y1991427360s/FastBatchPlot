using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.Core.Tasks;

namespace FastBatchPlot.UI.Views
{
    /// <summary>只读呈现历史快照并返回用户动作，不校验文件或调用宿主。</summary>
    public sealed class PdfTaskDetailsForm : Form
    {
        private readonly TextBox summary = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical };
        private readonly DataGridView pages = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, RowHeadersVisible = false, MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false,
            BackgroundColor = SystemColors.Window, BorderStyle = BorderStyle.FixedSingle
        };
        private readonly TextBox pageDetails = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical };
        private readonly Label instructions = new Label { Dock = DockStyle.Fill };
        private readonly Button retry = new Button { Text = "重试未完成页", AutoSize = true };
        private readonly Button merge = new Button { Text = "重新合并完整单页", AutoSize = true };
        private readonly Button close = new Button { Text = "关闭", AutoSize = true, DialogResult = DialogResult.Cancel };
        private readonly bool canRetry;
        private readonly bool canMerge;

        public PdfTaskDetailsForm(PdfTaskRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            var config = record.Config;
            if (record.Pages == null) throw new ArgumentException("任务缺少页面列表。", nameof(record));
            bool complete = record.Pages.Count > 0 && record.Pages.All(p => p.State == BatchPageState.Succeeded);
            canRetry = record.Pages.Count > 0 && !complete;
            canMerge = complete && config.MergeToSinglePdf && !string.IsNullOrWhiteSpace(record.MergedOutputPath) && record.MergeState != PdfTaskMergeState.Succeeded;
            Text = "PDF任务详情"; Size = new Size(1160, 740); MinimumSize = new Size(1000, 640);
            StartPosition = FormStartPosition.CenterParent; Font = new Font("Microsoft YaHei UI", 9);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 5 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 140)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            summary.Text = "任务：" + record.Id + "　共 " + record.Pages.Count + " 页" + Environment.NewLine
                + "创建：" + LocalTime(record.CreatedUtcTicks) + "　更新：" + LocalTime(record.UpdatedUtcTicks) + Environment.NewLine
                + "原设备：" + config.PrinterDevice + "　原打印样式：" + config.PlotStyleTable + Environment.NewLine
                + "原 PDF 参数：" + config.PdfOptions + Environment.NewLine
                + "无留白外边线："+(config.PrintOuterBorderLine?"保留":"裁切 "+config.OuterBorderInsetMm.ToString("0.##")+" mm")+Environment.NewLine
                + "原输出目录：" + config.OutputDirectory + Environment.NewLine
                + "合并：" + (config.MergeToSinglePdf ? MergeStateText(record.MergeState) : "原任务未启用合并") + "　目标：" + record.MergedOutputPath
                + (string.IsNullOrWhiteSpace(record.MergeError) ? "" : Environment.NewLine + "合并错误：" + record.MergeError);
            AddColumn("Order", "序号", 6, 45); AddColumn("DrawingNo", "图号", 12, 75); AddColumn("DrawingName", "图名", 16, 110);
            AddColumn("PaperScale", "纸张 / 比例", 13, 105); AddColumn("State", "状态", 10, 75);
            AddColumn("Error", "错误", 20, 150); AddColumn("OutputPath", "单页输出路径", 23, 180);
            foreach (var page in record.Pages)
            {
                var frame = page.Frame;
                string error = page.Error ?? "", path = page.OutputPath ?? "";
                int index = pages.Rows.Add(frame.OrderIndex, frame.TitleInfo.DrawingNo, frame.TitleInfo.DrawingName,
                    frame.DetectedPaper.Name + " / 1:" + frame.CalculatedScale.ToString("0.####"), PageStateText(page.State), error, path);
                var row = pages.Rows[index]; row.Tag = "来源文件：" + (string.IsNullOrWhiteSpace(frame.SourceFileName) ? "旧记录未保存" : frame.SourceFileName)
                    + "　布局：" + frame.LayoutName + Environment.NewLine + "错误：" + (string.IsNullOrWhiteSpace(error) ? "无" : error) + Environment.NewLine + "单页输出路径：" + path;
                row.Cells[5].ToolTipText = error; row.Cells[6].ToolTipText = path;
                if (page.State == BatchPageState.Failed) row.DefaultCellStyle.ForeColor = Color.Firebrick;
            }
            instructions.Text = "操作复用原任务设置，与当前主窗口的更改无关；点击按钮后仍须通过校验，查看本身不会输出。\n"
                + "重试要求原图会话与修改记录一致；重新打开 CAD 或修改图纸后，请新建批次。单页校验失败不会覆盖已有文件。\n"
                + "会话修改检查不覆盖外参、字体、打印样式文件的外部更改；这些文件改动后，请新建整批输出。\n"
                + (record.MergeState == PdfTaskMergeState.Succeeded ? "合并已成功，禁止再次合并以免覆盖。" : complete ? "单页已全部成功；重新合并只核验历史 PDF 文件，不重新验证原 DWG 内容。" : "仍有未完成页，完成并核验全部单页后才允许合并。");
            root.Controls.Add(summary, 0, 0); root.Controls.Add(pages, 0, 1); root.Controls.Add(pageDetails, 0, 2); root.Controls.Add(instructions, 0, 3);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            retry.Enabled = canRetry; merge.Enabled = canMerge;
            retry.Click += (s, e) => RetryIncomplete(); merge.Click += (s, e) => MergeComplete();
            footer.Controls.Add(close); footer.Controls.Add(merge); footer.Controls.Add(retry); root.Controls.Add(footer, 0, 4);
            Controls.Add(root); CancelButton = close;
            // Enter 不绑定任何输出动作，需用户明确点击重试或合并。
            pages.SelectionChanged += (s, e) => DisplayPage();
            pages.ClearSelection(); if (pages.Rows.Count > 0) pages.Rows[0].Selected = true; DisplayPage();
        }
        private void AddColumn(string name, string text, float weight, int minimum)
            => pages.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = text, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = weight, MinimumWidth = minimum, SortMode = DataGridViewColumnSortMode.NotSortable });
        private static string LocalTime(long ticks)
            => ticks > 0 && ticks <= DateTime.MaxValue.Ticks ? new DateTimeOffset(ticks, TimeSpan.Zero).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") : "无效时间";
        private static string PageStateText(BatchPageState state)
        {
            switch (state)
            {
                case BatchPageState.Pending: return "待处理";
                case BatchPageState.Running: return "执行中 / 待核验";
                case BatchPageState.Succeeded: return "成功";
                case BatchPageState.Failed: return "失败";
                case BatchPageState.Cancelled: return "已取消";
                case BatchPageState.NotSubmitted: return "未提交";
                default: return "未知状态";
            }
        }
        private static string MergeStateText(PdfTaskMergeState state)
        {
            switch (state)
            {
                case PdfTaskMergeState.Pending: return "待合并";
                case PdfTaskMergeState.Running: return "执行中 / 待核验";
                case PdfTaskMergeState.Succeeded: return "成功";
                case PdfTaskMergeState.Failed: return "失败";
                case PdfTaskMergeState.Cancelled: return "已取消";
                default: return "未知状态";
            }
        }
        private void DisplayPage() => pageDetails.Text = pages.SelectedRows.Count == 1 ? pages.SelectedRows[0].Tag as string ?? "" : "请选择一页查看完整错误及输出路径。";
        private void RetryIncomplete() { if (!canRetry) return; DialogResult = DialogResult.Retry; Close(); }
        private void MergeComplete() { if (!canMerge) return; DialogResult = DialogResult.Yes; Close(); }
    }
}
