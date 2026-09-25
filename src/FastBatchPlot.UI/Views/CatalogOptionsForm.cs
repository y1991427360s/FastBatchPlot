using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.Core.Export;

namespace FastBatchPlot.UI.Views
{
    public sealed class CatalogOptionsForm : Form
    {
        public CatalogOptions Options {get;private set;}=new CatalogOptions();
        private readonly DataGridView columns=new DataGridView{Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false};
        private readonly TextBox title=new TextBox{Width=210,Text="图纸目录"};
        private readonly CheckBox selected=new CheckBox{Text="仅勾选图纸",Checked=true,AutoSize=true};
        private readonly CheckBox statistics=new CheckBox{Text="附纸张统计",Checked=true,AutoSize=true};
        private readonly NumericUpDown height=new NumericUpDown{Minimum=16,Maximum=120,Value=28,Width=60};
        private readonly Label status=new Label{Dock=DockStyle.Bottom,Height=35};
        public CatalogOptionsForm(CatalogOptions? initial=null,string actionText="选择文件并导出",IReadOnlyList<KeyValuePair<string,CatalogOptions>>? profiles=null)
        {
            Options=(initial??new CatalogOptions()).Copy();
            Text="目录导出设置";Size=new Size(700,660);MinimumSize=new Size(620,580);StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",9);
            columns.Columns.Add(new DataGridViewCheckBoxColumn{HeaderText="导出",Width=50});
            columns.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="字段",ReadOnly=true,Width=110});
            columns.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="列名",Width=210});
            columns.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="列宽",Width=80});
            foreach(DataGridViewColumn c in columns.Columns)c.SortMode=DataGridViewColumnSortMode.NotSortable;
            LoadOptions(Options);
            var top=new FlowLayoutPanel{Dock=DockStyle.Top,Height=80};
            top.Controls.AddRange(new Control[]{new Label{Text="标题",AutoSize=true},title,selected,statistics,new Label{Text="行高(磅)",AutoSize=true},height});
            var up=new Button{Text="上移",AutoSize=true};up.Click+=(s,e)=>MoveColumn(-1);
            var down=new Button{Text="下移",AutoSize=true};down.Click+=(s,e)=>MoveColumn(1);
            top.Controls.Add(up);top.Controls.Add(down);
            var save=new Button{Text=actionText,Dock=DockStyle.Bottom,Height=36};
            save.Click+=(s,e)=>{try{Options=ReadOptions();DialogResult=DialogResult.OK;Close();}catch(Exception ex){status.Text=ex.Message;}};
            if(profiles!=null&&profiles.Count>1)
            {
                top.Height=116;
                var source=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=310};
                source.Items.AddRange(profiles.Select(p=>p.Key).Cast<object>().ToArray());
                top.SetFlowBreak(down,true);top.Controls.Add(new Label{Text="选择本次格式",AutoSize=true});top.Controls.Add(source);
                save.Enabled=false;status.Text="存在图框专用目录设置，请明确选择本次格式（应用于本次全部导出图纸）。";
                source.SelectedIndexChanged+=(s,e)=>{if(source.SelectedIndex>=0){LoadOptions(profiles[source.SelectedIndex].Value);save.Enabled=true;status.Text="已选："+profiles[source.SelectedIndex].Key;}};
            }
            Controls.Add(columns);Controls.Add(status);Controls.Add(save);Controls.Add(top);
        }
        private void LoadOptions(CatalogOptions options)
        {
            Options=options.Copy();title.Text=Options.Title;selected.Checked=Options.SelectedOnly;statistics.Checked=Options.IncludeStatistics;height.Value=(decimal)Options.RowHeight;
            columns.Rows.Clear();var defaults=Options.Columns;
            var order=defaults.Select(c=>c.Field).Concat(Enum.GetValues(typeof(CatalogField)).Cast<CatalogField>().Where(f=>defaults.All(c=>c.Field!=f)));
            foreach(var f in order)
            {
                var c=defaults.FirstOrDefault(x=>x.Field==f);int row=columns.Rows.Add(c!=null,CatalogOptions.Label(f),c?.Header??CatalogOptions.Label(f),c?.Width??18);columns.Rows[row].Tag=f;
            }
        }
        private void MoveColumn(int delta)
        {
            if(columns.CurrentRow==null)return;columns.EndEdit();int from=columns.CurrentRow.Index,to=from+delta;
            if(to<0||to>=columns.Rows.Count)return;
            var row=columns.Rows[from];columns.Rows.RemoveAt(from);columns.Rows.Insert(to,row);columns.CurrentCell=row.Cells[1];
        }
        private CatalogOptions ReadOptions()
        {
            columns.EndEdit();
            var result=new CatalogOptions{Title=title.Text.Trim(),SelectedOnly=selected.Checked,IncludeStatistics=statistics.Checked,RowHeight=(double)height.Value};
            result.Columns.Clear();
            foreach(DataGridViewRow row in columns.Rows)
            {
                if(!Convert.ToBoolean(row.Cells[0].Value))continue;
                if(!double.TryParse(Convert.ToString(row.Cells[3].Value),out double width)||double.IsNaN(width)||double.IsInfinity(width)||width<6||width>100)
                    throw new ArgumentException("列宽必须为 6 到 100。");
                string label=Convert.ToString(row.Cells[2].Value)?.Trim()??"";
                if(label.Length==0||label.Length>100)throw new ArgumentException("列名须为 1 到 100 个字符。");
                result.Columns.Add(new CatalogColumn{Field=(CatalogField)row.Tag!,Header=label,Width=width});
            }
            if(result.Columns.Count==0||string.IsNullOrWhiteSpace(result.Title))throw new ArgumentException("请输入标题并至少选择一列。");
            result.Validate();return result;
        }
    }
}
