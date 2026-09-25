using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.Core.Assets;

namespace FastBatchPlot.UI.Views
{
    /// <summary>只编辑内存副本，明确确认后保存库并返回本次选择。</summary>
    public sealed class StampLibraryForm : Form
    {
        private const long MaxImportBytes = 8L * 1024 * 1024;
        private readonly TextBox libraryPath = new TextBox { Dock = DockStyle.Fill };
        private readonly ListBox assets = new ListBox { Dock = DockStyle.Fill, DisplayMember = "Name" };
        private readonly ComboBox categoryFilter = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox assetName = new TextBox { Dock = DockStyle.Fill, MaxLength = 128 };
        private readonly PictureBox preview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.WhiteSmoke, BorderStyle = BorderStyle.FixedSingle };
        private readonly Label status = new Label { Dock = DockStyle.Fill, AutoEllipsis = true };
        private readonly Label dimensions = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        private readonly CheckBox noStamp = new CheckBox { Text = "本次不附加印章", AutoSize = true };
        private readonly Button confirm = new Button { Text = "保存并使用选择", AutoSize = true };
        private readonly Panel editor = new Panel { Dock = DockStyle.Fill };
        private StampLibrary library = new StampLibrary();
        private string loadedPath = "";
        private bool loaded;
        private bool dirty;
        private bool binding;
        private bool confirmed;
        private string filteredSelectionId = "";
        public string SelectedLibraryPath { get; private set; } = "";
        public string SelectedAssetId { get; private set; } = "";

