using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Naming;

namespace FastBatchPlot.UI.Views
{
    public partial class BatchPlotForm
    {
        private List<DrawingNumberChange>? _lastRenumber;
        private List<PlotFrame> DisplayOrder()=>dgvDrawings.Rows.Cast<DataGridViewRow>().Where(r=>r.Tag is PlotFrame).Select(r=>(PlotFrame)r.Tag!).ToList();
        private void RenumberSelected(bool all)
        {
            if(_isPlotting)return;dgvDrawings.EndEdit();
            var order=DisplayOrder();
            var selected=new HashSet<PlotFrame>(dgvDrawings.SelectedRows.Cast<DataGridViewRow>().Select(r=>(PlotFrame)r.Tag!));
            var targets=all?order:order.Where(selected.Contains).ToList();
            if(targets.Count==0){lblStatus.Text="请先选中要重编的行。";return;}
            using(var dialog=new RenumberForm(targets,_frames))
                if(dialog.ShowDialog(this)==DialogResult.OK)ApplyRenumber(dialog.Changes);
        }
        private void ApplyRenumber(List<DrawingNumberChange> changes)
        {
            if(_isPlotting)return;
            try
            {
                DrawingListEdits.ApplyNumbers(changes,_frames);_lastRenumber=changes;
                RefreshGrid();lblStatus.Text=$"已重编 {changes.Count} 张的列表图号，未写回 DWG。";
            }
            catch(Exception ex){lblStatus.Text="编号未修改："+ex.Message;}
        }
        private void UndoRenumber()
        {
            if(_isPlotting)return;
            if(_lastRenumber==null){lblStatus.Text="没有可撤销的图号重编。";return;}
            try{DrawingListEdits.ApplyNumbers(_lastRenumber,_frames,true);_lastRenumber=null;RefreshGrid();lblStatus.Text="已撤销最近一次列表图号重编。";}
            catch(Exception ex){lblStatus.Text="无法撤销："+ex.Message;}
        }
        private void MoveSelectedRows(int direction)
        {
            if(_isPlotting)return;dgvDrawings.EndEdit();
            var selected=new HashSet<PlotFrame>(dgvDrawings.SelectedRows.Cast<DataGridViewRow>().Select(r=>(PlotFrame)r.Tag!));
            if(selected.Count==0)return;
            var moved=DrawingListEdits.Move(DisplayOrder(),selected,direction);
            _frames.Clear();_frames.AddRange(moved);
            // Custom 不跨布局重分组，允许用户明确指定跨布局顺序。
            if(cboSortRule.SelectedIndex!=(int)SortOrderRule.Custom)cboSortRule.SelectedIndex=(int)SortOrderRule.Custom;
            else ReorderFrames();
            dgvDrawings.ClearSelection();
            foreach(DataGridViewRow row in dgvDrawings.Rows)row.Selected=selected.Contains((PlotFrame)row.Tag!);
            lblStatus.Text="已按当前显示顺序移动选中行，并切换为手动顺序。";
        }
        private void RefreshDuplicateIndicators()
        {
            var issues=DrawingListDiagnostics.Find(_frames).ToDictionary(i=>i.Frame);
            foreach(DataGridViewRow row in dgvDrawings.Rows)
            {
                if(!(row.Tag is PlotFrame frame))continue;
                issues.TryGetValue(frame,out var issue);
                MarkCell(row.Cells[2],issue?.DuplicateNumber==true,"图号重复（忽略大小写和首尾空格）");
                MarkCell(row.Cells[7],issue?.DuplicateFileName==true,"输出文件名重复，文件输出前必须改名或取消勾选冲突页");
            }
        }
        private static void MarkCell(DataGridViewCell cell,bool duplicate,string message)
        {
            cell.Style.BackColor=duplicate?Color.MistyRose:Color.Empty;
            cell.Style.ForeColor=duplicate?Color.DarkRed:Color.Empty;
            cell.Style.SelectionBackColor=duplicate?Color.Firebrick:Color.Empty;
            cell.ToolTipText=duplicate?message:"";
        }
    }
}
