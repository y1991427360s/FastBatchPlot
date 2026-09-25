using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;

namespace FastBatchPlot.UI.Views
{
    public partial class BatchPlotForm
    {
        private List<TitleWritePlan>? _lastTitleWrite;
        private void WriteTitlesToDwg()
        {
            if(_isPlotting)return;
            if(!(CadHostProvider.Host is ICadTitleWriteHost host)){lblStatus.Text="当前宿主不支持标题栏写回。";return;}
            if(_templateLoadError!=null){lblStatus.Text="模板库无效："+_templateLoadError;return;}
            dgvDrawings.EndEdit();var frames=DisplayOrder().Where(f=>f.IsSelected).ToList();
            if(frames.Count==0){lblStatus.Text="请先勾选图纸。";return;}
            SetPlottingState(true);
            try
            {
                using(var dialog=new TitleWriteForm(frames,(frame,values)=>
                {
                    var template=_titleTemplates.Templates.SingleOrDefault(t=>string.Equals(t.BlockName.Trim(),frame.SourceBlockName,StringComparison.OrdinalIgnoreCase));
                    if(template==null)throw new InvalidOperationException("图纸 "+frame.OrderIndex+" 没有匹配的图框模板。");
                    return host.PrepareTitleWrite(frame,template,values);
                }))
                {
                    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                    CommitTitleWrites(host,dialog.Plans);
                }
            }
            catch(Exception ex){lblStatus.Text="写回未完成："+ex.Message;}
            finally{SetPlottingState(false);RefreshGrid();}
        }
        private void CommitTitleWrites(ICadTitleWriteHost host,List<TitleWritePlan> plans)
        {
            TitleWritePlanner.ValidateBatch(plans);host.ApplyTitleWrites(plans);_lastTitleWrite=plans.ToList();
            foreach(var plan in plans)
            {
                var source=plan.Source;
                foreach(var frame in _frames.Where(f=>f.SourceDocumentId==source.SourceDocumentId&&f.SourceLayoutId==source.SourceLayoutId&&f.HandleOrId==source.HandleOrId))
                {
                    foreach(var value in plan.Values)typeof(TitleBlockInfo).GetProperty(value.Key.ToString())!.SetValue(frame.TitleInfo,value.Value);
                    frame.Status="标题已写回（未保存）";
                }
            }
            lblStatus.Text=$"已写回 {plans.Sum(p=>p.Changes.Count)} 处文字；DWG 尚未保存，可撤销最近一次写回。";
        }
        private void UndoTitleWrite()
        {
            if(_isPlotting)return;
            if(_lastTitleWrite==null||!(CadHostProvider.Host is ICadTitleWriteHost host)){lblStatus.Text="没有可撤销的标题写回。";return;}
            SetPlottingState(true);
            try{host.ApplyTitleWrites(_lastTitleWrite,true);_lastTitleWrite=null;lblStatus.Text="已撤销最近一次 DWG 标题写回；列表仍保留待写入值，可重新提取图框信息。";}
            catch(Exception ex){lblStatus.Text="撤销未完成："+ex.Message;}
            finally{SetPlottingState(false);}
        }
    }
}
