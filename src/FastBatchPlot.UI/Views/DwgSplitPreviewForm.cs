using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.Core.Export;

namespace FastBatchPlot.UI.Views
{
    public sealed class DwgSplitPreviewForm : Form
    {
        public DwgSplitPreviewForm(IReadOnlyList<DwgSplitPlan> plans,IReadOnlyList<string> paths)
        {
            if(plans.Count!=paths.Count)throw new ArgumentException("清单与路径数量不一致。");
            Text="拆分 DWG · 预检清单";Size=new Size(880,580);MinimumSize=new Size(700,440);
            StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",9);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new Padding(12)};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,50));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
            root.Controls.Add(new Label{Text="模型空间按图框范围拆分为 DWG 2013。跨边界对象整件保留，源图不修改。\n请逐项查看警告；红色项目必须处理后才能导出。",Dock=DockStyle.Fill},0,0);
            var grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells};
            grid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
            grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="目标文件",Width=220});
            grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="实体 / 跨界",Width=85});
            grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="预检结果",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill});
            for(int i=0;i<plans.Count;i++)
            {
                var p=plans[i];int row=grid.Rows.Add(paths[i],p.Handles.Count+" / "+p.CrossingCount,string.Join("\r\n",p.Errors.Concat(p.Warnings)));
                if(!p.CanExport)grid.Rows[row].DefaultCellStyle.BackColor=Color.MistyRose;
            }
            root.Controls.Add(grid,0,1);
            var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};
            var cancel=new Button{Text="返回",DialogResult=DialogResult.Cancel,AutoSize=true};
            var export=new Button{Text="确认清单并导出",DialogResult=DialogResult.OK,AutoSize=true,Enabled=plans.Count>0&&plans.All(p=>p.CanExport)};
            bar.Controls.Add(cancel);bar.Controls.Add(export);root.Controls.Add(bar,0,2);Controls.Add(root);CancelButton=cancel;
        }
    }
}
