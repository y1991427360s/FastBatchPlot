using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Naming;

namespace FastBatchPlot.UI.Views
{
    public sealed class RenumberForm : Form
    {
        private readonly List<PlotFrame> frames;
        private readonly List<PlotFrame> allFrames;
        private readonly TextBox prefix=new TextBox{Width=130};
        private readonly TextBox suffix=new TextBox{Width=100};
        private readonly NumericUpDown start=new NumericUpDown{Minimum=0,Maximum=int.MaxValue,Value=1,Width=95};
        private readonly NumericUpDown step=new NumericUpDown{Minimum=1,Maximum=100000,Value=1,Width=75};
        private readonly NumericUpDown digits=new NumericUpDown{Minimum=1,Maximum=9,Value=3,Width=50};
        private readonly DataGridView preview=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false};
        private readonly Label status=new Label{Dock=DockStyle.Bottom,Height=40};
        private readonly Button apply=new Button{Text="应用到列表",Dock=DockStyle.Bottom,Height=34};
        public List<DrawingNumberChange> Changes {get;private set;}=new List<DrawingNumberChange>();
        public RenumberForm(IEnumerable<PlotFrame> selected,IEnumerable<PlotFrame> all)
        {
            frames=selected.ToList();allFrames=all.ToList();Text="图号重编预览（不写回 DWG）";Size=new Size(820,560);MinimumSize=new Size(760,480);Font=new Font("Microsoft YaHei UI",9);StartPosition=FormStartPosition.CenterParent;
            var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=78};
            bar.Controls.AddRange(new Control[]{Label("前缀"),prefix,Label("起号"),start,Label("步长"),step,Label("位数"),digits,Label("后缀"),suffix});
            preview.Columns.Add("Name","图名");preview.Columns.Add("Old","原图号");preview.Columns.Add("New","新图号");preview.Columns.Add("Layout","布局");
            preview.Columns[0].Width=245;preview.Columns[1].Width=180;preview.Columns[2].Width=180;preview.Columns[3].Width=120;
            foreach(DataGridViewColumn c in preview.Columns)c.SortMode=DataGridViewColumnSortMode.NotSortable;
            Controls.Add(preview);Controls.Add(status);Controls.Add(apply);Controls.Add(bar);
            prefix.TextChanged+=(s,e)=>RefreshPreview();suffix.TextChanged+=(s,e)=>RefreshPreview();
            start.ValueChanged+=(s,e)=>RefreshPreview();step.ValueChanged+=(s,e)=>RefreshPreview();digits.ValueChanged+=(s,e)=>RefreshPreview();
            apply.Click+=(s,e)=>{RefreshPreview();if(apply.Enabled){DialogResult=DialogResult.OK;Close();}};
            RefreshPreview();
        }
        private static Label Label(string text)=>new Label{Text=text,AutoSize=true,Padding=new Padding(0,6,0,0)};
        private void RefreshPreview()
        {
            preview.Rows.Clear();apply.Enabled=false;
            try
            {
                Changes=DrawingListEdits.PlanNumbers(frames,new RenumberOptions{Prefix=prefix.Text,Suffix=suffix.Text,Start=(int)start.Value,Step=(int)step.Value,Digits=(int)digits.Value});
                var selected=new HashSet<PlotFrame>(frames);
                var existing=new HashSet<string>(allFrames.Where(f=>!selected.Contains(f)).Select(f=>f.TitleInfo.DrawingNo.Trim()),StringComparer.OrdinalIgnoreCase);
                int conflicts=0;
                foreach(var change in Changes)
                {
                    int row=preview.Rows.Add(change.Frame.TitleInfo.DrawingName,change.Before,change.After,change.Frame.LayoutName);
                    if(existing.Contains(change.After.Trim())){conflicts++;preview.Rows[row].Cells[2].Style.BackColor=Color.MistyRose;}
                }
                status.Text=conflicts>0?$"有 {conflicts} 个新图号与未重编图纸重复，请调整。":$"按当前显示顺序重编 {Changes.Count} 张；仅更新列表，可撤销最近一次重编。";
                apply.Enabled=conflicts==0;
            }
            catch(Exception ex){Changes.Clear();status.Text=ex.Message;}
        }
    }
}
