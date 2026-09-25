using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;

namespace FastBatchPlot.UI.Views
{
    public partial class BatchPlotForm
    {
        private void InitializePaperColumns()
        {
            dgvDrawings.Columns.Add(new DataGridViewTextBoxColumn {Name="BasePaperSize",HeaderText="基础尺寸 mm",Width=140,ToolTipText="留白前的宽 × 高，输入如 841 × 594；保持原比例。双击打开尺寸与适配预览，点击表头批量设置全部图纸。",SortMode=DataGridViewColumnSortMode.NotSortable});
            dgvDrawings.Columns.Add(new DataGridViewTextBoxColumn {Name="PdfPaperSize",HeaderText="PDF 尺寸 mm",Width=145,ReadOnly=true,ToolTipText="根据当前留白计算的纸面尺寸，尚需驱动核验。"});
            dgvDrawings.Columns.Add(new DataGridViewTextBoxColumn {Name="PdfScale",HeaderText="PDF 实际比例",Width=130,ReadOnly=true,ToolTipText="负留白会改变最终出图比例。"});
            dgvDrawings.Columns["BasePaperSize"]!.DisplayIndex=6;
            dgvDrawings.Columns["PdfPaperSize"]!.DisplayIndex=7;
            dgvDrawings.Columns["PdfScale"]!.DisplayIndex=9;
            dgvDrawings.Columns[5].SortMode=DataGridViewColumnSortMode.NotSortable;
            dgvDrawings.Columns[5].ToolTipText="可直接编辑比例；点击表头可预览并统一设置全部图纸。";
            dgvDrawings.CellDoubleClick+=(s,e)=>
            {
                if(e.RowIndex<0||_isPlotting)return;
                if(e.ColumnIndex==dgvDrawings.Columns["BasePaperSize"]!.Index) EditCustomPaper();
                else if(e.ColumnIndex>=0&&dgvDrawings.Columns[e.ColumnIndex].ReadOnly) LocateSelectedFrameInCad();
            };
            dgvDrawings.ColumnHeaderMouseClick+=(s,e)=>
            {
                if(e.ColumnIndex==dgvDrawings.Columns["BasePaperSize"]!.Index) EditAllPaperDimensions();
                else if(e.ColumnIndex==5) EditAllScales();
            };
        }

