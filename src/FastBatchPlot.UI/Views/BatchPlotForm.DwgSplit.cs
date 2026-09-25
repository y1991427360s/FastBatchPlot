using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Export;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.UI.Views
{
    public partial class BatchPlotForm
    {
        private void ExportSplitDwg()
        {
            if(_isPlotting)return;
            if(!CadHostProvider.IsInitialized || !(CadHostProvider.Host is ICadDwgSplitHost host))
            {lblStatus.Text="当前宿主不支持 DWG 拆分。";return;}
            dgvDrawings.EndEdit();
            var frames=DisplayOrder().Where(f=>f.IsSelected).ToList();
            if(frames.Count==0){lblStatus.Text="请先勾选需要拆分的图纸。";return;}
            using(var folder=new FolderBrowserDialog{Description="选择拆分 DWG 保存目录（不覆盖现有文件）"})
            {
                if(folder.ShowDialog(this)!=DialogResult.OK)return;
                try
                {
                    var planned=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var paths=frames.Select(f=>PlanOutputPath(folder.SelectedPath,f.CustomOutputFileName+".dwg",planned)).ToList();
                    SetPlottingState(true);
                    var plans=new List<DwgSplitPlan>();
                    foreach(var frame in frames)
                    {
                        try{plans.Add(host.InspectDwgSplit(frame));}
                        catch(Exception ex){plans.Add(new DwgSplitPlan(frame,Array.Empty<string>(),0,Array.Empty<string>(),new[]{ex.Message}));}
                    }
                    using(var preview=new DwgSplitPreviewForm(plans,paths))
                        if(preview.ShowDialog(this)!=DialogResult.OK)return;
                    ExecuteSplitPlans(host,frames,plans,paths);
                }
                catch(Exception ex){lblStatus.Text="拆分未完成："+ex.Message;}
                finally{SetPlottingState(false);RefreshGrid();}
            }
        }
        private void ExecuteSplitPlans(ICadDwgSplitHost host,IReadOnlyList<PlotFrame> frames,
            IReadOnlyList<DwgSplitPlan> plans,IReadOnlyList<string> paths)
        {
            if(frames.Count!=plans.Count || paths.Count!=plans.Count || plans.Any(p=>!p.CanExport))
                throw new InvalidOperationException("拆分清单未全部通过预检。");
            int done=0;
            for(int i=0;i<plans.Count;i++)
            {
                try
                {
                    host.ExportDwgSplit(plans[i],paths[i]);
                    if(!File.Exists(paths[i]) || new FileInfo(paths[i]).Length==0)throw new IOException("宿主未生成有效目标文件。");
                    frames[i].Status="DWG 已导出";frames[i].ErrorMessage=null;done++;
                }
                catch(Exception ex)
                {
                    frames[i].Status="DWG 失败";frames[i].ErrorMessage=ex.Message;
                    for(int j=i+1;j<frames.Count;j++){frames[j].Status="DWG 未执行";frames[j].ErrorMessage="前序拆分失败，任务已停止。";}
                    lblStatus.Text=$"拆分已停止，成功 {done}/{plans.Count}，已完成文件保留："+ex.Message;return;
                }
            }
            lblStatus.Text=$"拆分完成：{done} 个 DWG；请在 CAD 中复核标注、字段和字体。";
        }
    }
}
