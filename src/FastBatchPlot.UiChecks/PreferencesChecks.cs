using FastBatchPlot.CadBridge;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckPreferencesUi()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "FastBatchPlot.SettingsUiChecks-" + Guid.NewGuid().ToString("N"));
        string settingsPath = Path.Combine(tempDirectory, "preferences.json");
        var previousHost = CadHostProvider.Host;
        var previousPlotter = CadHostProvider.Plotter;
        CadHostProvider.Host = null;
        CadHostProvider.Plotter = null;
        Directory.CreateDirectory(tempDirectory);
        try
        {
            using (var first = new BatchPlotForm(settingsPath))
            {
                Field<ComboBox>(first, "cboPlotters").SelectedItem = "ZWCAD to PDF.pc5";
                Field<ComboBox>(first, "cboPlotStyles").SelectedItem = "acad.ctb";
                Field<NumericUpDown>(first, "numAreaFilter").Value = 6.3m;
                Field<CheckBox>(first, "chkRemoveDuplicates").Checked = false;
                Field<CheckBox>(first, "chkRemoveNestedFrames").Checked = false;
                Field<NumericUpDown>(first, "numMargin").Value = -1.25m;
                Field<ComboBox>(first, "cboSortRule").SelectedIndex = 3;
                Field<TextBox>(first, "txtOutputFolder").Text = tempDirectory;
                Field<TextBox>(first, "txtNamingTemplate").Text = "{Index:D2}_{DwgName}";
                Field<TextBox>(first, "txtMergedFileName").Text = "合并验收.pdf";
                Field<CheckBox>(first, "chkMergePdf").Checked = true;
                Field<CheckBox>(first, "chkPrintSignatures").Checked = false;
                Field<CheckBox>(first, "chkPrintStamps").Checked = false;
                Field<FastBatchPlot.Core.Models.PlotConfig>(first, "_config").SignatureLayerName = "设计签名";
                Field<FastBatchPlot.Core.Models.PlotConfig>(first, "_config").StampLayerName = "设计印章";
                Field<FastBatchPlot.Core.Models.PlotConfig>(first, "_config").BookmarkTemplate = "{图名} / {比例}";
                Field<FastBatchPlot.Core.Models.PlotConfig>(first, "_config").PdfOptions = new FastBatchPlot.Core.Models.PdfOutputOptions { VectorResolutionDpi = 1200, TextToGeometry = false };
                Call(first, "SavePreferences");
                Check(File.Exists(settingsPath) && Field<Label>(first, "lblStatus").Text.Contains("已保存"), "保存默认设置将界面值写入显式指定的隔离文件");
            }
            using (var second = new BatchPlotForm(settingsPath))
            {
                Check(Convert.ToString(Field<ComboBox>(second, "cboPlotters").SelectedItem) == "ZWCAD to PDF.pc5" &&
                    Convert.ToString(Field<ComboBox>(second, "cboPlotStyles").SelectedItem) == "acad.ctb", "重新创建表单恢复指定打印设备和样式");
                Check(Field<NumericUpDown>(second, "numAreaFilter").Value == 6.3m && Math.Abs(Field<double>(second, "_minimumAreaPercent") - 6.3) < 1e-9,
                    "设置恢复将小数面积阈值同步到控件和筛选逻辑");
                Check(!Field<CheckBox>(second,"chkRemoveDuplicates").Checked && !Field<CheckBox>(second,"chkRemoveNestedFrames").Checked,
                    "保存默认设置及重新打开窗口保留两个关闭的过滤开关");
                Check(Field<NumericUpDown>(second, "numMargin").Value == -1.25m, "默认设置往返保留两位小数的负留白");
                Check(Field<ComboBox>(second, "cboSortRule").SelectedIndex == 3 && Field<TextBox>(second, "txtOutputFolder").Text == tempDirectory &&
                    Field<TextBox>(second, "txtNamingTemplate").Text == "{Index:D2}_{DwgName}" && Field<TextBox>(second, "txtMergedFileName").Text == "合并验收.pdf" &&
                    Field<CheckBox>(second, "chkMergePdf").Checked && Field<CheckBox>(second, "chkPrintSignatures").Checked &&
                    Field<CheckBox>(second, "chkPrintStamps").Checked, "默认设置往返保留排序、命名和合并，不启用签章图层隐藏");
                Check(Field<FastBatchPlot.Core.Models.PlotConfig>(second, "_config").SignatureLayerName == "MS_Sign" &&
                    Field<FastBatchPlot.Core.Models.PlotConfig>(second, "_config").StampLayerName == "MS_Stamp", "旧签章图层映射不会带入PDF工作流");
                Check(Field<FastBatchPlot.Core.Models.PlotConfig>(second, "_config").BookmarkTemplate == "{图名} / {比例}", "书签模板从主窗保存并在新窗口恢复");
                var pdf = Field<FastBatchPlot.Core.Models.PlotConfig>(second, "_config").PdfOptions;
                Check(pdf.VectorResolutionDpi == 1200 && pdf.TextToGeometry == false && pdf.RasterResolutionDpi == null, "主窗默认设置往返保留 PDF 三态参数");
                var printers = Field<ComboBox>(second, "cboPlotters");
                printers.Items.Add("已移除的测试设备.pc3");
                printers.SelectedItem = "已移除的测试设备.pc3";
                Call(second, "SavePreferences");
            }
            using (var missing = new BatchPlotForm(settingsPath))
                Check(Field<ComboBox>(missing, "cboPlotters").SelectedIndex == -1 && Field<Label>(missing, "lblStatus").Text.Contains("不可用"),
                    "原设备已移除时清空选择并明确提示重新选择");

            const string broken = "{配置文件损坏";
            File.WriteAllText(settingsPath, broken);
            using (var corrupt = new BatchPlotForm(settingsPath))
                Check(File.ReadAllText(settingsPath) == broken && Field<Label>(corrupt, "lblStatus").Text.Contains("读取失败"),
                    "损坏偏好不会被表单构造覆盖，界面报告读取失败");
            using (var offline = new BatchPlotForm(null))
            {
                Call(offline, "SavePreferences");
                Check(Field<Label>(offline, "lblStatus").Text.Contains("不写入") && File.ReadAllText(settingsPath) == broken,
                    "空配置路径的离线表单拒绝写用户默认设置");
            }
        }
        finally
        {
            CadHostProvider.Host = previousHost;
            CadHostProvider.Plotter = previousPlotter;
            foreach (string file in Directory.GetFiles(tempDirectory)) File.Delete(file);
            Directory.Delete(tempDirectory);
        }
    }
}
