using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.Core.Templates;
namespace FastBatchPlot.UI.Views
{
    public sealed class TemplateCropForm : Form
    {
        private readonly TextBox[] coordinates=Enumerable.Range(0,4).Select(_=>new TextBox{Dock=DockStyle.Fill}).ToArray();
        private readonly CheckBox enabled=new CheckBox{Text="启用模板打印范围",AutoSize=true};
        private readonly NumericUpDown scale=new NumericUpDown{Minimum=0,Maximum=1000000,DecimalPlaces=4,Width=105};
        public double PrintScale => (double)scale.Value;
        private readonly Label status=new Label{Dock=DockStyle.Fill};
        public TemplateRegion? PrintRegion {get;private set;}
        public TemplateCropForm(TemplateRegion? initial,Func<TemplateRegion?> pick,double initialScale=0,bool stampMode=false)
        {
            Text="图框模板打印范围";Size=new Size(680,300);MinimumSize=Size;scale.Value=(decimal)initialScale;StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",9);
            if(stampMode){Text="图框模板印章区域";enabled.Text="启用模板印章区域";}
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=4,RowCount=5};
            for(int i=0;i<4;i++)root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,40));root.RowStyles.Add(new RowStyle(SizeType.Absolute,30));root.RowStyles.Add(new RowStyle(SizeType.Absolute,38));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            root.Controls.Add(enabled,0,0);root.SetColumnSpan(enabled,1);
            if(!stampMode){root.Controls.Add(new Label{Text="搜索比例 1:（0自动）",Dock=DockStyle.Fill},1,0);root.Controls.Add(scale,2,0);}
            string[] labels={"X1（左）","Y1（下）","X2（右）","Y2（上）"};
            for(int i=0;i<4;i++){root.Controls.Add(new Label{Text=labels[i],Dock=DockStyle.Fill},i,1);root.Controls.Add(coordinates[i],i,2);}
            root.Controls.Add(new Label{Text=stampMode?"坐标为图框本地右下角偏移，与字段区域使用同一基准。\nPNG 保持原比例居中放入此区域，随图框变换，输出期间临时插入；源图不会保留附加印章。":"坐标为图框本地右下角偏移，与字段区域使用同一基准。\n应用范围时保持列表出图比例，根据裁切宽高重新计算纸张尺寸；打印前需匹配驱动纸张。",Dock=DockStyle.Fill},0,3);root.SetColumnSpan(root.GetControlFromPosition(0,3)!,4);
            var picker=new Button{Text="从图中拾取",Dock=DockStyle.Fill};picker.Click+=(s,e)=>{try{Hide();var r=pick();if(r!=null){LoadRegion(r);enabled.Checked=true;}}catch(Exception ex){status.Text=ex.Message;}finally{Show();}};root.Controls.Add(picker,3,0);
            var apply=new Button{Text="应用",Dock=DockStyle.Fill};apply.Click+=(s,e)=>{try{PrintRegion=ReadRegion();DialogResult=DialogResult.OK;Close();}catch(Exception ex){status.Text=ex.Message;}};
            var cancel=new Button{Text="取消",Dock=DockStyle.Fill,DialogResult=DialogResult.Cancel};root.Controls.Add(status,0,4);root.SetColumnSpan(status,2);root.Controls.Add(apply,2,4);root.Controls.Add(cancel,3,4);CancelButton=cancel;Controls.Add(root);
            enabled.CheckedChanged+=(s,e)=>{foreach(var c in coordinates)c.Enabled=enabled.Checked;};LoadRegion(initial??new TemplateRegion());enabled.Checked=initial!=null;foreach(var c in coordinates)c.Enabled=enabled.Checked;
        }
        private void LoadRegion(TemplateRegion r){double[] values={r.X1,r.Y1,r.X2,r.Y2};for(int i=0;i<4;i++)coordinates[i].Text=values[i].ToString("R",CultureInfo.CurrentCulture);}
        private TemplateRegion? ReadRegion()
        {
            if(!enabled.Checked)return null;var values=new double[4];for(int i=0;i<4;i++)if(!double.TryParse(coordinates[i].Text,NumberStyles.Float,CultureInfo.CurrentCulture,out values[i]))throw new ArgumentException("范围坐标必须为数字。");
            var r=new TemplateRegion{X1=values[0],Y1=values[1],X2=values[2],Y2=values[3]};TemplateCropGeometry.Validate(r);return r;
        }
    }
}
