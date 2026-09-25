using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Printing;
using FastBatchPlot.Core.Tasks;
using FastBatchPlot.Core.Models;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.PlottingServices;

namespace FastBatchPlot.AutoCAD
{
    public partial class AcadPlotEngine : ICadPlotResourceHost
    {
        public string GetPlotResourceRevision(FastBatchPlot.Core.Models.PlotConfig config)
        {
            var doc=Application.DocumentManager.MdiActiveDocument??throw new InvalidOperationException("没有活动文档。");
            using(doc.LockDocument())
            {
                var matches=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bool absolute=Path.IsPathRooted(config.PrinterDevice);
                using(var devices=PlotConfigManager.Devices)
                    foreach(PlotConfigInfo item in devices)
                    {
                        string path=item.FullPath;
                        if(string.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path))continue;
                        if(absolute?string.Equals(Path.GetFullPath(config.PrinterDevice),Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase):string.Equals(item.DeviceName,config.PrinterDevice,StringComparison.OrdinalIgnoreCase))
                            matches.Add(Path.GetFullPath(path));
                    }
                if(matches.Count!=1)throw new InvalidOperationException("无法唯一定位原打印设备配置，不能核对任务资源。");
                string source=matches.Single();
                if(!string.Equals(Path.GetExtension(source),".pc3",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("PDF 任务需要可核验的 pc3 配置。");
                var files=new List<string>(AcadPdfMediaFiles.DependencyFiles(source));
                if(!string.Equals(config.PlotStyleTable,"None",StringComparison.OrdinalIgnoreCase))
                {
                    // 使用当前 CAD 偏好的样式搜索路径，不把默认支持文件路径当作样式路径。
                    object preferences=Application.Preferences;
                    object filePreferences=preferences.GetType().InvokeMember("Files",System.Reflection.BindingFlags.GetProperty,null,preferences,null)!;
                    string search=Convert.ToString(filePreferences.GetType().InvokeMember("PrinterStyleSheetPath",System.Reflection.BindingFlags.GetProperty,null,filePreferences,null))??"";
                    string style=FastBatchPlot.Core.Tasks.PlotResourceRevision.ResolveStyle(config.PlotStyleTable,search);
                    if(string.IsNullOrWhiteSpace(style)||!Path.IsPathRooted(style))throw new InvalidOperationException("宿主未解析出打印样式的完整路径。");
                    files.Add(style);
                }
                return PlotResourceRevision.Compute(files);
            }
        }
    }
}
