using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;

namespace FastBatchPlot.UI.Views
{
    public sealed class PageMarginsForm : Form
    {
        private readonly ComboBox mode=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
        private readonly NumericUpDown[] sides=Enumerable.Range(0,4).Select(_=>new NumericUpDown{Minimum=0,Maximum=100,DecimalPlaces=2,Increment=0.5m,Dock=DockStyle.Fill}).ToArray();
        private readonly DataGridView preview=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
        private readonly Button apply=new Button{Text="应用",DialogResult=DialogResult.OK,AutoSize=true};
        private readonly CheckBox outerBorder=new CheckBox{Text="不留白时打印最外边线",AutoSize=true,Checked=true};
        private readonly NumericUpDown borderInset=new NumericUpDown{Minimum=0.01m,Maximum=5,DecimalPlaces=2,Increment=0.05m,Value=0.25m,Width=80};
        public bool PrintOuterBorderLine=>outerBorder.Checked;
        public double OuterBorderInsetMm=>(double)borderInset.Value;
        private readonly IReadOnlyList<PlotFrame> frames;
        private readonly double uniform;
        public PageMargins Value=>new PageMargins{Mode=(MarginMode)mode.SelectedIndex,Left=(double)sides[0].Value,Right=(double)sides[1].Value,Top=(double)sides[2].Value,Bottom=(double)sides[3].Value};
        public PageMarginsForm(PageMargins initial,double uniformMargin,IReadOnlyList<PlotFrame> frames,bool printOuterBorderLine=true,double outerBorderInsetMm=0.25)
        {
            this.frames=frames;uniform=uniformMargin;Text="四边留白与比例预览";Size=new Size(800,500);MinimumSize=new Size(700,440);StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",9);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=6,ColumnCount=1,Padding=new Padding(12)};
            foreach(float h in new[]{36f,44f,82f,36f})root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
            mode.Items.AddRange(new object[]{"统一留白（使用主窗口正负数值）","四边独立 · 增大纸张，保持比例","四边独立 · 固定纸张，等比缩小内容"});root.Controls.Add(mode,0,0);
            var inputs=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=8};string[] labels={"左 mm","右 mm","上 mm","下 mm"};
            double[] values={initial.Left,initial.Right,initial.Top,initial.Bottom};
            for(int i=0;i<4;i++){inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,58));inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));inputs.Controls.Add(new Label{Text=labels[i],Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},i*2,0);sides[i].Value=(decimal)values[i];inputs.Controls.Add(sides[i],i*2+1,0);sides[i].ValueChanged+=(s,e)=>RefreshPreview();}
            root.Controls.Add(inputs,0,1);
            root.Controls.Add(new Label{Dock=DockStyle.Fill,Text="内容在留白区域内居中；下表为几何计划，尚未匹配驱动。\n关闭最外边线：仅在四边均不留白时，裁去指定纸面宽度的一圈内容。\n纸张、比例及其余内容位置不变；粗边框需调整宽度，边缘文字也会被裁切。"},0,2);
            var borderBar=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
            outerBorder.Checked=printOuterBorderLine;borderInset.Value=(decimal)outerBorderInsetMm;
            borderBar.Controls.Add(outerBorder);borderBar.Controls.Add(new Label{Text="裁切宽度 mm",AutoSize=true,Padding=new Padding(10,5,0,0)});borderBar.Controls.Add(borderInset);root.Controls.Add(borderBar,0,3);
            outerBorder.CheckedChanged+=(s,e)=>RefreshPreview();borderInset.ValueChanged+=(s,e)=>RefreshPreview();
            foreach(string title in new[]{"图号 / 序号","纸张 mm","实际比例","左 / 右 mm","上 / 下 mm","状态"})preview.Columns.Add(title,title);
            root.Controls.Add(preview,0,4);
            var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text="取消",DialogResult=DialogResult.Cancel,AutoSize=true};bar.Controls.Add(cancel);bar.Controls.Add(apply);root.Controls.Add(bar,0,5);Controls.Add(root);CancelButton=cancel;
            mode.SelectedIndexChanged+=(s,e)=>RefreshPreview();mode.SelectedIndex=(int)initial.Mode;
        }
        private void RefreshPreview()
        {
            foreach(var input in sides)input.Enabled=mode.SelectedIndex!=0;preview.Rows.Clear();bool valid=true;
            borderInset.Enabled=!outerBorder.Checked;
            foreach(var frame in frames)
            {
                string name=string.IsNullOrWhiteSpace(frame.TitleInfo.DrawingNo)?frame.OrderIndex.ToString():frame.TitleInfo.DrawingNo;
                try
                {
                    var p=PlotPlanBuilder.Create(frame,new PlotConfig{MarginMm=uniform,Margins=Value,PrintOuterBorderLine=PrintOuterBorderLine,OuterBorderInsetMm=OuterBorderInsetMm});
                    preview.Rows.Add(name,$"{p.PaperWidthMm:0.##} × {p.PaperHeightMm:0.##}",$"1:{p.ScaleDenominator:0.####}",
                        $"{p.ContentLeftMm:0.##} / {p.PaperWidthMm-p.ContentLeftMm-p.ContentWidthMm:0.##}",
                        $"{p.PaperHeightMm-p.ContentBottomMm-p.ContentHeightMm:0.##} / {p.ContentBottomMm:0.##}",p.AppliedBorderInsetMm>0?$"裁边 {p.AppliedBorderInsetMm:0.##} mm":"待驱动匹配");
                }
                catch(Exception ex){int row=preview.Rows.Add(name,"","","","",ex.Message);preview.Rows[row].DefaultCellStyle.BackColor=Color.MistyRose;valid=false;}
            }
            apply.Enabled=valid;
        }
    }
}
