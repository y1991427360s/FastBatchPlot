using System.Drawing;
using System.Drawing.Imaging;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckPdfHistoryFormUi()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PdfHistoryFormUi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            // 路径故意不存在：历史选择窗不能自行读取任务文件或开始输出。
            string firstPath = Path.Combine(directory, "first-task.json");
            string secondPath = Path.Combine(directory, "second-task.json");
            var entries = new[]
            {
                new PdfTaskHistoryEntry { Path = firstPath, Name = "首批电气图", Created = "2026-09-20 10:20", Summary = "共12张，成功10张，失败2张" },
                new PdfTaskHistoryEntry { Path = secondPath, Name = "第二批图纸", Created = "2026-09-20 11:00", Summary = "共3张，已完成" },
                new PdfTaskHistoryEntry { Path = Path.Combine(directory, "broken-task.json"), Name = "损坏记录", Created = "未知", Summary = "不应显示为成功", Error = "任务记录不是有效 JSON；原始文件未修改。" }
            };
            using (var form = new PdfTaskHistoryForm(entries))
            {
                ShowPdfHistoryOffscreen(form);
                var grid = Field<DataGridView>(form, "tasks");
                Check(grid.Rows.Count == 3 && grid.ReadOnly && !grid.MultiSelect && grid.SelectionMode == DataGridViewSelectionMode.FullRowSelect,
                    "PDF任务历史显示全部记录且仅允许单行只读选择");
                Check(Convert.ToString(grid.Rows[0].Cells[0].Value) == entries[0].Name &&
                    Convert.ToString(grid.Rows[0].Cells[1].Value) == entries[0].Created && Convert.ToString(grid.Rows[0].Cells[2].Value) == entries[0].Summary,
                    "PDF任务历史名称、创建时间、状态对应输入记录");
                Check(form.SelectedPath == "", "历史窗未确认前不提交任务路径");
                grid.ClearSelection(); grid.Rows[2].Selected = true;
                Check(!Field<Button>(form, "view").Enabled && Field<TextBox>(form, "details").Text.Contains(entries[2].Error) &&
                    Convert.ToString(grid.Rows[2].Cells[2].Value)!.Contains("读取失败"), "损坏历史行明确显示完整错误并禁用查看");
                typeof(PdfTaskHistoryForm).GetMethod("ViewSelected", PrivateInstance)!.Invoke(form, null);
                Check(form.DialogResult != DialogResult.OK && form.SelectedPath == "" && !form.IsDisposed,
                    "直接触发损坏行查看也不能返回成功或关闭窗口");
                grid.ClearSelection(); grid.Rows[1].Selected = true;
                Check(Field<Button>(form, "view").Enabled && Field<TextBox>(form, "details").Text.Contains(secondPath),
                    "选择有效历史行启用查看并显示对应任务路径");
                entries[1].Path = Path.Combine(directory, "caller-mutated.json");
                typeof(PdfTaskHistoryForm).GetMethod("ViewSelected", PrivateInstance)!.Invoke(form, null);
                Check(form.DialogResult == DialogResult.OK && form.SelectedPath == secondPath,
                    "查看有效历史仅返回所选路径，且隔离调用方后续修改");
            }
            using (var empty = new PdfTaskHistoryForm(Array.Empty<PdfTaskHistoryEntry>()))
            {
                Check(!Field<Button>(empty, "view").Enabled && Field<TextBox>(empty, "details").Text.Contains("暂无"), "空历史列表禁用查看并显示空状态");
                typeof(PdfTaskHistoryForm).GetMethod("ViewSelected", PrivateInstance)!.Invoke(empty, null);
                Check(empty.SelectedPath == "" && empty.DialogResult != DialogResult.OK, "空列表即使触发查看也不返回任务");
            }
            using (var missing = new PdfTaskHistoryForm(new[] { new PdfTaskHistoryEntry { Name = "缺少路径", Summary = "导入不完整" } }))
            {
                Check(!Field<Button>(missing, "view").Enabled, "历史记录缺少路径时禁用查看");
                typeof(PdfTaskHistoryForm).GetMethod("ViewSelected", PrivateInstance)!.Invoke(missing, null);
                Check(missing.SelectedPath == "", "无路径历史不能产生虚假成功选择");
            }
            using (var cancelled = new PdfTaskHistoryForm(entries))
            {
                ShowPdfHistoryOffscreen(cancelled); cancelled.Size = cancelled.MinimumSize; cancelled.PerformLayout();
                foreach (string field in new[] { "tasks", "details", "view", "cancel" })
                {
                    var control = Field<Control>(cancelled, field);
                    Check(control.Visible && control.Width > 0 && control.Height > 0 && control.Parent!.ClientRectangle.Contains(control.Bounds),
                        "PDF历史最小窗口控件完整可见：" + field);
                }
                string evidence = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "audit-evidence"));
                Directory.CreateDirectory(evidence);
                using (var image = new Bitmap(cancelled.Width, cancelled.Height))
                {
                    cancelled.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
                    image.Save(Path.Combine(evidence, "ui-pdf-task-history.png"), ImageFormat.Png);
                }
                Field<Button>(cancelled, "cancel").PerformClick();
                Check(cancelled.DialogResult == DialogResult.Cancel && cancelled.SelectedPath == "", "取消历史窗口不返回待查看任务");
            }
            Check(Directory.GetFiles(directory).Length == 0, "历史列表查看及取消不读取存在性、不创建记录或PDF输出");
        }
        finally { Directory.Delete(directory); }
    }
    private static void ShowPdfHistoryOffscreen(Form form)
    {
        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-32000, -32000);
        form.Show(); form.PerformLayout();
    }
}
