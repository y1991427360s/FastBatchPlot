using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.Core.Templates;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Export;

namespace FastBatchPlot.UI.Views
{
    public sealed class TitleTemplateForm : Form
    {
        public TitleTemplateLibrary Library { get; private set; }
        private readonly ListBox templates = new ListBox { Dock = DockStyle.Left, Width = 205 };
        private readonly TextBox name = new TextBox { Width = 170 };
        private readonly TextBox block = new TextBox { Width = 170 };
        private readonly NumericUpDown priority = new NumericUpDown { Minimum = 0, Maximum = 10000, Width = 70 };
        private readonly DataGridView fields = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
        private readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 45, AutoEllipsis = true };
        private readonly Func<string, PlotFrame?> findSampleFrame;
        private readonly Func<PlotFrame?> pickSampleFrame;
        private readonly Func<PlotFrame, TemplateRegion?> pickRegion;
        private readonly string? savePath;
        private PlotFrame? sampleFrame;
        private TitleBlockTemplate? editing;
        private bool selecting;
        private readonly TextBox naming=new TextBox{Width=335};
        private CatalogOptions? catalog;
        private TemplateRegion? printRegion;
        private TemplateRegion? stampRegion;
        private TemplateRegion? registrationStampRegion;
        private double printScale;

        public TitleTemplateForm(TitleTemplateLibrary library, string? savePath, Func<PlotFrame?> pickSampleFrame,
            Func<PlotFrame, TemplateRegion?> pickRegion)
            : this(library, savePath, _ => null, pickSampleFrame, pickRegion)
        {
        }

