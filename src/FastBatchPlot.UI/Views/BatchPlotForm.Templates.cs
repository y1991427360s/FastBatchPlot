using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;

namespace FastBatchPlot.UI.Views
{
    public partial class BatchPlotForm
    {
        private TitleTemplateLibrary _titleTemplates = new TitleTemplateLibrary();
        private string? _templateLoadError;
        private string? TitleTemplatePath => _settingsPath == null ? null : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_settingsPath))!, "title-templates.json");
        private void LoadTitleTemplates()
        {
            if (TitleTemplatePath == null) return;
            try
            {
                _titleTemplates = TitleTemplateStore.Load(TitleTemplatePath);
                var legacy = TitleTemplateService.DescribeLegacyTkImport(_titleTemplates);
                if (legacy != null) lblStatus.Text = legacy;
            }
            catch (Exception ex) { _templateLoadError = ex.Message; lblStatus.Text = "模板库读取失败：" + ex.Message; }
        }
        private void ManageTitleTemplates()
        {
            if (_isPlotting) return;
            if (_templateLoadError != null) { lblStatus.Text = "请先修复或备份并移走损坏模板库，再重新打开窗口：" + _templateLoadError; return; }
            using (var editor = new TitleTemplateForm(_titleTemplates, TitleTemplatePath, blockName =>
            {
                if (!(CadHostProvider.Host is ICadTitleTemplateHost host))
                    throw new InvalidOperationException("当前 CAD 宿主不支持图框拾取。");
                return FindTemplateSampleForFieldPick(blockName, host);
            }, () =>
            {
                if (!(CadHostProvider.Host is ICadTitleTemplateHost host))
                    throw new InvalidOperationException("当前 CAD 宿主不支持图框拾取。");
                return host.PromptSelectTemplateSample(out var sample) ? sample : null;
            }, sample =>
            {
                if (!(CadHostProvider.Host is ICadTitleTemplateHost host))
                    throw new InvalidOperationException("当前 CAD 宿主不支持区域拾取。");
                return host.PromptTemplateRegion(sample, out var region) ? region : null;
            }))
            {
                if (editor.ShowDialog(this) == DialogResult.OK)
                {
                    _titleTemplates = editor.Library;
                    RefreshFileNames();
                    lblStatus.Text = TitleTemplatePath == null ? "模板已更新到本次内存；可导出文件保存。" : "模板库已保存。请点击提取图框信息应用规则。";
                }
            }
        }
        private PlotFrame? FindTemplateSampleForFieldPick(string blockName, ICadTitleTemplateHost host)
        {
            var candidates = new System.Collections.Generic.List<PlotFrame>();
            if (dgvDrawings?.CurrentRow?.Tag is PlotFrame current) candidates.Add(current);
            if (dgvDrawings != null)
                candidates.AddRange(dgvDrawings.SelectedRows.Cast<DataGridViewRow>().Select(row => row.Tag as PlotFrame).Where(frame => frame != null).Cast<PlotFrame>());
            candidates.AddRange(_frames.Where(frame => frame.IsSelected));
            var contextHost = CadHostProvider.Host as ICadContextHost;
            foreach (var frame in candidates)
            {
                if (frame == null || frame.Type != FrameType.BlockReference
                    || !string.Equals(frame.SourceBlockName.Trim(), blockName.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                if (contextHost == null || contextHost.IsFrameContextCurrent(frame)) return frame;
            }
            return host.FindTemplateSample(blockName, out var sample) ? sample : null;
        }
        private void ExtractTitleTemplates()
        {
            if (_isPlotting) return;
            if (!(CadHostProvider.Host is ICadTitleTemplateHost host)) { lblStatus.Text = "当前宿主不支持模板文字采集。"; return; }
            if (_templateLoadError != null) { lblStatus.Text = "模板库无效：" + _templateLoadError; return; }
            int success = 0, failed = 0, skipped = 0, warnings = 0;
            foreach (var frame in _frames.Where(f => f.IsSelected))
            {
                var template = (!string.IsNullOrEmpty(frame.TemplateId) ? _titleTemplates.Templates.FirstOrDefault(t => t.Id == frame.TemplateId) : null)
                    ?? TitleTemplateService.FindBestTemplate(_titleTemplates.Templates.Where(t => string.Equals(t.BlockName.Trim(), frame.SourceBlockName, StringComparison.OrdinalIgnoreCase)), frame.Width, frame.Height);
                if (template == null || frame.Type != FrameType.BlockReference) { skipped++; continue; }
                try
                {
                    var texts = host.CollectTemplateTexts(frame);
                    var result = TitleTemplateService.Extract(template, texts, frame.TitleInfo);
                    frame.TitleInfo = result.Info;
                    frame.TemplateId = template.Id; frame.TemplatePriority = template.Priority;
                    warnings += result.Warnings.Count;
                    frame.Status = result.Warnings.Count == 0 ? "信息已提取" : string.Join("；", result.Warnings);
                    success++;
                }
                catch (Exception ex) { failed++; frame.Status = "提取失败：" + ex.Message; }
            }
            ReorderFrames();
            lblStatus.Text = $"信息提取：处理 {success} 张，失败 {failed} 张，无匹配模板 {skipped} 张，字段警告 {warnings} 条；未写回 DWG。";
        }
    }
}
