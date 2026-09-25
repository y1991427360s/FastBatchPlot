using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FastBatchPlot.UI.Views
{
    public sealed class PdfTaskHistoryEntry
    {
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public string Created { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Error { get; set; } = "";
    }

    /// <summary>只选择由调用方加载的记录；不读取任务文件，不提交任何打印操作。</summary>
    public sealed class PdfTaskHistoryForm : Form
    {
        private readonly DataGridView tasks = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, RowHeadersVisible = false, MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false,
            BackgroundColor = SystemColors.Window, BorderStyle = BorderStyle.FixedSingle
        };
        private readonly TextBox details = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle };
        private readonly Button view = new Button { Text = "查看任务", AutoSize = true, Enabled = false };
        private readonly Button cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
        public string SelectedPath { get; private set; } = "";

        public PdfTaskHistoryForm(IEnumerable<PdfTaskHistoryEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            Text = "PDF任务历史"; Size = new Size(900, 550); MinimumSize = new Size(720, 450);
            StartPosition = FormStartPosition.CenterParent; Font = new Font("Microsoft YaHei UI", 9);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 5 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.Controls.Add(new Label { Text = "选择一条历史任务查看完成情况及失败原因。", Dock = DockStyle.Fill }, 0, 0);
            tasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "TaskName", HeaderText = "任务名称", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 28, MinimumWidth = 130 });
            tasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Created", HeaderText = "创建时间", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 24, MinimumWidth = 140 });
            tasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Summary", HeaderText = "任务状态 / 读取错误", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 48, MinimumWidth = 200 });
            foreach (DataGridViewColumn column in tasks.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                var copy = new PdfTaskHistoryEntry { Path = entry.Path ?? "", Name = entry.Name ?? "", Created = entry.Created ?? "", Summary = entry.Summary ?? "", Error = entry.Error ?? "" };
                bool broken = !string.IsNullOrWhiteSpace(copy.Error);
                int index = tasks.Rows.Add(copy.Name, copy.Created, broken ? "读取失败：" + copy.Error : copy.Summary);
                var row = tasks.Rows[index]; row.Tag = copy;
                row.Cells[2].ToolTipText = broken ? copy.Error : copy.Summary;
                if (broken) row.DefaultCellStyle.ForeColor = Color.Firebrick;
            }
            root.Controls.Add(tasks, 0, 1); root.Controls.Add(details, 0, 2);
            root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "查看历史不会打印。失败重试只复用已核验的单页 PDF，来源图纸仍需有效。\n读取失败的记录不能打开；完整错误显示在上方详情中。" }, 0, 3);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            footer.Controls.Add(cancel); footer.Controls.Add(view); root.Controls.Add(footer, 0, 4);
            Controls.Add(root); AcceptButton = view; CancelButton = cancel;
            tasks.SelectionChanged += (s, e) => UpdateSelection();
            view.Click += (s, e) => ViewSelected();
            tasks.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) ViewSelected(); };
            tasks.ClearSelection();
            if (tasks.Rows.Count > 0) tasks.Rows[0].Selected = true;
            UpdateSelection();
        }

        private PdfTaskHistoryEntry? CurrentEntry()
            => tasks.SelectedRows.Count == 1 ? tasks.SelectedRows[0].Tag as PdfTaskHistoryEntry : null;
        private void UpdateSelection()
        {
            SelectedPath = "";
            var entry = CurrentEntry();
            view.Enabled = entry != null && string.IsNullOrWhiteSpace(entry.Error) && !string.IsNullOrWhiteSpace(entry.Path);
            if (entry == null) { details.Text = tasks.Rows.Count == 0 ? "暂无 PDF 任务历史。" : "请选择一条任务记录。"; return; }
            details.Text = !string.IsNullOrWhiteSpace(entry.Error) ? "读取失败：" + entry.Error + Environment.NewLine + "记录路径：" + entry.Path
                : string.IsNullOrWhiteSpace(entry.Path) ? "记录缺少任务路径，无法查看。" : entry.Summary + Environment.NewLine + "记录路径：" + entry.Path;
        }
        private void ViewSelected()
        {
            var entry = CurrentEntry();
            if (entry == null || !string.IsNullOrWhiteSpace(entry.Error) || string.IsNullOrWhiteSpace(entry.Path)) { UpdateSelection(); return; }
            SelectedPath = entry.Path; DialogResult = DialogResult.OK; Close();
        }
    }
}
