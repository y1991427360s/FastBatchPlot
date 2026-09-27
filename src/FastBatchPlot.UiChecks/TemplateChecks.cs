using System.Reflection;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckTemplatesUi()
    {
        var previous = CadHostProvider.Host;
        var host = new TemplateHost(); CadHostProvider.Host = host;
        string directory = Path.Combine(Path.GetTempPath(), "TemplateUi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var template = new TitleBlockTemplate { Name = "电气", BlockName = "A1框", Priority = 2,
                Fields = new() { new TitleFieldRule { Field = TitleField.DrawingName, AttributeTag = "TITLE" }, new TitleFieldRule { Field = TitleField.DrawingNo, AttributeTag = "NO" } } };
            var library = new TitleTemplateLibrary { Templates = new() { template } };
            string libraryPath = Path.Combine(directory, "title-templates.json");
            TitleTemplateStore.Save(libraryPath, library);
            using var form = new BatchPlotForm(Path.Combine(directory, "settings.json"));
            var frames = Field<List<PlotFrame>>(form, "_frames");
            var a = new PlotFrame { Type = FrameType.BlockReference, SourceBlockName = "A1框", HandleOrId = "1", TitleInfo = new TitleBlockInfo { DrawingName = "旧名称" } };
            var b = new PlotFrame { Type = FrameType.BlockReference, SourceBlockName = "A1框", HandleOrId = "2", TitleInfo = new TitleBlockInfo { DrawingName = "失败前名称" } };
            frames.Add(a); frames.Add(b); Call(form, "RefreshGrid");
            var grid = Field<DataGridView>(form, "dgvDrawings");
            grid.Rows[0].Cells[7].Value = "人工命名";
            Call(form, "ExtractTitleTemplates");
            Check(a.TitleInfo.DrawingName == "配电平面" && a.TitleInfo.DrawingNo == "电施-02" && a.TemplatePriority == 2, "模板提取更新字段和优先级");
            Check(a.CustomOutputFileName == "人工命名", "模板提取后人工文件名保留");
            Check(b.TitleInfo.DrawingName == "失败前名称" && b.Status.Contains("模拟采集失败"), "采集失败保留该页原信息并显示原因");
            Check(Field<Label>(form, "lblStatus").Text.Contains("失败 1"), "部分提取失败在汇总中明确报告");
            grid.CurrentCell = grid.Rows[1].Cells[0];
            var selectedFrameSample = (PlotFrame?)Call(form, "FindTemplateSampleForFieldPick", "A1框", host);
            Check(ReferenceEquals(selectedFrameSample, b), "模板字段拾取优先使用打印列表当前行的同名图框");
            host.FailSecond = false; Call(form, "ExtractTitleTemplates");
            Check(b.CustomOutputFileName.Contains("配电平面") && b.CustomOutputFileName.Contains("电施-02"), "模板提取刷新非人工输出名");
            string desktopTk = @"D:\Users\ys199\Desktop\图框信息配置文件-new.tk";
            if (File.Exists(desktopTk))
            {
                var importedTk = TkFormatService.Load(desktopTk);
                Check(importedTk.Templates.Count == 26, "真实.tk配置文件加载成功并包含26个模板");
                string testExportTk = Path.Combine(directory, "export-test.tk");
                TkFormatService.Save(testExportTk, importedTk);
                var reloadedTk = TkFormatService.Load(testExportTk);
                Check(reloadedTk.Templates.Count == 26 && reloadedTk.Templates[8].Name == "A3", ".tk图框配置导出并再次导入无损一致");
            }
        }
        finally
        {
            CadHostProvider.Host = previous;
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
    private sealed class TemplateHost : ICadHost, ICadTitleTemplateHost, ICadTemplateCropHost
    {
        public bool FailSecond = true;
        public Rect2D ResolveTemplatePrintBounds(PlotFrame frame,TemplateRegion region)
        {if(frame.HandleOrId=="2"&&FailSecond)throw new InvalidOperationException("模拟裁切范围失败");return new Rect2D(10,20,430,317);}
        public string PlatformName => "离线模板宿主";
        public string Version => "test";
        public bool FindTemplateSample(string blockName, out PlotFrame? frame) { frame = null; return false; }
        public bool PromptSelectTemplateSample(out PlotFrame? frame) { frame = null; return false; }
        public bool PromptTemplateRegion(PlotFrame frame, out TemplateRegion? region) { region = null; return false; }
        public List<TemplateText> CollectTemplateTexts(PlotFrame frame)
        {
            if (frame.HandleOrId == "2" && FailSecond) throw new InvalidOperationException("模拟采集失败");
            return new() { new TemplateText { Text="配电平面",AttributeTag="TITLE" },new TemplateText { Text="电施-02",AttributeTag="NO" } };
        }
        public void WriteMessage(string message) { }
        public string GetCurrentDocumentPath() => "D:\\offline\\template.dwg";
        public string GetCurrentDocumentName() => "template.dwg";
        public List<RawPolylineCandidate> CollectPolylineCandidates(string layerFilter="*") => new();
        public List<RawBlockCandidate> CollectBlockCandidates(string blockNameFilter="*") => new();
        public bool PromptSelectFrames(out List<RawPolylineCandidate> polylines,out List<RawBlockCandidate> blocks) {polylines=new();blocks=new();return false;}
        public bool PromptSelectSampleBlock(out string blockName,out string layerName) {blockName="";layerName="";return false;}
        public void ZoomToFrame(double minX,double minY,double maxX,double maxY) { }
        public List<string> GetAllLayers() => new();
        public List<string> GetAllBlockNames() => new();
    }
}