        public TitleTemplateForm(TitleTemplateLibrary library, string? savePath, Func<string, PlotFrame?> findSampleFrame,
            Func<PlotFrame?> pickSampleFrame, Func<PlotFrame, TemplateRegion?> pickRegion)
        {
            Library = TitleTemplateService.CopyLibrary(library);
            this.findSampleFrame = findSampleFrame; this.pickSampleFrame = pickSampleFrame; this.pickRegion = pickRegion; this.savePath = savePath;
            Text = "图框模板库"; Size = new Size(1050, 640); MinimumSize = new Size(920, 580);
            StartPosition = FormStartPosition.CenterParent; Font = new Font("Microsoft YaHei UI", 9);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 148, AutoScroll = true };
            AddButton(actions, "新增", NewTemplate); AddButton(actions, "复制", CopyTemplate);
            AddButton(actions, "删除", DeleteTemplate); AddButton(actions, "导入", ImportLibrary);
            AddButton(actions, "导出", ExportLibrary); AddButton(actions, "拾取字段区域", PickRegion);
            AddButton(actions, "拾取样本图框", PickSampleFrame);
            AddButton(actions, "保存并关闭", SaveLibrary);
            actions.SetFlowBreak(actions.Controls[actions.Controls.Count - 1], true);
            actions.Controls.Add(new Label { Text = "名称", AutoSize = true }); actions.Controls.Add(name);
            actions.Controls.Add(new Label { Text = "块名", AutoSize = true }); actions.Controls.Add(block);
            actions.Controls.Add(new Label { Text = "优先级", AutoSize = true }); actions.Controls.Add(priority);
            actions.SetFlowBreak(priority,true);
            actions.Controls.Add(new Label{Text="文件命名（空白用通用）",AutoSize=true});actions.Controls.Add(naming);
            AddButton(actions,"编辑专用目录",EditCatalog);AddButton(actions,"清除专用目录",()=>{catalog=null;status.Text="改用通用目录设置，保存后生效。";});
            actions.SetFlowBreak(actions.Controls[actions.Controls.Count-1],true);AddButton(actions,"调整打印范围",EditPrintRegion);
            fields.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "启用", Width = 45 });
            fields.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "字段", Width = 80, ReadOnly = true });
            fields.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "属性标签（优先）", Width = 150 });
            foreach (string coordinate in new[] { "X1", "Y1", "X2", "Y2" }) fields.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = coordinate, Width = 85 });
            foreach (DataGridViewColumn column in fields.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
            for (int i = 0; i < TitleTemplateService.FieldLabels.Length; i++) fields.Rows.Add(false, TitleTemplateService.FieldLabels[i], "", 0, 0, 0, 0);
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            panel.Controls.Add(fields); panel.Controls.Add(status);
            panel.Controls.Add(new Label { Dock = DockStyle.Bottom, Height = 42,
                Text = "选字段行后点【拾取字段区域】，程序会按模板块名在当前 CAD 空间自动找样本，再直接框选文字区域；找不到时可用【拾取样本图框】手动指定。\n区域坐标以图框本地坐标为基准（有线框时取右下角，纯文字块取块原点）。属性标签非空时优先按标签提取；此处不写回 DWG。" });
            Controls.Add(panel); Controls.Add(templates); Controls.Add(actions);
            templates.SelectedIndexChanged += (s,e) => SelectTemplate();
            RefreshList(null);
        }
        private void EditPrintRegion()
        {
            if(editing==null)throw new InvalidOperationException("请先选择图框模板。");
            EnsureSampleFrame();
            using(var dialog=new TemplateCropForm(printRegion,()=>
            {
                return PickRegionFromSample();
            },printScale))
                if(dialog.ShowDialog(this)==DialogResult.OK){printScale=dialog.PrintScale;printRegion=dialog.PrintRegion;status.Text="打印范围已更新；保存模板后，在列表右键应用模板打印范围。";}
        }
        private void EditCatalog()
        {
            if(editing==null)throw new InvalidOperationException("请先选择图框模板。");
            using(var dialog=new CatalogOptionsForm(catalog,"应用到当前图框模板"))
                if(dialog.ShowDialog(this)==DialogResult.OK){catalog=dialog.Options.Copy();status.Text="专用目录设置已更新，保存模板后生效。";}
        }
        private void EditStampRegion()
        {
            if(editing==null)throw new InvalidOperationException("请先选择图框模板。");
            EnsureSampleFrame();
            using(var dialog=new TemplateCropForm(stampRegion,()=>
            {
                return PickRegionFromSample();
            },stampMode:true){Text="图框模板主印章区域"})
                if(dialog.ShowDialog(this)==DialogResult.OK){stampRegion=dialog.PrintRegion==null?null:TemplateCropGeometry.Copy(dialog.PrintRegion);status.Text="主印章区域已更新；保存模板并选择主印章后，输出时按此区域放置。";}
        }
        private void EditRegistrationStampRegion()
        {
            if(editing==null)throw new InvalidOperationException("请先选择图框模板。");
            EnsureSampleFrame();
            using(var dialog=new TemplateCropForm(registrationStampRegion,()=>
            {
                return PickRegionFromSample();
            },stampMode:true){Text="图框模板注册章区域"})
                if(dialog.ShowDialog(this)==DialogResult.OK){registrationStampRegion=dialog.PrintRegion==null?null:TemplateCropGeometry.Copy(dialog.PrintRegion);status.Text="注册章区域已更新；保存模板并选择注册章后，输出时按此区域放置。";}
        }
        private void AddButton(FlowLayoutPanel panel, string caption, Action action)
        {
            var button = new Button { Text = caption, AutoSize = true, Height = 29 };
            button.Click += (s,e) => { try { action(); } catch (Exception ex) { status.Text = ex.Message; } };
            panel.Controls.Add(button);
        }
        private void PickSampleFrame()
        {
            if (editing == null) throw new InvalidOperationException("请先选择或新增一个图框模板。");
            Hide();
            try { sampleFrame = pickSampleFrame(); }
            finally { Show(); Activate(); }
            if (sampleFrame == null) { status.Text = "已取消样本图框拾取。"; return; }
            var previousName = block.Text.Trim();
            block.Text = sampleFrame.SourceBlockName;
            if (string.IsNullOrWhiteSpace(name.Text) || name.Text.Trim() == "新图框" || string.Equals(name.Text.Trim(), previousName, StringComparison.OrdinalIgnoreCase))
                name.Text = sampleFrame.SourceBlockName;
            status.Text = "已从 CAD 拾取样本图框：" + sampleFrame.SourceBlockName + "。现在可直接拾取图号、图名等字段区域。";
        }
        private void EnsureSampleFrame()
        {
            if (sampleFrame != null && string.Equals(block.Text.Trim(), sampleFrame.SourceBlockName.Trim(), StringComparison.OrdinalIgnoreCase)) return;
            string blockName = block.Text.Trim();
            if (string.IsNullOrWhiteSpace(blockName)) throw new InvalidOperationException("请先选择或填写图框模板的块名。");
            sampleFrame = findSampleFrame(blockName);
            if (sampleFrame == null)
                throw new InvalidOperationException($"当前 CAD 空间找不到块名“{blockName}”的图框。请切换到包含此图框的图纸/空间，或点【拾取样本图框】手动指定。");
            if (!string.Equals(blockName, sampleFrame.SourceBlockName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"自动找到的图框块名“{sampleFrame.SourceBlockName}”与模板块名“{blockName}”不一致。");
            status.Text = "已使用图框样本 " + sampleFrame.HandleOrId + "（" + sampleFrame.SourceBlockName + "）；现在直接框选字段区域。";
        }
        private TemplateRegion? PickRegionFromSample()
        {
            EnsureSampleFrame();
            var owner = Owner;
            bool restoreOwner = owner != null && owner.Visible;
            Hide();
            if (restoreOwner) owner!.Hide();
            try { return pickRegion(sampleFrame!); }
            finally
            {
                if (restoreOwner && !owner!.IsDisposed) owner.Show();
                Show(); Activate();
            }
        }
        private void CommitEditor()
        {
            fields.EndEdit();
            if (editing == null) return;
            var copy = TitleTemplateService.Clone(editing); copy.Id = editing.Id;
            copy.PrintScale=printScale;copy.PrintRegion=printRegion==null?null:TemplateCropGeometry.Copy(printRegion);
            copy.StampRegion=stampRegion==null?null:TemplateCropGeometry.Copy(stampRegion);
            copy.RegistrationStampRegion=registrationStampRegion==null?null:TemplateCropGeometry.Copy(registrationStampRegion);
            copy.NamingTemplate=string.IsNullOrWhiteSpace(naming.Text)?null:naming.Text.Trim();copy.Catalog=catalog?.Copy();
            copy.Name = name.Text.Trim(); copy.BlockName = block.Text.Trim(); copy.Priority = (int)priority.Value; copy.Fields.Clear();
            for (int i = 0; i < fields.Rows.Count; i++)
            {
                var cells = fields.Rows[i].Cells;
                if (!Convert.ToBoolean(cells[0].Value)) continue;
                var coords = new double[4];
                for (int c = 0; c < 4; c++)
                    if (!double.TryParse(Convert.ToString(cells[c+3].Value), NumberStyles.Float, CultureInfo.CurrentCulture, out coords[c]))
                        throw new InvalidDataException("区域坐标必须是数字。");
                copy.Fields.Add(new TitleFieldRule { Field = (TitleField)i, AttributeTag = Convert.ToString(cells[2].Value)?.Trim() ?? "",
                    Region = new TemplateRegion { X1 = coords[0], Y1 = coords[1], X2 = coords[2], Y2 = coords[3] } });
            }
            var proposed = new TitleTemplateLibrary { Templates = Library.Templates.Select(t => ReferenceEquals(t, editing) ? copy : t).ToList() };
            TitleTemplateService.Validate(proposed);
            int index = Library.Templates.IndexOf(editing); Library.Templates[index] = copy; editing = copy;
        }
        private void SelectTemplate()
        {
            if (selecting) return;
            var next = templates.SelectedItem as TitleBlockTemplate;
            try { CommitEditor(); }
            catch (Exception ex)
            {
                status.Text = ex.Message; selecting = true;
                try { templates.SelectedItem = templates.Items.Cast<TitleBlockTemplate>().FirstOrDefault(t => t.Id == editing?.Id); }
                finally { selecting = false; }
                return;
            }
            if (!string.Equals(next?.Id, editing?.Id, StringComparison.Ordinal)) sampleFrame = null;
            Display(next == null ? null : Library.Templates.FirstOrDefault(t => t.Id == next.Id));
        }
        private void Display(TitleBlockTemplate? template)
        {
            printScale=template?.PrintScale??0;printRegion=template?.PrintRegion==null?null:TemplateCropGeometry.Copy(template.PrintRegion);
            stampRegion=template?.StampRegion==null?null:TemplateCropGeometry.Copy(template.StampRegion);
            registrationStampRegion=template?.RegistrationStampRegion==null?null:TemplateCropGeometry.Copy(template.RegistrationStampRegion);
            naming.Text=template?.NamingTemplate??"";catalog=template?.Catalog?.Copy();naming.Enabled=template!=null;
            editing = template; name.Text = template?.Name ?? ""; block.Text = template?.BlockName ?? ""; priority.Value = template?.Priority ?? 100;
            name.Enabled = block.Enabled = priority.Enabled = fields.Enabled = template != null;
            foreach (DataGridViewRow row in fields.Rows)
            {
                var rule = template?.Fields.FirstOrDefault(f => (int)f.Field == row.Index);
                row.Cells[0].Value = rule != null; row.Cells[2].Value = rule?.AttributeTag ?? "";
                var r = rule?.Region ?? new TemplateRegion();
                row.Cells[3].Value = r.X1; row.Cells[4].Value = r.Y1; row.Cells[5].Value = r.X2; row.Cells[6].Value = r.Y2;
            }
        }
        private void RefreshList(TitleBlockTemplate? selected)
        {
            selecting = true;
            try
            {
                templates.Items.Clear(); templates.Items.AddRange(Library.Templates.Cast<object>().ToArray());
                templates.SelectedItem = selected ?? Library.Templates.FirstOrDefault();
                Display(templates.SelectedItem as TitleBlockTemplate);
            }
            finally { selecting = false; }
        }
        private void NewTemplate()
        {
            CommitEditor();
            string candidate = string.IsNullOrWhiteSpace(sampleFrame?.SourceBlockName) ? "新图框" : sampleFrame!.SourceBlockName;
            string unique = candidate; int suffix = 2;
            while (Library.Templates.Any(t => string.Equals(t.BlockName, unique, StringComparison.OrdinalIgnoreCase))) unique = candidate + "_" + suffix++;
            var t = new TitleBlockTemplate { Name = unique, BlockName = unique };
            Library.Templates.Add(t); RefreshList(t);
        }
        private void CopyTemplate()
        {
            CommitEditor(); if (editing == null) return;
            var t = TitleTemplateService.Clone(editing); t.Name += " 副本";
            string root = t.BlockName; int suffix = 2;
            do { t.BlockName = root + "_副本" + suffix++; } while (Library.Templates.Any(x => string.Equals(x.BlockName,t.BlockName,StringComparison.OrdinalIgnoreCase)));
            Library.Templates.Add(t); RefreshList(t);
        }
        private void DeleteTemplate() { if (editing == null) return; Library.Templates.Remove(editing); editing = null; RefreshList(null); }
        private void PickRegion()
        {
            if (editing == null || fields.CurrentRow == null) throw new InvalidOperationException("请先选择模板和字段行。");
            EnsureSampleFrame();
            int index = fields.CurrentRow.Index; TemplateRegion? r;
            r = PickRegionFromSample();
            if (r == null) { status.Text = "已取消区域拾取。"; return; }
            var cells = fields.Rows[index].Cells;
            cells[0].Value = true; cells[2].Value = ""; cells[3].Value = r.X1; cells[4].Value = r.Y1; cells[5].Value = r.X2; cells[6].Value = r.Y2;
            status.Text = "字段区域已录入，保存后生效。";
        }
        private void ImportLibrary()
        {
            CommitEditor();
            using (var dialog = new OpenFileDialog { Filter = "图框配置文件 (*.tk)|*.tk|JSON 模板库 (*.json)|*.json|所有支持的模板文件 (*.tk;*.json)|*.tk;*.json" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                bool isTk = string.Equals(Path.GetExtension(dialog.FileName), ".tk", StringComparison.OrdinalIgnoreCase);
                TitleTemplateLibrary imported;
                if (isTk)
                {
                    imported = TkFormatService.Load(dialog.FileName);
                }
                else
                {
                    imported = TitleTemplateStore.Load(dialog.FileName);
                }

                var choice = MessageBox.Show(this, "是否保留当前已有的图框模板？\n\n【是】合并导入（同名模板将被覆盖更新）\n【否】清空当前模板并完全替换为导入的图框配置\n【取消】放弃导入", "导入图框配置", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (choice == DialogResult.Cancel) return;

                var merged = choice == DialogResult.Yes ? TitleTemplateService.CopyLibrary(Library) : new TitleTemplateLibrary();
                foreach (var t in imported.Templates)
                {
                    var existing = merged.Templates.FirstOrDefault(x => string.Equals(x.Name.Trim(), t.Name.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        int idx = merged.Templates.IndexOf(existing);
                        merged.Templates[idx] = t;
                    }
                    else
                    {
                        merged.Templates.Add(t);
                    }
                }
                TitleTemplateService.Validate(merged);
                Library = merged;
                editing = null;
                RefreshList(null);
                status.Text = isTk ? $"已导入 {imported.Templates.Count} 个图框配置，保存后生效。" : "已导入到编辑列表，保存后生效。";
            }
        }
        private void ExportLibrary()
        {
            CommitEditor();
            using (var dialog = new SaveFileDialog { Filter = "图框配置文件 (*.tk)|*.tk|JSON 模板库 (*.json)|*.json", FileName = "图框信息配置文件.tk" })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    bool isTk = string.Equals(Path.GetExtension(dialog.FileName), ".tk", StringComparison.OrdinalIgnoreCase);
                    if (isTk)
                    {
                        TkFormatService.Save(dialog.FileName, Library);
                        status.Text = $"图框配置文件已导出为 .tk 文件（共 {Library.Templates.Count} 个模板）。";
                    }
                    else
                    {
                        TitleTemplateStore.Save(dialog.FileName, Library);
                        status.Text = "模板库已导出为 JSON。";
                    }
                }
            }
        }
        private void SaveLibrary()
        {
            CommitEditor(); TitleTemplateService.Validate(Library);
            if (savePath != null) TitleTemplateStore.Save(savePath, Library);
            DialogResult = DialogResult.OK; Close();
        }
    }
}
