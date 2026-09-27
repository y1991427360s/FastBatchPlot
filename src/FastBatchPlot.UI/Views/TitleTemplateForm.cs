using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;

namespace FastBatchPlot.UI.Views
{
    /// <summary>
    /// 图框信息库管理（仿原版）：每行一个已录图框，双击空白处录入新图框、双击记录修改、点“删除”移除；
    /// 底部只保留录入新图框、导出设置、导入设置、确定、取消。区域坐标与字段提取规则不变。
    /// </summary>
    public sealed class TitleTemplateForm : Form
    {
        private const int DeleteColumn = 4, NamingColumn = 5;
        public TitleTemplateLibrary Library { get; private set; }
        private readonly DataGridView frames = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            BackgroundColor = SystemColors.Window, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize, ShowCellToolTips = false };
        private readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 26, AutoEllipsis = true, ForeColor = SystemColors.GrayText,
            Padding = new Padding(12, 5, 12, 0), Text = "双击空白处录入新图框，双击已有行修改，点“删除”移除；修改在点击“确定”后保存。" };
        private readonly ToolTip tips = new ToolTip();
        private readonly Func<string, PlotFrame?> findSampleFrame;
        private readonly Func<PlotFrame?> pickSampleFrame;
        private readonly Func<PlotFrame, TemplateRegion?> pickRegion;
        private readonly Func<PlotFrame, TemplateRegion?> frameRegion;
        private readonly string? savePath;
        private bool dirty;
        // 模态交互集中于此，离线检查可替换，避免阻塞。
        private Func<FrameEntryForm, DialogResult> showEntry;
        private Func<string, bool> confirm;

        public TitleTemplateForm(TitleTemplateLibrary library, string? savePath, Func<PlotFrame?> pickSampleFrame,
            Func<PlotFrame, TemplateRegion?> pickRegion)
            : this(library, savePath, _ => null, pickSampleFrame, pickRegion)
        {
        }

        public TitleTemplateForm(TitleTemplateLibrary library, string? savePath, Func<string, PlotFrame?> findSampleFrame,
            Func<PlotFrame?> pickSampleFrame, Func<PlotFrame, TemplateRegion?> pickRegion, Func<PlotFrame, TemplateRegion?>? frameRegion = null)
        {
            Library = TitleTemplateService.CopyLibrary(library);
            this.findSampleFrame = findSampleFrame; this.pickSampleFrame = pickSampleFrame; this.pickRegion = pickRegion;
            this.frameRegion = frameRegion ?? (_ => null); this.savePath = savePath;
            showEntry = entry => entry.ShowDialog(this);
            confirm = message => MessageBox.Show(this, message, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
            Text = "图框信息库管理"; Size = new Size(960, 520); MinimumSize = new Size(760, 380);
            StartPosition = FormStartPosition.CenterParent; Font = new Font("Microsoft YaHei UI", 9);

            string[] headers = { "序号", "图框对应的图块", "对应纸张", "排序优先级别", "删除", "文件命名规则" };
            int[] widths = { 48, 150, 90, 90, 52, 100 };
            for (int i = 0; i < headers.Length; i++)
                frames.Columns.Add(i == DeleteColumn
                    ? new DataGridViewLinkColumn { HeaderText = headers[i], Width = widths[i], Text = "删除", UseColumnTextForLinkValue = true, LinkColor = Color.Firebrick,
                        ActiveLinkColor = Color.Red, VisitedLinkColor = Color.Firebrick, TrackVisitedState = false, LinkBehavior = LinkBehavior.HoverUnderline }
                    : (DataGridViewColumn)new DataGridViewTextBoxColumn { HeaderText = headers[i], Width = widths[i] });
            foreach (var slot in FrameLibrary.Slots) frames.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = slot.Caption, Width = 62 });
            frames.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; frames.Columns[1].MinimumWidth = 110;
            foreach (DataGridViewColumn column in frames.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            frames.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            frames.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(236, 236, 236);
            frames.RowTemplate.Height = 24;
            frames.CellClick += (s, e) => Run(() => { if (e.RowIndex >= 0 && e.ColumnIndex == DeleteColumn) DeleteFrame(e.RowIndex); });
            frames.CellDoubleClick += (s, e) => Run(() => { if (e.RowIndex >= 0 && e.ColumnIndex != DeleteColumn) EditFrame(e.RowIndex); });
            frames.MouseDoubleClick += (s, e) => Run(() => { if (frames.HitTest(e.X, e.Y).Type == DataGridViewHitTestType.None) NewFrame(); });
            frames.KeyDown += (s, e) => Run(() =>
            {
                if (e.KeyCode != Keys.Delete || frames.CurrentRow == null) return;
                e.Handled = true; DeleteFrame(frames.CurrentRow.Index);
            });
            tips.SetToolTip(frames, "双击空白处录入新图框；双击已有行修改该图框");

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 12, 12, 0) };
            body.Controls.Add(frames);
            var buttons = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 54, ColumnCount = 5, RowCount = 1, Padding = new Padding(8, 4, 8, 6) };
            for (int i = 0; i < 5; i++) buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            AddButton(buttons, "录入新图框", FrameLibraryStyle.ActionIcon, NewFrame, "在 CAD 中选择图框对应的图块，录入纸张、命名规则和信息框");
            AddButton(buttons, "导出设置", FrameLibraryStyle.ActionIcon, ExportSettings, "导出为图框信息配置文件（.tk）或 JSON，供其他电脑导入");
            AddButton(buttons, "导入设置", FrameLibraryStyle.ActionIcon, ImportSettings, "导入 .tk 或 JSON 图框信息");
            AddButton(buttons, "确  定", FrameLibraryStyle.OkIcon, SaveAndClose, "保存图框信息库并关闭");
            var cancel = FrameLibraryStyle.CreateButton("取  消", FrameLibraryStyle.CancelIcon);
            cancel.DialogResult = DialogResult.Cancel; cancel.Anchor = AnchorStyles.None;
            buttons.Controls.Add(cancel);
            CancelButton = cancel;
            Controls.Add(body); Controls.Add(status); Controls.Add(buttons);
            RefreshRows();
        }

        private void AddButton(TableLayoutPanel panel, string text, Image icon, Action action, string tip)
        {
            var button = FrameLibraryStyle.CreateButton(text, icon);
            button.Anchor = AnchorStyles.None;
            button.Click += (s, e) => Run(action);
            tips.SetToolTip(button, tip);
            panel.Controls.Add(button);
        }

        private void Run(Action action)
        {
            try { action(); }
            catch (Exception ex) { status.ForeColor = Color.Firebrick; status.Text = ex.Message; return; }
            status.ForeColor = SystemColors.GrayText;
        }

        private void RefreshRows(TitleBlockTemplate? select = null)
        {
            frames.Rows.Clear();
            for (int i = 0; i < Library.Templates.Count; i++)
            {
                var template = Library.Templates[i];
                string rule = FrameLibrary.NamingRuleText(template.NamingTemplate);
                var cells = new List<object> { i + 1, template.BlockName, FrameLibrary.PaperLabel(template.PaperWidth, template.PaperHeight),
                    template.Priority, "删除", rule.Length > 0 ? rule : "通用" };
                cells.AddRange(FrameLibrary.Slots.Select(slot => (object)(FrameLibrary.HasField(template, slot.Field) ? "√" : "")));
                var row = frames.Rows[frames.Rows.Add(cells.ToArray())];
                row.Tag = template;
                if (rule.Length == 0) row.Cells[NamingColumn].Style.ForeColor = SystemColors.GrayText;
            }
            var target = frames.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => select != null && ReferenceEquals(r.Tag, select));
            if (target != null) frames.CurrentCell = target.Cells[1];
            else frames.ClearSelection();
        }

        /// <summary>录入新图框：先在 CAD 中选择图框对应的图块，按图框自动识别纸张；同块同纸张已录入时改为修改原记录。</summary>
        private void NewFrame()
        {
            var sample = FrameLibraryStyle.HiddenWhile(this, pickSampleFrame);
            if (sample == null) { status.Text = "已取消录入新图框。"; return; }
            if (sample.Type != FrameType.BlockReference || string.IsNullOrWhiteSpace(sample.SourceBlockName))
                throw new InvalidOperationException("请选择图框对应的图块。");
            TemplateRegion? region = null;
            string notice = "";
            try { region = frameRegion(sample); }
            catch (Exception ex) { notice = "未能读取图框范围（" + ex.Message + "），将按整个图块打印。"; }
            var paper = (region == null ? null : FrameLibrary.ProposePaper(region.X2 - region.X1, region.Y2 - region.Y1))
                ?? FrameLibrary.ProposePaper(sample.Width, sample.Height);
            var recorded = FrameLibrary.FindRecorded(Library, sample.SourceBlockName, paper?.WidthMm ?? 0, paper?.HeightMm ?? 0);
            if (recorded != null)
            {
                var existing = Draft(recorded);
                if (!(existing.PaperWidth > 0 && existing.PaperHeight > 0) && paper != null) { existing.PaperWidth = paper.WidthMm; existing.PaperHeight = paper.HeightMm; }
                OpenEntry(existing, sample, "图块“" + recorded.BlockName + "”已录入，已打开原记录修改。");
                return;
            }
            if (paper == null) notice = "未能自动识别纸张，请点“选择对应纸张”。" + notice;
            OpenEntry(FrameLibrary.CreateDraft(Library, sample.SourceBlockName, paper, region), sample, notice);
        }

        private void EditFrame(int rowIndex)
        {
            if (frames.Rows[rowIndex].Tag is TitleBlockTemplate template) OpenEntry(Draft(template), null, "");
        }

        private static TitleBlockTemplate Draft(TitleBlockTemplate source)
        {
            var copy = TitleTemplateService.Clone(source); copy.Id = source.Id;
            return copy;
        }

        private void OpenEntry(TitleBlockTemplate draft, PlotFrame? sample, string notice)
        {
            bool isNew = !Library.Templates.Any(t => t.Id == draft.Id);
            var others = Library.Templates.Where(t => t.Id != draft.Id).ToList();
            using (var entry = new FrameEntryForm(draft, others, sample, findSampleFrame, pickSampleFrame, pickRegion))
            {
                entry.Text = isNew ? "录入新图框" : "修改图框 - " + draft.BlockName;
                if (notice.Length > 0) entry.ShowNotice(notice);
                if (showEntry(entry) != DialogResult.OK) { status.Text = isNew ? "已取消录入新图框。" : "已取消修改，原记录不变。"; return; }
                int index = Library.Templates.FindIndex(t => t.Id == draft.Id);
                if (index >= 0) Library.Templates[index] = entry.Template; else Library.Templates.Add(entry.Template);
                dirty = true; RefreshRows(entry.Template);
                status.Text = (isNew ? "已录入图框“" : "已修改图框“") + draft.BlockName + "”，点击“确定”后保存。";
            }
        }

        private void DeleteFrame(int rowIndex)
        {
            if (!(frames.Rows[rowIndex].Tag is TitleBlockTemplate template)) return;
            string label = template.BlockName + "（" + FrameLibrary.PaperLabel(template.PaperWidth, template.PaperHeight) + "）";
            if (!confirm("确定删除图框“" + label + "”吗？")) return;
            Library.Templates.Remove(template); dirty = true; RefreshRows();
            status.Text = "已删除图框“" + label + "”，点击“确定”后保存。";
        }

        private void ExportSettings()
        {
            if (Library.Templates.Count == 0) throw new InvalidOperationException("图框信息库为空，没有可导出的图框。");
            using (var dialog = new SaveFileDialog { Title = "导出设置", Filter = "图框信息配置文件 (*.tk)|*.tk|JSON 图框库 (*.json)|*.json", FileName = "图框信息配置文件.tk" })
                if (dialog.ShowDialog(this) == DialogResult.OK) ExportTo(dialog.FileName);
        }

        private void ExportTo(string path)
        {
            if (IsTk(path)) TkFormatService.Save(path, Library); else TitleTemplateStore.Save(path, Library);
            status.Text = "已导出 " + Library.Templates.Count + " 个图框到 " + Path.GetFileName(path) + "。";
        }

        private void ImportSettings()
        {
            using (var dialog = new OpenFileDialog { Title = "导入设置", Filter = "图框信息配置文件 (*.tk;*.json)|*.tk;*.json|所有文件 (*.*)|*.*" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var imported = LoadSettings(dialog.FileName);
                bool keepCurrent = false;
                if (Library.Templates.Count > 0)
                {
                    var choice = MessageBox.Show(this, "文件中有 " + imported.Templates.Count + " 个图框。\n\n【是】保留现有图框并合并（同一图块同一纸张的图框由导入内容替换）\n【否】清空现有图框，只使用导入的图框\n【取消】放弃导入",
                        "导入设置", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (choice == DialogResult.Cancel) return;
                    keepCurrent = choice == DialogResult.Yes;
                }
                ApplyImport(imported, keepCurrent, Path.GetFileName(dialog.FileName));
            }
        }

        private static TitleTemplateLibrary LoadSettings(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("找不到导入文件：" + path, path);
            return IsTk(path) ? TkFormatService.Load(path) : TitleTemplateStore.Load(path);
        }

        private void ApplyImport(TitleTemplateLibrary imported, bool keepCurrent, string source)
        {
            if (imported.Templates.Count == 0) throw new InvalidOperationException("导入文件中没有图框。");
            Library = FrameLibrary.Merge(Library, imported, keepCurrent); dirty = true; RefreshRows();
            status.Text = "已从 " + source + (keepCurrent ? " 合并导入 " : " 替换导入 ") + imported.Templates.Count + " 个图框，点击“确定”后保存。";
        }

        private static bool IsTk(string path) => string.Equals(Path.GetExtension(path), ".tk", StringComparison.OrdinalIgnoreCase);

        private void SaveAndClose()
        {
            TitleTemplateService.Validate(Library);
            if (savePath != null) TitleTemplateStore.Save(savePath, Library);
            dirty = false; DialogResult = DialogResult.OK; Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (dirty && DialogResult != DialogResult.OK && e.CloseReason != CloseReason.WindowsShutDown
                && !confirm("图框信息库有未保存的修改，确定放弃吗？")) e.Cancel = true;
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tips.Dispose();
            base.Dispose(disposing);
        }
    }
}
