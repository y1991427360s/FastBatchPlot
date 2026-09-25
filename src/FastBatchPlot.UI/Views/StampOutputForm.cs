using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.Core.Assets;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.UI.Views
{
    /// <summary>只返回两槽选择信息；图片与授权凭据由提交任务时捕获。</summary>
    public sealed class StampOutputForm : Form
    {
        private readonly CheckBox primaryEnabled = new CheckBox { Text = "启用主印章", AutoSize = true };
        private readonly CheckBox registrationEnabled = new CheckBox { Text = "启用注册章", AutoSize = true };
        private readonly TextBox primaryPath = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
        private readonly TextBox registrationPath = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
        private readonly TextBox primaryName = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
        private readonly TextBox registrationName = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
        private readonly Label status = new Label { Dock = DockStyle.Fill, ForeColor = Color.Firebrick };
        private readonly Button confirm = new Button { Text = "应用选择", AutoSize = true };
        private string primaryId;
        private string registrationId;
        private PlotConfig? selection;
        public PlotConfig Selection => DialogResult == DialogResult.OK && selection != null ? selection : throw new InvalidOperationException("尚未确认印章输出选择。");

        public StampOutputForm(PlotConfig source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            primaryId = source.StampAssetId ?? ""; registrationId = source.RegistrationStampAssetId ?? "";
            primaryPath.Text = source.StampLibraryPath ?? ""; registrationPath.Text = source.RegistrationStampLibraryPath ?? "";
            primaryEnabled.Checked = source.PrintPrimaryStamp; registrationEnabled.Checked = source.PrintRegistrationStamp;
            Text = "附加印章输出设置"; Size = new Size(900, 490); MinimumSize = new Size(820, 470);
            StartPosition = FormStartPosition.CenterParent; Font = new Font("Microsoft YaHei UI", 9);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 5 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.Controls.Add(CreateSlot("主印章（兼容原单章选择）", primaryEnabled, primaryPath, primaryName, () => Choose(false)), 0, 0);
            root.Controls.Add(CreateSlot("注册章（仅注册章类别）", registrationEnabled, registrationPath, registrationName, () => Choose(true)), 0, 1);
            root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "主印章兼容原来用注册章作为唯一印章的用法，不限制类别。注册章槽只允许选择“注册章”类别。\n两章分别使用模板的主印章区域和注册章区域；位置重复会叠印，请设置分开的区域。\n关闭某一槽会保留选择，且不检查其库。总开关仍由主窗口“印章输出”控制；提交出图前再验证授权。" }, 0, 2);
            root.Controls.Add(status, 0, 3);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
            footer.Controls.Add(cancel); footer.Controls.Add(confirm); root.Controls.Add(footer, 0, 4); Controls.Add(root);
            confirm.Click += (s, e) => { try { Confirm(); } catch (Exception ex) { status.Text = ex.Message; } };
            primaryEnabled.CheckedChanged += (s, e) => DisplayName(false);
            registrationEnabled.CheckedChanged += (s, e) => DisplayName(true);
            AcceptButton = confirm; CancelButton = cancel;
            DisplayName(false); DisplayName(true);
        }
        private GroupBox CreateSlot(string title, CheckBox enabled, TextBox path, TextBox name, Action choose)
        {
            var group = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(10, 20, 10, 8) };
            var rows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            rows.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); rows.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            rows.Controls.Add(enabled, 0, 0); rows.Controls.Add(name, 1, 0);
            var select = new Button { Text = "选择印章库", AutoSize = true }; select.Click += (s, e) => { try { choose(); } catch (Exception ex) { status.Text = ex.Message; } };
            rows.Controls.Add(select, 2, 0); rows.Controls.Add(new Label { Text = "库文件", Dock = DockStyle.Fill }, 0, 1); rows.Controls.Add(path, 1, 1); rows.SetColumnSpan(path, 2);
            group.Controls.Add(rows); return group;
        }
        private void Choose(bool registration)
        {
            var path = registration ? registrationPath : primaryPath;
            string id = registration ? registrationId : primaryId;
            string initial = string.IsNullOrWhiteSpace(path.Text) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "stamps.json") : path.Text;
            using (var dialog = new StampLibraryForm(initial, id))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                path.Text = dialog.SelectedLibraryPath;
                if (registration) registrationId = dialog.SelectedAssetId; else primaryId = dialog.SelectedAssetId;
                DisplayName(registration);
                status.Text = "选择已进入本窗口草稿，点击应用后生效；此处不会请求授权码。";
            }
        }
        private void DisplayName(bool registration)
        {
            string id = registration ? registrationId : primaryId;
            var name = registration ? registrationName : primaryName;
            var enabled = registration ? registrationEnabled : primaryEnabled;
            var path = registration ? registrationPath : primaryPath;
            if (string.IsNullOrEmpty(id)) { name.Text = "未选择附加印章"; return; }
            if (!enabled.Checked) { name.Text = "已停用，保留选择：" + id; return; }
            try
            {
                var asset = ReadSelected(path.Text, id, registration);
                name.Text = asset.Name;
            }
            catch (Exception ex) { name.Text = "选择不可用：" + ex.Message; }
        }
        private static StampAsset ReadSelected(string path, string id, bool registration)
        {
            var asset = StampLibraryStore.Load(path).Assets.SingleOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("印章不存在或库文件缺失，请重新选择。");
            if (registration && (asset.Details?.Kind ?? StampKind.Issue) != StampKind.Registration)
                throw new InvalidOperationException("注册章槽必须选择“注册章”类别，请先在库中设置印章属性。");
            return asset;
        }
        private static string ComparisonPath(string path)
        {
            // 停用槽不要求路径可用，仅尽力规范化以识别重复选择。
            try { return Path.GetFullPath(path.Trim()); }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException) { return path.Trim(); }
        }
        private void Confirm()
        {
            if (primaryEnabled.Checked && !string.IsNullOrEmpty(primaryId)) ReadSelected(primaryPath.Text, primaryId, false);
            if (registrationEnabled.Checked && !string.IsNullOrEmpty(registrationId)) ReadSelected(registrationPath.Text, registrationId, true);
            if (!string.IsNullOrEmpty(primaryId) && !string.IsNullOrEmpty(registrationId) &&
                string.Equals(primaryId, registrationId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ComparisonPath(primaryPath.Text), ComparisonPath(registrationPath.Text), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("主印章和注册章不能选择同一库中的同一印章；请清除其中一槽或另选印章。");
            selection = new PlotConfig
            {
                StampLibraryPath = primaryPath.Text, StampAssetId = primaryId, PrintPrimaryStamp = primaryEnabled.Checked,
                RegistrationStampLibraryPath = registrationPath.Text, RegistrationStampAssetId = registrationId, PrintRegistrationStamp = registrationEnabled.Checked
            };
            DialogResult = DialogResult.OK; Close();
        }
    }
}