        private void EditAllScales()
        {
            if(_isPlotting||_frames.Count==0)return;
            dgvDrawings.EndEdit();
            using(var dialog=new Form {Text="全部图纸：统一比例（含未勾选行）",ClientSize=new Size(640,420),StartPosition=FormStartPosition.CenterParent,Font=Font})
            {
                var scale=new NumericUpDown {Left=16,Top=16,Width=180,Minimum=0.001m,Maximum=1000000,DecimalPlaces=3,Value=100};
                var label=new Label {Left=210,Top=20,Width=410,Text="输入比例分母；保留每张纸的基础尺寸。"};
                var details=new TextBox {Left=16,Top=52,Width=608,Height=304,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
                var apply=new Button {Left=400,Top=374,Width=105,Text="应用到全部",DialogResult=DialogResult.OK};
                var cancel=new Button {Left=518,Top=374,Width=105,Text="取消",DialogResult=DialogResult.Cancel};
                Action preview=()=>
                {
                    var text=new System.Text.StringBuilder();int failed=0;
                    foreach(var frame in DisplayOrder())
                    {
                        try {var candidate=new Core.Tasks.BatchPage(frame,"").Frame;candidate.CalculatedScale=(double)scale.Value;
                            var plan=PlotPlanBuilder.Create(candidate,PaperPreviewConfig());
                            text.AppendLine(frame.OrderIndex+" / "+frame.TitleInfo.DrawingNo+"："+PaperDimensionText.Format(plan.PaperWidthMm,plan.PaperHeightMm)+" mm，1:"+plan.ScaleDenominator.ToString("0.########"));}
                        catch(Exception ex){failed++;text.AppendLine(frame.OrderIndex+"："+ex.Message);}
                    }
                    details.Text=text.ToString();apply.Enabled=failed==0;
                };
                scale.ValueChanged+=(s,e)=>preview();preview();
                dialog.Controls.AddRange(new Control[]{scale,label,details,apply,cancel});dialog.CancelButton=cancel;
                if(dialog.ShowDialog(this)==DialogResult.OK)ApplyScales(DisplayOrder(),(double)scale.Value);
            }
        }

        private void ApplyScales(List<PlotFrame> frames,double scale)
        {
            if(_isPlotting)throw new InvalidOperationException("任务运行时不能修改比例。");
            foreach(var frame in frames)
            {
                var copy=new Core.Tasks.BatchPage(frame,"").Frame;copy.CalculatedScale=scale;
                PlotPlanBuilder.Create(copy,PaperPreviewConfig());
            }
            foreach(var frame in frames)frame.CalculatedScale=scale;
            RefreshGrid();lblStatus.Text="已统一 "+frames.Count+" 张图纸比例，基础纸张尺寸保持不变。";
        }

        private void RefreshPaperPlans()
        {
            if(dgvDrawings==null||numMargin==null||!dgvDrawings.Columns.Contains("BasePaperSize"))return;
            foreach(DataGridViewRow row in dgvDrawings.Rows)
                if(row.Tag is PlotFrame frame)RefreshPaperPlan(row,frame);
        }

        private void RefreshPaperPlan(DataGridViewRow row,PlotFrame frame)
        {
            var paper=frame.DetectedPaper;
            bool landscape=_config.AutoOrientation?frame.IsLandscape:paper.IsLandscape;
            SetCellValue(row,dgvDrawings.Columns["BasePaperSize"]!.Index,PaperDimensionText.Format(landscape?paper.LongerEdgeMm:paper.ShorterEdgeMm,landscape?paper.ShorterEdgeMm:paper.LongerEdgeMm));
            var size=row.Cells["PdfPaperSize"];var scale=row.Cells["PdfScale"];
            try
            {
                var plan=PlotPlanBuilder.Create(frame,PaperPreviewConfig());
                SetCellValue(row,size.ColumnIndex,PaperDimensionText.Format(plan.PaperWidthMm,plan.PaperHeightMm));
                SetCellValue(row,scale.ColumnIndex,"1:"+plan.ScaleDenominator.ToString("0.########"));
                size.ErrorText=scale.ErrorText="";size.ToolTipText=scale.ToolTipText="计划值，打印时仍需驱动和生成 PDF 核验。";
                size.Style.BackColor=scale.Style.BackColor=Color.Empty;
            }
            catch(Exception ex)
            {
                SetCellValue(row,size.ColumnIndex,"设置无效");SetCellValue(row,scale.ColumnIndex,"—");
                size.ErrorText=scale.ErrorText=ex.Message;size.ToolTipText=scale.ToolTipText=ex.Message;
                size.Style.BackColor=scale.Style.BackColor=Color.MistyRose;
            }
        }

        private void ApplyPaperDimensionCell(DataGridViewRow row,PlotFrame frame)
        {
            string text=Convert.ToString(row.Cells["BasePaperSize"].Value)??"";
            try
            {
                if(!PaperDimensionText.TryParse(text,out double width,out double height))
                    throw new ArgumentException("尺寸格式为宽 × 高（mm），范围 1–100000，最多三位小数，如 841 × 594。");
                var candidate=CustomPaperEdit.Preview(frame,width,height,false);
                PlotPlanBuilder.Create(candidate,PaperPreviewConfig());
                frame.DetectedPaper=candidate.DetectedPaper;frame.IsLandscape=candidate.IsLandscape;
                SetCellValue(row,4,frame.DetectedPaper.Name);SetCellValue(row,6,frame.IsLandscape?"横向":"纵向");
                UpdateGeneratedFileName(frame,row);UpdateStats();
                lblStatus.Text="已修改基础纸张尺寸，保持原比例；PDF 尺寸列显示留白后的结果。";
            }
            catch(Exception ex){lblStatus.Text="尺寸未修改："+ex.Message;}
            RefreshPaperPlan(row,frame);
        }
    }
}