        public StampLibraryForm(string libraryPath, string selectedAssetId)
        {
            Text = "印章库与本次附加印章";
            Size = new Size(900, 650); MinimumSize = new Size(820, 600);
            StartPosition = FormStartPosition.CenterParent; Font = new Font("Microsoft YaHei UI", 9);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 5 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            var paths = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 3; i++) paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            paths.Controls.Add(this.libraryPath, 0, 0);
            paths.Controls.Add(Button("打开路径", OpenEnteredPath), 1, 0);
            paths.Controls.Add(Button("浏览库", BrowseLibrary), 2, 0);
            paths.Controls.Add(Button("新建库", NewLibrary), 3, 0);
            root.Controls.Add(paths, 0, 0);
            root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "导入自己拥有的透明 PNG 印章（单张不超过 8 MiB，须含可见内容）。库可放在共享文件夹。\n通过“打开路径”加载库；附加位置需在图框模板中设置印章区域。\n附加印章时，本页光栅图片边框不输出；出图结束恢复原边框设置。" }, 0, 1);
            var contents = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            contents.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36)); contents.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
            var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); left.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); left.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            categoryFilter.Items.AddRange(new object[] { "全部类别", "出图章", "注册章", "其他" }); categoryFilter.SelectedIndex = 0;
            left.Controls.Add(categoryFilter, 0, 0); left.Controls.Add(assets, 0, 1);
            var commands = new FlowLayoutPanel { Dock = DockStyle.Fill };
            commands.Controls.Add(Button("导入 PNG", ImportPng)); commands.Controls.Add(Button("删除所选", DeleteAsset));
            commands.SetFlowBreak(commands.Controls[commands.Controls.Count - 1], true);
            commands.Controls.Add(Button("授权设置", EditAuthorization)); commands.Controls.Add(Button("解锁预览", UnlockPreview));
            commands.SetFlowBreak(commands.Controls[commands.Controls.Count - 1], true);
            commands.Controls.Add(Button("印章属性", EditDetails));
            left.Controls.Add(commands, 0, 2); contents.Controls.Add(left, 0, 0);
            var right = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10, 0, 0, 0), ColumnCount = 1, RowCount = 4 };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); right.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); right.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            right.Controls.Add(new Label { Text = "印章名称（受保护印章请通过授权设置修改）", Dock = DockStyle.Fill }, 0, 0);
            right.Controls.Add(assetName, 0, 1); right.Controls.Add(preview, 0, 2); right.Controls.Add(dimensions, 0, 3);
            contents.Controls.Add(right, 1, 0); editor.Controls.Add(contents); root.Controls.Add(editor, 0, 2);
            root.Controls.Add(status, 0, 3);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
            footer.Controls.Add(cancel); footer.Controls.Add(confirm); footer.Controls.Add(noStamp); root.Controls.Add(footer, 0, 4);
            Controls.Add(root); CancelButton = cancel; AcceptButton = confirm;
            confirm.Click += (s, e) => Guard(ConfirmSelection);
            assets.SelectedIndexChanged += (s, e) =>
            {
                if (!binding && assets.SelectedItem is StampAsset selected) { noStamp.Checked = false; filteredSelectionId = selected.Id; }
                Guard(DisplaySelection);
            };
            categoryFilter.SelectedIndexChanged += (s, e) => Guard(() =>
            {
                string selected = (assets.SelectedItem as StampAsset)?.Id ?? filteredSelectionId;
                RefreshList(selected);
                status.Text = assets.SelectedItem == null && !string.IsNullOrEmpty(selected)
                    ? "当前筛选隐藏了原选择；切回原类别可恢复，或明确选择另一枚印章。"
                    : "仅筛选显示，未修改印章库。";
            });
            assetName.TextChanged += (s, e) =>
            {
                if (binding || !(assets.SelectedItem is StampAsset asset) || asset.Protection != null) return;
                asset.Name = assetName.Text; dirty = true;
                status.Text = "有未保存的修改；确认后写入库，取消会放弃。";
            };
            assetName.Leave += (s, e) => RefreshNames();
            FormClosing += (s, e) =>
            {
                if (!confirmed && dirty && MessageBox.Show(this, "放弃此次未保存的印章库修改？", "关闭印章库", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                { e.Cancel = true; DialogResult = DialogResult.None; }
            };
            this.libraryPath.Text = libraryPath ?? "";
            noStamp.Checked = string.IsNullOrEmpty(selectedAssetId);
            SetLoaded(false);
            if (!string.IsNullOrWhiteSpace(libraryPath)) Guard(() => LoadLibrary(libraryPath!, selectedAssetId ?? ""));
            else status.Text = "请打开已有印章库，或新建库后导入 PNG。";
        }

        private Button Button(string caption, Action action)
        {
            var button = new Button { Text = caption, AutoSize = true };
            button.Click += (s, e) => Guard(action); return button;
        }
        private void Guard(Action action)
        {
            try { action(); }
            catch (Exception ex) { status.Text = "操作未完成：" + ex.Message; }
        }
        private static string NormalizePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("印章库路径不能为空。");
            string result = Path.GetFullPath(value.Trim());
            if (!string.Equals(Path.GetExtension(result), ".json", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("印章库文件应使用 .json 扩展名。");
            if (Directory.Exists(result)) throw new InvalidDataException("请选择文件路径，不能使用文件夹路径。");
            return result;
        }
        private bool CanSwitch()
        {
            return !dirty || MessageBox.Show(this, "切换印章库会放弃当前未保存的修改，是否继续？", "切换印章库", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }
        private void SetLoaded(bool value) { loaded = value; editor.Enabled = value; }
        private void LoadLibrary(string value, string selection)
        {
            string path = NormalizePath(value);
            // 先读成功再替换当前编辑副本，损坏文件不能成为新空库。
            var incoming = StampLibraryStore.Load(path);
            library = incoming.Copy(); loadedPath = path; this.libraryPath.Text = path;
            dirty = false; SetLoaded(true); RefreshList(selection);
            status.Text = File.Exists(path) ? "印章库已加载；选择印章后确认。" : "新库路径尚未创建，确认时保存。";
        }
        private void OpenEnteredPath()
        {
            string path = NormalizePath(libraryPath.Text);
            if (!CanSwitch()) { libraryPath.Text = loadedPath; return; }
            LoadLibrary(path, "");
        }
        private void BrowseLibrary()
        {
            using (var dialog = new OpenFileDialog { Filter = "印章库 (*.json)|*.json", CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || !CanSwitch()) return;
                LoadLibrary(dialog.FileName, "");
            }
        }
        private void NewLibrary()
        {
            using (var dialog = new SaveFileDialog { Filter = "印章库 (*.json)|*.json", FileName = "印章库.json", AddExtension = true, DefaultExt = "json", OverwritePrompt = false })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string path = NormalizePath(dialog.FileName);
                if (File.Exists(path)) throw new InvalidDataException("新库不能覆盖已有文件；请另选文件名，或使用“浏览库”打开已有库。");
                if (!CanSwitch()) return;
                library = new StampLibrary(); loadedPath = path; libraryPath.Text = path;
                dirty = false; SetLoaded(true); RefreshList(""); status.Text = "新库尚未写入磁盘；导入印章后确认保存。";
            }
        }
        private void ImportPng()
        {
            if (!loaded) throw new InvalidOperationException("请先打开或新建印章库。");
            using (var dialog = new OpenFileDialog { Filter = "PNG 图片 (*.png)|*.png", CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                // 打开后核对长度并限量读取，避免检查与读取之间文件变化绕过大小限制。
                byte[] bytes;
                using (var stream = new FileStream(dialog.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length <= 0 || stream.Length > MaxImportBytes) throw new InvalidDataException("每张 PNG 必须大于 0 字节且不超过 8 MiB。");
                    bytes = new byte[(int)stream.Length]; int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int count = stream.Read(bytes, offset, bytes.Length - offset);
                        if (count == 0) throw new EndOfStreamException("PNG 文件读取不完整。");
                        offset += count;
                    }
                }
                var asset = StampAsset.Import(Path.GetFileNameWithoutExtension(dialog.FileName), bytes);
                var proposed=library.Copy(); proposed.Assets.Add(asset); proposed.Validate();
                library=proposed; dirty = true; categoryFilter.SelectedIndex = 0; RefreshList(asset.Id); noStamp.Checked = false;
                status.Text = "PNG 已导入编辑列表，尚未保存；可修改名称后确认。";
            }
        }
        private void DeleteAsset()
        {
            if (!(assets.SelectedItem is StampAsset asset)) return;
            if (asset.Protection != null)
            {
                var permission = StampAuthorizationForm.RequestManagementUnlock(this, asset);
                if (permission == null) return;
                permission.GetAssetForManagement(asset);
            }
            if (MessageBox.Show(this, "从库中删除“" + asset.Name + "”？确认保存后生效。", "删除印章", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            library.Assets.Remove(asset); dirty = true; RefreshList("");
            status.Text = "已从编辑列表删除，确认后保存。";
        }
        private void EditAuthorization()
        {
            if (!(assets.SelectedItem is StampAsset asset)) throw new InvalidOperationException("请先选择一枚印章。");
            var replacement = StampAuthorizationForm.RequestProtection(this, asset);
            if (replacement == null) return;
            var proposed = library.Copy();
            int index = proposed.Assets.FindIndex(a => a.Id == asset.Id);
            if (index < 0) throw new InvalidOperationException("印章已不在当前编辑库中，请重新加载。");
            proposed.Assets[index] = replacement;
            proposed.SchemaVersion = proposed.Assets.Any(a => a.Details != null) ? 3 : Math.Max(2, proposed.SchemaVersion);
            proposed.Validate();
            library = proposed; dirty = true; RefreshList(replacement.Id);
            status.Text = replacement.Protection == null ? "授权保护已在草稿中移除，印章独立期限保留；保存库后生效。" : "授权与印章属性已进入编辑草稿，保存库后生效；授权码不会写入库。";
        }
        private void EditDetails()
        {
            if (!(assets.SelectedItem is StampAsset asset)) throw new InvalidOperationException("请先选择一枚印章。");
            if (asset.Protection != null) { EditAuthorization(); return; }
            using (var dialog = new StampDetailsForm(asset.Details))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var proposed = library.Copy();
                int index = proposed.Assets.FindIndex(a => a.Id == asset.Id);
                if (index < 0) throw new InvalidOperationException("印章已不在当前编辑库中，请重新加载。");
                proposed.Assets[index].Details = dialog.Details!.Copy(); proposed.SchemaVersion = 3; proposed.Validate();
                library = proposed; dirty = true; RefreshList(asset.Id);
                status.Text = assets.SelectedItem == null ? "印章属性已进入草稿；新类别被当前筛选隐藏，可切换类别查看。" : "印章属性已进入库草稿，保存库后生效。";
            }
        }
        private void UnlockPreview()
        {
            if (!(assets.SelectedItem is StampAsset asset)) throw new InvalidOperationException("请先选择一枚印章。");
            if (asset.Protection == null) { DisplaySelection(); return; }
            var permission = StampAuthorizationForm.RequestManagementUnlock(this, asset);
            if (permission == null) return;
            DisplayImage(permission.GetAssetForManagement(asset));
            status.Text = "已完成本次管理预览，切换印章后重新锁定；管理预览不授予过期印章出图权限。";
        }
        private void RefreshNames()
        {
            if (binding) return;
            string selection = (assets.SelectedItem as StampAsset)?.Id ?? "";
            RefreshList(selection);
        }
        private void RefreshList(string selectedId)
        {
            filteredSelectionId = selectedId;
            binding = true;
            try
            {
                var visible = library.Assets.Where(a => categoryFilter.SelectedIndex <= 0 || (int)(a.Details?.Kind ?? StampKind.Issue) == categoryFilter.SelectedIndex - 1).ToArray();
                assets.Items.Clear(); assets.Items.AddRange(visible.Cast<object>().ToArray());
                assets.SelectedItem = visible.FirstOrDefault(a => a.Id == selectedId);
            }
            finally { binding = false; }
            DisplaySelection();
        }
        private void DisplaySelection()
        {
            if (binding) return;
            var asset = assets.SelectedItem as StampAsset;
            binding = true;
            try { assetName.Text = asset?.Name ?? ""; assetName.Enabled = asset != null && asset.Protection == null; }
            finally { binding = false; }
            Image? old = preview.Image; preview.Image = null; old?.Dispose();
            dimensions.Text = "";
            if (asset == null) return;
            var details = asset.Details;
            string kind = details?.Kind == StampKind.Registration ? "注册章" : details?.Kind == StampKind.Other ? "其他" : "出图章";
            string sizing = details?.Sizing == StampSizing.PhysicalSize ? "纸面 " + details.WidthMm.ToString("0.###") + " × " + details.HeightMm.ToString("0.###") + " mm" : "适应区域，保持比例";
            string expiryText = details == null || details.ValidUntilUtcTicks == 0 ? "印章独立期限：不限制" : FormatExpiry("印章", details.ValidUntilUtcTicks);
            dimensions.Text = kind + "；" + sizing + "\n" + asset.PixelWidth + " × " + asset.PixelHeight + " 像素（含透明边缘）\n" + expiryText;
            if (asset.Protection != null)
            {
                dimensions.Text += "\n受授权保护；默认不显示图像\n" + FormatExpiry("授权", asset.Protection.ExpiresUtcTicks);
                return;
            }
            DisplayImage(asset);
            dimensions.Text += "\n无需授权码；印章独立期限仍然生效";
        }
        private static string FormatExpiry(string label, long ticks)
        {
            var expiry = new DateTimeOffset(ticks, TimeSpan.Zero);
            return label + (DateTimeOffset.UtcNow >= expiry ? "已到期；" : "截止（不含）：") + expiry.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");
        }
        private void DisplayImage(StampAsset asset)
        {
            Image? old = preview.Image; preview.Image = null; old?.Dispose();
            using (var stream = new MemoryStream(Convert.FromBase64String(asset.PngBase64), false))
            using (var image = Image.FromStream(stream, true, true)) preview.Image = new Bitmap(image);
        }
        private void ConfirmSelection()
        {
            // “不附加”可清除失效选择，不要求重建损坏的库。
            if (!loaded)
            {
                if (!noStamp.Checked) throw new InvalidOperationException("请先成功打开印章库并选择印章。");
                SelectedLibraryPath = ""; SelectedAssetId = "";
                confirmed = true; DialogResult = DialogResult.OK; Close(); return;
            }
            if (!string.Equals(NormalizePath(libraryPath.Text), loadedPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("路径已更改；请先点击“打开路径”，再确认选择。");
            var selected = assets.SelectedItem as StampAsset;
            if (!noStamp.Checked && (selected == null || !library.Assets.Any(a => a.Id == selected.Id)))
                throw new InvalidOperationException("请选择一枚印章，或勾选“本次不附加印章”。");
            if (dirty || string.IsNullOrEmpty(library.Revision)) StampLibraryStore.Save(loadedPath, library);
            else
            {
                // 未编辑时只复读检查，允许使用只读共享库，同时拒绝陈旧选择。
                var current = StampLibraryStore.Load(loadedPath);
                if (!string.Equals(current.Revision, library.Revision, StringComparison.Ordinal))
                    throw new IOException("印章库已被其他窗口或电脑修改，请重新打开后选择。");
            }
            dirty = false; SelectedLibraryPath = loadedPath; SelectedAssetId = noStamp.Checked ? "" : selected!.Id;
            confirmed = true; DialogResult = DialogResult.OK; Close();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { Image? image = preview.Image; preview.Image = null; image?.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
