using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;

namespace FastBatchPlot.UI.Views
{
    public sealed class TitleWriteForm : Form
    {
        private readonly CheckedListBox fields=new CheckedListBox{Dock=DockStyle.Fill,CheckOnClick=true};
        private readonly CheckBox replace=new CheckBox{Text="勾选字段统一替换为右侧值（否则使用各图列表值）",AutoSize=true};
        private readonly TextBox value=new TextBox{Dock=DockStyle.Fill};
        private readonly DataGridView grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells};
        private readonly Button apply=new Button{Text="写入 DWG（不保存文件）",AutoSize=true,Enabled=false,DialogResult=DialogResult.OK};
        private readonly Label status=new Label{Dock=DockStyle.Fill};
        private readonly IReadOnlyList<PlotFrame> frames;
        private readonly Func<PlotFrame,IDictionary<TitleField,string>,TitleWritePlan> prepare;
        public List<TitleWritePlan> Plans {get;private set;}=new List<TitleWritePlan>();
        public TitleWriteForm(IReadOnlyList<PlotFrame> frames,Func<PlotFrame,IDictionary<TitleField,string>,TitleWritePlan> prepare)
        {
            this.frames=frames;this.prepare=prepare;Text="标题栏写回 · 修改前预览";Size=new Size(1050,650);MinimumSize=new Size(940,560);StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",9);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=4,Padding=new Padding(12)};root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,140));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,65));root.RowStyles.Add(new RowStyle(SizeType.Absolute,38));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            var info=new Label{Dock=DockStyle.Fill,Text="写回勾选图纸。先选字段，再生成预览；全部通过后才修改当前 DWG。\n多行文字的新值按纯文本写入，原行内格式将被替换；图层、位置、文字样式等保持。空值会清空文字。\n共享块定义、字段表达式、锁定图层及无法唯一定位的对象会阻止写入。"};root.Controls.Add(info,0,0);root.SetColumnSpan(info,2);
            var edit=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};edit.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));edit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));edit.Controls.Add(replace,0,0);edit.Controls.Add(value,1,0);root.Controls.Add(edit,0,1);root.SetColumnSpan(edit,2);
            fields.Items.AddRange(TitleTemplateService.FieldLabels.Cast<object>().ToArray());fields.SetItemChecked(0,true);root.Controls.Add(fields,0,2);
            foreach(string label in new[]{"图纸序号","字段","对象句柄","原值（含格式）","新值（含格式）","状态"})grid.Columns.Add(label,label);grid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;root.Controls.Add(grid,1,2);
            var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text="取消",AutoSize=true,DialogResult=DialogResult.Cancel};var preview=new Button{Text="生成修改预览",AutoSize=true};preview.Click+=(s,e)=>BuildPreview();bar.Controls.Add(cancel);bar.Controls.Add(apply);bar.Controls.Add(preview);root.Controls.Add(bar,1,3);root.Controls.Add(status,0,3);Controls.Add(root);CancelButton=cancel;
            value.Enabled=false;fields.ItemCheck+=(s,e)=>InvalidatePreview();replace.CheckedChanged+=(s,e)=>{value.Enabled=replace.Checked;InvalidatePreview();};value.TextChanged+=(s,e)=>InvalidatePreview();
        }
        private void InvalidatePreview(){apply.Enabled=false;Plans.Clear();grid.Rows.Clear();status.Text="设置已变化，请重新预览。";}
        private void BuildPreview()
        {
            Plans.Clear();grid.Rows.Clear();apply.Enabled=false;
            if(fields.CheckedIndices.Count==0){status.Text="请勾选字段。";return;}
            try
            {
                foreach(var frame in frames)
                {
                    var desired=fields.CheckedIndices.Cast<int>().ToDictionary(i=>(TitleField)i,i=>replace.Checked?value.Text:TitleTemplateService.Get(frame.TitleInfo,(TitleField)i));
                    var p=prepare(frame,desired);Plans.Add(p);
                    foreach(var c in p.Changes)grid.Rows.Add(frame.OrderIndex,TitleTemplateService.FieldLabels[(int)c.Field],c.Handle,c.Before,c.After,"待写回");
                    foreach(var error in p.Errors){int row=grid.Rows.Add(frame.OrderIndex,"","","","",error);grid.Rows[row].DefaultCellStyle.BackColor=Color.MistyRose;}
                    if(p.Changes.Count==0&&p.Errors.Count==0)grid.Rows.Add(frame.OrderIndex,"","","","","无需修改");
                }
                TitleWritePlanner.ValidateBatch(Plans);int count=Plans.Sum(p=>p.Changes.Count);apply.Enabled=count>0;status.Text=$"{count} 处修改待写回。";
            }
            catch(Exception ex){status.Text=ex.Message;}
        }
    }
}

